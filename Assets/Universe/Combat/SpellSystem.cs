using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;
using RealityEngine.Audio;
using RealityEngine.Player;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Combat
{
    public enum SpellId { Fire = 0, Lightning = 1, Force = 2 }

    /// <summary>Mana: 100 base (+skills), regenerates after a short delay.</summary>
    public static class ManaPool
    {
        public static float Max => 100f * SkillSystem.ManaMaxMult;
        public static float Current { get; private set; } = 100f;
        public static float Mana01 => Mathf.Clamp01(Current / Max);
        static float _lastSpend = -99f;

        public static bool TrySpend(float amount)
        {
            if (Current < amount)
            {
                QhysicsSfx.Play2D(SfxId.Denied, 0.4f);
                return false;
            }
            Current -= amount;
            _lastSpend = Time.unscaledTime;
            return true;
        }

        public static void Tick(float unscaledDt)
        {
            if (Time.unscaledTime - _lastSpend > 1f)
                Current = Mathf.Min(Max, Current + 12f * SkillSystem.ManaRegenMult * unscaledDt);
        }
    }

    /// <summary>Slow motion: timeScale 0.35 (fixedDeltaTime scaled with it), limited meter, respects pause.</summary>
    public static class FocusTime
    {
        public const float Scale = 0.35f;
        public static bool Active { get; private set; }
        public static float Meter01 { get; private set; } = 1f;
        static float _prevScale = 1f;
        static float _baseFixed = 1f / 90f; // restored exactly on exit
        static float _ourScale;

        public static void Toggle()
        {
            if (Active) End();
            else Begin();
        }

        public static void Begin()
        {
            if (Active || Meter01 < 0.15f || QhysicsPausePanel.AnyOpen || Time.timeScale <= 0f)
            {
                if (!Active) QhysicsSfx.Play2D(SfxId.Denied, 0.4f);
                return;
            }
            _prevScale = Time.timeScale;
            _baseFixed = Time.fixedDeltaTime;
            _ourScale = _prevScale * Scale;
            Time.timeScale = _ourScale;
            Time.fixedDeltaTime = _baseFixed * Scale; // keep physics steps per game-second constant
            Active = true;
            QhysicsSfx.Play2D(SfxId.FocusOn, 0.7f);
        }

        public static void End()
        {
            if (!Active) return;
            Active = false;
            // Only restore if nobody else (pause menu, sim speed) changed time meanwhile.
            if (Mathf.Approximately(Time.timeScale, _ourScale))
                Time.timeScale = _prevScale;
            Time.fixedDeltaTime = _baseFixed;
            QhysicsSfx.Play2D(SfxId.FocusOff, 0.6f);
        }

        public static void Tick(float unscaledDt)
        {
            if (Active)
            {
                if (!Mathf.Approximately(Time.timeScale, _ourScale))
                {
                    // Pause menu or another system took over time: drop focus without fighting it.
                    Active = false;
                    Time.fixedDeltaTime = _baseFixed;
                    return;
                }
                Meter01 = Mathf.Max(0f, Meter01 - unscaledDt / (5f * SkillSystem.FocusDurationMult));
                if (Meter01 <= 0f) End();
            }
            else
                Meter01 = Mathf.Min(1f, Meter01 + unscaledDt * 0.06f);
        }
    }

    /// <summary>
    /// Spells: Fire (projectile + burn), Lightning (chain), Force (tap = pull a loose weapon to your hand,
    /// charge = push wave). Desktop: hold V to charge, release to cast; Z next spell; B imbue held weapon;
    /// G focus. XR: hold trigger on an empty hand (combat mode), release to cast; left stick click next spell, hold it for focus;
    /// charge next to the blade in your other hand to imbue it.
    /// </summary>
    [DefaultExecutionOrder(152)]
    public sealed class SpellSystem : MonoBehaviour
    {
        public static SpellSystem Instance { get; private set; }
        public static SpellId Current { get; private set; } = SpellId.Fire;
        public static float Charge01 { get; private set; }
        public static bool Charging { get; private set; }

        sealed class CastHand
        {
            public XRNode node;
            public bool charging;
            public float t;
            public bool trigPrev;
            public bool primPrev;
            public float primDownAt = -9f;
            public GameObject orb;
        }

        readonly CastHand _desk = new CastHand { node = XRNode.RightHand };
        readonly CastHand _l = new CastHand { node = XRNode.LeftHand };
        readonly CastHand _r = new CastHand { node = XRNode.RightHand };
        float _pendingCycleAt = -1f;

        public static SpellSystem Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("QhysicsSpellSystem");
            Instance = go.AddComponent<SpellSystem>();
            return Instance;
        }

        public static string SpellName(SpellId s) => s == SpellId.Fire ? "Fire" : s == SpellId.Lightning ? "Lightning" : "Force";
        public static Color SpellColor(SpellId s) => s == SpellId.Fire ? new Color(1f, 0.45f, 0.1f) : s == SpellId.Lightning ? new Color(0.5f, 0.85f, 1f) : new Color(0.7f, 0.55f, 1f);

        public static void CycleSpell()
        {
            Current = (SpellId)(((int)Current + 1) % 3);
            QhysicsSfx.Play2D(SfxId.UiTick, 0.5f, 1.2f);
        }

        /// <summary>XR spells only fire in combat contexts so the trigger keeps working for UI/tools elsewhere.</summary>
        public static bool CombatModeActive()
        {
            if (CombatArena.Instance != null && CombatArena.Instance.PlayerInside()) return true;
            if (EnemyDirector.AliveCount() > 0) return true;
            var ph = PhysicsHands.Instance;
            return ph != null && ph.AnyHeld != null;
        }

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            ManaPool.Tick(udt);
            FocusTime.Tick(udt);
            if (CombatInputGate.Blocked)
            {
                CancelAll();
                return;
            }
            if (CombatInputGate.IsXr) UpdateXr();
            else UpdateDesktop();
            Charging = _desk.charging || _l.charging || _r.charging;
        }

        void CancelAll()
        {
            foreach (var h in new[] { _desk, _l, _r })
            {
                h.charging = false;
                h.t = 0f;
                if (h.orb != null) h.orb.SetActive(false);
            }
            Charging = false;
            Charge01 = 0f;
        }

        void UpdateDesktop()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return;
            if (kb.zKey.wasPressedThisFrame) CycleSpell();
            if (kb.gKey.wasPressedThisFrame) FocusTime.Toggle();
            if (kb.bKey.wasPressedThisFrame) TryImbue(PhysicsHands.Instance != null ? PhysicsHands.Instance.AnyHeld : null);

            Vector3 origin = cam.transform.TransformPoint(new Vector3(-0.22f, -0.2f, 0.45f));
            if (kb.vKey.isPressed)
            {
                if (!_desk.charging) { _desk.charging = true; _desk.t = 0f; QhysicsSfx.Play2D(SfxId.ChargeStart, 0.4f); }
                _desk.t += Time.unscaledDeltaTime;
                Charge01 = Mathf.Clamp01(_desk.t / 0.6f);
                ShowOrb(_desk, origin, Charge01);
            }
            else if (_desk.charging)
            {
                _desk.charging = false;
                HideOrb(_desk);
                Cast(Current, origin, cam.transform.forward, Mathf.Clamp01(_desk.t / 0.6f), _desk.t < 0.25f, XRNode.RightHand);
                Charge01 = 0f;
            }
