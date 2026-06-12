# Spine 3.5.51 小人接入指南

项目已内置 **spine-unity 3.5 官方运行时**（`Assets/Spine/`，对应 Spine 编辑器 3.5.x 导出格式），
在 Unity 6 下编译通过。注意：3.5 的骨骼数据**只能**由 3.5 运行时加载，4.x 运行时会拒绝读取；
反之亦然，不要把 4.x 导出的资源丢进来。

## 一、放资源（三件套）

把 Spine 3.5.x 导出的三个文件放进 `Assets/Resources/Spine/`：

| 文件 | 命名要求 |
|---|---|
| 骨骼数据 | `<名字>.json`（二进制导出则改名 `<名字>.skel.bytes`） |
| 图集 | `<名字>.atlas` **改名为** `<名字>.atlas.txt` |
| 贴图 | `<名字>.png`（与图集同目录，文件名与 atlas 内声明一致） |

切回 Unity 触发导入后，运行时会自动生成三个资产：
`<名字>_Atlas.asset`、`<名字>_Material.mat`、`<名字>_SkeletonData.asset`。
项目场景全部由代码构建，所以骨骼资产必须放在 `Resources/Spine/` 下供运行时按名加载。

## 二、用在游戏里

**方式 A（零代码）**：把 `staff.json`（或存档自定义员工）的 `look` 字段写成
`spine:<名字>`，员工实体就会用这个 Spine 小人渲染，默认循环播 `idle` 动画；
资产缺失时自动回退到原拼装造型，不会报错断游戏。

**方式 B（代码）**：`BistroBurrow.Util.SpineActor`

```csharp
var sa = SpineActor.Spawn("testbun", parent, Vector2.zero,
                          anim: "idle", loop: true, sortingOrder: 20, scale: 1f);
SpineActor.PlayIfExists(sa, "hop", loop: false); // 骨骼里没有该动画时安全返回 false
```

## 三、验证样例

`Assets/Resources/Spine/testbun.*` 是一套手写的 3.5.51 格式验证资源（奶油小兔，
idle/hop 两个动画），可作为格式参照；接好自己的小人后可以删掉这套样例。

## 四、工程结构备注

- `Assets/Spine/Spine.Runtime.asmdef`：运行时程序集，`Game.Runtime` 已引用它。
- `Assets/Spine/Editor/`（`Spine.Editor.asmdef`）：原版散落各处的 10 个 Editor
  目录已集中于此（asmdef 体系下 Editor 目录不再被特殊处理，必须独立成编辑器程序集）。
- 移植改动仅 2 处：`SpineMesh.cs` 两个 `Color32` 局部变量补默认值（新编译器
  definite-assignment 更严）；`SkeletonDataAssetInspector.cs` 去掉 `new Texture()`
  （构造函数已受保护）。其余为无害的过期 API 警告。
- webdemo 网页镜像不渲染 Spine（纯视觉层差异，数值公式不受影响）。
