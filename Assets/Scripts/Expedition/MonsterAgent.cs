using UnityEngine;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Expedition
{
    /// <summary>
    /// 魔物 AI：巡逻 → 仇恨追击 → 近身接触伤害（经 GDD §5.3 减伤公式结算）。
    /// 由 ExpeditionDirector 集中 Tick。死亡时通知导演生成食材掉落。
    /// </summary>
    public class MonsterAgent : MonoBehaviour
    {
        public MonsterDef Def { get; private set; }
        public bool IsDead => _hp <= 0;

        float _hp;
        float _spawnX;
        int _dir = 1;               // 巡逻朝向
        float _attackCooldown;
        float _flashTimer;          // 受击闪白
        SpriteRenderer _bodySr;
        Transform _hpFill;

        const float PatrolHalfRange = 2.2f;
        const float ContactRange = 0.78f;
        const float AttackInterval = 1.0f;

        public void Init(MonsterDef def, Vector2 pos)
        {
            Def = def;
            _hp = def != null ? def.maxHp : 1;
            _spawnX = pos.x;
            transform.position = pos;

            Color c = def != null ? SpriteFactory.ParseHex(def.colorHex) : Color.magenta;
            // 体型随血量上限略微放大，传达威胁度
            float size = def != null ? Mathf.Lerp(0.55f, 1.0f, Mathf.InverseLerp(10f, 70f, def.maxHp)) : 0.6f;
            _bodySr = SpriteFactory.NewSprite("Body", transform,
                SpriteFactory.Rect(size, size * 0.8f, c, 0.18f), new Vector2(0f, size * 0.4f), 22);
            SpriteFactory.NewSprite("Eye", transform,
                SpriteFactory.Circle(0.1f, new Color(0.1f, 0.08f, 0.08f)),
                new Vector2(size * 0.18f, size * 0.5f), 23);

            // 头顶血条
            var hpBg = SpriteFactory.NewSprite("HpBg", transform,
                SpriteFactory.Rect(0.6f, 0.08f, new Color(0f, 0f, 0f, 0.5f), 0.02f),
                new Vector2(0f, size * 0.95f), 30);
            var holder = new GameObject("HpPivot").transform;
            holder.SetParent(hpBg.transform, false);
            holder.localPosition = new Vector3(-0.3f, 0f, 0f);
            var fill = SpriteFactory.NewSprite("HpFill", holder,
                SpriteFactory.Rect(0.58f, 0.055f, new Color(0.85f, 0.3f, 0.3f), 0.02f),
                new Vector2(0.29f, 0f), 31);
            _hpFill = holder;
        }

        /// <summary>由导演驱动。玩家可能已昏厥（null），此时退回巡逻。</summary>
        public void Tick(float dt, ExpeditionPlayer player, float groundY)
        {
            if (Def == null || IsDead) return;

            _attackCooldown -= dt;
            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                if (_bodySr != null)
                    _bodySr.color = Color.Lerp(SpriteFactory.ParseHex(Def.colorHex), Color.white, _flashTimer / 0.15f);
            }

            float px = transform.position.x;
            bool chasing = false;
            if (player != null)
            {
                float dist = Mathf.Abs(player.transform.position.x - px);
                if (dist < Def.aggroRange)
                {
                    chasing = true;
                    _dir = player.transform.position.x > px ? 1 : -1;
                    // 近身攻击：伤害先过减伤公式（Def_buff 来自白天吃下的料理）
                    if (Vector2.Distance(player.transform.position, transform.position) < ContactRange)
                    {
                        if (_attackCooldown <= 0f)
                        {
                            _attackCooldown = AttackInterval;
                            float dmg = FormulaLib.ActualDamage(Def.damage, player.DefBuff);
                            player.TakeDamage(dmg, transform.position);
                        }
                        return; // 贴脸时不再前进，避免推挤
                    }
                }
            }

            float speed = chasing ? Def.moveSpeed : Def.moveSpeed * 0.55f;
            if (!chasing)
            {
                // 巡逻：在出生点附近折返
                if (px > _spawnX + PatrolHalfRange) _dir = -1;
                else if (px < _spawnX - PatrolHalfRange) _dir = 1;
            }
            transform.position = new Vector3(px + _dir * speed * dt, groundY, 0f);
        }

        /// <summary>受击。返回 true 表示这一击致死。</summary>
        public bool TakeHit(float damage, float fromX)
        {
            if (IsDead) return false;
            _hp -= Mathf.Max(0f, damage);
            _flashTimer = 0.15f;
            // 轻微击退（远离攻击者）
            float push = transform.position.x >= fromX ? 0.25f : -0.25f;
            transform.position += new Vector3(push, 0f, 0f);

            if (_hpFill != null && Def != null && Def.maxHp > 0)
                _hpFill.localScale = new Vector3(Mathf.Clamp01(_hp / Def.maxHp), 1f, 1f);

            return IsDead;
        }
    }
}
