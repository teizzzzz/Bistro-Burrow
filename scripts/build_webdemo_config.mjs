#!/usr/bin/env node
// 把 Assets/Resources/Configs/*.json（Unity 与网页镜像共用的唯一数值源）
// 打包成 webdemo/config.js（UMD：浏览器挂 window.GAME_CONFIG，Node 走 module.exports）。
// 改表后重跑：node scripts/build_webdemo_config.mjs
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const cfgDir = join(root, "Assets/Resources/Configs");

const read = (name) => JSON.parse(readFileSync(join(cfgDir, name), "utf8"));

const config = {
  balance: read("balance.json"),
  ingredients: read("ingredients.json").items,
  recipes: read("recipes.json").items,
  monsters: read("monsters.json").items,
  decor: read("decor.json").items,
  staff: read("staff.json").items,
  shopLevels: read("shop_levels.json").items,
};

const banner =
  "// 此文件由 scripts/build_webdemo_config.mjs 自动生成，请勿手改——\n" +
  "// 数值唯一源是 Assets/Resources/Configs/*.json（与 Unity 工程共用）。\n";
const body =
  `(function (root, factory) {\n` +
  `  if (typeof module === "object" && module.exports) module.exports = factory();\n` +
  `  else root.GAME_CONFIG = factory();\n` +
  `})(typeof self !== "undefined" ? self : this, function () {\n` +
  `  return ${JSON.stringify(config, null, 2)};\n` +
  `});\n`;

writeFileSync(join(root, "webdemo/config.js"), banner + body);
console.log("webdemo/config.js 已生成");
