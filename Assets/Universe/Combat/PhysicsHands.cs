using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;
using RealityEngine.Audio;
using RealityEngine.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Combat
{
    /// <summary>
    /// Physics-driven hands. Held weapons stay fully dynamic and are pulled toward the hand pose with a
    /// force/torque PD drive (clamped by strength), so heavy weapons lag, weapons stop at walls and
    /// collisions push back. XR: grip grabs the nearest weapon grip (second hand on a two-handed weapon
    /// adds strength and steers the blade). Desktop: E grab, LMB slash, RMB thrust, hold Alt block,
    /// F drop, R throw (synthetic hand arcs in front of the camera).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class PhysicsHands : MonoBehaviour
    {
        public const string RootName = "QhysicsPhysicsHands";
        public const float OneHandForce = 320f;   // N
        public const float OneHandTorque = 28f;   // N·m
        public const float TwoHandMult = 1.8f;
        public const float GrabRadius = 0.2f;

        public static PhysicsHands Instance { get; private set; }

        /// <summary>Desktop is holding a physics weapon: DesktopInteractor stands down.</summary>
        public static bool DesktopBusy => Instance != null && Instance._desk.held != null;

        sealed class Hand
        {
            public XRNode node;
            public PhysicsWeapon held;
            public bool secondary;
            public float gripY;
            public Quaternion relRot = Quaternion.identity;
            public Vector3 pos, prevPos, vel;
            public Quaternion rot = Quaternion.identity;
            public bool valid;
            public bool gripDown;
            public float farTime;
        }

        readonly Hand _l = new Hand { node = XRNode.LeftHand };
        readonly Hand _r = new Hand { node = XRNode.RightHand };
        readonly Hand _desk = new Hand { node = XRNode.RightHand };

        enum DeskMove { Idle, Slash, Thrust }
        DeskMove _move;
        float _moveT;
        float _moveDur;
        bool _slashFromRight = true;
        bool _block;

        // Canonical hold: weapon +Y along hand forward, weapon +X along hand up.
        static readonly Quaternion CanonRel = Quaternion.LookRotation(Vector3.right, Vector3.forward);
        static readonly Quaternion ReverseRel = Quaternion.LookRotation(Vector3.left, Vector3.back);

        public static PhysicsHands Ensure()
        {
            if (Instance != null)
                return Instance;
            var go = GameObject.Find(RootName);
            if (go == null)
                go = new GameObject(RootName);
            var ph = go.GetComponent<PhysicsHands>();
            if (ph == null)
                ph = go.AddComponent<PhysicsHands>();
            Instance = ph;
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public PhysicsWeapon HeldIn(XRNode node)
        {
            if (!CombatInputGate.IsXr)
                return node == XRNode.RightHand ? _desk.held : null;
            return node == XRNode.LeftHand ? _l.held : _r.held;
        }

        public PhysicsWeapon AnyHeld => _desk.held != null ? _desk.held : (_r.held != null ? _r.held : _l.held);

        // ------------------------------------------------------------------ Update (poses + input)

        void Update()
        {
            if (CombatInputGate.IsXr)
            {
                if (_desk.held != null) Release(_desk, Vector3.zero, false);
                UpdateXrHand(_l, _r);
                UpdateXrHand(_r, _l);
            }
            else
            {
                if (_l.held != null) Release(_l, Vector3.zero, false);
                if (_r.held != null) Release(_r, Vector3.zero, false);
                UpdateDesktop();
            }
        }

        void UpdateXrHand(Hand h, Hand other)
        {
            h.valid = XrTrackingSpace.TryGetWorldPose(h.node, out Vector3 p, out Quaternion q);
            if (!h.valid)
                return;
            h.prevPos = h.pos;
            h.pos = p;
            h.rot = q;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            Vector3 v = (h.pos - h.prevPos) / dt;
            h.vel = Vector3.Lerp(h.vel, v, 0.5f);

            InputDevice dev = InputDevices.GetDeviceAtXRNode(h.node);
            float grip = 0f;
            if (dev.isValid)
                dev.TryGetFeatureValue(CommonUsages.grip, out grip);
            bool was = h.gripDown;
            h.gripDown = was ? grip > 0.35f : grip > 0.6f;
            bool blocked = CombatInputGate.Blocked;

            if (h.gripDown && !was && h.held == null && !blocked)
                TryXrGrab(h, other);
            else if (!h.gripDown && was && h.held != null)
                Release(h, h.vel, true);

            // Distance break: weapon stuck behind a wall or yanked away.
            if (h.held != null && !h.secondary)
            {
                Vector3 grip0 = h.held.transform.TransformPoint(0f, h.gripY, 0f);
                if ((grip0 - h.pos).sqrMagnitude > 0.8f * 0.8f)
                {
                    h.farTime += Time.deltaTime;
                    if (h.farTime > 0.75f)
                        Release(h, Vector3.zero, false);
                }
                else h.farTime = 0f;
            }
        }

        void TryXrGrab(Hand h, Hand other)
        {
            PhysicsWeapon best = null;
            float bestD = GrabRadius;
            float bestY = 0f;
            for (int i = 0; i < PhysicsWeapon.All.Count; i++)
            {
                var w = PhysicsWeapon.All[i];
                if (w == null || w.ownerTeam == CombatTeam.Enemy) continue;
                if (w.IsHeld && !(w.twoHanded && other.held == w && !other.secondary)) continue;
                Vector3 gp = w.ClosestGripPoint(h.pos, out float gy);
                float d = Vector3.Distance(gp, h.pos);
                if (d < bestD) { bestD = d; best = w; bestY = gy; }
            }
            if (best == null)
                return;

            if (best.IsHeld)
            {
                // Second hand on a two-handed weapon.
                h.held = best;
                h.secondary = true;
                h.gripY = bestY;
                best.HeldCount++;
                CombatHaptics.Pulse(h.node, 0.3f, 0.05f);
                return;
            }
            Attach(h, best, bestY, h.rot);
            best.PrimaryNode = h.node;
            CombatHaptics.Pulse(h.node, 0.4f, 0.06f);
        }

        void Attach(Hand h, PhysicsWeapon w, float gripY, Quaternion handRot)
        {
            if (w.IsStuck && !w.IsHeld)
                w.Unstick();
            h.held = w;
            h.secondary = false;
            h.gripY = gripY;
            h.farTime = 0f;
            // Keep a reverse grip if the blade already points back along the hand.
            Vector3 handFwd = handRot * Vector3.forward;
            h.relRot = Vector3.Dot(w.Axis, handFwd) >= 0f || w.kind == WeaponKind.Shield ? CanonRel : ReverseRel;
            w.HeldCount = 1;
            w.HeldByPlayer = true;
            w.MarkPlayerHold();
            IgnorePlayer(w, true);
            QhysicsSfx.PlayAt(SfxId.Whoosh, w.transform.position, 0.25f, 1.6f);
        }

        void Release(Hand h, Vector3 handVel, bool throwBoost)
        {
            var w = h.held;
            h.held = null;
            if (w == null)
                return;
            w.HeldCount = Mathf.Max(0, w.HeldCount - 1);
            if (h.secondary)
            {
                h.secondary = false;
                return;
            }
            // Primary released: a remaining secondary hand becomes primary.
            Hand other = h == _l ? _r : h == _r ? _l : null;
            if (other != null && other.held == w && other.secondary)
            {
                other.secondary = false;
                other.relRot = CanonRel;
                w.PrimaryNode = other.node;
                return;
            }
            w.HeldCount = 0;
            w.HeldByPlayer = false;
            w.MarkPlayerHold();
            if (throwBoost && handVel.sqrMagnitude > 4f)
            {
                // Momentum is already in the body; add a little of the hand's velocity for a readable throw.
                w.Body.linearVelocity = Vector3.Lerp(w.Body.linearVelocity, handVel * SkillSystem.ThrowMult, 0.5f);
            }
            StartCoroutine(UnignoreLater(w));
        }

        System.Collections.IEnumerator UnignoreLater(PhysicsWeapon w)
        {
            yield return new WaitForSeconds(0.5f);
            if (w != null && !w.IsHeld)
                IgnorePlayer(w, false);
        }

        static readonly List<Collider> _playerCols = new List<Collider>();

        static void IgnorePlayer(PhysicsWeapon w, bool ignore)
        {
            _playerCols.Clear();
            var origin = GameObject.Find("XR Origin");
            if (origin != null)
                origin.GetComponentsInChildren(true, _playerCols);
            var desk = DesktopPlayerController.Instance;
            if (desk != null && desk.Origin != null && (origin == null || desk.Origin.gameObject != origin))
            {
                var extra = desk.Origin.GetComponentsInChildren<Collider>(true);
                _playerCols.AddRange(extra);
            }
            var hurt = PlayerHurtbox.Instance != null ? PlayerHurtbox.Instance.Collider : null;
            if (hurt != null)
                _playerCols.Add(hurt);
            foreach (var pc in _playerCols)
            {
                if (pc == null) continue;
                foreach (var wc in w.Colliders)
                    if (wc != null) UnityEngine.Physics.IgnoreCollision(wc, pc, ignore);
            }
        }

        // ------------------------------------------------------------------ Desktop

        public void DesktopGrab(PhysicsWeapon w)
        {
            if (w == null || CombatInputGate.IsXr || w.ownerTeam == CombatTeam.Enemy)
                return;
            if (_desk.held != null)
                Release(_desk, Vector3.zero, false);
            var cam = CombatInputGate.ViewCamera();
            if (cam == null)
                return;
            Attach(_desk, w, Mathf.Lerp(w.gripMinY, w.gripMaxY, 0.4f), cam.transform.rotation);
            _desk.relRot = CanonRel;
            w.PrimaryNode = XRNode.RightHand;
            _move = DeskMove.Idle;
            DesktopPose(cam.transform, out _desk.pos, out _desk.rot);
            _desk.prevPos = _desk.pos;
        }

        void UpdateDesktop()
        {
            var cam = CombatInputGate.ViewCamera();
            if (cam == null || _desk.held == null)
                return;

            bool blocked = CombatInputGate.Blocked;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (!blocked && kb != null && mouse != null)
            {
                _block = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
                if (!_block && _move == DeskMove.Idle)
                {
                    if (mouse.leftButton.wasPressedThisFrame)
                        StartMove(DeskMove.Slash, Mathf.Lerp(0.26f, 0.42f, Mathf.InverseLerp(0.4f, 3f, _desk.held.Body.mass)));
                    else if (mouse.rightButton.wasPressedThisFrame && _desk.held.kind != WeaponKind.Shield)
                        StartMove(DeskMove.Thrust, 0.22f);
                }
                if (kb.fKey.wasPressedThisFrame)
                {
                    Release(_desk, Vector3.zero, false);
                    return;
                }
                if (kb.rKey.wasPressedThisFrame)
                {
                    DesktopThrow(cam.transform);
                    return;
                }
            }
            else
                _block = false;
#endif
            if (_move != DeskMove.Idle)
            {
                _moveT += Time.deltaTime / Mathf.Max(0.05f, _moveDur);
                if (_moveT >= 1.6f) // 1.0 = strike end, 0.6 more = recover
                    _move = DeskMove.Idle;
            }

            _desk.prevPos = _desk.pos;
            DesktopPose(cam.transform, out _desk.pos, out _desk.rot);
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            _desk.vel = Vector3.Lerp(_desk.vel, (_desk.pos - _desk.prevPos) / dt, 0.6f);

            // Weapon wedged behind geometry: snap back to the hand.
            Vector3 grip0 = _desk.held.transform.TransformPoint(0f, _desk.gripY, 0f);
            if ((grip0 - _desk.pos).sqrMagnitude > 1.6f * 1.6f && !_desk.held.IsStuck)
            {
                _desk.held.Body.position = _desk.pos - _desk.rot * _desk.relRot * new Vector3(0f, _desk.gripY, 0f);
                _desk.held.Body.linearVelocity = Vector3.zero;
            }
        }

        void StartMove(DeskMove m, float dur)
        {
            if (_desk.held != null && _desk.held.IsStuck)
                _desk.held.Unstick();
            _move = m;
            _moveT = 0f;
            _moveDur = dur;
            if (m == DeskMove.Slash)
                _slashFromRight = !_slashFromRight;
            QhysicsSfx.PlayAt(SfxId.Whoosh, _desk.pos, 0.45f, m == DeskMove.Thrust ? 1.3f : 1f);
        }

        void DesktopThrow(Transform cam)
        {
            var w = _desk.held;
            if (w == null) return;
            Release(_desk, Vector3.zero, false);
            Vector3 fwd = cam.forward;
            bool pointy = w.kind == WeaponKind.Dagger || w.kind == WeaponKind.Spear;
            if (pointy)
            {
                w.Body.rotation = Quaternion.LookRotation(fwd, cam.up) * Quaternion.Euler(90f, 0f, 0f);
                w.Body.angularVelocity = Vector3.zero;
            }
            else
                w.Body.angularVelocity = cam.right * 12f;
            float speed = Mathf.Lerp(16f, 9f, Mathf.InverseLerp(0.4f, 3f, w.Body.mass)) * SkillSystem.ThrowMult;
            w.Body.linearVelocity = fwd * speed + cam.up * 0.8f;
            w.MarkPlayerHold();
            QhysicsSfx.PlayAt(SfxId.Whoosh, w.transform.position, 0.6f, 1.2f);
        }

        void DesktopPose(Transform cam, out Vector3 pos, out Quaternion rot)
        {
            bool shield = _desk.held != null && _desk.held.kind == WeaponKind.Shield;
            Vector3 lp;
            Quaternion lr;
            if (_block)
            {
                if (shield) { lp = new Vector3(-0.05f, -0.08f, 0.42f); lr = Quaternion.identity; }
                else { lp = new Vector3(0f, 0.08f, 0.45f); lr = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(-10f, 0f, 0f); }
            }
            else
            {
                Vector3 idleP = shield ? new Vector3(-0.3f, -0.32f, 0.42f) : new Vector3(0.26f, -0.3f, 0.45f);
                Quaternion idleR = shield ? Quaternion.Euler(10f, 0f, 0f) : Quaternion.Euler(-50f, 0f, 0f);
                lp = idleP;
                lr = idleR;
                if (_move == DeskMove.Slash)
                {
                    float s = _slashFromRight ? 1f : -1f;
                    Vector3 a = new Vector3(0.45f * s, 0.18f, 0.32f);
                    Vector3 c = new Vector3(0.05f * s, 0.05f, 0.78f);
                    Vector3 b = new Vector3(-0.38f * s, -0.32f, 0.45f);
                    Quaternion ra = Quaternion.Euler(-70f, 60f * s, 0f);
                    Quaternion rb = Quaternion.Euler(25f, -75f * s, 0f);
                    if (_moveT < 0.25f)
                    {
                        float k = Smooth(_moveT / 0.25f); // wind-up
                        lp = Vector3.Lerp(idleP, a, k); lr = Quaternion.Slerp(idleR, ra, k);
                    }
                    else if (_moveT < 1f)
                    {
                        float k = Smooth((_moveT - 0.25f) / 0.75f);
                        lp = Bezier(a, c, b, k); lr = Quaternion.Slerp(ra, rb, k);
                    }
                    else
                    {
                        float k = Smooth((_moveT - 1f) / 0.6f);
                        lp = Vector3.Lerp(b, idleP, k); lr = Quaternion.Slerp(rb, idleR, k);
                    }
                }
                else if (_move == DeskMove.Thrust)
                {
                    Vector3 back = new Vector3(0.18f, -0.18f, 0.28f);
                    Vector3 front = new Vector3(0.02f, -0.1f, 1.0f);
                    Quaternion level = Quaternion.Euler(-4f, -4f, 0f);
                    if (_moveT < 0.3f)
                    {
                        float k = Smooth(_moveT / 0.3f);
                        lp = Vector3.Lerp(idleP, back, k); lr = Quaternion.Slerp(idleR, level, k);
                    }
                    else if (_moveT < 1f)
                    {
                        float k = Mathf.Clamp01((_moveT - 0.3f) / 0.7f);
                        k = 1f - (1f - k) * (1f - k);
                        lp = Vector3.Lerp(back, front, k); lr = level;
                    }
                    else
                    {
                        float k = Smooth((_moveT - 1f) / 0.6f);
                        lp = Vector3.Lerp(front, idleP, k); lr = Quaternion.Slerp(level, idleR, k);
                    }
                }
            }
            pos = cam.TransformPoint(lp);
            rot = cam.rotation * lr;
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t) { float u = 1f - t; return u * u * a + 2f * u * t * c + t * t * b; }

        // ------------------------------------------------------------------ FixedUpdate (drive)

        void FixedUpdate()
        {
            if (CombatInputGate.IsXr)
            {
                DriveHand(_l, _r);
                DriveHand(_r, _l);
            }
            else
                DriveHand(_desk, null);
        }

        void DriveHand(Hand h, Hand other)
        {
            var w = h.held;
            if (w == null || h.secondary || w.Body == null)
                return;
            if (w.Body.isKinematic)
                w.Body.isKinematic = false;
            Quaternion targetRot = h.rot * h.relRot;
            bool twoHand = other != null && other.held == w && other.secondary && other.valid;
            if (twoHand)
            {
                // Blade axis follows the line between the hands.
                float sign = h.gripY >= other.gripY ? 1f : -1f;
                Vector3 line = (h.pos - other.pos) * sign;
                if (line.sqrMagnitude > 1e-4f)
                {
                    Vector3 curUp = targetRot * Vector3.up;
                    targetRot = Quaternion.FromToRotation(curUp, line.normalized) * targetRot;
                }
            }
            Vector3 targetPos = h.pos - targetRot * new Vector3(0f, h.gripY, 0f);
            float strength = SkillSystem.StrengthMult * (twoHand || (!CombatInputGate.IsXr && w.twoHanded) ? TwoHandMult : 1f);
            Drive(w.Body, targetPos, targetRot, h.vel, OneHandForce * strength, OneHandTorque * strength, 0.035f, 0.05f);
        }

        /// <summary>PD drive toward a pose using clamped forces (shared with enemy weapons).</summary>
        public static void Drive(Rigidbody rb, Vector3 targetPos, Quaternion targetRot, Vector3 targetVel,
            float maxForce, float maxTorque, float tauPos, float tauRot)
        {
            float dt = Time.fixedDeltaTime;
            float ts = Mathf.Max(0.05f, Time.timeScale);
            // Linear
            Vector3 err = targetPos - rb.position;
            Vector3 vDes = err / Mathf.Max(dt * 1.5f, tauPos * ts) + targetVel;
            Vector3 f = rb.mass * (vDes - rb.linearVelocity) / dt;
            if (rb.useGravity)
                f -= UnityEngine.Physics.gravity * rb.mass;
            f = Vector3.ClampMagnitude(f, maxForce / ts);
            rb.AddForce(f, ForceMode.Force);
            // Angular
            Quaternion q = targetRot * Quaternion.Inverse(rb.rotation);
            if (q.w < 0f) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
            q.ToAngleAxis(out float angDeg, out Vector3 axis);
            Vector3 wDes = Vector3.zero;
            if (angDeg > 0.01f && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
                wDes = axis.normalized * (angDeg * Mathf.Deg2Rad) / Mathf.Max(dt * 1.5f, tauRot * ts);
            Vector3 dw = (wDes - rb.angularVelocity) / dt;
            Quaternion R = rb.rotation * rb.inertiaTensorRotation;
            Vector3 torque = R * Vector3.Scale(rb.inertiaTensor, Quaternion.Inverse(R) * dw);
            torque = Vector3.ClampMagnitude(torque, maxTorque / ts);
            rb.AddTorque(torque, ForceMode.Force);
        }
    }
}
