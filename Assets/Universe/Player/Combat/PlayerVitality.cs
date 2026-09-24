using System;
using UnityEngine;
using RealityEngine.XR;

namespace RealityEngine.Player
{
    /// <summary>
    /// Operator HP + soft shield for the Training System. Death → plaza respawn (no softlock).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(127)]
    public sealed class PlayerVitality : MonoBehaviour
    {
        public const string RootName = "PlayerVitality";

        public static PlayerVitality Instance { get; private set; }

        [SerializeField] float maxHp = 100f;
        [SerializeField] float hp = 100f;
        [SerializeField] float regenPerSecond = 2.5f;
        [SerializeField] float regenDelay = 4f;

        float _shieldUntil;
        float _lastHurtTime = -999f;
        float _flashUntil;
        bool _dead;

        public float MaxHp => maxHp;
        public float Hp => hp;
        public float Hp01 => maxHp > 0.01f ? Mathf.Clamp01(hp / maxHp) : 0f;
        public bool HasShield => Time.time < _shieldUntil;
        public bool IsDead => _dead;

        public event Action Changed;

        public static PlayerVitality Ensure(Transform parent = null)
        {
            if (Instance != null)
                return Instance;
            var found = Object.FindFirstObjectByType<PlayerVitality>(FindObjectsInactive.Include);
            if (found != null)
            {
                Instance = found;
                return found;
            }
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<PlayerVitality>();
        }

        void OnEnable() => Instance = this;
        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ConfigureMaxHp(float max, bool refill)
        {
            maxHp = Mathf.Max(10f, max);
            if (refill)
                hp = maxHp;
            else
                hp = Mathf.Min(hp, maxHp);
            _dead = false;
            Changed?.Invoke();
        }

        public void Heal(float amount)
        {
            if (_dead || amount <= 0f)
                return;
            hp = Mathf.Min(maxHp, hp + amount);
            Changed?.Invoke();
        }

        public void ApplyShield(float seconds)
        {
            _shieldUntil = Mathf.Max(_shieldUntil, Time.time + Mathf.Max(0.1f, seconds));
            Changed?.Invoke();
        }

        public void ApplyDamage(float amount, string source = null)
        {
            if (_dead || amount <= 0f)
                return;
            if (HasShield)
                amount *= 0.35f;
            hp = Mathf.Max(0f, hp - amount);
            _lastHurtTime = Time.time;
            _flashUntil = Time.time + 0.18f;
            Changed?.Invoke();
            if (hp <= 0.01f)
                DieAndRespawn(source);
        }

        void Update()
        {
            if (_dead)
                return;
            if (hp < maxHp && Time.time - _lastHurtTime >= regenDelay)
            {
                hp = Mathf.Min(maxHp, hp + regenPerSecond * Time.deltaTime);
                Changed?.Invoke();
            }
        }

        void DieAndRespawn(string source)
        {
            _dead = true;
            Debug.Log("Training System: operator down" + (string.IsNullOrEmpty(source) ? "" : " (" + source + ")") + " — respawning at plaza.");
            // Soft reset — not a game-over softlock.
            LabPlayerSpawn.EnsureApplied();
            hp = maxHp;
            _shieldUntil = 0f;
            _dead = false;
            _lastHurtTime = Time.time;
            Changed?.Invoke();
        }

        public bool IsFlashing => Time.time < _flashUntil;
    }
}
