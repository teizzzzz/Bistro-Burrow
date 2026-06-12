using UnityEngine;

namespace BistroBurrow.Core
{
    /// <summary>
    /// 主相机装配：正交投影（GDD §3.3 视觉规范——消除横版位移的透视畸变）。
    /// 两种模式：
    /// 1. 定点平移（白天 店内⇄店外 视窗切换，平滑插值）
    /// 2. 跟随目标（夜晚探索，Lerp 跟随主角，GDD §3.3）
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }

        Transform _followTarget;     // 跟随目标（空 = 定点模式）
        float _targetX;              // 定点模式目标 X
        float _y;                    // 固定高度
        float _minX = float.MinValue; // 跟随左边界（探险防止看出地图）
        float _maxX = float.MaxValue; // 跟随右边界（白天舞台范围）
        float _followOffsetX = 1.5f;  // 跟随横向偏移（夜晚镜头让前方；白天选人居中=0）
        const float LerpSpeed = 5f;

        /// <summary>创建全局唯一主相机（常驻 ManagerScene，跨阶段复用）。</summary>
        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<CameraRig>();
            rig.Cam = cam;
            go.transform.position = new Vector3(0f, 0f, -10f);
            Object.DontDestroyOnLoad(go);
            return rig;
        }

        /// <summary>定点模式：平滑移动到指定 X（店内/店外切换）。</summary>
        public void PanTo(float x, float y)
        {
            _followTarget = null;
            _targetX = x;
            _y = y;
            _minX = float.MinValue; // 清除上一夜的跟随边界，防止白天镜头被错误钳制
            _maxX = float.MaxValue;
        }

        /// <summary>立即跳转（阶段切换时避免相机从旧位置飞过来）。</summary>
        public void SnapTo(float x, float y)
        {
            PanTo(x, y);
            transform.position = new Vector3(x, y, -10f);
        }

        /// <summary>跟随模式。minX/maxX 为镜头边界；offsetX 为横向构图偏移。</summary>
        public void Follow(Transform target, float y, float minX,
            float maxX = float.MaxValue, float offsetX = 1.5f)
        {
            _followTarget = target;
            _y = y;
            _minX = minX;
            _maxX = maxX;
            _followOffsetX = offsetX;
        }

        public void SetBackground(Color c)
        {
            if (Cam != null) Cam.backgroundColor = c;
        }

        void LateUpdate()
        {
            // 跟随目标可能在怪物击杀/场景卸载时被销毁，必须判空
            float wantX = _followTarget != null ? _followTarget.position.x + _followOffsetX : _targetX;
            wantX = Mathf.Clamp(wantX, _minX, _maxX);
            Vector3 p = transform.position;
            // 帧率无关的平滑插值（GDD §3.3 镜头平滑 Lerp 跟随）
            float t = 1f - Mathf.Exp(-LerpSpeed * Time.deltaTime);
            p.x = Mathf.Lerp(p.x, wantX, t);
            p.y = Mathf.Lerp(p.y, _y, t);
            transform.position = p;
        }
    }
}
