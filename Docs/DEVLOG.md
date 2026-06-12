# 开发日志（遵循《AI 游戏开发协同与代码追踪标准 v1.1》）

---

## 2026-06-13（十一）· 角色纵深行走（z 轴可行走带）+ 背景提亮

### 1. 本次修改目标

让 3D 舞台的纵深成为角色真正占用的空间：可行走带 z∈[0,1.3]（地板条带留边）。
老板 W/S（或↑↓）前后走、A/D 左右走；采集员巡场随机换纵深、疲劳睡觉自动
走到床那条深度（z=0.8，正好躺床上）。背景观感"发闷过深"通过提亮修正。

### 2. 涉及文件及职责变动

- **`Bistro/StaffAgent.cs`**：
  - 常量 `DepthMin=0 / DepthMax=1.3 / BedDepth=0.8`，字段 `_targetZ`；
  - `MoveTowards` 升级为三维寻路（x/y + 纵深），到达判定改 Vector3 距离；
  - 巡场换目标时随机抽纵深；进入休息态 `_targetZ=BedDepth`（睡上床）；
  - `ManualMove(dirX, dirZ, dt)`：双轴操控（纵深速度 2.2，纯纵深移动不改朝向）；
  - `PlaceAt` 保留放置时的纵深（放哪条深度留哪）。
- **`Bistro/BistroCameraController.cs`**：老板操控读 W/S/↑/↓ 作纵深轴。
- **`Bistro/Bistro3DStage.cs`**：主光 1.05→1.15、环境光 0.45 灰→0.58 暖，
  纵深处墙面不再发闷。
- 深度排序零代码：Spine 网格与精灵同在透明队列按相机距离排序，
  近遮远自动成立；顾客路径保持 z=0 不受影响。

### 3. 验证记录（Unity MCP Play 实测）

| 流程 | 结果 |
|---|---|
| 纵深操控 | 老板 W 键 0.6s 走到 z=1.30 顶到上限钳制；斜向（D+S）x/z 同步、朝向不被纵深干扰 |
| 深度排序 | 老板走到深处自动绘制在餐桌后方，透视近大远小成立（截图 depth_walk_bright.png） |
| 背景亮度 | 环境光提亮后纵深墙面通透，金色老板水晶清晰可辨 |
| 回归 | 编译 0 错误；EditMode **20/20** |

> 备注：睡觉走床深度依赖宿舍床摆位 z=0.85（Bistro3DStage）；2D 回退布景下
> 纵深仍可用（只是没有 3D 房间衬托，视差不明显）。

---

## 2026-06-13（十）· 长按拖拽员工 + 老板亲自操控

### 1. 本次修改目标

Sims 式直接干预：长按（0.45s）或按住拖动把员工提到空中、跟手移动、松手
落地安置（岗位/巡场锚点随迁）；创始伙伴正名为"老板"（玩家自身角色），
老板被选中时方向键直接操控走位，选中标识用金色区别于员工的绿色。

### 2. 涉及文件及职责变动

- **`Bistro/StaffAgent.cs`**：
  - `IsBoss`（id==custom_founder）/`IsHeld`/`PlayerControlled` 三状态；
    Tick 顶部短路（被提起=全停；老板操控=AI 让位）；
  - `SetHeld`（提起播 Sit 悬空感，放下回 Relax，隐藏 Zzz）、`DragTo`（跟手）、
    `PlaceAt`（落地 + _home/_target 随迁 + 落地小弹）、
    `ManualMove`（方向键走位：Move 动画 + 朝向镜像 + 店内范围钳制）。
