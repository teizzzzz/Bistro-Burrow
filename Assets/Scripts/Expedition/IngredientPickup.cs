using UnityEngine;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Expedition
{
    /// <summary>
    /// 地上的可拾取食材（魔物掉落 / 前景可采集植物，GDD §3.3 前景采集层）。
    /// 由 ExpeditionDirector 集中 Tick；接触玩家自动拾取（受背包负重上限制约）。
    /// </summary>
    public class IngredientPickup : MonoBehaviour
    {
        public string IngredientId { get; private set; }

        float _bobSeed;

        public void Init(string ingredientId, Vector2 pos)
        {
            IngredientId = ingredientId;
            transform.position = pos;
            _bobSeed = Random.value * 10f;

            IngredientDef def = ConfigService.GetIngredient(ingredientId);
            Color c = def != null ? SpriteFactory.ParseHex(def.colorHex) : Color.magenta;
            SpriteFactory.NewSprite("Icon", transform, SpriteFactory.Circle(0.34f, c), Vector2.zero, 25);
            // 微弱光环提升夜间可读性
            SpriteFactory.NewSprite("Glow", transform,
                SpriteFactory.Circle(0.5f, new Color(c.r, c.g, c.b, 0.25f)), Vector2.zero, 24);
        }

        /// <summary>悬浮动画 + 拾取判定。返回 true 表示已被拾取（待销毁）。</summary>
        public bool Tick(float dt, ExpeditionPlayer player)
        {
            // 上下漂浮
            float bob = Mathf.Sin(Time.time * 3f + _bobSeed) * 0.06f;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, p.y + bob * dt * 6f, p.z);

            if (player == null) return false;
            if (Vector2.Distance(player.transform.position, transform.position) < 0.75f)
            {
                return player.TryPickup(IngredientId, transform.position);
            }
            return false;
        }
    }
}
