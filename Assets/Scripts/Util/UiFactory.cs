using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BistroBurrow.Util
{
    /// <summary>
    /// UGUI 代码化构建工具集。整套 UI 不依赖任何 Prefab/TMP 资产，
    /// 保证仓库零二进制、WebGL 包体最小化。
    /// 中文字体策略（WebGL 兼容性关键点）：
    /// 1. 优先加载 Resources/Fonts/NotoSansSC-Sub（若仓库内提供了子集字体）；
    /// 2. 编辑器/桌面端回退到操作系统中文字体（WebGL 沙盒无系统字体，跳过）；
    /// 3. 最终回退内置 LegacyRuntime.ttf（无 CJK 字形，构建 WebGL 前请补字体，见 README）。
    /// </summary>
    public static class UiFactory
    {
        static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;

                // ① 仓库自带的 CJK 子集字体（推荐，WebGL 可用）
                _font = Resources.Load<Font>("Fonts/NotoSansSC-Sub");
                if (_font != null) return _font;

#if !UNITY_WEBGL || UNITY_EDITOR
                // ② 桌面端/编辑器：借用操作系统中文字体（WebGL 不支持，编译期裁剪）
                string[] osFonts = { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "WenQuanYi Micro Hei", "SimHei" };
                foreach (string name in osFonts)
                {
                    try
                    {
                        Font f = Font.CreateDynamicFontFromOSFont(name, 16);
                        if (f != null) { _font = f; return _font; }
                    }
                    catch { /* 该字体不存在，继续尝试下一个 */ }
                }
#endif
                // ③ 内置兜底字体（仅拉丁字形）
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>屏幕空间 Canvas（含 CanvasScaler，1280x720 基准、按高度适配）。</summary>
        public static Canvas CreateScreenCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f; // 按高度适配，横版游戏左右富余
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>确保场景中存在 EventSystem（UGUI 按钮交互的前提），全局只建一个。</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>(); // 旧输入系统（项目未启用 InputSystem 包）
            Object.DontDestroyOnLoad(go);
        }

        /// <summary>满父容器的色块面板。</summary>
        public static RectTransform Panel(Transform parent, Color color, string name = "Panel")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = (RectTransform)go.transform;
            FillParent(rt);
            return rt;
        }

        /// <summary>文本标签。UGUI 经典 Text（刻意不用 TMP：避免必须导入 TMP 资源包）。</summary>
        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, string name = "Label")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text ?? "";
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; // 文本不挡点击
            return t;
        }

        /// <summary>文字按钮（色块底 + Label）。尺寸交由 LayoutGroup/调用方控制。</summary>
        public static Button TextButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick,
            Color bg, Color fg, int fontSize = 18)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            // 按下反馈：颜色微变（默认 ColorTint 过淡，略加强）
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f * bg.r, 1.1f * bg.g, 1.1f * bg.b, 1f);
            colors.pressedColor = new Color(0.8f * bg.r, 0.8f * bg.g, 0.8f * bg.b, 1f);
            btn.colors = colors;

            Text t = Label(go.transform, label, fontSize, fg, TextAnchor.MiddleCenter, "Text");
            FillParent((RectTransform)t.transform);
            return btn;
        }

        /// <summary>纵向自动布局容器（结算面板列表用）。</summary>
        public static RectTransform VerticalGroup(Transform parent, float spacing, RectOffset padding, string name = "VGroup")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        /// <summary>横向自动布局容器（按钮行/标签行）。</summary>
        public static RectTransform HorizontalGroup(Transform parent, float spacing, string name = "HGroup")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        /// <summary>给布局元素指定固定尺寸。</summary>
        public static void SetLayoutSize(Component c, float width, float height)
        {
            if (c == null) return;
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width > 0) { le.preferredWidth = width; le.minWidth = width; }
            if (height > 0) { le.preferredHeight = height; le.minHeight = height; }
        }

        /// <summary>RectTransform 撑满父级。</summary>
        public static void FillParent(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>按锚点+偏移摆放（屏幕 HUD 元素用）。</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }
    }
}
