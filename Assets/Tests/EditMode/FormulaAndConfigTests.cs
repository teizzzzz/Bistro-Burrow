using System.Collections.Generic;
using NUnit.Framework;
using BistroBurrow.Cooking;
using BistroBurrow.Core;

namespace BistroBurrow.Tests
{
    /// <summary>
    /// EditMode 测试：锁死 GDD 数值公式与配置表的契约。
    /// Unity 内运行：Window → General → Test Runner → EditMode → Run All。
    /// 同一套断言在 webdemo/test.mjs 有 JS 镜像，保证双端数值一致。
    /// </summary>
    public class FormulaAndConfigTests
    {
        [SetUp]
        public void SetUp()
        {
            ConfigService.LoadAll();
        }

        // ---------- GDD §5.3 饱食度衰减 ----------

        [Test]
        public void SatietyDecay_EmptyBag_IsBaseRate()
        {
            // 空背包：S_decay = 0.5
            Assert.AreEqual(0.5f, FormulaLib.SatietyDecayPerSecond(0.5f, 0f, 30f, 1.5f), 1e-4f);
        }

        [Test]
        public void SatietyDecay_FullBag_IsBasePlusFactor()
        {
            // 满负重：S_decay = 0.5 + 1.5 = 2.0
            Assert.AreEqual(2.0f, FormulaLib.SatietyDecayPerSecond(0.5f, 30f, 30f, 1.5f), 1e-4f);
        }

        [Test]
        public void SatietyDecay_ZeroMaxWeight_DoesNotThrow()
        {
            // 防御：配置异常（W_max=0）退化为基础衰减，不得除零
            Assert.AreEqual(0.5f, FormulaLib.SatietyDecayPerSecond(0.5f, 10f, 0f, 1.5f), 1e-4f);
        }

        // ---------- GDD §5.3 减伤公式 ----------

        [Test]
        public void ActualDamage_NoBuff_IsFullDamage()
        {
            Assert.AreEqual(30f, FormulaLib.ActualDamage(30f, 0f), 1e-4f);
        }

        [Test]
        public void ActualDamage_Def100_HalvesDamage()
        {
            // Damage × 100/(100+100) = 50%
            Assert.AreEqual(15f, FormulaLib.ActualDamage(30f, 100f), 1e-4f);
        }

        [Test]
        public void ActualDamage_NegativeBuff_ClampedToZero()
        {
            Assert.AreEqual(30f, FormulaLib.ActualDamage(30f, -50f), 1e-4f);
        }

        // ---------- GDD §4.2 吸引力引擎 ----------

        [Test]
        public void Attraction_SumsWithCoefAndEventBonus()
        {
            // (5+8)×1.0 × (1+0.5) = 19.5
            var decors = new List<int> { 5, 8 };
            Assert.AreEqual(19.5f, FormulaLib.TotalAttraction(decors, 1f, 0.5f), 1e-4f);
        }

        [Test]
        public void SpawnInterval_ShrinksWithAttraction()
        {
            float none = FormulaLib.CustomerSpawnInterval(6f, 0f, 50f);
            float some = FormulaLib.CustomerSpawnInterval(6f, 25f, 50f);
            Assert.AreEqual(6f, none, 1e-4f);
            Assert.AreEqual(4f, some, 1e-4f); // 6 / (1 + 25/50) = 4
        }

        // ---------- GDD §5.2 成长表（配置契约） ----------

        [Test]
        public void ShopLevels_MatchGddTable()
        {
            Assert.AreEqual(3, ConfigService.ShopLevels.Count, "GDD 定义三档店铺评级");

            Assert.AreEqual(0, ConfigService.ShopLevels[0].upgradeCost);
            Assert.AreEqual(15, ConfigService.ShopLevels[0].maxCustomersPerDay);
            Assert.AreEqual(1.0f, ConfigService.ShopLevels[0].difficultyFactor, 1e-4f);

            Assert.AreEqual(800, ConfigService.ShopLevels[1].upgradeCost);
            Assert.AreEqual(35, ConfigService.ShopLevels[1].maxCustomersPerDay);
            Assert.AreEqual(1.4f, ConfigService.ShopLevels[1].difficultyFactor, 1e-4f);

            Assert.AreEqual(3500, ConfigService.ShopLevels[2].upgradeCost);
            Assert.AreEqual(70, ConfigService.ShopLevels[2].maxCustomersPerDay);
            Assert.AreEqual(2.0f, ConfigService.ShopLevels[2].difficultyFactor, 1e-4f);
        }

