using System;
using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;
using RealityEngine.Crystals;
using CC = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Combat
{
    public sealed partial class CombatEnemy
    {
        /// <summary>Piezo shock startle: breaks a windup (and, when allowed, a swing) into a short stagger. True when it interrupted.</summary>
        public bool PiezoStartle(float seconds, bool alsoSwing)
        {
            if (_state == State.Windup || (alsoSwing && _state == State.Swing && _attack != Attack.Cast))
            {
                _comboLeft = 0;
                SetState(State.Stagger, seconds);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// PIEZO STRIKE (skill tree MAGIC, 3 ranks): a PZT-5A igniter element in the striking face of your blunt gear (mace head,
    /// staff ends, shield face, pommels). A blunt hit squeezes it: impact force F = m v / tau (tau = 15 ms contact on a
    /// padded target), capped at the element's stress limit (sigma_max x area). Open-circuit voltage V = d33 F / C,
    /// C = eps0 epsR A / t; charge Q = n d33 F (n = rank = elements wired in parallel); energy W = Q V / 2.
    /// V above 3 kV (air breakdown over the 1 mm striker gap) arcs into the target: bonus Lightning damage scaled by V,
    /// and a windup in progress is startled into a stagger (rank 3: swings too).
    /// Numbers (model element, 3 mm dia x 6 mm PZT-5A): C = 17.7 pF, 21 V/N, cap 353 N -> 7.5 kV, 0.13 uC, ~0.5 mJ per element.
    /// Honesty: ~0.5 mJ is a static-shock sting, not an injury - the bonus damage and startle are gameplay; the physics
    /// (volts, charge, millijoules, arc length) shown in the toast is real for that element.
    /// </summary>
    [DefaultExecutionOrder(213)]
    public sealed class PiezoStrike : MonoBehaviour
    {
        public const string SkillId = "piezo_strike";
        /// <summary>Contact time of a weapon on a padded / armoured body (order 10-20 ms).</summary>
        public const double ContactTau = 0.015;
        public const double GapMm = 1.0;
        const float PerWeaponCooldown = 0.45f;

        static PiezoStrike _instance;
        public static double LastVolts { get; private set; }

        struct Pending { public CombatHealth h; public CombatEnemy e; public float dmg; public Vector3 p; public bool startle; public float startleSec; }

        readonly List<Pending> _queue = new List<Pending>();
        readonly Dictionary<PhysicsWeapon, float> _ready = new Dictionary<PhysicsWeapon, float>();
        float _nextToast, _nextStudScan;
        bool _logged;

        public static double ElementArea => Math.PI * 0.25 * CC.PztIgniterDia * CC.PztIgniterDia;
        public static double ElementCap => CC.Eps0 * CC.PztEpsR * ElementArea / CC.PztIgniterLen;
        public static double MaxForce => CC.PztMaxStress * ElementArea;
        public static double VoltsPerNewton => CC.PztD33 / ElementCap;
        public static double MaxVolts => VoltsPerNewton * MaxForce;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var go = new GameObject("QhysicsPiezoStrike");
            _instance = go.AddComponent<PiezoStrike>();
        }

        void Awake()
        {
            _instance = this;
            CombatEvents.Hit += OnHit;
        }

        void OnDestroy()
        {
            CombatEvents.Hit -= OnHit;
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Force (N), volts, charge (C) and energy (J) for a blunt impact of a weapon of mass m (kg) at v (m/s).</summary>
        public static void Evaluate(float mass, float speed, int elements, out double force, out double volts, out double charge, out double energy)
        {
            force = Math.Min(mass * speed / ContactTau, MaxForce);
            volts = VoltsPerNewton * force;
            charge = Math.Max(1, elements) * CC.PztD33 * force;
            energy = 0.5 * charge * volts;
        }

        void OnHit(ICombatTarget t, DamageInfo d)
        {
            if (d.sourceTeam != CombatTeam.Player || d.type != DamageType.Blunt || d.source == null)
                return;
            int rank = SkillSystem.Rank(SkillId);
            if (rank <= 0)
                return;
            var pw = d.source.GetComponent<PhysicsWeapon>();
            if (pw == null || !pw.HeldByPlayer || pw.Body == null)
                return;
            if (_ready.TryGetValue(pw, out float until) && Time.time < until)
                return;
            _ready[pw] = Time.time + PerWeaponCooldown;

            Evaluate(pw.Body.mass, d.relativeSpeed, rank, out double force, out double volts, out double charge, out double energy);
            LastVolts = volts;
            double needed = CC.AirBreakdown * GapMm * 1e-3;
            if (volts < needed)
            {
                QhysicsSfx.PlayAt(SfxId.CrystalClick, d.point, 0.25f, 1.4f);
                return;
            }

            // Arc from the striking face into the target.
            Vector3 face = d.point - d.normal * 0.02f + UnityEngine.Random.insideUnitSphere * 0.015f;
            CrystalFx.Spark(face, d.point, new Color(0.7f, 0.85f, 1f));
            LightningVfx.Bolt(face + Vector3.up * 0.03f, d.point, new Color(0.6f, 0.85f, 1f, 0.9f), 0.12f);
            QhysicsSfx.PlayAt(SfxId.SparkSnap, d.point, Mathf.Clamp01(0.5f + (float)(volts / MaxVolts) * 0.5f));
            CombatHaptics.Pulse(pw.PrimaryNode, 0.55f, 0.04f);

            CombatHealth h = t as CombatHealth;
            if (h == null && t is BodyPart bp)
                h = bp.health;
            if (h != null && h.team == CombatTeam.Enemy && h.IsAlive)
            {
                float scale = (float)(volts / MaxVolts);
                var e = h.GetComponent<CombatEnemy>();
                if (e == null) e = h.GetComponentInParent<CombatEnemy>();
                _queue.Add(new Pending
                {
                    h = h,
                    e = e,
                    dmg = (1.5f + 1.5f * rank) * scale * SkillSystem.SpellPowerMult,
                    p = d.point,
                    startle = scale > 0.55f,
                    startleSec = 0.3f + 0.1f * rank
                });
            }

            double arcMm = volts / CC.AirBreakdown * 1e3;
            if (Time.unscaledTime >= _nextToast)
            {
                _nextToast = Time.unscaledTime + 2.5f;
                CombatHud.Toast("PIEZO " + CrystalFx.FormatVolts(volts) + "  " + CrystalFx.FormatCharge(charge) + "  " + (energy * 1e3).ToString("0.00") + " mJ  arc " + arcMm.ToString("0.0") + " mm");
            }
            if (!_logged)
            {
                _logged = true;
                Debug.Log("PiezoStrike: first spark F " + force.ToString("0") + " N -> " + volts.ToString("0") + " V, Q " + (charge * 1e9).ToString("0") + " nC, W "
                    + (energy * 1e3).ToString("0.00") + " mJ (element C " + (ElementCap * 1e12).ToString("0.0") + " pF, " + VoltsPerNewton.ToString("0.0") + " V/N, cap "
                    + MaxForce.ToString("0") + " N).");
            }
        }

        void Update()
        {
            // Apply arc damage a frame later, outside CombatHealth.TakeHit, so a kill is never reported twice.
            if (_queue.Count > 0)
            {
                int rank = SkillSystem.Rank(SkillId);
                for (int i = 0; i < _queue.Count; i++)
                {
                    Pending q = _queue[i];
                    if (q.h == null || !q.h.IsAlive)
                        continue;
                    if (q.startle && q.e != null && !q.e.IsDead && q.e.PiezoStartle(q.startleSec, rank >= 3) && Time.unscaledTime >= _nextToast - 1.5f)
                        CombatHud.Toast("Piezo shock - attack broken!");
                    q.h.TakeHit(new DamageInfo
                    {
                        amount = Mathf.Max(0.5f, q.dmg),
                        type = DamageType.Lightning,
                        point = q.p,
                        sourceTeam = CombatTeam.Player,
                        source = gameObject
                    });
                }
                _queue.Clear();
            }

            if (Time.unscaledTime >= _nextStudScan)
            {
                _nextStudScan = Time.unscaledTime + 1f;
                if (SkillSystem.Rank(SkillId) > 0)
                    FitStuds();
            }
        }

        /// <summary>Small cyan PZT studs on maces and staffs you can use (visual cue that Piezo Strike is armed).</summary>
        static void FitStuds()
        {
            var list = PhysicsWeapon.All;
            for (int i = 0; i < list.Count; i++)
            {
                PhysicsWeapon w = list[i];
                if (w == null || w.ownerTeam == CombatTeam.Enemy || w.enemyOwner != null)
                    continue;
                if (w.kind != WeaponKind.Mace && w.kind != WeaponKind.Staff)
                    continue;
                if (w.transform.Find("PiezoStud") != null)
                    continue;
                var stud = CrystalFx.MeshObject("PiezoStud", w.transform, CrystalGadgets.HexPoint,
                    CrystalFx.Crystal(new Color(0.6f, 0.95f, 1f, 0.7f), new Color(0f, 0.6f, 0.9f)),
                    new Vector3(0f, w.tipY + 0.005f, 0f), Quaternion.identity, new Vector3(0.022f, 0.035f, 0.022f));
                stud.layer = w.gameObject.layer;
            }
        }
    }
}
