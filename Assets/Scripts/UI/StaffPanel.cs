using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.UI
{
    /// <summary>
    /// 白天员工管理面板（顶栏「员工」按钮打开）：
    /// 招聘 / 今晚派遣 / 疲劳一览。打开期间 GameManager.UIPaused = true，
    /// 时钟与店内模拟全部冻结——玩家可以安心做人事决策，不会被偷偷气走顾客。
    /// </summary>
    public class StaffPanel : MonoBehaviour
    {
        GameManager _gm;
        Text _titleGold;
        RectTransform _content;

        public static StaffPanel Create(Transform canvasParent, GameManager gm)
        {
            var go = new GameObject("StaffPanel", typeof(RectTransform));
            go.transform.SetParent(canvasParent, false);
            var panel = go.AddComponent<StaffPanel>();
            panel._gm = gm;
            panel.BuildShell();
            go.SetActive(false);
            return panel;
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Show()
        {
            gameObject.SetActive(true);
            if (_gm != null) _gm.UIPaused = true; // 冻结时钟+店内模拟
            Rebuild();
        }

        public void Hide()
        {
            if (_gm != null) _gm.UIPaused = false;
            gameObject.SetActive(false);
        }

        void BuildShell()
        {
            UiFactory.FillParent((RectTransform)transform);
            RectTransform dim = UiFactory.Panel(transform, new Color(0f, 0f, 0f, 0.5f), "Dim");
            UiFactory.FillParent(dim);
            // 点遮罩空白处也能关面板（快捷直觉操作）
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Hide);

            RectTransform window = UiFactory.Panel(transform, new Color(0.10f, 0.10f, 0.15f, 0.97f), "Window");
            UiFactory.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(940, 480));

            Text title = UiFactory.Label(window, "员工管理 · 招聘与派遣（营业暂停中）", 24,
                new Color(0.93f, 0.91f, 0.85f));
            UiFactory.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26, -32), new Vector2(560, 34));

            _titleGold = UiFactory.Label(window, "", 21, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleRight);
            UiFactory.Place((RectTransform)_titleGold.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26, -32), new Vector2(240, 34));

            _content = UiFactory.Panel(window, new Color(1f, 1f, 1f, 0.03f), "Content");
            UiFactory.Place(_content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(892, 330));
            _content.pivot = new Vector2(0.5f, 1f);
            _content.gameObject.AddComponent<RectMask2D>();

            Button close = UiFactory.TextButton(window, "关闭（继续营业）", Hide,
                new Color(0.3f, 0.38f, 0.52f), Color.white, 18);
            UiFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(240, 46));
        }

        void Rebuild()
        {
            if (_gm == null || _gm.State == null || _content == null) return;
            if (_titleGold != null) _titleGold.text = $"金币 {_gm.State.Gold}";

            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            RectTransform list = UiFactory.VerticalGroup(_content, 6f, new RectOffset(18, 18, 12, 12), "List");
            UiFactory.FillParent(list);
            StaffRosterUi.BuildInto(list, _gm, Rebuild);
        }
    }
}
