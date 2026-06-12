using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>员工运行时状态（随存档持久化）。</summary>
    [Serializable]
    public class StaffState
    {
        public string id;
        public int fatigue;          // 0~100，≥配置上限时禁止派遣
        public bool dispatchTonight; // 今晚是否被标记为外出采集
    }

    /// <summary>
    /// 存档数据模型（JsonUtility 序列化 → PlayerPrefs）。
    /// 仅存"进度"，不存任何配置数值，保证调表后旧档兼容。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int dayIndex = 1;
        public int gold;
        public int shopLevel = 1;
        public List<ItemStack> inventory = new();
        public List<string> unlockedRecipes = new();
        public List<string> ownedDecor = new();
        public List<StaffState> staff = new();
    }

    /// <summary>白天营业结算用的流水统计（不持久化）。</summary>
    public class DayReport
    {
        public int served;
        public int angryLeft;     // 耐心耗尽离开
        public int noDishLeft;    // 没有想吃的菜离开（库存不足）
        public int revenue;
        public int tips;
        public int wages;
        public int NetProfit => revenue + tips - wages;
    }

    /// <summary>
    /// 玩家全局状态：对存档数据的运行时封装。
    /// 所有变更都走方法（带防御检查），UI 通过 OnChanged 事件刷新。
    /// </summary>
    public class PlayerState
    {
        public SaveData Data { get; private set; }

        /// <summary>战前用餐获得的夜间增益（不持久化，过夜即清零）。</summary>
        public int NightDefBuff { get; private set; }
        public int NightSatietyBuff { get; private set; }
        public string NightMealName { get; private set; }

        /// <summary>任意状态变更后触发（金币/库存/解锁等），UI 订阅刷新。</summary>
        public event Action OnChanged;

        public int Gold => Data.gold;
        public int DayIndex => Data.dayIndex;
        public int ShopLevel => Data.shopLevel;

        public PlayerState(SaveData data)
        {
            Data = data ?? new SaveData();
        }

        /// <summary>开新档：按 balance.json 写入初始金币、初始食材、初始菜谱。</summary>
        public static PlayerState CreateNew()
        {
            var save = new SaveData();
            BalanceDef bal = ConfigService.Balance;
            save.gold = bal != null ? bal.startingGold : 0;

            if (bal != null && bal.startingIngredients != null)
            {
                foreach (ItemStack s in bal.startingIngredients)
                {
                    if (s != null && !string.IsNullOrEmpty(s.id) && s.count > 0)
                        save.inventory.Add(new ItemStack { id = s.id, count = s.count });
                }
            }
            if (ConfigService.Recipes != null)
            {
                foreach (RecipeDef r in ConfigService.Recipes)
                {
                    if (r != null && r.unlockedAtStart) save.unlockedRecipes.Add(r.id);
                }
            }
            return new PlayerState(save);
        }

        // ---------- 金币 ----------

        public void AddGold(int amount)
        {
            Data.gold = Mathf.Max(0, Data.gold + amount);
            OnChanged?.Invoke();
        }

        public bool TrySpendGold(int cost)
        {
            if (cost < 0 || Data.gold < cost) return false;
            Data.gold -= cost;
            OnChanged?.Invoke();
            return true;
        }

        // ---------- 库存 ----------

        public int CountOf(string ingredientId)
        {
            ItemStack s = FindStack(ingredientId);
            return s != null ? s.count : 0;
        }

        public void AddIngredient(string ingredientId, int count)
        {
            if (string.IsNullOrEmpty(ingredientId) || count <= 0) return;
            ItemStack s = FindStack(ingredientId);
            if (s == null) Data.inventory.Add(new ItemStack { id = ingredientId, count = count });
            else s.count += count;
            OnChanged?.Invoke();
        }

        public bool TryConsumeIngredient(string ingredientId, int count)
        {
            ItemStack s = FindStack(ingredientId);
            if (s == null || s.count < count || count <= 0) return false;
            s.count -= count;
            if (s.count <= 0) Data.inventory.Remove(s);
            OnChanged?.Invoke();
            return true;
        }

        ItemStack FindStack(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            // 线性查找：库存种类极少（<20），无需字典
            for (int i = 0; i < Data.inventory.Count; i++)
                if (Data.inventory[i] != null && Data.inventory[i].id == id) return Data.inventory[i];
            return null;
        }

        // ---------- 菜谱 ----------

        public bool IsRecipeUnlocked(string recipeId) =>
            !string.IsNullOrEmpty(recipeId) && Data.unlockedRecipes.Contains(recipeId);

        public void UnlockRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId) || Data.unlockedRecipes.Contains(recipeId)) return;
            Data.unlockedRecipes.Add(recipeId);
            OnChanged?.Invoke();
        }

        // ---------- 装饰 ----------

        public bool OwnsDecor(string decorId) =>
            !string.IsNullOrEmpty(decorId) && Data.ownedDecor.Contains(decorId);

        public bool TryBuyDecor(DecorDef def)
        {
            if (def == null || OwnsDecor(def.id) || !TrySpendGold(def.cost)) return false;
            Data.ownedDecor.Add(def.id);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>当前店外总吸引力（GDD §4.2 公式，含季节/活动系数）。</summary>
        public float CurrentAttraction()
        {
            var values = new List<int>();
            foreach (string id in Data.ownedDecor)
            {
                DecorDef d = ConfigService.GetDecor(id);
                if (d != null) values.Add(d.baseAttraction);
            }
            BalanceDef bal = ConfigService.Balance;
            return FormulaLib.TotalAttraction(values,
                bal != null ? bal.seasonStyleCoef : 1f,
                bal != null ? bal.eventBonusCoef : 0f);
        }

        // ---------- 员工 ----------

        public bool HasStaff(string staffId)
        {
            return FindStaff(staffId) != null;
        }

        public StaffState FindStaff(string staffId)
        {
            if (string.IsNullOrEmpty(staffId)) return null;
            for (int i = 0; i < Data.staff.Count; i++)
                if (Data.staff[i] != null && Data.staff[i].id == staffId) return Data.staff[i];
            return null;
        }

        public bool TryHireStaff(StaffDef def)
        {
            if (def == null || HasStaff(def.id) || !TrySpendGold(def.hireCost)) return false;
            Data.staff.Add(new StaffState { id = def.id, fatigue = 0, dispatchTonight = false });
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>是否雇佣了指定职能的员工（Cook=白天自动料理）。</summary>
        public bool HasStaffWithRole(string role)
        {
            foreach (StaffState s in Data.staff)
            {
                StaffDef def = ConfigService.GetStaff(s.id);
                if (def != null && def.role == role) return true;
            }
            return false;
        }

        /// <summary>当日总工资（黄昏结算扣除）。</summary>
        public int TotalDailyWages()
        {
            int sum = 0;
            foreach (StaffState s in Data.staff)
            {
                StaffDef def = ConfigService.GetStaff(s.id);
                if (def != null) sum += def.dailyWage;
            }
            return sum;
        }

        // ---------- 夜间增益 ----------

        public void SetNightMeal(RecipeDef recipe)
        {
            if (recipe == null) return;
            NightDefBuff = recipe.defBuff;
            NightSatietyBuff = recipe.satietyBuff;
            NightMealName = recipe.displayName;
            OnChanged?.Invoke();
        }

        public void ClearNightMeal()
        {
            NightDefBuff = 0;
            NightSatietyBuff = 0;
            NightMealName = null;
        }

        public void NotifyChanged() => OnChanged?.Invoke();
    }
}
