using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Primitive humanoid swordsman. Approaches, winds up, swings a physics sword (force-driven like the
    /// player's), blocks fast incoming blades, staggers when hit hard or parried, and turns into a jointed
    /// ragdoll on death. Damage to the player goes through <see cref="PlayerHurtbox"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEnemy : MonoBehaviour
    {
        public static readonly List<CombatEnemy> All = new List<CombatEnemy>();

        enum State { Approach, Guard, Windup, Swing, Recover, Block, Stagger, Dead }

        public float moveSpeed = 1.8f;
        public float attackRange = 1.75f;
        public float swingDamage = 12f;

        public CombatHealth Health { get; private set; }
        public bool IsDead => _state == State.Dead;
        public bool IsSwinging => _state == State.Swing;
        public float DeathTime { get; private set; } = -1f;

        Rigidbody _rb;
        PhysicsWeapon _sword;
        Transform _rightArm;
        Transform _hpBar, _hpFill;
        readonly List<Transform> _parts = new List<Transform>();
        State _state = State.Approach;
        float _stateT, _stateDur;
        float _nextAttack;
        float _nextBlockCheck;
        bool _hitThisSwing;

        static readonly Vector3 ShoulderR = new Vector3(0.24f, 1.5f, 0f);

        public static CombatEnemy Spawn(Vector3 feet, Quaternion facing, float hpScale = 1f)
        {
            var go = new GameObject("CombatEnemy_Raider");
            go.transform.SetPositionAndRotation(feet, facing);
            var e = go.AddComponent<CombatEnemy>();
            e.Build(hpScale);
            return e;
        }

        void Build(float hpScale)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.mass = 70f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearDamping = 0.5f;

            Health = gameObject.AddComponent<CombatHealth>();
            Health.ResetHp(60f * hpScale);
            Health.team = CombatTeam.Enemy;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;

            Material armor = CombatMaterials.Get(new Color(0.2f, 0.21f, 0.24f), 0.6f, 0.45f);
            Material cloth = CombatMaterials.Get(new Color(0.35f, 0.08f, 0.07f), 0f, 0.3f);
            Material skin = CombatMaterials.Get(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.4f);
            Material visor = CombatMaterials.Get(new Color(0.3f, 0.02f, 0.02f), 0f, 0.8f, new Color(1f, 0.1f, 0.05f) * 2f);

            AddPart(PrimitiveType.Cube, "Pelvis", new Vector3(0f, 0.95f, 0f), new Vector3(0.34f, 0.18f, 0.2f), cloth, 0.9f);
            AddPart(PrimitiveType.Cube, "Torso", new Vector3(0f, 1.3f, 0f), new Vector3(0.4f, 0.5f, 0.24f), armor, 1f);
            AddPart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.72f, 0f), Vector3.one * 0.24f, skin, 2f);
            CombatUtil.Prim(PrimitiveType.Cube, "Visor", _parts[2], new Vector3(0f, 0.05f, 0.42f), new Vector3(0.8f, 0.14f, 0.2f), visor, false);
            AddPart(PrimitiveType.Capsule, "LegL", new Vector3(-0.11f, 0.45f, 0f), new Vector3(0.15f, 0.44f, 0.15f), cloth, 0.6f);
            AddPart(PrimitiveType.Capsule, "LegR", new Vector3(0.11f, 0.45f, 0f), new Vector3(0.15f, 0.44f, 0.15f), cloth, 0.6f);
            AddPart(PrimitiveType.Capsule, "ArmL", new Vector3(-0.29f, 1.24f, 0.05f), new Vector3(0.11f, 0.3f, 0.11f), armor, 0.6f);
            // Right arm is visual-only (follows the sword hand) so it never fights its own weapon.
            _rightArm = CombatUtil.Prim(PrimitiveType.Capsule, "ArmR", transform, ShoulderR, new Vector3(0.11f, 0.3f, 0.11f), armor, false).transform;

            // HP bar
            _hpBar = new GameObject("HpBar").transform;
            _hpBar.SetParent(transform, false);
            _hpBar.localPosition = new Vector3(0f, 2.02f, 0f);
            CombatUtil.Prim(PrimitiveType.Cube, "Bg", _hpBar, Vector3.zero, new Vector3(0.5f, 0.05f, 0.01f),
                CombatMaterials.Get(new Color(0.04f, 0.06f, 0.08f)), false);
            _hpFill = CombatUtil.Prim(PrimitiveType.Cube, "Fill", _hpBar, new Vector3(0f, 0f, -0.006f), new Vector3(0.48f, 0.035f, 0.01f),
                CombatMaterials.Get(new Color(0.9f, 0.15f, 0.1f), 0f, 0.3f, new Color(1f, 0.1f, 0.05f))).transform;
            _hpBar.gameObject.SetActive(false);

            Health.RefreshRenderers();

            // Sword
            Vector3 hand = transform.TransformPoint(0.25f, 1.25f, 0.4f);
            _sword = WeaponFactory.Spawn(WeaponKind.EnemySword, hand, transform.rotation);
            _sword.enemyOwner = this;
            foreach (var p in _parts)
            {
                var pc = p.GetComponent<Collider>();
                if (pc == null) continue;
                foreach (var sc in _sword.Colliders)
                    if (sc != null) UnityEngine.Physics.IgnoreCollision(sc, pc, true);
            }
            _nextAttack = Time.time + 1.5f;
            All.Add(this);
        }

        void AddPart(PrimitiveType t, string name, Vector3 lp, Vector3 ls, Material m, float mult)
        {
            var g = CombatUtil.Prim(t, name, transform, lp, ls, m, true);
            var bp = g.AddComponent<BodyPart>();
            bp.health = Health;
            bp.multiplier = mult;
            bp.zone = name;
            _parts.Add(g.transform);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (_sword != null && _sword.enemyOwner == this)
                Destroy(_sword.gameObject);
        }

        // ------------------------------------------------------------------ AI

        Vector3 PlayerFeet()
        {
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return transform.position + transform.forward * 3f;
            Vector3 p = cam.transform.position;
            p.y = transform.position.y;
            return p;
        }

        void SetState(State s, float dur)
        {
            _state = s;
            _stateT = 0f;
            _stateDur = dur;
            if (s == State.Swing)
            {
                _hitThisSwing = false;
                QhysicsSfx.PlayAt(SfxId.Whoosh, _sword != null ? _sword.transform.position : transform.position, 0.6f, 0.85f);
            }
        }

        void Update()
        {
            if (_state == State.Dead)
                return;
            _stateT += Time.deltaTime;
            float dist = Vector3.Distance(PlayerFeet(), transform.position);
            bool paused = CombatInputGate.Blocked;

            switch (_state)
            {
                case State.Approach:
                    if (dist <= attackRange) SetState(State.Guard, 0f);
                    break;
                case State.Guard:
                    if (dist > attackRange + 0.6f) { SetState(State.Approach, 0f); break; }
                    if (!paused && Time.time >= _nextAttack) SetState(State.Windup, 0.5f);
                    else TryBlock();
                    break;
                case State.Windup:
                    if (_stateT >= _stateDur) SetState(State.Swing, 0.32f);
                    break;
                case State.Swing:
                    if (_stateT >= _stateDur) SetState(State.Recover, 0.6f);
                    break;
                case State.Recover:
                case State.Block:
                case State.Stagger:
                    if (_stateT >= _stateDur)
                    {
                        if (_state != State.Block) _nextAttack = Time.time + Random.Range(1.1f, 2.2f);
                        SetState(State.Guard, 0f);
                    }
                    break;
            }

            if (_hpBar != null)
            {
                bool show = Health.Hp < Health.maxHp;
                _hpBar.gameObject.SetActive(show);
                if (show)
                {
                    float f = Health.Hp01;
                    _hpFill.localScale = new Vector3(0.48f * f, 0.035f, 0.01f);
                    _hpFill.localPosition = new Vector3(-0.24f * (1f - f), 0f, -0.006f);
                    var cam = CombatInputGate.ViewCamera();
                    if (cam != null)
                        _hpBar.rotation = Quaternion.LookRotation(_hpBar.position - cam.transform.position);
                }
            }
        }

        void TryBlock()
        {
            if (Time.time < _nextBlockCheck) return;
            _nextBlockCheck = Time.time + 0.2f;
            Vector3 head = transform.TransformPoint(0f, 1.5f, 0f);
            foreach (var w in PhysicsWeapon.All)
            {
                if (w == null || !w.HeldByPlayer) continue;
                if (w.Body.linearVelocity.magnitude < 2.5f && w.Body.angularVelocity.magnitude < 6f) continue;
                if (Vector3.Distance(w.TipWorld, head) > 1.3f) continue;
                if (Random.value < 0.5f * SkillSystem.EnemyBlockChanceMult)
                    SetState(State.Block, 0.55f);
                return;
            }
        }

        void FixedUpdate()
        {
            if (_state == State.Dead || _rb == null)
                return;
            Vector3 to = PlayerFeet() - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

            // Face the player.
            if (_state != State.Stagger)
            {
                Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
                _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, face, 240f * Time.fixedDeltaTime));
            }

            // Move (keep vertical velocity from gravity).
            Vector3 v = _rb.linearVelocity;
            Vector3 want = Vector3.zero;
            if (_state == State.Approach) want = dir * moveSpeed;
            else if ((_state == State.Guard || _state == State.Recover) && dist < 1.0f) want = -dir * 1.2f;
            else if (_state == State.Windup && dist > 1.3f) want = dir * 0.8f;
            Vector3 horiz = new Vector3(v.x, 0f, v.z);
            if (_state != State.Stagger)
            {
                Vector3 dv = want - horiz;
                _rb.AddForce(Vector3.ClampMagnitude(dv * 12f, 25f), ForceMode.Acceleration);
            }

            DriveSword();
        }

        void DriveSword()
        {
            if (_sword == null || _sword.enemyOwner != this) return;
            GetHandTarget(out Vector3 lp, out Quaternion lr);
            Vector3 hand = transform.TransformPoint(lp);
            Quaternion rot = transform.rotation * lr;
            Vector3 targetPos = hand - rot * new Vector3(0f, 0.1f, 0f);
            float strength = _state == State.Stagger ? 0.35f : 1f;
            PhysicsHands.Drive(_sword.Body, targetPos, rot, _rb.linearVelocity, 520f * strength, 50f * strength, 0.04f, 0.05f);
        }

        void GetHandTarget(out Vector3 lp, out Quaternion lr)
        {
            Vector3 guardP = new Vector3(0.25f, 1.25f, 0.42f);
            Quaternion guardR = Quaternion.Euler(30f, 0f, 0f);
            Vector3 windP = new Vector3(0.38f, 1.72f, -0.02f);
            Quaternion windR = Quaternion.Euler(-40f, 0f, -40f);
            Vector3 endP = new Vector3(-0.32f, 0.95f, 0.6f);
            Quaternion endR = Quaternion.Euler(100f, 0f, 60f);
            float k = _stateDur > 0f ? Mathf.Clamp01(_stateT / _stateDur) : 1f;
            float s = k * k * (3f - 2f * k);
            switch (_state)
            {
                case State.Windup: lp = Vector3.Lerp(guardP, windP, s); lr = Quaternion.Slerp(guardR, windR, s); return;
                case State.Swing: lp = Vector3.Lerp(windP, endP, k); lr = Quaternion.Slerp(windR, endR, k); return;
                case State.Recover: lp = Vector3.Lerp(endP, guardP, s); lr = Quaternion.Slerp(endR, guardR, s); return;
                case State.Block: lp = new Vector3(0f, 1.5f, 0.45f); lr = Quaternion.Euler(0f, 0f, 90f); return;
                case State.Stagger: lp = new Vector3(0.35f, 0.9f, 0.15f); lr = Quaternion.Euler(150f, 0f, 0f); return;
                default: lp = guardP; lr = guardR; return;
            }
        }

        void LateUpdate()
        {
            if (_rightArm == null || _sword == null || _state == State.Dead) return;
            Vector3 sh = transform.TransformPoint(ShoulderR);
            Vector3 hand = _sword.transform.TransformPoint(0f, 0.1f, 0f);
            Vector3 d = hand - sh;
            float len = Mathf.Clamp(d.magnitude, 0.2f, 0.8f);
            _rightArm.position = sh + d.normalized * (len * 0.5f);
            _rightArm.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.down);
            _rightArm.localScale = new Vector3(0.11f, len * 0.5f, 0.11f);
        }

        // ------------------------------------------------------------------ Events

        public void OnSwingConnected(PlayerHurtbox hurt, Vector3 point)
        {
            if (_hitThisSwing || _state != State.Swing) return;
            _hitThisSwing = true;
            hurt.Hurt(swingDamage, point, "Raider");
        }

        public void OnWeaponBlocked(bool parry, Vector3 point)
        {
            if (_state == State.Dead) return;
            _hitThisSwing = true;
            if (parry)
            {
                SetState(State.Stagger, 1.3f);
                _rb.AddForce(-transform.forward * 3f, ForceMode.VelocityChange);
            }
            else
                SetState(State.Recover, 0.5f);
        }

        void OnDamaged(DamageInfo hit)
        {
            if (_state == State.Dead) return;
            if (hit.amount >= 8f && _state != State.Stagger)
                SetState(State.Stagger, Mathf.Lerp(0.3f, 0.7f, Mathf.InverseLerp(8f, 40f, hit.amount)));
        }

        void OnDied(DamageInfo hit)
        {
            if (_state == State.Dead) return;
            _state = State.Dead;
            DeathTime = Time.time;
            QhysicsSfx.PlayAt(SfxId.EnemyDeath, transform.position + Vector3.up, 0.9f);
            if (_hpBar != null) _hpBar.gameObject.SetActive(false);

            // Free anything embedded in us before the root body goes away.
            foreach (var w in PhysicsWeapon.All.ToArray())
                if (w != null && w.IsStuck && w.StuckBody == _rb) w.Unstick();

            // Sword becomes a loose weapon the player can pick up.
            if (_sword != null)
            {
                _sword.enemyOwner = null;
                _sword.ownerTeam = CombatTeam.Neutral;
                _sword.gameObject.name = "Weapon_RaiderSword_Loot";
                _sword = null;
            }
            if (_rightArm != null)
            {
                var col = _rightArm.gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.5f;
                _parts.Add(_rightArm);
            }
            BuildRagdoll(hit);
        }

        void BuildRagdoll(DamageInfo hit)
        {
            Vector3 vel = _rb.linearVelocity;
            Transform torso = _parts.Find(p => p.name == "Torso");
            Transform pelvis = _parts.Find(p => p.name == "Pelvis");
            var bodies = new Dictionary<Transform, Rigidbody>();
            foreach (var p in _parts)
            {
                if (p == null) continue;
                p.SetParent(null, true);
                var rb = p.gameObject.AddComponent<Rigidbody>();
                float vol = p.lossyScale.x * p.lossyScale.y * p.lossyScale.z;
                rb.mass = Mathf.Clamp(vol * 900f, 2f, 30f);
                rb.linearVelocity = vel;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                bodies[p] = rb;
            }
            void Join(string child, Transform parent, Vector3 worldPt)
            {
                Transform c = _parts.Find(p => p != null && p.name == child);
                if (c == null || parent == null || !bodies.ContainsKey(parent)) return;
                var j = c.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = bodies[parent];
                j.anchor = c.InverseTransformPoint(worldPt);
                j.enablePreprocessing = false;
                j.lowTwistLimit = new SoftJointLimit { limit = -30f };
                j.highTwistLimit = new SoftJointLimit { limit = 30f };
                j.swing1Limit = new SoftJointLimit { limit = 40f };
                j.swing2Limit = new SoftJointLimit { limit = 40f };
            }
            Vector3 P(float x, float y) => transform.TransformPoint(x, y, 0f);
            Join("Pelvis", torso, P(0f, 1.05f));
            Join("Head", torso, P(0f, 1.58f));
            Join("LegL", pelvis, P(-0.11f, 0.88f));
            Join("LegR", pelvis, P(0.11f, 0.88f));
            Join("ArmL", torso, P(-0.26f, 1.5f));
            Join("ArmR", torso, ShoulderR.x > 0 ? transform.TransformPoint(ShoulderR) : P(0.26f, 1.5f));

            // Push the part nearest the killing blow.
            Rigidbody nearest = null;
            float best = float.MaxValue;
            foreach (var kv in bodies)
            {
                float d = (kv.Key.position - hit.point).sqrMagnitude;
                if (d < best) { best = d; nearest = kv.Value; }
            }
            if (nearest != null && hit.impulse.sqrMagnitude > 0f)
                nearest.AddForceAtPosition(Vector3.ClampMagnitude(hit.impulse * 1.5f, 80f), hit.point, ForceMode.Impulse);

            var corpse = new GameObject("CombatEnemy_Corpse").AddComponent<CorpseCleanup>();
            corpse.parts = new List<Transform>(_parts);
            Destroy(_rb);
            Destroy(gameObject, 0.05f);
        }
    }

    /// <summary>Removes ragdoll parts after a delay.</summary>
    public sealed class CorpseCleanup : MonoBehaviour
    {
        public List<Transform> parts;
        public float lifetime = 8f;
        float _born;

        void Start() { _born = Time.time; }

        void Update()
        {
            if (Time.time - _born < lifetime) return;
            foreach (var p in parts)
                if (p != null) Destroy(p.gameObject);
            Destroy(gameObject);
        }
    }
}
