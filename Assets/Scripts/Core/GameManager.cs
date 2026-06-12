using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BistroBurrow.Bistro;
using BistroBurrow.Expedition;
using BistroBurrow.UI;

namespace BistroBurrow.Core
{
    /// <summary>游戏日生命周期阶段（GDD §2.1）。</summary>
    public enum GamePhase
    {
        Boot,   // 启动加载
        Day,    // 08:00-18:00 白昼营业（BistroScene）
        Dusk,   // 18:00-19:00 黄昏结算（经营大盘 UI）
        Night,  // 19:00-08:00 黑夜探索（ExpeditionScene）或就寝跳过
        Dawn    // 黎明：派遣结算 + 存档 + 进入新的一天
    }

    /// <summary>
    /// 中央核心管理器（GDD §6.2 ManagerScene 常驻）：
    /// 持有全局状态（存档/时钟/相机/UI），驱动 日→昏→夜→晨 状态机，
    /// 并负责子场景（Bistro/Expedition）的动态叠加与卸载。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Boot;
        public PlayerState State { get; private set; }
        public GameClock Clock { get; private set; }
        public DayReport Report { get; private set; }
        public CameraRig CamRig { get; private set; }
        public UIRoot UI { get; private set; }

        Scene _bistroScene;
        Scene _expeditionScene;
        BistroDirector _bistro;
        ExpeditionDirector _expedition;

