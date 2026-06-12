using UnityEngine;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 餐厅 3D 实景舞台（2.5D：3D 房间 + Spine 小人在台前演）。
    /// 资产来自 Ark3D 导入管线（Resources/Ark3D/Prefabs/…，gitignore 仅本地），
    /// 缺资产时返回 false → BistroDirector 回退原 2D 手绘背景，仓库协作不受影响。
    ///
    /// 空间换算（相机在 -z 朝 +z 看，房间模型面朝 +z）：
    /// - 房间整体绕 Y 转 180°，原始 x∈[1,35] 镜像后取右段，房门正好落在店门口
    ///   （世界 x≈4.3~6.3），与排队点（x≥6.6）自然衔接；
    /// - 地板原始顶面 y=1 → 根节点 y = GroundY-1，角色脚底正好踩在 3D 地板上；
    /// - 地板条带旋转后 z∈[0,1.5]，墙体 z∈[1.5,3.5]——都在角色（z=0）身后。
    /// </summary>
    public static class Bistro3DStage
    {
        const string Res = "Ark3D/Prefabs/";
        const string RoomPath = Res + "Rooms/s_room_dormitory_6x2_ikeabw_ikea";
        const string ThemePath = Res + "Furni/ikeabw_ikea/";

        public static bool Available => Resources.Load<GameObject>(RoomPath) != null;

        /// <summary>搭建 3D 餐厅 + 宿舍角。tablePos/restSpot 与 2D 逻辑共用同一组坐标。</summary>
        public static bool Build(Transform parent, float groundY, Vector2[] tablePos, Vector2 restSpot)
        {
            var roomPrefab = Resources.Load<GameObject>(RoomPath);
            if (roomPrefab == null) return false;

            var stage = new GameObject("Stage3D").transform;
            stage.SetParent(parent, false);

            var room = Object.Instantiate(roomPrefab, stage);
            room.name = "Room";
            // Unity 导入 OBJ 时已镜像 X（右手系→左手系），叠加 180° 旋转后 x 回到原值：
            // 世界 x = pos.x + 原始 x(1..35)。取 -8.2 → 房间覆盖 -7.2..26.8（满屏），
            // 左门(原始 x≈1..3)正好落在宿舍角旁（世界 -7.2..-5.2，权当休息室的门）
            room.transform.SetPositionAndRotation(
                new Vector3(-8.2f, groundY - 1f, 0f), Quaternion.Euler(0f, 180f, 0f));

            // 暖色方向光：从镜头侧打向房间内壁（3D 网格没光照会一片黑）
            var lightGo = new GameObject("StageLight");
            lightGo.transform.SetParent(stage, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.88f);
            // 房间整体转了 180°，可见面法线朝 -z：光必须从镜头侧(-z)往 +z 打
            lightGo.transform.rotation = Quaternion.Euler(42f, 15f, 0f);

            // 环境光兜底（代码建的场景默认环境光近黑；精灵用无光照着色器不受影响）。
            // 偏暖偏亮：纵深处的墙面不再发闷（"背景太深"观感修正）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.54f, 0.50f);

            // 餐桌（与 2D 桌位同坐标，客人入座逻辑零改动）
            foreach (Vector2 p in tablePos)
                Place("s_furni_s1_coffeetable_01", stage, new Vector3(p.x, groundY, 0.55f), 1.5f);

            // 宿舍角：床是休憩点本体（疲劳采集员到这睡 Sleep）+ 床头柜
            Place("s_furni_s1_bed_01", stage, new Vector3(restSpot.x, groundY, 0.85f), 2.4f);
            Place("s_furni_s1_nightstand_01", stage, new Vector3(restSpot.x + 1.45f, groundY, 0.95f), 0.7f);

            // 陈设：厨房柜衬托灶台区，书柜靠门侧墙
            Place("s_furni_s1_comcabinet_01", stage, new Vector3(-5.2f, groundY, 1.15f), 1.8f);
            Place("s_furni_s1_bookcase_01", stage, new Vector3(3.9f, groundY, 1.25f), 1.9f);
            return true;
        }

        /// <summary>摆一件家具：转 180° 面向镜头、按目标宽度等比缩放、包围盒贴地。</summary>
        static void Place(string prefabName, Transform parent, Vector3 pos, float targetWidth)
        {
            var prefab = Resources.Load<GameObject>(ThemePath + prefabName);
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, parent);
            go.name = prefabName;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 180f, 0f));

            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float scale = targetWidth / Mathf.Max(0.01f, b.size.x);
            go.transform.localScale = Vector3.one * scale;

            // 缩放后重新量包围盒，把脚底对齐地面
            b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += Vector3.up * (pos.y - b.min.y);
        }
    }
}
