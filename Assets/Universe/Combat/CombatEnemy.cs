using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;

namespace RealityEngine.Combat
{
    public enum EnemyType { Raider = 0, Brute = 1, Duelist = 2, Caster = 3, Warden = 4 }

    /// <summary>
    /// Primitive humanoid fighter on an <see cref="ActiveRagdollRig"/> (Blade &amp; Sorcery-style): a kinematic
    /// "animation" root walks, faces and poses; the physical body chases that pose through a pelvis PD drive and
    /// joint slerp drives, so hits shove limbs around. Stagger = wobbly (drives ~40%), heavy hit / parry / loss of
    /// balance = limp for a moment, then gets back up; death = limp for good (loot weapon, corpse after 8 s).
    /// Types (see CombatEnemyTypes.cs): Raider (sword or axe, fast), Brute (mace, slow overhead smash, heavy),
    /// Duelist (saber, blocks your swings with its blade, counter-parries and ripostes, 2-3 hit combos, strafes),
    /// Caster (staff, keeps 4-9 m away and throws slow homing orbs you can bat back with a weapon or shield),
    /// Warden (tower shield + short sword thrusts and shield bashes; batter the shield to break its guard).
    /// Combat depth: windup telegraph glow (turns white in the perfect-parry window), perfect parry = big stagger,
    /// +15 mana and a chance to disarm; poise meter (break it for a long stagger taking +30% damage); directional
    /// hit reactions (torso / head recoil, stumble); disarmed enemies run for the nearest loose weapon or punch.
    /// Damage to the player goes through <see cref="PlayerHurtbox"/>; max 3 alive via EnemyDirector (waves set their own cap).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CombatEnemy : MonoBehaviour
    {
        public static readonly List<CombatEnemy> All = new List<CombatEnemy>();
        /// <summary>Force the type of the next spawn (toolbelt "Brute" / "Duelist" / "Caster" / "Warden" chips); cleared after use.</summary>
        public static EnemyType? NextSpawnOverride;
        static int _spawnCount;
        static readonly EnemyType[] Rotation =
            { EnemyType.Raider, EnemyType.Duelist, EnemyType.Raider, EnemyType.Warden, EnemyType.Caster, EnemyType.Brute };

        enum State { Approach, Guard, Windup, Swing, Recover, Block, Stagger, Down, GetUp, Dead }
        enum Attack { Slash, Thrust, Bash, Cast, Punch }

        const float PerfectWindow = 0.14f;

        public float moveSpeed = 1.8f;
        public float attackRange = 1.75f;
        public float swingDamage = 12f;

        public EnemyType Type { get; private set; }
        public CombatHealth Health { get; private set; }
        public bool IsDead => _state == State.Dead;
        public bool IsSwinging => _state == State.Swing && _attack != Attack.Cast;
        public bool IsBashing => _state == State.Swing && _attack == Attack.Bash;
        public bool IsDown => _state == State.Down || _state == State.GetUp;
        public bool IsArmed => _weapon != null;
        /// <summary>Last 0.14 s of a windup or first 0.14 s of a swing: a parry here is a perfect parry.</summary>
        public bool InParryWindow =>
            (_attack == Attack.Slash || _attack == Attack.Thrust) &&
            ((_state == State.Swing && _stateT < PerfectWindow) || (_state == State.Windup && _stateDur - _stateT < PerfectWindow));
        public float DeathTime { get; private set; } = -1f;
        /// <summary>Chest point for aiming (deflected orbs, spells).</summary>
        public Vector3 AimPoint => _torso != null && _torso.t != null ? _torso.t.position : transform.position + Vector3.up * 1.3f * _scale;

        float _scale = 1f;
        float _staggerDamage = 8f;
        float _knockdownDamage = 22f;
        float _windupTime = 0.5f;
        float _swingTime = 0.32f;
        float _damageMult = 1f;
        float _tempo = 1f;
        float _poiseMax = 30f, _poise = 30f, _lastPoiseHit = -99f;

        ActiveRagdollRig _rig;
        ActiveRagdollRig.Bone _pelvis, _torso, _head, _legL, _legR, _armL;
        PhysicsWeapon _weapon;
        float _gripY = 0.1f;
        Transform _rightArm;
        Transform _hpBar, _hpFill;
        GameObject _tell;
        Renderer _tellR;
        State _state = State.Approach;
        Attack _attack = Attack.Slash;
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

        // hit reaction
        Vector3 _reactAxis = Vector3.right;
        float _reactAmt, _reactStart = -99f;
        bool _reactHead;

        // disarm / re-arm
        PhysicsWeapon _seek;
        bool _seekShield;
        float _seekGiveUp, _noSeekUntil, _nextSeekScan;

        static float _lastNotice = -99f;
        static readonly Vector3 ShoulderR = new Vector3(0.24f, 1.5f, 0f);
        static readonly RaycastHit[] Hits = new RaycastHit[24];
        static Material _tellMat, _tellHotMat;

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
                type = Rotation[(_spawnCount - 1) % Rotation.Length];
                if (type == EnemyType.Brute && AnyAlive(EnemyType.Brute)) type = EnemyType.Raider;
                if (type == EnemyType.Caster && CountAlive(EnemyType.Caster) >= 2) type = EnemyType.Raider;
            }
            return Spawn(feet, facing, hpScale, type);
        }

        public static CombatEnemy Spawn(Vector3 feet, Quaternion facing, float hpScale, EnemyType type)
        {
            return Spawn(feet, facing, hpScale, type, 1f, 1f);
        }

        /// <summary>Full spawn: damageMult scales every hit on the player, tempoMult scales the pause between attacks (lower = more aggressive).</summary>
        public static CombatEnemy Spawn(Vector3 feet, Quaternion facing, float hpScale, EnemyType type, float damageMult, float tempoMult)
        {
            var go = new GameObject("CombatEnemy_" + type);
            Vector3 fwd = facing * Vector3.forward;
            fwd.y = 0f;
            go.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd : Vector3.forward));
            var e = go.AddComponent<CombatEnemy>();
            e.Build(hpScale, type, Mathf.Max(0.1f, damageMult), Mathf.Clamp(tempoMult, 0.3f, 3f));
            return e;
        }

        static bool AnyAlive(EnemyType t) => CountAlive(t) > 0;

        public static int CountAlive(EnemyType t)
        {
            int n = 0;
            foreach (var e in All)
                if (e != null && !e.IsDead && e.Type == t) n++;
            return n;
        }

        void Build(float hpScale, EnemyType type, float damageMult, float tempo)
        {
            Type = type;
            _damageMult = damageMult;
            _tempo = tempo;
            ConfigureType(out float hp, out float m);
            float k = _scale;

            Health = gameObject.AddComponent<CombatHealth>();
            Health.ResetHp(hp * hpScale);
            Health.team = CombatTeam.Enemy;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;

            Palette(out Material armor, out Material cloth, out Material skin, out Material visor);

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
            Decorate(armor, cloth, visor);
            // Right arm is visual-only (follows the weapon hand) so it never fights its own weapon.
            _rightArm = CombatUtil.Prim(PrimitiveType.Capsule, "ArmR", _rig.Container, Vector3.zero, new Vector3(0.11f, 0.3f, 0.11f) * k, armor, false).transform;

            // HP bar
            _hpBar = new GameObject("HpBar").transform;
            _hpBar.SetParent(transform, false);
            _hpBar.localPosition = new Vector3(0f, 2.02f * k, 0f);
            CombatUtil.Prim(PrimitiveType.Cube, "Bg", _hpBar, Vector3.zero, new Vector3(0.5f, 0.05f, 0.01f),
                CombatMaterials.Get(new Color(0.04f, 0.06f, 0.08f)), false);
            _hpFill = CombatUtil.Prim(PrimitiveType.Cube, "Fill", _hpBar, new Vector3(0f, 0f, -0.006f), new Vector3(0.48f, 0.035f, 0.01f),
                CombatMaterials.Get(new Color(0.9f, 0.15f, 0.1f), 0f, 0.3f, new Color(1f, 0.1f, 0.05f)), false).transform;
            _hpBar.gameObject.SetActive(false);

            Health.SetRenderers(_rig.Renderers());

            swingDamage *= _damageMult;
            _poise = _poiseMax;
            ArmUp();
            _nextAttack = Time.time + 1.5f * _tempo;
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
            if (_shield != null && _shield.enemyOwner == this)
                Destroy(_shield.gameObject);
            if (_castOrb != null)
                Destroy(_castOrb);
            // Despawned while alive (not a corpse): take the body with us.
            if (_state != State.Dead && _rig != null && _rig.Container != null)
                Destroy(_rig.Container.gameObject);
        }

        /// <summary>True if the collider is part of this enemy's body or its carried gear.</summary>
        public bool OwnsCollider(Collider c)
        {
            if (c == null) return false;
            if (_rig != null && _rig.Owns(c)) return true;
            var w = c.GetComponentInParent<PhysicsWeapon>();
            return w != null && w.enemyOwner == this;
        }

        // ------------------------------------------------------------------ Weapons

        void Equip(PhysicsWeapon w, bool shield)
        {
            if (w == null) return;
            w.enemyOwner = this;
            w.ownerTeam = CombatTeam.Enemy;
            w.enemyGuardOnly = shield;
            var tag = w.GetComponent<LootTag>();
            if (tag != null) Destroy(tag);
            if (w.IsStuck) w.Unstick();
            foreach (var pc in _rig.Colliders)
                foreach (var sc in w.Colliders)
                    if (sc != null && pc != null) UnityEngine.Physics.IgnoreCollision(sc, pc, true);
            if (shield)
            {
                _shield = w;
                IgnorePair(_weapon, _shield);
            }
            else
            {
                _weapon = w;
                _gripY = w.kind == WeaponKind.Staff ? 0.7f : 0.1f;
                IgnorePair(_weapon, _shield);
                MakeTell();
            }
        }

        static void IgnorePair(PhysicsWeapon a, PhysicsWeapon b)
        {
            if (a == null || b == null) return;
            foreach (var ca in a.Colliders)
                foreach (var cb in b.Colliders)
                    if (ca != null && cb != null) UnityEngine.Physics.IgnoreCollision(ca, cb, true);
        }

        /// <summary>Hands a carried weapon / shield to the world as loot (grabbable by the player, re-armable by enemies).</summary>
        static void Release(PhysicsWeapon w, string lootName, Vector3 fling)
        {
            if (w == null) return;
            w.enemyOwner = null;
            w.ownerTeam = CombatTeam.Neutral;
            w.enemyGuardOnly = false;
            w.gameObject.name = lootName;
            var tell = w.transform.Find("Tell");
            if (tell != null) Destroy(tell.gameObject);
            if (w.GetComponent<LootTag>() == null) w.gameObject.AddComponent<LootTag>();
            if (fling.sqrMagnitude > 0f && w.Body != null)
            {
                w.Body.AddForce(fling, ForceMode.VelocityChange);
                w.Body.AddTorque(Random.onUnitSphere * 6f, ForceMode.VelocityChange);
            }
        }

        void MakeTell()
        {
            if (_weapon == null) return;
            if (_tellMat == null) _tellMat = CombatMaterials.Transparent(new Color(1f, 0.45f, 0.1f, 0.55f), new Color(1f, 0.4f, 0.05f) * 2.5f);
            if (_tellHotMat == null) _tellHotMat = CombatMaterials.Transparent(new Color(1f, 0.95f, 0.75f, 0.7f), new Color(1f, 0.95f, 0.7f) * 4f);
            _tell = CombatUtil.Prim(PrimitiveType.Sphere, "Tell", _weapon.transform, new Vector3(0f, _weapon.tipY * 0.82f, 0f), Vector3.one * 0.08f, _tellMat, false);
            _tellR = _tell.GetComponent<Renderer>();
            _tellR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tell.SetActive(false);
        }

        void Disarm(Vector3 awayDir)
        {
            if (_weapon == null) return;
            var w = _weapon;
            _weapon = null;
            _tell = null;
            _tellR = null;
            awayDir.y = 0f;
            if (awayDir.sqrMagnitude < 1e-4f) awayDir = -transform.forward;
            Release(w, "Weapon_" + Type + "_Dropped", awayDir.normalized * 3f + Vector3.up * 2.5f);
            QhysicsSfx.PlayAt(SfxId.Disarm, w.transform.position, 0.9f);
            _noSeekUntil = Time.time + 1.2f;
            if (_state == State.Windup || _state == State.Swing) SetState(State.Recover, 0.5f);
        }

        static void Notice(string msg)
        {
            if (Time.unscaledTime - _lastNotice < 1.2f) return;
            _lastNotice = Time.unscaledTime;
            CombatHud.Toast(msg);
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

        Vector3 TorsoPos() => AimPoint;

        float TempoFor()
        {
            float t = Type == EnemyType.Brute ? 1.3f : Type == EnemyType.Duelist ? 0.75f : Type == EnemyType.Warden ? 1.1f : 1f;
            return t * _tempo;
        }

        void SetState(State s, float dur)
        {
            _state = s;
            _stateT = 0f;
            _stateDur = dur;
            if (s != State.Stagger && Health != null)
                Health.incomingMult = 1f;
            if (s == State.Windup)
            {
                if (_attack == Attack.Cast)
                    QhysicsSfx.PlayAt(SfxId.OrbCharge, TorsoPos(), 0.6f);
                else if (_attack != Attack.Punch && _weapon != null)
                    QhysicsSfx.PlayAt(SfxId.EnemyTell, _weapon.TipWorld, 0.35f, Type == EnemyType.Brute ? 0.75f : Type == EnemyType.Duelist ? 1.2f : 1f);
            }
            if (s == State.Swing)
            {
                _hitThisSwing = false;
                if (_attack != Attack.Cast)
                    QhysicsSfx.PlayAt(SfxId.Whoosh, _weapon != null ? _weapon.transform.position : transform.position + Vector3.up * 1.3f, 0.6f,
                        Type == EnemyType.Brute ? 0.65f : _attack == Attack.Bash ? 0.7f : 0.85f);
            }
        }

        void Update()
        {
            if (_state == State.Dead)
                return;
            _stateT += Time.deltaTime;
            float dist = Vector3.Distance(PlayerFeet(), transform.position);
            bool paused = CombatInputGate.Blocked;

            TickPoise();
            TickTypeTimers();
            UpdateSeek();
            if (Type == EnemyType.Caster) UpdateCaster(dist, paused);
            else UpdateMelee(dist, paused);
            UpdateTell();
        }

        /// <summary>Reach for the current loadout: weapon range, shield-bash range, or fists.</summary>
        float EffectiveRange()
        {
            if (_weapon != null) return attackRange;
            return (Type == EnemyType.Warden && _shield != null ? 1.3f : 1.05f) * _scale;
        }

        void UpdateMelee(float dist, bool paused)
        {
            float range = EffectiveRange();
            switch (_state)
            {
                case State.Approach:
                    if (_seek == null && dist <= range) SetState(State.Guard, 0f);
                    break;
                case State.Guard:
                    if (dist > range + 0.6f || _seek != null) { SetState(State.Approach, 0f); break; }
                    if (!paused && Time.time >= _nextAttack) BeginAttack(dist);
                    else TryBlock();
                    break;
                case State.Windup:
                    if (_stateT >= _stateDur) SetState(State.Swing, SwingTime());
                    break;
                case State.Swing:
                    if (_stateT >= _stateDur)
                    {
                        if (Type == EnemyType.Duelist && _comboLeft > 0 && !paused && _weapon != null && dist <= range + 0.4f)
                        {
                            _comboLeft--;
                            _side = -_side;
                            _attack = Attack.Slash;
                            SetState(State.Windup, 0.22f);
                        }
                        else SetState(State.Recover, _attack == Attack.Bash ? 0.45f : 0.6f);
                    }
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
                        if (_state != State.Block) _nextAttack = Time.time + Random.Range(1.1f, 2.2f) * TempoFor();
                        SetState(State.Guard, 0f);
                    }
                    break;
            }
        }

        void BeginAttack(float dist)
        {
            _attack = Attack.Slash;
            _comboLeft = 0;
            if (Type == EnemyType.Warden && _shield != null && (_weapon == null || (dist < 1.35f * _scale && Random.value < 0.5f)))
                _attack = Attack.Bash;
            else if (_weapon == null)
                _attack = Attack.Punch;
            else if (Type == EnemyType.Warden)
                _attack = Attack.Thrust;
            if (Type == EnemyType.Duelist && _weapon != null)
            {
                _comboLeft = Random.Range(1, 3);
                _side = Random.value < 0.5f ? 1f : -1f;
            }
            if (_attack == Attack.Bash && _shield == null) _attack = Attack.Punch;
            float w = _windupTime;
            if (_attack == Attack.Bash) w *= 0.7f;
            if (_attack == Attack.Punch) w = 0.35f;
            SetState(State.Windup, w);
        }

        float SwingTime()
        {
            switch (_attack)
            {
                case Attack.Bash: return 0.25f;
                case Attack.Punch: return 0.22f;
                case Attack.Cast: return 0.25f;
                default: return _swingTime;
            }
        }

        void TryBlock()
        {
            if (Time.time < _nextBlockCheck || Type == EnemyType.Brute || Type == EnemyType.Caster) return; // brutes take it on the armour
            bool duelist = Type == EnemyType.Duelist;
            bool warden = Type == EnemyType.Warden && _shield != null;
            if (_weapon == null && !warden) return;
            _nextBlockCheck = Time.time + (duelist ? 0.1f : 0.2f);
            Vector3 head = _head != null && _head.t != null ? _head.t.position : transform.TransformPoint(0f, 1.5f, 0f);
            float minLin = duelist ? 2f : 2.5f, minAng = duelist ? 5f : 6f, reach = duelist ? 1.5f : 1.3f;
            foreach (var w in PhysicsWeapon.All)
            {
                if (w == null || !w.HeldByPlayer) continue;
                if (w.Body.linearVelocity.magnitude < minLin && w.Body.angularVelocity.magnitude < minAng) continue;
                if (Vector3.Distance(w.TipWorld, head) > reach) continue;
                float chance = duelist ? 0.85f : warden ? 0.8f : 0.5f;
                if (Random.value < chance * SkillSystem.EnemyBlockChanceMult)
                {
                    _threat = w;
                    SetState(State.Block, duelist ? 0.45f : warden ? 0.7f : 0.55f);
                }
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
            _poise = _poiseMax;
            _comboLeft = 0;
        }

        /// <summary>Heavy external impact (deflected orb, explosion): go limp briefly.</summary>
        public void OnHeavyImpact(float seconds)
        {
            if (_state == State.Dead || _state == State.Down) return;
            Knockdown(Type == EnemyType.Brute ? seconds * 0.6f : seconds);
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
            SetState(State.GetUp, Type == EnemyType.Brute ? 1.3f : Type == EnemyType.Warden ? 1.15f : 1.0f);
        }

        void TickPoise()
        {
            if (_poise < _poiseMax && Time.time - _lastPoiseHit > 1.5f)
                _poise = Mathf.Min(_poiseMax, _poise + _poiseMax * 0.3f * Time.deltaTime);
        }

        void PoiseBreak(Vector3 point)
        {
            _poise = _poiseMax;
            _comboLeft = 0;
            SetState(State.Stagger, 1.1f);
            Health.incomingMult = 1.3f;
            QhysicsSfx.PlayAt(SfxId.GuardBreak, point, 0.8f);
            SparkBurst.Spawn(point, new Color(1f, 0.75f, 0.3f), 12);
            Notice("Staggered - strike now!");
        }

        // ------------------------------------------------------------------ Disarmed: find a weapon

        void UpdateSeek()
        {
            if (Type == EnemyType.Caster) { _seek = null; return; } // casters keep casting without the staff
            bool needWeapon = _weapon == null;
            bool needShield = Type == EnemyType.Warden && _shield == null;
            if (!needWeapon && !needShield) { _seek = null; return; }
            if (_state != State.Approach && _state != State.Guard) return;
            if (_seek != null)
            {
                if (!SeekValid(_seek) || Time.time > _seekGiveUp)
                {
                    _seek = null;
                    _noSeekUntil = Time.time + 5f;
                    return;
                }
                Vector3 d = _seek.Body.worldCenterOfMass - transform.position;
                float vy = d.y;
                d.y = 0f;
                if (d.magnitude < 0.8f * _scale && vy < 1.4f)
                {
                    var w = _seek;
                    _seek = null;
                    Equip(w, _seekShield);
                    QhysicsSfx.PlayAt(SfxId.Unstick, w.transform.position, 0.6f, 0.8f);
                    _nextAttack = Mathf.Max(_nextAttack, Time.time + 0.8f);
                }
                return;
            }
            if (Time.time < _noSeekUntil || Time.time < _nextSeekScan) return;
            _nextSeekScan = Time.time + 0.5f;
            PhysicsWeapon best = null;
            bool bestShield = false;
            float bd = 8f;
            foreach (var w in PhysicsWeapon.All)
            {
                if (!SeekValid(w)) continue;
                bool isShield = w.IsShield;
                if (isShield ? !needShield : !needWeapon) continue;
                Vector3 d = w.Body.worldCenterOfMass - transform.position;
                if (Mathf.Abs(d.y) > 1.3f) continue;
                d.y = 0f;
                float m = d.magnitude;
                if (m < bd) { bd = m; best = w; bestShield = isShield; }
            }
            if (best != null)
            {
                _seek = best;
                _seekShield = bestShield;
                _seekGiveUp = Time.time + 7f;
            }
        }

        static bool SeekValid(PhysicsWeapon w)
        {
            return w != null && w.Body != null && !w.IsHeld && !w.IsStuck && w.enemyOwner == null && w.ownerTeam != CombatTeam.Enemy
                && w.GetComponent<RecallFlight>() == null && w.GetComponent<RackHold>() == null && w.Body.linearVelocity.sqrMagnitude < 4f;
        }

        // ------------------------------------------------------------------ Physics step

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
            DriveShield();
            TryPunchHit();
        }

        void Locomote(float dt)
        {
            Vector3 playerFeet = PlayerFeet();
            Vector3 goal = playerFeet;
            if (_seek != null && _seek.Body != null)
            {
                goal = _seek.Body.worldCenterOfMass;
                goal.y = transform.position.y;
            }
            Vector3 to = goal - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
            Vector3 toPlayer = playerFeet - transform.position;
            toPlayer.y = 0f;
            float pDist = toPlayer.magnitude;
            Vector3 pDir = pDist > 0.01f ? toPlayer / pDist : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, pDir);

            if (_state != State.Stagger)
            {
                Quaternion face = Quaternion.LookRotation(_seek != null ? dir : pDir, Vector3.up);
                float turn = Type == EnemyType.Brute ? 150f : Type == EnemyType.Duelist ? 320f : Type == EnemyType.Warden ? 170f : 240f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turn * dt);
            }

            Vector3 want = Vector3.zero;
            if (_state == State.Approach) want = dir * moveSpeed * (_seek != null ? 1.1f : 1f);
            else if (Type == EnemyType.Caster)
            {
                if ((_state == State.Guard || _state == State.Recover) && pDist < 4f) want = -pDir * 1.5f;
                else if (_state == State.Guard) want = right * _strafe * 0.8f;
            }
            else if ((_state == State.Guard || _state == State.Recover) && pDist < 1.0f * _scale) want = -pDir * 1.2f;
            else if (_state == State.Windup && pDist > 1.3f * _scale) want = pDir * (_attack == Attack.Bash ? 1.4f : 0.8f);
            if (Type == EnemyType.Duelist)
            {
                if (_state == State.Guard && pDist >= 1.0f * _scale) want += right * _strafe * 1.1f;
                if (_state == State.Recover && _stateT < 0.3f) want = -pDir * 1.6f;
            }
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
            Quaternion arm = Type == EnemyType.Warden && _shield != null ? Quaternion.Euler(-70f, 0f, 15f) : Quaternion.Euler(-40f, 0f, -10f);
            Quaternion windT, swingT;
            switch (_attack)
            {
                case Attack.Thrust: windT = Quaternion.Euler(-4f, 15f, 0f); swingT = Quaternion.Euler(14f, -5f, 0f); break;
                case Attack.Bash: windT = Quaternion.Euler(-6f, -20f, 0f); swingT = Quaternion.Euler(12f, 15f, 0f); break;
                case Attack.Cast: windT = Quaternion.Euler(-10f, 10f, 0f); swingT = Quaternion.Euler(12f, -5f, 0f); break;
                default:
                    windT = brute ? Quaternion.Euler(-14f, 6f, 0f) : Quaternion.Euler(-5f, 25f * _side, 0f);
                    swingT = brute ? Quaternion.Euler(28f, -6f, 0f) : Quaternion.Euler(18f, -30f * _side, 0f);
                    break;
            }
            switch (_state)
            {
                case State.Windup:
                    torso = Quaternion.Slerp(guardT, windT, s);
                    arm = _attack == Attack.Cast ? Quaternion.Euler(-85f, 0f, 10f) : _attack == Attack.Bash ? Quaternion.Euler(-55f, 0f, 30f) : Quaternion.Euler(-60f, 0f, -20f);
                    break;
                case State.Swing:
                    torso = Quaternion.Slerp(windT, swingT, k);
                    arm = _attack == Attack.Cast ? Quaternion.Euler(-95f, 0f, 0f) : _attack == Attack.Bash ? Quaternion.Euler(-85f, 0f, 5f) : Quaternion.Euler(-25f, 0f, -25f);
                    break;
                case State.Recover: torso = Quaternion.Slerp(swingT, guardT, s); break;
                case State.Block:
                    torso = Quaternion.identity;
                    arm = Type == EnemyType.Warden && _shield != null ? Quaternion.Euler(-95f, 0f, 10f) : Quaternion.Euler(-80f, 0f, -10f);
                    break;
                case State.Stagger: torso = Quaternion.Euler(-20f, 0f, 0f); head = Quaternion.Euler(-25f, 0f, 0f); arm = Quaternion.Euler(-20f, 0f, -45f); break;
            }

            // Directional hit reaction: lean away from the hit, head snaps on head hits.
            float rk = 1f - (Time.time - _reactStart) / 0.35f;
            if (rk > 0f && _state != State.Down && _state != State.GetUp)
            {
                Quaternion lean = Quaternion.AngleAxis(_reactAmt * rk, _reactAxis);
                torso = lean * torso;
                if (_reactHead) head = Quaternion.AngleAxis(_reactAmt * 1.4f * rk, _reactAxis) * head;
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

        Vector3 FromTorso(Vector3 lp)
        {
            Quaternion torsoRot = _torso.rb.rotation;
            return _torso.rb.position + torsoRot * (lp * _scale - _torso.center);
        }

        void DriveWeapon()
        {
            if (_weapon == null || _weapon.enemyOwner != this || _torso == null || _torso.rb == null) return;
            float strength = _strength;
            if (strength < 0.05f) return; // limp: the weapon drops with the arm
            GetHandTarget(out Vector3 lp, out Quaternion lr);
            // Hand path is authored root-local; carry it on the physical torso so the arm reacts to hits and leans.
            Vector3 hand = FromTorso(lp);
            Quaternion rot = _torso.rb.rotation * lr;
            Vector3 targetPos = hand - rot * new Vector3(0f, _gripY, 0f);
            float mass = Type == EnemyType.Brute ? 2.2f : _weapon.kind == WeaponKind.Staff ? 1.6f : _weapon.kind == WeaponKind.Axe ? 1.4f : 1f;
            PhysicsHands.Drive(_weapon.Body, targetPos, rot, _torso.rb.linearVelocity, 520f * mass * strength, 50f * mass * strength, 0.04f, 0.05f);
        }

        Vector3 M(Vector3 p) => _side < 0f ? new Vector3(-p.x, p.y, p.z) : p;
        Quaternion MR(float x, float y, float z) => Quaternion.Euler(x, y * _side, z * _side);

        void GetHandTarget(out Vector3 lp, out Quaternion lr)
        {
            bool brute = Type == EnemyType.Brute;
            bool staff = _weapon != null && _weapon.kind == WeaponKind.Staff;
            Vector3 guardP = staff ? new Vector3(0.32f, 1.05f, 0.25f) : new Vector3(0.25f, 1.25f, 0.42f);
            Quaternion guardR = staff ? Quaternion.Euler(8f, 0f, 0f) : Quaternion.Euler(30f, 0f, 0f);
            Vector3 windP, endP;
            Quaternion windR, endR;
            switch (_attack)
            {
                case Attack.Thrust:
                    windP = new Vector3(0.3f, 1.35f, -0.12f); windR = Quaternion.Euler(80f, 0f, 0f);
                    endP = new Vector3(0.12f, 1.32f, 0.95f); endR = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case Attack.Bash:
                    windP = endP = guardP; windR = endR = guardR;
                    break;
                case Attack.Cast:
                    windP = new Vector3(0.3f, 1.3f, 0.1f); windR = Quaternion.Euler(-25f, 0f, 0f);
                    endP = new Vector3(0.25f, 1.35f, 0.55f); endR = Quaternion.Euler(55f, 0f, 0f);
                    break;
                case Attack.Punch:
                    guardP = new Vector3(0.22f, 1.3f, 0.3f); guardR = Quaternion.Euler(80f, 0f, 0f);
                    windP = new Vector3(0.3f, 1.4f, -0.05f); windR = guardR;
                    endP = new Vector3(0.08f, 1.42f, 0.75f); endR = guardR;
                    break;
                default:
                    windP = brute ? new Vector3(0.12f, 2.0f, -0.1f) : M(new Vector3(0.38f, 1.72f, -0.02f));
                    windR = brute ? Quaternion.Euler(-60f, 0f, 0f) : MR(-40f, 0f, -40f);
                    endP = brute ? new Vector3(0.08f, 0.8f, 0.72f) : M(new Vector3(-0.32f, 0.95f, 0.6f));
                    endR = brute ? Quaternion.Euler(120f, 0f, 0f) : MR(100f, 0f, 60f);
                    if (staff) { windP = new Vector3(0.35f, 1.6f, 0f); windR = Quaternion.Euler(-50f, 0f, -20f); endP = new Vector3(-0.1f, 1.1f, 0.6f); endR = Quaternion.Euler(70f, 0f, 30f); }
                    break;
            }
            float k = _stateDur > 0f ? Mathf.Clamp01(_stateT / _stateDur) : 1f;
            float s = k * k * (3f - 2f * k);
            switch (_state)
            {
                case State.Windup: lp = Vector3.Lerp(guardP, windP, s); lr = Quaternion.Slerp(guardR, windR, s); return;
                case State.Swing: lp = Vector3.Lerp(windP, endP, k); lr = Quaternion.Slerp(windR, endR, k); return;
                case State.Recover: lp = Vector3.Lerp(endP, guardP, s); lr = Quaternion.Slerp(endR, guardR, s); return;
                case State.Block: BlockHandTarget(out lp, out lr); return;
                case State.Stagger: lp = new Vector3(0.35f, 0.9f, 0.15f); lr = Quaternion.Euler(150f, 0f, 0f); return;
                default: lp = guardP; lr = guardR; return;
            }
        }

        Vector3 HandWorld()
        {
            if (_torso == null || _torso.rb == null) return transform.position + Vector3.up * 1.3f;
            GetHandTarget(out Vector3 lp, out _);
            return FromTorso(lp);
        }

        void TryPunchHit()
        {
            if (_state != State.Swing || _attack != Attack.Punch || _hitThisSwing || _stateT < SwingTime() * 0.4f) return;
            var hb = PlayerHurtbox.Instance;
            if (hb == null || hb.Collider == null) return;
            Vector3 hand = HandWorld();
            Vector3 cp = hb.Collider.ClosestPoint(hand);
            if ((cp - hand).sqrMagnitude > 0.3f * 0.3f) return;
            _hitThisSwing = true;
            hb.Hurt(6f * _damageMult, cp, Type.ToString());
            QhysicsSfx.PlayAt(SfxId.FleshHit, cp, 0.6f, 1.2f);
        }

        void UpdateTell()
        {
            if (_tell == null) return;
            bool melee = _attack == Attack.Slash || _attack == Attack.Thrust;
            bool show = melee && (_state == State.Windup || (_state == State.Swing && _stateT < PerfectWindow));
            if (_tell.activeSelf != show) _tell.SetActive(show);
            if (!show) return;
            bool hot = InParryWindow;
            if (_tellR != null) _tellR.sharedMaterial = hot ? _tellHotMat : _tellMat;
            float k = _stateDur > 0f ? Mathf.Clamp01(_stateT / _stateDur) : 1f;
            float sz = hot ? 0.14f : 0.05f + 0.05f * k;
            Vector3 ps = _tell.transform.parent != null ? _tell.transform.parent.lossyScale : Vector3.one;
            _tell.transform.localScale = new Vector3(sz / Mathf.Max(1e-3f, ps.x), sz / Mathf.Max(1e-3f, ps.y), sz / Mathf.Max(1e-3f, ps.z));
        }

        void LateUpdate()
        {
            if (_state == State.Dead) return;
            if (_rightArm != null && _torso != null && _torso.t != null)
            {
                Vector3 sh = _torso.t.position + _torso.t.rotation * (ShoulderR * _scale - _torso.center);
                Vector3 hand = _weapon != null ? _weapon.transform.TransformPoint(0f, _gripY, 0f)
                    : (_strength > 0.05f && _torso.rb != null ? HandWorld() : sh + Vector3.down * 0.5f);
                Vector3 d = hand - sh;
                float len = Mathf.Clamp(d.magnitude, 0.2f, 0.8f * _scale);
                _rightArm.position = sh + d.normalized * (len * 0.5f);
                _rightArm.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.down);
                _rightArm.localScale = new Vector3(0.11f * _scale, len * 0.5f, 0.11f * _scale);
            }
            UpdateCastOrb();
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
            if (_hitThisSwing || _state != State.Swing || _attack == Attack.Cast || _attack == Attack.Punch) return;
            _hitThisSwing = true;
            float dmg = _attack == Attack.Bash ? 8f * _damageMult : swingDamage;
            hurt.Hurt(dmg, point, Type.ToString());
        }

        /// <summary>Force telekinesis grabbed a body part: go limp so the limb can be yanked (B&amp;S lift).</summary>
        public void OnForceLifted()
        {
            if (_state == State.Dead) return;
            Knockdown(Type == EnemyType.Brute ? 1.6f : 1.3f);
        }

        public void OnWeaponBlocked(bool parry, Vector3 point)
        {
            OnWeaponBlocked(parry, false, point, null);
        }

        /// <summary>The player's weapon met this enemy's weapon mid-attack (block = slow, parry = fast, perfect = fast inside the window).</summary>
        public void OnWeaponBlocked(bool parry, bool perfect, Vector3 point, PhysicsWeapon by)
        {
            if (_state == State.Dead) return;
            _hitThisSwing = true;
            _comboLeft = 0;
            if (parry && perfect)
            {
                PerfectParried(point, by);
                return;
            }
            if (parry)
            {
                if (Type == EnemyType.Brute) SetState(State.Stagger, 0.9f);
                else if (Type == EnemyType.Duelist) SetState(State.Stagger, 0.5f);
                else if (Type == EnemyType.Warden) SetState(State.Stagger, 0.7f);
                else Knockdown(1.2f);
                if (_torso != null && _torso.rb != null)
                    _torso.rb.AddForce(-transform.forward * 2.5f, ForceMode.VelocityChange);
            }
            else
                SetState(State.Recover, 0.5f);
        }

        void PerfectParried(Vector3 point, PhysicsWeapon by)
        {
            float disarmChance;
            switch (Type)
            {
                case EnemyType.Brute: disarmChance = 0.2f; SetState(State.Stagger, 1.4f); break;
                case EnemyType.Duelist: disarmChance = 0.35f; SetState(State.Stagger, 1.0f); break;
                case EnemyType.Warden: disarmChance = 0.45f; SetState(State.Stagger, 1.0f); break;
                case EnemyType.Caster: disarmChance = 0.7f; Knockdown(1.4f); break;
                default: disarmChance = 0.6f; Knockdown(1.5f); break;
            }
            if (_torso != null && _torso.rb != null)
                _torso.rb.AddForce(-transform.forward * 3.5f, ForceMode.VelocityChange);
            QhysicsSfx.PlayAt(SfxId.PerfectParry, point, 1f);
            SparkBurst.Spawn(point, new Color(0.6f, 1f, 1f), 30);
            if (by != null && by.HeldByPlayer) CombatHaptics.Pulse(by.PrimaryNode, 1f, 0.15f);
            ManaPool.Add(15f);
            bool disarmed = _weapon != null && Random.value < disarmChance;
            if (disarmed)
                Disarm(by != null ? transform.position - by.transform.position : -transform.forward);
            Notice(disarmed ? "Perfect parry - disarmed!" : "Perfect parry! +15 mana");
        }

        /// <summary>
        /// The player's weapon hit this enemy's weapon while it was not attacking. Returns true when handled
        /// (duelist counter-parry, guard break, disarm) so the caller skips the plain clang.
        /// </summary>
        public bool OnWeaponStruck(PhysicsWeapon by, float speed, Vector3 point)
        {
            if (_state == State.Dead || by == null) return false;
            if (Type == EnemyType.Duelist && _state == State.Block)
                return DuelistCounter(by, speed, point);
            if (_weapon != null && Type != EnemyType.Brute && speed > 8f && by.Body.mass >= 1.8f && _state != State.Down && Random.value < 0.25f)
            {
                Disarm(transform.position - by.transform.position);
                SparkBurst.Spawn(point, new Color(1f, 0.9f, 0.7f), 10);
                Notice("Disarmed!");
                return true;
            }
            return false;
        }

        void OnDamaged(DamageInfo hit)
        {
            if (_state == State.Dead) return;
            _flinchUntil = Time.time + 0.15f;
            // The root is kinematic: push the part that was hit instead.
            var b = _rig != null ? _rig.Nearest(hit.point) : null;
            if (b != null && b.rb != null && hit.impulse.sqrMagnitude > 0f)
                b.rb.AddForceAtPosition(Vector3.ClampMagnitude(hit.impulse, 40f), hit.point, ForceMode.Impulse);
            React(hit, b);
            if (_state == State.Down || _state == State.GetUp) return;
            _lastPoiseHit = Time.time;
            _poise -= hit.amount * (hit.type == DamageType.Blunt ? 1.4f : hit.type == DamageType.Force ? 1.2f : 1f);
            if (hit.amount >= _knockdownDamage)
                Knockdown(Mathf.Lerp(1.2f, 2.0f, Mathf.InverseLerp(_knockdownDamage, _knockdownDamage * 2f, hit.amount)));
            else if (_poise <= 0f)
                PoiseBreak(hit.point);
            else if (hit.amount >= _staggerDamage && _state != State.Stagger)
            {
                _comboLeft = 0;
                SetState(State.Stagger, Mathf.Lerp(0.3f, 0.7f, Mathf.InverseLerp(_staggerDamage, _knockdownDamage, hit.amount)));
            }
        }

        void React(DamageInfo hit, ActiveRagdollRig.Bone bone)
        {
            Vector3 dirW = hit.impulse.sqrMagnitude > 1e-4f ? hit.impulse : TorsoPos() - hit.point;
            dirW.y = 0f;
            if (dirW.sqrMagnitude < 1e-6f) return;
            dirW.Normalize();
            Vector3 dl = transform.InverseTransformDirection(dirW);
            Vector3 axis = Vector3.Cross(Vector3.up, dl);
            if (axis.sqrMagnitude < 1e-6f) return;
            _reactAxis = axis.normalized;          // tilts the top toward the push direction (away from the attacker)
            _reactAmt = Mathf.Clamp(hit.amount * 1.2f, 6f, 28f);
            _reactStart = Time.time;
            _reactHead = bone != null && bone == _head;
            if (_state != State.Down && _state != State.GetUp)
                _vel += dirW * Mathf.Clamp(hit.amount / 15f, 0.2f, 1.5f); // stumble
        }

        void OnDied(DamageInfo hit)
        {
            if (_state == State.Dead) return;
            _state = State.Dead;
            DeathTime = Time.time;
            Vector3 at = _torso != null && _torso.t != null ? _torso.t.position : transform.position + Vector3.up;
            QhysicsSfx.PlayAt(SfxId.EnemyDeath, at, 0.9f);
            if (_hpBar != null) _hpBar.gameObject.SetActive(false);
            if (_castOrb != null) { Destroy(_castOrb); _castOrb = null; }
            _seek = null;

            // Weapon (and shield) become loot the player can pick up.
            if (_weapon != null)
            {
                string loot = Type == EnemyType.Brute ? "Weapon_BruteMace_Loot" : Type == EnemyType.Raider && _weapon.kind == WeaponKind.EnemySword
                    ? "Weapon_RaiderSword_Loot" : "Weapon_" + Type + "_Loot";
                Release(_weapon, loot, Vector3.zero);
                _weapon = null;
                _tell = null;
            }
            if (_shield != null)
            {
                Release(_shield, "Weapon_WardenShield_Loot", Vector3.zero);
                _shield = null;
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

    /// <summary>Marks a weapon an enemy dropped (disarm / death) so the arena can tidy old loot.</summary>
    public sealed class LootTag : MonoBehaviour
    {
        public float born;
        void Awake() { born = Time.time; }
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