        /// <summary>幂等创建单例（Bootstrap 与 RuntimeInitialize 双入口都走这里）。</summary>
        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>(); // Awake 内完成初始化
        }

        void Awake()
        {
            // 单例防重：场景里手动摆放 + 自动引导同时发生时，后者自毁
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // WebGL：标签页失焦时浏览器会节流 rAF，但桌面端编辑器保持后台运行便于调试
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            ConfigService.LoadAll();
            State = SaveSystem.LoadOrNull() ?? PlayerState.CreateNew();
            Clock = new GameClock(ConfigService.Balance.gameMinutesPerRealSecond);
            CamRig = CameraRig.Create();
            UI = UIRoot.Create(this);
        }

        void Start()
        {
            // Awake 里若发生单例自毁，Start 不会再执行到这（对象已销毁）
            EnterDay();
        }

        void Update()
        {
            // 编辑器域重载防御：Play 中热重载会清空非序列化运行时状态（State/Clock 等），
            // 此时静默冻结，避免每帧 NRE 刷屏（正式构建不存在运行时域重载）
            if (State == null || Clock == null || UI == null) return;

            BalanceDef bal = ConfigService.Balance;
            if (bal == null) return;

            switch (Phase)
            {
                case GamePhase.Day:
                    Clock.Tick(Time.deltaTime);
                    UI.RefreshTopBar();
                    if (Clock.HourFloat >= bal.dayEndHour) EnterDusk();
                    break;

                case GamePhase.Night:
                    Clock.Tick(Time.deltaTime);
                    UI.RefreshTopBar();
                    // 跨日判断：19:00 起步，HourFloat 持续增长，24+8=32 即次日清晨
                    if (Clock.HourFloat >= 24f + bal.nightEndHour && _expedition != null)
                        _expedition.ForceReturn("天亮了，强制收队！");
                    break;
            }
        }

        // =====================================================================
        // 阶段流转
        // =====================================================================

        /// <summary>进入白天：叠加创建 BistroScene 并交由 BistroDirector 构建经营场景。</summary>
        public void EnterDay()
        {
            Phase = GamePhase.Day;
            Clock.SetHour(ConfigService.Balance.dayStartHour);
            Report = new DayReport();

            _bistroScene = SceneFlow.CreateAdditive("BistroScene");
            var root = new GameObject("BistroRoot");
            _bistro = root.AddComponent<BistroDirector>();
            SceneFlow.MoveToScene(root, _bistroScene);
            _bistro.Init(this); // Init 必须在 MoveToScene 之后：其内部生成的子物体都挂在 root 下

            CamRig.SetBackground(new Color(0.13f, 0.15f, 0.20f));
            CamRig.SnapTo(0f, 0f);
            UI.EnterDayMode();
            BistroBurrow.Util.SfxSynth.Play(BistroBurrow.Util.SfxSynth.Id.DayStart, 0.4f);
            UI.Toast($"第 {State.DayIndex} 天 · {ConfigService.GetShopLevel(State.ShopLevel)?.title ?? ""} 开始营业！");
        }

        /// <summary>顶栏按钮：白天 店内⇄店外 视窗切换（透传给当日导演）。</summary>
        public void ToggleBistroView()
        {
            if (Phase == GamePhase.Day && _bistro != null) _bistro.ToggleView();
        }

        /// <summary>黄昏：冻结营业，扣除工资，弹出经营大盘（结算面板）。</summary>
        void EnterDusk()
        {
            Phase = GamePhase.Dusk;
            Clock.SetHour(ConfigService.Balance.dayEndHour);

            if (_bistro != null) _bistro.FinishDay(); // 停止生成顾客、清场统计

            // 员工日薪在黄昏对账时扣除（GDD §2.1）
            Report.wages = State.TotalDailyWages();
            if (Report.wages > 0) State.AddGold(-Report.wages);

            UI.EnterDuskMode(Report);
        }

        /// <summary>结算面板点击「亲自探险」：卸载白天场景，进入黑夜探索。</summary>
        public void BeginNightExpedition()
        {
            if (Phase != GamePhase.Dusk) return; // 防御：重复点击/错误时序
            Phase = GamePhase.Night;
            Clock.SetHour(ConfigService.Balance.nightStartHour);

            UnloadBistro();

            _expeditionScene = SceneFlow.CreateAdditive("ExpeditionScene");
            var root = new GameObject("ExpeditionRoot");
            _expedition = root.AddComponent<ExpeditionDirector>();
            SceneFlow.MoveToScene(root, _expeditionScene);
            _expedition.Init(this);

            CamRig.SetBackground(new Color(0.06f, 0.07f, 0.11f));
            UI.EnterNightMode();
        }

        /// <summary>结算面板点击「派遣并就寝」：跳过亲自探险，直接入夜→黎明。</summary>
        public void SleepThrough()
        {
            if (Phase != GamePhase.Dusk) return;
            Phase = GamePhase.Night;
            UnloadBistro();
            UI.FadeTransition("夜深了……小馆打烊。", () => EnterDawn(null));
        }

        /// <summary>探险结束回调（回家 / 昏厥 / 天亮强制收队）。</summary>
        public void OnExpeditionFinished(ExpeditionResult result)
        {
            if (Phase != GamePhase.Night) return;

            string summary;
            if (result == null)
            {
                summary = "深夜无事发生。";
            }
            else if (result.passedOut)
            {
                // 昏厥惩罚：按 GDD 丢失部分战利品后由"好心人"抬回小馆
                int lost = ApplyLoot(result.loot, true);
                summary = $"你在地穴中昏了过去……被抬回小馆时丢失了 {lost} 件食材。";
            }
            else
            {
                ApplyLoot(result.loot, false);
                int total = CountLoot(result.loot);
                summary = total > 0 ? $"满载而归！带回 {total} 件食材。" : "空手而归，明天再战。";
            }

            if (_expedition != null) { _expedition = null; }
            SceneFlow.UnloadAsync(_expeditionScene);
            UI.FadeTransition(summary, () => EnterDawn(summary));
        }

        /// <summary>黎明：派遣结算、疲劳恢复、清空夜餐增益、自动存档、天数+1。</summary>
        void EnterDawn(string nightSummary)
        {
            Phase = GamePhase.Dawn;

            ResolveDispatchAndFatigue();
            State.ClearNightMeal();
            State.Data.dayIndex++;
            SaveSystem.Save(State); // WebGL：写入 IndexedDB，防浏览器关闭丢档

            EnterDay();
        }

        // =====================================================================
        // 内部工具
        // =====================================================================

        void UnloadBistro()
        {
            _bistro = null;
            SceneFlow.UnloadAsync(_bistroScene);
        }

        /// <summary>战利品入库；passOut=true 时按配置百分比丢失。返回丢失件数。</summary>
        int ApplyLoot(List<ItemStack> loot, bool passOut)
        {
            int lostTotal = 0;
            if (loot == null) return 0;
            int lossPercent = ConfigService.Balance.passOutLootLossPercent;
            foreach (ItemStack stack in loot)
            {
                if (stack == null || string.IsNullOrEmpty(stack.id) || stack.count <= 0) continue;
                int kept = passOut ? FormulaLib.LootKeptAfterPassOut(stack.count, lossPercent) : stack.count;
                lostTotal += stack.count - kept;
                if (kept > 0) State.AddIngredient(stack.id, kept);
            }
            return lostTotal;
        }

        static int CountLoot(List<ItemStack> loot)
        {
            int n = 0;
            if (loot == null) return 0;
            foreach (ItemStack s in loot) if (s != null) n += s.count;
            return n;
        }

        /// <summary>
        /// 派遣与疲劳结算（GDD §2.1 挂机派遣）：
        /// 被派遣的采集员带回随机基础食材并累积疲劳；留守员工恢复疲劳。
        /// </summary>
        void ResolveDispatchAndFatigue()
        {
            BalanceDef bal = ConfigService.Balance;
            var pool = bal.dispatchLootPool;
            foreach (StaffState s in State.Data.staff)
            {
                if (s == null) continue;
                StaffDef def = ConfigService.GetStaff(s.id);
                bool isGatherer = def != null && def.role == "Gatherer";

                if (s.dispatchTonight && isGatherer && pool != null && pool.Length > 0)
                {
                    int yield = Random.Range(bal.dispatchYieldMin, bal.dispatchYieldMax + 1);
                    for (int i = 0; i < yield; i++)
                    {
                        string id = pool[Random.Range(0, pool.Length)];
                        if (ConfigService.GetIngredient(id) != null) State.AddIngredient(id, 1);
                    }
                    s.fatigue = Mathf.Min(100, s.fatigue + bal.dispatchFatigueCost);
                    UI.Toast($"{def.displayName} 派遣归来，带回 {yield} 件食材（疲劳 {s.fatigue}）");
                }
                else
                {
                    s.fatigue = Mathf.Max(0, s.fatigue - bal.fatigueRecoverPerNight);
                }
                s.dispatchTonight = false;
            }
        }
    }
}
