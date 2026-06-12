# 开发日志（遵循《AI 游戏开发协同与代码追踪标准 v1.1》）

---

## 2026-06-13（四）· 接入 Spine 3.5.51 运行时（骨骼小人管线）

### 1. 本次修改目标

让项目能直接使用 Spine **3.5.51** 导出的骨骼小人：内置官方 spine-unity 3.5
运行时并移植到 Unity 6，打通"丢三件套→自动生成资产→运行时按名生成小人"的
完整管线，员工造型支持 `spine:` 前缀零代码切换。

### 2. 涉及文件及职责变动

- **`Assets/Spine/`**（新增）：spine-runtimes 仓库 3.5 分支官方源码
  （spine-csharp + spine-unity，105 个 .cs）。Unity 6 移植仅 2 处：
  `SpineMesh.cs` 两个 `Color32` 局部变量补 `default` 初始化（CS0165）、
  `SkeletonDataAssetInspector.cs` 移除 `new Texture()`（CS0122）。
- **程序集重组**：原版 10 个散落的 Editor 目录集中到 `Assets/Spine/Editor/`，
  新增 `Spine.Runtime.asmdef` + `Spine.Editor.asmdef`；`Game.Runtime` 引用
  `Spine.Runtime`（asmdef 体系下 Editor 目录不再特殊处理，必须独立程序集）。
- **`Util/SpineActor.cs`**（新增）：运行时加载器——`Spawn(名字, …)` 从
  `Resources/Spine/<名字>_SkeletonData` 生成 SkeletonAnimation；
  `PlayIfExists` 安全播动画；`Exists` 供回退判断。
- **`Bistro/StaffAgent.cs`**：`look = "spine:<名字>"` → 员工实体改用 Spine
  小人渲染（默认循环 idle），资产缺失自动回退拼装造型。
- **`Assets/Resources/Spine/testbun.*`**（新增）：手写 3.5.51 格式验证资源
  （奶油小兔：3 骨骼/2 插槽/idle+hop 双动画 + libgdx 图集 + 代码生成贴图）。
- **`Docs/SPINE.md`**（新增）：资源放置约定（.atlas 须改 .atlas.txt）、
  两种使用方式、版本兼容警告（3.5 数据≠4.x 运行时）。

### 3. 验证记录（Unity MCP Play 实测）

| 流程 | 结果 |
|---|---|
| 编译移植 | 2 个错误修复后 0 错误（余为过期 API 警告，无害） |
| 自动导入 | testbun 三件套 → `_Atlas/_Material/_SkeletonData` 全自动生成 |
| 数据解析 | `SkeletonData version=3.5.51`，3 骨骼/2 插槽/idle 1.2s + hop 0.4s |
| 运行时生成 | Play 中 `SpineActor.Spawn` 出网格 8 顶点，店内截图渲染正确（spine35_testbun_in_bistro.png） |
| 动画驱动 | t=0.23s 时头部 4.55°，与关键帧插值理论值 4.6° 一致；身体 squash 0.985 生效 |
| 回归 | EditMode **20/20**；webdemo **28/28**（镜像不渲染 Spine，纯视觉层差异） |

> 备注：3.5 与 4.x 运行时互不兼容，本项目锁定 3.5.51；若未来要换 4.x 小人，
> 需整体替换 `Assets/Spine/` 并重导所有骨骼资源。

---

## 2026-06-13（三）· 三档位存档 + 创始伙伴定制（取名/职业/属性加点）

### 1. 本次修改目标

存档系统升级为 3 个独立档位（选档/删档/旧档迁移）；新开局流程加入"创始伙伴"
定制——玩家给第一位员工取名、选职业、自由分配 10 点属性（勤快/耐力）、选配色，
属性真实作用于玩法（开火速度/派遣产量/疲劳消耗）。

### 2. 涉及文件及职责变动

