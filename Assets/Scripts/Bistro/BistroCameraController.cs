using UnityEngine;
using UnityEngine.EventSystems;
using BistroBurrow.Core;
using BistroBurrow.Util;

namespace BistroBurrow.Bistro
{
    /// <summary>
    /// 白天经营的相机与点选控制（基建/模拟人生式操作）：
    /// - 3D 舞台在场时切透视相机（FOV 50 + 3° 俯角）——房间纵深产生自然视差；
    /// - 左键拖拽 / A·D 平移视角（带边界），滚轮推拉镜头（仅透视）；
    /// - 左键点选员工：头顶出现旋转绿水晶标识，相机平滑跟随（选中时轻微推近）；
    ///   点空地取消；拖拽平移会停止跟随但保留选中。
    /// 挂在 BistroDirector 根上，随场景卸载销毁并恢复相机原状（夜晚/菜单不受影响）。
    /// </summary>
    public class BistroCameraController : MonoBehaviour
    {
        GameManager _gm;
        CameraRig _rig;
        bool _perspective;

        StaffAgent _selected;
        SelectionMarker _marker;

        Vector3 _dragStartMouse;
        float _dragStartCamX;
        bool _dragging;
        bool _dragMoved;

        StaffAgent _pressedAgent;  // 按在员工身上（待判定：点选 or 长按提起）
        float _pressStartTime;
        Vector3 _pressMouse;
        bool _lifting;             // 拖拽提起进行中
        const float LiftHoldSeconds = 0.45f; // 长按判定时长
        const float MinX = -7f, MaxX = 6.2f; // 店内可放置范围

        float _zoom = -10.5f;          // 透视模式期望机位 z（滚轮推拉）
        const float PanLimit = 5.5f;   // 镜头水平边界（舞台范围）
        const float PerspY = 0.4f;     // 透视模式镜头高度
        const float Fov = 50f;

        // 进场前相机状态备份（OnDestroy 恢复，避免污染夜晚/菜单）
        bool _wasOrtho; float _orthoSize; float _savedFov; float _savedZ; Quaternion _savedRot;

        public void Init(GameManager gm, bool stage3D)
        {
            _gm = gm;
            _rig = gm.CamRig;
            _perspective = stage3D;
            Camera cam = _rig.Cam;
            _wasOrtho = cam.orthographic;
            _orthoSize = cam.orthographicSize;
            _savedFov = cam.fieldOfView;
            _savedZ = cam.transform.position.z;
            _savedRot = cam.transform.rotation;

            if (_perspective)
            {
                cam.orthographic = false;
                cam.fieldOfView = Fov;
                cam.transform.rotation = Quaternion.Euler(3f, 0f, 0f);
                Vector3 p = cam.transform.position;
                p.z = _zoom;
                cam.transform.position = p;
                _rig.PanTo(0f, PerspY);
            }
        }

        void OnDestroy()
        {
            Deselect();
            if (_rig == null || _rig.Cam == null) return;
            Camera cam = _rig.Cam;
            cam.orthographic = _wasOrtho;
            cam.orthographicSize = _orthoSize;
            cam.fieldOfView = _savedFov;
            cam.transform.rotation = _savedRot;
            Vector3 p = cam.transform.position;
            p.z = _savedZ;
            cam.transform.position = p;
        }

        void Update()
        {
            if (_gm == null || _gm.Phase != GamePhase.Day || _gm.UIPaused) return;

            if (_perspective) UpdateZoom();
            UpdateKeys();
            UpdateMouse();
        }

        void UpdateZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _zoom = Mathf.Clamp(_zoom + scroll * 0.9f, -13f, -6f);

            // 选中跟随时轻微推近（Sims 的"凑近看"），帧率无关平滑
            float wantZ = _zoom + (_selected != null ? 2.2f : 0f);
            Transform ct = _rig.Cam.transform;
            Vector3 p = ct.position;
            p.z = Mathf.Lerp(p.z, Mathf.Min(wantZ, -6f), 1f - Mathf.Exp(-6f * Time.deltaTime));
            ct.position = p;
        }

