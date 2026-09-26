using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using RealityEngine.Audio;
using RealityEngine.Player;

namespace RealityEngine.Combat
{
    public enum WeaponKind { Dagger = 0, Sword = 1, Spear = 2, Mace = 3, Shield = 4, EnemySword = 5 }

    public enum WeaponPartType { Handle = 0, Edge = 1, Tip = 2, Head = 3, Guard = 4, ShieldFace = 5 }

    /// <summary>Tags a weapon collider with its role (edge slashes, tip pierces, head/handle bludgeons).</summary>
    public sealed class WeaponPart : MonoBehaviour
    {
        public PhysicsWeapon weapon;
        public WeaponPartType part;
    }

    /// <summary>
    /// A fully dynamic rigidbody weapon. Hands drive it with forces (see <see cref="PhysicsHands"/>), so it
    /// collides with the world and cannot pass through walls. Damage comes from contact point velocity,
    /// mass and the part that hit. Tips can embed (ConfigurableJoint) and are pulled out by force.
    /// Built along local +Y: handle at the bottom, tip at the top.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsWeapon : MonoBehaviour
    {
        public static readonly List<PhysicsWeapon> All = new List<PhysicsWeapon>();

        public WeaponKind kind;
        public string displayName = "Weapon";
        public bool twoHanded;
        public bool canStab;
        /// <summary>Grip segment along local Y (hands can grab anywhere between these).</summary>
        public float gripMinY;
        public float gripMaxY = 0.15f;
        public float tipY = 1f;
        public float damageScale = 1f;
        public float knockbackScale = 1f;
        public CombatTeam ownerTeam = CombatTeam.Neutral;
        /// <summary>Set by the enemy that owns this weapon; used for block/parry.</summary>
        public CombatEnemy enemyOwner;

        public Rigidbody Body { get; private set; }
        public int HeldCount { get; set; }
        public bool IsHeld => HeldCount > 0;
        public XRNode PrimaryNode { get; set; } = XRNode.RightHand;
        public bool HeldByPlayer { get; set; }

        public Element Imbue { get; private set; }
        float _imbueUntil;

        public bool IsStuck => _joint != null;
        public Rigidbody StuckBody => _stuckBody;
        ConfigurableJoint _joint;
        Collider _stuckIn;
        Rigidbody _stuckBody;
        Vector3 _stuckLocalPoint;
        float _stuckTime;
        readonly List<Collider> _ignored = new List<Collider>();
        float _lastPlayerHoldTime = -99f;

        readonly Dictionary<object, float> _cooldown = new Dictionary<object, float>();
        readonly List<Collider> _colliders = new List<Collider>();
        TrailRenderer _trail;
        float _lastClang;

        public IReadOnlyList<Collider> Colliders => _colliders;
        public Vector3 TipWorld => transform.TransformPoint(0f, tipY, 0f);
        public Vector3 Axis => transform.up;

        /// <summary>Team credited with damage: held by player (or thrown by player in the last 3 s) = Player.</summary>
        public CombatTeam ActiveTeam
        {
            get
            {
                if (ownerTeam == CombatTeam.Enemy)
                    return CombatTeam.Enemy;
                if (HeldByPlayer || Time.time - _lastPlayerHoldTime < 3f)
                    return CombatTeam.Player;
                return CombatTeam.Neutral;
            }
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 40f;
            RefreshColliders();
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void RefreshColliders()
        {
            _colliders.Clear();
            GetComponentsInChildren(true, _colliders);
        }

        public void MarkPlayerHold()
        {
            _lastPlayerHoldTime = Time.time;
        }

        public void SetTrail(TrailRenderer t) { _trail = t; }

        public void ApplyImbue(Element e, float seconds)
        {
            Imbue = e;
            _imbueUntil = Time.time + seconds;
            ImbueVfx.Attach(this, e);
        }

        void Update()
        {
            if (Imbue != Element.None && Time.time > _imbueUntil)
            {
                Imbue = Element.None;
                ImbueVfx.Attach(this, Element.None);
            }
            if (HeldByPlayer)
                _lastPlayerHoldTime = Time.time;
            if (_trail != null)
                _trail.emitting = (IsHeld || ActiveTeam != CombatTeam.Neutral) && Body.linearVelocity.sqrMagnitude > 9f;
        }

        void FixedUpdate()
        {
            if (_joint == null)
                return;
            // Target died, got destroyed, or blade was levered out: release.
            bool targetGone = _stuckIn == null || !_stuckIn.enabled || !_stuckIn.gameObject.activeInHierarchy;
            var tgt = _stuckIn != null ? _stuckIn.GetComponentInParent<ICombatTarget>() : null;
            if (targetGone || (tgt != null && !tgt.IsAlive && Time.time - _stuckTime > 2f))
            {
                Unstick();
                return;
            }
            // Pull-out along blade axis beyond the allowance.
            Vector3 anchorWorld = _stuckBody != null ? _stuckBody.transform.TransformPoint(_stuckLocalPoint) : _stuckLocalPoint;
            float outward = Vector3.Dot(TipWorld - anchorWorld, Axis);
            if (outward > 0.09f && Time.time - _stuckTime > 0.15f)
                Unstick();
        }

        void OnJointBreak(float force)
        {
            // Unity destroys the joint; clean up our state next frame.
            _joint = null;
            RestoreIgnored();
            QhysicsSfx.PlayAt(SfxId.Unstick, TipWorld, 0.7f);
        }

        void OnCollisionEnter(Collision c)
        {
            HandleContact(c);
        }

        void HandleContact(Collision c)
        {
            if (c.contactCount == 0)
                return;
            ContactPoint cp = c.GetContact(0);
            Collider mine = cp.thisCollider;
            Collider other = cp.otherCollider;
            if (other == null)
                return;

            WeaponPartType part = WeaponPartType.Handle;
            var wp = mine != null ? mine.GetComponent<WeaponPart>() : null;
            if (wp != null)
                part = wp.part;

            Vector3 p = cp.point;
            Vector3 vMine = Body.GetPointVelocity(p);
            Vector3 vOther = c.rigidbody != null ? c.rigidbody.GetPointVelocity(p) : Vector3.zero;
            Vector3 rel = vMine - vOther;
            float speed = Mathf.Min(rel.magnitude, 16f);

            // Weapon vs weapon: block / parry / clang.
            var otherWeapon = other.GetComponentInParent<PhysicsWeapon>();
            if (otherWeapon != null && otherWeapon != this)
            {
                HandleWeaponClash(otherWeapon, p, speed);
                return;
            }

            CombatTeam team = ActiveTeam;
            var target = other.GetComponentInParent<ICombatTarget>();
            if (target == null && team == CombatTeam.Player)
            {
                // Legacy targets (Training Drone) only implement IDamageable.
                var legacy = other.GetComponentInParent<IDamageable>();
                if (legacy != null && legacy.IsAlive && speed >= CombatTuning.MinSwingSpeed
                    && !(_cooldown.TryGetValue(legacy, out float lu) && Time.time < lu))
                {
                    _cooldown[legacy] = Time.time + CombatTuning.PerTargetCooldown;
                    float ld = CombatTuning.DamagePerMs * (speed - CombatTuning.MinSwingSpeed) * Mathf.Sqrt(Body.mass) * damageScale;
                    legacy.ApplyDamage(Mathf.Clamp(ld, 1f, 120f), p, cp.normal);
                    DamageNumbers.Spawn(p, ld, DamageType.Blunt);
                    QhysicsSfx.PlayAt(SfxId.HitBlunt, p, 0.7f);
                    if (HeldByPlayer) CombatHaptics.Pulse(PrimaryNode, 0.6f, 0.06f);
                    return;
                }
            }
            if (target == null || !target.IsAlive)
            {
                // World impact: small feedback, allow sticking daggers/spears in walls.
                if (speed > 3f && Time.time - _lastClang > 0.12f)
                {
                    _lastClang = Time.time;
                    QhysicsSfx.PlayAt(part == WeaponPartType.Head ? SfxId.HitBlunt : SfxId.Block, p, Mathf.Clamp01(speed / 10f) * 0.6f);
                    if (HeldByPlayer)
                        CombatHaptics.Pulse(PrimaryNode, Mathf.Clamp01(speed / 8f) * 0.5f, 0.05f);
                }
                if (team == CombatTeam.Player && canStab && part == WeaponPartType.Tip && other.attachedRigidbody == null && !other.isTrigger)
                    TryStick(other, c.rigidbody, p, rel, 1.4f);
                return;
            }
            if (team == CombatTeam.Neutral || target.Team == team)
                return;
            if (team == CombatTeam.Enemy)
                return; // enemies damage the player via PlayerHurtbox, not ICombatTarget

            if (speed < CombatTuning.MinSwingSpeed * SkillSystem.SwingThresholdMult)
                return;
            if (_cooldown.TryGetValue(target, out float until) && Time.time < until)
                return;
            _cooldown[target] = Time.time + CombatTuning.PerTargetCooldown;

            DamageType type;
            float partMult;
            Vector3 dir = rel / Mathf.Max(0.001f, rel.magnitude);
            float alongBlade = Mathf.Abs(Vector3.Dot(dir, Axis));
            switch (part)
            {
                case WeaponPartType.Edge:
                    type = DamageType.Slash;
                    partMult = Mathf.Lerp(1.15f, 0.55f, alongBlade); // cutting sideways works, dragging along the edge doesn't
                    break;
                case WeaponPartType.Tip:
                    type = alongBlade > 0.7f ? DamageType.Pierce : DamageType.Slash;
                    partMult = type == DamageType.Pierce ? 1.25f : 0.9f;
                    break;
                case WeaponPartType.Head:
                    type = DamageType.Blunt;
                    partMult = 1.2f;
                    break;
                case WeaponPartType.ShieldFace:
                    type = DamageType.Blunt;
                    partMult = 0.45f;
                    break;
                default:
                    type = DamageType.Blunt;
                    partMult = 0.5f;
                    break;
            }

            float mass = Body.mass;
            float over = speed - CombatTuning.MinSwingSpeed * SkillSystem.SwingThresholdMult;
            float dmg = CombatTuning.DamagePerMs * over * Mathf.Sqrt(mass) * partMult * damageScale * SkillSystem.DamageMult(type);
            if (Imbue == Element.Fire) dmg *= 1.25f;
            if (Imbue == Element.Lightning) dmg *= 1.15f;
            dmg = Mathf.Clamp(dmg, 1f, 120f);

            var hit = new DamageInfo
            {
                amount = dmg,
                type = type,
                point = p,
                normal = cp.normal,
                impulse = dir * (mass * speed * 0.6f * knockbackScale),
                sourceTeam = team,
                source = gameObject,
                relativeSpeed = speed
            };
            target.TakeHit(hit);

            if (Imbue == Element.Fire)
                SpellSystem.ApplyBurn(target, 3f);
            else if (Imbue == Element.Lightning)
                SpellSystem.ApplyShock(target, p);

            SfxId sfx = type == DamageType.Slash ? SfxId.HitSlash : type == DamageType.Pierce ? SfxId.HitPierce : SfxId.HitBlunt;
            QhysicsSfx.PlayAt(sfx, p, Mathf.Clamp01(0.35f + speed / 12f));
            if (HeldByPlayer)
            {
                float amp = Mathf.Clamp01(0.3f + speed / 10f);
                CombatHaptics.Pulse(PrimaryNode, amp, 0.08f);
                if (twoHanded && HeldCount > 1)
                    CombatHaptics.Pulse(PrimaryNode == XRNode.RightHand ? XRNode.LeftHand : XRNode.RightHand, amp * 0.7f, 0.08f);
            }

            if (canStab && part == WeaponPartType.Tip && type == DamageType.Pierce)
                TryStick(other, c.rigidbody, p, rel, 1f);
        }

        void HandleWeaponClash(PhysicsWeapon other, Vector3 p, float speed)
        {
            if (Time.time - _lastClang < 0.1f)
                return;
            _lastClang = Time.time;

            // Player weapon meets an enemy weapon mid-swing: block (slow) or parry (fast).
            if (ActiveTeam == CombatTeam.Player && other.enemyOwner != null && other.enemyOwner.IsSwinging)
            {
                bool parry = speed >= CombatTuning.ParryMinSpeed * SkillSystem.ParryThresholdMult;
                other.enemyOwner.OnWeaponBlocked(parry, p);
                if (parry)
                {
                    QhysicsSfx.PlayAt(SfxId.Parry, p, 1f);
                    CombatEvents.RaiseParried(p);
                    CombatHaptics.Pulse(PrimaryNode, 0.9f, 0.12f);
                    SparkBurst.Spawn(p, new Color(1f, 0.85f, 0.4f), 18);
                }
                else
                {
                    QhysicsSfx.PlayAt(SfxId.Block, p, 0.8f);
                    CombatHaptics.Pulse(PrimaryNode, 0.5f, 0.08f);
                    SparkBurst.Spawn(p, new Color(1f, 0.9f, 0.7f), 8);
                }
                return;
            }
            if (speed > 2.5f)
            {
                QhysicsSfx.PlayAt(SfxId.Block, p, Mathf.Clamp01(speed / 10f));
                if (HeldByPlayer)
                    CombatHaptics.Pulse(PrimaryNode, Mathf.Clamp01(speed / 10f), 0.06f);
                if (speed > 5f)
                    SparkBurst.Spawn(p, new Color(1f, 0.9f, 0.7f), 6);
            }
        }

        void TryStick(Collider other, Rigidbody otherBody, Vector3 point, Vector3 relVel, float speedScale)
        {
            if (_joint != null || !canStab)
                return;
            float along = Vector3.Dot(relVel, Axis);
            if (along < CombatTuning.StabMinSpeed * speedScale * SkillSystem.StabThresholdMult)
                return;
            float angle = Vector3.Angle(relVel, Axis);
            if (angle > CombatTuning.StabMaxAngle)
                return;

            Rigidbody targetBody = otherBody != null ? otherBody : other.attachedRigidbody;
            // Push the tip in a little.
            float depth = kind == WeaponKind.Spear ? 0.16f : 0.1f;
            transform.position += Axis * depth * 0.5f;

            _joint = gameObject.AddComponent<ConfigurableJoint>();
            _joint.autoConfigureConnectedAnchor = true;
            _joint.anchor = new Vector3(0f, tipY, 0f);
            _joint.connectedBody = targetBody;
            _joint.axis = Vector3.up;          // joint X = blade axis
            _joint.secondaryAxis = Vector3.right;
            _joint.xMotion = ConfigurableJointMotion.Limited;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;
            _joint.angularXMotion = ConfigurableJointMotion.Locked;
            _joint.angularYMotion = ConfigurableJointMotion.Limited;
            _joint.angularZMotion = ConfigurableJointMotion.Limited;
            _joint.angularYLimit = new SoftJointLimit { limit = 6f };
            _joint.angularZLimit = new SoftJointLimit { limit = 6f };
            _joint.linearLimit = new SoftJointLimit { limit = depth };
            _joint.xDrive = new JointDrive { positionSpring = 0f, positionDamper = 120f * Body.mass, maximumForce = 1e6f };
            _joint.breakForce = 900f * SkillSystem.PullOutForceMult;
            _joint.breakTorque = 400f;
            _joint.enableCollision = false;

            _stuckIn = other;
            _stuckBody = targetBody;
            Vector3 tip = TipWorld;
            _stuckLocalPoint = targetBody != null ? targetBody.transform.InverseTransformPoint(tip) : tip;
            _stuckTime = Time.time;

            // Blade passes through the victim's collider while embedded.
            _ignored.Clear();
            var victimCols = targetBody != null ? targetBody.GetComponentsInChildren<Collider>() : new[] { other };
            foreach (var vc in victimCols)
            {
                if (vc == null || vc.isTrigger) continue;
                foreach (var mc in _colliders)
                    if (mc != null) UnityEngine.Physics.IgnoreCollision(mc, vc, true);
                _ignored.Add(vc);
            }
            QhysicsSfx.PlayAt(SfxId.Stick, point, 0.9f);
            if (HeldByPlayer)
                CombatHaptics.Pulse(PrimaryNode, 0.7f, 0.1f);
        }

        public void Unstick()
        {
            if (_joint != null)
            {
                Destroy(_joint);
                _joint = null;
                QhysicsSfx.PlayAt(SfxId.Unstick, TipWorld, 0.7f);
            }
            RestoreIgnored();
        }

        void RestoreIgnored()
        {
            _stuckIn = null;
            _stuckBody = null;
            if (_ignored.Count == 0)
                return;
            var cols = new List<Collider>(_ignored);
            _ignored.Clear();
            StartCoroutine(RestoreLater(cols));
        }

        System.Collections.IEnumerator RestoreLater(List<Collider> cols)
        {
            yield return new WaitForSeconds(0.35f);
            foreach (var vc in cols)
            {
                if (vc == null) continue;
                foreach (var mc in _colliders)
                    if (mc != null) UnityEngine.Physics.IgnoreCollision(mc, vc, false);
            }
        }

        /// <summary>Nearest point on this weapon's grip segment to a world position (and its local Y).</summary>
        public Vector3 ClosestGripPoint(Vector3 world, out float localY)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            localY = Mathf.Clamp(local.y, gripMinY, gripMaxY);
            return transform.TransformPoint(0f, localY, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            // Enemy weapons hurt the player through the player's hurtbox trigger.
            if (ownerTeam != CombatTeam.Enemy || enemyOwner == null || !enemyOwner.IsSwinging)
                return;
            var hurt = other.GetComponent<PlayerHurtbox>();
            if (hurt == null)
                return;
            enemyOwner.OnSwingConnected(hurt, other.ClosestPoint(TipWorld));
        }
    }

    /// <summary>Small spark particle burst (pooled one-shot ParticleSystems).</summary>
    public static class SparkBurst
    {
        static ParticleSystem _ps;

        public static void Spawn(Vector3 pos, Color color, int count)
        {
            if (_ps == null)
            {
                var go = new GameObject("CombatSparks");
                Object.DontDestroyOnLoad(go);
                _ps = go.AddComponent<ParticleSystem>();
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
                main.gravityModifier = 1f;
                main.maxParticles = 400;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = _ps.emission;
                em.rateOverTime = 0f;
                var shape = _ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.02f;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = CombatMaterials.UnlitLine(Color.white);
            }
            var ep = new ParticleSystem.EmitParams { position = pos, startColor = color, applyShapeToPosition = true };
            _ps.Emit(ep, count);
        }
    }
}
