using UnityEngine;
using BistroBurrow.Util;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 端菜飞盘：出锅的菜沿抛物线从灶台飞向顾客，落点触发真正的上菜。
    /// 把"上菜"从瞬间状态切换变成可感知的小事件（手感升级核心件）。
    /// 顾客中途离席/销毁时自动作废。
    /// </summary>
    public class FlyingDish : MonoBehaviour
    {
        const float FlightSeconds = 0.42f;
        const float ArcHeight = 1.3f;

        CustomerAgent _target;
        Vector3 _from;
        float _age;

        public static void Launch(Transform sceneRoot, Vector2 from, CustomerAgent target)
        {
            var go = new GameObject("FlyingDish");
            if (sceneRoot != null) go.transform.SetParent(sceneRoot, false);
            go.transform.position = from;

            // 盘子 + 菜：暖色小圆叠盘
            SpriteFactory.NewSprite("Plate", go.transform,
                SpriteFactory.Circle(0.4f, new Color(0.93f, 0.91f, 0.86f)), Vector2.zero, 45);
            SpriteFactory.NewSprite("Food", go.transform,
                SpriteFactory.Circle(0.28f, new Color(0.85f, 0.58f, 0.32f)), new Vector2(0f, 0.04f), 46);

            var dish = go.AddComponent<FlyingDish>();
            dish._target = target;
            dish._from = from;
        }

        void Update()
        {
            // 顾客可能已愤然离席：盘子原地消失（食材损耗已在开火时扣除）
            if (_target == null || _target.State != CustomerAgent.Stage.WaitingFood)
            {
                Destroy(gameObject);
                return;
            }

            _age += Time.deltaTime;
            float k = Mathf.Clamp01(_age / FlightSeconds);
            Vector3 to = _target.transform.position + new Vector3(0f, 1.0f, 0f);
            Vector3 pos = Vector3.Lerp(_from, to, k);
            pos.y += Mathf.Sin(k * Mathf.PI) * ArcHeight; // 抛物线弧顶
            transform.position = pos;

            if (k >= 1f)
            {
                _target.Serve();
                SfxSynth.Play(SfxSynth.Id.Serve, 0.45f);
                FloatingText.Spawn(transform.parent, to + Vector3.up * 0.6f,
                    "上菜！", new Color(1f, 0.92f, 0.6f));
                Destroy(gameObject);
            }
        }
    }
}
