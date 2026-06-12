using System.Collections.Generic;
using BistroBurrow.Core;

namespace BistroBurrow.Cooking
{
    /// <summary>研发结果（GDD §4.1：匹配成功解锁正式菜谱，冲突产出黑暗料理）。</summary>
    public enum ResearchOutcome
    {
        UnlockedNew,   // 解锁了新菜谱（消耗食材）
        AlreadyKnown,  // 组合对应的菜谱已会做（不消耗，给予提示）
        DarkCuisine    // 风味矩阵无匹配 → 微妙的黑暗料理（消耗食材，回收少量金币）
    }

    /// <summary>
    /// 料理研发与配料系统（GDD §4.1）：
    /// 不设死板配方，按"所选食材隐藏风味矩阵求和 ≥ 菜谱需求"判定匹配。
    /// 纯逻辑、不依赖 UnityEngine 对象，可被编辑器测试与 Web 原型对照验证。
    /// </summary>
    public static class RecipeSystem
    {
        /// <summary>求和一组食材（按 id，可重复）的风味矩阵。未知 id 自动忽略。</summary>
        public static Dictionary<string, int> SumFlavors(IReadOnlyList<string> ingredientIds)
        {
            var sum = new Dictionary<string, int>();
            if (ingredientIds == null) return sum;
            foreach (string id in ingredientIds)
            {
                IngredientDef def = ConfigService.GetIngredient(id);
                if (def == null || def.flavors == null) continue;
                foreach (FlavorEntry f in def.flavors)
                {
                    if (f == null || string.IsNullOrEmpty(f.tag)) continue;
                    sum.TryGetValue(f.tag, out int v);
                    sum[f.tag] = v + f.value;
                }
            }
            return sum;
        }

        /// <summary>风味和是否满足菜谱全部需求。</summary>
        public static bool Satisfies(Dictionary<string, int> flavorSum, RecipeDef recipe)
        {
            if (flavorSum == null || recipe == null || recipe.requiredFlavors == null) return false;
            foreach (FlavorEntry req in recipe.requiredFlavors)
            {
                if (req == null) continue;
                flavorSum.TryGetValue(req.tag, out int v);
                if (v < req.value) return false;
            }
            return true;
        }

        /// <summary>在全部菜谱中找第一个被该风味和满足的（按配置表顺序，配置即优先级）。</summary>
        public static RecipeDef MatchAnyRecipe(Dictionary<string, int> flavorSum)
        {
            if (ConfigService.Recipes == null) return null;
            foreach (RecipeDef r in ConfigService.Recipes)
            {
                if (Satisfies(flavorSum, r)) return r;
            }
            return null;
        }

        /// <summary>仅在"尚未解锁"的菜谱中匹配（研发的优先目标）。</summary>
        public static RecipeDef MatchLockedRecipe(Dictionary<string, int> flavorSum, PlayerState state)
        {
            if (ConfigService.Recipes == null || state == null) return null;
            foreach (RecipeDef r in ConfigService.Recipes)
            {
                if (r != null && !state.IsRecipeUnlocked(r.id) && Satisfies(flavorSum, r)) return r;
            }
            return null;
        }

        /// <summary>
        /// 执行一次研发：判定匹配结果并落实消耗/解锁/回收。
        /// selectedIds 为玩家放进大锅的 1~3 个食材单位（同 id 可重复）。
        /// 匹配优先级：未解锁菜谱 > 已解锁菜谱——否则高级组合永远会被
        /// 低需求的已知菜（如烤串的"土2"）截胡，玩家无法解锁新菜。
        /// </summary>
        public static ResearchOutcome Research(PlayerState state, IReadOnlyList<string> selectedIds, out RecipeDef matched)
        {
            matched = null;
            if (state == null || selectedIds == null || selectedIds.Count == 0) return ResearchOutcome.DarkCuisine;

            Dictionary<string, int> sum = SumFlavors(selectedIds);
            matched = MatchLockedRecipe(sum, state);

            if (matched == null)
            {
                // 没有可解锁的新菜：看看是否撞上了已会做的组合（不浪费玩家食材）
                RecipeDef known = MatchAnyRecipe(sum);
                if (known != null)
                {
                    matched = known;
                    return ResearchOutcome.AlreadyKnown;
                }
            }

            // 走到这里必然消耗食材（研发成功或炼出黑暗料理）
            foreach (string id in selectedIds) state.TryConsumeIngredient(id, 1);

            if (matched != null)
            {
                state.UnlockRecipe(matched.id);
                return ResearchOutcome.UnlockedNew;
            }

            // 失败惩罚→补偿（GDD §4.1：黑暗料理可流入黑市，MVP 简化为直接回收金币）
            BalanceDef bal = ConfigService.Balance;
            state.AddGold(bal != null ? bal.darkCuisineSalvageGold : 0);
            return ResearchOutcome.DarkCuisine;
        }

