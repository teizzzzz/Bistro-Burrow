using System.Collections.Generic;
using UnityEngine;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 灶台（GDD §3.1 灶台与厨房区）：多槽位并行烹饪，槽位数随店铺评级成长。
    /// 纯逻辑类（非 MonoBehaviour），由 BistroDirector 集中驱动与冻结。
    /// </summary>
    public class StoveStation
    {
        class CookJob
        {
            public CustomerAgent agent;
            public RecipeDef recipe;
            public float remain;
            public float total;
            public Transform barFill; // 槽位进度条前景
        }

        readonly List<CookJob> _jobs = new();
        readonly int _slots;
        readonly Transform _sceneRoot;
        readonly Vector2 _stovePos;
        readonly List<Transform> _slotBars = new(); // 每槽位一根进度条

        public StoveStation(Transform sceneRoot, Vector2 stovePos, int slots)
        {
            _sceneRoot = sceneRoot;
            _stovePos = stovePos;
            _slots = Mathf.Max(1, slots);
            BuildVisual();
        }

        public bool HasFreeSlot => _jobs.Count < _slots;

        /// <summary>开火烹饪。调用前由导演完成食材扣除（本类只管时序）。</summary>
        public bool TryStart(CustomerAgent agent, RecipeDef recipe)
        {
            if (agent == null || recipe == null || !HasFreeSlot) return false;
            var job = new CookJob
            {
                agent = agent,
                recipe = recipe,
                remain = Mathf.Max(0.2f, recipe.cookSeconds),
                total = Mathf.Max(0.2f, recipe.cookSeconds)
            };
            _jobs.Add(job);
            return true;
        }

        /// <summary>由导演每帧驱动：推进烹饪、完成即上菜、清理无效订单。</summary>
        public void Tick(float dt)
        {
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                CookJob job = _jobs[i];

                // 客人可能已愤然离席/被销毁：订单作废，槽位释放（食材已损耗，符合现实）
                if (job.agent == null || job.agent.State != CustomerAgent.Stage.WaitingFood)
                {
                    _jobs.RemoveAt(i);
                    continue;
                }

                job.remain -= dt;
                if (job.remain <= 0f)
                {
                    job.agent.Serve();
                    FloatingText.Spawn(_sceneRoot, job.agent.transform.position + Vector3.up * 1.6f,
                        "上菜！", new Color(1f, 0.92f, 0.6f));
                    _jobs.RemoveAt(i);
                }
            }
            RefreshBars();
        }

        public void CancelAll() => _jobs.Clear();

        // ---------- 视觉 ----------

        void BuildVisual()
        {
            // 灶台主体 + 烟囱色块
            SpriteFactory.NewSprite("StoveBody", _sceneRoot,
                SpriteFactory.Rect(1.5f, 1.1f, new Color(0.42f, 0.30f, 0.24f), 0.08f),
                _stovePos + new Vector2(0f, 0.55f), 12);
            SpriteFactory.NewSprite("StoveTop", _sceneRoot,
                SpriteFactory.Rect(1.7f, 0.18f, new Color(0.25f, 0.18f, 0.15f), 0.05f),
                _stovePos + new Vector2(0f, 1.15f), 13);
            SpriteFactory.NewSprite("Pot", _sceneRoot,
                SpriteFactory.Rect(0.7f, 0.4f, new Color(0.22f, 0.24f, 0.30f), 0.12f),
                _stovePos + new Vector2(0f, 1.4f), 14);

            // 槽位进度条（默认隐藏，开火时显示）
            for (int i = 0; i < _slots; i++)
            {
                Vector2 barPos = _stovePos + new Vector2(-0.45f + 0.5f * i, 2.0f);
                var bg = SpriteFactory.NewSprite($"CookBarBg{i}", _sceneRoot,
                    SpriteFactory.Rect(0.42f, 0.1f, new Color(0f, 0f, 0f, 0.45f), 0.03f), barPos, 30);
                var fill = SpriteFactory.NewSprite("Fill", bg.transform,
                    SpriteFactory.Rect(0.4f, 0.07f, new Color(1f, 0.62f, 0.25f), 0.02f), Vector2.zero, 31);
                // 以左缘为缩放锚点：先平移半宽再 scale
                fill.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
                var holder = new GameObject("FillPivot").transform;
                holder.SetParent(bg.transform, false);
                holder.localPosition = new Vector3(-0.2f, 0f, 0f);
                fill.transform.SetParent(holder, true);
                fill.transform.localPosition = new Vector3(0.2f, 0f, 0f);
                _slotBars.Add(holder);
                bg.gameObject.SetActive(false);
            }
        }

        void RefreshBars()
        {
            for (int i = 0; i < _slotBars.Count; i++)
            {
                Transform holder = _slotBars[i];
                if (holder == null) continue;
                Transform barRoot = holder.parent; // CookBarBg
                bool active = i < _jobs.Count;
                if (barRoot != null && barRoot.gameObject.activeSelf != active)
                    barRoot.gameObject.SetActive(active);
                if (active)
                {
                    CookJob job = _jobs[i];
                    float progress = 1f - Mathf.Clamp01(job.remain / job.total);
                    holder.localScale = new Vector3(progress, 1f, 1f);
                }
            }
        }
    }
}
