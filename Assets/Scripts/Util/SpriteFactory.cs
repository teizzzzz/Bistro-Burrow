using System.Collections.Generic;
using UnityEngine;

namespace BistroBurrow.Util
{
    /// <summary>
    /// 程序化贴图工厂：运行时生成纯色圆角矩形/圆形 Sprite 并缓存。
    /// MVP 阶段以零美术资产实现"微缩模型"色块风格（GDD §6.3 包体控制：
    /// 不引入任何 PNG，WebGL 首包体积趋近于纯代码）。
    /// 后续接入正式美术时，仅替换本工厂的产出即可。
    /// </summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new();
        const int PPU = 64; // 像素/世界单位：64 足够色块风格使用，纹理极小

        /// <summary>解析 #RRGGBB 配置色；解析失败返回品红色便于肉眼发现配置错误。</summary>
        public static Color ParseHex(string hex, float alpha = 1f)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                c.a = alpha;
                return c;
            }
            return Color.magenta;
        }

        /// <summary>圆角矩形 Sprite（worldW/H 为世界单位尺寸，corner 同为世界单位）。</summary>
        public static Sprite Rect(float worldW, float worldH, Color color, float corner = 0.06f)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(worldW * PPU));
            int h = Mathf.Max(4, Mathf.RoundToInt(worldH * PPU));
            int r = Mathf.Clamp(Mathf.RoundToInt(corner * PPU), 0, Mathf.Min(w, h) / 2);
            string key = $"R{w}x{h}r{r}_{ColorUtility.ToHtmlStringRGBA(color)}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[w * h];
            Color32 c32 = color;
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = InsideRoundedRect(x, y, w, h, r) ? c32 : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true); // makeNoLongerReadable：释放 CPU 侧拷贝，省 WebGL 内存

            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>圆形 Sprite。</summary>
        public static Sprite Circle(float worldDiameter, Color color)
        {
            int d = Mathf.Max(4, Mathf.RoundToInt(worldDiameter * PPU));
            string key = $"C{d}_{ColorUtility.ToHtmlStringRGBA(color)}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = new Texture2D(d, d, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[d * d];
            Color32 c32 = color;
            Color32 clear = new Color32(0, 0, 0, 0);
            float cx = (d - 1) * 0.5f;
            float radius = d * 0.5f - 0.5f;
            for (int y = 0; y < d; y++)
            {
                for (int x = 0; x < d; x++)
                {
                    float dx = x - cx, dy = y - cx;
                    pixels[y * d + x] = (dx * dx + dy * dy) <= radius * radius ? c32 : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), PPU);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>快捷创建带 SpriteRenderer 的显示节点。z 由 sortingOrder 表达，统一 z=0。</summary>
        public static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite,
            Vector2 localPos, int sortingOrder)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        static bool InsideRoundedRect(int x, int y, int w, int h, int r)
        {
            if (r <= 0) return true;
            int nx = Mathf.Min(x, w - 1 - x); // 到最近竖边的距离
            int ny = Mathf.Min(y, h - 1 - y); // 到最近横边的距离
            if (nx >= r || ny >= r) return true;
            float dx = r - nx, dy = r - ny;
            return dx * dx + dy * dy <= r * r;
        }

        // =====================================================================
        // 氛围扩展：渐变 / 辉光 / 阴影 —— 把"程序员色块"推向"微缩模型"质感的三件套
        // =====================================================================

        /// <summary>
        /// 垂直渐变圆角矩形（top→bottom）。用顶部受光、底部沉色的微差
        /// 替代纯平色块，是最便宜的"体积感"来源。
        /// </summary>
        public static Sprite GradientRect(float worldW, float worldH, Color top, Color bottom, float corner = 0.06f)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(worldW * PPU));
            int h = Mathf.Max(4, Mathf.RoundToInt(worldH * PPU));
            int r = Mathf.Clamp(Mathf.RoundToInt(corner * PPU), 0, Mathf.Min(w, h) / 2);
            string key = $"G{w}x{h}r{r}_{ColorUtility.ToHtmlStringRGBA(top)}_{ColorUtility.ToHtmlStringRGBA(bottom)}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[w * h];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
            {
                Color32 row = Color.Lerp(bottom, top, h <= 1 ? 1f : (float)y / (h - 1));
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = InsideRoundedRect(x, y, w, h, r) ? row : clear;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 径向辉光（中心实、边缘透明的软圆）。配低透明度叠在灯具/月亮/拾取物上，
        /// 是夜景氛围的核心元素。falloff 越大边缘衰减越急。
        /// </summary>
        public static Sprite RadialGlow(float worldDiameter, Color color, float falloff = 2.2f)
        {
            int d = Mathf.Max(8, Mathf.RoundToInt(worldDiameter * PPU));
            string key = $"L{d}_{ColorUtility.ToHtmlStringRGBA(color)}_{falloff:F1}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = new Texture2D(d, d, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[d * d];
            float c = (d - 1) * 0.5f;
            float maxR = d * 0.5f;
            for (int y = 0; y < d; y++)
            {
                for (int x = 0; x < d; x++)
                {
                    float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - dist), falloff);
                    pixels[y * d + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), PPU);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 椭圆软阴影（横向压扁的径向衰减）。垫在角色/家具脚下，
        /// 让物体"落在地上"而不是浮在背景前。
        /// </summary>
        public static Sprite SoftShadow(float worldW, float worldH, float alpha = 0.35f)
        {
            int w = Mathf.Max(8, Mathf.RoundToInt(worldW * PPU));
            int h = Mathf.Max(6, Mathf.RoundToInt(worldH * PPU));
            string key = $"S{w}x{h}_{alpha:F2}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[w * h];
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - cx) / (w * 0.5f), ny = (y - cy) / (h * 0.5f);
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - dist), 1.8f) * alpha;
                    pixels[y * w + x] = new Color(0f, 0f, 0f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
            Cache[key] = sprite;
            return sprite;
        }
    }
}
