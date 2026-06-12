using System.Collections.Generic;
using UnityEngine;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Expedition
{
    /// <summary>
    /// 夜晚探索的主厨操控（GDD §2.1 亲自带队 / §5.3 生存数值）：
    /// A/D 或 ←→ 移动，空格跳跃，J 或 鼠标左键 攻击，E 在家门口收队。
    /// 不用物理引擎：平地 + 手写重力，省去 WebGL 端 Physics2D 的开销与不确定性。
    /// 饱食度按 GDD 公式持续衰减——背包越满扣得越快；归零后改扣血直至昏厥。
    /// </summary>
    public class ExpeditionPlayer : MonoBehaviour
    {
        public float DefBuff { get; private set; }
        public float Satiety { get; private set; }
        public float SatietyMax { get; private set; }
        public float Hp { get; private set; }
        public float HpMax { get; private set; }
        public float CarryWeight { get; private set; }
        public float CarryMax { get; private set; }

        ExpeditionDirector _director;
        BalanceDef _bal;
        readonly Dictionary<string, int> _loot = new();

        float _groundY;
        float _vy;
        bool _grounded = true;
        int _facing = 1;
        float _attackCooldown;
        float _hurtFlash;
        SpriteRenderer _bodySr;
        readonly Color _bodyColor = new Color(0.92f, 0.86f, 0.74f);

        public void Init(ExpeditionDirector director, float groundY)
        {
            _director = director;
            _bal = ConfigService.Balance;
            _groundY = groundY;

            PlayerState st = GameManager.Instance != null ? GameManager.Instance.State : null;
            DefBuff = st != null ? st.NightDefBuff : 0;
            SatietyMax = _bal.satietyMax + (st != null ? st.NightSatietyBuff : 0);
            Satiety = SatietyMax;
            HpMax = _bal.playerMaxHp;
            Hp = HpMax;
            CarryMax = _bal.maxCarryWeight;

            transform.position = new Vector3(0f, groundY, 0f);

            SpriteFactory.NewSprite("Shadow", transform,
                SpriteFactory.SoftShadow(0.78f, 0.34f), new Vector2(0f, 0.02f), 25);

            // 主厨造型：围裙色身体 + 厨师帽
            _bodySr = SpriteFactory.NewSprite("Body", transform,
                SpriteFactory.Rect(0.55f, 0.9f, _bodyColor, 0.15f), new Vector2(0f, 0.62f), 26);
            SpriteFactory.NewSprite("Apron", transform,
                SpriteFactory.Rect(0.4f, 0.45f, new Color(0.75f, 0.3f, 0.28f), 0.1f), new Vector2(0f, 0.48f), 27);
            SpriteFactory.NewSprite("Head", transform,
                SpriteFactory.Circle(0.46f, new Color(0.96f, 0.84f, 0.72f)), new Vector2(0f, 1.34f), 27);
            SpriteFactory.NewSprite("Hat", transform,
                SpriteFactory.Rect(0.5f, 0.3f, Color.white, 0.1f), new Vector2(0f, 1.65f), 28);
        }

        /// <summary>由导演每帧驱动。返回 false 表示昏厥（探险被迫结束）。</summary>
        public bool Tick(float dt)
        {
            if (_bal == null) return true;

            // ---- 移动（旧输入系统：WebGL 默认兼容） ----
            float h = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(h) > 0.01f)
            {
                _facing = h > 0 ? 1 : -1;
                float nx = transform.position.x + h * _bal.playerMoveSpeed * dt;
                nx = Mathf.Clamp(nx, _director.MinX, _director.MaxX);
                transform.position = new Vector3(nx, transform.position.y, 0f);
            }

            // ---- 跳跃 + 手写重力 ----
            if (_grounded && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)))
            {
                _vy = _bal.playerJumpSpeed;
                _grounded = false;
            }
            if (!_grounded)
            {
                _vy -= _bal.gravity * dt;
                float ny = transform.position.y + _vy * dt;
                if (ny <= _groundY) { ny = _groundY; _vy = 0f; _grounded = true; }
                transform.position = new Vector3(transform.position.x, ny, 0f);
            }

            // ---- 攻击 ----
            _attackCooldown -= dt;
            if (_attackCooldown <= 0f && (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0)))
            {
                _attackCooldown = _bal.playerAttackCooldown;
                Attack();
            }

            // ---- 饱食度衰减（GDD §5.3：S_decay = S_base + (W/W_max)×1.5） ----
            float decay = FormulaLib.SatietyDecayPerSecond(
                _bal.satietyBaseDecayPerSecond, CarryWeight, CarryMax, _bal.satietyLoadDecayFactor);
            Satiety = Mathf.Max(0f, Satiety - decay * dt);
            if (Satiety <= 0f)
            {
                // 饥饿状态：持续掉血，逼迫玩家"贪与撤"的博弈
                Hp -= _bal.starveHpLossPerSecond * dt;
            }

            // 受击红闪恢复（tint 是乘法，基准必须是白色，否则会越闪越暗）
            if (_hurtFlash > 0f)
            {
                _hurtFlash -= dt;
                if (_bodySr != null)
                    _bodySr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), Mathf.Clamp01(_hurtFlash / 0.2f));
            }

            if (Hp <= 0f) return false; // 昏厥
            return true;
        }

        void Attack()
        {
            // 挥击特效：面前短暂出现的弧光色块
            var fx = SpriteFactory.NewSprite("SlashFx", _director.transform,
                SpriteFactory.Rect(0.7f, 0.5f, new Color(1f, 1f, 0.85f, 0.55f), 0.2f),
                (Vector2)transform.position + new Vector2(_facing * 0.75f, 0.8f), 40);
            Destroy(fx.gameObject, 0.09f);

            _director.DamageMonstersInArc(transform.position, _facing,
                _bal.playerAttackRange, _bal.playerAttackDamage);
        }

        public void TakeDamage(float amount, Vector3 fromPos)
        {
            if (amount <= 0f) return;
            Hp = Mathf.Max(0f, Hp - amount);
            _hurtFlash = 0.2f;
            SfxSynth.Play(SfxSynth.Id.Hurt, 0.5f);
            if (GameManager.Instance != null && GameManager.Instance.CamRig != null)
                Juice.Shake(GameManager.Instance.CamRig.transform, 0.13f, 0.18f); // 受击震屏
            // 击退半步
            float push = transform.position.x >= fromPos.x ? 0.35f : -0.35f;
            float nx = Mathf.Clamp(transform.position.x + push, _director.MinX, _director.MaxX);
            transform.position = new Vector3(nx, transform.position.y, 0f);
        }

        /// <summary>
        /// 拾取食材；超出负重上限会被拒绝（GDD：负重直接联动饱食度衰减，
        /// "再贪一组"与"活着撤退"的核心博弈点）。返回 true 表示拾取成功。
        /// </summary>
        public bool TryPickup(string ingredientId, Vector3 atPos)
        {
            IngredientDef def = ConfigService.GetIngredient(ingredientId);
            if (def == null) return false;
            if (CarryWeight + def.weight > CarryMax)
            {
                _director.NotifyOverweight();
                return false;
            }
            _loot.TryGetValue(ingredientId, out int n);
            _loot[ingredientId] = n + 1;
            CarryWeight += def.weight;
            SfxSynth.Play(SfxSynth.Id.Pickup, 0.4f);
            Juice.Pulse(transform, 1.1f, 0.15f);
            Bistro.FloatingText.Spawn(_director.transform, atPos + Vector3.up * 0.5f,
                def.displayName, SpriteFactory.ParseHex(def.colorHex));
            return true;
        }

        /// <summary>战利品清单（探险结束移交 GameManager 入库）。</summary>
        public List<ItemStack> LootList()
        {
            var list = new List<ItemStack>();
            foreach (KeyValuePair<string, int> kv in _loot)
                list.Add(new ItemStack { id = kv.Key, count = kv.Value });
            return list;
        }

        public int LootCount()
        {
            int n = 0;
            foreach (KeyValuePair<string, int> kv in _loot) n += kv.Value;
            return n;
        }
    }
}