#endif
        }

        void UpdateXr()
        {
            // Left stick click: short press = next spell, hold 0.5 s = focus (slow-mo).
            // (A/X show the teleport ray and teleport on release, so they are not used for combat.)
            bool clickNow = Btn(XRNode.LeftHand, CommonUsages.primary2DAxisClick);
            if (clickNow && !_l.primPrev)
            {
                _l.primDownAt = Time.unscaledTime;
                _pendingCycleAt = 1f; // armed
            }
            if (clickNow && _pendingCycleAt > 0f && Time.unscaledTime - _l.primDownAt >= 0.5f)
            {
                _pendingCycleAt = -1f;
                FocusTime.Toggle();
            }
            if (!clickNow && _l.primPrev && _pendingCycleAt > 0f)
            {
                _pendingCycleAt = -1f;
                CycleSpell();
            }
            _l.primPrev = clickNow;

            bool mode = CombatModeActive();
            XrHand(_l, _r, mode);
            XrHand(_r, _l, mode);
            Charge01 = Mathf.Max(_l.charging ? Mathf.Clamp01(_l.t / 0.6f) : 0f, _r.charging ? Mathf.Clamp01(_r.t / 0.6f) : 0f);
        }

        void XrHand(CastHand h, CastHand other, bool mode)
        {
            float trig = 0f;
            var dev = InputDevices.GetDeviceAtXRNode(h.node);
            if (dev.isValid) dev.TryGetFeatureValue(CommonUsages.trigger, out trig);
            bool down = h.trigPrev ? trig > 0.4f : trig > 0.7f;
            bool edge = down && !h.trigPrev;
            h.trigPrev = down;

            var ph = PhysicsHands.Instance;
            bool handBusy = ph != null && ph.HeldIn(h.node) != null;
            bool baton = h.node == XRNode.RightHand && PlayerCarryInventory.Instance != null
                && PlayerCarryInventory.Instance.EquippedItem == CarryItemId.TrainingBaton;
            if (!XrTrackingSpace.TryGetWorldPose(h.node, out Vector3 pos, out Quaternion rot))
                return;

            if (edge && mode && !handBusy && !baton)
            {
                h.charging = true;
                h.t = 0f;
                QhysicsSfx.PlayAt(SfxId.ChargeStart, pos, 0.4f);
                CombatHaptics.Pulse(h.node, 0.2f, 0.04f);
            }
            if (h.charging && down)
            {
                h.t += Time.unscaledDeltaTime;
                float c = Mathf.Clamp01(h.t / 0.6f);
                ShowOrb(h, pos + rot * Vector3.forward * 0.08f, c);
                if (c < 1f) CombatHaptics.Pulse(h.node, 0.05f + 0.15f * c, 0.02f);
                // Imbue: fully charged fire/lightning held against the blade in the other hand.
                var w = ph != null ? ph.HeldIn(other.node) : null;
                if (c >= 1f && w != null && Current != SpellId.Force)
                {
                    Vector3 onBlade = ClosestOnSegment(w.transform.position, w.TipWorld, pos);
                    if (Vector3.Distance(onBlade, pos) < 0.2f)
                    {
                        TryImbue(w);
                        h.charging = false;
                        HideOrb(h);
                    }
                }
            }
            else if (h.charging && !down)
            {
                h.charging = false;
                HideOrb(h);
                Cast(Current, pos + rot * Vector3.forward * 0.1f, rot * Vector3.forward, Mathf.Clamp01(h.t / 0.6f), h.t < 0.25f, h.node);
            }
        }

        static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return a + ab * t;
        }

        static bool Btn(XRNode node, InputFeatureUsage<bool> usage)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);
            return d.isValid && d.TryGetFeatureValue(usage, out bool b) && b;
        }

        void ShowOrb(CastHand h, Vector3 pos, float c)
        {
            if (h.orb == null)
            {
                h.orb = CombatUtil.Prim(PrimitiveType.Sphere, "SpellOrb", null, pos, Vector3.one * 0.05f, null, false);
                var l = h.orb.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 1.5f;
            }
            Color col = SpellColor(Current);
            h.orb.GetComponent<Renderer>().sharedMaterial = CombatMaterials.Transparent(new Color(col.r, col.g, col.b, 0.75f), col * 2f);
            var li = h.orb.GetComponent<Light>();
            li.color = col;
            li.intensity = 0.5f + 1.5f * c;
            h.orb.SetActive(true);
            h.orb.transform.position = pos;
            h.orb.transform.localScale = Vector3.one * Mathf.Lerp(0.035f, 0.09f, c);
        }

        static void HideOrb(CastHand h) { if (h.orb != null) h.orb.SetActive(false); }

        // ------------------------------------------------------------------ Casting

        public static void Cast(SpellId s, Vector3 origin, Vector3 dir, float charge, bool tap, XRNode node)
        {
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            float power = SkillSystem.SpellPowerMult;
            switch (s)
            {
                case SpellId.Fire:
                    if (!ManaPool.TrySpend(15f)) return;
                    FireBolt.Launch(origin, dir, (10f + 18f * charge) * power);
                    QhysicsSfx.PlayAt(SfxId.FireCast, origin, 0.8f);
                    break;
                case SpellId.Lightning:
                    if (!ManaPool.TrySpend(20f)) return;
                    CastLightning(origin, dir, (8f + 16f * charge) * power);
                    break;
                case SpellId.Force:
                    if (tap) { if (ManaPool.TrySpend(8f)) ForcePull(origin, dir, node); }
                    else if (ManaPool.TrySpend(18f)) ForcePush(origin, dir, charge * power);
                    break;
            }
            CombatHaptics.Pulse(node, 0.6f, 0.08f);
        }

        static bool IsPlayerOwned(Collider c)
        {
            if (CombatInputGate.IsPlayerCollider(c)) return true;
            var w = c.GetComponentInParent<PhysicsWeapon>();
            return w != null && w.HeldByPlayer;
        }

        static void CastLightning(Vector3 origin, Vector3 dir, float dmg)
        {
            Vector3 end = origin + dir * 25f;
            ICombatTarget first = null;
            var hits = UnityEngine.Physics.RaycastAll(origin, dir, 25f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (IsPlayerOwned(h.collider)) continue;
                end = h.point;
                first = h.collider.GetComponentInParent<ICombatTarget>();
                if (first == null)
                {
                    var legacy = h.collider.GetComponentInParent<IDamageable>();
                    if (legacy != null && legacy.IsAlive) legacy.ApplyDamage(dmg, h.point, h.normal);
                }
                break;
            }
            // Aim assist: nothing hit directly, snap to the enemy nearest the ray within 1.2 m.
            if (first == null)
            {
                float best = 1.2f;
                foreach (var e in CombatEnemy.All)
                {
                    if (e == null || e.IsDead) continue;
                    Vector3 c = e.transform.position + Vector3.up * 1.3f;
                    float along = Vector3.Dot(c - origin, dir);
                    if (along < 0f || along > 25f) continue;
                    float off = Vector3.Distance(origin + dir * along, c);
                    if (off < best) { best = off; first = e.Health; end = c; }
                }
            }
            LightningVfx.Bolt(origin, end);
            QhysicsSfx.PlayAt(SfxId.Lightning, end, 0.9f);
            if (first == null || !first.IsAlive) return;
            var hit = new DamageInfo { amount = dmg, type = DamageType.Lightning, point = end, impulse = dir * 3f, sourceTeam = CombatTeam.Player, relativeSpeed = 0f };
            first.TakeHit(hit);
            // Chain to up to 2 more enemies within 4 m.
            Vector3 from = end;
            var done = new HashSet<ICombatTarget> { first };
            for (int k = 0; k < 2; k++)
            {
                CombatEnemy next = null;
                float bd = 4f;
                foreach (var e in CombatEnemy.All)
                {
                    if (e == null || e.IsDead || done.Contains(e.Health)) continue;
                    float d = Vector3.Distance(from, e.transform.position + Vector3.up * 1.3f);
                    if (d < bd) { bd = d; next = e; }
                }
                if (next == null) break;
                Vector3 to = next.transform.position + Vector3.up * 1.3f;
                LightningVfx.Bolt(from, to);
                hit.amount *= 0.6f;
                hit.point = to;
                next.Health.TakeHit(hit);
                done.Add(next.Health);
                from = to;
            }
        }

        static void ForcePush(Vector3 origin, Vector3 dir, float power)
        {
            float range = 8f;
            QhysicsSfx.PlayAt(SfxId.ForcePush, origin, 0.9f);
            ForceWaveVfx.Spawn(origin, dir);
            var cols = UnityEngine.Physics.OverlapSphere(origin + dir * range * 0.5f, range * 0.6f, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<Rigidbody>();
            var hitTargets = new HashSet<ICombatTarget>();
            foreach (var c in cols)
            {
                if (IsPlayerOwned(c)) continue;
                Vector3 to = c.bounds.center - origin;
                if (to.magnitude > range || Vector3.Angle(dir, to) > 40f) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic && seen.Add(rb))
                    rb.AddForce((to.normalized + Vector3.up * 0.25f) * Mathf.Lerp(6f, 16f, power), ForceMode.VelocityChange);
                var t = c.GetComponentInParent<ICombatTarget>();
                if (t != null && t.IsAlive && t.Team != CombatTeam.Player && hitTargets.Add(t))
                    t.TakeHit(new DamageInfo { amount = 4f + 8f * power, type = DamageType.Force, point = c.bounds.center, impulse = to.normalized * 20f, sourceTeam = CombatTeam.Player });
            }
        }

        static void ForcePull(Vector3 origin, Vector3 dir, XRNode node)
        {
            PhysicsWeapon best = null;
            float bestScore = float.MaxValue;
            foreach (var w in PhysicsWeapon.All)
            {
                if (w == null || w.IsHeld || w.ownerTeam == CombatTeam.Enemy) continue;
                Vector3 to = w.transform.position - origin;
                float dist = to.magnitude;
                if (dist > 15f || dist < 0.3f) continue;
                float ang = Vector3.Angle(dir, to);
                if (ang > 25f) continue;
                float score = ang * 0.2f + dist;
                if (score < bestScore) { bestScore = score; best = w; }
            }
            if (best == null) { QhysicsSfx.Play2D(SfxId.Denied, 0.35f); return; }
            if (best.IsStuck) best.Unstick();
            // Ballistic velocity that lands the weapon at the hand in ~0.45 s.
            float t = 0.45f;
            Vector3 d = origin - best.Body.worldCenterOfMass;
            Vector3 v = d / t - 0.5f * UnityEngine.Physics.gravity * t;
            best.Body.linearVelocity = v;
            best.Body.angularVelocity = Vector3.zero;
            best.MarkPlayerHold();
            QhysicsSfx.PlayAt(SfxId.ForcePush, best.transform.position, 0.5f, 1.4f);
            LightningVfx.Bolt(origin, best.transform.position, new Color(0.7f, 0.55f, 1f, 0.8f), 0.2f);
        }

        public static void TryImbue(PhysicsWeapon w)
        {
            if (w == null || w.kind == WeaponKind.Shield) { QhysicsSfx.Play2D(SfxId.Denied, 0.4f); return; }
            if (Current == SpellId.Force) { QhysicsSfx.Play2D(SfxId.Denied, 0.4f); return; }
            if (!ManaPool.TrySpend(25f)) return;
            w.ApplyImbue(Current == SpellId.Fire ? Element.Fire : Element.Lightning, 20f * SkillSystem.ImbueDurationMult);
            QhysicsSfx.PlayAt(Current == SpellId.Fire ? SfxId.FireCast : SfxId.Lightning, w.TipWorld, 0.6f);
        }

        // ------------------------------------------------------------------ Status effects

        public static void ApplyBurn(ICombatTarget t, float seconds)
        {
            if (!(t is Component c) || !t.IsAlive) return;
            var health = c.GetComponentInParent<CombatHealth>();
            if (health == null) return;
            var b = health.GetComponent<Burning>();
            if (b == null) b = health.gameObject.AddComponent<Burning>();
            b.Refresh(seconds);
        }

        public static void ApplyShock(ICombatTarget t, Vector3 point)
        {
            CombatEnemy near = null;
            float bd = 3f;
            foreach (var e in CombatEnemy.All)
            {
                if (e == null || e.IsDead || ReferenceEquals(e.Health, t)) continue;
                float d = Vector3.Distance(point, e.transform.position + Vector3.up * 1.2f);
                if (d < bd) { bd = d; near = e; }
            }
            if (near == null) return;
            Vector3 to = near.transform.position + Vector3.up * 1.2f;
            LightningVfx.Bolt(point, to);
            near.Health.TakeHit(new DamageInfo { amount = 6f, type = DamageType.Lightning, point = to, sourceTeam = CombatTeam.Player });
        }
    }

    /// <summary>Fire damage over time with flame particles.</summary>
    public sealed class Burning : MonoBehaviour
    {
        float _until, _nextTick;
        ParticleSystem _ps;
        CombatHealth _h;

        public void Refresh(float seconds)
        {
            _until = Mathf.Max(_until, Time.time + seconds);
            enabled = true;
            if (_h == null) _h = GetComponent<CombatHealth>();
            if (_ps == null) _ps = ImbueVfx.MakeFlames(transform, Vector3.up * 1.1f, 0.35f, new Color(1f, 0.45f, 0.1f));
            _ps.Play();
        }

        void Update()
        {
            if (_h == null || !_h.IsAlive || Time.time > _until)
            {
                if (_ps != null) _ps.Stop();
                enabled = Time.time <= _until && _h != null && _h.IsAlive;
                return;
            }
            if (Time.time >= _nextTick)
            {
                _nextTick = Time.time + 0.5f;
                _h.TakeHit(new DamageInfo { amount = 2f, type = DamageType.Fire, point = transform.position + Vector3.up * 1.2f, sourceTeam = CombatTeam.Player });
            }
        }
    }

    /// <summary>Fire bolt projectile: explodes on contact (direct hit + splash, burn).</summary>
    public sealed class FireBolt : MonoBehaviour
    {
        float _dmg;
        float _born;
        bool _done;

        public static void Launch(Vector3 origin, Vector3 dir, float dmg)
        {
            Color col = new Color(1f, 0.45f, 0.1f);
            var go = CombatUtil.Prim(PrimitiveType.Sphere, "FireBolt", null, origin, Vector3.one * 0.14f, CombatMaterials.Get(col, 0f, 0.5f, col * 3f));
            go.GetComponent<Collider>().isTrigger = false;
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = dir * 20f;
            var l = go.AddComponent<Light>();
            l.color = col; l.range = 3f; l.intensity = 2f;
            ImbueVfx.MakeFlames(go.transform, Vector3.zero, 0.08f, col);
            var fb = go.AddComponent<FireBolt>();
            fb._dmg = dmg;
            fb._born = Time.time;
            // Never collide with the caster.
            var hb = PlayerHurtbox.Instance;
            var myCol = go.GetComponent<Collider>();
            var origin0 = GameObject.Find("XR Origin");
            if (origin0 != null)
                foreach (var pc in origin0.GetComponentsInChildren<Collider>(true)) UnityEngine.Physics.IgnoreCollision(myCol, pc, true);
            if (hb != null && hb.Collider != null) UnityEngine.Physics.IgnoreCollision(myCol, hb.Collider, true);
            foreach (var w in PhysicsWeapon.All)
                if (w != null && w.HeldByPlayer)
                    foreach (var wc in w.Colliders) if (wc != null) UnityEngine.Physics.IgnoreCollision(myCol, wc, true);
        }

        void Update()
        {
            if (Time.time - _born > 4f) Explode(transform.position, null);
        }

        void OnCollisionEnter(Collision c)
        {
            Explode(c.contactCount > 0 ? c.GetContact(0).point : transform.position, c.collider);
        }

        void Explode(Vector3 p, Collider direct)
        {
            if (_done) return;
            _done = true;
            QhysicsSfx.PlayAt(SfxId.FireImpact, p, 0.9f);
            SparkBurst.Spawn(p, new Color(1f, 0.5f, 0.1f), 30);
            ICombatTarget directT = direct != null ? direct.GetComponentInParent<ICombatTarget>() : null;
            if (directT != null && directT.IsAlive && directT.Team != CombatTeam.Player)
            {
                directT.TakeHit(new DamageInfo { amount = _dmg, type = DamageType.Fire, point = p, impulse = GetComponent<Rigidbody>().linearVelocity.normalized * 6f, sourceTeam = CombatTeam.Player });
                SpellSystem.ApplyBurn(directT, 3f);
            }
            else if (direct != null)
            {
                var legacy = direct.GetComponentInParent<IDamageable>();
                if (legacy != null && legacy.IsAlive) legacy.ApplyDamage(_dmg, p, Vector3.up);
            }
            var done = new HashSet<ICombatTarget>();
            if (directT != null) done.Add(directT);
            foreach (var c in UnityEngine.Physics.OverlapSphere(p, 1.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = c.GetComponentInParent<ICombatTarget>();
                if (t is BodyPart bp) t = bp.health;
                if (t == null || !t.IsAlive || t.Team == CombatTeam.Player || !done.Add(t)) continue;
                t.TakeHit(new DamageInfo { amount = _dmg * 0.5f, type = DamageType.Fire, point = c.bounds.center, impulse = (c.bounds.center - p).normalized * 4f, sourceTeam = CombatTeam.Player });
                if (c.attachedRigidbody != null) c.attachedRigidbody.AddExplosionForce(300f, p, 2f, 0.3f);
            }
            Destroy(gameObject);
        }
    }

    /// <summary>Jagged LineRenderer bolts that fade quickly.</summary>
    public sealed class LightningVfx : MonoBehaviour
    {
        float _born, _life;
        LineRenderer _lr;

        public static void Bolt(Vector3 a, Vector3 b) => Bolt(a, b, new Color(0.6f, 0.9f, 1f, 1f), 0.15f);

        public static void Bolt(Vector3 a, Vector3 b, Color col, float life)
        {
            var go = new GameObject("LightningBolt");
            var lr = go.AddComponent<LineRenderer>();
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / 0.4f), 3, 40);
            lr.positionCount = n + 1;
            Vector3 dir = (b - a);
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-3f) side = Vector3.right;
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                Vector3 p = a + dir * t;
                if (i > 0 && i < n)
                    p += (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * 0.12f;
                lr.SetPosition(i, p);
            }
            lr.widthMultiplier = 0.03f;
            lr.sharedMaterial = CombatMaterials.UnlitLine(col);
            lr.startColor = lr.endColor = col;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<Light>();
            go.transform.position = b;
            l.color = col; l.range = 4f; l.intensity = 3f;
            var v = go.AddComponent<LightningVfx>();
            v._born = Time.time; v._life = life; v._lr = lr;
        }

        void Update()
        {
            float a = (Time.time - _born) / _life;
            if (a >= 1f) { Destroy(gameObject); return; }
            _lr.widthMultiplier = 0.03f * (1f - a);
        }
    }

    /// <summary>Expanding translucent ring for Force push.</summary>
    public sealed class ForceWaveVfx : MonoBehaviour
    {
        float _born;
        Vector3 _dir;

        public static void Spawn(Vector3 origin, Vector3 dir)
        {
            var go = CombatUtil.Prim(PrimitiveType.Cylinder, "ForceWave", null, origin, new Vector3(0.3f, 0.01f, 0.3f),
                CombatMaterials.Transparent(new Color(0.7f, 0.55f, 1f, 0.35f), new Color(0.5f, 0.35f, 1f)), false);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            var v = go.AddComponent<ForceWaveVfx>();
            v._born = Time.time;
            v._dir = dir;
        }

        void Update()
        {
            float a = (Time.time - _born) / 0.35f;
            if (a >= 1f) { Destroy(gameObject); return; }
            transform.position += _dir * (18f * Time.deltaTime);
            float s = Mathf.Lerp(0.3f, 4f, a);
            transform.localScale = new Vector3(s, 0.01f, s);
        }
    }

    /// <summary>Element VFX attached to imbued weapons (and flames helper).</summary>
    public static class ImbueVfx
    {
        public static void Attach(PhysicsWeapon w, Element e)
        {
            var old = w.transform.Find("ImbueVfx");
            if (old != null) Object.Destroy(old.gameObject);
            if (e == Element.None) return;
            Color col = e == Element.Fire ? new Color(1f, 0.45f, 0.1f) : new Color(0.5f, 0.85f, 1f);
            var ps = MakeFlames(w.transform, new Vector3(0f, w.tipY * 0.6f, 0f), 0.02f, col);
            ps.gameObject.name = "ImbueVfx";
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.03f, w.tipY * 0.7f, 0.01f);
            var l = ps.gameObject.AddComponent<Light>();
            l.color = col; l.range = 1.5f; l.intensity = 1.2f;
        }

        public static ParticleSystem MakeFlames(Transform parent, Vector3 localPos, float radius, Color col)
        {
            var go = new GameObject("Flames");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(col, new Color(1f, 0.9f, 0.5f));
            main.gravityModifier = -0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var em = ps.emission;
            em.rateOverTime = 40f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = CombatMaterials.UnlitLine(col);
            ps.Play();
            return ps;
        }
    }
}
