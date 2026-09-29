using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;

namespace RealityEngine.Combat
{
    public enum EnemyType { Raider = 0, Brute = 1 }

    /// <summary>
    /// Primitive humanoid fighter on an <see cref="ActiveRagdollRig"/> (Blade &amp; Sorcery-style): a kinematic
    /// "animation" root walks, faces and poses; the physical body chases that pose through a pelvis PD drive and
    /// joint slerp drives, so hits shove limbs around. Stagger = wobbly (drives ~40%), heavy hit / parry / loss of
    /// balance = limp for a moment, then gets back up; death = limp for good (loot weapon, corpse after 8 s).
    /// Types: Raider (sword, fast) and Brute (mace, slow overhead smash, heavier, harder to knock down).
    /// Damage to the player goes through <see cref="PlayerHurtbox"/>; max 3 alive (EnemyDirector.MaxEnemies).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEnemy : MonoBehaviour
    {
        public static readonly List<CombatEnemy> All = new List<CombatEnemy>();
        /// <summary>Force the type of the next spawn (toolbelt "Brute" chip); cleared after use.</summary>
        public static EnemyType? NextSpawnOverride;
        static int _spawnCount;

        enum State { Approach, Guard, Windup, Swing, Recover, Block, Stagger, Down, GetUp, Dead }

        public float moveSpeed = 1.8f;
        public float attackRange = 1.75f;
        public float swingDamage = 12f;

        public EnemyType Type { get; private set; }
        public CombatHealth Health { get; private set; }
        public bool IsDead => _state == State.Dead;
        public bool IsSwinging => _state == State.Swing;
        public bool IsDown => _state == State.Down || _state == State.GetUp;
        public float DeathTime { get; private set; } = -1f;

        float _scale = 1f;
        float _staggerDamage = 8f;
        float _knockdownDamage = 22f;
        float _windupTime = 0.5f;
        float _swingTime = 0.32f;

        ActiveRagdollRig _rig;
        ActiveRagdollRig.Bone _pelvis, _torso, _head, _legL, _legR, _armL;
        PhysicsWeapon _weapon;
        Transform _rightArm;
        Transform _hpBar, _hpFill;
        State _state = State.Approach;
        float _stateT, _stateDur;
        float _nextAttack;
        float _nextBlockCheck;
        bool _hitThisSwing;
        Vector3 _vel;
        float _walkPhase;
        float _flinchUntil;
        float _offBalanceT;
        float _strength = 1f;
        Vector3 _getupFromOffset;
        Quaternion _getupFromRot = Quaternion.identity;

        static readonly Vector3 ShoulderR = new Vector3(0.24f, 1.5f, 0f);
        static readonly RaycastHit[] Hits = new RaycastHit[24];

        public static CombatEnemy Spawn(Vector3 feet, Quaternion facing, float hpScale = 1f)
        {
            EnemyType type;
            if (NextSpawnOverride.HasValue)
            {
                type = NextSpawnOverride.Value;
                NextSpawnOverride = null;
            }
            else
            {
                _spawnCount++;
                type = _spawnCount % 3 == 0 && !AnyAlive(EnemyType.Brute) ? EnemyType.Brute : EnemyType.Raider;
            }
            return Spawn(feet, facing, hpScale, type);
        }

        public static CombatEnemy Spawn(Vector3 feet, Quaternion facing, float hpScale, EnemyType type)
        {
            var go = new GameObject(type == EnemyType.Brute ? "CombatEnemy_Brute" : "CombatEnemy_Raider");
            Vector3 fwd = facing * Vector3.forward;
            fwd.y = 0f;
            go.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd : Vector3.forward));
            var e = go.AddComponent<CombatEnemy>();
            e.Build(hpScale, type);
            return e;
        }

        static bool AnyAlive(EnemyType t)
        {
            foreach (var e in All)
                if (e != null && !e.IsDead && e.Type == t) return true;
            return false;
        }

        void Build(float hpScale, EnemyType type)
        {
            Type = type;
            bool brute = type == EnemyType.Brute;
            float hp = 60f;
            if (brute)
            {
                _scale = 1.15f;
                hp = 120f;
                moveSpeed = 1.25f;
                attackRange = 1.95f;
                swingDamage = 22f;
                _staggerDamage = 16f;
                _knockdownDamage = 38f;
                _windupTime = 0.85f;
                _swingTime = 0.38f;
            }
            float k = _scale;
            float m = brute ? 1.5f : 1f;

            Health = gameObject.AddComponent<CombatHealth>();
            Health.ResetHp(hp * hpScale);
            Health.team = CombatTeam.Enemy;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;

            Material armor = brute ? CombatMaterials.Get(new Color(0.42f, 0.3f, 0.16f), 0.75f, 0.5f)
                                   : CombatMaterials.Get(new Color(0.2f, 0.21f, 0.24f), 0.6f, 0.45f);
            Material cloth = brute ? CombatMaterials.Get(new Color(0.16f, 0.14f, 0.12f), 0f, 0.25f)
                                   : CombatMaterials.Get(new Color(0.35f, 0.08f, 0.07f), 0f, 0.3f);
            Material skin = CombatMaterials.Get(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.4f);
            Material visor = CombatMaterials.Get(new Color(0.3f, 0.02f, 0.02f), 0f, 0.8f, new Color(1f, 0.1f, 0.05f) * 2f);

            _rig = new ActiveRagdollRig(gameObject.name, transform.position);
            Transform r = transform;
            _pelvis = Bone(PrimitiveType.Cube, "Pelvis", -1, new Vector3(0f, 0.95f, 0f), new Vector3(0.34f, 0.18f, 0.2f), new Vector3(0f, 0.95f, 0f), 12f * m, 0f, cloth, 0.9f);
            _torso = Bone(PrimitiveType.Cube, "Torso", 0, new Vector3(0f, 1.3f, 0f), new Vector3(0.4f, 0.5f, 0.24f), new Vector3(0f, 1.04f, 0f), 20f * m, 900f * m, armor, 1f);
            _head = Bone(PrimitiveType.Sphere, "Head", 1, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.24f, new Vector3(0f, 1.58f, 0f), 5f * m, 160f * m, skin, 2f);
            _legL = Bone(PrimitiveType.Capsule, "LegL", 0, new Vector3(-0.11f, 0.45f, 0f), new Vector3(0.15f, 0.44f, 0.15f), new Vector3(-0.11f, 0.88f, 0f), 9f * m, 450f * m, cloth, 0.6f);
            _legR = Bone(PrimitiveType.Capsule, "LegR", 0, new Vector3(0.11f, 0.45f, 0f), new Vector3(0.15f, 0.44f, 0.15f), new Vector3(0.11f, 0.88f, 0f), 9f * m, 450f * m, cloth, 0.6f);
            _armL = Bone(PrimitiveType.Capsule, "ArmL", 1, new Vector3(-0.29f, 1.24f, 0.05f), new Vector3(0.11f, 0.3f, 0.11f), new Vector3(-0.27f, 1.5f, 0f), 4f * m, 160f * m, armor, 0.6f);
            _rig.BuildJoints(r);

            CombatUtil.Prim(PrimitiveType.Cube, "Visor", _head.t, new Vector3(0f, 0.05f, 0.42f), new Vector3(0.8f, 0.14f, 0.2f), visor, false);
            if (brute)
            {
                CombatUtil.Prim(PrimitiveType.Cube, "HornL", _head.t, new Vector3(-0.45f, 0.45f, 0f), new Vector3(0.12f, 0.5f, 0.12f), armor, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                CombatUtil.Prim(PrimitiveType.Cube, "HornR", _head.t, new Vector3(0.45f, 0.45f, 0f), new Vector3(0.12f, 0.5f, 0.12f), armor, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
                CombatUtil.Prim(PrimitiveType.Cube, "Pauldrons", _torso.t, new Vector3(0f, 0.45f, 0f), new Vector3(1.35f, 0.18f, 1.15f), armor, false);
            }
            // Right arm is visual-only (follows the weapon hand) so it never fights its own weapon.
            _rightArm = CombatUtil.Prim(PrimitiveType.Capsule, "ArmR", _rig.Container, Vector3.zero, new Vector3(0.11f, 0.3f, 0.11f) * k, armor, false).transform;

            // HP bar
            _hpBar = new GameObject("HpBar").transform;
            _hpBar.SetParent(transform, false);
            _hpBar.localPosition = new Vector3(0f, 2.02f * k, 0f);
            CombatUtil.Prim(PrimitiveType.Cube, "Bg", _hpBar, Vector3.zero, new Vector3(0.5f, 0.05f, 0.01f),
                CombatMaterials.Get(new Color(0.04f, 0.06f, 0.08f)), false);
            _hpFill = CombatUtil.Prim(PrimitiveType.Cube, "Fill", _hpBar, new Vector3(0f, 0f, -0.006f), new Vector3(0.48f, 0.035f, 0.01f),
                CombatMaterials.Get(new Color(0.9f, 0.15f, 0.1f), 0f, 0.3f, new Color(1f, 0.1f, 0.05f))).transform;
            _hpBar.gameObject.SetActive(false);

            Health.SetRenderers(_rig.Renderers());

            // Weapon
            Vector3 hand = transform.TransformPoint(new Vector3(0.25f, 1.25f, 0.4f) * k);
            _weapon = WeaponFactory.Spawn(brute ? WeaponKind.Mace : WeaponKind.EnemySword, hand, transform.rotation);
            _weapon.enemyOwner = this;
            _weapon.ownerTeam = CombatTeam.Enemy;
            if (brute)
                _weapon.displayName = "Brute Mace";
            foreach (var pc in _rig.Colliders)
                foreach (var sc in _weapon.Colliders)
                    if (sc != null && pc != null) UnityEngine.Physics.IgnoreCollision(sc, pc, true);
            _nextAttack = Time.time + 1.5f;
            All.Add(this);
        }

        ActiveRagdollRig.Bone Bone(PrimitiveType t, string name, int parent, Vector3 center, Vector3 size, Vector3 pivot,
            float mass, float spring, Material mat, float mult)
        {
            float k = _scale;
            var b = _rig.AddBone(transform, t, name, parent, center * k, size * k, pivot * k, mass, spring, mat);
            var bp = b.t.gameObject.AddComponent<BodyPart>();
            bp.health = Health;
            bp.multiplier = mult;
            bp.zone = name;
            return b;
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (_weapon != null && _weapon.enemyOwner == this)
                Destroy(_weapon.gameObject);
            // Despawned while alive (not a corpse): take the body with us.
            if (_state != State.Dead && _rig != null && _rig.Container != null)
                Destroy(_rig.Container.gameObject);
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
                QhysicsSfx.PlayAt(SfxId.Whoosh, _weapon != null ? _weapon.transform.position : transform.position, 0.6f,
                    Type == EnemyType.Brute ? 0.65f : 0.85f);
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
                    if (!paused && Time.time >= _nextAttack) SetState(State.Windup, _windupTime);
                    else TryBlock();
                    break;
                case State.Windup:
                    if (_stateT >= _stateDur) SetState(State.Swing, _swingTime);
                    break;
                case State.Swing:
                    if (_stateT >= _stateDur) SetState(State.Recover, 0.6f);
                    break;
                case State.Down:
                    if (_stateT >= _stateDur) BeginGetUp();
                    break;
                case State.Recover:
                case State.Block:
                case State.Stagger:
                case State.GetUp:
                    if (_stateT >= _stateDur)
                    {
                        if (_state != State.Block) _nextAttack = Time.time + Random.Range(1.1f, 2.2f) * (Type == EnemyType.Brute ? 1.3f : 1f);
                        SetState(State.Guard, 0f);
                    }
                    break;
            }
        }

        void TryBlock()
        {
            if (Time.time < _nextBlockCheck || Type == EnemyType.Brute) return; // brutes take it on the armour
            _nextBlockCheck = Time.time + 0.2f;
            Vector3 head = _head != null && _head.t != null ? _head.t.position : transform.TransformPoint(0f, 1.5f, 0f);
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

        void Knockdown(float seconds)
        {
            if (_state == State.Dead) return;
            SetState(State.Down, seconds);
            _strength = 0f;
            _rig.ApplyStrength(0f, true);
            _offBalanceT = 0f;
        }

        void BeginGetUp()
        {
            // Re-root under the fallen pelvis, face the player, then blend the pelvis target from where it lies to standing.
            if (_pelvis != null && _pelvis.rb != null)
            {
                Vector3 p = _pelvis.rb.position;
                Vector3 feet = new Vector3(p.x, transform.position.y, p.z);
                if (FindGround(feet, transform.position.y, out float gy)) feet.y = gy;
                transform.position = feet;
                Vector3 to = PlayerFeet() - feet;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                _getupFromOffset = transform.InverseTransformPoint(p) - _pelvis.pivot;
                _getupFromRot = Quaternion.Inverse(transform.rotation) * _pelvis.rb.rotation;
            }
            _vel = Vector3.zero;
            SetState(State.GetUp, Type == EnemyType.Brute ? 1.3f : 1.0f);
        }

        void FixedUpdate()
        {
            if (_state == State.Dead || _rig == null || _pelvis == null || _pelvis.rb == null)
                return;
            float dt = Time.fixedDeltaTime;

            if (_state == State.Down)
            {
                // Root follows the limp body so spells, AI distance and the HP bar stay on it.
                Vector3 p = _pelvis.rb.position;
                Vector3 feet = new Vector3(p.x, transform.position.y, p.z);
                if (FindGround(feet, transform.position.y, out float gy)) feet.y = gy;
                transform.position = feet;
            }
            else if (_state != State.GetUp)
            {
                Locomote(dt);
            }

            Vector3 pelvisOffset = Pose(dt);
            _rig.SolveTargets(transform, pelvisOffset);

            float target = 1f;
            float blend = _stateDur > 0f ? Mathf.Clamp01(_stateT / _stateDur) : 1f;
            if (_state == State.Down) target = 0f;
            else if (_state == State.Stagger) target = 0.4f;
            else if (_state == State.GetUp) target = Mathf.Lerp(0.5f, 1f, blend);
            if (Time.time < _flinchUntil) target *= 0.55f;
            _strength = target < _strength ? Mathf.MoveTowards(_strength, target, dt * 10f) : Mathf.MoveTowards(_strength, target, dt * 3f);
            _rig.ApplyStrength(_strength);
            _rig.Step(70f, 45f);

            // Lost balance (shoved / blasted far from the pose) -> go limp and get back up.
            if (_state != State.Down && _state != State.GetUp)
            {
                if (_rig.PelvisError() > 0.5f * _scale) _offBalanceT += dt;
                else _offBalanceT = 0f;
                if (_offBalanceT > 0.2f) Knockdown(1.3f);
            }

            DriveWeapon();
        }

        void Locomote(float dt)
        {
            Vector3 to = PlayerFeet() - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

            if (_state != State.Stagger)
            {
                Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
                float turn = Type == EnemyType.Brute ? 150f : 240f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turn * dt);
            }

            Vector3 want = Vector3.zero;
            if (_state == State.Approach) want = dir * moveSpeed;
            else if ((_state == State.Guard || _state == State.Recover) && dist < 1.0f * _scale) want = -dir * 1.2f;
            else if (_state == State.Windup && dist > 1.3f * _scale) want = dir * 0.8f;
            // Separation from other enemies.
            foreach (var e in All)
            {
                if (e == null || e == this || e.IsDead) continue;
                Vector3 d = transform.position - e.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                if (m < 0.9f && m > 1e-3f) want += d / m * (0.9f - m) * 3f;
            }
            _vel = Vector3.MoveTowards(_vel, want, dt * 8f);

            // Shoves: let the root drift toward where the body was pushed (stumble) instead of snapping back.
            Vector3 err = _pelvis.rb.position - _pelvis.targetPos;
            err.y = 0f;
            Vector3 delta = _vel * dt;
            if (err.magnitude > 0.2f * _scale)
                delta += err * Mathf.Min(1f, 3f * dt);

            if (delta.sqrMagnitude < 1e-10f)
                return;
            Vector3 pos = transform.position;
            float len = delta.magnitude;
            Vector3 mdir = delta / len;
            int n = UnityEngine.Physics.SphereCastNonAlloc(pos + Vector3.up * 0.6f * _scale, 0.22f * _scale, mdir, Hits, len + 0.25f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = Hits[i].collider;
                if (!Blocks(c) || Hits[i].distance <= 0f) continue;
                Vector3 nrm = Hits[i].normal;
                nrm.y = 0f;
                if (nrm.sqrMagnitude < 1e-4f) continue;
                nrm.Normalize();
                if (Vector3.Dot(delta, nrm) < 0f) delta -= nrm * Vector3.Dot(delta, nrm);
                if (Vector3.Dot(_vel, nrm) < 0f) _vel -= nrm * Vector3.Dot(_vel, nrm);
            }
            Vector3 next = pos + delta;
            if (FindGround(next, pos.y, out float gy))
            {
                if (Mathf.Abs(gy - pos.y) < 0.5f) next.y = gy;
                else next = pos; // ledge / wall step: stay
            }
            else
                next = pos; // no ground ahead: don't walk off
            transform.position = next;
        }

        bool Blocks(Collider c)
        {
            if (c == null || c.isTrigger || c.attachedRigidbody != null) return false;
            if (_rig != null && _rig.Owns(c)) return false;
            return !CombatInputGate.IsPlayerCollider(c);
        }

        bool FindGround(Vector3 p, float refY, out float groundY)
        {
            groundY = refY;
            int n = UnityEngine.Physics.RaycastNonAlloc(new Vector3(p.x, refY + 1.2f, p.z), Vector3.down, Hits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (!Blocks(Hits[i].collider)) continue;
                if (Hits[i].distance < best) { best = Hits[i].distance; groundY = Hits[i].point.y; found = true; }
            }
            return found;
        }

        // ------------------------------------------------------------------ Pose ("animation")

        Vector3 Pose(float dt)
        {
            bool brute = Type == EnemyType.Brute;
            float k = _stateDur > 0f ? Mathf.Clamp01(_stateT / _stateDur) : 1f;
            float s = k * k * (3f - 2f * k);

            Quaternion guardT = Quaternion.Euler(8f, 0f, 0f);
            Quaternion torso = guardT;
            Quaternion head = Quaternion.Euler(-8f, 0f, 0f);
            Quaternion arm = Quaternion.Euler(-40f, 0f, -10f);
            Quaternion windT = brute ? Quaternion.Euler(-14f, 6f, 0f) : Quaternion.Euler(-5f, 25f, 0f);
            Quaternion swingT = brute ? Quaternion.Euler(28f, -6f, 0f) : Quaternion.Euler(18f, -30f, 0f);
            switch (_state)
            {
                case State.Windup: torso = Quaternion.Slerp(guardT, windT, s); arm = Quaternion.Euler(-60f, 0f, -20f); break;
                case State.Swing: torso = Quaternion.Slerp(windT, swingT, k); arm = Quaternion.Euler(-25f, 0f, -25f); break;
                case State.Recover: torso = Quaternion.Slerp(swingT, guardT, s); break;
                case State.Block: torso = Quaternion.identity; arm = Quaternion.Euler(-80f, 0f, -10f); break;
                case State.Stagger: torso = Quaternion.Euler(-20f, 0f, 0f); head = Quaternion.Euler(-25f, 0f, 0f); arm = Quaternion.Euler(-20f, 0f, -45f); break;
            }

            // Walk cycle from the root's horizontal speed.
            float speed = new Vector3(_vel.x, 0f, _vel.z).magnitude;
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, moveSpeed));
            _walkPhase += speed * dt * (5.5f / _scale);
            float sw = Mathf.Sin(_walkPhase) * 30f * speed01;
            Quaternion legL = Quaternion.Euler(-sw, 0f, 0f);
            Quaternion legR = Quaternion.Euler(sw, 0f, 0f);
            arm = arm * Quaternion.Euler(sw * 0.5f, 0f, 0f);
            Vector3 offset = new Vector3(0f, -Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.03f * speed01 * _scale, 0f);
            Quaternion pelvis = Quaternion.identity;

            if (_state == State.GetUp)
            {
                offset = Vector3.Lerp(_getupFromOffset, Vector3.zero, s);
                pelvis = Quaternion.Slerp(_getupFromRot, Quaternion.identity, s);
                legL = legR = Quaternion.identity;
            }

            _pelvis.local = pelvis;
            _torso.local = torso;
            _head.local = head;
            _armL.local = arm;
            _legL.local = legL;
            _legR.local = legR;
            return offset;
        }

        void DriveWeapon()
        {
            if (_weapon == null || _weapon.enemyOwner != this || _torso == null || _torso.rb == null) return;
            float strength = _strength;
            if (strength < 0.05f) return; // limp: the weapon drops with the arm
            GetHandTarget(out Vector3 lp, out Quaternion lr);
            // Hand path is authored root-local; carry it on the physical torso so the arm reacts to hits and leans.
            Quaternion torsoRot = _torso.rb.rotation;
            Vector3 hand = _torso.rb.position + torsoRot * (lp * _scale - _torso.center);
            Quaternion rot = torsoRot * lr;
            Vector3 targetPos = hand - rot * new Vector3(0f, 0.1f, 0f);
            float mass = Type == EnemyType.Brute ? 2.2f : 1f;
            PhysicsHands.Drive(_weapon.Body, targetPos, rot, _torso.rb.linearVelocity, 520f * mass * strength, 50f * mass * strength, 0.04f, 0.05f);
        }

        void GetHandTarget(out Vector3 lp, out Quaternion lr)
        {
            bool brute = Type == EnemyType.Brute;
            Vector3 guardP = new Vector3(0.25f, 1.25f, 0.42f);
            Quaternion guardR = Quaternion.Euler(30f, 0f, 0f);
            Vector3 windP = brute ? new Vector3(0.12f, 2.0f, -0.1f) : new Vector3(0.38f, 1.72f, -0.02f);
            Quaternion windR = brute ? Quaternion.Euler(-60f, 0f, 0f) : Quaternion.Euler(-40f, 0f, -40f);
            Vector3 endP = brute ? new Vector3(0.08f, 0.8f, 0.72f) : new Vector3(-0.32f, 0.95f, 0.6f);
            Quaternion endR = brute ? Quaternion.Euler(120f, 0f, 0f) : Quaternion.Euler(100f, 0f, 60f);
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
            if (_state == State.Dead) return;
            if (_rightArm != null && _torso != null && _torso.t != null)
            {
                Vector3 sh = _torso.t.position + _torso.t.rotation * (ShoulderR * _scale - _torso.center);
                Vector3 hand = _weapon != null ? _weapon.transform.TransformPoint(0f, 0.1f, 0f) : sh + Vector3.down * 0.5f;
                Vector3 d = hand - sh;
                float len = Mathf.Clamp(d.magnitude, 0.2f, 0.8f * _scale);
                _rightArm.position = sh + d.normalized * (len * 0.5f);
                _rightArm.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.down);
                _rightArm.localScale = new Vector3(0.11f * _scale, len * 0.5f, 0.11f * _scale);
            }
            if (_hpBar != null)
            {
                bool show = Health.Hp < Health.maxHp;
                _hpBar.gameObject.SetActive(show);
                if (show)
                {
                    if (_head != null && _head.t != null)
                        _hpBar.position = _head.t.position + Vector3.up * 0.32f * _scale;
                    float f = Health.Hp01;
                    _hpFill.localScale = new Vector3(0.48f * f, 0.035f, 0.01f);
                    _hpFill.localPosition = new Vector3(-0.24f * (1f - f), 0f, -0.006f);
                    var cam = CombatInputGate.ViewCamera();
                    if (cam != null)
                        _hpBar.rotation = Quaternion.LookRotation(_hpBar.position - cam.transform.position);
                }
            }
        }

        // ------------------------------------------------------------------ Events

        public void OnSwingConnected(PlayerHurtbox hurt, Vector3 point)
        {
            if (_hitThisSwing || _state != State.Swing) return;
            _hitThisSwing = true;
            hurt.Hurt(swingDamage, point, Type == EnemyType.Brute ? "Brute" : "Raider");
        }

        public void OnWeaponBlocked(bool parry, Vector3 point)
        {
            if (_state == State.Dead) return;
            _hitThisSwing = true;
            if (parry)
            {
                if (Type == EnemyType.Brute) SetState(State.Stagger, 0.9f);
                else Knockdown(1.2f);
                if (_torso != null && _torso.rb != null)
                    _torso.rb.AddForce(-transform.forward * 2.5f, ForceMode.VelocityChange);
            }
            else
                SetState(State.Recover, 0.5f);
        }

        void OnDamaged(DamageInfo hit)
        {
            if (_state == State.Dead) return;
            _flinchUntil = Time.time + 0.15f;
            // The root is kinematic: push the part that was hit instead.
            var b = _rig != null ? _rig.Nearest(hit.point) : null;
            if (b != null && b.rb != null && hit.impulse.sqrMagnitude > 0f)
                b.rb.AddForceAtPosition(Vector3.ClampMagnitude(hit.impulse, 40f), hit.point, ForceMode.Impulse);
            if (_state == State.Down || _state == State.GetUp) return;
            if (hit.amount >= _knockdownDamage)
                Knockdown(Mathf.Lerp(1.2f, 2.0f, Mathf.InverseLerp(_knockdownDamage, _knockdownDamage * 2f, hit.amount)));
            else if (hit.amount >= _staggerDamage && _state != State.Stagger)
                SetState(State.Stagger, Mathf.Lerp(0.3f, 0.7f, Mathf.InverseLerp(_staggerDamage, _knockdownDamage, hit.amount)));
        }

        void OnDied(DamageInfo hit)
        {
            if (_state == State.Dead) return;
            _state = State.Dead;
            DeathTime = Time.time;
            Vector3 at = _torso != null && _torso.t != null ? _torso.t.position : transform.position + Vector3.up;
            QhysicsSfx.PlayAt(SfxId.EnemyDeath, at, 0.9f);
            if (_hpBar != null) _hpBar.gameObject.SetActive(false);

            // Weapon becomes loot the player can pick up.
            if (_weapon != null)
            {
                _weapon.enemyOwner = null;
                _weapon.ownerTeam = CombatTeam.Neutral;
                _weapon.gameObject.name = Type == EnemyType.Brute ? "Weapon_BruteMace_Loot" : "Weapon_RaiderSword_Loot";
                _weapon = null;
            }
            // Visual right arm joins the ragdoll.
            if (_rightArm != null && _torso != null && _torso.rb != null)
            {
                var col = _rightArm.gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.5f;
                col.height = 2f;
                foreach (var c in _rig.Colliders)
                    if (c != null) UnityEngine.Physics.IgnoreCollision(c, col, true);
                var rb = _rightArm.gameObject.AddComponent<Rigidbody>();
                rb.mass = 4f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                var j = _rightArm.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = _torso.rb;
                j.anchor = new Vector3(0f, 1f, 0f);
                j.enablePreprocessing = false;
            }
            _rig.GoLimpForever(8f);
            var b = _rig.Nearest(hit.point);
            if (b != null && b.rb != null && hit.impulse.sqrMagnitude > 0f)
                b.rb.AddForceAtPosition(Vector3.ClampMagnitude(hit.impulse * 1.5f, 80f), hit.point, ForceMode.Impulse);
            Destroy(gameObject, 0.05f);
        }
    }

    /// <summary>Removes ragdoll parts after a delay (legacy; kept for anything still spawning loose parts).</summary>
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
