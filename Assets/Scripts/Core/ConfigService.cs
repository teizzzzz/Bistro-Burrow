using System.Collections.Generic;
using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 配置加载服务（GDD §6.1 数据驱动）。
    /// 启动时一次性从 Resources/Configs 读取全部 JSON（TextAsset 方式，
    /// 与 StreamingAssets 不同，无需 UnityWebRequest，天然兼容 WebGL）。
    /// 所有读取均做空引用防御：缺表只报错不崩溃，返回空集合。
    /// </summary>
    public static class ConfigService
    {
        public static BalanceDef Balance { get; private set; }
        public static IReadOnlyList<IngredientDef> Ingredients { get; private set; }
        public static IReadOnlyList<RecipeDef> Recipes { get; private set; }
        public static IReadOnlyList<MonsterDef> Monsters { get; private set; }
        public static IReadOnlyList<DecorDef> Decors { get; private set; }
        public static IReadOnlyList<StaffDef> Staffs { get; private set; }
        public static IReadOnlyList<ShopLevelDef> ShopLevels { get; private set; }

        static readonly Dictionary<string, IngredientDef> _ingredientById = new();
        static readonly Dictionary<string, RecipeDef> _recipeById = new();
        static readonly Dictionary<string, MonsterDef> _monsterById = new();
        static readonly Dictionary<string, DecorDef> _decorById = new();
        static readonly Dictionary<string, StaffDef> _staffById = new();

        static bool _loaded;

        /// <summary>幂等加载：重复调用直接返回，保证编辑器域重载/测试环境下安全。</summary>
        public static void LoadAll()
        {
            if (_loaded) return;
            _loaded = true;

            Balance = LoadJson<BalanceDef>("Configs/balance");
            if (Balance == null)
            {
                Debug.LogError("[ConfigService] balance.json 缺失或解析失败，使用兜底默认值。");
                Balance = new BalanceDef
                {
                    gameMinutesPerRealSecond = 10, dayStartHour = 8, dayEndHour = 18,
                    nightStartHour = 19, nightEndHour = 8, startingGold = 60,
                    satietyMax = 100, satietyBaseDecayPerSecond = 0.5f, satietyLoadDecayFactor = 1.5f,
                    playerMaxHp = 100, playerMoveSpeed = 4.5f, playerJumpSpeed = 7.5f, gravity = 20f,
                    playerAttackDamage = 12, playerAttackRange = 1.3f, playerAttackCooldown = 0.35f,
                    starveHpLossPerSecond = 2f, maxCarryWeight = 30, passOutLootLossPercent = 50,
                    patienceBaseSeconds = 22, customerEatSeconds = 3, baseSpawnIntervalSeconds = 6,
                    attractionSpawnDivisor = 50, seasonStyleCoef = 1f, eventBonusCoef = 0f,
                    tipFastServePercent = 25, dispatchFatigueCost = 40, fatigueRecoverPerNight = 30,
                    fatigueDispatchLimit = 80, dispatchYieldMin = 2, dispatchYieldMax = 4,
                    darkCuisineSalvageGold = 5, startingIngredients = new ItemStack[0]
                };
            }

            Ingredients = LoadList<IngredientConfigFile, IngredientDef>("Configs/ingredients", f => f.items);
            Recipes     = LoadList<RecipeConfigFile, RecipeDef>("Configs/recipes", f => f.items);
            Monsters    = LoadList<MonsterConfigFile, MonsterDef>("Configs/monsters", f => f.items);
            Decors      = LoadList<DecorConfigFile, DecorDef>("Configs/decor", f => f.items);
            Staffs      = LoadList<StaffConfigFile, StaffDef>("Configs/staff", f => f.items);
            ShopLevels  = LoadList<ShopLevelConfigFile, ShopLevelDef>("Configs/shop_levels", f => f.items);

            BuildIndex(Ingredients, _ingredientById, i => i.id, "ingredients");
            BuildIndex(Recipes, _recipeById, r => r.id, "recipes");
            BuildIndex(Monsters, _monsterById, m => m.id, "monsters");
            BuildIndex(Decors, _decorById, d => d.id, "decor");
            BuildIndex(Staffs, _staffById, s => s.id, "staff");
        }

        public static IngredientDef GetIngredient(string id) => Lookup(_ingredientById, id);
        public static RecipeDef GetRecipe(string id) => Lookup(_recipeById, id);
        public static MonsterDef GetMonster(string id) => Lookup(_monsterById, id);
        public static DecorDef GetDecor(string id) => Lookup(_decorById, id);
        public static StaffDef GetStaff(string id) => Lookup(_staffById, id);

        /// <summary>按店铺等级取成长表行；越界时夹取到最近一档，避免存档异常导致崩溃。</summary>
        public static ShopLevelDef GetShopLevel(int level)
        {
            if (ShopLevels == null || ShopLevels.Count == 0) return null;
            int idx = Mathf.Clamp(level - 1, 0, ShopLevels.Count - 1);
            return ShopLevels[idx];
        }

        // ---------- 内部工具 ----------

        static T LoadJson<T>(string resourcePath) where T : class
        {
            // Resources.Load 在资源缺失时返回 null 而非抛异常，这里统一拦截。
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                Debug.LogError($"[ConfigService] 找不到配置 Resources/{resourcePath}.json");
                return null;
            }
            try
            {
                return JsonUtility.FromJson<T>(asset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ConfigService] 解析 {resourcePath} 失败: {e.Message}");
                return null;
            }
        }

        static IReadOnlyList<TItem> LoadList<TFile, TItem>(string path, System.Func<TFile, TItem[]> selector)
            where TFile : class
        {
            TFile file = LoadJson<TFile>(path);
            TItem[] items = file != null ? selector(file) : null;
            return items ?? new TItem[0];
        }

        static void BuildIndex<T>(IReadOnlyList<T> list, Dictionary<string, T> map,
            System.Func<T, string> key, string label)
        {
            map.Clear();
            if (list == null) return;
            foreach (T item in list)
            {
                string k = key(item);
                if (string.IsNullOrEmpty(k)) { Debug.LogError($"[ConfigService] {label} 存在空 id 条目"); continue; }
                if (map.ContainsKey(k)) { Debug.LogError($"[ConfigService] {label} id 重复: {k}"); continue; }
                map[k] = item;
            }
        }

        static T Lookup<T>(Dictionary<string, T> map, string id) where T : class
        {
            if (string.IsNullOrEmpty(id)) return null;
            return map.TryGetValue(id, out T v) ? v : null;
        }

#if UNITY_EDITOR
        /// <summary>编辑器/测试专用：强制重载（域重载关闭时配置表热修改后使用）。</summary>
        public static void ForceReloadForTests()
        {
            _loaded = false;
            LoadAll();
        }
#endif
    }
}
