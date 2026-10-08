using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Caster projectile: a slow (6.5 m/s) violet orb that homes on the player's head for its first 1.2 s.
    /// Hits the player through <see cref="PlayerHurtbox"/> (or the player's body). Bat it with any held weapon or
    /// shield to deflect it: it turns cyan, flies back at 15 m/s toward the caster (or the nearest enemy in a 40 deg
    /// cone), deals 1.8x damage and knocks the target down. Passes through other enemies and their gear until deflected.
    /// Kinematic trigger moved in FixedUpdate; emissive + trail only (no real-time light) for Quest.
    /// </summary>
    public sealed class EnemyOrb : MonoBehaviour
    {
        public static readonly List<EnemyOrb> All = new List<EnemyOrb>();
        const float Speed = 6.5f;
        const float DeflectSpeed = 15f;

        static Material _enemyMat, _playerMat, _enemyTrail, _playerTrail;

        CombatEnemy _owner;
        CombatEnemy _homeTarget;
        float _dmg;
        Vector3 _vel;
        float _born;
        float _homingUntil;
        bool _deflected;
        bool _done;
        Rigidbody _rb;
        Renderer _r;
        TrailRenderer _trail;

        public static Material EnemyMaterial()
        {
            if (_enemyMat == null) _enemyMat = CombatMaterials.Get(new Color(0.35f, 0.1f, 0.55f), 0f, 0.6f, new Color(0.8f, 0.25f, 1.4f) * 1.6f);
            return _enemyMat;
        }

        static Material PlayerMaterial()
        {
            if (_playerMat == null) _playerMat = CombatMaterials.Get(new Color(0.05f, 0.3f, 0.35f), 0f, 0.6f, new Color(0f, 1.2f, 1.5f) * 1.6f);
            return _playerMat;
        }

        public static EnemyOrb Launch(Vector3 from, Vector3 target, CombatEnemy owner, float damage)
        {
            var go = CombatUtil.Prim(PrimitiveType.Sphere, "EnemyOrb", null, from, Vector3.one * 0.18f, EnemyMaterial(), true);
            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (_enemyTrail == null) _enemyTrail = CombatMaterials.Transparent(new Color(0.75f, 0.3f, 1f, 0.5f), new Color(0.8f, 0.25f, 1.4f));
            if (_playerTrail == null) _playerTrail = CombatMaterials.Transparent(new Color(0f, 0.9f, 1f, 0.5f), new Color(0f, 1.2f, 1.5f));
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.25f;
            tr.minVertexDistance = 0.03f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(1f, 0f));
            tr.sharedMaterial = _enemyTrail;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var o = go.AddComponent<EnemyOrb>();
            o._owner = owner;
            o._dmg = damage;
            Vector3 d = target - from;
            o._vel = (d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward) * Speed;
            o._born = Time.time;
            o._homingUntil = Time.time + 1.2f;
            o._rb = rb;
            o._r = r;
            o._trail = tr;
            All.Add(o);
            return o;
        }

        void OnDestroy() { All.Remove(this); }

        void FixedUpdate()
        {
            if (_done || _rb == null) return;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = _rb.position;
            if (!_deflected && Time.time < _homingUntil)
            {
                var cam = CombatInputGate.ViewCamera();
                if (cam != null)
                {
                    Vector3 want = (cam.transform.position + Vector3.down * 0.3f - pos).normalized * _vel.magnitude;
                    _vel = Vector3.RotateTowards(_vel, want, 40f * Mathf.Deg2Rad * dt, 0f);
                }
            }
            else if (_deflected && _homeTarget != null && !_homeTarget.IsDead)
            {
                Vector3 want = (_homeTarget.AimPoint - pos).normalized * _vel.magnitude;
                _vel = Vector3.RotateTowards(_vel, want, 150f * Mathf.Deg2Rad * dt, 0f);
            }
            _rb.MovePosition(pos + _vel * dt);
            if (Time.time - _born > 6f) Burst(pos);
        }

        void OnTriggerEnter(Collider c)
        {
            if (_done || c == null) return;
            if (c.isTrigger)
            {
                if (!_deflected && c.GetComponent<PlayerHurtbox>() != null) HitPlayer();
                return;
            }
            var w = c.GetComponentInParent<PhysicsWeapon>();
            if (w != null)
            {
                if (w.enemyOwner != null || w.ownerTeam == CombatTeam.Enemy) return; // enemy gear: pass through
                if (!_deflected && (w.HeldByPlayer || w.ActiveTeam == CombatTeam.Player)) { Deflect(w); return; }
                if (_deflected) return;
                Burst(transform.position);
                return;
            }
            if (_owner != null && _owner.OwnsCollider(c) && !_deflected) return;
            if (CombatInputGate.IsPlayerCollider(c))
            {
                if (!_deflected) HitPlayer();
                return;
            }
            var t = c.GetComponentInParent<ICombatTarget>();
            if (t != null)
            {
                if (!_deflected && t.Team == CombatTeam.Enemy) return; // passes through its allies (and the training dummies)
                if (t.IsAlive && (_deflected || t.Team == CombatTeam.Neutral))
                {
                    Vector3 p = c.ClosestPoint(transform.position);
                    t.TakeHit(new DamageInfo
                    {
                        amount = _dmg, type = DamageType.Force, point = p, normal = -_vel.normalized,
                        impulse = _vel.normalized * 8f, sourceTeam = _deflected ? CombatTeam.Player : CombatTeam.Enemy, source = gameObject
                    });
                    if (_deflected && t is BodyPart bp && bp.health != null)
                    {
                        var e = bp.health.GetComponent<CombatEnemy>();
                        if (e != null) e.OnHeavyImpact(1.3f);
                    }
                }
                Burst(transform.position);
                return;
            }
            Burst(transform.position);
        }

        void HitPlayer()
        {
            var hb = PlayerHurtbox.Instance;
            if (hb != null) hb.Hurt(_dmg, transform.position, "Caster orb");
            Burst(transform.position);
        }

        void Deflect(PhysicsWeapon w)
        {
            _deflected = true;
            if (_r != null) _r.sharedMaterial = PlayerMaterial();
            if (_trail != null) _trail.sharedMaterial = _playerTrail;
            Vector3 pos = transform.position;
            Vector3 baseDir = -_vel.normalized;
            if (w.Body != null)
            {
                Vector3 wv = w.Body.GetPointVelocity(pos);
                if (wv.sqrMagnitude > 4f) baseDir = (baseDir + wv.normalized).normalized;
            }
            CombatEnemy target = null;
            if (_owner != null && !_owner.IsDead && Vector3.Angle(baseDir, _owner.AimPoint - pos) < 60f)
                target = _owner;
            else
            {
                float best = 40f;
                foreach (var e in CombatEnemy.All)
                {
                    if (e == null || e.IsDead) continue;
                    float a = Vector3.Angle(baseDir, e.AimPoint - pos);
                    if (a < best) { best = a; target = e; }
                }
            }
            _homeTarget = target;
            Vector3 dir = target != null ? (target.AimPoint - pos).normalized : baseDir;
            _vel = dir * DeflectSpeed;
            _dmg *= 1.8f;
            _born = Time.time;
            QhysicsSfx.PlayAt(SfxId.OrbDeflect, pos, 0.9f);
            SparkBurst.Spawn(pos, new Color(0.4f, 1f, 1f), 18);
            if (w.HeldByPlayer) CombatHaptics.Pulse(w.PrimaryNode, 0.7f, 0.08f);
            CombatEvents.RaiseParried(pos);
        }

        void Burst(Vector3 p)
        {
            if (_done) return;
            _done = true;
            QhysicsSfx.PlayAt(SfxId.OrbBurst, p, 0.7f);
            SparkBurst.Spawn(p, _deflected ? new Color(0.3f, 1f, 1f) : new Color(0.8f, 0.4f, 1f), 16);
            Destroy(gameObject);
        }
    }
}
