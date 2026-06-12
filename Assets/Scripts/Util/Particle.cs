using UnityEngine;

namespace BistroBurrow.Util
{
    /// <summary>
    /// 微型自治粒子（蒸汽/金币/击杀碎屑）。
    /// 刻意不用 Unity ParticleSystem：场上同时存在的粒子不过几十个，
    /// 一个自驱 SpriteRenderer 更轻、零序列化资产、WebGL 零额外模块。
    /// </summary>
    public class Particle : MonoBehaviour
    {
        Vector2 _vel;
        float _gravity;
        float _life;
        float _age;
        float _startScale;
        float _endScale;
        SpriteRenderer _sr;
        float _baseAlpha;

        /// <summary>
        /// 生成一枚粒子。gravity 为向下加速度（负值=上飘加速，蒸汽用）。
        /// 生命末段 45% 自动淡出。parent 传场景根，随子场景卸载整树销毁。
        /// </summary>
        public static void Spawn(Transform parent, Sprite sprite, Vector2 pos, Vector2 vel,
            float gravity, float life, float startScale, float endScale, int sortingOrder)
        {
            var go = new GameObject("Particle");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var p = go.AddComponent<Particle>();
            p._vel = vel;
            p._gravity = gravity;
            p._life = Mathf.Max(0.05f, life);
            p._startScale = startScale;
            p._endScale = endScale;
            p._sr = go.AddComponent<SpriteRenderer>();
            p._sr.sprite = sprite;
            p._sr.sortingOrder = sortingOrder;
            p._baseAlpha = p._sr.color.a;
            go.transform.localScale = Vector3.one * startScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= _life)
            {
                Destroy(gameObject);
                return;
            }

            _vel.y -= _gravity * dt;
            transform.position += (Vector3)(_vel * dt);

            float k = _age / _life;
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, k);

            if (_sr != null && k > 0.55f)
            {
                Color c = _sr.color;
                c.a = _baseAlpha * (1f - (k - 0.55f) / 0.45f);
                _sr.color = c;
            }
        }

        // ---------- 常用配方（调用方一行出效果） ----------

        /// <summary>灶台蒸汽：白色软团上飘、放大、消散。</summary>
        public static void Steam(Transform parent, Vector2 pos)
        {
            Spawn(parent,
                SpriteFactory.RadialGlow(0.34f, new Color(0.95f, 0.95f, 0.98f, 0.45f), 1.6f),
                pos + new Vector2(Random.Range(-0.08f, 0.08f), 0f),
                new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0.5f, 0.7f)),
                -0.25f, // 负重力：越飘越快，像热气
                Random.Range(0.7f, 1.0f), 0.7f, 1.6f, 35);
        }

        /// <summary>金币喷溅：抛物线四散的小金点（收款反馈）。</summary>
        public static void CoinBurst(Transform parent, Vector2 pos, int count = 4)
        {
            for (int i = 0; i < count; i++)
            {
                Spawn(parent,
                    SpriteFactory.Circle(0.11f, new Color(1f, 0.84f, 0.35f)),
                    pos,
                    new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(2.0f, 3.2f)),
                    7f, Random.Range(0.5f, 0.75f), 1f, 0.55f, 48);
            }
        }

        /// <summary>击杀爆裂：按魔物颜色四散的碎屑。</summary>
        public static void Burst(Transform parent, Vector2 pos, Color color, int count = 6)
        {
            for (int i = 0; i < count; i++)
            {
                Spawn(parent,
                    SpriteFactory.Circle(Random.Range(0.09f, 0.16f), color),
                    pos,
                    new Vector2(Random.Range(-2.2f, 2.2f), Random.Range(0.8f, 3.0f)),
                    8f, Random.Range(0.4f, 0.7f), 1f, 0.4f, 40);
            }
        }
    }
}
