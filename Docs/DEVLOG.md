# 开发日志（遵循《AI 游戏开发协同与代码追踪标准 v1.1》）

---

## 2026-06-12 · 首个垂直切片：核心闭环全量落地

### 1. 本次修改目标

从零搭建《Bistro & Burrow》Unity 6 WebGL 轻量垂直切片：实现 GDD §2 完整核心循环
（白天经营 → 黄昏结算 → 夜晚探索/派遣 → 黎明存档），数据驱动、零美术资产、
附带浏览器即开即玩的网页镜像原型与双端数值自动化验证。

### 2. 涉及文件及职责变动（全部为新增）

- **`Assets/Resources/Configs/*.json`（7 张表）**
  - **职责**：全项目唯一数值源（GDD §6.1 数据驱动）；Unity 与 webdemo 共用。
  - **波及**：任何数值调整只动这里；改表后跑 `node scripts/build_webdemo_config.mjs` 同步网页端。
- **`Assets/Scripts/Core/`**
  - `ConfigModels.cs` / `ConfigService.cs`：JSON → 只读配置（缺表降级不崩溃）。
  - `FormulaLib.cs`：GDD §4.2/§5.3 公式唯一实现点（纯静态可单测）。
  - `GameManager.cs`：常驻单例状态机 Day→Dusk→Night→Dawn；子场景叠加/卸载编排。
  - `SceneFlow.cs`：`CreateScene`+`MoveGameObjectToScene`+`UnloadSceneAsync` 封装（GDD §6.2）。
  - `GameClock.cs`：1 现实秒=10 游戏分钟；白天 60s / 夜晚 78s。
  - `PlayerState.cs` / `SaveSystem.cs`：进度模型 + PlayerPrefs 存档（WebGL→IndexedDB，写后强制 `Save()`）。
  - `CameraRig.cs` / `Bootstrap.cs`：正交相机（定点平移/Lerp 跟随）；场景自举入口。
- **`Assets/Scripts/Bistro/`**：`BistroDirector`（场景构建/顾客调度/点击开火/统计）、
  `CustomerAgent`（排队→入座→点单→耐心→用餐→付款状态机）、`StoveStation`（多槽烹饪）、`FloatingText`。
- **`Assets/Scripts/Expedition/`**：`ExpeditionDirector`(关卡/HUD/三种收束)、`ExpeditionPlayer`
  （手写重力移动/攻击/负重/饱食衰减）、`MonsterAgent`（巡逻/追击/减伤结算）、`IngredientPickup`、`ParallaxLayer`。
- **`Assets/Scripts/Cooking/RecipeSystem.cs`**：风味矩阵求和匹配 + 贪心自动配料（白天点单与战前用餐共用）。
- **`Assets/Scripts/UI/`**：`UIRoot`（顶栏/吐司/黑场转场）、`SettlementPanel`（五页签经营大盘）。
- **`Assets/Editor/WebGLBuildTools.cs`**：GDD §6.3 构建指标设置即代码 + 一键构建菜单。
- **`Assets/Tests/EditMode/FormulaAndConfigTests.cs`**：数值/配置/研发优先级契约。
- **`webdemo/`**：网页镜像原型（index.html + main.js + formulas.js + 生成的 config.js）、
  `test.mjs`（25 项断言）、`smoke.mjs`（无头整环冒烟）。
- **`scripts/`**：meta 生成、字体子集化、配置打包三个工具。

### 3. 修改内容总览（Diff Checklist）

- [x] **新增** 七张 JSON 配置表：含 GDD §5.2 成长表（0/800/3500 金，15/35/70 客，难度 1.0/1.4/2.0）
- [x] **新增** `FormulaLib`：S_decay = S_base + (W/W_max)×1.5；Damage×100/(100+Def)；A=Σ(D_i×α)×(1+β)
- [x] **新增** 常驻 ManagerScene + 运行时叠加 BistroScene/ExpeditionScene（不走场景文件，规避 WebGL 卡顿）
- [x] **新增** 白天经营全流程（吸引力→刷客间隔、耐心=基础/难度因子、快速上菜小费、帮厨自动开火）
- [x] **新增** 黄昏五页签结算（对账/研发/采购/人事/战备）与两种夜间行动
- [x] **新增** 夜晚横版探索（负重-饱食博弈、饥饿掉血、昏厥丢 50% 战利品、天亮强制收队）
- [x] **新增** 研发系统：风味求和匹配；**未解锁菜谱优先于已解锁**（防"低需求已知菜截胡"，双端同修）
- [x] **新增** 中文字体子集（NotoSansSC 1064 字 / 185KB）解决 WebGL 无系统字体问题
- [x] **新增** 双端自动化验证：Unity EditMode 测试 + Node 25 断言 + 无头闭环冒烟

### 4. 防御性编程要点（Unity/WebGL 专项）

- 所有 `Resources.Load`/配置查询判空降级，缺表报错不崩溃；`GetShopLevel` 对存档越级夹取；
- `GameClock` 对 0 时间倍率、`FormulaLib` 对 W_max=0 / 负 Def / 除零全部钳制（有测试锁定）;
- 顾客/灶台/魔物统一由导演集中 Tick，黄昏一键冻结，规避散落 Update 的时序竞态；
- 订单的食材在"开火时"二次校验并扣除（下单到开火之间库存可能被并行订单消耗）；
- 转场期间重复请求、重复进入阶段（`Phase` 闸门）、单例双开（`EnsureExists`）均有防重；
- WebGL 专项：PlayerPrefs 写后强制 `Save()` 落 IndexedDB；不依赖系统字体；旧输入系统；
  `CreateDynamicFontFromOSFont` 用 `#if !UNITY_WEBGL` 隔离；纹理生成后 `makeNoLongerReadable` 释放 CPU 副本。

### 5. 验证记录（本容器无 Unity 编辑器，采用镜像验证策略）

| 验证项 | 方式 | 结果 |
|---|---|---|
| GDD 公式/配置/研发契约 | `node webdemo/test.mjs` | **25/25 通过** |
| 完整闭环（日→昏→夜→晨→次日） | `node webdemo/smoke.mjs`（DOM 桩无头驱动） | **通过，零异常** |
| JS 语法 | `node --check` ×4 文件 | 通过 |
| GUID 引用链（场景→脚本、构建表→场景） | `scripts/gen_unity_meta.py` 校验 | 一致 |
| Unity 端编译与 EditMode 测试 | 需本地 Unity 6 打开后执行 Test Runner | **待本地验证** |

### 6. 已知限制与下一步

1. **待本地 Unity 验证**：本次 C# 未经过真实编译（容器无 Unity）；镜像端已验证全部逻辑，
   C# 若有笔误属编译期即可暴露的低风险问题。
2. URP/2D 光照、Spine、SO 导入管线、羁绊/剧情 NPC、突发事件 → 见 README「已裁剪项」。
3. 建议下一迭代：URP 迁移 + 2D 点光（火把/提灯）、顾客好感度落库、探险区域随店级解锁分层。
