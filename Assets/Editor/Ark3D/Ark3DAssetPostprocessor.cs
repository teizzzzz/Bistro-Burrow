using System.IO;
using UnityEditor;

namespace BistroBurrow.EditorTools
{
    /// <summary>
    /// 明日方舟基建 3D 资源（Assets/Ark3D/ 下）的导入规则：
    /// - OBJ：Scale Factor 100（原始顶点尺度 ~0.02）、法线 Import、
    ///   不生成 Lightmap UV、不导入灯光/相机/动画、不生成材质（由批量工具统一配）
    /// - PNG：Texture2D 默认类型 + sRGB；TX_Shadow* 开启 Alpha 透明
    /// 只对 Assets/Ark3D/ 生效，不影响项目其他资源。
    /// </summary>
    public class Ark3DAssetPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/Ark3D/";

        static bool InScope(string path) => path.Replace('\\', '/').StartsWith(Root);

        void OnPreprocessModel()
        {
            if (!InScope(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            // 家具 OBJ 顶点 ~0.02-0.07 → ×100；房间 OBJ 原始就是米级（~34×4.5）→ ×1
            bool isRoom = assetPath.Replace('\\', '/').Contains("/Models/room/");
            mi.globalScale = isRoom ? 1f : 100f;
            mi.importNormals = ModelImporterNormals.Import;
            mi.generateSecondaryUV = false;   // 不生成 Lightmap UV
            mi.importLights = false;
            mi.importCameras = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            // 材质由 Ark3DBatchImporter 按命名前缀统一生成，不让模型各自吐材质
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessTexture()
        {
            if (!InScope(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;

            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (name.StartsWith("TX_Shadow"))
            {
                // 软阴影贴图：透明叠加用
                ti.alphaIsTransparency = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
            }
        }
    }
}
