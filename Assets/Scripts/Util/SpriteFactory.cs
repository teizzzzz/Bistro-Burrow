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
    }
}