- **`Bistro/BistroCameraController.cs`**：
  - 鼠标按下先射线判定——按在员工身上走"点选/长按提起"分支，按在空地走
    "镜头平移"分支（互不干扰）；长按 0.45s 或移动 >14px 触发提起，
    跟手坐标经角色平面（z=0）反投影（正交/透视通用），高度钳制在空中带；
  - `UpdateKeys`：老板被选中时 A/D/←/→ 操控老板（镜头本就在跟随），
    否则保持镜头平移语义；
  - Select/Deselect 维护 `PlayerControlled`；SelectionMarker 增加
    老板金色（员工绿色）。
- **`UI/FounderPanel.cs`**：displayName "（创始伙伴）"→"（老板）"。

### 3. 验证记录（Unity MCP Play 实测，存档"阿布"）

| 流程 | 结果 |
|---|---|
| 老板识别 | 阿布[老板]=IsBoss；选中 → 金色水晶 + PlayerControlled=true |
| 方向键操控 | ManualMove 右行 0.5s 位移 1.30m、anim=Move、朝向正确；松键回 Relax |
| 提起 | 悬空 y=0.3、anim=Sit、Tick 全停（截图 boss_lift_drag_v2.png：阿布悬在半空） |
| 放下 | 落地 y=-1.6、anim=Relax、岗位锚点随迁 |
| 回归 | 编译 0 错误；EditMode **20/20**（含上轮相机改动一并补跑） |

> 备注：被提起的员工松手永远落回 GroundY（不会卡在空中/家具上）；
> 黄昏冻结/面板暂停期间拖拽输入同样冻结。

---

## 2026-06-13（九）· 基建式透视相机 + Sims 式点选员工/镜头跟随

### 1. 本次修改目标

白天经营视图升级为明日方舟基建式 3D 视角：透视相机带纵深视差、拖拽/键盘
平移、滚轮推拉；并加入模拟人生式交互——点选员工头顶亮旋转绿水晶标识，
镜头平滑跟随并轻微推近，点空地取消。

### 2. 涉及文件及职责变动

- **`Core/CameraRig.cs`**：Follow 参数化——新增 maxX 右边界与 offsetX
  构图偏移（白天选人居中 offset=0；夜晚保持原 +1.5 让前方）。
- **`Bistro/BistroCameraController.cs`**（新增）：
  - 3D 舞台在场 → 透视相机（FOV 50 + 3° 俯角，机位 z=-10.5）；
    房间纵深（z 0→10）在平移时产生近快远慢的自然视差；
  - 左键拖拽平移（9px 阈值区分点选）、A/D 键平移、滚轮推拉（-13~-6）；
  - 射线点选 StaffAgent → SelectionMarker（双交叉菱形绿水晶自转+浮动+
    柔光，名牌上方）+ CamRig.Follow 居中跟随 + 选中推近 2.2m；
  - UI 上的点击经 EventSystem 屏蔽；黄昏/面板暂停时输入冻结；
  - OnDestroy 恢复相机原状（正交/size/旋转/z），夜晚与菜单零影响。
- **`Bistro/StaffAgent.cs`**：加 BoxCollider（0.9×1.8×0.8）供射线点选。
- **`Bistro/BistroDirector.cs`**：记录 `_stage3D` 并在 Init 末挂控制器。

### 3. 验证记录（Unity MCP Play 实测，用户实玩存档"阿布"）

| 流程 | 结果 |
|---|---|
| 透视模式 | ortho=False / FOV 50 / 3° 俯角生效；拖拽与滚轮实时响应（机位 x/z 随用户操作变化） |
| 点选+跟随 | Select(阿布) → 头顶绿水晶 + 镜头居中推近，3D 床/橱柜纵深感明显（截图 bistro3d_select_follow.png） |
| 回归 | 编译 0 错误；EditMode 因用户 Play 中暂未重跑（本轮全为表现层，无数值/逻辑变更，测试不覆盖相机路径） |

> 备注：2D 回退布景下控制器仍可点选/跟随（正交模式），仅不启用透视与推拉。

---

## 2026-06-13（八）· 餐厅 3D 实景舞台（2.5D：3D 房间 + Spine 小人）