        void UpdateKeys()
        {
            float dir = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                      - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);

            // 老板被选中：方向键直接操控角色（A/D 左右 + W/S 纵深，镜头本就在跟随）
            if (_selected != null && _selected.IsBoss && !_selected.IsHeld)
            {
                float dirZ = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                           - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                _selected.ManualMove(dir, dirZ, Time.deltaTime);
                return;
            }

            if (Mathf.Abs(dir) < 0.5f) return;
            // 普通模式：键盘平移镜头（停止跟随但保留选中标识）
            float x = Mathf.Clamp(_rig.transform.position.x + dir * 7f * Time.deltaTime, -PanLimit, PanLimit);
            _rig.PanTo(x, CamY());
        }

        void UpdateMouse()
        {
            if (Input.GetMouseButtonDown(0) && !OverUI())
            {
                _pressMouse = Input.mousePosition;
                _pressedAgent = RaycastAgent(_pressMouse);
                _lifting = false;
                if (_pressedAgent != null)
                {
                    _pressStartTime = Time.unscaledTime; // 按在员工身上：等待点选/长按判定
                }
                else
                {
                    _dragging = true; // 按在空地：镜头拖拽平移
                    _dragMoved = false;
                    _dragStartMouse = _pressMouse;
                    _dragStartCamX = _rig.transform.position.x;
                }
            }

            // ---- 按住员工：长按或拖动 → 提起；提起后跟手 ----
            if (_pressedAgent != null && Input.GetMouseButton(0))
            {
                float movedPix = (Input.mousePosition - _pressMouse).magnitude;
                if (!_lifting && (Time.unscaledTime - _pressStartTime > LiftHoldSeconds || movedPix > 14f))
                {
                    _lifting = true;
                    _pressedAgent.SetHeld(true);
                    SfxSynth.Play(SfxSynth.Id.Pickup, 0.4f);
                }
                if (_lifting)
                {
                    Vector3 w = MouseWorldOnCharPlane();
                    _pressedAgent.DragTo(new Vector2(
                        Mathf.Clamp(w.x, MinX, MaxX),
                        Mathf.Clamp(w.y, BistroDirector.GroundY + 0.6f, BistroDirector.GroundY + 2.6f)));
                }
            }

            // ---- 按住空地：镜头平移 ----
            if (_dragging && Input.GetMouseButton(0))
            {
                float dxPix = Input.mousePosition.x - _dragStartMouse.x;
                if (Mathf.Abs(dxPix) > 9f) _dragMoved = true; // 超过阈值才算拖拽（区分空地点击）
                if (_dragMoved)
                {
                    float worldPerPixel = VisibleWorldWidth() / Mathf.Max(1, Screen.width);
                    float x = Mathf.Clamp(_dragStartCamX - dxPix * worldPerPixel, -PanLimit, PanLimit);
                    _rig.PanTo(x, CamY()); // 拖拽 = 停止跟随
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (_lifting)
                {
                    // 放下：落回地面，岗位/巡场锚点迁到新位置
                    _pressedAgent.SetHeld(false);
                    _pressedAgent.PlaceAt(new Vector2(
                        Mathf.Clamp(_pressedAgent.transform.position.x, MinX, MaxX),
                        BistroDirector.GroundY));
                    SfxSynth.Play(SfxSynth.Id.Click, 0.4f);
                }
                else if (_pressedAgent != null)
                {
                    Select(_pressedAgent); // 短按未拖动 = 点选
                }
                else if (_dragging && !_dragMoved && !OverUI())
                {
                    Deselect(); // 点空地（未拖动）→ 取消选中
                }
                _pressedAgent = null;
                _lifting = false;
                _dragging = false;
            }
        }

        StaffAgent RaycastAgent(Vector3 screenPos)
        {
            Ray ray = _rig.Cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                return hit.collider.GetComponentInParent<StaffAgent>();
            return null;
        }

        /// <summary>鼠标在角色平面（z=0）上的世界坐标（正交/透视通用）。</summary>
        Vector3 MouseWorldOnCharPlane()
        {
            Ray ray = _rig.Cam.ScreenPointToRay(Input.mousePosition);
            float t = Mathf.Abs(ray.direction.z) < 1e-5f ? 0f : -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * Mathf.Max(0f, t);
        }

        public void Select(StaffAgent agent)
        {
            if (agent == _selected) return;
            Deselect();
            _selected = agent;
            _marker = SelectionMarker.Attach(agent.transform, agent.IsBoss); // 老板=金色水晶
            _selected.PlayerControlled = agent.IsBoss; // 老板选中即接管操控（方向键移动）
            // 相机居中跟随（offsetX=0），范围限制在舞台内
            _rig.Follow(agent.transform, CamY(), -PanLimit, PanLimit, 0f);
            SfxSynth.Play(SfxSynth.Id.Click, 0.35f);
        }

        public void Deselect()
        {
            if (_marker != null) Object.Destroy(_marker.gameObject);
            _marker = null;
            if (_selected != null)
            {
                _selected.PlayerControlled = false; // 交还 AI
                if (_rig != null)
                    _rig.PanTo(Mathf.Clamp(_rig.transform.position.x, -PanLimit, PanLimit), CamY());
            }
            _selected = null;
        }

        float CamY() => _perspective ? PerspY : 0f;

        float VisibleWorldWidth()
        {
            Camera cam = _rig.Cam;
            if (cam.orthographic) return cam.orthographicSize * 2f * cam.aspect;
            float dist = Mathf.Abs(cam.transform.position.z); // 到角色平面 z=0 的距离
            return 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
        }

        static bool OverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }

    /// <summary>选中标识：头顶旋转水晶（双交叉菱形 + 柔光），自转 + 上下浮动。员工绿色，老板金色。</summary>
    public class SelectionMarker : MonoBehaviour
    {
        Transform _spin;
        bool _gold;
        const float BaseY = 2.35f; // 名牌(1.95)之上

        public static SelectionMarker Attach(Transform owner, bool boss = false)
        {
            var go = new GameObject("SelectionMarker");
            go.transform.SetParent(owner, false);
            var m = go.AddComponent<SelectionMarker>();
            m._gold = boss;
            m.BuildVisual();
            return m;
        }

        void BuildVisual()
        {
            transform.localPosition = new Vector3(0f, BaseY, 0f);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(transform, false);

            var gem = _gold ? new Color(1f, 0.84f, 0.32f) : new Color(0.35f, 0.95f, 0.45f);
            var a = SpriteFactory.NewSprite("DiamondA", _spin,
                SpriteFactory.Rect(0.24f, 0.24f, gem, 0.03f), Vector2.zero, 58);
            a.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var b = SpriteFactory.NewSprite("DiamondB", _spin,
                SpriteFactory.Rect(0.24f, 0.24f, gem, 0.03f), Vector2.zero, 58);
            b.transform.localRotation = Quaternion.Euler(0f, 90f, 45f); // 交叉面，自转时有体积感

            SpriteFactory.NewSprite("Glow", transform,
                SpriteFactory.RadialGlow(0.9f, new Color(0.4f, 1f, 0.5f, 0.35f)), Vector2.zero, 57);
            Juice.PopIn(transform);
        }

        void Update()
        {
            _spin.localRotation = Quaternion.Euler(0f, Time.time * 170f % 360f, 0f);
            transform.localPosition = new Vector3(0f, BaseY + Mathf.Sin(Time.time * 3.4f) * 0.08f, 0f);
        }
    }
}
