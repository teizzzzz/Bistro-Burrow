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
        CustomerAgent[] _tables;                      // 每张桌的占用者
        Vector2[] _tablePos;
        Vector2[] _queuePos;

        int _dailyMax;
        int _spawnedToday;
        float _spawnInterval;
        float _spawnTimer;
        float _autoCookTimer;
        bool _open;

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
            // 只在白天且营业中模拟；黄昏/夜晚整体冻结（场景作为结算面板的背景板）
            if (_gm == null || _gm.Phase != GamePhase.Day || !_open) return;
            float dt = Time.deltaTime;

            SpawnTick(dt);
            SeatQueueFront();
            AutoCookTick(dt);
            _stove.Tick(dt);
            AgentsTick(dt);
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
            }
        }

        /// <summary>帮厨自动开火（雇佣 Cook 后的自动化体验，GDD：依赖排班达成自动化）。</summary>
        void AutoCookTick(float dt)
        {
            if (!_gm.State.HasStaffWithRole("Cook")) return;
            _autoCookTimer -= dt;
            if (_autoCookTimer > 0f) return;
            _autoCookTimer = 0.9f;

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
            string text = tip > 0 ? $"+{price} (小费+{tip})" : $"+{price}";
            FloatingText.Spawn(transform, agent.transform.position + Vector3.up * 1.8f,
                text, new Color(1f, 0.85f, 0.35f));
        }

        public void OnCustomerAngry(CustomerAgent agent)
        {
            FreeTableOf(agent);
            DayReport report = _gm.Report;
            if (report != null) report.angryLeft++;
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
            // ---- 店内（Inside View，相机 X=0）----
            SpriteFactory.NewSprite("InsideWall", transform,
                SpriteFactory.Rect(12.6f, 5.6f, new Color(0.15f, 0.17f, 0.23f), 0.1f),
                new Vector2(-0.4f, 1.0f), 0);
            SpriteFactory.NewSprite("InsideFloor", transform,
                SpriteFactory.Rect(13.4f, 1.5f, new Color(0.29f, 0.23f, 0.18f), 0.04f),
                new Vector2(0f, -2.35f), 1);

            // 餐桌
            _tablePos = new[]
            {
                new Vector2(-2.8f, -1.25f),
                new Vector2(-1.0f, -1.25f),
                new Vector2(0.8f, -1.25f),
                new Vector2(2.6f, -1.25f)
            };
            _tables = new CustomerAgent[_tablePos.Length];
            foreach (Vector2 p in _tablePos)
            {
                SpriteFactory.NewSprite("Table", transform,
                    SpriteFactory.Rect(0.95f, 0.5f, new Color(0.45f, 0.33f, 0.21f), 0.08f), p, 10);
                SpriteFactory.NewSprite("TableLeg", transform,
                    SpriteFactory.Rect(0.16f, 0.32f, new Color(0.33f, 0.24f, 0.15f), 0.03f),
                    p + new Vector2(0f, -0.38f), 9);
            }

            // 店门
            SpriteFactory.NewSprite("Door", transform,
                SpriteFactory.Rect(0.95f, 2.1f, new Color(0.36f, 0.26f, 0.17f), 0.1f),
                new Vector2(4.7f, -0.58f), 4);

            // ---- 店外（Outside View，相机 X=11.5）----
            SpriteFactory.NewSprite("OutsideSky", transform,
                SpriteFactory.Rect(14.5f, 6.2f, new Color(0.33f, 0.40f, 0.55f), 0.0f),
                new Vector2(12.4f, 1.0f), -5);
            SpriteFactory.NewSprite("OutsideStreet", transform,
                SpriteFactory.Rect(14.5f, 1.5f, new Color(0.24f, 0.26f, 0.23f), 0.04f),
                new Vector2(12.4f, -2.35f), 1);
            // 店铺外立面 + 招牌底
            SpriteFactory.NewSprite("Facade", transform,
                SpriteFactory.Rect(1.4f, 5.6f, new Color(0.20f, 0.16f, 0.13f), 0.05f),
                new Vector2(5.6f, 1.0f), 2);

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
                // 立柱 + 主体色块的极简装饰造型
                SpriteFactory.NewSprite($"DecorPole_{id}", transform,
                    SpriteFactory.Rect(0.12f, 1.0f, new Color(0.3f, 0.26f, 0.2f), 0.02f),
                    new Vector2(x, -1.1f), 6);
                SpriteFactory.NewSprite($"Decor_{id}", transform,
                    SpriteFactory.Rect(0.62f, 0.62f, c, 0.16f),
                    new Vector2(x, -0.35f), 7);
                x += 1.35f;
            }
        }
    }
}
