using UnityEngine;
using RealityEngine.Audio;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Per-type setup and behaviour for <see cref="CombatEnemy"/>: stats, colours / silhouettes, starting gear,
    /// Duelist blade-blocking + counter-parry + combos, Caster orb casting, Warden shield guard meter and bash.
    /// </summary>
    public sealed partial class CombatEnemy
    {
        PhysicsWeapon _shield;
        int _comboLeft;
        float _side = 1f;
        float _strafe = 1f, _nextStrafeFlip;
        PhysicsWeapon _threat;
        float _lastCounter = -99f;
        float _guard, _lastGuardHit = -99f;
        const float GuardMax = 100f;
        GameObject _castOrb;
        float _nextMelee;

        public float Guard01 => _shield != null ? Mathf.Clamp01(_guard / GuardMax) : 0f;

        void ConfigureType(out float hp, out float massMult)
        {
            hp = 60f;
            massMult = 1f;
            _poiseMax = 30f;
            switch (Type)
            {
                case EnemyType.Brute:
                    _scale = 1.15f; hp = 120f; moveSpeed = 1.25f; attackRange = 1.95f; swingDamage = 22f;
                    _staggerDamage = 16f; _knockdownDamage = 38f; _windupTime = 0.85f; _swingTime = 0.38f; massMult = 1.5f; _poiseMax = 70f;
                    break;
                case EnemyType.Duelist:
                    _scale = 0.97f; hp = 70f; moveSpeed = 2.4f; attackRange = 1.85f; swingDamage = 10f;
                    _staggerDamage = 11f; _knockdownDamage = 26f; _windupTime = 0.32f; _swingTime = 0.24f; massMult = 0.95f; _poiseMax = 26f;
                    break;
                case EnemyType.Caster:
                    _scale = 0.95f; hp = 45f; moveSpeed = 1.6f; attackRange = 1.9f; swingDamage = 8f;
                    _staggerDamage = 7f; _knockdownDamage = 18f; _windupTime = 0.45f; _swingTime = 0.3f; massMult = 0.85f; _poiseMax = 18f;
                    break;
                case EnemyType.Warden:
                    _scale = 1.05f; hp = 95f; moveSpeed = 1.45f; attackRange = 1.9f; swingDamage = 14f;
                    _staggerDamage = 14f; _knockdownDamage = 32f; _windupTime = 0.6f; _swingTime = 0.3f; massMult = 1.25f; _poiseMax = 50f;
                    break;
            }
            _strafe = Random.value < 0.5f ? 1f : -1f;
        }

        void Palette(out Material armor, out Material cloth, out Material skin, out Material visor)
        {
            skin = CombatMaterials.Get(new Color(0.3f, 0.3f, 0.32f), 0.3f, 0.4f);
            switch (Type)
            {
                case EnemyType.Brute:
                    armor = CombatMaterials.Get(new Color(0.42f, 0.3f, 0.16f), 0.75f, 0.5f);
                    cloth = CombatMaterials.Get(new Color(0.16f, 0.14f, 0.12f), 0f, 0.25f);
                    visor = CombatMaterials.Get(new Color(0.3f, 0.02f, 0.02f), 0f, 0.8f, new Color(1f, 0.1f, 0.05f) * 2f);
                    break;
                case EnemyType.Duelist:
                    armor = CombatMaterials.Get(new Color(0.1f, 0.1f, 0.12f), 0.7f, 0.6f);
                    cloth = CombatMaterials.Get(new Color(0.5f, 0.06f, 0.08f), 0f, 0.35f);
                    visor = CombatMaterials.Get(new Color(0.35f, 0.22f, 0.02f), 0f, 0.8f, new Color(1f, 0.7f, 0.2f) * 2f);
                    break;
                case EnemyType.Caster:
                    armor = CombatMaterials.Get(new Color(0.16f, 0.1f, 0.22f), 0.2f, 0.4f);
                    cloth = CombatMaterials.Get(new Color(0.12f, 0.08f, 0.18f), 0f, 0.3f);
                    visor = CombatMaterials.Get(new Color(0.25f, 0.05f, 0.4f), 0f, 0.8f, new Color(0.7f, 0.2f, 1f) * 2f);
                    break;
                case EnemyType.Warden:
                    armor = CombatMaterials.Get(new Color(0.34f, 0.37f, 0.42f), 0.8f, 0.5f);
                    cloth = CombatMaterials.Get(new Color(0.1f, 0.12f, 0.2f), 0f, 0.3f);
                    visor = CombatMaterials.Get(new Color(0.3f, 0.02f, 0.02f), 0f, 0.8f, new Color(1f, 0.1f, 0.05f) * 2f);
                    break;
                default:
                    armor = CombatMaterials.Get(new Color(0.2f, 0.21f, 0.24f), 0.6f, 0.45f);
                    cloth = CombatMaterials.Get(new Color(0.35f, 0.08f, 0.07f), 0f, 0.3f);
                    visor = CombatMaterials.Get(new Color(0.3f, 0.02f, 0.02f), 0f, 0.8f, new Color(1f, 0.1f, 0.05f) * 2f);
                    break;
            }
        }

        /// <summary>Visual-only accessory on a (non-uniformly scaled) bone: offset / size are in metres in the bone's frame.</summary>
        GameObject Deco(ActiveRagdollRig.Bone bone, PrimitiveType t, string name, Vector3 offsetM, Vector3 sizeM, Material m)
        {
            Vector3 ps = bone.t.localScale;
            var g = CombatUtil.Prim(t, name, bone.t, Vector3.zero, Vector3.one, m, false);
            g.transform.localPosition = new Vector3(offsetM.x / ps.x, offsetM.y / ps.y, offsetM.z / ps.z) * _scale;
            g.transform.localScale = new Vector3(sizeM.x / ps.x, sizeM.y / ps.y, sizeM.z / ps.z) * _scale;
            return g;
        }

        void Decorate(Material armor, Material cloth, Material visor)
        {
            switch (Type)
            {
                case EnemyType.Brute:
                    CombatUtil.Prim(PrimitiveType.Cube, "HornL", _head.t, new Vector3(-0.45f, 0.45f, 0f), new Vector3(0.12f, 0.5f, 0.12f), armor, false)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    CombatUtil.Prim(PrimitiveType.Cube, "HornR", _head.t, new Vector3(0.45f, 0.45f, 0f), new Vector3(0.12f, 0.5f, 0.12f), armor, false)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
                    CombatUtil.Prim(PrimitiveType.Cube, "Pauldrons", _torso.t, new Vector3(0f, 0.45f, 0f), new Vector3(1.35f, 0.18f, 1.15f), armor, false);
                    break;
                case EnemyType.Duelist:
                    Deco(_head, PrimitiveType.Cube, "Plume", new Vector3(0f, 0.14f, -0.03f), new Vector3(0.03f, 0.1f, 0.22f), cloth);
                    Deco(_torso, PrimitiveType.Cube, "Sash", new Vector3(0f, 0f, 0f), new Vector3(0.41f, 0.07f, 0.25f), cloth)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    Deco(_torso, PrimitiveType.Cube, "Gorget", new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.05f, 0.2f), visor);
                    break;
                case EnemyType.Caster:
                    Deco(_head, PrimitiveType.Sphere, "Hood", new Vector3(0f, 0.025f, -0.045f), new Vector3(0.29f, 0.3f, 0.29f), cloth);
                    Deco(_pelvis, PrimitiveType.Cylinder, "Robe", new Vector3(0f, -0.3f, 0f), new Vector3(0.42f, 0.32f, 0.34f), cloth);
                    Deco(_torso, PrimitiveType.Cube, "Rune", new Vector3(0f, 0.05f, 0.125f), new Vector3(0.08f, 0.16f, 0.01f), visor);
                    break;
                case EnemyType.Warden:
                    Deco(_head, PrimitiveType.Cube, "Crest", new Vector3(0f, 0.15f, 0f), new Vector3(0.04f, 0.08f, 0.26f), visor);
                    Deco(_torso, PrimitiveType.Cube, "Pauldrons", new Vector3(0f, 0.22f, 0f), new Vector3(0.52f, 0.08f, 0.28f), armor);
                    Deco(_pelvis, PrimitiveType.Cube, "Tassets", new Vector3(0f, -0.12f, 0f), new Vector3(0.36f, 0.16f, 0.22f), armor);
                    break;
            }
        }

        void ArmUp()
        {
            Vector3 hand = transform.TransformPoint(new Vector3(0.25f, 1.25f, 0.4f) * _scale);
            PhysicsWeapon w;
            switch (Type)
            {
                case EnemyType.Brute:
                    w = WeaponFactory.Spawn(WeaponKind.Mace, hand, transform.rotation);
                    w.displayName = "Brute Mace";
                    break;
                case EnemyType.Duelist: w = WeaponFactory.Spawn(WeaponKind.Saber, hand, transform.rotation, true); break;
                case EnemyType.Caster: w = WeaponFactory.Spawn(WeaponKind.Staff, transform.TransformPoint(new Vector3(0.32f, 0.4f, 0.25f) * _scale), transform.rotation, true); break;
                case EnemyType.Warden: w = WeaponFactory.Spawn(WeaponKind.EnemySword, hand, transform.rotation); break;
                default:
                    w = Random.value < 0.3f ? WeaponFactory.Spawn(WeaponKind.Axe, hand, transform.rotation, true)
                                            : WeaponFactory.Spawn(WeaponKind.EnemySword, hand, transform.rotation);
                    break;
            }
            Equip(w, false);
            if (Type == EnemyType.Warden)
            {
                Vector3 sh = transform.TransformPoint(new Vector3(-0.2f, 1.2f, 0.5f) * _scale);
                Equip(WeaponFactory.Spawn(WeaponKind.TowerShield, sh, transform.rotation * Quaternion.Euler(90f, 0f, 0f), true), true);
            }
        }

        void TickTypeTimers()
        {
            if (Time.time >= _nextStrafeFlip)
            {
                _nextStrafeFlip = Time.time + Random.Range(1.5f, 3f);
                _strafe = -_strafe;
            }
            if (_shield != null && _guard > 0f && Time.time - _lastGuardHit > 2f)
                _guard = Mathf.Max(0f, _guard - 15f * Time.deltaTime);
        }

        // ------------------------------------------------------------------ Caster

        const float CastRange = 9f;

        void UpdateCaster(float dist, bool paused)
        {
            switch (_state)
            {
                case State.Approach:
                    if (dist <= CastRange) SetState(State.Guard, 0f);
                    break;
                case State.Guard:
                    if (dist > CastRange + 1.5f) { SetState(State.Approach, 0f); break; }
                    if (paused) break;
                    if (dist < 2.1f && _weapon != null && Time.time >= _nextMelee)
                    {
                        _attack = Attack.Slash;
                        _nextMelee = Time.time + 2.5f;
                        SetState(State.Windup, _windupTime);
                    }
                    else if (Time.time >= _nextAttack)
                    {
                        _attack = Attack.Cast;
                        SetState(State.Windup, 0.9f);
                    }
                    break;
                case State.Windup:
                    if (_stateT >= _stateDur)
                    {
                        if (_attack == Attack.Cast) LaunchOrb();
                        SetState(State.Swing, _attack == Attack.Cast ? 0.25f : _swingTime);
                    }
                    break;
                case State.Swing:
                    if (_stateT >= _stateDur) SetState(State.Recover, _attack == Attack.Cast ? 0.7f : 0.6f);
                    break;
                case State.Down:
                    if (_stateT >= _stateDur) BeginGetUp();
                    break;
                default:
                    if (_stateT >= _stateDur)
                    {
                        if (_state != State.Block) _nextAttack = Time.time + Random.Range(1.8f, 3.0f) * _tempo;
                        SetState(State.Guard, 0f);
                    }
                    break;
            }
        }

        Vector3 CastPoint()
        {
            if (_weapon != null) return _weapon.TipWorld + _weapon.transform.up * 0.08f;
            if (_armL != null && _armL.t != null) return _armL.t.TransformPoint(0f, -1.15f, 0f);
            return transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
        }

        void LaunchOrb()
        {
            var cam = CombatInputGate.ViewCamera();
            Vector3 target = cam != null ? cam.transform.position + Vector3.down * 0.3f : transform.position + transform.forward * 5f + Vector3.up * 1.3f;
            EnemyOrb.Launch(CastPoint(), target, this, 13f * _damageMult);
            QhysicsSfx.PlayAt(SfxId.OrbFire, CastPoint(), 0.75f);
        }

        void UpdateCastOrb()
        {
            bool show = _state == State.Windup && _attack == Attack.Cast;
            if (!show)
            {
                if (_castOrb != null && _castOrb.activeSelf) _castOrb.SetActive(false);
                return;
            }
            if (_castOrb == null)
            {
                _castOrb = CombatUtil.Prim(PrimitiveType.Sphere, "CastOrb_" + name, null, CastPoint(), Vector3.one * 0.05f, EnemyOrb.EnemyMaterial(), false);
                _castOrb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!_castOrb.activeSelf) _castOrb.SetActive(true);
            float k = _stateDur > 0f ? Mathf.Clamp01(_stateT / _stateDur) : 1f;
            _castOrb.transform.position = CastPoint();
            _castOrb.transform.localScale = Vector3.one * Mathf.Lerp(0.04f, 0.18f, k) * (1f + 0.08f * Mathf.Sin(Time.time * 30f));
        }

        // ------------------------------------------------------------------ Duelist

        void BlockHandTarget(out Vector3 lp, out Quaternion lr)
        {
            if (Type == EnemyType.Duelist && _threat != null)
            {
                // Put the blade across the incoming weapon's path.
                Vector3 mid = Vector3.Lerp(_threat.transform.position, _threat.TipWorld, 0.6f);
                Vector3 local = transform.InverseTransformPoint(mid) / Mathf.Max(0.1f, _scale);
                local.x = Mathf.Clamp(local.x, -0.45f, 0.5f);
                local.y = Mathf.Clamp(local.y, 0.95f, 1.85f);
                local.z = 0.5f;
                lp = local;
                lr = local.y > 1.45f ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(10f, 0f, local.x < 0f ? 20f : -20f);
                return;
            }
            if (Type == EnemyType.Warden && _shield != null)
            {
                lp = new Vector3(0.35f, 1.1f, 0.2f);
                lr = Quaternion.Euler(20f, 0f, 0f);
                return;
            }
            lp = new Vector3(0f, 1.5f, 0.45f);
            lr = Quaternion.Euler(0f, 0f, 90f);
        }

        bool DuelistCounter(PhysicsWeapon by, float speed, Vector3 point)
        {
            if (Time.time - _lastCounter < 0.3f) return true;
            _lastCounter = Time.time;
            if (speed > 9f || by.Body.mass > 2.5f)
            {
                // Too heavy / too fast to turn aside: the guard folds.
                SetState(State.Stagger, 0.7f);
                Health.incomingMult = 1.2f;
                QhysicsSfx.PlayAt(SfxId.GuardBreak, point, 0.8f);
                SparkBurst.Spawn(point, new Color(1f, 0.85f, 0.5f), 14);
                Notice("Guard broken!");
                return true;
            }
            // Counter-parry: knock the player's blade aside, then riposte (which you can perfect-parry back).
            Vector3 away = by.Body.worldCenterOfMass - TorsoPos();
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = transform.forward;
            by.Body.AddForce(away.normalized * 4.5f + Vector3.up * 1.2f, ForceMode.VelocityChange);
            if (by.HeldByPlayer) CombatHaptics.Pulse(by.PrimaryNode, 0.85f, 0.12f);
            QhysicsSfx.PlayAt(SfxId.Parry, point, 0.9f, 0.85f);
            SparkBurst.Spawn(point, new Color(1f, 0.6f, 0.3f), 14);
            _attack = Attack.Slash;
            _comboLeft = 0;
            _side = -_side;
            SetState(State.Windup, 0.18f);
            Notice("Parried - riposte incoming!");
            return true;
        }

        // ------------------------------------------------------------------ Warden shield

        void GetShieldTarget(out Vector3 lp, out Quaternion lr)
        {
            Vector3 guardP = new Vector3(-0.18f, 1.2f, 0.45f);
            Quaternion guardR = Quaternion.Euler(90f, 0f, 0f); // face forward, long side vertical
            float k = _stateDur > 0f ? Mathf.Clamp01(_stateT / _stateDur) : 1f;
            float s = k * k * (3f - 2f * k);
            switch (_state)
            {
                case State.Block: lp = new Vector3(-0.05f, 1.45f, 0.5f); lr = Quaternion.Euler(78f, 0f, 0f); return;
                case State.Windup when _attack == Attack.Bash: lp = Vector3.Lerp(guardP, new Vector3(-0.2f, 1.2f, 0.22f), s); lr = guardR; return;
                case State.Swing when _attack == Attack.Bash: lp = Vector3.Lerp(new Vector3(-0.2f, 1.2f, 0.22f), new Vector3(-0.05f, 1.25f, 0.9f), k); lr = guardR; return;
                case State.Stagger: lp = new Vector3(-0.55f, 1.0f, 0.2f); lr = Quaternion.Euler(60f, -60f, 0f); return;
                default: lp = guardP; lr = guardR; return;
            }
        }

        void DriveShield()
        {
            if (_shield == null || _shield.enemyOwner != this || _torso == null || _torso.rb == null) return;
            float strength = _strength;
            if (strength < 0.05f) return;
            GetShieldTarget(out Vector3 lp, out Quaternion lr);
            Vector3 hand = FromTorso(lp);
            Quaternion rot = _torso.rb.rotation * lr;
            Vector3 targetPos = hand - rot * new Vector3(0f, 0.03f, 0f);
            PhysicsHands.Drive(_shield.Body, targetPos, rot, _torso.rb.linearVelocity, 900f * strength, 120f * strength, 0.04f, 0.05f);
        }

        /// <summary>The player's weapon hit this Warden's shield: fill the guard meter; full = guard break.</summary>
        public void OnGuardStruck(PhysicsWeapon by, float speed, Vector3 point)
        {
            if (_state == State.Dead || _shield == null) return;
            if (Time.time - _lastGuardHit < 0.12f) return;
            _lastGuardHit = Time.time;
            float mass = by != null && by.Body != null ? by.Body.mass : 1f;
            float add = Mathf.Max(0f, speed - 1.5f) * Mathf.Sqrt(mass) * 7f;
            _guard += add;
            QhysicsSfx.PlayAt(SfxId.Block, point, Mathf.Clamp01(0.3f + speed / 10f), 0.75f);
            if (by != null && by.HeldByPlayer) CombatHaptics.Pulse(by.PrimaryNode, Mathf.Clamp01(0.25f + speed / 12f), 0.07f);
            if (speed > 4f) SparkBurst.Spawn(point, new Color(1f, 0.9f, 0.7f), 6);
            if (_guard >= GuardMax) GuardBreak(point, by);
            else if (_state == State.Guard && add > 20f) SetState(State.Block, 0.4f);
        }

        void GuardBreak(Vector3 point, PhysicsWeapon by)
        {
            _guard = 0f;
            _comboLeft = 0;
            SetState(State.Stagger, 1.3f);
            Health.incomingMult = 1.25f;
            QhysicsSfx.PlayAt(SfxId.GuardBreak, point, 1f);
            SparkBurst.Spawn(point, new Color(1f, 0.8f, 0.4f), 20);
            if (by != null && by.HeldByPlayer) CombatHaptics.Pulse(by.PrimaryNode, 0.9f, 0.12f);
            if (Random.value < 0.35f && _shield != null)
            {
                var s = _shield;
                _shield = null;
                Vector3 side = -transform.right * 2.5f + Vector3.up * 2f;
                Release(s, "Weapon_WardenShield_Dropped", side);
                QhysicsSfx.PlayAt(SfxId.Disarm, s.transform.position, 0.8f, 0.7f);
                Notice("Guard broken - shield knocked away!");
            }
            else Notice("Guard broken!");
        }
    }
}
