using System;

namespace BistroBurrow.Core
{
    // =====================================================================
    // 数据驱动配置模型（GDD §6.1）
    // 所有数值均来自 Assets/Resources/Configs/*.json，代码中严禁硬编码数值。
    // 注意：JsonUtility 不支持 Dictionary，因此风味矩阵用 FlavorEntry[] 表达。
    // =====================================================================

    /// <summary>风味矩阵条目：一个风味标签（鲜/甘/土/辛/滑/脆）及其强度值。</summary>
    [Serializable]
    public class FlavorEntry
    {
        public string tag;
        public int value;
    }

    /// <summary>食材定义。category：Main(主料/魔物肉) / Side(辅料/迷宫植物) / Seasoning(调味品)。</summary>
    [Serializable]
    public class IngredientDef
    {
        public string id;
        public string displayName;
        public string category;
        public FlavorEntry[] flavors;
        public int weight;          // 背包负重（直接参与夜晚饱食度衰减公式）
        public int satietyRestore;  // 生吃可恢复的饱食度（预留）
        public int basePrice;       // 黑市/回收基础价（预留）
        public string colorHex;     // 程序化贴图用主题色
    }

    /// <summary>菜谱定义。研发匹配规则：所选食材风味矩阵求和 ≥ requiredFlavors 即解锁。</summary>
    [Serializable]
    public class RecipeDef
    {
        public string id;
        public string displayName;
        public FlavorEntry[] requiredFlavors;
        public int price;           // 客人付款金额
        public float cookSeconds;   // 灶台烹饪耗时（现实秒）
        public int defBuff;         // 战前用餐获得的 Def_buff（参与减伤公式）
        public int satietyBuff;     // 战前用餐附加的饱食度上限加成
        public bool unlockedAtStart;
    }

    /// <summary>魔物定义。夜晚探索的战斗与掉落数据源。</summary>
    [Serializable]
    public class MonsterDef
    {
        public string id;
        public string displayName;
        public int maxHp;
        public int damage;          // 原始伤害，实际伤害经 FormulaLib.ActualDamage 减算
        public float moveSpeed;
        public float aggroRange;    // 仇恨半径（米）
        public string dropIngredientId;
        public int dropMin;
        public int dropMax;
        public string colorHex;
    }

    /// <summary>店外装饰定义。baseAttraction 即吸引力公式中的 D_i。</summary>
    [Serializable]
    public class DecorDef
    {
        public string id;
        public string displayName;
        public int baseAttraction;
        public int cost;
        public string colorHex;
    }

    /// <summary>员工定义。role：Cook(自动料理) / Gatherer(可夜间派遣采集)。</summary>
    [Serializable]
    public class StaffDef
    {
        public string id;
        public string displayName;
        public string shortName;   // 店内名牌用短名
        public string role;
        public string look;        // 造型标签：rabbit/adventurer/hunter/chef（纯视觉）
        public int dailyWage;
        public int hireCost;
        public string colorHex;
        // —— 员工属性（0~10，创始伙伴可自定义分配；效果公式见 FormulaLib）——
        public int diligence;      // 勤快：帮厨自动开火更快 / 采集员派遣产量更高
        public int stamina;        // 耐力：派遣的疲劳消耗更低
    }

    /// <summary>店铺评级（GDD §5.2 阶梯式成长表）。</summary>
    [Serializable]
    public class ShopLevelDef
    {
        public int level;
        public string title;
        public int upgradeCost;          // 升到本级所需金币
        public int maxCustomersPerDay;   // 初始最大客流量
        public float difficultyFactor;   // 顾客耐心减少率
        public int stoveSlots;           // 同时烹饪槽位
    }

    /// <summary>库存堆叠（同时也是存档与初始库存的数据结构）。</summary>
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int count;
    }

    /// <summary>全局数值平衡表（GDD §5）。字段与 balance.json 一一对应。</summary>
    [Serializable]
    public class BalanceDef
    {
        public float gameMinutesPerRealSecond; // 1现实秒 = 10游戏分钟
        public int dayStartHour;
        public int dayEndHour;
        public int nightStartHour;
        public int nightEndHour;
        public int startingGold;

        public float satietyMax;
        public float satietyBaseDecayPerSecond; // S_base = 0.5
        public float satietyLoadDecayFactor;    // 负重系数 1.5
        public float playerMaxHp;
        public float playerMoveSpeed;
        public float playerJumpSpeed;
        public float gravity;
        public float playerAttackDamage;
        public float playerAttackRange;
        public float playerAttackCooldown;
        public float starveHpLossPerSecond;
        public float maxCarryWeight;            // W_max
        public int passOutLootLossPercent;
        public int newbieNights;                // 新手保护覆盖的前 N 晚
        public float newbieDamageMultiplier;    // 保护期内受到伤害的倍率（<1 减伤）

        public float patienceBaseSeconds;
        public float customerEatSeconds;
        public float baseSpawnIntervalSeconds;
        public float attractionSpawnDivisor;
        public float seasonStyleCoef;           // α_i（MVP 全局统一）
        public float eventBonusCoef;            // β_event
        public int tipFastServePercent;
        public float autoCookBaseInterval;      // 帮厨自动开火基础间隔（受勤快加成）

        public int dispatchFatigueCost;
        public int fatigueRecoverPerNight;
        public int fatigueDispatchLimit;
        public int dispatchYieldMin;
        public int dispatchYieldMax;
        public int darkCuisineSalvageGold;
        public string[] dispatchLootPool;   // 员工派遣可带回的食材池

        public ItemStack[] startingIngredients;
    }

    // ---------- JsonUtility 顶层包装（JSON 顶层必须是对象） ----------
    [Serializable] public class IngredientConfigFile { public IngredientDef[] items; }
    [Serializable] public class RecipeConfigFile { public RecipeDef[] items; }
    [Serializable] public class MonsterConfigFile { public MonsterDef[] items; }
    [Serializable] public class DecorConfigFile { public DecorDef[] items; }
    [Serializable] public class StaffConfigFile { public StaffDef[] items; }
    [Serializable] public class ShopLevelConfigFile { public ShopLevelDef[] items; }
}