- **`Core/SaveSystem.cs`**（重写）：3 档位独立键 + `MigrateLegacySave`（旧单档自动
  迁入 1 号位）+ `Peek(slot)` 只读摘要。
- **`Core/PlayerState.cs`**：`SaveData.customStaff`（自定义员工随档持久化）；
  `GetStaffDef(id)` 统一解析配置员工与自定义员工——所有"按已雇佣 id 取定义"
  的调用点（工资/职能/派遣/实体生成/花名册）改走此入口。
- **`Core/ConfigModels.cs` + `staff.json`**：StaffDef 新增 `diligence/stamina`
  （配置员工各有预设：莉珂 勤5耐4 / 加恩 勤3耐6 / 薇尔 勤7耐5）。
- **`Core/FormulaLib.cs`**：三条属性公式——`AutoCookInterval`（勤10≈手速翻倍）/
  `DispatchBonusYield`（勤7→+2件）/`DispatchFatigueCost`（耐10→40变24）。
- **`Core/GameManager.cs`**：`CurrentSlot` + 档位化的 继续/新开局/回主菜单/各阶段存档；
  `StartNewGame(slot, founder)` 注入创始伙伴；派遣结算接入属性公式。
- **`UI/FounderPanel.cs`**（新增）：创始伙伴定制面板——取名输入框（经典 InputField，
  WebGL 兼容）/职业切换/±加点（总池 10、单项上限 8、剩余点实时显示）/四色配色/
  日薪随属性浮动预览（6+勤+耐）。
- **`UI/UIRoot.cs`**：主菜单改为三档位卡片（摘要/继续营业/删除二次确认/新开局）。
- **`UI/StaffRosterUi.cs`**：已雇佣区改为遍历存档花名册（自定义员工可见），
  行内展示 勤X耐Y。
- **`Bistro/BistroDirector.cs`**：自动开火间隔按最勤快帮厨计算；员工实体生成
  走 GetStaffDef。`StaffAgent` 新增 chef 造型（白厨师帽）。
- **`Util/UiFactory.cs`**：`CreateInput` 输入框构件。
- **`balance.json`**：`autoCookBaseInterval=0.9`（消灭硬编码）。

### 3. 验证记录（Unity MCP Play 实测）

| 流程 | 结果 |
|---|---|
| 旧档迁移 | 单档自动迁入档位 1，菜单摘要正确 |
| 创始伙伴全流程 | 取名"团子"/帮厨/勤7耐3/蓝色 → 开店：日薪 16（=6+7+3）、白厨师帽实体守灶台、自动开火间隔 0.577s（公式精确命中）、接待收款正常 |
| 存档往返 | 档 2 Peek：customStaff 完整序列化（职业/属性/日薪/配色） |
| 三档位菜单 | 档1/档2 摘要+继续+删除（二次确认），档3 空档新开局 |
| 回归 | 编译 0 错误；EditMode **20/20**（新增属性公式/自定义员工解析/档位隔离）；webdemo **28/28** |

> 备注：webdemo 镜像保持单档存档（仅同步属性公式与派遣效果）；多档位与创始伙伴
> 定制为 Unity 端功能。另：本轮排障发现 Game 视图设为 4K 导致截图管线空白，
> 与游戏无关（已将视图恢复 16:9）。

---

## 2026-06-13（二）· 氛围粒子 + 新手保护 + WebGL 包体实测达标

### 1. 本次修改目标

补白天的"烟火气"与夜战反馈（粒子三件套）；用数值驱动的新手保护抹平首两晚
的劝退曲线；执行首次真实 WebGL 构建验证 GDD §6.3 的 25MB 包体红线。

### 2. 涉及文件及职责变动

- **`Util/Particle.cs`**（新增）：微型自治粒子（刻意不用 ParticleSystem——场上粒子
  不过几十个，自驱 SpriteRenderer 更轻且零序列化资产）。三个预制配方：
  `Steam`（灶台蒸汽，负重力上飘）/`CoinBurst`（收款金币喷溅）/`Burst`（击杀同色碎屑）。
