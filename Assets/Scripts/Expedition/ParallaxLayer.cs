using UnityEngine;

namespace BistroBurrow.Expedition
{
    /// <summary>
    /// 视差背景层（GDD §3.3 前景/中景/背景三层卷轴）。
    /// factor 越小越"远"：0 = 完全跟随相机（贴在镜头上），1 = 与世界静止。
    /// 纯视觉组件，自驱动 LateUpdate（相机停了它自然也停）。
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        Transform _cam;
        float _factor = 1f;
        float _baseX;

        public void Init(Transform cam, float factor)
        {
            _cam = cam;
            _factor = factor;
            _baseX = transform.position.x;
        }

        void LateUpdate()
        {
            if (_cam == null) return; // 相机销毁（退出播放）时静默退场
            float offset = _cam.position.x * (1f - _factor);
            transform.position = new Vector3(_baseX + offset, transform.position.y, transform.position.z);
        }
    }
}
