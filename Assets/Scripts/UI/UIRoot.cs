using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.UI
{
    /// <summary>
    /// 全局核心 Canvas（GDD §6.2 常驻 ManagerScene 的全局 UI）：
    /// 顶栏（天数/时钟/金币/阶段）、吐司提示、黑场转场、结算面板宿主。
    /// 生命周期与 GameManager 一致，跨子场景存活。
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        GameManager _gm;
        Text _topDay;
        Text _topClock;
        Text _topGold;
        Text _topPhase;
        Button _viewToggleBtn;
        Text _toastText;
        float _toastTimer;
        Image _fadeOverlay;
        Text _fadeMessage;
        bool _fading;
        SettlementPanel _settlement;

        public static UIRoot Create(GameManager gm)
        {
            var go = new GameObject("UIRoot");
            DontDestroyOnLoad(go);
            var ui = go.AddComponent<UIRoot>();
            ui._gm = gm;
            ui.Build();
            // 任意状态变更（买装饰/解锁菜谱/金币变动）即刷新顶栏
            if (gm != null && gm.State != null) gm.State.OnChanged += ui.RefreshTopBar;
            return ui;
        }

        void Build()
        {
            Canvas canvas = UiFactory.CreateScreenCanvas("GlobalCanvas", 100, transform);

            // ---- 顶栏 ----
            RectTransform bar = UiFactory.Panel(canvas.transform, new Color(0.06f, 0.06f, 0.1f, 0.82f), "TopBar");
            UiFactory.Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1280, 44));
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.offsetMin = new Vector2(0f, -44f);
            bar.offsetMax = new Vector2(0f, 0f);

            _topDay = UiFactory.Label(bar, "", 19, new Color(0.95f, 0.9f, 0.8f));
            UiFactory.Place((RectTransform)_topDay.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16, 0), new Vector2(170, 30));

            _topClock = UiFactory.Label(bar, "", 19, new Color(0.8f, 0.88f, 1f));
            UiFactory.Place((RectTransform)_topClock.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(196, 0), new Vector2(90, 30));

            _topPhase = UiFactory.Label(bar, "", 19, new Color(0.75f, 0.95f, 0.8f));
            UiFactory.Place((RectTransform)_topPhase.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(296, 0), new Vector2(170, 30));

            _topGold = UiFactory.Label(bar, "", 19, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleRight);
            UiFactory.Place((RectTransform)_topGold.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150, 0), new Vector2(220, 30));

            _viewToggleBtn = UiFactory.TextButton(bar, "店内/店外", () =>
            {
                if (_gm != null) _gm.ToggleBistroView();
            }, new Color(0.25f, 0.3f, 0.42f), Color.white, 16);
            UiFactory.Place((RectTransform)_viewToggleBtn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12, 0), new Vector2(124, 32));

            // ---- 吐司 ----
            _toastText = UiFactory.Label(canvas.transform, "", 21, new Color(1f, 0.97f, 0.85f), TextAnchor.MiddleCenter, "Toast");
            UiFactory.Place((RectTransform)_toastText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(900, 30));

            // ---- 结算面板（常驻构建，按需显隐） ----
            // 显式带 RectTransform：纯逻辑组件不会自动转换，后续 FillParent 强转会炸
            var settlementGo = new GameObject("SettlementPanel", typeof(RectTransform));
            settlementGo.transform.SetParent(canvas.transform, false);
            _settlement = settlementGo.AddComponent<SettlementPanel>();
            _settlement.BuildShell(_gm);
            settlementGo.SetActive(false);

            // ---- 黑场转场（最上层，激活时拦截点击） ----
            Canvas fadeCanvas = UiFactory.CreateScreenCanvas("FadeCanvas", 200, transform);
            RectTransform fadeRt = UiFactory.Panel(fadeCanvas.transform, new Color(0f, 0f, 0f, 0f), "Fade");
            _fadeOverlay = fadeRt.GetComponent<Image>();
            _fadeOverlay.raycastTarget = false;
            _fadeMessage = UiFactory.Label(fadeRt, "", 26, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleCenter);
            UiFactory.FillParent((RectTransform)_fadeMessage.transform);

            RefreshTopBar();
        }

        // =====================================================================
        // 阶段 UI 模式
        // =====================================================================

        public void EnterDayMode()
        {
            if (_settlement != null) _settlement.gameObject.SetActive(false);
            if (_viewToggleBtn != null) _viewToggleBtn.gameObject.SetActive(true);
            RefreshTopBar();
        }

        public void EnterDuskMode(DayReport report)
        {
            if (_viewToggleBtn != null) _viewToggleBtn.gameObject.SetActive(false);
            if (_settlement != null)
            {
                _settlement.gameObject.SetActive(true);
                _settlement.Show(report);
            }
            RefreshTopBar();
        }

        public void EnterNightMode()
        {
            if (_settlement != null) _settlement.gameObject.SetActive(false);
            if (_viewToggleBtn != null) _viewToggleBtn.gameObject.SetActive(false);
            RefreshTopBar();
        }

        // =====================================================================
        // 顶栏 / 吐司 / 转场
        // =====================================================================

        public void RefreshTopBar()
        {
            if (_gm == null) return;
            if (_topDay != null) _topDay.text = $"第 {_gm.State.DayIndex} 天";
            if (_topClock != null && _gm.Clock != null) _topClock.text = _gm.Clock.TimeText;
            if (_topGold != null) _topGold.text = $"金币 {_gm.State.Gold}";
            if (_topPhase != null) _topPhase.text = PhaseLabel(_gm.Phase);
        }

        static string PhaseLabel(GamePhase phase)
        {
            // 刻意不用 emoji：子集字体/WebGL 下 emoji 字形大概率缺失
            switch (phase)
            {
                case GamePhase.Day: return "营业中";
                case GamePhase.Dusk: return "黄昏结算";
                case GamePhase.Night: return "地穴探索";
                case GamePhase.Dawn: return "黎明";
                default: return "加载中";
            }
        }

        /// <summary>顶部居中吐司，2.4 秒后淡出。后到的消息直接覆盖。</summary>
        public void Toast(string message)
        {
            if (_toastText == null) return;
            _toastText.text = message ?? "";
            Color c = _toastText.color; c.a = 1f;
            _toastText.color = c;
            _toastTimer = 2.4f;
        }

        void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer < 0.6f && _toastText != null)
                {
                    Color c = _toastText.color;
                    c.a = Mathf.Clamp01(_toastTimer / 0.6f);
                    _toastText.color = c;
                }
            }
        }

        /// <summary>
        /// 黑场转场：盖黑 → 执行 onCovered（在全黑时切换场景，掩盖加载）→ 文案停留 → 揭开。
        /// </summary>
        public void FadeTransition(string message, System.Action onCovered)
        {
            if (_fading)
            {
                // 防御：转场期间的重复请求直接执行回调，避免吞掉阶段切换
                onCovered?.Invoke();
                return;
            }
            StartCoroutine(FadeRoutine(message, onCovered));
        }

        IEnumerator FadeRoutine(string message, System.Action onCovered)
        {
            _fading = true;
            _fadeOverlay.raycastTarget = true;
            if (_fadeMessage != null) _fadeMessage.text = "";

            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                SetFade(t / 0.45f);
                yield return null;
            }
            SetFade(1f);
            if (_fadeMessage != null) _fadeMessage.text = message ?? "";

            onCovered?.Invoke(); // 全黑期间完成场景切换

            yield return new WaitForSeconds(1.4f);
            if (_fadeMessage != null) _fadeMessage.text = "";
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                SetFade(1f - t / 0.45f);
                yield return null;
            }
            SetFade(0f);
            _fadeOverlay.raycastTarget = false;
            _fading = false;
        }

        void SetFade(float alpha)
        {
            if (_fadeOverlay == null) return;
            Color c = _fadeOverlay.color;
            c.a = Mathf.Clamp01(alpha);
            _fadeOverlay.color = c;
        }
    }
}
