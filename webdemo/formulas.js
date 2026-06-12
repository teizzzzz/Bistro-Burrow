// =====================================================================
// 核心公式库（JS 镜像）—— 与 Assets/Scripts/Core/FormulaLib.cs 及
// Assets/Scripts/Cooking/RecipeSystem.cs 逐条对应。
// node webdemo/test.mjs 会断言这里的实现与 GDD 数值完全一致；
// Unity 侧由 EditMode 测试（FormulaAndConfigTests.cs）锁同一组契约。
// UMD：浏览器挂 window.Formulas，Node 走 module.exports。
// =====================================================================
(function (root, factory) {
  if (typeof module === "object" && module.exports) module.exports = factory();
  else root.Formulas = factory();
})(typeof self !== "undefined" ? self : this, function () {
  const clamp01 = (v) => Math.min(1, Math.max(0, v));

  // GDD §5.3 饱食度衰减：S_decay = S_base + (W/W_max) × loadFactor
  function satietyDecayPerSecond(baseDecay, currentWeight, maxWeight, loadFactor) {
    if (maxWeight <= 0) return baseDecay;
    return baseDecay + clamp01(currentWeight / maxWeight) * loadFactor;
  }

  // GDD §5.3 减伤：Damage × 100/(100+Def_buff)
  function actualDamage(monsterDamage, defBuff) {
    const def = Math.max(0, defBuff);
    return monsterDamage * (100 / (100 + def));
  }

  // GDD §4.2 吸引力：A = Σ(D_i × α) × (1 + β)
  function totalAttraction(decorBaseValues, styleCoef, eventBonus) {
    let sum = 0;
    for (const d of decorBaseValues || []) sum += d * styleCoef;
    return sum * (1 + eventBonus);
  }

  // 吸引力 → 刷新间隔
  function spawnInterval(baseInterval, attraction, divisor) {
    if (divisor <= 0) return baseInterval;
    return baseInterval / (1 + Math.max(0, attraction) / divisor);
  }

  // 顾客耐心 = 基础 / 难度因子
  function patienceSeconds(baseSeconds, difficultyFactor) {
    return baseSeconds / Math.max(0.01, difficultyFactor);
  }

  // 昏厥后保留的战利品数
  function lootKeptAfterPassOut(lootCount, lossPercent) {
    const loss = Math.floor((lootCount * Math.min(100, Math.max(0, lossPercent))) / 100);
    return Math.max(0, lootCount - loss);
  }

  // ---- 风味矩阵研发（GDD §4.1） ----

  function sumFlavors(ingredientIds, ingredientsById) {
    const sum = {};
    for (const id of ingredientIds || []) {
      const def = ingredientsById[id];
      if (!def || !def.flavors) continue;
      for (const f of def.flavors) sum[f.tag] = (sum[f.tag] || 0) + f.value;
    }
    return sum;
  }

  function satisfies(flavorSum, recipe) {
    if (!recipe || !recipe.requiredFlavors) return false;
    for (const req of recipe.requiredFlavors) {
      if ((flavorSum[req.tag] || 0) < req.value) return false;
    }
    return true;
  }

  // 配置表顺序即匹配优先级（与 C# 一致）
  function matchAnyRecipe(flavorSum, recipes) {
    for (const r of recipes || []) if (satisfies(flavorSum, r)) return r;
    return null;
  }

  // ---- 贪心自动配料（镜像 RecipeSystem.TryPayIngredients） ----
  // inventoryCounts: {id: count}；返回 {ok, picked:[id...]}；consume 时由调用方扣减。
  function tryPayIngredients(recipe, inventoryCounts, ingredientsById) {
    if (!recipe || !recipe.requiredFlavors) return { ok: false, picked: [] };
    const deficit = {};
    for (const req of recipe.requiredFlavors) {
      if (req.value > 0) deficit[req.tag] = (deficit[req.tag] || 0) + req.value;
    }
    if (Object.keys(deficit).length === 0) return { ok: true, picked: [] };

    const available = { ...inventoryCounts };
    const picked = [];
    const allMet = () => Object.values(deficit).every((v) => v <= 0);
    const gainOf = (def) => {
      if (!def || !def.flavors) return 0;
      let g = 0;
      for (const f of def.flavors) {
        const need = deficit[f.tag] || 0;
        if (need > 0) g += Math.min(f.value, need);
      }
      return g;
    };

    for (let round = 0; round < 12; round++) {
      if (allMet()) break;
      let bestId = null;
      let bestGain = 0;
      for (const [id, count] of Object.entries(available)) {
        if (count <= 0) continue;
        const gain = gainOf(ingredientsById[id]);
        if (gain > bestGain) { bestGain = gain; bestId = id; }
      }
      if (!bestId) return { ok: false, picked: [] };
      picked.push(bestId);
      available[bestId] -= 1;
      const def = ingredientsById[bestId];
      for (const f of def.flavors || []) {
        const need = deficit[f.tag] || 0;
        if (need > 0) deficit[f.tag] = Math.max(0, need - f.value);
      }
    }
    return allMet() ? { ok: true, picked } : { ok: false, picked: [] };
  }

  return {
    satietyDecayPerSecond,
    actualDamage,
    totalAttraction,
    spawnInterval,
    patienceSeconds,
    lootKeptAfterPassOut,
    sumFlavors,
    satisfies,
    matchAnyRecipe,
    tryPayIngredients,
  };
});