- **`Bistro/StoveStation.cs`**：有锅在烧时每 0.32s 冒一缕蒸汽。
- **`Bistro/BistroDirector.cs`**：收款接金币喷溅（小费单更多）。
- **`Expedition/ExpeditionDirector.cs`**：击杀接同色碎屑爆裂。
- **`Configs/balance.json` + `ConfigModels.BalanceDef`**：新增 `newbieNights=2` /
  `newbieDamageMultiplier=0.6`——前两晚受到伤害打 6 折（拟态怪 10 伤→6 伤）。
- **`Expedition/ExpeditionPlayer.TakeDamage`** 与 **`webdemo/main.js`**：双端同步应用；
  `webdemo/test.mjs` 新增契约断言（26 项全过）。
- **`README.md`**：操作说明更新（主菜单/员工面板/保存）。

### 3. 验证记录

| 验证项 | 结果 |
|---|---|
| 蒸汽/金币粒子 | Play 实测可见（灶台白雾上飘；收款金点喷溅） |
| 新手保护 | webdemo 断言：10 伤 → 6 伤 ✓ |
| **WebGL 构建** | **总包体 6.6MB（目标 ≤25MB，仅用 26% 预算）**：wasm.br 5.2MB + data.br 1.14MB（含 185KB 中文子集字体）+ loader 0.1MB |
| 回归 | 编译 0 错误；EditMode 17/17；webdemo 26/26 |

> 注：Brotli 构建需服务器正确发送 Content-Encoding（itch.io 原生支持）；
> 本地双击 index.html 无法直接运行属预期（decompressionFallback 已按包体优先关闭）。

---

## 2026-06-13 · 开始界面 + 存档管理 + 白天员工面板

### 1. 本次修改目标

补齐"作为一款完整游戏"的外壳：主菜单（继续营业/开新店）、存档管理强化、
白天随时可开的员工管理面板（招聘/派遣不再只能等黄昏）；同时修复一个布局底层 Bug。

### 2. 涉及文件及职责变动

- **`Core/GameManager.cs`**：新增 `Menu` 阶段；State 改为由主菜单决定加载
  （`StartContinueGame`/`StartNewGame`/`ReturnToMenuFromDusk`）；新增 `UIPaused`
  （员工面板打开时冻结时钟与店内模拟）；黄昏阶段自动存档；`OnApplicationQuit` 兜底存档。
- **`Core/SaveSystem.cs`**：新增 `HasSave`/`Peek`（主菜单只读摘要，不构建运行时状态）。
- **`UI/UIRoot.cs`**：主菜单界面（渐变夜空+灯光氛围/标题/存档摘要/继续营业/开新店
  **二次确认覆盖**/操作提示）；顶栏新增「员工」按钮；State 改为动态 Bind/Unbind；
  暂停时顶栏显示"已暂停"。
- **`UI/StaffRosterUi.cs`**（新增）：员工花名册共享构件——招聘（差额提示）/今晚派遣
  开关/疲劳与休息状态/派遣人数汇总。白天面板与黄昏人事页签共用，行为永不漂移。
- **`UI/StaffPanel.cs`**（新增）：白天员工管理面板。打开即 `UIPaused=true`
  （顾客耐心/灶台/生成全部冻结，玩家安心做人事决策），点遮罩或关闭按钮恢复营业。
- **`UI/SettlementPanel.cs`**：人事页签改用共享花名册；底部新增「保存并回主菜单」。
- **`Util/UiFactory.cs`**：**修复** `VerticalGroup.childControlHeight=false` 导致子行
  按默认 100px 高排布、LayoutElement 行高被无视的问题（员工面板曾因此"丢行"，
  黄昏面板行距过松同源）。

### 3. 验证记录（Unity MCP Play 实测）