### 1. 本次修改目标

把 Ark3D 管线的产物接进游戏：白天经营场景改为 3D 实景——房间壳体
（墙/地板/门）+ 3D 家具（餐桌/宿舍床/床头柜/厨柜/书柜），Spine 小人
在台前演，达成 GDD 的 2.5D 形态。资产缺失自动回退原 2D 手绘背景。

### 2. 涉及文件及职责变动

- **`Editor/Ark3D/Ark3DBatchImporter.cs`**：Prefab 输出改到
  `Assets/Ark3D/Resources/Ark3D/Prefabs/`（场景全代码构建，运行时必须
  Resources.Load）；自动清理旧版非 Resources 输出。
- **`Bistro/Bistro3DStage.cs`**（新增）：3D 舞台搭建——房间定位/旋转、
  暖色方向光 + Flat 环境光兜底、家具按逻辑锚点摆放（包围盒贴地 +
  目标宽度等比缩放）。`Available`/`Build` 返回 false 即回退。
- **`Bistro/BistroDirector.cs`**：`_tablePos`/`_restSpot` 坐标锚点先行
  （2D/3D 布景共用，客人入座/员工休憩逻辑零改动）；3D 舞台成功则跳过
  全部 2D 室内手绘，店外街景与排队不变。

### 3. 踩坑记录（坐标系三连）

1. 游戏相机在 -z 朝 +z 看，房间模型面朝 +z → 房间须绕 Y 转 180°；
2. 转 180° 后灯光也反了（照向镜头），方向光改为从镜头侧往 +z 打，
   另补 Flat 环境光（代码建场景默认环境光近黑）；
3. **Unity 导入 OBJ 自动镜像 X 轴**（右手系→左手系），叠加 180° 旋转后
   x 回到原值——房间整体跑到屏幕右侧画外。根位置改为 -8.2：房间覆盖
   全屏，左门恰好落在宿舍角旁（休息室之门，歪打正着）。

### 4. 验证记录（Unity MCP Play 实测）

| 流程 | 结果 |
|---|---|
| 3D 舞台 | Stage3D 10 子物体：房间+灯+4 桌+床+床头柜+厨柜+书柜（截图 bistro3d_day_v3.png） |
| 同台混演 | Spine 员工/顾客踩在 3D 地板上，气泡/名牌/耐心条正常叠加 |
| 宿舍角 | 加恩疲劳 100 → 走到床位(-6.5)播 Sleep；莉珂灶台开火播 Interact |
| 完整营业日 | 第 3 天在 3D 舞台跑完整天：接待 8 位、净利 30、黄昏结算正常弹出 |
| 回归 | 编译 0 错误；EditMode 20/20（缺 Ark3D 资产的环境自动回退 2D，测试不依赖素材） |

> 备注：3D 素材仍为第三方提取物（gitignore），他人拉仓库看到的是 2D 回退
> 布景，功能完全一致；上架前替换自有美术后重跑导入管线即可。

---

## 2026-06-13（七）· 基建 3D 资源批量导入管线（OBJ→材质→Prefab）

### 1. 本次修改目标

用户提供一批基建 AssetBundle 拆出的 3D 资源（项目根 `3dobj/diy/arts/`：
furni 家具 25 件、room 房间 2 个、贴图 16 张，OBJ 无 mtllib）。建立一键导入
管线：导入规则统一、按命名前缀自动配材质、家具/房间自动出 Prefab。
渲染管线确认为**内置（Built-in）**→ Standard 着色器 + `_MainTex`
（脚本已做 URP 探测，迁移后重跑即自动切 `URP/Lit` + `_BaseMap`）。

### 2. 涉及文件及职责变动