        // =====================================================================
        // 自动配料：白天接单/战前用餐时，从库存中为菜谱凑足风味需求
        // =====================================================================

        /// <summary>
        /// 贪心选料：每轮挑选"对未满足风味贡献最大"的库存食材，直至满足或无解。
        /// consume=false 仅做可行性检查（顾客点单前判断"这道菜做不做得出"）。
        /// </summary>
        public static bool TryPayIngredients(RecipeDef recipe, PlayerState state, bool consume)
        {
            if (recipe == null || state == null || recipe.requiredFlavors == null) return false;

            // 剩余需求表
            var deficit = new Dictionary<string, int>();
            foreach (FlavorEntry req in recipe.requiredFlavors)
            {
                if (req == null || req.value <= 0) continue;
                deficit.TryGetValue(req.tag, out int v);
                deficit[req.tag] = v + req.value;
            }
            if (deficit.Count == 0) return true; // 零需求菜谱视为免费可做

            // 库存可用量快照（不直接动真实库存）
            var available = new Dictionary<string, int>();
            foreach (ItemStack s in state.Data.inventory)
            {
                if (s != null && s.count > 0) available[s.id] = s.count;
            }

            var picked = new List<string>();
            const int MaxPicks = 12; // 防御：异常配置下避免死循环
            for (int round = 0; round < MaxPicks; round++)
            {
                if (AllMet(deficit)) break;

                string bestId = null;
                int bestGain = 0;
                foreach (KeyValuePair<string, int> kv in available)
                {
                    if (kv.Value <= 0) continue;
                    IngredientDef def = ConfigService.GetIngredient(kv.Key);
                    int gain = GainTowards(def, deficit);
                    if (gain > bestGain) { bestGain = gain; bestId = kv.Key; }
                }
                if (bestId == null) return false; // 库存无任何食材能推进需求 → 无解

                picked.Add(bestId);
                available[bestId]--;
                ApplyToDeficit(ConfigService.GetIngredient(bestId), deficit);
            }

            if (!AllMet(deficit)) return false;

            if (consume)
            {
                foreach (string id in picked) state.TryConsumeIngredient(id, 1);
            }
            return true;
        }

        static bool AllMet(Dictionary<string, int> deficit)
        {
            foreach (KeyValuePair<string, int> kv in deficit)
                if (kv.Value > 0) return false;
            return true;
        }

        /// <summary>该食材对当前未满足需求的贡献值（只计 deficits 内的标签，封顶到缺口）。</summary>
        static int GainTowards(IngredientDef def, Dictionary<string, int> deficit)
        {
            if (def == null || def.flavors == null) return 0;
            int gain = 0;
            foreach (FlavorEntry f in def.flavors)
            {
                if (f == null) continue;
                if (deficit.TryGetValue(f.tag, out int need) && need > 0)
                    gain += System.Math.Min(f.value, need);
            }
            return gain;
        }

        static void ApplyToDeficit(IngredientDef def, Dictionary<string, int> deficit)
        {
            if (def == null || def.flavors == null) return;
            foreach (FlavorEntry f in def.flavors)
            {
                if (f == null) continue;
                if (deficit.TryGetValue(f.tag, out int need) && need > 0)
                    deficit[f.tag] = System.Math.Max(0, need - f.value);
            }
        }
    }
}
