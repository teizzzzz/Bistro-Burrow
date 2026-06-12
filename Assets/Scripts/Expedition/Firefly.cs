using UnityEngine;
using BistroBurrow.Util;

namespace BistroBurrow.Expedition
{
    /// <summary>
    /// 萤火虫：围绕锚点缓慢漂移 + 呼吸式明暗。纯视觉、自驱动，
    /// 是夜林氛围最便宜的"活物感"来源（十几只仅十几个 SpriteRenderer）。
    /// </summary>
    public class Firefly : MonoBehaviour
    {
        Vector2 _anchor;
        float _seed;
        SpriteRenderer _glow;
        Color _baseColor;

        public static Firefly Spawn(Transform parent, Vector2 anchor)
        {
            var go = new GameObject("Firefly");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<Firefly>();
            f._anchor = anchor;
            f._seed = Random.value * 100f;
            f._baseColor = new Color(0.85f, 1.0f, 0.55f, 0.85f);
            f._glow = SpriteFactory.NewSprite("Glow", go.transform,
                SpriteFactory.RadialGlow(0.35f, f._baseColor, 1.8f), Vector2.zero, 15);
            go.transform.position = anchor;
            return f;
        }

        void Update()
        {
            float t = Time.time;
            // 双频正弦合成的"无规律"漂移轨迹
            float x = _anchor.x + Mathf.Sin(t * 0.5f + _seed) * 0.8f + Mathf.Sin(t * 1.3f + _seed * 2f) * 0.25f;
            float y = _anchor.y + Mathf.Sin(t * 0.7f + _seed * 3f) * 0.45f + Mathf.Sin(t * 1.9f + _seed) * 0.12f;
            transform.position = new Vector3(x, y, 0f);

            if (_glow != null)
            {
                // 呼吸明暗：偶尔几乎熄灭，更像真萤火虫
                float pulse = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 1.6f + _seed * 5f), 2f);
                Color c = _baseColor;
                c.a = _baseColor.a * pulse;
                _glow.color = c;
            }
        }
    }
}
