using UnityEngine;
using UnityEngine.UI;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 店内员工实体（让"雇佣"看得见摸得着）：
    /// - Cook（帮厨）：常驻灶台旁，灶上有锅时小跑步律动（在干活）；
    /// - Gatherer（采集员）：店内缓慢巡场；疲劳≥派遣上限时瘫到休息沙发打瞌睡
    ///   （GDD §3.1 员工休憩室的轻量化呈现）。
    /// 造型由 staff.json 的 colorHex + look 决定；头顶挂短名名牌。
    /// 不自带 Update：由 BistroDirector 集中 Tick（黄昏随场冻结）。
    /// </summary>
    public class StaffAgent : MonoBehaviour
    {
        public StaffDef Def { get; private set; }

        BistroDirector _director;
        StaffState _state;
        Transform _body;
        Spine.Unity.SkeletonAnimation _spine;   // look=spine: 时的骨骼小人（否则为 null）
        string _spineAnim;                       // 当前骨骼动画名（避免每帧重设轨道）
        GameObject _sleepTag;       // "Zzz" 名牌（休息时显示）
        Vector2 _home;              // 岗位锚点（Cook=灶台旁；Gatherer=巡场起点）
        Vector2 _target;
        float _wanderTimer;
        bool _resting;

        const float WalkSpeed = 1.6f;

        public void Init(BistroDirector director, StaffDef def, StaffState state, Vector2 home, Vector2 restSpot)
        {
            _director = director;
            Def = def;
            _state = state;
            _home = home;
            _target = home;
            transform.position = home;

            BuildVisual(def);

            // 点选碰撞体（BistroCameraController 射线选人用）
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.85f, 0f);
            col.size = new Vector3(0.9f, 1.8f, 0.8f);

            // 疲劳过高的采集员直接趴在沙发上开工
            _resting = def.role == "Gatherer" && state != null &&
                       state.fatigue >= ConfigService.Balance.fatigueDispatchLimit;
            if (_resting) _target = restSpot;
            Juice.PopIn(_body);
        }

        /// <summary>由导演驱动；stoveBusy = 灶台当前有锅在烧（Cook 的干活动画开关）。</summary>
        public void Tick(float dt, bool stoveBusy, Vector2 restSpot)
        {
            switch (Def.role)
            {
                case "Cook":
                    // 帮厨守灶台：Spine 小人有锅时播 Interact（基建干活动作），没锅时 Relax；
                    // 拼装造型保留原律动（有锅小幅高频，没锅呼吸感）
                    if (_spine != null)
                    {
                        SetSpineAnim(stoveBusy ? "Interact" : "Relax");
                    }
                    else if (_body != null)
                    {
                        float amp = stoveBusy ? 0.07f : 0.02f;
                        float freq = stoveBusy ? 11f : 2.2f;
                        _body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * freq)) * amp, 0f);
                    }
                    break;

                case "Gatherer":
                    bool shouldRest = _state != null &&
                        _state.fatigue >= ConfigService.Balance.fatigueDispatchLimit;
                    if (shouldRest != _resting)
                    {
                        _resting = shouldRest;
                        _target = _resting ? restSpot : _home;
                    }
                    if (_sleepTag != null && _sleepTag.activeSelf != _resting)
                        _sleepTag.SetActive(_resting);

                    if (_resting)
                    {
                        if (MoveTowards(restSpot, dt) && _spine != null)
                            SetSpineAnim("Sleep"); // 走到沙发后睡觉（基建 Sleep 动作）
                    }
                    else
                    {
                        // 巡场：到点后随机挑下一个店内位置溜达
                        if (MoveTowards(_target, dt))
                        {
                            _wanderTimer -= dt;
                            if (_wanderTimer <= 0f)
                            {
                                _wanderTimer = Random.Range(2.5f, 5f);
                                _target = new Vector2(Random.Range(-3.2f, 3.6f), BistroDirector.GroundY);
                            }
                        }
                    }
                    break;
            }
        }

        bool MoveTowards(Vector2 target, float dt)
        {
            Vector3 pos = transform.position;
            Vector3 next = Vector3.MoveTowards(pos, new Vector3(target.x, target.y, pos.z), WalkSpeed * dt);
            transform.position = next;
            bool arrived = Vector2.Distance(next, target) < 0.03f;
            if (_spine != null)
            {
                SetSpineAnim(arrived ? "Relax" : "Move");
                if (!arrived && Mathf.Abs(next.x - pos.x) > 1e-5f)
                    _spine.Skeleton.FlipX = next.x < pos.x; // 小人默认朝右，向左走时镜像
            }
            else if (_body != null && !arrived)
            {
                _body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.06f, 0f);
            }
            return arrived;
        }

        void SetSpineAnim(string anim)
        {
            if (_spineAnim == anim) return;
            SpineActor.PlayIfExists(_spine, anim, true); // 缺该动画时内部兜底，不会僵住
            _spineAnim = anim;
        }

        void BuildVisual(StaffDef def)
        {
            Color tone = SpriteFactory.ParseHex(def.colorHex);
            Color toneLight = Color.Lerp(tone, Color.white, 0.25f);

            SpriteFactory.NewSprite("Shadow", transform,
                SpriteFactory.SoftShadow(0.72f, 0.30f), new Vector2(0f, -0.02f), 19);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // look = "spine:<骨骼名>" → 使用 Spine 3.5 小人（资源缺失回退到拼装造型）
            if (!string.IsNullOrEmpty(def.look) && def.look.StartsWith("spine:"))
            {
                string skel = def.look.Substring("spine:".Length);
                // 0.75：方舟基建小人原始 ~2.1m，缩到 ~1.6m 与拼装角色/店面比例一致，名牌(1.95m)不被挡
                _spine = SpineActor.Spawn(skel, _body, Vector2.zero, "Relax", true, 20, 0.75f);
                if (_spine != null)
                {
                    _spineAnim = "Relax";
                    BuildNameTag(def);
                    BuildSleepTag();
                    return;
                }
            }
            SpriteFactory.NewSprite("Torso", _body,
                SpriteFactory.GradientRect(0.55f, 0.85f, toneLight, tone, 0.16f), new Vector2(0f, 0.62f), 20);
            SpriteFactory.NewSprite("Head", _body,
                SpriteFactory.Circle(0.46f, new Color(0.96f, 0.86f, 0.74f)), new Vector2(0f, 1.32f), 21);
            Color eye = new Color(0.12f, 0.10f, 0.10f);
            SpriteFactory.NewSprite("EyeL", _body, SpriteFactory.Circle(0.07f, eye), new Vector2(-0.14f, 1.35f), 22);
            SpriteFactory.NewSprite("EyeR", _body, SpriteFactory.Circle(0.07f, eye), new Vector2(-0.02f, 1.35f), 22);

            // 造型差异（look 标签，纯视觉无逻辑）
            switch (def.look)
            {
                case "rabbit": // 兔耳 ×2 + 小围裙
                    SpriteFactory.NewSprite("EarL", _body,
                        SpriteFactory.Rect(0.1f, 0.4f, toneLight, 0.05f), new Vector2(-0.13f, 1.68f), 21);
                    SpriteFactory.NewSprite("EarR", _body,
                        SpriteFactory.Rect(0.1f, 0.4f, toneLight, 0.05f), new Vector2(0.1f, 1.68f), 21);
                    SpriteFactory.NewSprite("Apron", _body,
                        SpriteFactory.Rect(0.4f, 0.4f, new Color(0.95f, 0.93f, 0.88f), 0.08f), new Vector2(0f, 0.45f), 21);
                    break;
                case "adventurer": // 额带 + 背上小剑
                    SpriteFactory.NewSprite("Headband", _body,
                        SpriteFactory.Rect(0.46f, 0.09f, new Color(0.85f, 0.3f, 0.25f), 0.02f), new Vector2(0f, 1.45f), 22);
                    SpriteFactory.NewSprite("Sword", _body,
                        SpriteFactory.Rect(0.09f, 0.62f, new Color(0.65f, 0.66f, 0.72f), 0.02f), new Vector2(-0.3f, 0.88f), 19);
                    break;
                case "hunter": // 兜帽
                    SpriteFactory.NewSprite("Hood", _body,
                        SpriteFactory.Circle(0.52f, Color.Lerp(tone, Color.black, 0.25f)), new Vector2(0f, 1.42f), 20);
                    break;
                case "chef": // 白色厨师帽（自定义创始帮厨）
                    SpriteFactory.NewSprite("ChefHat", _body,
                        SpriteFactory.Rect(0.5f, 0.3f, Color.white, 0.1f), new Vector2(0f, 1.65f), 22);
                    break;
            }

            BuildNameTag(def);
            BuildSleepTag();
        }

        void BuildNameTag(StaffDef def)
        {
            var tag = new GameObject("NameTag");
            tag.transform.SetParent(transform, false);
            tag.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            var canvas = tag.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 55;
            var rt = (RectTransform)tag.transform;
            rt.sizeDelta = new Vector2(120, 30);
            rt.localScale = Vector3.one * 0.011f;

            Text name = UiFactory.Label(tag.transform,
                string.IsNullOrEmpty(def.shortName) ? def.displayName : def.shortName,
                20, new Color(1f, 0.95f, 0.8f, 0.85f), TextAnchor.MiddleCenter);
            UiFactory.FillParent((RectTransform)name.transform);
        }

        void BuildSleepTag()
        {
            _sleepTag = new GameObject("SleepTag");
            _sleepTag.transform.SetParent(transform, false);
            _sleepTag.transform.localPosition = new Vector3(0.42f, 1.7f, 0f);
            var canvas = _sleepTag.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 56;
            var rt = (RectTransform)_sleepTag.transform;
            rt.sizeDelta = new Vector2(80, 36);
            rt.localScale = Vector3.one * 0.012f;
            Text z = UiFactory.Label(_sleepTag.transform, "Zzz…", 24,
                new Color(0.7f, 0.8f, 1f, 0.9f), TextAnchor.MiddleCenter);
            UiFactory.FillParent((RectTransform)z.transform);
            _sleepTag.SetActive(false);
        }
    }
}
