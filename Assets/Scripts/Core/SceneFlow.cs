using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 场景动态叠加管理（GDD §6.2）：常驻 ManagerScene + 运行时创建/卸载子场景。
    /// 为什么不用 SceneManager.LoadScene("xxx")：
    /// 1. 避免 WebGL 端整场景文件的下载/反序列化卡顿（子场景内容全部代码化构建）；
    /// 2. 子场景无需进 Build Settings，杜绝场景名拼写漂移。
    /// 卸载用 UnloadSceneAsync，整棵对象树连同其引用一并释放显存。
    /// </summary>
    public static class SceneFlow
    {
        /// <summary>创建（或复用已存在的）附加子场景。</summary>
        public static Scene CreateAdditive(string sceneName)
        {
            Scene existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded) return existing; // 防御：重复进入同阶段
            return SceneManager.CreateScene(sceneName);
        }

        /// <summary>把根对象挪进子场景（其子孙随根一起归属，卸载时整树销毁）。</summary>
        public static void MoveToScene(GameObject root, Scene scene)
        {
            if (root == null || !scene.IsValid()) return;
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        /// <summary>异步卸载子场景；ManagerScene 永远常驻，不可能成为"最后一个场景"。</summary>
        public static void UnloadAsync(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            SceneManager.UnloadSceneAsync(scene);
        }
    }
}
