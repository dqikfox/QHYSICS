using System;
using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;
using RealityEngine.Player;
using TMPro;

namespace RealityEngine.Combat
{
    /// <summary>Hit points for enemies/dummies. Also accepts Training Baton hits (IDamageable).</summary>
    [DisallowMultipleComponent]
    public sealed class CombatHealth : MonoBehaviour, ICombatTarget, IDamageable
    {
        public float maxHp = 100f;
        public CombatTeam team = CombatTeam.Enemy;
        public bool resetsWhenIdle;
        public float resetDelay = 3f;
        public bool showNumbers = true;

        public float Hp { get; private set; }
        public float Hp01 => maxHp > 0f ? Mathf.Clamp01(Hp / maxHp) : 0f;
        public float LastHitTime { get; private set; } = -99f;
        public bool IsAlive => Hp > 0f;
        public CombatTeam Team => team;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

        Rigidbody _rb;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        float _flashUntil;
        bool _flashing;

        void Awake()
        {
            Hp = maxHp;
            _rb = GetComponent<Rigidbody>();
        }

        public void ResetHp(float max)
        {
            maxHp = max;
            Hp = max;
        }

        public void RefreshRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>Hit-flash renderers for bodies that are not children of this object (active ragdoll parts).</summary>
        public void SetRenderers(Renderer[] renderers)
        {
            _renderers = renderers;
        }

        public void TakeHit(DamageInfo hit)
        {
            if (Hp <= 0f || hit.amount <= 0f)
                return;
            Hp = Mathf.Max(0f, Hp - hit.amount);
            LastHitTime = Time.time;
            if (showNumbers)
                DamageNumbers.Spawn(hit.point + Vector3.up * 0.1f, hit.amount, hit.type);
            if (_rb != null && !_rb.isKinematic && hit.impulse.sqrMagnitude > 0f)
                _rb.AddForceAtPosition(Vector3.ClampMagnitude(hit.impulse, 60f), hit.point, ForceMode.Impulse);
            Flash();
            Damaged?.Invoke(hit);
            CombatEvents.RaiseHit(this, hit);
            if (Hp <= 0f)
            {
                Died?.Invoke(hit);
                CombatEvents.RaiseKilled(this, hit);
            }
        }

        // Training Baton / legacy damage path.
        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            TakeHit(new DamageInfo
            {
                amount = amount,
                type = DamageType.Blunt,
                point = hitPoint,
                normal = hitNormal,
                impulse = -hitNormal * amount * 0.4f,
                sourceTeam = CombatTeam.Player,
                relativeSpeed = 0f
            });
        }

