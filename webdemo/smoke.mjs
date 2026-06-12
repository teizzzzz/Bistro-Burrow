#!/usr/bin/env node
// =====================================================================
// 无头冒烟测试（node webdemo/smoke.mjs）：
// 用 DOM 桩在 Node 里完整驱动 main.js 跑一个游戏日——
// 白天 60 秒营业 → 黄昏结算 → 点击「亲自下地穴」→ 夜晚 78 秒（按住 D 前进）
// → 天亮强制收队 → 黎明存档。断言：全程零异常 + 存档进入第 2 天。
// =====================================================================
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import vm from "node:vm";
import assert from "node:assert/strict";

const here = dirname(fileURLToPath(import.meta.url));
const src = (f) => readFileSync(join(here, f), "utf8");

// ---------- DOM / 浏览器 API 桩 ----------
const noop = () => {};
const ctx2d = new Proxy({}, { get: (t, k) => (k === "canvas" ? canvas : noop), set: () => true });
const listeners = { global: {}, canvas: {}, overlay: {} };
const on = (bucket) => (type, fn) => { (listeners[bucket][type] ||= []).push(fn); };

const canvas = {
  width: 1280, height: 720,
  getContext: () => ctx2d,
  addEventListener: on("canvas"),
  getBoundingClientRect: () => ({ left: 0, top: 0, width: 1280, height: 720 }),
};
const overlay = {
  style: {}, _html: "",
  set innerHTML(v) { this._html = v; },
  get innerHTML() { return this._html; },
  addEventListener: on("overlay"),
};
const buttons = {
  btnSpeed: { addEventListener: noop, textContent: "" },
  btnReset: { addEventListener: noop, textContent: "" },
};
const store = {};
let now = 0;
const rafQueue = [];

const sandbox = {
  console,
  Math, JSON, Object, Array, String, Number, Date, Set, Map,
  performance: { now: () => now },
  requestAnimationFrame: (cb) => rafQueue.push(cb),
  localStorage: {
    getItem: (k) => (k in store ? store[k] : null),
    setItem: (k, v) => { store[k] = String(v); },
    removeItem: (k) => delete store[k],
  },
  confirm: () => true,
  document: { getElementById: (id) => (id === "game" ? canvas : id === "overlay" ? overlay : buttons[id]) },
  addEventListener: on("global"),
};
sandbox.window = sandbox;
sandbox.self = sandbox;
vm.createContext(sandbox);

// ---------- 加载游戏三件套 ----------
vm.runInContext(src("config.js"), sandbox, { filename: "config.js" });
vm.runInContext(src("formulas.js"), sandbox, { filename: "formulas.js" });
vm.runInContext(src("main.js"), sandbox, { filename: "main.js" });

// ---------- 驱动工具 ----------
function pump(seconds, step = 1 / 30) {
  const frames = Math.ceil(seconds / step);
  for (let i = 0; i < frames; i++) {
    now += step * 1000;
    const cbs = rafQueue.splice(0);
    assert.ok(cbs.length > 0, "渲染循环意外停止");
    for (const cb of cbs) cb(now);
  }
}
const fireKey = (type, key) => (listeners.global[type] || []).forEach((fn) => fn({ key, preventDefault: noop }));
const clickOverlay = (act) =>
  (listeners.overlay.click || []).forEach((fn) =>
    fn({ target: { closest: () => ({ disabled: false, dataset: { act } }) } }));

// ---------- ① 白天：跑满 60 现实秒到黄昏 ----------
pump(62);
assert.ok(overlay._html.includes("黄昏结算"), "白天结束应弹出黄昏结算面板");
assert.ok(overlay._html.includes("亲自下地穴"), "结算面板应有夜间行动按钮");
console.log("  ✓ 白天 60 秒营业 → 黄昏结算面板弹出");

// ---------- ② 黄昏：点「亲自下地穴」 ----------
clickOverlay("goNight");
console.log("  ✓ 黄昏 → 进入夜晚探索");

// ---------- ③ 夜晚：按住 D 前进，直到天亮强制收队 ----------
fireKey("keydown", "d");
pump(40);
fireKey("keyup", "d");
pump(42); // 夜长 78 秒，跑到 82 秒处必然触发天亮
const saved = JSON.parse(store["bistro_burrow_web_save_v1"]);
assert.equal(saved.dayIndex, 2, "黎明后应自动存档并进入第 2 天");
console.log("  ✓ 夜晚 78 秒 → 天亮强制收队 → 黎明存档（第 2 天）");

// ---------- ④ 新的一天照常运转 ----------
pump(5);
console.log("  ✓ 第 2 天白天循环正常运转");
console.log("\n冒烟测试通过：完整闭环（日→昏→夜→晨）零异常 ✓");