        // ---------- 配置完整性 ----------

        [Test]
        public void Configs_AllLoaded()
        {
            Assert.IsNotNull(ConfigService.Balance);
            Assert.Greater(ConfigService.Ingredients.Count, 0);
            Assert.Greater(ConfigService.Recipes.Count, 0);
            Assert.Greater(ConfigService.Monsters.Count, 0);
            Assert.Greater(ConfigService.Decors.Count, 0);
            Assert.Greater(ConfigService.Staffs.Count, 0);
        }

        [Test]
        public void Monsters_DropIdsExistInIngredients()
        {
            foreach (MonsterDef m in ConfigService.Monsters)
            {
                Assert.IsNotNull(ConfigService.GetIngredient(m.dropIngredientId),
                    $"魔物 {m.id} 的掉落 {m.dropIngredientId} 不在食材表中");
            }
        }

        [Test]
        public void Balance_DispatchPoolIdsValid()
        {
            foreach (string id in ConfigService.Balance.dispatchLootPool)
            {
                Assert.IsNotNull(ConfigService.GetIngredient(id), $"派遣产物 {id} 不在食材表中");
            }
        }

        // ---------- GDD §4.1 风味矩阵研发 ----------

        [Test]
        public void Research_MushroomPlusMimic_UnlocksPot()
        {
            // GDD 给定示例：走路菇 + 拟态怪肉 → 地牢风味菇菇鲜肉煲（土2 + 鲜1+2 ≥ 土2鲜2）
            var sum = RecipeSystem.SumFlavors(new[] { "walking_mushroom", "mimic_meat" });
            RecipeDef matched = RecipeSystem.MatchAnyRecipe(sum);
            Assert.IsNotNull(matched);
            // 烤走路菇串（土2）也满足，但配置顺序代表优先级——串在前
            Assert.AreEqual("roasted_mushroom_skewer", matched.id);

            // 真正的"煲"匹配：必须同时含土与鲜的更高需求
            var potRecipe = ConfigService.GetRecipe("mushroom_meat_pot");
            Assert.IsTrue(RecipeSystem.Satisfies(sum, potRecipe), "走路菇+拟态怪肉 应满足菇菇鲜肉煲的风味需求");
        }

        [Test]
        public void Research_PrefersLockedRecipe_OverKnownOne()
        {
            // 开新档（已会烤串/沙拉）：菇+拟态肉的研发必须解锁"煲"，
            // 而不是被低需求的已知"烤串"截胡（核心研发体验契约）
            var state = NewStateWith(("walking_mushroom", 1), ("mimic_meat", 1));
            state.UnlockRecipe("roasted_mushroom_skewer");

            ResearchOutcome outcome = RecipeSystem.Research(
                state, new[] { "walking_mushroom", "mimic_meat" }, out RecipeDef matched);

            Assert.AreEqual(ResearchOutcome.UnlockedNew, outcome);
            Assert.AreEqual("mushroom_meat_pot", matched.id);
            Assert.IsTrue(state.IsRecipeUnlocked("mushroom_meat_pot"));
            Assert.AreEqual(0, state.CountOf("walking_mushroom"), "研发成功必须消耗食材");
        }

        [Test]
        public void Research_JunkCombo_MatchesNothing()
        {
            var sum = RecipeSystem.SumFlavors(new[] { "slime_jelly" }); // 仅 滑2
            // 滑2 不满足任何需要 甘/土/鲜 的菜谱
            RecipeDef matched = RecipeSystem.MatchAnyRecipe(sum);
            Assert.IsNull(matched, "单独史莱姆凝胶不应匹配任何菜谱（黑暗料理路线）");
        }

