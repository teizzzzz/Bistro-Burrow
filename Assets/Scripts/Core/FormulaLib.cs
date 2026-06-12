using System.Collections.Generic;
using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 核心数值公式库（纯静态、零状态、可单元测试）。
    /// 每条公式与 GDD 章节一一对应，修改数值请改 balance.json，而不是这里。
    /// </summary>
    public static class FormulaLib
    {
        /// <summary>
        /// GDD §5.3 饱食度持续消耗：
        /// S_decay = S_base + (W_current / W_max) × loadFactor
        /// 背包越满，每秒扣减越快（默认 0.5 ~ 2.0 点/秒）。
        /// </summary>
        public static float SatietyDecayPerSecond(float baseDecay, float currentWeight, float maxWeight, float loadFactor)
        {
            if (maxWeight <= 0f) return baseDecay; // 防御：配置异常时退化为基础衰减
            float ratio = Mathf.Clamp01(currentWeight / maxWeight);
            return baseDecay + ratio * loadFactor;
        }

        /// <summary>
        /// GDD §5.3 战斗减算防护：
        /// Damage_actual = Damage_monster × (100 / (100 + Def_buff))
        /// Def_buff 完全来自白天吃下的料理（以吃代练），乘算曲线防通胀。
        /// </summary>
        public static float ActualDamage(float monsterDamage, float defBuff)
        {
            float def = Mathf.Max(0f, defBuff);
            return monsterDamage * (100f / (100f + def));
        }

        /// <summary>
        /// GDD §4.2 店外吸引力引擎：
        /// A_total = Σ(D_i × α_i) × (1 + β_event)
        /// MVP 中 α 为全局季节系数、β 为全局活动加成（balance.json 配置）。
        /// </summary>
        public static float TotalAttraction(IEnumerable<int> decorBaseValues, float styleCoef, float eventBonus)
        {
            float sum = 0f;
            if (decorBaseValues != null)
            {
                foreach (int d in decorBaseValues) sum += d * styleCoef;
            }
            return sum * (1f + eventBonus);
        }

        /// <summary>
        /// 吸引力 → 客流刷新间隔：interval = base / (1 + A / divisor)。
        /// 吸引力越高，客人来得越密（GDD：吸引力决定客流刷新的时间间隔）。
        /// </summary>
        public static float CustomerSpawnInterval(float baseInterval, float attraction, float divisor)
        {
            if (divisor <= 0f) return baseInterval;
            float a = Mathf.Max(0f, attraction);
            return baseInterval / (1f + a / divisor);
        }

        /// <summary>
        /// 顾客耐心时长 = 基础耐心 / 难度因子（GDD §5.2 难度因子=顾客耐心减少率）。
        /// </summary>
        public static float PatienceSeconds(float baseSeconds, float difficultyFactor)
        {
            float diff = Mathf.Max(0.01f, difficultyFactor); // 防止除零
            return baseSeconds / diff;
        }

        /// <summary>昏厥损失：按百分比丢弃战利品数量（向下取整，至少保留 0）。</summary>
        public static int LootKeptAfterPassOut(int lootCount, int lossPercent)
        {
            int loss = Mathf.FloorToInt(lootCount * Mathf.Clamp(lossPercent, 0, 100) / 100f);
            return Mathf.Max(0, lootCount - loss);
        }

        // =====================================================================
        // 员工属性效果（勤快/耐力 0~10；创始伙伴可自定义分配）
        // =====================================================================

        /// <summary>帮厨自动开火间隔：勤快 10 时约为基础的一半（0.9s → 0.5s）。</summary>
        public static float AutoCookInterval(float baseInterval, float diligence)
        {
            return baseInterval / (1f + 0.08f * Mathf.Max(0f, diligence));
        }

        /// <summary>采集员派遣产量加成：勤快每 3 点约 +1 件（10 点 → +3）。</summary>
        public static int DispatchBonusYield(float diligence)
        {
            return Mathf.FloorToInt(Mathf.Max(0f, diligence) * 0.34f);
        }

        /// <summary>派遣疲劳消耗：耐力每点 -4%（10 点 → 40 变 24），下限 5。</summary>
        public static int DispatchFatigueCost(int baseCost, float stamina)
        {
            return Mathf.Max(5, Mathf.RoundToInt(baseCost * (1f - 0.04f * Mathf.Clamp(stamina, 0f, 10f))));
        }
    }
}
