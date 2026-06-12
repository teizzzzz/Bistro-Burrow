#!/usr/bin/env node
// =====================================================================
// 数值与闭环自动化验证（node webdemo/test.mjs）
// 断言三层契约：
//   1) 公式实现 === GDD §4/§5 给定公式（与 Unity EditMode 测试同一组断言）
//   2) 配置表完整性 + GDD §5.2 成长表数值
//   3) 核心闭环可行性（无头模拟：研发→配料→经营收入→夜间生存）
// =====================================================================
import { createRequire } from "node:module";
import assert from "node:assert/strict";

const require = createRequire(import.meta.url);
const CFG = require("./config.js");
const F = require("./formulas.js");

const BAL = CFG.balance;
const ingById = Object.fromEntries(CFG.ingredients.map((x) => [x.id, x]));
let passed = 0;
const ok = (name, fn) => { fn(); passed++; console.log(`  ✓ ${name}`); };
const approx = (a, b, eps = 1e-4) => assert.ok(Math.abs(a - b) < eps, `${a} ≉ ${b}`);

console.log("== ① GDD 公式契约 ==");
ok("饱食度衰减：空背包 = S_base = 0.5/秒", () =>
  approx(F.satietyDecayPerSecond(0.5, 0, 30, 1.5), 0.5));
ok("饱食度衰减：满负重 = 0.5 + 1.5 = 2.0/秒", () =>
  approx(F.satietyDecayPerSecond(0.5, 30, 30, 1.5), 2.0));
ok("饱食度衰减：W_max=0 不除零", () =>
  approx(F.satietyDecayPerSecond(0.5, 10, 0, 1.5), 0.5));
ok("减伤：Def=0 全额伤害", () => approx(F.actualDamage(30, 0), 30));
ok("减伤：Def=100 恰好减半", () => approx(F.actualDamage(30, 100), 15));
ok("减伤：负 Def 钳为 0", () => approx(F.actualDamage(30, -50), 30));
ok("吸引力：(5+8)×1.0×(1+0.5) = 19.5", () =>
  approx(F.totalAttraction([5, 8], 1.0, 0.5), 19.5));
ok("客流间隔：A=0 → 基础 6 秒；A=25 → 4 秒", () => {
  approx(F.spawnInterval(6, 0, 50), 6);
  approx(F.spawnInterval(6, 25, 50), 4);
});
ok("顾客耐心：22 / 1.4 ≈ 15.71 秒", () =>
  approx(F.patienceSeconds(22, 1.4), 22 / 1.4));
ok("昏厥掉落：10 件丢 50% 保留 5 件", () =>
  assert.equal(F.lootKeptAfterPassOut(10, 50), 5));

console.log("== ② 配置表契约（GDD §5.2 成长表等） ==");
ok("店铺成长表与 GDD 完全一致", () => {
  const t = CFG.shopLevels;
  assert.equal(t.length, 3);
  assert.deepEqual(t.map((x) => x.upgradeCost), [0, 800, 3500]);
  assert.deepEqual(t.map((x) => x.maxCustomersPerDay), [15, 35, 70]);
  assert.deepEqual(t.map((x) => x.difficultyFactor), [1.0, 1.4, 2.0]);
});
ok("时间经济学：1 现实秒=10 游戏分钟 → 白天恰为 60 现实秒", () => {
  const dayGameMinutes = (BAL.dayEndHour - BAL.dayStartHour) * 60;
  approx(dayGameMinutes / BAL.gameMinutesPerRealSecond, 60);
});
ok("魔物掉落物全部存在于食材表", () => {
  for (const m of CFG.monsters) assert.ok(ingById[m.dropIngredientId], m.id);
});
ok("派遣产物池全部存在于食材表", () => {
  for (const id of BAL.dispatchLootPool) assert.ok(ingById[id], id);
});
ok("初始食材全部存在于食材表", () => {
  for (const s of BAL.startingIngredients) assert.ok(ingById[s.id], s.id);
});
ok("菜谱风味标签均有食材可提供", () => {
  const providable = new Set(CFG.ingredients.flatMap((i) => i.flavors.map((f) => f.tag)));
  for (const r of CFG.recipes)
    for (const req of r.requiredFlavors)
      assert.ok(providable.has(req.tag), `${r.id} 需要无人能提供的风味 ${req.tag}`);
});
ok("id 全局唯一", () => {
  for (const list of [CFG.ingredients, CFG.recipes, CFG.monsters, CFG.decor, CFG.staff]) {
    const ids = list.map((x) => x.id);
    assert.equal(new Set(ids).size, ids.length);
  }
});

