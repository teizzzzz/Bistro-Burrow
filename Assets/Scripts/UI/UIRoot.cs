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
        RectTransform _topBar;
        Text _topDay;
        Text _topClock;
        Text _topGold;
        Text _topPhase;
        Button _viewToggleBtn;
        Button _staffBtn;
        Text _toastText;
        float _toastTimer;
        Image _fadeOverlay;
        Text _fadeMessage;
        bool _fading;
        SettlementPanel _settlement;
        StaffPanel _staffPanel;
        FounderPanel _founderPanel;
        GameObject _menu;
        RectTransform _slotList;          // 三档位卡片容器（每次显示菜单时重建）
        int _pendingDeleteSlot = -1;      // 删除二次确认中的档位（-1=无）

        public static UIRoot Create(GameManager gm)
        {
            var go = new GameObject("UIRoot");
            DontDestroyOnLoad(go);
            var ui = go.AddComponent<UIRoot>();
            ui._gm = gm;
            ui.Build();
            return ui;
        }

        // =====================================================================
        // 存档状态绑定（State 在主菜单选择后才存在，因此动态绑定/解绑）
        // =====================================================================

        public void BindState(PlayerState state)
        {
            if (state != null) state.OnChanged += RefreshTopBar;
            RefreshTopBar();
        }

        public void UnbindState(PlayerState state)
        {
            if (state != null) state.OnChanged -= RefreshTopBar;
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
            UiFactory.Place((RectTransform)_topGold.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-232, 0), new Vector2(220, 30));

            _viewToggleBtn = UiFactory.TextButton(bar, "店内/店外", () =>
            {
                if (_gm != null) _gm.ToggleBistroView();
            }, new Color(0.25f, 0.3f, 0.42f), Color.white, 16);
            UiFactory.Place((RectTransform)_viewToggleBtn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12, 0), new Vector2(124, 32));

            _staffBtn = UiFactory.TextButton(bar, "员工", () =>
            {
                if (_gm != null) _gm.ToggleStaffPanel();
            }, new Color(0.42f, 0.32f, 0.2f), Color.white, 16);
            UiFactory.Place((RectTransform)_staffBtn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-144, 0), new Vector2(76, 32));
            _topBar = bar;

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

            // ---- 白天员工管理面板 ----
            _staffPanel = StaffPanel.Create(canvas.transform, _gm);

            // ---- 主菜单（sort 150：盖住游戏 UI，低于黑场转场） ----
            BuildMainMenu();

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
        // 主菜单
        // =====================================================================

        void BuildMainMenu()
        {
            Canvas menuCanvas = UiFactory.CreateScreenCanvas("MenuCanvas", 150, transform);
            _menu = menuCanvas.gameObject;

            // 背景：夜空→暖炉色的纵向渐变 + 三处灯光氛围
            RectTransform bg = UiFactory.Panel(menuCanvas.transform, Color.white, "Bg");
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = SpriteFactory.GradientRect(16f, 9f,
                new Color(0.07f, 0.08f, 0.14f), new Color(0.22f, 0.15f, 0.11f), 0f);
            UiFactory.FillParent(bg);

            PlaceGlow(menuCanvas.transform, new Vector2(-380, 60), 520, new Color(1f, 0.72f, 0.38f, 0.20f));
            PlaceGlow(menuCanvas.transform, new Vector2(330, -140), 420, new Color(1f, 0.62f, 0.30f, 0.16f));
            PlaceGlow(menuCanvas.transform, new Vector2(120, 220), 360, new Color(0.75f, 0.85f, 1f, 0.10f));

            Text title = UiFactory.Label(_menu.transform, "Bistro & Burrow", 64,
                new Color(0.96f, 0.90f, 0.76f), TextAnchor.MiddleCenter, "Title");
            UiFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(900, 80));

            Text subtitle = UiFactory.Label(_menu.transform, "小馆与地穴 —— 白天喂饱冒险者，夜晚猎取魔物食材", 24,
                new Color(0.8f, 0.76f, 0.68f), TextAnchor.MiddleCenter, "Subtitle");
            UiFactory.Place((RectTransform)subtitle.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -222), new Vector2(900, 36));

            // 三档位卡片容器（内容在 RebuildSlotList 动态生成）
            var listGo = new GameObject("SlotList", typeof(RectTransform));
            listGo.transform.SetParent(_menu.transform, false);
            _slotList = (RectTransform)listGo.transform;
            UiFactory.Place(_slotList, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(760, 280));

            Text hint = UiFactory.Label(_menu.transform,
                "白天：点顾客气泡开火做菜 · 顶栏「员工」招聘派遣 ｜ 夜晚：A/D 移动 · 空格跳 · J 攻击 · 门口按 E 回家", 15,
                new Color(0.6f, 0.6f, 0.62f), TextAnchor.MiddleCenter, "Hint");
            UiFactory.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(1100, 24));

            // 创始伙伴定制面板：挂在菜单 Canvas 内（仅菜单流程使用，随菜单显隐）
            _founderPanel = FounderPanel.Create(_menu.transform, _gm);

            _menu.SetActive(false);
        }

        /// <summary>重建三张存档卡片（进入菜单与每次档位变更后调用）。</summary>
        void RebuildSlotList()
        {
            if (_slotList == null) return;
            for (int i = _slotList.childCount - 1; i >= 0; i--)
                Destroy(_slotList.GetChild(i).gameObject);

            for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
            {
                int s = slot; // 闭包独立捕获
                RectTransform card = UiFactory.Panel(_slotList, new Color(1f, 1f, 1f, 0.06f), $"Slot{slot}");
                UiFactory.Place(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -slot * 92), new Vector2(760, 84));
                card.pivot = new Vector2(0.5f, 1f);

                SaveData save = SaveSystem.Peek(slot);
                string title;
                if (save != null)
                {
                    ShopLevelDef lvl = ConfigService.GetShopLevel(save.shopLevel);
                    title = $"存档 {slot + 1}　第 {save.dayIndex} 天 · {lvl?.title ?? "?"} · 金币 {save.gold} · 菜谱 {save.unlockedRecipes?.Count ?? 0} · 员工 {save.staff?.Count ?? 0}";
                }
                else
                {
                    title = $"存档 {slot + 1}　—— 空档位，等待一段新的小馆物语";
                }
                Text label = UiFactory.Label(card, title, 18,
                    save != null ? new Color(0.93f, 0.90f, 0.82f) : new Color(0.6f, 0.6f, 0.62f));
                UiFactory.Place((RectTransform)label.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20, 0), new Vector2(470, 60));

                if (save != null)
                {
                    Button cont = UiFactory.TextButton(card, "继续营业", () =>
                    {
                        SfxSynth.Play(SfxSynth.Id.DayStart, 0.45f);
                        _gm?.StartContinueGame(s);
                    }, new Color(0.8f, 0.55f, 0.25f), Color.white, 18);
                    UiFactory.Place((RectTransform)cont.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-130, 0), new Vector2(132, 48));

                    bool confirming = _pendingDeleteSlot == s;
                    Button del = UiFactory.TextButton(card, confirming ? "确认删除？" : "删除", () =>
                    {
                        if (_pendingDeleteSlot == s)
                        {
                            SaveSystem.Wipe(s);
                            _pendingDeleteSlot = -1;
                            Toast($"存档 {s + 1} 已删除。");
                        }
                        else
                        {
                            _pendingDeleteSlot = s; // 二次确认，点其他地方自动还原
                        }
                        RebuildSlotList();
                    }, confirming ? new Color(0.72f, 0.3f, 0.26f) : new Color(0.32f, 0.3f, 0.34f),
                       new Color(0.9f, 0.88f, 0.84f), 16);
                    UiFactory.Place((RectTransform)del.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16, 0), new Vector2(104, 48));
                }
                else
                {
                    Button create = UiFactory.TextButton(card, "新开局", () =>
                    {
                        SfxSynth.Play(SfxSynth.Id.Click, 0.4f);
                        _pendingDeleteSlot = -1;
                        if (_founderPanel != null) _founderPanel.Show(s); // → 创始伙伴定制
                    }, new Color(0.3f, 0.38f, 0.52f), Color.white, 18);
                    UiFactory.Place((RectTransform)create.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16, 0), new Vector2(246, 48));
                }
            }
        }

        void PlaceGlow(Transform parent, Vector2 pos, float size, Color color)
        {
            var go = new GameObject("Glow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.RadialGlow(4f, color);
            img.raycastTarget = false;
            UiFactory.Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        }

        public void ShowMainMenu()
        {
            // 收起一切游戏内 UI
            if (_settlement != null) _settlement.gameObject.SetActive(false);
            if (_staffPanel != null && _staffPanel.IsOpen) _staffPanel.Hide();
            if (_founderPanel != null) _founderPanel.Hide();
            if (_topBar != null) _topBar.gameObject.SetActive(false);

            _pendingDeleteSlot = -1;
            RebuildSlotList();
            if (_menu != null) _menu.SetActive(true);
        }

        public void HideMainMenu()
        {
            if (_founderPanel != null) _founderPanel.Hide();
            if (_menu != null) _menu.SetActive(false);
            if (_topBar != null) _topBar.gameObject.SetActive(true);
        }

        // =====================================================================
        // 阶段 UI 模式
        // =====================================================================

        public void EnterDayMode()
        {
            if (_settlement != null) _settlement.gameObject.SetActive(false);
            if (_viewToggleBtn != null) _viewToggleBtn.gameObject.SetActive(true);
            if (_staffBtn != null) _staffBtn.gameObject.SetActive(true);
            RefreshTopBar();
        }

        public void EnterDuskMode(DayReport report)
        {
            if (_viewToggleBtn != null) _viewToggleBtn.gameObject.SetActive(false);
            if (_staffBtn != null) _staffBtn.gameObject.SetActive(false);
            if (_staffPanel != null && _staffPanel.IsOpen) _staffPanel.Hide();
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
            if (_staffBtn != null) _staffBtn.gameObject.SetActive(false);
            if (_staffPanel != null && _staffPanel.IsOpen) _staffPanel.Hide();
            RefreshTopBar();
        }

        // =====================================================================
        // 员工面板
        // =====================================================================

        public void ToggleStaffPanel()
        {
            if (_staffPanel == null) return;
            if (_staffPanel.IsOpen) _staffPanel.Hide();
            else _staffPanel.Show();
        }

        public void CloseStaffPanel()
        {
            if (_staffPanel != null && _staffPanel.IsOpen) _staffPanel.Hide();
        }

        // =====================================================================
        // 顶栏 / 吐司 / 转场
        // =====================================================================

        public void RefreshTopBar()
        {
            // State 在主菜单阶段为 null（尚未读档/开新档）
            if (_gm == null || _gm.State == null) return;
            if (_topDay != null) _topDay.text = $"第 {_gm.State.DayIndex} 天";
            if (_topClock != null && _gm.Clock != null) _topClock.text = _gm.Clock.TimeText;
            if (_topGold != null) _topGold.text = $"金币 {_gm.State.Gold}";
            if (_topPhase != null)
                _topPhase.text = _gm.UIPaused && _gm.Phase == GamePhase.Day ? "已暂停" : PhaseLabel(_gm.Phase);
        }

        static string PhaseLabel(GamePhase phase)
        {
            // 刻意不用 emoji：子集字体/WebGL 下 emoji 字形大概率缺失
            switch (phase)
            {
                case GamePhase.Menu: return "主菜单";
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