| 流程 | 结果 |
|---|---|
| 启动 → 主菜单 | Phase=Menu，State=null（按设计延迟加载）；存档摘要正确（第5天/金币424/员工3人） |
| 继续营业 | 读档进入第 5 天，员工/库存/菜谱完整恢复 |
| 员工面板 | 三名员工全员显示（在岗/可派遣/累瘫禁派），打开即暂停（顶栏"已暂停"），关闭恢复 |
| 保存并回主菜单 | 落盘 → 卸载场景 → State 清空 → 菜单摘要刷新 |
| 开新店 | 覆盖二次确认 → 全新第 1 天（金币 60/员工 0/菜谱 2/食材 3 种） |
| 回归 | 编译 0 错误；EditMode 17/17 |

---

## 2026-06-12（三）· 员工系统可见化：从"幕后数值"到"看得见的伙伴"

### 1. 本次修改目标

玩家反馈"貌似没有招聘/部署员工功能"——实际逻辑早已在（雇佣/派遣/帮厨自动开火），
但员工没有实体，玩家零感知。本轮把员工做成看得见的店内角色，并补上端菜动画。

### 2. 涉及文件及职责变动

- **`Bistro/StaffAgent.cs`**（新增）：店内员工实体。Cook 常驻灶台（有锅时高频律动），
  Gatherer 店内巡场；疲劳≥派遣上限时走向休息沙发趴下亮 "Zzz…"（GDD §3.1 休憩室）。
  造型由 staff.json 的 `colorHex + look` 驱动（rabbit=兔耳+围裙 / adventurer=额带+背剑 /
  hunter=兜帽），头顶世界空间短名名牌。由导演集中 Tick，黄昏随场冻结。
- **`Bistro/FlyingDish.cs`**（新增）：端菜飞盘——出锅的菜沿抛物线从灶台飞向顾客，
  落点才触发 Serve/音效/飘字；顾客中途离席自动作废。
- **`Bistro/StoveStation.cs`**：出餐改走 FlyingDish；新增 `JobCount`（帮厨干活动画信号）。
- **`Bistro/BistroDirector.cs`**：开店时按存档生成员工实体；新增休憩沙发（厨房侧角落，
  替换原左侧盆栽）；StaffTick 接入主循环。
- **`Configs/staff.json` + `ConfigModels.StaffDef`**：新增 `shortName/look` 字段；
  新增第三位员工"风行猎手·薇尔"（Gatherer，日薪 35/签约 400，拉开中期派遣深度）。
- **`UI/SettlementPanel.cs`**：雇佣行显示差额提示（"还差 X 金币"）；入职 Toast 注明"明日到岗"。

### 3. 验证记录（Unity MCP Play 实测）

| 验证项 | 结果 |
|---|---|
| 编译 / EditMode | 0 错误；17/17 通过；webdemo 25 断言同步通过 |
| 雇佣→到岗 | 黄昏雇佣 3 人 → 次日开店全员实体出现（莉珂守灶台/加恩薇尔巡场/名牌正常） |
| 帮厨自动化 | 全程零点击，自动开火→飞盘上菜→收款（接待 3 营收 36 起步，金币 104→474） |
| 派遣→疲劳→休息 | 薇尔派遣过夜疲劳 100 → 次日自动走向沙发趴下亮 Zzz（截图 v3_staff_resting） |
| 压力闭环 | 连续自动经营耗尽食材 → 顾客"没想吃的……"离店，夜采动机成立 |

### 4. 下一步候选

烹饪蒸汽/金币粒子、首夜新手保护、WebGL 构建包体实测、URP+2D 点光迁移。

---

## 2026-06-12（二）· 表现力升级 Pass 1：对标 Steam 独游的画面/手感/音效

### 1. 本次修改目标

在已验证的核心闭环之上做第一轮"游戏感"升级：暖色酒馆氛围、夜景层次、
角色表现力、打击反馈与程序化音效；同时修复两个真实 Bug（编辑器域重载 NRE、
结算面板布局错位）。全程通过 Unity MCP 在编辑器内实测验证（Play 模式跑通
3 个游戏日，含玩家真人试玩的夜间战斗）。

