using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.UI
{
    /// <summary>
    /// 创始伙伴定制面板（新开局流程）：
    /// 取名（输入框）→ 选职业（帮厨/采集员）→ 10 点属性自由分配（勤快/耐力）→ 选配色。
    /// 产出一份随档保存的自定义 StaffDef（id=custom_founder），开局即入职、零签约费。
    /// 工资随属性总点数浮动：吃得越多干得越贵。
    /// </summary>
    public class FounderPanel : MonoBehaviour
    {
        const int TotalPoints = 10;
        const int StatMax = 8;
        static readonly string[] ColorOptions = { "#F2B8C6", "#7B9CD9", "#8FBF6B", "#E8A33D" };
        static readonly string[] DefaultNames = { "阿布", "小米", "石头", "露娜" };

        GameManager _gm;
        int _slot;
        InputField _nameInput;
        string _role = "Cook";
        int _diligence = 5;
        int _stamina = 5;
        int _colorIdx;

        Text _statLine;
        Text _roleHint;
        Text _wageLine;
        Button _cookBtn;
        Button _gathererBtn;
        readonly Button[] _colorBtns = new Button[ColorOptions.Length];

        public static FounderPanel Create(Transform canvasParent, GameManager gm)
        {
            var go = new GameObject("FounderPanel", typeof(RectTransform));
            go.transform.SetParent(canvasParent, false);
            var p = go.AddComponent<FounderPanel>();
            p._gm = gm;
            p.BuildShell();
            go.SetActive(false);
            return p;
        }

        /// <summary>为指定空档位打开定制流程。</summary>
        public void Show(int slot)
        {
            _slot = slot;
            _role = "Cook";
            _diligence = 5;
            _stamina = 5;
            _colorIdx = 0;
            if (_nameInput != null)
                _nameInput.text = DefaultNames[Random.Range(0, DefaultNames.Length)];
            gameObject.SetActive(true);
            RefreshDynamic();
        }

        public void Hide() => gameObject.SetActive(false);

        // =====================================================================
        // 构建
        // =====================================================================

        void BuildShell()
        {
            UiFactory.FillParent((RectTransform)transform);
            RectTransform dim = UiFactory.Panel(transform, new Color(0f, 0f, 0f, 0.6f), "Dim");
            UiFactory.FillParent(dim);

            RectTransform window = UiFactory.Panel(transform, new Color(0.10f, 0.10f, 0.15f, 0.98f), "Window");
            UiFactory.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 560));

            Text title = UiFactory.Label(window, "创始伙伴 · 你的第一位员工", 26, new Color(0.95f, 0.90f, 0.78f), TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(600, 36));

            // —— 名字 ——
            Text nameLabel = UiFactory.Label(window, "名字", 19, new Color(0.8f, 0.8f, 0.78f), TextAnchor.MiddleRight);
            UiFactory.Place((RectTransform)nameLabel.transform, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-160, -92), new Vector2(120, 30));
            _nameInput = UiFactory.CreateInput(window, "阿布", 20, 8, "NameInput");
            UiFactory.Place((RectTransform)_nameInput.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-140, -92), new Vector2(280, 42));

            // —— 职业 ——
            Text roleLabel = UiFactory.Label(window, "职业", 19, new Color(0.8f, 0.8f, 0.78f), TextAnchor.MiddleRight);
            UiFactory.Place((RectTransform)roleLabel.transform, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-160, -150), new Vector2(120, 30));
            _cookBtn = UiFactory.TextButton(window, "帮厨", () => { _role = "Cook"; RefreshDynamic(); },
                new Color(0.8f, 0.55f, 0.25f), Color.white, 19);
            UiFactory.Place((RectTransform)_cookBtn.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-140, -150), new Vector2(132, 42));
            _gathererBtn = UiFactory.TextButton(window, "采集员", () => { _role = "Gatherer"; RefreshDynamic(); },
                new Color(0.3f, 0.38f, 0.52f), Color.white, 19);
            UiFactory.Place((RectTransform)_gathererBtn.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(8, -150), new Vector2(132, 42));
            _roleHint = UiFactory.Label(window, "", 15, new Color(0.66f, 0.66f, 0.68f), TextAnchor.MiddleLeft);
            UiFactory.Place((RectTransform)_roleHint.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-140, -186), new Vector2(440, 24));

            // —— 属性加点 ——
            BuildStatRow(window, "勤快", -232, () => _diligence, v => _diligence = v);
            BuildStatRow(window, "耐力", -286, () => _stamina, v => _stamina = v);
            _statLine = UiFactory.Label(window, "", 17, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)_statLine.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -332), new Vector2(560, 26));

            // —— 配色 ——
            Text colorLabel = UiFactory.Label(window, "配色", 19, new Color(0.8f, 0.8f, 0.78f), TextAnchor.MiddleRight);
            UiFactory.Place((RectTransform)colorLabel.transform, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-160, -380), new Vector2(120, 30));
            for (int i = 0; i < ColorOptions.Length; i++)
            {
                int idx = i; // 闭包独立捕获
                Button b = UiFactory.TextButton(window, "", () => { _colorIdx = idx; RefreshDynamic(); },
                    SpriteFactory.ParseHex(ColorOptions[i]), Color.white, 14);
                UiFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(-140 + i * 58, -380), new Vector2(46, 42));
                _colorBtns[i] = b;
            }

            _wageLine = UiFactory.Label(window, "", 15, new Color(0.66f, 0.66f, 0.68f), TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)_wageLine.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -424), new Vector2(560, 24));

            // —— 行动 ——
            Button startBtn = UiFactory.TextButton(window, "带上伙伴，开店！", Confirm,
                new Color(0.8f, 0.55f, 0.25f), Color.white, 21);
            UiFactory.Place((RectTransform)startBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-90, 22), new Vector2(280, 50));
            Button backBtn = UiFactory.TextButton(window, "返回", Hide,
                new Color(0.32f, 0.3f, 0.34f), Color.white, 19);
            UiFactory.Place((RectTransform)backBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150, 22), new Vector2(140, 50));
        }

        void BuildStatRow(RectTransform window, string label, float y,
            System.Func<int> getter, System.Action<int> setter)
        {
            Text l = UiFactory.Label(window, label, 19, new Color(0.8f, 0.8f, 0.78f), TextAnchor.MiddleRight);
            UiFactory.Place((RectTransform)l.transform, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-160, y), new Vector2(120, 30));

            Button minus = UiFactory.TextButton(window, "－", () =>
            {
                if (getter() > 0) { setter(getter() - 1); RefreshDynamic(); }
            }, new Color(0.3f, 0.3f, 0.36f), Color.white, 20);
            UiFactory.Place((RectTransform)minus.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-140, y), new Vector2(44, 40));

            // 数值文本挂在按钮之间，RefreshDynamic 里按名字找会很脆——用闭包持有
            Text value = UiFactory.Label(window, "5", 22, new Color(0.95f, 0.93f, 0.86f), TextAnchor.MiddleCenter, $"Stat_{label}");
            UiFactory.Place((RectTransform)value.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-90, y), new Vector2(70, 30));

            Button plus = UiFactory.TextButton(window, "＋", () =>
            {
                if (getter() < StatMax && PointsLeft() > 0) { setter(getter() + 1); RefreshDynamic(); }
            }, new Color(0.3f, 0.3f, 0.36f), Color.white, 20);
            UiFactory.Place((RectTransform)plus.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-14, y), new Vector2(44, 40));

            _statTexts.Add((label, value));
        }

        readonly System.Collections.Generic.List<(string label, Text text)> _statTexts = new();

        // =====================================================================
        // 状态
        // =====================================================================

        int PointsLeft() => TotalPoints - _diligence - _stamina;

        int Wage() => 6 + _diligence + _stamina; // 属性越高日薪越贵

        void RefreshDynamic()
        {
            foreach ((string label, Text text) in _statTexts)
            {
                if (text == null) continue;
                text.text = label == "勤快" ? _diligence.ToString() : _stamina.ToString();
            }
            if (_statLine != null) _statLine.text = $"剩余可分配点数：{PointsLeft()}";
            if (_roleHint != null)
                _roleHint.text = _role == "Cook"
                    ? "帮厨：白天自动开火做菜，勤快越高出餐越快"
                    : "采集员：夜晚可派遣采集，勤快加产量、耐力降疲劳";
            if (_wageLine != null) _wageLine.text = $"日薪：{Wage()} 金币（随属性点数浮动）　·　签约费：0（创始伙伴）";

            // 职业按钮高亮
            SetBtnColor(_cookBtn, _role == "Cook" ? new Color(0.8f, 0.55f, 0.25f) : new Color(0.3f, 0.32f, 0.4f));
            SetBtnColor(_gathererBtn, _role == "Gatherer" ? new Color(0.8f, 0.55f, 0.25f) : new Color(0.3f, 0.32f, 0.4f));
            // 配色按钮选中描边（用文字 √ 表示，避免额外描边构件）
            for (int i = 0; i < _colorBtns.Length; i++)
            {
                Text t = _colorBtns[i] != null ? _colorBtns[i].GetComponentInChildren<Text>() : null;
                if (t != null) t.text = i == _colorIdx ? "√" : "";
            }
        }

        static void SetBtnColor(Button b, Color c)
        {
            var img = b != null ? b.GetComponent<Image>() : null;
            if (img != null) img.color = c;
        }

        void Confirm()
        {
            string name = _nameInput != null ? _nameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                _gm.UI.Toast("给伙伴取个名字吧！");
                return;
            }

            var founder = new StaffDef
            {
                id = "custom_founder",
                displayName = $"{name}（创始伙伴）",
                shortName = name,
                role = _role,
                look = _role == "Cook" ? "chef" : "adventurer",
                dailyWage = Wage(),
                hireCost = 0,
                colorHex = ColorOptions[_colorIdx],
                diligence = _diligence,
                stamina = _stamina
            };

            SfxSynth.Play(SfxSynth.Id.Unlock, 0.5f);
            Hide();
            _gm.StartNewGame(_slot, founder);
        }
    }
}
