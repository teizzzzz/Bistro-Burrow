using UnityEngine;
using Spine.Unity;

namespace BistroBurrow.Util
{
    /// <summary>
    /// Spine 3.5.51 小人运行时加载器（配套 Assets/Spine 下的 spine-unity 3.5 运行时）。
    ///
    /// 资源放置约定（三件套丢进 Assets/Resources/Spine/ 即可）：
    ///   1. 骨骼数据  <名字>.json        —— Spine 3.5.x 导出（二进制则命名 <名字>.skel.bytes）
    ///   2. 图集     <名字>.atlas.txt   —— Spine 导出的 .atlas 重命名加 .txt 后缀
    ///   3. 贴图     <名字>.png         —— 与图集同目录
    /// 导入时 spine-unity 自动生成 <名字>_Atlas / <名字>_Material / <名字>_SkeletonData 资产，
    /// 本类按 "<名字>_SkeletonData" 从 Resources 运行时加载（场景全部代码构建，无法拖引用）。
    /// </summary>
    public static class SpineActor
    {
        /// <summary>对应骨骼资产是否存在（look 配置回退判断用）。</summary>
        public static bool Exists(string skeletonName)
        {
            return LoadDataAsset(skeletonName) != null;
        }

        /// <summary>支持两种布局：Spine/&lt;名&gt;_SkeletonData 或 Spine/&lt;名&gt;/&lt;名&gt;_SkeletonData。</summary>
        static SkeletonDataAsset LoadDataAsset(string skeletonName)
        {
            var asset = Resources.Load<SkeletonDataAsset>("Spine/" + skeletonName + "/" + skeletonName + "_SkeletonData");
            if (asset == null)
                asset = Resources.Load<SkeletonDataAsset>("Spine/" + skeletonName + "_SkeletonData");
            return asset;
        }

        /// <summary>
        /// 生成一个 Spine 小人并挂到 parent 下。加载失败返回 null（调用方应回退到拼装造型）。
        /// scale 为世界缩放（SkeletonDataAsset 自带 0.01 像素→米换算，这里再乘体型系数）。
        /// </summary>
        public static SkeletonAnimation Spawn(string skeletonName, Transform parent, Vector2 localPos,
            string anim = "idle", bool loop = true, int sortingOrder = 20, float scale = 1f)
        {
            var dataAsset = LoadDataAsset(skeletonName);
            if (dataAsset == null)
            {
                Debug.LogWarning($"[SpineActor] 未找到骨骼资产 Resources/Spine/{skeletonName}_SkeletonData，" +
                                 "请确认三件套已放入并触发过一次导入。");
                return null;
            }

            var sa = SkeletonAnimation.NewSkeletonAnimationGameObject(dataAsset);
            sa.gameObject.name = $"Spine_{skeletonName}";
            sa.transform.SetParent(parent, false);
            sa.transform.localPosition = localPos;
            sa.transform.localScale = Vector3.one * scale;

            var mr = sa.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = sortingOrder;

            sa.Initialize(false);
            PlayIfExists(sa, anim, loop);
            return sa;
        }

        /// <summary>
        /// 播放指定动画。先精确匹配，再大小写不敏感匹配（方舟系小人动画名是 Idle/Move 大写开头），
        /// 仍找不到时回退到骨骼的第一个动画——保证小人永远不会摆出 setup pose 僵住。
        /// </summary>
        public static bool PlayIfExists(SkeletonAnimation sa, string anim, bool loop)
        {
            if (sa == null) return false;
            var data = sa.SkeletonDataAsset != null ? sa.SkeletonDataAsset.GetSkeletonData(true) : null;
            if (data == null || data.Animations.Count == 0) return false;

            Spine.Animation found = string.IsNullOrEmpty(anim) ? null : data.FindAnimation(anim);
            if (found == null && !string.IsNullOrEmpty(anim))
            {
                foreach (var a in data.Animations)
                    if (string.Equals(a.Name, anim, System.StringComparison.OrdinalIgnoreCase)) { found = a; break; }
            }
            bool exact = found != null;
            if (found == null) found = data.Animations.Items[0];
            sa.state.SetAnimation(0, found, loop);
            return exact;
        }
    }
}
