using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Expedition
{
    /// <summary>探险结算结果，移交 GameManager 入库。</summary>
    public class ExpeditionResult
    {
        public List<ItemStack> loot = new();
        public bool passedOut;
    }

    /// <summary>
    /// 黑夜探索导演（GDD §3.3 探险视图）：
    /// 构建横版卷轴关卡（前景采集物/中景战斗层/背景视差），集中驱动玩家、
    /// 魔物与拾取物，维护探险 HUD，并在 回家/昏厥/天亮 三种出口收束结果。
    /// </summary>
    public class ExpeditionDirector : MonoBehaviour
    {
        public const float GroundY = -1.8f;
        public float MinX => -2.5f;
        public float MaxX => 60f;

        GameManager _gm;
        ExpeditionPlayer _player;
        readonly List<MonsterAgent> _monsters = new();
        readonly List<IngredientPickup> _pickups = new();
        bool _ended;
        float _doorX = -1.5f;
        float _overweightToastCd; // 超重提示节流，避免每帧刷屏

        // HUD
        Text _hudSatiety;
        Text _hudHp;
        Text _hudWeight;
        Text _hudLoot;
        Text _hudHint;
        Transform _satietyFill;
        Transform _hpFill;

        public void Init(GameManager gm)
        {
            _gm = gm;
            if (_gm == null)
            {
                Debug.LogError("[ExpeditionDirector] GameManager 为空，探险无法开始。");
                return;
            }

            BuildLevel();

            var playerGo = new GameObject("Chef");
            playerGo.transform.SetParent(transform, false);
            _player = playerGo.AddComponent<ExpeditionPlayer>();
            _player.Init(this, GroundY);

            // 镜头平滑跟随主角（GDD §3.3），左缘锁死防穿帮
            _gm.CamRig.Follow(_player.transform, 0.3f, 0f);

            BuildHud();
        }

        void Update()
        {
            // _gm.State 判空：编辑器 Play 中域重载会清空 GameManager 的运行时状态
            if (_gm == null || _gm.State == null || _gm.Phase != GamePhase.Night || _ended) return;
            float dt = Time.deltaTime;

            if (_player != null)
            {
                if (!_player.Tick(dt))
                {
                    End(passedOut: true); // 昏厥
                    return;
                }

                // 家门口收队
                bool nearDoor = Mathf.Abs(_player.transform.position.x - _doorX) < 1.3f;
                if (_hudHint != null)
                    _hudHint.text = nearDoor ? "按 E 收队回家" : "A/D 移动 · 空格 跳跃 · J/左键 攻击 · 碰触拾取";
                if (nearDoor && Input.GetKeyDown(KeyCode.E))
                {
                    End(passedOut: false);
                    return;
                }
            }

            for (int i = _monsters.Count - 1; i >= 0; i--)
            {
                MonsterAgent m = _monsters[i];
                if (m == null || m.IsDead) { _monsters.RemoveAt(i); continue; }
                m.Tick(dt, _player, GroundY);
            }

            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                IngredientPickup p = _pickups[i];
                if (p == null) { _pickups.RemoveAt(i); continue; }
                if (p.Tick(dt, _player))
                {
                    _pickups.RemoveAt(i);
                    Destroy(p.gameObject);
                }
            }

            _overweightToastCd -= dt;
            RefreshHud();
        }

        /// <summary>天亮强制收队（GameManager 在时钟越过次日清晨时调用）。</summary>
        public void ForceReturn(string reason)
        {
            if (_ended) return;
            if (!string.IsNullOrEmpty(reason) && _gm != null) _gm.UI.Toast(reason);
            End(passedOut: false);
        }

        void End(bool passedOut)
        {
            if (_ended) return;
            _ended = true;
            var result = new ExpeditionResult
            {
                loot = _player != null ? _player.LootList() : new List<ItemStack>(),
                passedOut = passedOut
            };
            _gm.OnExpeditionFinished(result);
        }

        // =====================================================================
        // 战斗与拾取回调
        // =====================================================================

        /// <summary>玩家挥击：命中面朝方向扇形（简化为矩形区）内的所有魔物。</summary>
        public void DamageMonstersInArc(Vector3 from, int facing, float range, float damage)
        {
            for (int i = 0; i < _monsters.Count; i++)
            {
                MonsterAgent m = _monsters[i];
                if (m == null || m.IsDead) continue;
                float dx = m.transform.position.x - from.x;
                bool inFront = facing > 0 ? dx > -0.2f : dx < 0.2f;
                if (!inFront || Mathf.Abs(dx) > range) continue;
                if (Mathf.Abs(m.transform.position.y - from.y) > 1.4f) continue;

                SfxSynth.Play(SfxSynth.Id.Hit, 0.45f);
                Juice.Pulse(m.transform, 1.15f, 0.12f); // 受击挤压感
                if (m.TakeHit(damage, from.x))
                {
                    // 击杀：小震屏 + 掉落弹出
                    if (_gm.CamRig != null) Juice.Shake(_gm.CamRig.transform, 0.09f, 0.14f);
                    SpawnDrops(m.Def, m.transform.position);
                    Destroy(m.gameObject);
                }
            }
        }

        void SpawnDrops(MonsterDef def, Vector3 pos)
        {
            if (def == null || string.IsNullOrEmpty(def.dropIngredientId)) return;
            int count = Random.Range(def.dropMin, def.dropMax + 1);
            for (int i = 0; i < count; i++)
            {
                CreatePickup(def.dropIngredientId,
                    new Vector2(pos.x + Random.Range(-0.45f, 0.45f), GroundY + 0.35f));
            }
        }

        public void NotifyOverweight()
        {
            if (_overweightToastCd > 0f) return;
            _overweightToastCd = 2f;
            _gm.UI.Toast("背包太重了！再装下去会走不动……（先回家卸货）");
        }

        // =====================================================================
        // 关卡构建
        // =====================================================================

        void BuildLevel()
        {
            Transform camT = _gm.CamRig != null ? _gm.CamRig.transform : null;

            // ---- 天幕层（几乎贴住镜头）：夜空渐变 + 星星 + 月亮 ----
            var skyLayer = new GameObject("SkyLayer");
            skyLayer.transform.SetParent(transform, false);
            SpriteFactory.NewSprite("NightSky", skyLayer.transform,
                SpriteFactory.GradientRect(22f, 12f, new Color(0.05f, 0.06f, 0.14f), new Color(0.13f, 0.15f, 0.28f), 0f),
                new Vector2(0f, 0.8f), -10);
            // 星星：两种大小随机散布在天幕上半区
            for (int i = 0; i < 36; i++)
            {
                float sx = Random.Range(-10f, 10f);
                float sy = Random.Range(0.6f, 5.4f);
                float size = Random.value < 0.2f ? 0.07f : 0.045f;
                float a = Random.Range(0.35f, 0.9f);
                SpriteFactory.NewSprite("Star", skyLayer.transform,
                    SpriteFactory.Circle(size, new Color(0.92f, 0.94f, 1f, a)),
                    new Vector2(sx, sy), -9);
            }
            // 月亮 + 月晕
            SpriteFactory.NewSprite("MoonGlow", skyLayer.transform,
                SpriteFactory.RadialGlow(4.5f, new Color(0.85f, 0.88f, 0.75f, 0.22f)),
                new Vector2(5.8f, 3.7f), -9);
            SpriteFactory.NewSprite("Moon", skyLayer.transform,
                SpriteFactory.Circle(0.95f, new Color(0.93f, 0.92f, 0.80f)),
                new Vector2(5.8f, 3.7f), -8);
            SpriteFactory.NewSprite("MoonCrater", skyLayer.transform,
                SpriteFactory.Circle(0.22f, new Color(0.82f, 0.81f, 0.70f)),
                new Vector2(6.0f, 3.85f), -7);
            skyLayer.AddComponent<ParallaxLayer>().Init(camT, 0.04f); // 近乎跟随镜头

            // ---- 远山脊（深剪影）----
            var far = new GameObject("ParallaxFar");
            far.transform.SetParent(transform, false);
            for (int i = 0; i < 9; i++)
            {
                SpriteFactory.NewSprite($"Ridge{i}", far.transform,
                    SpriteFactory.GradientRect(7f, Random.Range(2.2f, 3.6f), new Color(0.09f, 0.10f, 0.18f), new Color(0.07f, 0.08f, 0.14f), 0.4f),
                    new Vector2(i * 7.5f - 3f, 0.6f), -7);
            }
            far.AddComponent<ParallaxLayer>().Init(camT, 0.35f);

            // ---- 中景树影 ----
            var mid = new GameObject("ParallaxMid");
            mid.transform.SetParent(transform, false);
            for (int i = 0; i < 14; i++)
            {
                float x = i * 4.6f - 2f;
                SpriteFactory.NewSprite($"Trunk{i}", mid.transform,
                    SpriteFactory.Rect(0.3f, 2.4f, new Color(0.11f, 0.10f, 0.14f), 0.04f),
                    new Vector2(x, -0.4f), -6);
                SpriteFactory.NewSprite($"Crown{i}", mid.transform,
                    SpriteFactory.Circle(Random.Range(1.4f, 2.2f), new Color(0.10f, 0.14f, 0.15f)),
                    new Vector2(x, 1.2f), -6);
            }
            mid.AddComponent<ParallaxLayer>().Init(camT, 0.65f);

            // ---- 雾气分层：两条低空软雾带（不同视差，制造空气透视）----
            var fogA = new GameObject("FogFar");
            fogA.transform.SetParent(transform, false);
            for (int i = 0; i < 7; i++)
            {
                var fog = SpriteFactory.NewSprite($"Fog{i}", fogA.transform,
                    SpriteFactory.RadialGlow(3.2f, new Color(0.32f, 0.38f, 0.58f, 0.10f), 1.4f),
                    new Vector2(i * 9f, GroundY + 0.9f), -5);
                fog.transform.localScale = new Vector3(3.4f, 1f, 1f);
            }
            fogA.AddComponent<ParallaxLayer>().Init(camT, 0.5f);
            var fogB = new GameObject("FogNear");
            fogB.transform.SetParent(transform, false);
            for (int i = 0; i < 7; i++)
            {
                var fog = SpriteFactory.NewSprite($"Fog{i}", fogB.transform,
                    SpriteFactory.RadialGlow(2.6f, new Color(0.25f, 0.30f, 0.48f, 0.13f), 1.4f),
                    new Vector2(i * 8f + 3f, GroundY + 0.35f), 18);
                fog.transform.localScale = new Vector3(3.8f, 0.8f, 1f);
            }
            fogB.AddComponent<ParallaxLayer>().Init(camT, 0.88f);

            // ---- 地面：顶面微亮的渐变 + 草簇 ----
            SpriteFactory.NewSprite("Ground", transform,
                SpriteFactory.GradientRect(66f, 1.6f, new Color(0.17f, 0.16f, 0.22f), new Color(0.10f, 0.09f, 0.13f), 0.02f),
                new Vector2(28.5f, GroundY - 0.8f), 5);
            for (float gx = -2f; gx < 60f; gx += Random.Range(1.6f, 3.2f))
            {
                SpriteFactory.NewSprite("Grass", transform,
                    SpriteFactory.Rect(0.1f, Random.Range(0.15f, 0.3f), new Color(0.14f, 0.20f, 0.18f), 0.03f),
                    new Vector2(gx, GroundY + 0.1f), 6);
            }

            // ---- 萤火虫（夜林的"活物感"）----
            for (int i = 0; i < 14; i++)
            {
                Firefly.Spawn(transform, new Vector2(Random.Range(2f, 58f), GroundY + Random.Range(0.8f, 2.6f)));
            }

            // ---- 家门（探险入口/出口）：门 + 暖灯 + 大光晕，黑夜中的"安全感灯塔" ----
            SpriteFactory.NewSprite("HomeDoorFrame", transform,
                SpriteFactory.Rect(1.26f, 2.36f, new Color(0.20f, 0.14f, 0.09f), 0.1f),
                new Vector2(_doorX, GroundY + 1.1f), 7);
            SpriteFactory.NewSprite("HomeDoor", transform,
                SpriteFactory.GradientRect(1.1f, 2.2f, new Color(0.50f, 0.37f, 0.22f), new Color(0.38f, 0.27f, 0.16f), 0.12f),
                new Vector2(_doorX, GroundY + 1.1f), 8);
            SpriteFactory.NewSprite("HomeLamp", transform,
                SpriteFactory.Circle(0.3f, new Color(1f, 0.8f, 0.45f, 0.95f)),
                new Vector2(_doorX + 0.8f, GroundY + 2.2f), 9);
            SpriteFactory.NewSprite("HomeLampGlow", transform,
                SpriteFactory.RadialGlow(4.6f, new Color(1f, 0.72f, 0.38f, 0.30f)),
                new Vector2(_doorX + 0.6f, GroundY + 1.6f), 9);

            // 可采集植物（前景采集层）：浅区香草/蜜果，深区烬火椒
            SpawnPlant("dungeon_herb", 6f); SpawnPlant("honey_fruit", 9.5f);
            SpawnPlant("dungeon_herb", 14f); SpawnPlant("honey_fruit", 19f);
            SpawnPlant("dungeon_herb", 21.5f); SpawnPlant("dungeon_herb", 28f);
            SpawnPlant("honey_fruit", 33f); SpawnPlant("ember_pepper", 38f);
            SpawnPlant("ember_pepper", 50f); SpawnPlant("honey_fruit", 57f);

            // 魔物分布：浅→深 难度递增（GDD §5.2 区域随店级解锁，MVP 全开但纵深排布）
            SpawnMonster("mushroom_walker", 8f); SpawnMonster("slime_blob", 11f);
            SpawnMonster("mushroom_walker", 13.5f); SpawnMonster("slime_blob", 17f);
            SpawnMonster("mushroom_walker", 20f);
            SpawnMonster("mimic_crawler", 24f); SpawnMonster("mimic_crawler", 30f);
            SpawnMonster("mimic_crawler", 36f);
            SpawnMonster("giant_scorpion", 45f); SpawnMonster("giant_scorpion", 54f);
        }

        void SpawnPlant(string ingredientId, float x)
        {
            // 植物底座（茎），拾取物悬浮其上
            SpriteFactory.NewSprite($"Stem_{x}", transform,
                SpriteFactory.Rect(0.1f, 0.5f, new Color(0.2f, 0.3f, 0.2f), 0.02f),
                new Vector2(x, GroundY + 0.25f), 20);
            CreatePickup(ingredientId, new Vector2(x, GroundY + 0.65f));
        }

        void CreatePickup(string ingredientId, Vector2 pos)
        {
            if (ConfigService.GetIngredient(ingredientId) == null)
            {
                Debug.LogWarning($"[ExpeditionDirector] 未知食材 id: {ingredientId}，跳过生成。");
                return;
            }
            var go = new GameObject($"Pickup_{ingredientId}");
            go.transform.SetParent(transform, false);
            var p = go.AddComponent<IngredientPickup>();
            p.Init(ingredientId, pos);
            Juice.PopIn(go.transform); // 掉落物弹出
            _pickups.Add(p);
        }

        void SpawnMonster(string monsterId, float x)
        {
            MonsterDef def = ConfigService.GetMonster(monsterId);
            if (def == null)
            {
                Debug.LogWarning($"[ExpeditionDirector] 未知魔物 id: {monsterId}，跳过生成。");
                return;
            }
            var go = new GameObject($"Monster_{monsterId}");
            go.transform.SetParent(transform, false);
            var m = go.AddComponent<MonsterAgent>();
            m.Init(def, new Vector2(x, GroundY));
            _monsters.Add(m);
        }

        // =====================================================================
        // 探险 HUD（挂在本场景根下，随场景卸载一并销毁）
        // =====================================================================

        void BuildHud()
        {
            Canvas canvas = UiFactory.CreateScreenCanvas("ExpeditionHud", 50, transform);

            // y=-56：让出全局顶栏（44px）的高度，避免与时钟/金币重叠
            RectTransform panel = UiFactory.Panel(canvas.transform, new Color(0f, 0f, 0f, 0.35f), "StatusPanel");
            UiFactory.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -56), new Vector2(330, 132));

            _hudSatiety = UiFactory.Label(panel, "", 18, new Color(1f, 0.9f, 0.6f));
            UiFactory.Place((RectTransform)_hudSatiety.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -8), new Vector2(300, 22));
            _satietyFill = BuildBar(panel, -36, new Color(0.95f, 0.72f, 0.3f));

            _hudHp = UiFactory.Label(panel, "", 18, new Color(1f, 0.6f, 0.6f));
            UiFactory.Place((RectTransform)_hudHp.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -56), new Vector2(300, 22));
            _hpFill = BuildBar(panel, -84, new Color(0.85f, 0.32f, 0.3f));

            _hudWeight = UiFactory.Label(panel, "", 16, new Color(0.85f, 0.85f, 0.9f));
            UiFactory.Place((RectTransform)_hudWeight.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -100), new Vector2(300, 20));

            _hudLoot = UiFactory.Label(canvas.transform, "", 16, new Color(0.8f, 0.92f, 0.8f), TextAnchor.UpperLeft);
            UiFactory.Place((RectTransform)_hudLoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16, -196), new Vector2(420, 200));

            _hudHint = UiFactory.Label(canvas.transform, "", 17, new Color(0.95f, 0.95f, 0.85f, 0.9f), TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)_hudHint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(700, 24));
        }

        Transform BuildBar(RectTransform parent, float y, Color color)
        {
            RectTransform bg = UiFactory.Panel(parent, new Color(0f, 0f, 0f, 0.45f), "BarBg");
            UiFactory.Place(bg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, y), new Vector2(280, 12));
            RectTransform fill = UiFactory.Panel(bg, color, "BarFill");
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = new Vector2(280, 0);
            fill.sizeDelta = new Vector2(280, 0);
            return fill;
        }

        void RefreshHud()
        {
            if (_player == null) return;
            if (_hudSatiety != null)
                _hudSatiety.text = $"饱食度 {_player.Satiety:0}/{_player.SatietyMax:0}" +
                    (_player.Satiety <= 0f ? "（饥饿！持续掉血）" : "");
            if (_satietyFill != null && _player.SatietyMax > 0f)
                _satietyFill.localScale = new Vector3(Mathf.Clamp01(_player.Satiety / _player.SatietyMax), 1f, 1f);

            if (_hudHp != null)
                _hudHp.text = $"生命 {_player.Hp:0}/{_player.HpMax:0}" +
                    (_player.DefBuff > 0 ? $" · 料理护体 Def+{_player.DefBuff:0}" : "");
            if (_hpFill != null && _player.HpMax > 0f)
                _hpFill.localScale = new Vector3(Mathf.Clamp01(_player.Hp / _player.HpMax), 1f, 1f);

            if (_hudWeight != null)
                _hudWeight.text = $"负重 {_player.CarryWeight:0}/{_player.CarryMax:0}（越重饱食度掉越快）";

            if (_hudLoot != null)
            {
                var sb = new System.Text.StringBuilder("今夜收获：");
                List<ItemStack> loot = _player.LootList();
                if (loot.Count == 0) sb.Append("（空）");
                foreach (ItemStack s in loot)
                {
                    IngredientDef def = ConfigService.GetIngredient(s.id);
                    sb.Append($"\n  {(def != null ? def.displayName : s.id)} ×{s.count}");
                }
                _hudLoot.text = sb.ToString();
            }
        }
    }
}