        // ---------- 员工属性效果 ----------

        [Test]
        public void StaffStats_FormulasMatchContract()
        {
            // 勤快 10 → 帮厨手速约翻倍；勤快 7 → 派遣 +2；耐力 10 → 疲劳 40 变 24
            Assert.AreEqual(0.5f, FormulaLib.AutoCookInterval(0.9f, 10f), 1e-3f);
            Assert.AreEqual(2, FormulaLib.DispatchBonusYield(7f));
            Assert.AreEqual(24, FormulaLib.DispatchFatigueCost(40, 10f));
            Assert.AreEqual(40, FormulaLib.DispatchFatigueCost(40, 0f));
        }

        [Test]
        public void CustomStaff_ResolvedByPlayerState()
        {
            // 自定义创始伙伴不在配置表，必须经 PlayerState.GetStaffDef 解析
            var state = new PlayerState(new SaveData());
            var founder = new StaffDef
            {
                id = "custom_founder", displayName = "测试伙伴（创始伙伴）", shortName = "测试伙伴",
                role = "Gatherer", dailyWage = 16, diligence = 7, stamina = 3
            };
            state.Data.customStaff.Add(founder);
            state.Data.staff.Add(new StaffState { id = founder.id });

            Assert.IsNull(ConfigService.GetStaff("custom_founder"), "自定义员工不应进配置表");
            Assert.IsNotNull(state.GetStaffDef("custom_founder"));
            Assert.AreEqual(7, state.GetStaffDef("custom_founder").diligence);
            Assert.IsTrue(state.HasStaffWithRole("Gatherer"));
            Assert.AreEqual(16, state.TotalDailyWages());
        }

        // ---------- 存档档位 ----------

        [Test]
        public void SaveSlots_AreIsolated()
        {
            // 用 1/2 号档位互不干扰验证隔离性；测试后清理现场
            SaveSystem.Wipe(1);
            SaveSystem.Wipe(2);
            try
            {
                var a = new PlayerState(new SaveData { gold = 111, dayIndex = 3 });
                var b = new PlayerState(new SaveData { gold = 222, dayIndex = 7 });
                SaveSystem.Save(a, 1);
                SaveSystem.Save(b, 2);

                Assert.AreEqual(111, SaveSystem.Peek(1).gold);
                Assert.AreEqual(7, SaveSystem.Peek(2).dayIndex);

                SaveSystem.Wipe(1);
                Assert.IsFalse(SaveSystem.HasSave(1), "删除 1 号档不应影响读取逻辑");
                Assert.IsTrue(SaveSystem.HasSave(2), "2 号档应不受影响");
            }
            finally
            {
                SaveSystem.Wipe(1);
                SaveSystem.Wipe(2);
            }
        }

        // ---------- 配料贪心算法 ----------

        [Test]
        public void TryPay_ConsumesOnlyWhenAsked()
        {
            var state = NewStateWith(("walking_mushroom", 2), ("mimic_meat", 1));
            RecipeDef pot = ConfigService.GetRecipe("mushroom_meat_pot");

            Assert.IsTrue(RecipeSystem.TryPayIngredients(pot, state, consume: false));
            Assert.AreEqual(2, state.CountOf("walking_mushroom"), "仅检查不应消耗库存");

            Assert.IsTrue(RecipeSystem.TryPayIngredients(pot, state, consume: true));
            Assert.Less(state.CountOf("walking_mushroom") + state.CountOf("mimic_meat"), 3, "实际制作应消耗食材");
        }

        [Test]
        public void TryPay_FailsWhenStockInsufficient()
        {
            var state = NewStateWith(("slime_jelly", 1));
            RecipeDef skewer = ConfigService.GetRecipe("roasted_mushroom_skewer");
            Assert.IsFalse(RecipeSystem.TryPayIngredients(skewer, state, consume: false));
        }

        static PlayerState NewStateWith(params (string id, int count)[] stock)
        {
            var state = new PlayerState(new SaveData());
            foreach ((string id, int count) in stock) state.AddIngredient(id, count);
            return state;
        }
    }
}