- **`Editor/Ark3D/Ark3DAssetPostprocessor.cs`**（新增）：仅作用于 `Assets/Ark3D/`——
  OBJ：法线 Import、不生成 Lightmap UV、不导灯光/相机/动画、materialImportMode=None；
  **Scale 分目录**：furni ×100（顶点 ~0.02）、room ×1（顶点本身就是米级 ~34×4.5，
  踩坑：最初统一 ×100 导致房间 3600m）。PNG：Default + sRGB；TX_Shadow* 开 Alpha 透明。
- **`Editor/Ark3D/Ark3DBatchImporter.cs`**（新增）：菜单
  「Bistro/导入明日方舟 3D 家具」一键执行——拷贝（跳过 TT_*.ab.json）→
  前缀配对（文件夹 ikeabw_ikea ↔ 贴图 TX_IKEAbw_IKEA_*，小写相等即配）→
  生成材质（furni_01/room_01/floor_01 → _MainTex；影子=Standard 透明 Fade）→
  家具 Prefab（主网格+shadow*.obj 透明叠加子物体）→
  房间 Prefab（floor/frame/room + Door_L/Door_R 独立可控子物体；
  dormitory 每主题一变体，empty 用 TX_room_empty_01）。
- **`.gitignore`**：`3dobj/`、`Assets/Ark3D/`（第三方素材不入库）。

### 3. 验证记录（Unity MCP 编辑器实测）

| 流程 | 结果 |
|---|---|
| 一键导入 | 家具 Prefab×25、房间 Prefab×3（宿舍×2 主题+空房）、材质×8，零缺贴图警告 |
| 尺度 | 房间 36×12×10m，床 4.9×7m——6×2 格宿舍约容 6~7 件大家具，比例正确 |
| 材质配对 | floor/frame/room/门/家具/影子各就各位（渲染器逐一核对） |
| 朝向坑 | 家具源模型朝 -z、房间朝 +z → 家具 Prefab 内统一转 180° 对齐（截图 ark3d_room_preview_v3.png） |
| 回归 | 编译 0 错误；EditMode 20/20 |

> 备注：① OBJ 无 mtllib 且 ab.json 的材质为外部引用，furni_01/02 变体绑定
> 无法自动恢复，默认 01，个别家具如贴错在材质球上换 02 即可；
> ② 素材为第三方提取物，仅本地验证，不得随版本发行。

---

## 2026-06-13（六）· 顾客 + 夜战主厨全面 Spine 化

### 1. 本次修改目标

继店员之后，把剩下两类角色也换成骨骼小人：顾客用 7 选 1 随机换装池
（与店员不撞脸），夜间探险主厨用战斗正面骨骼（移动/攻击/受击/昏厥全接动画）。
全部走 balance.json 配置驱动，留空即回退原拼装造型。

### 2. 涉及文件及职责变动

- **素材**：新增 7 套（6 套基建换装皮肤作顾客池 + 阿米娅战斗正面），
  全部单页贴图，gitignore 规则已覆盖（ak_*）。
- **`ConfigModels.cs` + `balance.json`**：`customerSpineLooks`（顾客随机外观池）、
  `nightPlayerSpineLook`（夜战主厨骨骼名）。
- **`Util/SpineActor.cs`**：抽出 `Resolve` 三级动画名解析；新增 `PlayOnceThen`
  （一次性动画播完自动接回循环——攻击/受击类专用，名字不存在时不做兜底以免误切）。
