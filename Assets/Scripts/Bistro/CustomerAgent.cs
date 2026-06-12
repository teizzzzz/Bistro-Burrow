using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 顾客行为状态机（GDD §3.1 点单气泡 + 耐心倒计时槽）：
    /// 进店排队 → 入座 → 点单 → 等待（耐心衰减）→ 用餐 → 付款 → 离店。
    /// 不自带 Update：统一由 BistroDirector.Tick 驱动，便于黄昏一键冻结。
    /// </summary>
    public class CustomerAgent : MonoBehaviour
    {
        public enum Stage
        {
            WalkToQueue,   // 走向队尾
            InQueue,       // 排队等空位（含队列前移）
            WalkToSeat,    // 入店走向餐桌
            Deciding,      // 看菜单（短暂停顿）
            WaitingFood,   // 已点单，等待烹饪+上菜（耐心倒计时）
            Eating,        // 用餐
            Leave,         // 满意离店（已付款）
            LeaveAngry,    // 耐心耗尽离店
            LeaveNoDish    // 库存做不出任何菜，遗憾离店
        }

        public Stage State { get; private set; } = Stage.WalkToQueue;
        public RecipeDef Order { get; private set; }
        public bool CookStarted { get; set; }           // 防止重复给同一人开火
        public float PatienceRemain { get; private set; }
        public float PatienceTotal { get; private set; }
        public int TableIndex { get; private set; } = -1;
        public Vector3 BubbleWorldPos => transform.position + new Vector3(0f, 2.05f, 0f);

        const float WalkSpeed = 2.4f;

        BistroDirector _director;
        Vector2 _target;
        float _decideTimer;
        float _eatTimer;
        Transform _body;          // 行走上下浮动用
        GameObject _bubble;       // 点单气泡（含菜名+耐心条）
        Text _bubbleText;
        Transform _patienceFill;  // 耐心条前景（scale.x = 比例）

        /// <summary>构建顾客外观并进入排队流程。</summary>
        public void Init(BistroDirector director, Vector2 spawnPos)
        {
            _director = director;
            transform.position = spawnPos;

            // 随机柔和配色的"色块小人"：身体圆角矩形 + 头部圆形
            Color tone = Color.HSVToRGB(Random.value, 0.45f, 0.85f);
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            SpriteFactory.NewSprite("Torso", _body, SpriteFactory.Rect(0.55f, 0.85f, tone, 0.16f), new Vector2(0f, 0.62f), 20);
            SpriteFactory.NewSprite("Head", _body, SpriteFactory.Circle(0.46f, Color.HSVToRGB(0.09f, 0.35f, 0.95f)), new Vector2(0f, 1.32f), 21);

            BuildBubble();
            SetBubbleVisible(false);
        }

        public void GoToQueueSpot(Vector2 spot)
        {
            _target = spot;
            if (State == Stage.InQueue) State = Stage.WalkToQueue; // 队列前移：重新走位
        }

        public void AssignSeat(int tableIndex, Vector2 seatPos)
        {
            TableIndex = tableIndex;
            _target = seatPos;
            State = Stage.WalkToSeat;
        }

        /// <summary>上菜成功 → 进入用餐。耐心计时随之停止。</summary>
        public void Serve()
        {
            if (State != Stage.WaitingFood) return; // 防御：客人已愤然离席时菜白做
            State = Stage.Eating;
            _eatTimer = ConfigService.Balance.customerEatSeconds;
            SetBubbleVisible(false);
        }

        /// <summary>由导演每帧驱动。返回 false 表示生命周期结束（可回收销毁）。</summary>
        public bool Tick(float dt)
        {
            switch (State)
            {
                case Stage.WalkToQueue:
                    if (MoveTowards(_target, dt)) State = Stage.InQueue;
                    break;

                case Stage.InQueue:
                    // 等待导演调度入座，原地小幅摇摆表示活着
                    break;

                case Stage.WalkToSeat:
                    if (MoveTowards(_target, dt))
                    {
                        State = Stage.Deciding;
                        _decideTimer = 0.6f;
                    }
                    break;

                case Stage.Deciding:
                    _decideTimer -= dt;
                    if (_decideTimer <= 0f) PlaceOrder();
                    break;

                case Stage.WaitingFood:
                    PatienceRemain -= dt;
                    UpdatePatienceBar();
                    if (PatienceRemain <= 0f)
                    {
                        // 耐心耗尽：愤然离店（GDD 难度因子的核心压力来源）
                        State = Stage.LeaveAngry;
                        SetBubbleVisible(false);
                        _director.OnCustomerAngry(this);
                        _target = _director.ExitPos;
                    }
                    break;

                case Stage.Eating:
                    _eatTimer -= dt;
                    if (_eatTimer <= 0f)
                    {
                        _director.OnCustomerPaid(this); // 付款 + 小费判定在导演侧
                        State = Stage.Leave;
                        _target = _director.ExitPos;
                    }
                    break;

                case Stage.Leave:
                case Stage.LeaveAngry:
                case Stage.LeaveNoDish:
                    if (MoveTowards(_target, dt)) return false; // 走出画面 → 销毁
                    break;
            }
            return true;
        }

        /// <summary>点单：从"已解锁且库存做得出"的菜里随机挑一道。</summary>
        void PlaceOrder()
        {
            Order = _director.PickOrderFor(this);
            if (Order == null)
            {
                // 库存做不出任何菜：遗憾离店（逼玩家晚上去采集，闭环压力点）
                State = Stage.LeaveNoDish;
                _bubbleText.text = "没想吃的……";
                SetBubbleVisible(true);
                _director.OnCustomerNoDish(this);
                _target = _director.ExitPos;
                return;
            }

            PatienceTotal = FormulaLib.PatienceSeconds(
                ConfigService.Balance.patienceBaseSeconds,
                _director.DifficultyFactor);
            PatienceRemain = PatienceTotal;

            _bubbleText.text = Order.displayName;
            SetBubbleVisible(true);
            State = Stage.WaitingFood;
        }

        bool MoveTowards(Vector2 target, float dt)
        {
            Vector3 pos = transform.position;
            Vector3 next = Vector3.MoveTowards(pos, new Vector3(target.x, target.y, pos.z), WalkSpeed * dt);
            transform.position = next;

            // 行走时身体轻微浮动，模拟步伐（替代骨骼动画的最低成本方案）
            bool arrived = Vector2.Distance(next, target) < 0.02f;
            if (_body != null)
                _body.localPosition = arrived ? Vector3.zero
                    : new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.07f, 0f);
            return arrived;
        }

        // ---------- 气泡 UI（世界空间） ----------

        void BuildBubble()
        {
            _bubble = new GameObject("Bubble");
            _bubble.transform.SetParent(transform, false);
            _bubble.transform.localPosition = new Vector3(0f, 2.05f, 0f);

            var canvas = _bubble.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 60;
            var rt = (RectTransform)_bubble.transform;
            rt.sizeDelta = new Vector2(190, 64);
            rt.localScale = Vector3.one * 0.012f;

            var bg = new GameObject("Bg");
            bg.transform.SetParent(_bubble.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.92f);
            bgImg.sprite = SpriteFactory.Rect(2.2f, 0.8f, Color.white, 0.18f);
            UiFactory.FillParent((RectTransform)bg.transform);

            _bubbleText = UiFactory.Label(_bubble.transform, "", 24, new Color(0.15f, 0.13f, 0.10f), TextAnchor.MiddleCenter);
            var textRt = (RectTransform)_bubbleText.transform;
            UiFactory.FillParent(textRt);
            textRt.offsetMin = new Vector2(6, 14);

            // 耐心倒计时槽：底条 + 按比例缩放的前景条
            var barBg = new GameObject("PatienceBg");
            barBg.transform.SetParent(_bubble.transform, false);
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0f, 0f, 0f, 0.25f);
            UiFactory.Place((RectTransform)barBg.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(160, 9));

            var barFill = new GameObject("PatienceFill");
            barFill.transform.SetParent(barBg.transform, false);
            var fillImg = barFill.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.78f, 0.38f);
            var fillRt = (RectTransform)barFill.transform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(160, 0);
            _patienceFill = fillRt;
        }

        void UpdatePatienceBar()
        {
            if (_patienceFill == null || PatienceTotal <= 0f) return;
            float ratio = Mathf.Clamp01(PatienceRemain / PatienceTotal);
            _patienceFill.localScale = new Vector3(ratio, 1f, 1f);
            var img = _patienceFill.GetComponent<Image>();
            if (img != null) // 绿→黄→红渐变提示紧迫感
                img.color = Color.Lerp(new Color(0.85f, 0.25f, 0.2f), new Color(0.35f, 0.78f, 0.38f), ratio);
        }

        void SetBubbleVisible(bool visible)
        {
            if (_bubble != null) _bubble.SetActive(visible);
        }
    }
}
