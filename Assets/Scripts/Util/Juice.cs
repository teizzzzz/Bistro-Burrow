using System.Collections;
using UnityEngine;

namespace BistroBurrow.Util
{
    /// <summary>
    /// 轻量游戏手感（Juice）库：弹性入场、脉冲、镜头震动、漂浮。
    /// 不引入 DOTween 等第三方依赖（WebGL 包体优先），全部协程实现。
    /// 协程宿主挂在隐藏的常驻对象上，目标对象销毁时协程自然终止（每帧判空）。
    /// </summary>
    public class Juice : MonoBehaviour
    {
        static Juice _host;

        static Juice Host
        {
            get
            {
                if (_host == null)
                {
                    var go = new GameObject("~Juice");
                    go.hideFlags = HideFlags.HideInHierarchy;
                    DontDestroyOnLoad(go);
                    _host = go.AddComponent<Juice>();
                }
                return _host;
            }
        }

        /// <summary>弹性入场：从 0 缩放回弹到原始大小（overshoot 回弹感）。</summary>
        public static void PopIn(Transform target, float duration = 0.32f)
        {
            if (target == null) return;
            Host.StartCoroutine(PopRoutine(target, target.localScale, duration));
        }

        /// <summary>脉冲：瞬间放大再弹回（上菜/收钱/拾取的轻反馈）。</summary>
        public static void Pulse(Transform target, float scale = 1.25f, float duration = 0.22f)
        {
            if (target == null) return;
            Host.StartCoroutine(PulseRoutine(target, scale, duration));
        }

        /// <summary>镜头震动（受击/击杀）。幅度按世界单位，自动衰减。</summary>
        public static void Shake(Transform cam, float amplitude = 0.12f, float duration = 0.18f)
        {
            if (cam == null) return;
            Host.StartCoroutine(ShakeRoutine(cam, amplitude, duration));
        }

        static IEnumerator PopRoutine(Transform t, Vector3 baseScale, float dur)
        {
            float e = 0f;
            while (e < dur)
            {
                if (t == null) yield break; // 目标可能中途销毁
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                // back-out 缓动：略微过冲后回落
                float s = 1f + 1.70158f;
                float v = 1f + (s + 1f) * Mathf.Pow(k - 1f, 3f) + s * Mathf.Pow(k - 1f, 2f);
                t.localScale = baseScale * Mathf.Max(0.01f, v);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        static IEnumerator PulseRoutine(Transform t, float peak, float dur)
        {
            Vector3 baseScale = t.localScale;
            float e = 0f;
            while (e < dur)
            {
                if (t == null) yield break;
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                // 先冲到 peak 再平滑回 1（正弦半波）
                float v = 1f + (peak - 1f) * Mathf.Sin(k * Mathf.PI);
                t.localScale = baseScale * v;
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        static IEnumerator ShakeRoutine(Transform cam, float amp, float dur)
        {
            float e = 0f;
            while (e < dur)
            {
                if (cam == null) yield break;
                e += Time.deltaTime;
                float falloff = 1f - Mathf.Clamp01(e / dur);
                // 叠加在相机当前位置上的高频偏移；CameraRig 的 Lerp 会自然吸收回正
                Vector3 p = cam.position;
                cam.position = p + (Vector3)(Random.insideUnitCircle * amp * falloff);
                yield return null;
            }
        }
    }
}