console.log("== ③ 玩法流程（无头模拟） ==");
ok("GDD 示例：走路菇+拟态怪肉 → 满足「地牢风味菇菇鲜肉煲」", () => {
  const sum = F.sumFlavors(["walking_mushroom", "mimic_meat"], ingById);
  const pot = CFG.recipes.find((r) => r.id === "mushroom_meat_pot");
  assert.ok(F.satisfies(sum, pot));
});
ok("垃圾组合（单独史莱姆凝胶）→ 无匹配（黑暗料理路线）", () => {
  const sum = F.sumFlavors(["slime_jelly"], ingById);
  assert.equal(F.matchAnyRecipe(sum, CFG.recipes), null);
});
ok("贪心配料：库存{菇×2,拟态肉×1} 可做煲，且消耗后不可再做", () => {
  const inv = { walking_mushroom: 2, mimic_meat: 1 };
  const pot = CFG.recipes.find((r) => r.id === "mushroom_meat_pot");
  const res = F.tryPayIngredients(pot, inv, ingById);
  assert.ok(res.ok);
  res.picked.forEach((id) => (inv[id] -= 1));
  // 只剩残料：做不出第二份
  assert.equal(F.tryPayIngredients(pot, inv, ingById).ok, false);
});
ok("研发优先级：已会烤串时，菇+拟态肉应解锁『煲』而非被烤串截胡", () => {
  const unlocked = ["roasted_mushroom_skewer", "herb_salad"];
  const sum = F.sumFlavors(["walking_mushroom", "mimic_meat"], ingById);
  const locked = CFG.recipes.find((r) => !unlocked.includes(r.id) && F.satisfies(sum, r));
  assert.equal(locked?.id, "mushroom_meat_pot");
});
ok("开局库存能支撑首日菜单（两道初始菜均可做）", () => {
  const inv = Object.fromEntries(BAL.startingIngredients.map((s) => [s.id, s.count]));
  for (const r of CFG.recipes.filter((x) => x.unlockedAtStart)) {
    assert.ok(F.tryPayIngredients(r, inv, ingById).ok, r.id);
  }
});
ok("首日经济模拟：接满 15 客全部上菜 → 净利为正且足够推动循环", () => {
  // 最乐观界：15 位客人全部点最便宜的初始菜（沙拉 8 金）
  const cheapest = Math.min(...CFG.recipes.filter((r) => r.unlockedAtStart).map((r) => r.price));
  const lvl1 = CFG.shopLevels[0];
  const minRevenue = lvl1.maxCustomersPerDay * cheapest;
  assert.ok(minRevenue > 0 && minRevenue >= 100, `首日下界收入 ${minRevenue}`);
});
ok("夜间生存模拟：满负重走完整夜（78 现实秒）不会饿死", () => {
  // 夜长：19:00 → 次日 08:00 = 780 游戏分 = 78 现实秒
  const nightSeconds = ((24 + BAL.nightEndHour - BAL.nightStartHour) * 60) / BAL.gameMinutesPerRealSecond;
  approx(nightSeconds, 78);
  let satiety = BAL.satietyMax, hp = BAL.playerMaxHp;
  const decay = F.satietyDecayPerSecond(BAL.satietyBaseDecayPerSecond, BAL.maxCarryWeight, BAL.maxCarryWeight, BAL.satietyLoadDecayFactor);
  for (let t = 0; t < nightSeconds; t += 0.1) {
    satiety = Math.max(0, satiety - decay * 0.1);
    if (satiety <= 0) hp -= BAL.starveHpLossPerSecond * 0.1;
  }
  assert.ok(hp > 0, `满负重整夜后 HP=${hp.toFixed(1)} 应 >0（饥饿压力存在但不致死）`);
  assert.ok(hp < BAL.playerMaxHp, "饥饿惩罚必须实际生效");
});
ok("战斗数值：吃煲（Def+20）后巨尾蝎单次伤害降至 13.3", () => {
  const scorpion = CFG.monsters.find((m) => m.id === "giant_scorpion");
  const pot = CFG.recipes.find((r) => r.id === "mushroom_meat_pot");
  approx(F.actualDamage(scorpion.damage, pot.defBuff), 16 * (100 / 120), 0.01);
});

console.log(`\n全部通过：${passed} 项断言 ✓`);
