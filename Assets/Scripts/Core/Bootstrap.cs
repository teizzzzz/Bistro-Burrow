using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 启动引导：ManagerScene 中唯一被场景文件引用的脚本。
    /// 场景 YAML 只挂这一个组件，其余对象全部由代码构建——
    /// 这样即使后续重构 GameManager，场景文件也无需改动。
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            GameManager.EnsureExists();
        }

        /// <summary>
        /// 兜底入口：即便有人从一个空场景直接点 Play（或场景引用丢失），
        /// 游戏也能自举启动。EnsureExists 内部有单例防重，不会双开。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            GameManager.EnsureExists();
        }
    }
}
