using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Bistro;
using BistroBurrow.Util;

namespace BistroBurrow.UI
{
    /// <summary>
    /// 点选信息卡（基建式）：左下角常驻小卡，实时显示选中对象的状态。
    /// - 员工/老板：身份、职业、勤快/耐力、疲劳条、当前行为；
    /// - 顾客：点单、耐心条（绿→红）、当前行为。
    /// 由 BistroCameraController 创建/驱动，随白天场景销毁；每帧拉取目标最新值。
    /// </summary>
    public class InfoCardUi : MonoBehaviour
    {
        Text _title;
        Text _line2;
        Text _mood;
        Image _barFill;
        Text _barLabel;
        GameObject _card;

        StaffAgent _staff;
        CustomerAgent _customer;

        public static InfoCardUi Create()
        {
            var go = new GameObject("InfoCardUi", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45; // 顶栏/面板之下，场景之上
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var ui = go.AddComponent<InfoCardUi>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            _card = new GameObject("Card", typeof(RectTransform));
            _card.transform.SetParent(transform, false);
            var rt = (RectTransform)_card.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(14, 14);
            rt.sizeDelta = new Vector2(300, 112);

            var bg = _card.AddComponent<Image>();
            bg.sprite = SpriteFactory.Rect(3f, 1.12f, Color.white, 0.14f);
            bg.color = new Color(0.10f, 0.10f, 0.14f, 0.88f);

            _title = UiFactory.Label(_card.transform, "", 22, new Color(1f, 0.93f, 0.78f), TextAnchor.MiddleLeft);
            Place(_title, new Vector2(14, -8), new Vector2(272, 28));
            _title.fontStyle = FontStyle.Bold;

            _line2 = UiFactory.Label(_card.transform, "", 17, new Color(0.85f, 0.85f, 0.88f), TextAnchor.MiddleLeft);
            Place(_line2, new Vector2(14, -36), new Vector2(272, 22));

            _mood = UiFactory.Label(_card.transform, "", 16, new Color(0.62f, 0.78f, 0.95f), TextAnchor.MiddleLeft);
            Place(_mood, new Vector2(14, -58), new Vector2(272, 22));

            // 状态条（疲劳/耐心通用）：底 + 填充 + 标签
            _barLabel = UiFactory.Label(_card.transform, "", 14, new Color(0.7f, 0.7f, 0.72f), TextAnchor.MiddleLeft);
            Place(_barLabel, new Vector2(14, -84), new Vector2(64, 18));

            var barBg = new GameObject("BarBg", typeof(RectTransform));
            barBg.transform.SetParent(_card.transform, false);
            var bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.45f);
            var bgRt = (RectTransform)barBg.transform;
            bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0f, 1f);
            bgRt.anchoredPosition = new Vector2(80, -86);
            bgRt.sizeDelta = new Vector2(200, 12);

            var fill = new GameObject("BarFill", typeof(RectTransform));
            fill.transform.SetParent(barBg.transform, false);
            _barFill = fill.AddComponent<Image>();
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(200, 0);

            _card.SetActive(false);
        }

        static void Place(Text t, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public void ShowStaff(StaffAgent a)
        {
            _staff = a;
            _customer = null;
            _card.SetActive(true);
        }

        public void ShowCustomer(CustomerAgent c)
        {
            _customer = c;
            _staff = null;
            _card.SetActive(true);
        }

        public void Hide()
        {
            _staff = null;
            _customer = null;
            if (_card != null) _card.SetActive(false);
        }

        void Update()
        {
            if (_staff != null) RefreshStaff();
            else if (_customer != null) RefreshCustomer();
            else if (_card.activeSelf) _card.SetActive(false); // 目标被销毁（顾客离店等）
        }

        void RefreshStaff()
        {
            var def = _staff.Def;
            string who = _staff.IsBoss ? "老板" : (def.role == "Cook" ? "帮厨" : "采集员");
            _title.text = $"{def.shortName}  <color=#C8A86B><size=15>[{who}]</size></color>";
            _line2.text = $"勤快 {def.diligence} · 耐力 {def.stamina} · 日薪 {def.dailyWage} 金";
            _mood.text = _staff.MoodText();

            int fatigue = _staff.Stats != null ? _staff.Stats.fatigue : 0;
            int limit = Core.ConfigService.Balance.fatigueDispatchLimit;
            SetBar("疲劳", Mathf.Clamp01(fatigue / 100f),
                Color.Lerp(new Color(0.4f, 0.8f, 0.45f), new Color(0.88f, 0.3f, 0.25f),
                    Mathf.Clamp01((float)fatigue / Mathf.Max(1, limit))));
        }

        void RefreshCustomer()
        {
            _title.text = "顾客  <color=#C8A86B><size=15>[食客]</size></color>";
            _line2.text = _customer.Order != null
                ? $"点了：{_customer.Order.displayName}（{_customer.Order.price} 金）"
                : "还没点单";
            _mood.text = _customer.MoodText();

            if (_customer.State == CustomerAgent.Stage.WaitingFood && _customer.PatienceTotal > 0f)
            {
                float ratio = Mathf.Clamp01(_customer.PatienceRemain / _customer.PatienceTotal);
                SetBar("耐心", ratio,
                    Color.Lerp(new Color(0.85f, 0.25f, 0.2f), new Color(0.35f, 0.78f, 0.38f), ratio));
            }
            else
            {
                SetBar("", 0f, Color.clear);
            }
        }

        void SetBar(string label, float ratio, Color color)
        {
            _barLabel.text = label;
            _barFill.color = color;
            _barFill.transform.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f);
        }
    }
}
