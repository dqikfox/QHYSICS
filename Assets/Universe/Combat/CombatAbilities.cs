using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using RealityEngine.Audio;
using RealityEngine.Player;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Combat
{
    /// <summary>
    /// Skill-tree abilities (POWERS branch):
    /// GROUND SLAM - XR: punch both fists down fast (both hands &gt; 3 m/s downward within 0.15 s, below chest height),
    ///   desktop F11. Radial shockwave at your feet: knocks enemies/props out and up, Force damage, 30 mana, 4 s cooldown.
    ///   Rank 1/2/3: radius 4/5/6 m, damage 14/22/30 (x spell power).
    /// BLADE RECALL - XR: hold grip on an empty hand for 0.5 s (with the trigger released); your nearest loose weapon
    ///   (not an enemy's) flies back on a homing path and is caught if you are still squeezing. Desktop F12 (lands in hand).
    ///   Rank 1/2: range 20/30 m. 10 mana, 1.5 s cooldown.
    /// Haptics on charge/cast/catch. Locked abilities give a denied buzz + hint. Runtime only.
    /// </summary>
    [DefaultExecutionOrder(212)]
    public sealed class CombatAbilities : MonoBehaviour
    {
        static CombatAbilities _instance;
        public static float SlamReadyAt { get; private set; }
        public static float RecallReadyAt { get; private set; }

        const float SlamMana = 30f, SlamCooldown = 4f;
        const float RecallMana = 10f, RecallCooldown = 1.5f, RecallHold = 0.5f;

        sealed class HandTrack { public XRNode node; public Vector3 pos; public bool valid; public float downAt = -9f; public float gripHeld; public bool fired; }
        readonly HandTrack _l = new HandTrack { node = XRNode.LeftHand };
        readonly HandTrack _r = new HandTrack { node = XRNode.RightHand };
        float _lastLockedHint = -99f;

        public static CombatAbilities Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("QhysicsCombatAbilities");
            _instance = go.AddComponent<CombatAbilities>();
            return _instance;
        }

        void Awake() { _instance = this; }

        void Update()
        {
            if (CombatInputGate.Blocked) { _l.gripHeld = _r.gripHeld = 0f; return; }
            if (CombatInputGate.IsXr) UpdateXr();
#if ENABLE_INPUT_SYSTEM
            else
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.f11Key.wasPressedThisFrame) TrySlam(XRNode.RightHand);
                    if (kb.f12Key.wasPressedThisFrame) TryRecall(XRNode.RightHand, null);
                }
            }
