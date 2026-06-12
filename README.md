# Bistro & Burrow（小馆与地穴）

> 白天经营魔物食材小馆，夜晚下地穴猎取食材 —— 基于 Notion 策划案 GDD v1.0.0 的 **Unity 6 WebGL 轻量垂直切片**。

本仓库包含两个互为镜像、**共用同一份数值配置**的可玩实现：

| 目录 | 说明 | 怎么验证 |
|---|---|---|
| 仓库根（`Assets/` 等） | Unity 6 正式工程（WebGL 目标） | 用 Unity 6 打开 → Play / 一键构建 WebGL |
| `webdemo/` | 纯网页镜像原型（零依赖） | **浏览器直接打开 `webdemo/index.html` 即玩** |

两者的所有数值（公式、菜谱、魔物、成长表）都来自 `Assets/Resources/Configs/*.json` 这一个源头，改表后两端同时生效。

---

## 一、快速验证（无需 Unity）

```bash
# 方式 A：直接双击 webdemo/index.html（无需服务器）
# 方式 B：本地起服务
cd webdemo && python3 -m http.server 8000   # 浏览器开 http://localhost:8000

# 自动化验证（Node ≥ 18）
node webdemo/test.mjs    # 25 项断言：GDD 公式 / 配置契约 / 玩法模拟
node webdemo/smoke.mjs   # 无头跑通完整闭环：日→昏→夜→晨
```

**玩法**（与 Unity 端一致）：
- **主菜单**（Unity 端）：继续营业（读档，含存档摘要）/ 开新店（覆盖旧档需二次确认）；
- **白天（60 现实秒 = 游戏内 08:00–18:00）**：顾客排队入座点单，**点击头顶气泡开火做菜**，上菜越快小费越多；顶栏「员工」随时打开员工管理（招聘/派遣，打开时营业暂停）；雇了帮厨后自动开火、飞盘上菜；
- **黄昏结算**：对账 → 菜品研发（风味矩阵融合）→ 店外采购（吸引力引擎）→ 人事派遣 → 战前用餐（以吃代练 Def）→ 亲自下地穴 / 派遣就寝 / 保存回主菜单；
- **夜晚（78 现实秒）**：横版探索，`A/D` 移动、`空格` 跳、`J/点击` 攻击、门口 `E` 收队。背包越重饱食度掉越快，饿到 0 持续掉血，昏厥丢一半战利品（前 2 晚有新手减伤保护）；
- **黎明**：派遣结算、疲劳恢复（疲劳≥80 的采集员次日会瘫在休憩沙发上）、自动存档（浏览器 localStorage / Unity PlayerPrefs→IndexedDB；黄昏也会自动落盘）。

## 二、Unity 工程

- **版本**：Unity **6.3 LTS（6000.3.x）**（首次打开会自动还原 Library 与默认 ProjectSettings；其他 Unity 6 分支也可打开，勿用 2022 及更早版本）
- **运行**：打开 `Assets/Scenes/ManagerScene.unity` → Play（即使打开空场景也能自举启动，见 `Bootstrap.cs`）
- **WebGL 构建**：菜单 **Bistro → 构建 WebGL（Brotli 压缩）**（自动应用 GDD §6.3 指标：Brotli + High Stripping + 最低异常支持 + Gamma）
- **EditMode 测试**：Window → General → Test Runner → EditMode → Run All（与 `webdemo/test.mjs` 同一组数值契约）

### 架构（对应 GDD §6 工程规划）

```
ManagerScene（常驻，Build Settings 中唯一场景）
 └─ Bootstrap → GameManager（单例状态机：Day→Dusk→Night→Dawn）
     ├─ ConfigService     Resources/Configs/*.json → 只读配置（数据驱动，零硬编码）
     ├─ FormulaLib        GDD §4/§5 全部公式（纯静态，可单测）
     ├─ PlayerState/SaveSystem  存档（PlayerPrefs，WebGL 落 IndexedDB）
     ├─ CameraRig / UIRoot / SettlementPanel   全局相机与 UI
     ├─ BistroScene（白天动态叠加，黄昏 UnloadSceneAsync 释放）
     │   └─ BistroDirector + CustomerAgent + StoveStation
     └─ ExpeditionScene（夜晚动态叠加，黎明卸载）
         └─ ExpeditionDirector + ExpeditionPlayer + MonsterAgent + IngredientPickup
```

要点：
- **子场景不走场景文件**：`SceneManager.CreateScene` 运行时创建 + 代码化构建全部内容，规避 WebGL 场景反序列化卡顿与场景文件合并冲突；
- **零美术资产**：所有视觉为 `SpriteFactory` 运行时程序化色块（后续接 Spine/正式美术只换工厂产出）；
- **中文 WebGL 字体**：`Assets/Resources/Fonts/NotoSansSC-Sub.otf`（185KB，OFL 协议，仅含项目用到的 1064 字符）。**新增 UI 文案后**需重跑 `python3 scripts/subset_font.py <完整字体>` 重新子集化，否则新字符在 WebGL 端不显示（编辑器会回退系统字体，不易察觉）。

### 配置表（唯一数值源）

`Assets/Resources/Configs/`：`balance.json`（时间/生存/经济参数）、`ingredients.json`（风味矩阵/负重）、`recipes.json`（需求矩阵/售价/Buff）、`monsters.json`、`decor.json`（吸引力 D_i）、`staff.json`、`shop_levels.json`（GDD §5.2 成长表）。

改表后同步网页端：`node scripts/build_webdemo_config.mjs`

### 脚本工具

| 命令 | 作用 |
|---|---|
| `python3 scripts/gen_unity_meta.py` | 为新增资源生成确定性 GUID 的 .meta 并回填引用（新增文件后跑一次） |
| `python3 scripts/subset_font.py <字体>` | 重新子集化中文字体 |
| `node scripts/build_webdemo_config.mjs` | JSON 配置 → webdemo/config.js |

## 三、与 GDD 的差异 / 已裁剪项（MVP 范围声明）

| GDD 条目 | 本切片处理 | 原因 |
|---|---|---|
| URP + Sprite-Lit + 2D 光照 | 暂用内置管线 | 程序化色块阶段无光照需求；URP 资产留给编辑器内创建（手写 YAML 风险高）。迁移点已隔离在 `SpriteFactory`/相机 |
| Spine 骨骼小人 | 程序化色块小人 | 零二进制资产、包体最小化 |
| Excel→JSON→ScriptableObject 导入管线 | 运行时直读 JSON TextAsset | MVP 等价且 WebGL 安全；SO 化是纯增量工作 |
| 顾客好感/羁绊、剧情 NPC、突发事件、黑市、二楼扩建、龙族区域 | 配置已留位（如 `dragon_loin`），玩法未实装 | 闭环优先 |
| 白天拖拽员工去休憩室 | 疲劳只来自夜间派遣 | 简化首版排班 |

## 四、版权说明

- 字体：Noto Sans SC（SIL Open Font License 1.1），已按许可证以子集形式内嵌；
- 其余代码与配置均为本仓库原创；游戏设定为受《迷宫饭》启发的原创内容，未使用任何原作素材。
