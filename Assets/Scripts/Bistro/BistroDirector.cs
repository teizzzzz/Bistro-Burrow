using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using BistroBurrow.Cooking;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 白天经营导演（GDD §3.1/§3.2 店内+店外双视窗）：
    /// 负责构建场景、生成与调度顾客、驱动灶台、处理点击交互与营业统计。
    /// 所有子物体都挂在本组件所在的根节点下 → 随子场景 UnloadSceneAsync 整树释放。
    /// 顾客/灶台不自带 Update，统一由本导演 Tick，黄昏可一键冻结。
    /// </summary>
    public class BistroDirector : MonoBehaviour
    {
        public const float GroundY = -1.6f;     // 角色脚底基准线
        public const float InsideCamX = 0f;     // 店内视窗相机位
        public const float OutsideCamX = 11.5f; // 店外视窗相机位

        public Vector2 ExitPos => new Vector2(18.5f, GroundY);
        public float DifficultyFactor { get; private set; } = 1f;
        public bool IsOutsideView { get; private set; }

        GameManager _gm;
        StoveStation _stove;

        readonly List<CustomerAgent> _agents = new(); // 场上全部顾客
        readonly List<CustomerAgent> _queue = new();  // 店外排队序列（子集）
        readonly List<StaffAgent> _staffAgents = new(); // 在岗员工实体
        CustomerAgent[] _tables;                      // 每张桌的占用者
        Vector2[] _tablePos;
        Vector2[] _queuePos;
        Vector2 _restSpot;                            // 休息沙发位置（疲劳采集员瘫倒点）

        int _dailyMax;
        int _spawnedToday;
        float _spawnInterval;
        float _spawnTimer;
        float _autoCookTimer;
        float _autoCookInterval = 0.9f; // 由 SpawnStaffAgents 按帮厨勤快属性计算
        bool _open;
        bool _stage3D; // 3D 实景舞台是否在场（决定相机透视模式）

        /// <summary>构建白天场景。必须在根节点被移入 BistroScene 之后调用。</summary>
        public void Init(GameManager gm)
        {
            _gm = gm;
            if (_gm == null)
            {
                Debug.LogError("[BistroDirector] GameManager 为空，无法开店。");
                return;
            }

            ShopLevelDef lvl = ConfigService.GetShopLevel(_gm.State.ShopLevel);
            _dailyMax = lvl != null ? lvl.maxCustomersPerDay : 15;
            DifficultyFactor = lvl != null ? lvl.difficultyFactor : 1f;
            int stoveSlots = lvl != null ? lvl.stoveSlots : 2;

            BuildScene();
            _stove = new StoveStation(transform, new Vector2(-5.2f, GroundY), stoveSlots);
            SpawnStaffAgents(); // 雇佣的员工实体到岗（依赖 _restSpot，须在 BuildScene 后）

            // 基建式相机操作：3D 舞台下透视+视差；任意模式下点选员工/镜头跟随
            gameObject.AddComponent<BistroCameraController>().Init(_gm, _stage3D);

            // 吸引力引擎（GDD §4.2）：店外装修决定客流刷新间隔
            BalanceDef bal = ConfigService.Balance;
            float attraction = _gm.State.CurrentAttraction();
            _spawnInterval = FormulaLib.CustomerSpawnInterval(
                bal.baseSpawnIntervalSeconds, attraction, bal.attractionSpawnDivisor);
            _spawnTimer = 1.5f; // 开门后第一位客人尽快出现，避免开局冷场
            _open = true;
        }

        void Update()
        {
            // 只在白天且营业中模拟；黄昏/夜晚整体冻结（场景作为结算面板的背景板）。
            // UIPaused：员工面板打开时同步冻结（顾客耐心/灶台/生成全停，玩家安心浏览）。
            // _gm.State 判空：编辑器 Play 中域重载会清空 GameManager 的运行时状态
            if (_gm == null || _gm.State == null || _gm.Phase != GamePhase.Day || !_open || _gm.UIPaused) return;
            float dt = Time.deltaTime;

            SpawnTick(dt);
            SeatQueueFront();
            AutoCookTick(dt);
            _stove.Tick(dt);
            AgentsTick(dt);
            StaffTick(dt);
            ClickTick();
        }

        /// <summary>黄昏打烊：停止生成与模拟，清场剩余顾客。</summary>
        public void FinishDay()
        {
            _open = false;
            if (_stove != null) _stove.CancelAll();
            foreach (CustomerAgent a in _agents)
            {
                if (a != null) Destroy(a.gameObject);
            }
            _agents.Clear();
            _queue.Clear();
            if (_tables != null)
                for (int i = 0; i < _tables.Length; i++) _tables[i] = null;
        }

        /// <summary>店内⇄店外视窗切换（GDD §3.2 水平平移视角）。</summary>
        public void ToggleView()
        {
            IsOutsideView = !IsOutsideView;
            _gm.CamRig.PanTo(IsOutsideView ? OutsideCamX : InsideCamX, 0f);
        }

        // =====================================================================
        // 顾客调度
        // =====================================================================

        void SpawnTick(float dt)
        {
            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;
            _spawnTimer = _spawnInterval;

            if (_spawnedToday >= _dailyMax) return;        // 当日客流上限（店铺评级）
            if (_queue.Count >= _queuePos.Length) return;  // 队伍排满，路人过门不入

            var go = new GameObject("Customer");
            go.transform.SetParent(transform, false);
            var agent = go.AddComponent<CustomerAgent>();
            agent.Init(this, new Vector2(18.5f, GroundY));
            _agents.Add(agent);
            _queue.Add(agent);
            agent.GoToQueueSpot(_queuePos[_queue.Count - 1]);
            _spawnedToday++;
        }

        /// <summary>队首顾客入座（有空桌即放行），其余顾客向前补位。</summary>
        void SeatQueueFront()
        {
            while (_queue.Count > 0)
            {
                int tableIdx = FindFreeTable();
                if (tableIdx < 0) return;
                CustomerAgent front = _queue[0];
                if (front == null) { _queue.RemoveAt(0); continue; }
                if (front.State != CustomerAgent.Stage.InQueue) return; // 还在走向队位，等它站定

                _queue.RemoveAt(0);
                _tables[tableIdx] = front;
                front.AssignSeat(tableIdx, new Vector2(_tablePos[tableIdx].x - 0.55f, GroundY));
                ReflowQueue();
            }
        }

        void ReflowQueue()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i] != null) _queue[i].GoToQueueSpot(_queuePos[i]);
            }
        }

        int FindFreeTable()
        {
            for (int i = 0; i < _tables.Length; i++)
                if (_tables[i] == null) return i;
            return -1;
        }

        void AgentsTick(float dt)
        {
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                CustomerAgent a = _agents[i];
                if (a == null) { _agents.RemoveAt(i); continue; }
                if (!a.Tick(dt))
                {
                    _agents.RemoveAt(i);
                    Destroy(a.gameObject);
                }
            }
        }

        /// <summary>驱动在岗员工实体（帮厨干活律动 / 采集员巡场与休息）。</summary>
        void StaffTick(float dt)
        {
            bool stoveBusy = _stove != null && _stove.JobCount > 0;
            foreach (StaffAgent s in _staffAgents)
            {
                if (s != null) s.Tick(dt, stoveBusy, _restSpot);
            }
        }

        /// <summary>把存档中的员工以实体形式摆进店里（含自定义创始伙伴）。</summary>
        void SpawnStaffAgents()
        {
            int gathererIdx = 0;
            float bestCookDiligence = 0f;
            foreach (StaffState st in _gm.State.Data.staff)
            {
                if (st == null) continue;
                StaffDef def = _gm.State.GetStaffDef(st.id); // 兼容配置员工与自定义员工
                if (def == null) continue;

                var go = new GameObject($"Staff_{def.id}");
                go.transform.SetParent(transform, false);
                var agent = go.AddComponent<StaffAgent>();
                // Cook 守灶台旁；Gatherer 在用餐区巡场（多名采集员错开起点）
                Vector2 home = def.role == "Cook"
                    ? new Vector2(-4.3f, GroundY)
                    : new Vector2(1.2f + gathererIdx++ * 0.8f, GroundY);
                agent.Init(this, def, st, home, _restSpot);
                _staffAgents.Add(agent);

                if (def.role == "Cook") bestCookDiligence = Mathf.Max(bestCookDiligence, def.diligence);
            }
            // 帮厨自动开火间隔：取最勤快帮厨的属性（员工属性效果）
            _autoCookInterval = FormulaLib.AutoCookInterval(
                ConfigService.Balance.autoCookBaseInterval, bestCookDiligence);
        }

        // =====================================================================
        // 点单 / 烹饪 / 结账
        // =====================================================================

        /// <summary>为顾客挑一道"已解锁且当前库存做得出"的菜；做不出任何菜返回 null。</summary>
        public RecipeDef PickOrderFor(CustomerAgent agent)
        {
            var candidates = new List<RecipeDef>();
            if (ConfigService.Recipes != null)
            {
                foreach (RecipeDef r in ConfigService.Recipes)
                {
                    if (r == null || !_gm.State.IsRecipeUnlocked(r.id)) continue;
                    if (RecipeSystem.TryPayIngredients(r, _gm.State, consume: false))
                        candidates.Add(r);
                }
            }
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>点击顾客气泡 → 扣料开火。</summary>
        void ClickTick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            // 防御：点在结算按钮等 UI 上时不穿透到世界
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Camera cam = _gm.CamRig != null ? _gm.CamRig.Cam : null;
            if (cam == null) return;
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);

            foreach (CustomerAgent a in _agents)
            {
                if (a == null || a.State != CustomerAgent.Stage.WaitingFood || a.CookStarted) continue;
                if (Vector2.Distance(world, a.BubbleWorldPos) < 0.95f)
                {
                    TryCookFor(a);
                    return;
                }
            }
        }

        void TryCookFor(CustomerAgent agent)
        {
            if (agent == null || agent.Order == null) return;
            if (!_stove.HasFreeSlot)
            {
                _gm.UI.Toast("灶台全满，先等等锅！");
                return;
            }
            // 下单时库存可能已被其他订单消耗，开火前必须复查并真实扣料
            if (!RecipeSystem.TryPayIngredients(agent.Order, _gm.State, consume: true))
            {
                _gm.UI.Toast($"食材不足，做不了「{agent.Order.displayName}」！");
                return;
            }
            if (_stove.TryStart(agent, agent.Order))
            {
                agent.CookStarted = true;
                SfxSynth.Play(SfxSynth.Id.Click, 0.4f); // 开火确认音
            }
        }

        /// <summary>帮厨自动开火（雇佣 Cook 后的自动化体验，GDD：依赖排班达成自动化）。</summary>
        void AutoCookTick(float dt)
        {
            if (!_gm.State.HasStaffWithRole("Cook")) return;
            _autoCookTimer -= dt;
            if (_autoCookTimer > 0f) return;
            _autoCookTimer = _autoCookInterval; // 勤快属性越高手速越快

            if (!_stove.HasFreeSlot) return;
            foreach (CustomerAgent a in _agents)
            {
                if (a != null && a.State == CustomerAgent.Stage.WaitingFood && !a.CookStarted)
                {
                    TryCookFor(a);
                    return; // 一次只开一锅，保持帮厨"手速"节奏
                }
            }
        }

        /// <summary>用餐完毕付款：菜价 + 快速服务小费（GDD §2.1 产出金币与好感）。</summary>
        public void OnCustomerPaid(CustomerAgent agent)
        {
            if (agent == null || agent.Order == null) return;
            FreeTableOf(agent);

            int price = agent.Order.price;
            // 上菜时剩余耐心超过 60% 视为"神速"，给小费
            bool fast = agent.PatienceTotal > 0f && agent.PatienceRemain / agent.PatienceTotal > 0.6f;
            int tip = fast ? Mathf.RoundToInt(price * ConfigService.Balance.tipFastServePercent / 100f) : 0;

            _gm.State.AddGold(price + tip);
            DayReport report = _gm.Report;
            if (report != null)
            {
                report.served++;
                report.revenue += price;
                report.tips += tip;
            }
            SfxSynth.Play(SfxSynth.Id.Coin, 0.5f);
            Particle.CoinBurst(transform, agent.transform.position + Vector3.up * 1.2f, tip > 0 ? 6 : 4);
            string text = tip > 0 ? $"+{price} (小费+{tip})" : $"+{price}";
            FloatingText.Spawn(transform, agent.transform.position + Vector3.up * 1.8f,
                text, new Color(1f, 0.85f, 0.35f));
        }

        public void OnCustomerAngry(CustomerAgent agent)
        {
            FreeTableOf(agent);
            DayReport report = _gm.Report;
            if (report != null) report.angryLeft++;
            SfxSynth.Play(SfxSynth.Id.Hurt, 0.25f);
            FloatingText.Spawn(transform, agent.transform.position + Vector3.up * 1.8f,
                "气走了！", new Color(0.95f, 0.45f, 0.4f));
        }

        public void OnCustomerNoDish(CustomerAgent agent)
        {
            FreeTableOf(agent);
            DayReport report = _gm.Report;
            if (report != null) report.noDishLeft++;
        }

        void FreeTableOf(CustomerAgent agent)
        {
            if (agent == null || _tables == null) return;
            int idx = agent.TableIndex;
            if (idx >= 0 && idx < _tables.Length && _tables[idx] == agent)
                _tables[idx] = null;
        }

        // =====================================================================
        // 场景搭建（全部程序化色块，零美术资产）
        // =====================================================================

        void BuildScene()
        {
            BuildInterior();
            BuildExterior();

            // 排队点（GDD §3.2 排队队列管理）
            _queuePos = new[]
            {
                new Vector2(6.6f, GroundY),
                new Vector2(7.5f, GroundY),
                new Vector2(8.4f, GroundY),
                new Vector2(9.3f, GroundY),
                new Vector2(10.2f, GroundY),
                new Vector2(11.1f, GroundY)
            };

            // 已购店外装饰（吸引力来源可视化）
            BuildOwnedDecor();
        }

        /// <summary>店内：暖色酒馆——墙裙、木地板、窗光、吊灯光池、陈设细节。</summary>
        void BuildInterior()
        {
            // 桌位/休憩点坐标是逻辑锚点，2D/3D 两套布景共用
            _restSpot = new Vector2(-6.5f, GroundY);
            _tablePos = new[]
            {
                new Vector2(-2.8f, -1.25f),
                new Vector2(-1.0f, -1.25f),
                new Vector2(0.8f, -1.25f),
                new Vector2(2.6f, -1.25f)
            };
            _tables = new CustomerAgent[_tablePos.Length];

            // Ark3D 资产在场 → 3D 实景舞台（餐厅 + 宿舍角）；缺资产走下方 2D 手绘
            _stage3D = Bistro3DStage.Build(transform, GroundY, _tablePos, _restSpot);
            if (_stage3D) return;

            // 背景墙：顶部受光的暖棕渐变 + 深色墙裙 + 黄铜色腰线
            SpriteFactory.NewSprite("Wall", transform,
                SpriteFactory.GradientRect(13.0f, 5.0f, new Color(0.33f, 0.26f, 0.22f), new Color(0.24f, 0.18f, 0.15f), 0.06f),
                new Vector2(-0.5f, 1.25f), 0);
            SpriteFactory.NewSprite("Wainscot", transform,
                SpriteFactory.GradientRect(13.0f, 1.15f, new Color(0.20f, 0.15f, 0.12f), new Color(0.16f, 0.12f, 0.09f), 0.03f),
                new Vector2(-0.5f, -1.02f), 1);
            SpriteFactory.NewSprite("Trim", transform,
                SpriteFactory.Rect(13.0f, 0.09f, new Color(0.72f, 0.58f, 0.36f), 0.02f),
                new Vector2(-0.5f, -0.42f), 2);
            // 天花板压顶
            SpriteFactory.NewSprite("Ceiling", transform,
                SpriteFactory.Rect(13.0f, 0.42f, new Color(0.14f, 0.10f, 0.08f), 0.04f),
                new Vector2(-0.5f, 3.55f), 2);

            // 地板：木色渐变 + 板缝线
            SpriteFactory.NewSprite("Floor", transform,
                SpriteFactory.GradientRect(13.4f, 1.55f, new Color(0.42f, 0.29f, 0.18f), new Color(0.27f, 0.18f, 0.11f), 0.03f),
                new Vector2(-0.3f, -2.33f), 1);
            for (float px = -6.4f; px < 6.4f; px += 0.92f)
            {
                SpriteFactory.NewSprite("Plank", transform,
                    SpriteFactory.Rect(0.05f, 1.45f, new Color(0f, 0f, 0f, 0.18f), 0.01f),
                    new Vector2(px, -2.33f), 2);
            }

            // 窗户：框 + 天光 + 漫射光斑（白天的氛围光源）
            SpriteFactory.NewSprite("WindowFrame", transform,
                SpriteFactory.Rect(1.6f, 1.8f, new Color(0.15f, 0.11f, 0.08f), 0.06f),
                new Vector2(-3.4f, 1.45f), 3);
            SpriteFactory.NewSprite("WindowSky", transform,
                SpriteFactory.GradientRect(1.36f, 1.56f, new Color(0.66f, 0.78f, 0.92f), new Color(0.85f, 0.90f, 0.97f), 0.04f),
                new Vector2(-3.4f, 1.45f), 4);
            SpriteFactory.NewSprite("WindowBar", transform,
                SpriteFactory.Rect(0.07f, 1.56f, new Color(0.15f, 0.11f, 0.08f), 0.01f),
                new Vector2(-3.4f, 1.45f), 5);
            SpriteFactory.NewSprite("WindowGlow", transform,
                SpriteFactory.RadialGlow(4.2f, new Color(1.0f, 0.95f, 0.80f, 0.16f)),
                new Vector2(-3.3f, 0.7f), 6);

            // 吊灯 ×3：灯线 + 灯罩 + 灯泡 + 暖光池（场景氛围的主角）
            float[] lampXs = { -4.6f, -0.2f, 3.0f };
            foreach (float lx in lampXs)
            {
                SpriteFactory.NewSprite("LampCord", transform,
                    SpriteFactory.Rect(0.045f, 0.85f, new Color(0.10f, 0.08f, 0.06f), 0.01f),
                    new Vector2(lx, 2.95f), 3);
                SpriteFactory.NewSprite("LampShade", transform,
                    SpriteFactory.GradientRect(0.62f, 0.34f, new Color(0.45f, 0.30f, 0.18f), new Color(0.30f, 0.20f, 0.12f), 0.1f),
                    new Vector2(lx, 2.42f), 4);
                SpriteFactory.NewSprite("LampBulb", transform,
                    SpriteFactory.Circle(0.18f, new Color(1.0f, 0.87f, 0.58f)),
                    new Vector2(lx, 2.22f), 5);
                SpriteFactory.NewSprite("LampGlow", transform,
                    SpriteFactory.RadialGlow(3.6f, new Color(1.0f, 0.78f, 0.43f, 0.30f)),
                    new Vector2(lx, 2.0f), 6);
            }

            // 墙面陈设：挂画与圆盘
            SpriteFactory.NewSprite("FrameA", transform,
                SpriteFactory.Rect(0.58f, 0.70f, new Color(0.42f, 0.31f, 0.19f), 0.04f),
                new Vector2(-1.8f, 1.7f), 3);
            SpriteFactory.NewSprite("FrameAInner", transform,
                SpriteFactory.GradientRect(0.44f, 0.54f, new Color(0.55f, 0.62f, 0.50f), new Color(0.36f, 0.42f, 0.34f), 0.02f),
                new Vector2(-1.8f, 1.7f), 4);
            SpriteFactory.NewSprite("PlateDecor", transform,
                SpriteFactory.Circle(0.5f, new Color(0.78f, 0.70f, 0.55f)),
                new Vector2(1.4f, 1.85f), 3);
            SpriteFactory.NewSprite("PlateDecorIn", transform,
                SpriteFactory.Circle(0.34f, new Color(0.52f, 0.40f, 0.28f)),
                new Vector2(1.4f, 1.85f), 4);

            // 菜单黑板（门边）：板面 + 三行"粉笔字"
            SpriteFactory.NewSprite("MenuBoard", transform,
                SpriteFactory.Rect(0.95f, 1.15f, new Color(0.12f, 0.10f, 0.09f), 0.05f),
                new Vector2(3.8f, 0.45f), 3);
            for (int i = 0; i < 3; i++)
            {
                SpriteFactory.NewSprite("Chalk", transform,
                    SpriteFactory.Rect(0.62f - i * 0.12f, 0.06f, new Color(0.92f, 0.90f, 0.82f, 0.55f), 0.01f),
                    new Vector2(3.74f, 0.78f - i * 0.3f), 4);
            }

            // 厨房后挡板 + 挂具（衬托灶台区域）
            SpriteFactory.NewSprite("KitchenSplash", transform,
                SpriteFactory.GradientRect(2.4f, 1.7f, new Color(0.19f, 0.14f, 0.11f), new Color(0.14f, 0.10f, 0.08f), 0.04f),
                new Vector2(-5.2f, 0.25f), 3);
            SpriteFactory.NewSprite("PanHang", transform,
                SpriteFactory.Circle(0.34f, new Color(0.30f, 0.30f, 0.34f)),
                new Vector2(-5.7f, 0.55f), 4);
            SpriteFactory.NewSprite("LadleHang", transform,
                SpriteFactory.Rect(0.08f, 0.5f, new Color(0.55f, 0.48f, 0.38f), 0.02f),
                new Vector2(-4.8f, 0.5f), 4);

            // 室内盆栽（门边点缀）
            BuildPottedPlant(new Vector2(4.25f, GroundY), 8);

            // 员工休憩沙发（GDD §3.1 休憩室的轻量化呈现）：厨房侧角落
            SpriteFactory.NewSprite("SofaShadow", transform,
                SpriteFactory.SoftShadow(1.3f, 0.3f), _restSpot + new Vector2(0f, -0.04f), 7);
            SpriteFactory.NewSprite("SofaBack", transform,
                SpriteFactory.GradientRect(1.15f, 0.7f, new Color(0.46f, 0.32f, 0.26f), new Color(0.36f, 0.24f, 0.19f), 0.12f),
                _restSpot + new Vector2(0f, 0.52f), 8);
            SpriteFactory.NewSprite("SofaSeat", transform,
                SpriteFactory.GradientRect(1.15f, 0.4f, new Color(0.60f, 0.44f, 0.34f), new Color(0.48f, 0.33f, 0.25f), 0.12f),
                _restSpot + new Vector2(0f, 0.24f), 9);
            SpriteFactory.NewSprite("SofaArmL", transform,
                SpriteFactory.Rect(0.18f, 0.5f, new Color(0.40f, 0.27f, 0.21f), 0.08f),
                _restSpot + new Vector2(-0.56f, 0.36f), 10);
            SpriteFactory.NewSprite("SofaArmR", transform,
                SpriteFactory.Rect(0.18f, 0.5f, new Color(0.40f, 0.27f, 0.21f), 0.08f),
                _restSpot + new Vector2(0.56f, 0.36f), 10);

            // 餐桌：桌布渐变 + 桌腿 + 落地阴影 + 凳子
            foreach (Vector2 p in _tablePos)
            {
                SpriteFactory.NewSprite("TableShadow", transform,
                    SpriteFactory.SoftShadow(1.3f, 0.36f), new Vector2(p.x, GroundY - 0.06f), 8);
                SpriteFactory.NewSprite("Table", transform,
                    SpriteFactory.GradientRect(0.98f, 0.5f, new Color(0.56f, 0.41f, 0.26f), new Color(0.42f, 0.29f, 0.17f), 0.09f),
                    p, 10);
                SpriteFactory.NewSprite("TableLeg", transform,
                    SpriteFactory.Rect(0.15f, 0.34f, new Color(0.28f, 0.20f, 0.13f), 0.03f),
                    p + new Vector2(0f, -0.4f), 9);
                // 客人对面的小凳子
                SpriteFactory.NewSprite("Stool", transform,
                    SpriteFactory.GradientRect(0.4f, 0.16f, new Color(0.48f, 0.35f, 0.22f), new Color(0.36f, 0.25f, 0.15f), 0.05f),
                    p + new Vector2(0.62f, -0.28f), 9);
                SpriteFactory.NewSprite("StoolLeg", transform,
                    SpriteFactory.Rect(0.1f, 0.28f, new Color(0.28f, 0.20f, 0.13f), 0.02f),
                    p + new Vector2(0.62f, -0.5f), 8);
            }

            // 店门（含门框与把手）
            SpriteFactory.NewSprite("DoorFrame", transform,
                SpriteFactory.Rect(1.12f, 2.3f, new Color(0.16f, 0.11f, 0.08f), 0.07f),
                new Vector2(4.7f, -0.52f), 3);
            SpriteFactory.NewSprite("Door", transform,
                SpriteFactory.GradientRect(0.92f, 2.1f, new Color(0.42f, 0.30f, 0.19f), new Color(0.31f, 0.21f, 0.13f), 0.08f),
                new Vector2(4.7f, -0.58f), 4);
            SpriteFactory.NewSprite("DoorKnob", transform,
                SpriteFactory.Circle(0.1f, new Color(0.78f, 0.66f, 0.40f)),
                new Vector2(4.4f, -0.62f), 5);
        }

        /// <summary>店外：日间街区——天空渐变、远景屋脊、雨棚招牌、街灯与路面细节。</summary>
        void BuildExterior()
        {
            // 天空：清晨蓝渐变 + 太阳光斑 + 两朵软云
            SpriteFactory.NewSprite("Sky", transform,
                SpriteFactory.GradientRect(15.5f, 6.6f, new Color(0.46f, 0.62f, 0.84f), new Color(0.78f, 0.86f, 0.94f), 0f),
                new Vector2(12.9f, 1.0f), -9);
            SpriteFactory.NewSprite("Sun", transform,
                SpriteFactory.RadialGlow(3.2f, new Color(1.0f, 0.96f, 0.82f, 0.55f)),
                new Vector2(17.0f, 3.3f), -8);
            var cloudA = SpriteFactory.NewSprite("CloudA", transform,
                SpriteFactory.RadialGlow(2.0f, new Color(1f, 1f, 1f, 0.5f), 1.6f),
                new Vector2(9.0f, 3.1f), -8);
            cloudA.transform.localScale = new Vector3(2.3f, 1f, 1f);
            var cloudB = SpriteFactory.NewSprite("CloudB", transform,
                SpriteFactory.RadialGlow(1.6f, new Color(1f, 1f, 1f, 0.42f), 1.6f),
                new Vector2(14.5f, 2.4f), -8);
            cloudB.transform.localScale = new Vector3(2.6f, 1f, 1f);

            // 远景屋脊剪影（低对比度，制造街区纵深）
            float[][] houses = { new[] { 8.2f, 1.9f, 1.1f }, new[] { 10.4f, 2.6f, 1.5f }, new[] { 13.6f, 2.1f, 1.2f }, new[] { 16.4f, 2.8f, 1.7f }, new[] { 19.0f, 2.0f, 1.3f } };
            foreach (float[] hse in houses)
            {
                SpriteFactory.NewSprite("FarHouse", transform,
                    SpriteFactory.GradientRect(hse[1], hse[2], new Color(0.52f, 0.60f, 0.74f), new Color(0.44f, 0.52f, 0.66f), 0.05f),
                    new Vector2(hse[0], -0.85f + hse[2] * 0.5f), -7);
            }

            // 街道：路面渐变 + 石板横线
            SpriteFactory.NewSprite("Street", transform,
                SpriteFactory.GradientRect(15.5f, 1.55f, new Color(0.40f, 0.38f, 0.33f), new Color(0.27f, 0.25f, 0.21f), 0.03f),
                new Vector2(12.9f, -2.33f), 1);
            for (float sx = 6.4f; sx < 20f; sx += 1.15f)
            {
                SpriteFactory.NewSprite("Cobble", transform,
                    SpriteFactory.Rect(0.55f, 0.05f, new Color(0f, 0f, 0f, 0.20f), 0.01f),
                    new Vector2(sx, -1.86f), 2);
            }

            // 店铺外立面 + 红色雨棚 + 悬挂招牌
            SpriteFactory.NewSprite("Facade", transform,
                SpriteFactory.GradientRect(1.5f, 5.7f, new Color(0.30f, 0.22f, 0.16f), new Color(0.21f, 0.15f, 0.11f), 0.04f),
                new Vector2(5.65f, 1.0f), 2);
            SpriteFactory.NewSprite("Awning", transform,
                SpriteFactory.GradientRect(2.3f, 0.4f, new Color(0.76f, 0.38f, 0.27f), new Color(0.62f, 0.28f, 0.20f), 0.1f),
                new Vector2(6.4f, 1.45f), 3);
            SpriteFactory.NewSprite("AwningPole", transform,
                SpriteFactory.Rect(0.07f, 0.9f, new Color(0.30f, 0.24f, 0.18f), 0.02f),
                new Vector2(7.4f, 0.95f), 2);
            // 招牌：吊杆 + 木牌 + 杯子图标 + 微光（指引玩家这是入口）
            SpriteFactory.NewSprite("SignArm", transform,
                SpriteFactory.Rect(0.7f, 0.07f, new Color(0.16f, 0.12f, 0.09f), 0.02f),
                new Vector2(6.65f, 0.65f), 3);
            SpriteFactory.NewSprite("SignBoard", transform,
                SpriteFactory.GradientRect(0.85f, 0.6f, new Color(0.50f, 0.36f, 0.22f), new Color(0.38f, 0.26f, 0.16f), 0.08f),
                new Vector2(6.95f, 0.25f), 4);
            SpriteFactory.NewSprite("SignMug", transform,
                SpriteFactory.Circle(0.3f, new Color(0.92f, 0.84f, 0.66f)),
                new Vector2(6.95f, 0.25f), 5);
            SpriteFactory.NewSprite("SignGlow", transform,
                SpriteFactory.RadialGlow(1.6f, new Color(1.0f, 0.85f, 0.5f, 0.22f)),
                new Vector2(6.95f, 0.25f), 5);

            // 街灯 ×2（白天熄灯，剪影即可）
            foreach (float lx in new[] { 9.6f, 15.2f })
            {
                SpriteFactory.NewSprite("StreetLampPole", transform,
                    SpriteFactory.Rect(0.1f, 2.6f, new Color(0.15f, 0.14f, 0.12f), 0.03f),
                    new Vector2(lx, -0.3f), 0);
                SpriteFactory.NewSprite("StreetLampHead", transform,
                    SpriteFactory.GradientRect(0.34f, 0.4f, new Color(0.95f, 0.88f, 0.66f), new Color(0.72f, 0.62f, 0.42f), 0.08f),
                    new Vector2(lx, 1.1f), 0);
            }

            // 排队等待区地贴（弱引导）
            for (int i = 0; i < 6; i++)
            {
                SpriteFactory.NewSprite("QueueMark", transform,
                    SpriteFactory.Rect(0.4f, 0.07f, new Color(0.85f, 0.78f, 0.55f, 0.25f), 0.02f),
                    new Vector2(6.6f + i * 0.9f, GroundY - 0.18f), 2);
            }
        }

        /// <summary>小盆栽：陶盆 + 三叶簇。室内角落与店外装饰共用。</summary>
        void BuildPottedPlant(Vector2 groundPos, int order)
        {
            SpriteFactory.NewSprite("PlantShadow", transform,
                SpriteFactory.SoftShadow(0.7f, 0.25f), groundPos + new Vector2(0f, -0.04f), order - 1);
            SpriteFactory.NewSprite("PlantPot", transform,
                SpriteFactory.GradientRect(0.42f, 0.34f, new Color(0.62f, 0.40f, 0.28f), new Color(0.46f, 0.28f, 0.19f), 0.07f),
                groundPos + new Vector2(0f, 0.17f), order);
            SpriteFactory.NewSprite("PlantLeafM", transform,
                SpriteFactory.Circle(0.4f, new Color(0.32f, 0.52f, 0.34f)),
                groundPos + new Vector2(0f, 0.55f), order + 1);
            SpriteFactory.NewSprite("PlantLeafL", transform,
                SpriteFactory.Circle(0.3f, new Color(0.27f, 0.45f, 0.29f)),
                groundPos + new Vector2(-0.16f, 0.44f), order);
            SpriteFactory.NewSprite("PlantLeafR", transform,
                SpriteFactory.Circle(0.3f, new Color(0.36f, 0.57f, 0.37f)),
                groundPos + new Vector2(0.16f, 0.46f), order);
        }

        void BuildOwnedDecor()
        {
            IReadOnlyList<string> owned = _gm.State.Data.ownedDecor;
            if (owned == null) return;
            float x = 12.2f;
            foreach (string id in owned)
            {
                DecorDef def = ConfigService.GetDecor(id);
                if (def == null) continue;
                Color c = SpriteFactory.ParseHex(def.colorHex);
                // 阴影 + 立柱 + 主体 + 同色微光：让装饰品像"摆出来的商品"
                SpriteFactory.NewSprite($"DecorShadow_{id}", transform,
                    SpriteFactory.SoftShadow(0.8f, 0.28f), new Vector2(x, GroundY - 0.05f), 5);
                SpriteFactory.NewSprite($"DecorPole_{id}", transform,
                    SpriteFactory.Rect(0.12f, 1.0f, new Color(0.3f, 0.26f, 0.2f), 0.02f),
                    new Vector2(x, -1.1f), 6);
                SpriteFactory.NewSprite($"Decor_{id}", transform,
                    SpriteFactory.GradientRect(0.62f, 0.62f, Color.Lerp(c, Color.white, 0.15f), c, 0.16f),
                    new Vector2(x, -0.35f), 7);
                SpriteFactory.NewSprite($"DecorGlow_{id}", transform,
                    SpriteFactory.RadialGlow(1.3f, new Color(c.r, c.g, c.b, 0.18f)),
                    new Vector2(x, -0.35f), 7);
                x += 1.35f;
            }
        }
    }
}