- **`Bistro/CustomerAgent.cs`**：进店随机抽池生成骨骼小人；行为映射
  走路 `Move`/排队·看菜单 `Relax`/**用餐 `Sit`**，方向镜像；色块小人路径完整保留。
- **`Expedition/ExpeditionPlayer.cs`**：战斗骨骼接入——待机/移动 `Idle`
  （阿米娅系术师无 Move 动画，滑步可接受）、攻击 `Attack` 单次（`_animLock`
  按动画时长锁定，期间不被移动状态打断）、昏厥 `Die`、受击红闪改为压
  骨骼 G/B 通道（拼装路径仍走 SpriteRenderer tint）。

### 3. 验证记录（Unity MCP Play 实测）

| 流程 | 结果 |
|---|---|
| 资产导入 | 7/7 全自动 ingest（JSON 路径），v3.5.51 解析全过 |
| 白天顾客 | 同屏命中三映射：Deciding=Relax / WalkToQueue=Move / Eating=Sit；随机池抽中 aglina_boc、amgoat、amiya_test 等（截图 ak_spine_customers_day.png） |
| 夜战主厨 | ak_amiya_battle 上身（世界高 1.64m）；Attack 单次 1.77s loop=False 自动接回 Idle（截图 ak_spine_night_expedition.png） |
| 回归 | 编译 0 错误；EditMode 20/20；webdemo 28/28（config 已重新生成） |

> 备注：与上轮相同——ak_* 为第三方提取素材，仅本地验证；上架前以同管线
> 替换为自有美术即可，代码零改动。

---

## 2026-06-13（五）· 真实 Spine 3.5.51 小人上岗（6 角色 + 动画状态机）

### 1. 本次修改目标

用户放入 6 套真实 Spine 3.5.51 基建小人（第三方提取素材，本地验证用），
接到员工系统：三名可雇佣员工 + 创始伙伴全部换成骨骼小人，并按行为驱动
Relax/Move/Interact/Sleep 动画与朝向镜像。

### 2. 涉及文件及职责变动

- **素材整理**：55MB/2723 张图的原始库移出 Assets 到 `SpineRaw/`（避免全量
  打包+导入）；每角色仅取默认皮肤基建小人三件套规范化拷入
  `Resources/Spine/ak_<代号>/`。**ak_* 与 SpineRaw/ 已入 .gitignore——
  版权素材禁止入库/发行**。
- **`Util/SpineActor.cs`**：支持子文件夹布局（`Spine/<名>/<名>_SkeletonData`）；
  `PlayIfExists` 升级为 精确→大小写不敏感→首个动画 三级兜底。
- **`Bistro/StaffAgent.cs`**：Spine 小人动画状态机——巡场 `Move`（按方向
  FlipX 镜像）/待机 `Relax`/帮厨开火 `Interact`/疲劳睡沙发 `Sleep`；
  统一 0.75 体型缩放（原始 ~2.1m→~1.6m，名牌 1.95m 不被挡）。
- **`staff.json`**：莉珂=spine:ak_amiya（兔耳对兔耳）、加恩=spine:ak_peacok、
  薇尔=spine:ak_platnm；`FounderPanel`：帮厨=ak_plosis、采集员=ak_aglina。
- **`Docs/SPINE.md`**：补关键坑——本运行时（3.5 分支末期）二进制读取器混入
  3.6 双色字段，读标准 3.5.51 `.skel` 必越界，**必须用 .json**；
  及 `_SkeletonData` 缺失时的手动补建方法。

### 3. 验证记录（Unity MCP Play 实测）

| 流程 | 结果 |
|---|---|
| .skel 二进制 | 6/6 解析失败（IndexOutOfBounds@SkeletonBinary:179，槽位双色字段错位）→ 改用 .json |
| .json 解析 | 6/6 成功，v3.5.51，动画集 Default/Interact/Move/Relax/Sit/Sleep |
| 店内上岗 | 三员工实体=对应骨骼小人，世界身高 1.5~1.7m，名牌正常露出（截图 ak_spine_staff_v2_scaled.png） |
| 动画状态机 | 薇尔巡场 Move、加恩到位 Relax+朝左镜像保持、莉珂守灶台 Relax；切换无轨道重置抖动 |
| 回归 | 编译 0 错误；webdemo 28/28（config 重新生成，look 字段仅 Unity 端消费） |

> 备注：素材为第三方游戏提取物，仅作本地玩法/管线验证；上架前必须替换为
> 自有美术（管线已就绪，三件套即放即用）。

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
