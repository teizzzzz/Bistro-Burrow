// =====================================================================
// 《Bistro & Burrow》网页镜像原型
// 作用：在浏览器中即点即玩地验证核心闭环（白天经营 → 黄昏结算 → 夜晚探索 → 黎明）。
// 数值与规则与 Unity 工程完全同源：config.js（由 Configs/*.json 生成）+ formulas.js。
// 本文件只做表现层与流程编排，公式一律调 Formulas.*，禁止散落数值。
// =====================================================================
"use strict";
(() => {
  const CFG = window.GAME_CONFIG;
  const F = window.Formulas;
  const BAL = CFG.balance;

  const byId = (list) => Object.fromEntries(list.map((x) => [x.id, x]));
  const ingById = byId(CFG.ingredients);
  const recById = byId(CFG.recipes);
  const monById = byId(CFG.monsters);
  const decorById = byId(CFG.decor);
  const staffById = byId(CFG.staff);

  const canvas = document.getElementById("game");
  const ctx = canvas.getContext("2d");
  const overlay = document.getElementById("overlay");
  const W = canvas.width, H = canvas.height;
  const GROUND = 560;
  const SAVE_KEY = "bistro_burrow_web_save_v1";

  // ---------------------------------------------------------------
  // 存档
  // ---------------------------------------------------------------
  function newSave() {
    return {
      dayIndex: 1,
      gold: BAL.startingGold,
      shopLevel: 1,
      inventory: Object.fromEntries(BAL.startingIngredients.map((s) => [s.id, s.count])),
      unlockedRecipes: CFG.recipes.filter((r) => r.unlockedAtStart).map((r) => r.id),
      ownedDecor: [],
      staff: [], // {id, fatigue, dispatchTonight}
    };
  }
  function load() {
    try {
      const raw = localStorage.getItem(SAVE_KEY);
      if (!raw) return null;
      const data = JSON.parse(raw);
      return data && data.dayIndex ? data : null;
    } catch { return null; }
  }
  function save() { localStorage.setItem(SAVE_KEY, JSON.stringify(S)); }

  let S = load() || newSave();
  let nightBuff = { def: 0, satiety: 0, mealName: null }; // 过夜即清，与 Unity 一致

  // ---------------------------------------------------------------
  // 全局流程
  // ---------------------------------------------------------------
  let phase = "day"; // day | dusk | night
  let speed = 1;     // 白天加速验证用
  let toast = { text: "", t: 0 };
  const say = (text) => { toast = { text, t: 2.6 }; };

  const shopLevel = () => CFG.shopLevels[Math.min(S.shopLevel, CFG.shopLevels.length) - 1];
  const invCount = (id) => S.inventory[id] || 0;
  const invAdd = (id, n) => { S.inventory[id] = invCount(id) + n; if (S.inventory[id] <= 0) delete S.inventory[id]; };
  const attraction = () =>
    F.totalAttraction(S.ownedDecor.map((id) => decorById[id]?.baseAttraction || 0), BAL.seasonStyleCoef, BAL.eventBonusCoef);
  const hasCook = () => S.staff.some((st) => staffById[st.id]?.role === "Cook");

  function tryPay(recipe, consume) {
    const res = F.tryPayIngredients(recipe, S.inventory, ingById);
    if (res.ok && consume) res.picked.forEach((id) => invAdd(id, -1));
    return res.ok;
  }

  // ---------------------------------------------------------------
  // 白天（经营）
  // ---------------------------------------------------------------
  const TABLES = [320, 470, 620, 770];
  const QUEUE_X = [950, 1010, 1070, 1130, 1190, 1250];
  const STOVE_X = 150;
  let day = null;

  function startDay() {
    phase = "day";
    overlay.style.display = "none";
    day = {
      clockMin: BAL.dayStartHour * 60,
      customers: [],
      tables: TABLES.map(() => null),
      queue: [],
      stoveJobs: [],
      spawnTimer: 1.5,
      spawnedToday: 0,
      autoCookT: 0,
      floats: [],
      report: { served: 0, angryLeft: 0, noDishLeft: 0, revenue: 0, tips: 0, wages: 0 },
    };
    day.spawnInterval = F.spawnInterval(BAL.baseSpawnIntervalSeconds, attraction(), BAL.attractionSpawnDivisor);
    say(`第 ${S.dayIndex} 天 · ${shopLevel().title} 开始营业！`);
  }

  function pickOrder() {
    const candidates = CFG.recipes.filter(
      (r) => S.unlockedRecipes.includes(r.id) && F.tryPayIngredients(r, S.inventory, ingById).ok
    );
    return candidates.length ? candidates[(Math.random() * candidates.length) | 0] : null;
  }

  function floatText(x, y, text, color) { day.floats.push({ x, y, text, color, t: 0.9 }); }

  function spawnCustomer() {
    day.customers.push({
      x: 1340, y: GROUND, state: "walkQueue", queueIdx: day.queue.length,
      tableIdx: -1, order: null, patience: 0, patienceTotal: 0,
      cookStarted: false, decideT: 0, eatT: 0,
      hue: (Math.random() * 360) | 0,
    });
    day.queue.push(day.customers[day.customers.length - 1]);
  }

  function tickDay(dt) {
    day.clockMin += dt * BAL.gameMinutesPerRealSecond;
    if (day.clockMin >= BAL.dayEndHour * 60) return enterDusk();

    // 生成顾客（吸引力引擎决定节奏，店级决定上限）
    day.spawnTimer -= dt;
    if (day.spawnTimer <= 0) {
      day.spawnTimer = day.spawnInterval;
      if (day.spawnedToday < shopLevel().maxCustomersPerDay && day.queue.length < QUEUE_X.length) {
        spawnCustomer();
        day.spawnedToday++;
      }
    }

    // 队首入座
    while (day.queue.length > 0) {
      const free = day.tables.findIndex((t) => t === null);
      if (free < 0) break;
      const front = day.queue[0];
      if (front.state !== "queue") break;
      day.queue.shift();
      day.tables[free] = front;
      front.tableIdx = free;
      front.state = "walkSeat";
      day.queue.forEach((c, i) => { c.queueIdx = i; if (c.state === "queue") c.state = "walkQueue"; });
    }

    // 帮厨自动开火
    if (hasCook()) {
      day.autoCookT -= dt;
      if (day.autoCookT <= 0) {
        day.autoCookT = 0.9;
        const waiting = day.customers.find((c) => c.state === "wait" && !c.cookStarted);
        if (waiting) tryCookFor(waiting, true);
      }
    }

    // 灶台
    for (let i = day.stoveJobs.length - 1; i >= 0; i--) {
      const job = day.stoveJobs[i];
      if (job.customer.state !== "wait") { day.stoveJobs.splice(i, 1); continue; } // 客人没了，订单作废
      job.remain -= dt;
      if (job.remain <= 0) {
        job.customer.state = "eat";
        job.customer.eatT = BAL.customerEatSeconds;
        floatText(job.customer.x, job.customer.y - 130, "上菜！", "#ffe9a0");
        day.stoveJobs.splice(i, 1);
      }
    }

    // 顾客状态机
    const SPEEDPX = 230;
    for (let i = day.customers.length - 1; i >= 0; i--) {
      const c = day.customers[i];
      const walkTo = (tx) => {
        const dir = Math.sign(tx - c.x);
        c.x += dir * SPEEDPX * dt;
        return Math.abs(c.x - tx) < 6;
      };
      switch (c.state) {
        case "walkQueue":
          if (walkTo(QUEUE_X[c.queueIdx])) c.state = "queue";
          break;
        case "queue": break;
        case "walkSeat":
          if (walkTo(TABLES[c.tableIdx] - 36)) { c.state = "decide"; c.decideT = 0.6; }
          break;
        case "decide":
          c.decideT -= dt;
          if (c.decideT <= 0) {
            c.order = pickOrder();
            if (!c.order) {
              c.state = "leaveNoDish";
              day.report.noDishLeft++;
              day.tables[c.tableIdx] = null;
            } else {
              c.patienceTotal = F.patienceSeconds(BAL.patienceBaseSeconds, shopLevel().difficultyFactor);
              c.patience = c.patienceTotal;
              c.state = "wait";
            }
          }
          break;
        case "wait":
          c.patience -= dt;
          if (c.patience <= 0) {
            c.state = "leaveAngry";
            day.report.angryLeft++;
            day.tables[c.tableIdx] = null;
            floatText(c.x, c.y - 130, "气走了！", "#f27267");
          }
          break;
        case "eat":
          c.eatT -= dt;
          if (c.eatT <= 0) {
            const price = c.order.price;
            const fast = c.patienceTotal > 0 && c.patience / c.patienceTotal > 0.6;
            const tip = fast ? Math.round((price * BAL.tipFastServePercent) / 100) : 0;
            S.gold += price + tip;
            day.report.served++;
            day.report.revenue += price;
            day.report.tips += tip;
            day.tables[c.tableIdx] = null;
            floatText(c.x, c.y - 130, tip ? `+${price}（小费+${tip}）` : `+${price}`, "#ffd75e");
            c.state = "leave";
          }
          break;
        default: // leave / leaveAngry / leaveNoDish
          if (walkTo(1340)) day.customers.splice(i, 1);
      }
    }

    day.floats = day.floats.filter((f) => (f.t -= dt) > 0);
  }

  function tryCookFor(c, silent) {
    if (!c.order || c.cookStarted) return;
    if (day.stoveJobs.length >= shopLevel().stoveSlots) { if (!silent) say("灶台全满，先等等锅！"); return; }
    if (!tryPay(c.order, true)) { if (!silent) say(`食材不足，做不了「${c.order.displayName}」！`); return; }
    c.cookStarted = true;
    day.stoveJobs.push({ customer: c, recipe: c.order, remain: c.order.cookSeconds, total: c.order.cookSeconds });
  }

  canvas.addEventListener("click", (e) => {
    const rect = canvas.getBoundingClientRect();
    const x = ((e.clientX - rect.left) / rect.width) * W;
    const y = ((e.clientY - rect.top) / rect.height) * H;
    if (phase === "day") {
      for (const c of day.customers) {
        if (c.state !== "wait" || c.cookStarted) continue;
        if (Math.abs(x - c.x) < 70 && Math.abs(y - (c.y - 150)) < 40) return tryCookFor(c, false);
      }
    } else if (phase === "night" && night && !night.ended) {
      doAttack();
    }
  });

  // ---------------------------------------------------------------
  // 黄昏（结算面板，DOM）
  // ---------------------------------------------------------------
  let pot = [];
  let tab = "report";

  function enterDusk() {
    phase = "dusk";
    day.report.wages = S.staff.reduce((sum, st) => sum + (staffById[st.id]?.dailyWage || 0), 0);
    S.gold = Math.max(0, S.gold - day.report.wages);
    pot = [];
    tab = "report";
    renderPanel();
    overlay.style.display = "flex";
  }

  const esc = (s) => String(s).replace(/[&<>"]/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[ch]));
  const flavorText = (fl) => (fl || []).map((f) => `${f.tag}${f.value}`).join(" ") || "无";

  function renderPanel() {
    const r = day.report;
    const tabs = [
      ["report", "今日对账"], ["research", "菜品研发"], ["decor", "店外采购"],
      ["staff", "人事派遣"], ["meal", "战前用餐"],
    ];
    let body = "";

    if (tab === "report") {
      const cur = shopLevel();
      const next = CFG.shopLevels[S.shopLevel] || null;
      const interval = F.spawnInterval(BAL.baseSpawnIntervalSeconds, attraction(), BAL.attractionSpawnDivisor);
      body += `<p>接待 <b>${r.served}</b> 位 ｜ 气走 ${r.angryLeft} ｜ 无菜可点 ${r.noDishLeft}</p>
        <p class="gold">营业额 ${r.revenue} ＋ 小费 ${r.tips} − 日薪 ${r.wages} ＝ 净利 ${r.revenue + r.tips - r.wages}</p>
        <p class="dim">店外吸引力 A = ${attraction().toFixed(1)}（明日客流约每 ${interval.toFixed(1)} 秒一位）</p>
        <p>当前评级：${"★".repeat(S.shopLevel)} ${esc(cur.title)}（客流上限 ${cur.maxCustomersPerDay}/日，难度 ${cur.difficultyFactor}）</p>`;
      if (next) {
        const ok = S.gold >= next.upgradeCost;
        body += `<div class="row"><span>升级 → ${"★".repeat(next.level)} ${esc(next.title)}：客流 ${next.maxCustomersPerDay}/日、灶台 ${next.stoveSlots} 口（需 ${next.upgradeCost} 金币）</span>
          <button data-act="upgrade" ${ok ? "" : "disabled"}>${ok ? "升级！" : "金币不足"}</button></div>`;
      } else {
        body += `<p class="dim">已是最高评级——地穴巨擘。</p>`;
      }
    }

    if (tab === "research") {
      const potNames = pot.map((id) => ingById[id].displayName).join("、") || "（空）";
      const sum = F.sumFlavors(pot, ingById);
      const sumText = Object.entries(sum).map(([t, v]) => t + v).join(" ") || "（无）";
      body += `<p class="dim">选 1~3 个食材入锅，风味匹配即解锁新菜谱；失败炼成黑暗料理（回收 ${BAL.darkCuisineSalvageGold} 金币）。</p>
        <div class="row"><span>大锅：${esc(potNames)}　风味求和：${esc(sumText)}</span>
          <button data-act="potClear" ${pot.length ? "" : "disabled"}>清空</button>
          <button data-act="potCook" ${pot.length ? "" : "disabled"}>开锅！</button></div>`;
      for (const [id, count] of Object.entries(S.inventory)) {
        const def = ingById[id];
        if (!def) continue;
        const free = count - pot.filter((p) => p === id).length;
        body += `<div class="row"><span>${esc(def.displayName)} ×${free}　[${flavorText(def.flavors)}]　负重 ${def.weight}</span>
          <button data-act="potAdd" data-id="${id}" ${free > 0 && pot.length < 3 ? "" : "disabled"}>放入</button></div>`;
      }
      if (!Object.keys(S.inventory).length) body += `<p class="dim">库房空空如也……今晚去地穴采集吧。</p>`;
    }

    if (tab === "decor") {
      body += `<p class="dim">店外装潢决定基础吸引力（当前 A = ${attraction().toFixed(1)}），明日生效。</p>`;
      for (const d of CFG.decor) {
        const owned = S.ownedDecor.includes(d.id);
        const ok = S.gold >= d.cost;
        body += `<div class="row"><span>${esc(d.displayName)}　吸引力 +${d.baseAttraction}　价格 ${d.cost}</span>
          <button data-act="buyDecor" data-id="${d.id}" ${owned || !ok ? "disabled" : ""}>${owned ? "已拥有" : ok ? "购买" : "金币不足"}</button></div>`;
      }
    }

    if (tab === "staff") {
      body += `<p class="dim">帮厨白天自动开火；采集员可夜派（疲劳 +${BAL.dispatchFatigueCost}，≥${BAL.fatigueDispatchLimit} 须休息；留守每晚恢复 ${BAL.fatigueRecoverPerNight}）。</p>`;
      for (const def of CFG.staff) {
        const st = S.staff.find((s) => s.id === def.id);
        const role = def.role === "Cook" ? "帮厨" : "采集员";
        if (!st) {
          const ok = S.gold >= def.hireCost;
          body += `<div class="row"><span>${esc(def.displayName)}（${role}）　日薪 ${def.dailyWage}　签约费 ${def.hireCost}</span>
            <button data-act="hire" data-id="${def.id}" ${ok ? "" : "disabled"}>${ok ? "雇佣" : "金币不足"}</button></div>`;
        } else if (def.role === "Gatherer") {
          const tired = st.fatigue >= BAL.fatigueDispatchLimit;
          body += `<div class="row"><span>${esc(def.displayName)}（${role}）　疲劳 ${st.fatigue}/100${tired ? "　——累瘫了须休息" : ""}</span>
            <button data-act="dispatch" data-id="${def.id}" ${tired && !st.dispatchTonight ? "disabled" : ""}>${st.dispatchTonight ? "取消派遣" : "今晚派遣"}</button></div>`;
        } else {
          body += `<div class="row"><span>${esc(def.displayName)}（${role}）　已入职，白天自动开火做菜</span></div>`;
        }
      }
    }

    if (tab === "meal") {
      const cur = nightBuff.mealName
        ? `已食用「${esc(nightBuff.mealName)}」：Def+${nightBuff.def}，饱食上限 +${nightBuff.satiety}`
        : "尚未用餐（今晚裸装下地穴）";
      body += `<p>${cur}</p><p class="dim">吃一道已解锁的菜（消耗食材），获得今晚的减伤与饱食加成（GDD：以吃代练）。</p>`;
      for (const rcp of CFG.recipes) {
        if (!S.unlockedRecipes.includes(rcp.id)) continue;
        const ok = F.tryPayIngredients(rcp, S.inventory, ingById).ok;
        body += `<div class="row"><span>${esc(rcp.displayName)}　Def+${rcp.defBuff}　饱食上限+${rcp.satietyBuff}　[需 ${flavorText(rcp.requiredFlavors)}]</span>
          <button data-act="eat" data-id="${rcp.id}" ${ok ? "" : "disabled"}>${ok ? "食用" : "食材不足"}</button></div>`;
      }
    }

    overlay.innerHTML = `<div class="window">
      <div class="head"><span class="title">黄昏结算 · 第 ${S.dayIndex} 天</span><span class="gold">金币 ${S.gold}</span></div>
      <div class="tabs">${tabs.map(([k, label]) => `<button data-tab="${k}" class="${tab === k ? "on" : ""}">${label}</button>`).join("")}</div>
      <div class="body">${body}</div>
      <div class="actions">
        <button class="main" data-act="goNight">亲自下地穴（动作采集）</button>
        <button class="sub" data-act="sleep">派遣并就寝（跳过夜晚）</button>
      </div></div>`;
  }

  overlay.addEventListener("click", (e) => {
    const btn = e.target.closest("button");
    if (!btn || btn.disabled) return;
    if (btn.dataset.tab) { tab = btn.dataset.tab; return renderPanel(); }
    const id = btn.dataset.id;
    switch (btn.dataset.act) {
      case "upgrade": {
        const next = CFG.shopLevels[S.shopLevel];
        if (next && S.gold >= next.upgradeCost) {
          S.gold -= next.upgradeCost;
          S.shopLevel = next.level;
          say(`小馆升级为「${next.title}」！明日生效。`);
        }
        break;
      }
      case "potAdd": if (pot.length < 3) pot.push(id); break;
      case "potClear": pot = []; break;
      case "potCook": {
        // 匹配优先级：未解锁 > 已解锁（与 RecipeSystem.Research 一致），
        // 否则高级组合永远被低需求的已知菜截胡
        const sum = F.sumFlavors(pot, ingById);
        const locked = CFG.recipes.find((r) => !S.unlockedRecipes.includes(r.id) && F.satisfies(sum, r));
        const known = locked ? null : F.matchAnyRecipe(sum, CFG.recipes);
        if (known) {
          say(`这锅炖出来还是「${known.displayName}」，早就会做了（食材未消耗）。`);
        } else if (locked) {
          pot.forEach((p) => invAdd(p, -1));
          S.unlockedRecipes.push(locked.id);
          say(`研发成功！解锁「${locked.displayName}」（售价 ${locked.price}）`);
        } else {
          pot.forEach((p) => invAdd(p, -1));
          S.gold += BAL.darkCuisineSalvageGold;
          say(`咕嘟咕嘟……炼成了微妙的黑暗料理（回收 ${BAL.darkCuisineSalvageGold} 金币）。`);
        }
        pot = [];
        break;
      }
      case "buyDecor": {
        const d = decorById[id];
        if (d && !S.ownedDecor.includes(id) && S.gold >= d.cost) {
          S.gold -= d.cost;
          S.ownedDecor.push(id);
          say(`已购入「${d.displayName}」！`);
        }
        break;
      }
      case "hire": {
        const def = staffById[id];
        if (def && !S.staff.some((s) => s.id === id) && S.gold >= def.hireCost) {
          S.gold -= def.hireCost;
          S.staff.push({ id, fatigue: 0, dispatchTonight: false });
          say(`${def.displayName} 入职了！`);
        }
        break;
      }
      case "dispatch": {
        const st = S.staff.find((s) => s.id === id);
        if (st) st.dispatchTonight = !st.dispatchTonight;
        break;
      }
      case "eat": {
        const rcp = recById[id];
        if (rcp && tryPay(rcp, true)) {
          nightBuff = { def: rcp.defBuff, satiety: rcp.satietyBuff, mealName: rcp.displayName };
          say(`饱餐一顿「${rcp.displayName}」！今晚 Def+${rcp.defBuff}。`);
        }
        break;
      }
      case "goNight": return startNight();
      case "sleep": return dawn(null, []);
    }
    renderPanel();
  });

  // ---------------------------------------------------------------
  // 夜晚（横版探索）
  // ---------------------------------------------------------------
  const PX = 110; // Unity 世界单位 → 像素
  let night = null;
  const keys = {};
  addEventListener("keydown", (e) => {
    const k = e.key.toLowerCase();
    // 阻止空格/方向键滚动页面（夜晚操作键）
    if ([" ", "arrowup", "arrowdown", "arrowleft", "arrowright"].includes(k)) e.preventDefault();
    keys[k] = true;
    if (phase === "night" && night && !night.ended) {
      if (k === "j") doAttack();
      if (k === "e") tryReturnHome();
    }
  });
  addEventListener("keyup", (e) => { keys[e.key.toLowerCase()] = false; });

  function startNight() {
    phase = "night";
    overlay.style.display = "none";
    const mk = (mid, ux) => {
      const def = monById[mid];
      return { def, x: ux * PX, hp: def.maxHp, dir: 1, spawnX: ux * PX, atkCd: 0 };
    };
    const pk = (iid, ux) => ({ id: iid, x: ux * PX, y: GROUND - 36 });
    night = {
      clockMin: BAL.nightStartHour * 60,
      camX: 0,
      ended: false,
      player: {
        x: 180, y: GROUND, vy: 0, grounded: true, facing: 1,
        satietyMax: BAL.satietyMax + nightBuff.satiety,
        satiety: BAL.satietyMax + nightBuff.satiety,
        hp: BAL.playerMaxHp, def: nightBuff.def,
        weight: 0, loot: {}, atkCd: 0,
      },
      doorX: 80,
      worldW: 62 * PX,
      monsters: [
        mk("mushroom_walker", 8), mk("slime_blob", 11), mk("mushroom_walker", 13.5),
        mk("slime_blob", 17), mk("mushroom_walker", 20),
        mk("mimic_crawler", 24), mk("mimic_crawler", 30), mk("mimic_crawler", 36),
        mk("giant_scorpion", 45), mk("giant_scorpion", 54),
      ],
      pickups: [
        pk("dungeon_herb", 6), pk("honey_fruit", 9.5), pk("dungeon_herb", 14),
        pk("honey_fruit", 19), pk("dungeon_herb", 21.5), pk("dungeon_herb", 28),
        pk("honey_fruit", 33), pk("ember_pepper", 38), pk("ember_pepper", 50),
        pk("honey_fruit", 57),
      ],
      floats: [],
    };
    say("夜探地穴：A/D 移动 · 空格跳跃 · J/点击攻击 · 门口按 E 回家");
  }

  function doAttack() {
    const p = night.player;
    if (p.atkCd > 0) return;
    p.atkCd = BAL.playerAttackCooldown;
    p.slashT = 0.1;
    for (const m of night.monsters) {
      const dx = m.x - p.x;
      const inFront = p.facing > 0 ? dx > -20 : dx < 20;
      if (!inFront || Math.abs(dx) > BAL.playerAttackRange * PX) continue;
      m.hp -= BAL.playerAttackDamage;
      m.x += m.x >= p.x ? 24 : -24;
      if (m.hp <= 0) {
        const n = m.def.dropMin + ((Math.random() * (m.def.dropMax - m.def.dropMin + 1)) | 0);
        for (let i = 0; i < n; i++) {
          night.pickups.push({ id: m.def.dropIngredientId, x: m.x + (Math.random() * 80 - 40), y: GROUND - 36 });
        }
      }
    }
    night.monsters = night.monsters.filter((m) => m.hp > 0);
  }

  function tryReturnHome() {
    if (Math.abs(night.player.x - night.doorX) < 140) endNight(false);
  }

  function lootList(p) { return Object.entries(p.loot).map(([id, count]) => ({ id, count })); }

  function endNight(passedOut) {
    if (night.ended) return;
    night.ended = true;
    const loot = lootList(night.player);
    let lost = 0;
    for (const s of loot) {
      const kept = passedOut ? F.lootKeptAfterPassOut(s.count, BAL.passOutLootLossPercent) : s.count;
      lost += s.count - kept;
      if (kept > 0) invAdd(s.id, kept);
    }
    dawn(passedOut ? `你在地穴中昏了过去……丢失了 ${lost} 件食材。` : null, loot);
  }

  function tickNight(dt) {
    const p = night.player;
    night.clockMin += dt * BAL.gameMinutesPerRealSecond;
    if (night.clockMin >= (24 + BAL.nightEndHour) * 60) { say("天亮了，强制收队！"); return endNight(false); }

    // 移动 / 跳跃 / 重力（手写，无物理引擎，与 Unity 镜像一致）
    const left = keys["a"] || keys["arrowleft"];
    const right = keys["d"] || keys["arrowright"];
    if (left || right) {
      p.facing = right ? 1 : -1;
      p.x += p.facing * BAL.playerMoveSpeed * PX * dt;
      p.x = Math.max(40, Math.min(night.worldW, p.x));
    }
    if ((keys[" "] || keys["w"] || keys["arrowup"]) && p.grounded) {
      p.vy = -BAL.playerJumpSpeed * PX;
      p.grounded = false;
    }
    if (!p.grounded) {
      p.vy += BAL.gravity * PX * dt;
      p.y += p.vy * dt;
      if (p.y >= GROUND) { p.y = GROUND; p.vy = 0; p.grounded = true; }
    }
    p.atkCd -= dt;
    if (p.slashT > 0) p.slashT -= dt;

    // 饱食度衰减（核心公式）→ 饥饿掉血
    const decay = F.satietyDecayPerSecond(BAL.satietyBaseDecayPerSecond, p.weight, BAL.maxCarryWeight, BAL.satietyLoadDecayFactor);
    p.satiety = Math.max(0, p.satiety - decay * dt);
    if (p.satiety <= 0) p.hp -= BAL.starveHpLossPerSecond * dt;
    if (p.hp <= 0) return endNight(true);

    // 魔物 AI：巡逻/追击/近身（伤害过减伤公式）
    for (const m of night.monsters) {
      m.atkCd -= dt;
      const dist = Math.abs(p.x - m.x);
      if (dist < m.def.aggroRange * PX) {
        m.dir = p.x > m.x ? 1 : -1;
        if (dist < 80 && Math.abs(p.y - GROUND) < 60) {
          if (m.atkCd <= 0) {
            m.atkCd = 1.0;
            p.hp -= F.actualDamage(m.def.damage, p.def);
            p.x += p.x >= m.x ? 30 : -30;
          }
          continue;
        }
        m.x += m.dir * m.def.moveSpeed * PX * dt;
      } else {
        if (m.x > m.spawnX + 2.2 * PX) m.dir = -1;
        else if (m.x < m.spawnX - 2.2 * PX) m.dir = 1;
        m.x += m.dir * m.def.moveSpeed * 0.55 * PX * dt;
      }
    }

    // 拾取（负重上限拒收）
    for (let i = night.pickups.length - 1; i >= 0; i--) {
      const k = night.pickups[i];
      if (Math.abs(p.x - k.x) < 60 && Math.abs(p.y - GROUND) < 80) {
        const def = ingById[k.id];
        if (p.weight + def.weight > BAL.maxCarryWeight) {
          say("背包太重了！再装下去会走不动……（先回家卸货）");
          continue;
        }
        p.loot[k.id] = (p.loot[k.id] || 0) + 1;
        p.weight += def.weight;
        night.floats.push({ x: k.x, y: GROUND - 80, text: def.displayName, color: def.colorHex, t: 0.9 });
        night.pickups.splice(i, 1);
      }
    }
    night.floats = night.floats.filter((f) => (f.t -= dt) > 0);

    // 相机平滑跟随
    const targetCam = Math.max(0, Math.min(p.x - 420, night.worldW - W));
    night.camX += (targetCam - night.camX) * Math.min(1, dt * 5);
  }

  // ---------------------------------------------------------------
  // 黎明：派遣结算 + 疲劳 + 存档 + 新的一天
  // ---------------------------------------------------------------
  function dawn(summary, loot) {
    overlay.style.display = "none";
    for (const st of S.staff) {
      const def = staffById[st.id];
      if (st.dispatchTonight && def?.role === "Gatherer") {
        const n = BAL.dispatchYieldMin + ((Math.random() * (BAL.dispatchYieldMax - BAL.dispatchYieldMin + 1)) | 0);
        for (let i = 0; i < n; i++) {
          invAdd(BAL.dispatchLootPool[(Math.random() * BAL.dispatchLootPool.length) | 0], 1);
        }
        st.fatigue = Math.min(100, st.fatigue + BAL.dispatchFatigueCost);
      } else {
        st.fatigue = Math.max(0, st.fatigue - BAL.fatigueRecoverPerNight);
      }
      st.dispatchTonight = false;
    }
    nightBuff = { def: 0, satiety: 0, mealName: null };
    S.dayIndex++;
    save();
    if (summary) say(summary);
    startDay();
  }

  // ---------------------------------------------------------------
  // 渲染
  // ---------------------------------------------------------------
  const fmtClock = (min) => {
    const h = Math.floor(min / 60) % 24, m = Math.floor(min % 60);
    return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`;
  };
  function rect(x, y, w, h, color, r = 6) {
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.roundRect(x, y, w, h, r);
    ctx.fill();
  }
  function circle(x, y, d, color) {
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(x, y, d / 2, 0, Math.PI * 2);
    ctx.fill();
  }
  function text(str, x, y, size, color, align = "left") {
    ctx.fillStyle = color;
    ctx.font = `${size}px "Microsoft YaHei", "PingFang SC", sans-serif`;
    ctx.textAlign = align;
    ctx.fillText(str, x, y);
  }
  function person(x, y, hue, hat) {
    rect(x - 16, y - 88, 32, 56, `hsl(${hue} 45% 62%)`, 10);
    circle(x, y - 104, 30, "#f3d8b8");
    if (hat) rect(x - 16, y - 130, 32, 18, "#fff", 6);
  }

  function drawDay() {
    ctx.fillStyle = "#20242e";
    ctx.fillRect(0, 0, W, H);
    // 店内
    rect(20, 130, 840, 460, "#262b3a", 12);
    rect(20, 540, 840, 60, "#4a3b2f", 6);
    // 店外
    rect(880, 130, 400, 410, "#3d4865", 10);
    rect(880, 540, 400, 60, "#3a3f36", 6);
    rect(862, 200, 22, 400, "#332620", 4); // 外立面
    text("店外街区（排队）", 1080, 165, 18, "#aab3cc", "center");
    // 店外装饰
    S.ownedDecor.forEach((id, i) => {
      const d = decorById[id];
      rect(905 + i * 60, 480, 10, 60, "#4d4337", 3);
      rect(890 + i * 60, 445, 40, 40, d.colorHex, 10);
    });
    // 灶台
    rect(STOVE_X - 55, 460, 110, 90, "#6b4f3c", 8);
    rect(STOVE_X - 30, 430, 60, 34, "#383b46", 8);
    text("灶台", STOVE_X, 590, 16, "#c9b8a3", "center");
    const slots = shopLevel().stoveSlots;
    for (let i = 0; i < slots; i++) {
      const bx = STOVE_X - 50 + i * 38;
      rect(bx, 400, 32, 10, "rgba(0,0,0,.45)", 3);
      if (day.stoveJobs[i]) {
        const j = day.stoveJobs[i];
        rect(bx, 400, 32 * (1 - j.remain / j.total), 10, "#ffa040", 3);
      }
    }
    // 餐桌
    for (const tx of TABLES) {
      rect(tx - 50, 500, 100, 30, "#73522f", 8);
      rect(tx - 8, 530, 16, 24, "#54391c", 3);
    }
    // 顾客
    for (const c of day.customers) {
      person(c.x, c.y, c.hue, false);
      if (c.state === "wait") {
        const bw = 150;
        rect(c.x - bw / 2, c.y - 195, bw, 54, "rgba(255,255,255,.93)", 10);
        text(c.order.displayName, c.x, c.y - 168, 16, "#2b2218", "center");
        const ratio = Math.max(0, c.patience / c.patienceTotal);
        rect(c.x - 60, c.y - 152, 120, 7, "rgba(0,0,0,.25)", 3);
        rect(c.x - 60, c.y - 152, 120 * ratio, 7, ratio > 0.4 ? "#59c763" : "#d9534f", 3);
        if (!c.cookStarted) text("点我开火", c.x, c.y - 200, 13, "#ffd75e", "center");
      } else if (c.state === "leaveNoDish") {
        text("没想吃的……", c.x, c.y - 160, 14, "#aab", "center");
      }
    }
    // 主厨常驻灶台旁
    person(STOVE_X + 80, GROUND, 28, true);
    drawFloats(day.floats, 0);
    drawTopBar(`第 ${S.dayIndex} 天`, fmtClock(day.clockMin), `金币 ${S.gold}`, `营业中 · 已接待 ${day.report.served}`);
  }

  function drawNight() {
    const camX = night.camX;
    ctx.fillStyle = "#0f1118";
    ctx.fillRect(0, 0, W, H);
    // 视差背景
    for (let i = 0; i < 18; i++) {
      const x = i * 760 - (camX * 0.35) % 760 - 380;
      rect(x, 180, 620, 320, "#171a26", 40);
    }
    for (let i = 0; i < 26; i++) {
      const x = i * 500 - (camX * 0.65) % 500 - 250;
      rect(x, 300, 26, 260, "#13121a", 6);
      circle(x + 13, 280, 150, "#101820");
    }
    // 地面
    rect(-50, GROUND, W + 100, H - GROUND, "#1c1b22", 0);

    const sx = (wx) => wx - camX; // 世界→屏幕
    // 家门
    rect(sx(night.doorX) - 55, GROUND - 220, 110, 220, "#6b4c2e", 10);
    circle(sx(night.doorX) + 80, GROUND - 240, 26, "#ffd075");
    text("家", sx(night.doorX), GROUND - 100, 22, "#ffe9c0", "center");
    // 拾取物
    for (const k of night.pickups) {
      const def = ingById[k.id];
      circle(sx(k.x), k.y + Math.sin(Date.now() / 300 + k.x) * 4, 30, def.colorHex);
    }
    // 魔物
    for (const m of night.monsters) {
      const size = 50 + m.def.maxHp;
      rect(sx(m.x) - size / 2, GROUND - size * 0.8, size, size * 0.8, m.def.colorHex, 10);
      rect(sx(m.x) - 30, GROUND - size * 0.8 - 16, 60, 7, "rgba(0,0,0,.5)", 3);
      rect(sx(m.x) - 30, GROUND - size * 0.8 - 16, 60 * (m.hp / m.def.maxHp), 7, "#d9534f", 3);
    }
    // 主厨
    const p = night.player;
    person(sx(p.x), p.y, 28, true);
    if (p.slashT > 0) rect(sx(p.x) + p.facing * 30, p.y - 100, 60 * p.facing, 50, "rgba(255,255,220,.5)", 12);

    drawFloats(night.floats, camX);

    // HUD
    rect(14, 60, 340, 120, "rgba(0,0,0,.45)", 10);
    text(`饱食度 ${p.satiety.toFixed(0)}/${p.satietyMax}${p.satiety <= 0 ? "（饥饿！掉血）" : ""}`, 26, 88, 16, "#ffe9a0");
    rect(26, 98, 280, 10, "rgba(0,0,0,.5)", 4);
    rect(26, 98, 280 * Math.max(0, p.satiety / p.satietyMax), 10, "#f2b84c", 4);
    text(`生命 ${Math.max(0, p.hp).toFixed(0)}/${BAL.playerMaxHp}${p.def ? ` · 料理护体 Def+${p.def}` : ""}`, 26, 130, 16, "#ff9d97");
    rect(26, 140, 280, 10, "rgba(0,0,0,.5)", 4);
    rect(26, 140, 280 * Math.max(0, p.hp / BAL.playerMaxHp), 10, "#d9534f", 4);
    text(`负重 ${p.weight}/${BAL.maxCarryWeight}（越重饱食度掉越快）`, 26, 172, 14, "#cfd2e0");
    const lootText = lootList(p).map((s) => `${ingById[s.id].displayName}×${s.count}`).join("  ") || "（空）";
    text(`今夜收获：${lootText}`, 16, 210, 14, "#bfe4bf");
    text(Math.abs(p.x - night.doorX) < 140 ? "按 E 收队回家" : "A/D 移动 · 空格跳跃 · J/点击攻击 · 碰触拾取", W / 2, H - 24, 16, "#eee9d0", "center");
    drawTopBar(`第 ${S.dayIndex} 天`, fmtClock(night.clockMin), `金币 ${S.gold}`, "地穴探索");
  }

  function drawFloats(floats, camX) {
    for (const f of floats) {
      ctx.globalAlpha = Math.min(1, f.t / 0.9);
      text(f.text, f.x - camX, f.y - (0.9 - f.t) * 60, 17, f.color || "#ffd75e", "center");
      ctx.globalAlpha = 1;
    }
  }

  function drawTopBar(a, b, c, d) {
    rect(0, 0, W, 44, "rgba(8,9,14,.85)", 0);
    text(a, 16, 29, 18, "#f2e6cf");
    text(b, 150, 29, 18, "#cfe0ff");
    text(d, 240, 29, 18, "#bfe9c8");
    text(c, W - 16, 29, 18, "#ffd75e", "right");
    if (toast.t > 0) {
      ctx.globalAlpha = Math.min(1, toast.t / 0.6);
      text(toast.text, W / 2, 70, 19, "#fff6dd", "center");
      ctx.globalAlpha = 1;
    }
  }

  // ---------------------------------------------------------------
  // 主循环
  // ---------------------------------------------------------------
  let last = performance.now();
  function frame(now) {
    let dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    toast.t -= dt;
    if (phase === "day") { tickDay(dt * speed); drawDay(); }
    else if (phase === "night") { if (!night.ended) tickNight(dt); if (night && !night.ended) drawNight(); }
    else if (phase === "dusk") drawDay(); // 结算面板悬浮在冻结的店面上
    requestAnimationFrame(frame);
  }

  // 页脚控制
  document.getElementById("btnSpeed").addEventListener("click", (e) => {
    speed = speed === 1 ? 3 : 1;
    e.target.textContent = `白天速度 ×${speed}`;
  });
  document.getElementById("btnReset").addEventListener("click", () => {
    if (confirm("确定重置存档？")) {
      localStorage.removeItem(SAVE_KEY);
      S = newSave();
      nightBuff = { def: 0, satiety: 0, mealName: null };
      startDay();
    }
  });

  startDay();
  requestAnimationFrame(frame);
})();