#endif
        }

        void UpdateXr()
        {
            Track(_l); Track(_r);
            // Ground slam gesture: both fists driven down together.
            if (_l.valid && _r.valid && Mathf.Abs(_l.downAt - _r.downAt) < 0.15f && Time.unscaledTime - Mathf.Max(_l.downAt, _r.downAt) < 0.05f
                && SpellSystem.CombatModeActive())
            {
                _l.downAt = _r.downAt = -9f;
                TrySlam(XRNode.RightHand);
            }
            Recall(_l); Recall(_r);
        }

        void Track(HandTrack h)
        {
            bool ok = XrTrackingSpace.TryGetWorldPose(h.node, out Vector3 p, out _);
            float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
            if (ok && h.valid)
            {
                Vector3 v = (p - h.pos) / dt;
                var cam = CombatInputGate.ViewCamera();
                bool low = cam == null || p.y < cam.transform.position.y - 0.35f;
                if (v.y < -3f && low && Time.unscaledTime - h.downAt > 0.3f) h.downAt = Time.unscaledTime;
            }
            h.valid = ok;
            if (ok) h.pos = p;
        }

        void Recall(HandTrack h)
        {
            var ph = PhysicsHands.Instance;
            if (ph == null || !h.valid) return;
            float trig = 0f;
            var dev = InputDevices.GetDeviceAtXRNode(h.node);
            if (dev.isValid) dev.TryGetFeatureValue(CommonUsages.trigger, out trig);
            bool hold = ph.GripDown(h.node) && ph.HeldIn(h.node) == null && trig < 0.3f;
            if (!hold) { h.gripHeld = 0f; h.fired = false; return; }
            h.gripHeld += Time.unscaledDeltaTime;
            if (!h.fired && h.gripHeld >= RecallHold && SkillSystem.Rank("blade_recall") > 0)
            {
                h.fired = true;
                TryRecall(h.node, h);
            }
        }

        void LockedHint(string name)
        {
            QhysicsSfx.Play2D(SfxId.Denied, 0.4f);
            if (Time.unscaledTime - _lastLockedHint < 4f) return;
            _lastLockedHint = Time.unscaledTime;
            CombatHud.Toast(name + " is locked - unlock it in Skills > POWERS");
        }

        // ------------------------------------------------------------------ Ground slam

        public void TrySlam(XRNode node)
        {
            int rank = SkillSystem.Rank("ground_slam");
            if (rank <= 0) { LockedHint("Ground Slam"); return; }
            if (Time.unscaledTime < SlamReadyAt) { QhysicsSfx.Play2D(SfxId.Denied, 0.3f); return; }
            if (!ManaPool.TrySpend(SlamMana)) return;
            SlamReadyAt = Time.unscaledTime + SlamCooldown;
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return;
            Vector3 head = cam.transform.position;
            Vector3 feet = head + Vector3.down * 1.6f;
            var hits = UnityEngine.Physics.RaycastAll(head, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var hh in hits)
                if (!CombatInputGate.IsPlayerCollider(hh.collider) && hh.distance < best) { best = hh.distance; feet = hh.point; }

            float radius = 3f + rank;
            float dmg = (6f + 8f * rank) * SkillSystem.SpellPowerMult;
            QhysicsSfx.PlayAt(SfxId.Shockwave, feet, 1f);
            QhysicsSfx.PlayAt(SfxId.ForcePush, feet, 0.7f, 0.6f);
            CombatHaptics.Both(1f, 0.25f);
            SlamRingVfx.Spawn(feet, radius);

            var seen = new HashSet<Rigidbody>();
            var done = new HashSet<object>();
            foreach (var c in UnityEngine.Physics.OverlapSphere(feet, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (CombatInputGate.IsPlayerCollider(c)) continue;
                var pw = c.GetComponentInParent<PhysicsWeapon>();
                if (pw != null && pw.HeldByPlayer && pw.IsHeld) continue;
                Vector3 to = c.bounds.center - feet; to.y = 0f;
                float d = to.magnitude;
                float fall = 1f - Mathf.Clamp01(d / radius);
                Vector3 dir = (d > 0.01f ? to / d : Vector3.forward) * 0.8f + Vector3.up * 0.6f;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic && seen.Add(rb))
                    rb.AddForce(dir.normalized * Mathf.Lerp(3f, 11f, fall) * SkillSystem.ForcePowerMult, ForceMode.VelocityChange);
                var t = c.GetComponentInParent<ICombatTarget>();
                if (t != null && t.IsAlive && t.Team != CombatTeam.Player && done.Add(t is BodyPart bp && bp.health != null ? (object)bp.health : t))
                    t.TakeHit(new DamageInfo { amount = dmg * Mathf.Lerp(0.5f, 1f, fall), type = DamageType.Force, point = c.bounds.center,
                        impulse = dir.normalized * 25f * fall, sourceTeam = CombatTeam.Player });
            }
            Debug.Log("CombatAbilities: ground slam r" + rank + " radius " + radius + " m, " + done.Count + " target(s)");
        }

        // ------------------------------------------------------------------ Blade recall

        void TryRecall(XRNode node, HandTrack h)
        {
            int rank = SkillSystem.Rank("blade_recall");
            if (rank <= 0) { LockedHint("Blade Recall"); return; }
            if (Time.unscaledTime < RecallReadyAt) return;
            Vector3 handPos;
            if (h != null) handPos = h.pos;
            else { var cam = CombatInputGate.ViewCamera(); if (cam == null) return; handPos = cam.transform.position + cam.transform.forward * 0.5f + Vector3.down * 0.3f; }
            float range = rank >= 2 ? 30f : 20f;
            PhysicsWeapon best = null;
            float bd = range;
            foreach (var w in PhysicsWeapon.All)
            {
                if (w == null || w.IsHeld || w.ownerTeam == CombatTeam.Enemy || w.Body == null) continue;
                if (w.GetComponent<RecallFlight>() != null) continue;
                float d = Vector3.Distance(w.Body.worldCenterOfMass, handPos);
                if (d < 0.6f) continue;
                if (d < bd) { bd = d; best = w; }
            }
            if (best == null) { QhysicsSfx.Play2D(SfxId.Denied, 0.35f); CombatHaptics.Pulse(node, 0.1f, 0.03f); return; }
            if (!ManaPool.TrySpend(RecallMana)) return;
            RecallReadyAt = Time.unscaledTime + RecallCooldown;
            if (best.IsStuck) best.Unstick();
            best.MarkPlayerHold();
            var f = best.gameObject.AddComponent<RecallFlight>();
            f.Init(best, node, h != null, rank);
            QhysicsSfx.PlayAt(SfxId.Recall, best.transform.position, 0.8f);
            CombatHaptics.Pulse(node, 0.35f, 0.08f);
            LightningVfx.Bolt(handPos, best.transform.position, new Color(0f, 0.9f, 1f, 0.8f), 0.25f);
        }
    }

    /// <summary>Homing flight that steers a recalled weapon into the hand; caught on arrival (XR if grip still held, desktop always).</summary>
    public sealed class RecallFlight : MonoBehaviour
    {
        PhysicsWeapon _w;
        XRNode _node;
        bool _xr;
        float _speed, _t;

        public void Init(PhysicsWeapon w, XRNode node, bool xr, int rank)
        {
            _w = w; _node = node; _xr = xr; _speed = rank >= 2 ? 18f : 14f;
        }

        Vector3 Target()
        {
            if (_xr && XrTrackingSpace.TryGetWorldPose(_node, out Vector3 p, out _)) return p;
            var cam = CombatInputGate.ViewCamera();
            return cam != null ? cam.transform.position + cam.transform.forward * 0.5f + Vector3.down * 0.3f : _w.transform.position;
        }

        void FixedUpdate()
        {
            if (_w == null || _w.Body == null || _w.IsHeld) { Destroy(this); return; }
            _t += Time.fixedDeltaTime;
            Vector3 target = Target();
            Vector3 to = target - _w.Body.worldCenterOfMass;
            float d = to.magnitude;
            if (d < 0.3f || _t > 2f)
            {
                _w.Body.linearVelocity = Vector3.zero;
                _w.Body.angularVelocity = Vector3.zero;
                var ph = PhysicsHands.Instance;
                bool caught = false;
                if (ph != null)
                {
                    if (_xr) caught = ph.TryGrabArrived(_node);
                    else if (d < 1f) { ph.DesktopGrab(_w); caught = true; }
                }
                if (caught)
                {
                    QhysicsSfx.PlayAt(SfxId.Catch, target, 0.8f);
                    CombatHaptics.Pulse(_node, 0.8f, 0.1f);
                }
                Destroy(this);
                return;
            }
            float sp = Mathf.Min(_speed, d / Mathf.Max(0.02f, Time.fixedDeltaTime) * 0.5f);
            _w.Body.linearVelocity = Vector3.Lerp(_w.Body.linearVelocity, to / d * sp, 0.35f);
            _w.Body.angularVelocity *= 0.9f;
            _w.Body.WakeUp();
        }
    }

    /// <summary>Expanding cyan ground ring for Ground Slam (no collider, unscaled time).</summary>
    public sealed class SlamRingVfx : MonoBehaviour
    {
        float _born, _radius;
        Renderer _r;
        const float Life = 0.45f;

        public static void Spawn(Vector3 at, float radius)
        {
            var g = CombatUtil.Prim(PrimitiveType.Cylinder, "SlamRing", null, at + Vector3.up * 0.03f, new Vector3(0.2f, 0.01f, 0.2f),
                CombatMaterials.Transparent(new Color(0f, 0.9f, 1f, 0.55f), new Color(0f, 1.6f, 2f)), false);
            var v = g.AddComponent<SlamRingVfx>();
            v._born = Time.unscaledTime; v._radius = radius; v._r = g.GetComponent<Renderer>();
            SparkBurst.Spawn(at + Vector3.up * 0.1f, new Color(0.3f, 0.95f, 1f), 30);
        }

        void Update()
        {
            float u = (Time.unscaledTime - _born) / Life;
            if (u >= 1f) { Destroy(gameObject); return; }
            float s = Mathf.Lerp(0.2f, _radius * 2f, 1f - (1f - u) * (1f - u));
            transform.localScale = new Vector3(s, 0.01f, s);
            if (_r != null && _r.material.HasProperty("_BaseColor"))
            {
                var c = _r.material.GetColor("_BaseColor"); c.a = 0.55f * (1f - u); _r.material.SetColor("_BaseColor", c);
            }
        }
    }
}