### 2. 涉及文件及职责变动

- **`Util/SpriteFactory.cs`**（扩展）：新增 `GradientRect`（垂直渐变）/`RadialGlow`
  （径向辉光）/`SoftShadow`（椭圆软阴影）三件套——程序化美术从"平色块"升级为
  "受光体积 + 光池 + 落地感"。
- **`Util/Juice.cs`**（新增）：零依赖缓动库（PopIn 回弹入场 / Pulse 脉冲 / Shake 镜头震动），
  协程实现，逐帧判空防目标销毁。
- **`Util/SfxSynth.cs`**（新增）：运行时 PCM 合成 8 种音效（开火/上菜/收钱/命中/受击/
  拾取/解锁/开店铃），正弦+谐波+指数包络，零音频资产，WebGL 兼容。
- **`Bistro/BistroDirector.cs`**：场景构图全面重做（墙裙/腰线/木地板板缝/窗光/吊灯×3
  光池/挂画/菜单黑板/厨房挡板/盆栽/雨棚/悬挂招牌/远景屋脊/石板路/排队地贴）；
  事件接入音效；**修复**域重载后 `_gm.State` 空引用（Update 增加判空守卫）。
- **`Bistro/CustomerAgent.cs`**：顾客升级——眼睛、软阴影、渐变身体、入场回弹、
  上菜脉冲、用餐点头律动。
- **`Expedition/ExpeditionDirector.cs`**：夜景重做（星空 36 颗/月亮+月晕/双层视差雾带/
  草簇/家门灯塔光晕）；命中音效+受击脉冲+击杀震屏；HUD 下移让出顶栏。
- **`Expedition/Firefly.cs`**（新增）：双频正弦漂移 + 呼吸明暗的萤火虫 ×14。
- **`Expedition/ExpeditionPlayer.cs` / `MonsterAgent.cs`**：脚下阴影、双眼造型、
  受击红闪 **tint 基准修复**（乘法 tint 下旧写法会越闪越暗）、受击震屏+音效。
- **`UI/UiFactory.cs`**：**修复** `HorizontalGroup` 的 `childControlWidth=false` 导致
  LayoutElement 尺寸不被采纳、按钮叠压错位的问题。
- **`UI/SettlementPanel.cs`**：底部行动按钮居中修复、内容区 RectMask2D 防溢出、
  研发/采购/雇佣/用餐接入音效。
- **`Core/GameManager.cs` / `CameraRig.cs`**：域重载守卫；PanTo 清除跟随边界残留。
- **`Packages/manifest.json`**：正式收编 `com.coplaydev.unity-mcp`（git URL）——
  此前插件未持久化到工程，是桥接反复掉线的根因。

### 3. 验证记录（Unity MCP 编辑器内实测）

| 验证项 | 结果 |
|---|---|
| 编译 | 0 错误 0 警告 |
| EditMode 测试 | 17/17 通过（两轮回归） |
| Play 实测 | 连续 3 个游戏日闭环：开火→上菜→收钱+小费（60→104 金币）、耐心耗尽气走、黄昏五页签、夜探（真人试玩：HP 4 惊险撤退）、战利品入库（史莱姆凝胶/烬火椒）、存档跨日 |
| 截图存档 | Assets/Screenshots/v2_*.png（本地，不入库） |

### 4. 已知事项与下一步

1. 编辑器侧已通过注册表把"Play 中脚本变更"设为停止后再编译（防域重载打断）；
2. 夜间前期数值偏辣：无战前餐时拟态怪区(10 伤/次)对脆皮主厨威胁极大——符合
   GDD 风险/回报设计，但可考虑给首夜加新手保护；待玩家反馈后调表；
3. 下一轮候选：烹饪蒸汽/金币粒子、店内顾客挑剔表情、URP 迁移与 2D 点光、
   WebGL 构建包体实测。

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