        void Flash()
        {
            if (_renderers == null)
                RefreshRenderers();
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();
            _flashUntil = Time.time + 0.1f;
            _flashing = true;
            _mpb.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.1f) * 1.5f);
            foreach (var r in _renderers)
                if (r != null && !(r is TrailRenderer) && !(r is ParticleSystemRenderer)) r.SetPropertyBlock(_mpb);
        }

        void Update()
        {
            if (_flashing && Time.time > _flashUntil)
            {
                _flashing = false;
                foreach (var r in _renderers)
                    if (r != null) r.SetPropertyBlock(null);
            }
            if (resetsWhenIdle && Hp < maxHp && Time.time - LastHitTime > resetDelay)
                Hp = maxHp;
        }
    }

    /// <summary>Hit zone on a collider (head x2, limbs x0.7) forwarding to a <see cref="CombatHealth"/>.</summary>
    public sealed class BodyPart : MonoBehaviour, ICombatTarget, IDamageable
    {
        public CombatHealth health;
        public float multiplier = 1f;
        public string zone = "Body";

        public CombatTeam Team => health != null ? health.Team : CombatTeam.Neutral;
        public bool IsAlive => health != null && health.IsAlive;

        public void TakeHit(DamageInfo hit)
        {
            if (health == null) return;
            hit.amount *= multiplier;
            health.TakeHit(hit);
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (health != null) health.ApplyDamage(amount * multiplier, hitPoint, hitNormal);
        }
    }

    /// <summary>Floating world-space damage numbers (pooled TextMeshPro).</summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        static DamageNumbers _inst;
        sealed class Num { public TextMeshPro t; public float born; public Vector3 start; }
        readonly List<Num> _pool = new List<Num>();
        const float Life = 0.85f;

        public static void Spawn(Vector3 pos, float amount, DamageType type)
        {
            if (_inst == null)
            {
                var go = new GameObject("CombatDamageNumbers");
                _inst = go.AddComponent<DamageNumbers>();
            }
            _inst.SpawnInternal(pos, amount, type);
        }

        void SpawnInternal(Vector3 pos, float amount, DamageType type)
        {
            Num n = null;
            foreach (var p in _pool)
                if (!p.t.gameObject.activeSelf) { n = p; break; }
            if (n == null)
            {
                if (_pool.Count >= 24) n = _pool[0];
                else
                {
                    var g = new GameObject("DmgNum");
                    g.transform.SetParent(transform, false);
                    var t = g.AddComponent<TextMeshPro>();
                    t.alignment = TextAlignmentOptions.Center;
                    t.fontSize = 2.2f;
                    t.fontStyle = FontStyles.Bold;
                    t.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
                    n = new Num { t = t };
                    _pool.Add(n);
                }
            }
            Color c = type switch
            {
                DamageType.Slash => new Color(1f, 1f, 1f),
                DamageType.Pierce => new Color(1f, 0.85f, 0.3f),
                DamageType.Fire => new Color(1f, 0.5f, 0.1f),
                DamageType.Lightning => new Color(0.5f, 0.85f, 1f),
                DamageType.Force => new Color(0.7f, 0.6f, 1f),
                _ => new Color(0.85f, 0.9f, 0.95f)
            };
            n.t.text = Mathf.RoundToInt(amount).ToString();
            n.t.color = c;
            n.t.fontSize = Mathf.Lerp(1.6f, 3.4f, Mathf.InverseLerp(5f, 60f, amount));
            n.start = pos + UnityEngine.Random.insideUnitSphere * 0.08f;
            n.born = Time.unscaledTime;
            n.t.transform.position = n.start;
            n.t.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            var cam = CombatInputGate.ViewCamera();
            foreach (var n in _pool)
            {
                if (!n.t.gameObject.activeSelf) continue;
                float a = (Time.unscaledTime - n.born) / Life;
                if (a >= 1f) { n.t.gameObject.SetActive(false); continue; }
                n.t.transform.position = n.start + Vector3.up * (0.45f * a);
                if (cam != null)
                    n.t.transform.rotation = Quaternion.LookRotation(n.t.transform.position - cam.transform.position);
                var col = n.t.color;
                col.a = 1f - a * a;
                n.t.color = col;
            }
        }
    }

    /// <summary>Kinematic trigger capsule that follows the player's head; enemy weapons hit this.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHurtbox : MonoBehaviour
    {
        public static PlayerHurtbox Instance { get; private set; }
        public CapsuleCollider Collider { get; private set; }

        public static PlayerHurtbox Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PlayerHurtbox");
            go.layer = 2; // Ignore Raycast
            Instance = go.AddComponent<PlayerHurtbox>();
            return Instance;
        }

        void Awake()
        {
            Instance = this;
            Collider = gameObject.AddComponent<CapsuleCollider>();
            Collider.isTrigger = true;
            Collider.radius = 0.25f;
            Collider.height = 1.5f;
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void LateUpdate()
        {
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return;
            Vector3 head = cam.transform.position;
            transform.position = head + Vector3.down * 0.65f;
            transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        }

        /// <summary>Apply an enemy hit to <see cref="PlayerVitality"/> with feedback.</summary>
        public void Hurt(float amount, Vector3 point, string source)
        {
            var v = PlayerVitality.Instance;
            if (v == null || v.IsDead) return;
            v.ApplyDamage(amount * SkillSystem.IncomingDamageMult, source);
        }
    }
}
