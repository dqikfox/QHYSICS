using System.Collections.Generic;
using UnityEngine;
using UiButton = UnityEngine.UI.Button;
using RealityEngine.Audio;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Combat sound layer (procedural clips from <see cref="QhysicsSfx"/>, no imported audio):
    /// - flesh thud on every player melee hit on an enemy, louder/lower the faster the swing; bone crunch above 7 m/s;
    /// - crunch + death sting on kills;
    /// - swing swooshes on held player weapons when the tip crosses 4 m/s (heavy swoosh for mace/spear), pitch by speed;
    /// - a quiet click on every uGUI Button press (menus, toolbelt, skills, settings), hooked at runtime.
    /// Runtime only, nothing saved.
    /// </summary>
    [DefaultExecutionOrder(160)]
    public sealed class CombatSfxDirector : MonoBehaviour
    {
        static CombatSfxDirector _instance;
        const float SwooshSpeed = 4f;
        const float SwooshGap = 0.3f;

        readonly Dictionary<PhysicsWeapon, float> _lastTipSpeed = new Dictionary<PhysicsWeapon, float>();
        readonly Dictionary<PhysicsWeapon, float> _lastSwoosh = new Dictionary<PhysicsWeapon, float>();
        readonly Dictionary<PhysicsWeapon, Vector3> _lastTip = new Dictionary<PhysicsWeapon, Vector3>();
        readonly HashSet<UiButton> _hookedButtons = new HashSet<UiButton>();
        float _nextButtonScan;
        static float _lastClick;

        public static CombatSfxDirector Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("QhysicsCombatSfx");
            _instance = go.AddComponent<CombatSfxDirector>();
            return _instance;
        }

        void Awake()
        {
            _instance = this;
            CombatEvents.Hit += OnHit;
            CombatEvents.Killed += OnKilled;
        }

        void OnDestroy()
        {
            CombatEvents.Hit -= OnHit;
            CombatEvents.Killed -= OnKilled;
        }

        static void OnHit(ICombatTarget t, DamageInfo d)
        {
            if (t == null || t.Team != CombatTeam.Enemy || d.sourceTeam != CombatTeam.Player) return;
            if (d.type != DamageType.Blunt && d.type != DamageType.Slash && d.type != DamageType.Pierce && d.type != DamageType.Force) return;
            float s = Mathf.Max(0f, d.relativeSpeed);
            float k = Mathf.Clamp01(s / 12f);
            float vol = d.type == DamageType.Force ? 0.6f : Mathf.Clamp01(0.3f + 0.7f * k);
            float pitch = Mathf.Lerp(1.15f, 0.8f, k);
            QhysicsSfx.PlayAt(SfxId.FleshHit, d.point, vol, pitch);
            if (s > 7f) QhysicsSfx.PlayAt(SfxId.BoneCrunch, d.point, Mathf.Clamp01(0.4f + (s - 7f) / 8f), Mathf.Lerp(1.1f, 0.85f, k));
        }

        static void OnKilled(ICombatTarget t, DamageInfo d)
        {
            if (t == null || t.Team != CombatTeam.Enemy) return;
            QhysicsSfx.PlayAt(SfxId.BoneCrunch, d.point, 0.9f, 0.75f);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            for (int i = 0; i < PhysicsWeapon.All.Count; i++)
            {
                var w = PhysicsWeapon.All[i];
                if (w == null || !w.IsHeld || !w.HeldByPlayer || w.IsShield) { if (w != null) _lastTip.Remove(w); continue; }
                Vector3 tip = w.TipWorld;
                if (!_lastTip.TryGetValue(w, out Vector3 prev)) { _lastTip[w] = tip; continue; }
                _lastTip[w] = tip;
                float speed = Time.deltaTime > 0f ? (tip - prev).magnitude / dt : 0f;
                _lastTipSpeed.TryGetValue(w, out float was);
                _lastTipSpeed[w] = speed;
                if (speed < SwooshSpeed || was >= SwooshSpeed) continue;
                if (_lastSwoosh.TryGetValue(w, out float last) && now - last < SwooshGap) continue;
                _lastSwoosh[w] = now;
                bool heavy = w.kind == WeaponKind.Mace || w.kind == WeaponKind.Spear || w.kind == WeaponKind.Axe || w.kind == WeaponKind.Staff;
                float k = Mathf.Clamp01((speed - SwooshSpeed) / 8f);
                QhysicsSfx.PlayAt(heavy ? SfxId.SwooshHeavy : SfxId.Whoosh, tip, 0.25f + 0.5f * k, Mathf.Lerp(0.9f, 1.3f, k));
            }

            if (now >= _nextButtonScan)
            {
                _nextButtonScan = now + 2f;
                var buttons = FindObjectsByType<UiButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var b in buttons)
                {
                    if (b == null || !_hookedButtons.Add(b)) continue;
                    b.onClick.AddListener(UiClick);
                }
            }
        }

        static void UiClick()
        {
            if (Time.unscaledTime - _lastClick < 0.05f) return;
            _lastClick = Time.unscaledTime;
            QhysicsSfx.Play2D(SfxId.UiClick, 0.3f);
        }
    }
}
