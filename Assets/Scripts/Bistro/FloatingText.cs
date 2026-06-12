using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Util;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 世界空间飘字（+12金币 / 顾客情绪等）。
    /// 自驱动 Update：生命周期极短（<1s），不参与导演的集中 Tick。
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        const float LifeSeconds = 0.9f;
        const float RiseSpeed = 1.1f;

        float _age;
        Text _text;

        /// <summary>在世界坐标处生成一条飘字。parent 为所属场景根（保证随子场景卸载）。</summary>
        public static void Spawn(Transform parent, Vector3 worldPos, string content, Color color)
        {
            var go = new GameObject("FloatingText");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = worldPos;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 80;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(220, 50);
            rt.localScale = Vector3.one * 0.012f; // 世界空间 UI 统一缩放系数

            var ft = go.AddComponent<FloatingText>();
            ft._text = UiFactory.Label(go.transform, content, 30, color, TextAnchor.MiddleCenter);
            UiFactory.FillParent((RectTransform)ft._text.transform);
        }

        void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);
            if (_text != null)
            {
                Color c = _text.color;
                c.a = Mathf.Clamp01(1f - _age / LifeSeconds);
                _text.color = c;
            }
            if (_age >= LifeSeconds) Destroy(gameObject);
        }
    }
}
