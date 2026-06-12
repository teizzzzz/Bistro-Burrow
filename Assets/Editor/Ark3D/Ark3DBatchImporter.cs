using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBurrow.EditorTools
{
    /// <summary>
    /// 明日方舟基建 3D 资源批量导入（菜单：Bistro/导入明日方舟 3D 家具）。
    /// 流程：
    ///   1. 项目根 3dobj/diy/arts → 拷贝 .obj 与 .png 到 Assets/Ark3D/（结构保留，跳过 TT_*.ab.json）
    ///   2. 导入规则由 Ark3DAssetPostprocessor 接管（Scale 100 / 法线 Import / sRGB 等）
    ///   3. 按命名前缀生成材质：文件夹 ikeabw_ikea ↔ 贴图前缀 IKEAbw_IKEA（小写相等即配对）；
    ///      内置管线用 Standard(_MainTex)，URP 自动改用 Universal Render Pipeline/Lit(_BaseMap)
    ///   4. 每件家具生成 Prefab：主网格 + 材质 + shadow*.obj 子物体（TX_Shadow_01 透明叠加）
    ///   5. 房间五件套（floor/frame/room/LDoor/RDoor）组装成房间 Prefab，门是可独立控制的子物体；
    ///      dormitory 按每个主题各出一个变体，empty 房间用 TX_room_empty_01
    /// 注意：素材为第三方提取物，Assets/Ark3D/ 已 gitignore，仅本地原型验证。
    /// </summary>
    public static class Ark3DBatchImporter
    {
        const string SrcRoot = "3dobj/diy/arts";                 // 项目根目录下的原始库
        const string Root = Ark3DAssetPostprocessor.Root;        // Assets/Ark3D/
        const string TexDir = Root + "Textures";
        const string ModelDir = Root + "Models";
        const string MatDir = Root + "Materials";
        const string PrefabDir = Root + "Prefabs";

        [MenuItem("Bistro/导入明日方舟 3D 家具（OBJ→材质→Prefab）")]
        public static void Run()
        {
            string src = Path.GetFullPath(SrcRoot);
            if (!Directory.Exists(src))
            {
                EditorUtility.DisplayDialog("Ark3D", $"未找到原始库：{src}\n请把拆包资源放到项目根目录 3dobj/diy/arts", "好");
                return;
            }

            CopySources(src);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            bool urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null;
            var mats = new MaterialLibrary(urp);

            int furniCount = BuildFurniturePrefabs(mats);
            int roomCount = BuildRoomPrefabs(mats);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Ark3D] 完成：管线={(urp ? "URP" : "内置(Standard)")}，家具 Prefab×{furniCount}，房间 Prefab×{roomCount}，材质×{mats.Created}。输出在 {PrefabDir}");
        }

        // ---------- 1. 拷贝 ----------

        static void CopySources(string src)
        {
            // 贴图：[uc]texture + [uc]materials 两处的 png 平铺进 Textures/
            Directory.CreateDirectory(TexDir);
            foreach (string dir in new[] { "[uc]texture", "[uc]materials" })
            {
                string d = Path.Combine(src, dir);
                if (!Directory.Exists(d)) continue;
                foreach (string f in Directory.GetFiles(d, "*.png"))
                    File.Copy(f, Path.Combine(TexDir, Path.GetFileName(f)), true);
            }
            // 模型：保留 furni/<主题>/<家具>/、room/<房间>/ 结构，仅取 .obj
            foreach (string f in Directory.GetFiles(Path.Combine(src, "models"), "*.obj", SearchOption.AllDirectories))
            {
                string rel = f.Substring(Path.Combine(src, "models").Length + 1).Replace('\\', '/');
                string dst = Path.Combine(ModelDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(f, dst, true);
            }
        }

        // ---------- 2. 材质库（按需创建并缓存） ----------

        class MaterialLibrary
        {
            readonly bool _urp;
            readonly Dictionary<string, Material> _cache = new();
            public int Created { get; private set; }

            public MaterialLibrary(bool urp) { _urp = urp; Directory.CreateDirectory(MatDir); }

            string ShaderName => _urp ? "Universal Render Pipeline/Lit" : "Standard";
            string BaseMapProp => _urp ? "_BaseMap" : "_MainTex";

            /// <summary>按贴图名取/建不透明材质（M_前缀去 TX_）。</summary>
            public Material Opaque(string texName)
            {
                if (texName == null) return null;
                return GetOrCreate("M_" + texName.Replace("TX_", ""), texName, transparent: false);
            }

            /// <summary>影子专用透明叠加材质。</summary>
            public Material Shadow() => GetOrCreate("M_Shadow_01", "TX_Shadow_01", transparent: true);

            Material GetOrCreate(string matName, string texName, bool transparent)
            {
                if (_cache.TryGetValue(matName, out Material m)) return m;
                string path = $"{MatDir}/{matName}.mat";
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(Shader.Find(ShaderName));
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texName}.png");
                    if (tex == null) Debug.LogWarning($"[Ark3D] 缺贴图 {texName}.png（材质 {matName} 将是白模）");
                    m.SetTexture(BaseMapProp, tex);
                    if (transparent) MakeTransparent(m);
                    AssetDatabase.CreateAsset(m, path);
                    Created++;
                }
                _cache[matName] = m;
                return m;
            }

            void MakeTransparent(Material m)
            {
                if (_urp)
                {
                    m.SetFloat("_Surface", 1f); // Transparent
                    m.SetFloat("_Blend", 0f);   // Alpha
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                else
                {
                    m.SetFloat("_Mode", 3f);    // Standard 的 Transparent 模式
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetInt("_ZWrite", 0);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.EnableKeyword("_ALPHABLEND_ON");
                    m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                }
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        // ---------- 3. 命名配对：文件夹 ikeabw_ikea ↔ 贴图前缀 IKEAbw_IKEA ----------

        /// <summary>在 Textures/ 里找 TX_&lt;主题前缀&gt;_&lt;用途&gt;_&lt;序号&gt;.png（前缀小写 == 主题文件夹名）。</summary>
        static string FindThemeTexture(string themeFolder, string usage, string index = "01")
        {
            string want = $"tx_{themeFolder}_{usage}_{index}".ToLowerInvariant();
            foreach (string f in Directory.GetFiles(TexDir, "TX_*.png"))
            {
                string n = Path.GetFileNameWithoutExtension(f);
                if (n.ToLowerInvariant() == want) return n;
            }
            return null;
        }

        // ---------- 4. 家具 Prefab ----------

        static int BuildFurniturePrefabs(MaterialLibrary mats)
        {
            string furniRoot = ModelDir + "/furni";
            if (!Directory.Exists(furniRoot)) return 0;
            int count = 0;

            foreach (string themeDir in Directory.GetDirectories(furniRoot))
            {
                string theme = Path.GetFileName(themeDir);                       // ikeabw_ikea
                string furniTex = FindThemeTexture(theme, "furni");              // TX_IKEAbw_IKEA_furni_01
                Material furniMat = mats.Opaque(furniTex);

                foreach (string itemDir in Directory.GetDirectories(themeDir))
                {
                    string item = Path.GetFileName(itemDir);                     // s_furni_s1_bed_01
                    var objs = Directory.GetFiles(itemDir, "*.obj").Select(p => p.Replace('\\', '/')).ToList();
                    if (objs.Count == 0) continue;

                    var root = new GameObject(item);
                    try
                    {
                        foreach (string objPath in objs.OrderBy(p => Path.GetFileName(p).StartsWith("shadow"))) // 主网格在前
                        {
                            bool isShadow = Path.GetFileName(objPath).StartsWith("shadow");
                            var child = AddModelChild(root.transform, objPath, isShadow ? mats.Shadow() : furniMat,
                                          isShadow ? "Shadow" : "Mesh");
                            // 家具源模型朝 -z、房间朝 +z：统一转 180° 与房间同向
                            if (child != null) child.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        }
                        SavePrefab(root, $"{PrefabDir}/Furni/{theme}/{item}.prefab");
                        count++;
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            return count;
        }

        // ---------- 5. 房间 Prefab（门独立可控） ----------

        static int BuildRoomPrefabs(MaterialLibrary mats)
        {
            string roomRoot = ModelDir + "/room";
            if (!Directory.Exists(roomRoot)) return 0;
            int count = 0;

            // 所有有 room+floor 贴图的主题（dormitory 每主题出一个变体）
            var themes = Directory.GetDirectories(ModelDir + "/furni")
                                  .Select(Path.GetFileName)
                                  .Where(t => FindThemeTexture(t, "room") != null)
                                  .ToList();

            foreach (string roomDir in Directory.GetDirectories(roomRoot).Select(p => p.Replace('\\', '/')))
            {
                string roomName = Path.GetFileName(roomDir);                     // s_room_dormitory_6x2
                bool isEmpty = roomName.Contains("empty");

                if (isEmpty)
                {
                    // 空房间：整体用 TX_room_empty_01
                    Material m = mats.Opaque("TX_room_empty_01");
                    count += BuildOneRoom(roomDir, $"{PrefabDir}/Rooms/{roomName}.prefab", m, m) ? 1 : 0;
                }
                else
                {
                    foreach (string theme in themes)
                    {
                        Material roomMat = mats.Opaque(FindThemeTexture(theme, "room"));
                        Material floorMat = mats.Opaque(FindThemeTexture(theme, "floor"));
                        count += BuildOneRoom(roomDir, $"{PrefabDir}/Rooms/{roomName}_{theme}.prefab",
                                              roomMat, floorMat) ? 1 : 0;
                    }
                }
            }
            return count;
        }

        static bool BuildOneRoom(string roomDir, string prefabPath, Material roomMat, Material floorMat)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                bool any = false;
                // floor/frame/room 是固定结构；LDoor/RDoor 作为可独立控制的子物体挂在根上
                foreach (string part in new[] { "floor", "frame", "room", "LDoor", "RDoor" })
                {
                    string objPath = $"{roomDir}/{part}.obj";
                    if (!File.Exists(objPath)) continue;
                    Material m = part == "floor" ? floorMat : roomMat;
                    string childName = part == "LDoor" ? "Door_L" : part == "RDoor" ? "Door_R" : part;
                    AddModelChild(root.transform, objPath, m, childName);
                    any = true;
                }
                if (any) SavePrefab(root, prefabPath);
                return any;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---------- 共用 ----------

        static GameObject AddModelChild(Transform parent, string objAssetPath, Material mat, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(objAssetPath);
            if (model == null) { Debug.LogWarning($"[Ark3D] 模型未导入：{objAssetPath}"); return null; }
            var child = (GameObject)PrefabUtility.InstantiatePrefab(model);
            child.name = name;
            child.transform.SetParent(parent, false);
            foreach (var r in child.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            return child;
        }

        static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
    }
}
