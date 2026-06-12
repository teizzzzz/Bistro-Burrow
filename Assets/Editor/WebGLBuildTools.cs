using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BistroBurrow.EditorTools
{
    /// <summary>
    /// WebGL 原型构建工具（GDD §6.3 极致包体控制，设置即代码）：
    /// - 压缩格式 Brotli（itch.io 高压缩比传输）
    /// - Managed Stripping Level = High（剥离未用引擎子系统）
    /// - 关闭解压回退（节省包体；要求托管端正确配置 Content-Encoding，itch.io 原生支持）
    /// 菜单：Bistro → 一键应用设置 / 一键构建。
    /// </summary>
    public static class WebGLBuildTools
    {
        const string OutputDir = "Builds/WebGL";

        [MenuItem("Bistro/应用 WebGL MVP 构建设置")]
        public static void ApplySettings()
        {
            PlayerSettings.companyName = "BistroBurrow Team";
            PlayerSettings.productName = "Bistro & Burrow";
            PlayerSettings.runInBackground = true;

            // 压缩与裁剪（GDD §6.3 第 4 条）
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            // 异常支持降到最低档：WebAssembly 代码体积显著缩减
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // 2D 色块项目用 Gamma 色彩空间即可，省去线性空间的纹理换算成本
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            Debug.Log("[WebGLBuildTools] 已应用 WebGL MVP 构建设置（Brotli / High Stripping / Gamma）。");
        }

        [MenuItem("Bistro/构建 WebGL（Brotli 压缩）")]
        public static void BuildWebGL()
        {
            ApplySettings();

            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuildTools] Build Settings 中没有场景，请确认 ManagerScene 已加入。");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(scenes, s => s.path),
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report != null && report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                ulong sizeMb = report.summary.totalSize / (1024 * 1024);
                Debug.Log($"[WebGLBuildTools] 构建成功 → {OutputDir}（总大小 ≈ {sizeMb} MB，目标 ≤ 25MB）。" +
                          "本地预览请用 itch.io 上传或带 Brotli MIME 的静态服务器。");
            }
            else
            {
                Debug.LogError("[WebGLBuildTools] 构建失败，详见 Console / Editor.log。");
            }
        }
    }
}
