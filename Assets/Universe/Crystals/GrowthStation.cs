using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Audio;
using RealityEngine.Player;
using C = RealityEngine.Crystals.CrystalConstants;

namespace RealityEngine.Crystals
{
    /// <summary>A seed crystal on a rod (station rod or toolbelt CRYSTAL > Seed Crystal). Dip the tip into supersaturated solution.</summary>
    [DisallowMultipleComponent]
    public sealed class SeedCrystal : MonoBehaviour, ICrystalInspectable
    {
        public const string RootName = "Gadget_SeedCrystal";
        public static readonly List<SeedCrystal> All = new List<SeedCrystal>();
        public Transform Tip;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static GameObject Create(Vector3 pos, string name)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            CrystalFx.Prim(PrimitiveType.Cylinder, "Rod", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.008f, 0.08f, 0.008f), CrystalFx.Crystal(new Color(0.85f, 0.95f, 1f, 0.45f)), false);
            var tip = CrystalFx.Prim(PrimitiveType.Cube, "SeedTip", root.transform, new Vector3(0f, -0.025f, 0f), new Vector3(0.012f, 0.02f, 0.012f), CrystalFx.Solid(new Color(0.97f, 0.97f, 0.95f), 0f, 0.3f), false);
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 20f);
            root.AddComponent<CapsuleCollider>().height = 0.2f;
            root.GetComponent<CapsuleCollider>().radius = 0.008f;
            var s = root.AddComponent<SeedCrystal>();
            s.Tip = tip.transform;
            CrystalFx.MakeGrabbable(root, 0.02f);
            return root;
        }

        public string InspectStats(bool held) => "Seed: sodium acetate trihydrate crystal";
        public string InspectDetail() => "A template: molecules in the liquid lock onto its lattice";
        public string InspectHint(bool held) => held ? "Dip the white tip into the clear 'hot ice' solution" : "Grab the rod";
    }

    /// <summary>
    /// Station 6: "hot ice". Sodium acetate trihydrate melts at 58 C; dissolved in its own water and cooled
    /// carefully it stays liquid at room temperature (supersaturated / supercooled). A seed crystal gives the
    /// molecules a template: a crystallisation front races out, and the latent heat warms the beaker toward 58 C.
    /// Physics: front speed v = k dT_sub (k = 0.3 mm/s per K, tuned to demo videos); adiabatic fraction
    /// x = c dT / L (c = 2.8 kJ/kgK, L = 264 kJ/kg); temperature rises to just below the melting point, then
    /// cools by Newton's law (time-lapsed x20). Honesty: front is a sphere from the contact point; real growth is
    /// dendritic and the warm crystal mush slows it.
    /// </summary>
    public sealed class GrowthStation : CrystalStation
    {
        public override string Id => "growth";
        public override string Title => "Crystal Growth";
        public override string ChallengeId => "crystal_hotice";
        protected override CrystalUnitCell.Mode CellMode => CrystalUnitCell.Mode.HotIce;
        protected override string CellTitle => "liquid -> ordered lattice";

        const float BeakerR = 0.055f, LiquidH = 0.09f;
        const float Ambient = 22f, CoolTimeLapse = 20f, CoolTau = 600f;
        const double KFront = 0.3e-3; // m/s per K
        Transform _beaker, _liquid, _seedHome;
        Renderer _liquidR;
        Material _clear, _cloudy;
        readonly List<Transform> _needles = new List<Transform>();
        readonly List<float> _needleDist = new List<float>();
        readonly List<Vector3> _needlePos = new List<Vector3>();
        Vector3 _seedLocal;
        float _front = -1f;
        float _temp = Ambient;
        float _done;
        float _tPeak = Ambient;
        bool _melting;
        TextMeshPro _thermo;
        Transform _mercury;
        float _tick, _crackle;
        GameObject _rod;

        bool Crystallising => _front >= 0f && _done < 1f;
        float SubCool => Mathf.Max(0f, (float)C.NaOAcMeltC - _temp);

        protected override void BuildStation()
        {
            Material dark = CrystalFx.Solid(new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.5f);
            Material glass = CrystalFx.Crystal(new Color(0.85f, 0.95f, 1f, 0.15f));
            _clear = CrystalFx.Crystal(new Color(0.9f, 0.95f, 1f, 0.22f), new Color(0.02f, 0.03f, 0.04f));
            _cloudy = CrystalFx.Crystal(new Color(0.96f, 0.97f, 0.98f, 0.7f), new Color(0.06f, 0.06f, 0.06f));
            Material needle = CrystalFx.Solid(new Color(0.97f, 0.98f, 1f), 0f, 0.7f, new Color(0.1f, 0.12f, 0.14f));

            _beaker = new GameObject("Beaker").transform;
            _beaker.SetParent(transform, false);
            _beaker.localPosition = OnTop(-0.2f, 0f, 0.05f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Glass", _beaker, new Vector3(0f, 0.065f, 0f), new Vector3(BeakerR * 2.15f, 0.065f, BeakerR * 2.15f), glass, false);
            var liq = CrystalFx.Prim(PrimitiveType.Cylinder, "Solution", _beaker, new Vector3(0f, LiquidH * 0.5f + 0.003f, 0f), new Vector3(BeakerR * 2f, LiquidH * 0.5f, BeakerR * 2f), _clear, false);
            _liquid = liq.transform;
            _liquidR = liq.GetComponent<Renderer>();
            CrystalFx.Prim(PrimitiveType.Cylinder, "Base", _beaker, new Vector3(0f, 0.0015f, 0f), new Vector3(BeakerR * 2.2f, 0.0015f, BeakerR * 2.2f), dark);
            var bc = _beaker.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.065f, 0f);
            bc.size = new Vector3(BeakerR * 2.1f, 0.13f, BeakerR * 2.1f);
            _beaker.gameObject.AddComponent<XRSimpleInteractable>();
            _beaker.gameObject.AddComponent<HotIceInspect>().Station = this;

            var rnd = new System.Random(11);
            for (int i = 0; i < 120; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rnd.NextDouble()) * (BeakerR - 0.006f);
                var p = new Vector3(Mathf.Cos(a) * r, 0.008f + (float)rnd.NextDouble() * (LiquidH - 0.012f), Mathf.Sin(a) * r);
                var n = CrystalFx.Prim(PrimitiveType.Cube, "Needle", _beaker, p, new Vector3(0.0018f, 0.02f + (float)rnd.NextDouble() * 0.018f, 0.0018f), needle, false);
                n.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 180f, (float)rnd.NextDouble() * 180f, (float)rnd.NextDouble() * 180f);
                n.SetActive(false);
                _needles.Add(n.transform);
                _needlePos.Add(p);
                _needleDist.Add(0f);
            }

            // Thermometer in the beaker.
            CrystalFx.Prim(PrimitiveType.Cylinder, "ThermoTube", _beaker, new Vector3(0.035f, 0.11f, 0.02f), new Vector3(0.006f, 0.1f, 0.006f), glass, false);
            _mercury = CrystalFx.Prim(PrimitiveType.Cube, "ThermoRed", _beaker, new Vector3(0.035f, 0.05f, 0.02f), new Vector3(0.003f, 0.03f, 0.003f), CrystalFx.Emissive(new Color(1f, 0.15f, 0.1f), 1.2f), false).transform;

            _thermo = CrystalFx.Text(transform, "Thermo", "", 0.17f, new Color(0.55f, 1f, 0.85f), OnTop(0.32f, 0.14f, 0.05f), new Vector2(0.42f, 0.24f), TextAlignmentOptions.TopLeft);
            CrystalFx.Prim(PrimitiveType.Cube, "ThermoPanel", transform, OnTop(0.32f, 0.13f, 0.075f), new Vector3(0.44f, 0.26f, 0.04f), dark);

            // Seed rod on a rack.
            CrystalFx.Prim(PrimitiveType.Cube, "Rack", transform, OnTop(-0.55f, 0.06f, 0.05f), new Vector3(0.08f, 0.12f, 0.05f), dark);
            _seedHome = new GameObject("SeedHome").transform;
            _seedHome.SetParent(transform, false);
            _seedHome.localPosition = OnTop(-0.55f, 0.16f, 0.05f);
            SpawnRod();
            CrystalFx.Text(transform, "SeedLabel", "seed rod", 0.13f, new Color(0.6f, 0.9f, 1f), OnTop(-0.55f, 0.03f, 0.02f), new Vector2(0.16f, 0.03f));
        }

        void SpawnRod()
        {
            if (_rod != null)
                return;
            _rod = SeedCrystal.Create(_seedHome.position, "Crystal_SeedRod");
            CrystalHomeReturn.Attach(_rod, transform);
        }

        public float Temperature => _temp;
        public float Fraction => _done;
        /// <summary>Mass fraction solid: the fast (adiabatic) part x = c dT / L, then the rest as the heat leaks away.</summary>
        public float MassSolid
        {
            get
            {
                float xAd = Mathf.Clamp01((float)(C.NaOAcCp * Mathf.Max(0f, (float)C.NaOAcMeltC - Ambient) / C.NaOAcLatent));
                if (_front < 0f && _done <= 0f) return 0f;
                if (_done < 1f) return _done * xAd;
                return xAd + (1f - xAd) * Mathf.Clamp01((_tPeak - _temp) / Mathf.Max(1f, _tPeak - Ambient));
            }
        }

        public void Seed(Vector3 worldPoint)
        {
            if (_front >= 0f || _melting || _temp >= C.NaOAcMeltC - 1f)
                return;
            _seedLocal = _beaker.InverseTransformPoint(worldPoint);
            _seedLocal.y = Mathf.Clamp(_seedLocal.y, 0.005f, LiquidH);
            float maxD = 0f;
            for (int i = 0; i < _needlePos.Count; i++)
            {
                float d = Vector3.Distance(_needlePos[i], _seedLocal);
                _needleDist[i] = d;
                maxD = Mathf.Max(maxD, d);
            }
            _front = 0f;
            _done = 0f;
            QhysicsSfx.PlayAt(SfxId.FreezeFront, worldPoint, 0.5f);
            CrystalFx.Flash(worldPoint, new Color(0.8f, 0.95f, 1f), 0.4f, 0.15f, 0.01f);
        }

        float MaxDist
        {
            get
            {
                float m = 0.001f;
                for (int i = 0; i < _needleDist.Count; i++) m = Mathf.Max(m, _needleDist[i]);
                return Mathf.Max(m, 0.02f);
            }
        }

        void Remelt()
        {
            _melting = true;
            _front = -1f;
            _tPeak = Ambient;
            QhysicsSfx.PlayAt(SfxId.FireCast, _beaker.position, 0.3f, 0.8f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // Seeding by a seed tip inside the liquid cylinder.
            if (_front < 0f && !_melting)
            {
                for (int i = 0; i < SeedCrystal.All.Count; i++)
                {
                    var s = SeedCrystal.All[i];
                    if (s == null || s.Tip == null) continue;
                    Vector3 lp = _beaker.InverseTransformPoint(s.Tip.position);
                    if (lp.y > 0f && lp.y < LiquidH + 0.003f && new Vector2(lp.x, lp.z).magnitude < BeakerR)
                    {
                        Seed(s.Tip.position);
                        break;
                    }
                }
            }

            if (_melting)
            {
                _temp = Mathf.MoveTowards(_temp, 70f, dt * 12f);
                _done = Mathf.MoveTowards(_done, 0f, dt * 0.5f);
                if (_temp >= 69.9f && _done <= 0f)
                    _melting = false;
            }
            else if (_front >= 0f && _done < 1f)
            {
                float v = (float)(KFront * Mathf.Max(2f, SubCool));
                _front += v * dt;
                float frac = Mathf.Clamp01(_front / MaxDist);
                // Volume fraction of a sphere front inside the beaker ~ frac^2 (bounded geometry, approx).
                _done = Mathf.Max(_done, frac);
                // Latent heat: temperature approaches (but stays below) the 58 C melting point.
                float target = Ambient + ((float)C.NaOAcMeltC - Ambient) * 0.9f;
                _temp = Mathf.Max(_temp, Mathf.Lerp(Ambient, target, frac));
                _tPeak = _temp;
                _crackle += dt;
                if (_crackle > 0.35f)
                {
                    _crackle = 0f;
                    QhysicsSfx.PlayAt(SfxId.TriboCrackle, _beaker.position, 0.12f, 1.6f);
                }
                if (_done >= 1f)
                {
                    QhysicsSfx.PlayAt(SfxId.CrystalRing, _beaker.position, 0.35f, 0.8f);
                    if (_temp > 50f)
                        CrystalGoals.Latch("hot_ice");
                }
            }
            else
            {
                // Newton cooling toward ambient (time-lapsed so you can watch it).
                _temp += (Ambient - _temp) * dt * CoolTimeLapse / CoolTau;
            }

            for (int i = 0; i < _needles.Count; i++)
            {
                bool on = _front >= 0f ? _needleDist[i] <= _front : (_done > 0f && i < _done * _needles.Count);
                if (_needles[i].gameObject.activeSelf != on)
                    _needles[i].gameObject.SetActive(on);
            }
            if (_liquidR != null)
            {
                Material want = _done > 0.6f ? _cloudy : _clear;
                if (_liquidR.sharedMaterial != want) _liquidR.sharedMaterial = want;
            }
            if (_mercury != null)
            {
                float h = Mathf.Lerp(0.01f, 0.17f, Mathf.InverseLerp(0f, 80f, _temp));
                _mercury.localScale = new Vector3(0.003f, h, 0.003f);
                _mercury.localPosition = new Vector3(0.035f, 0.015f + h * 0.5f, 0.02f);
            }
            if (Cell != null)
            {
                Cell.Aux = Mathf.MoveTowards(Cell.Aux, Mathf.Clamp01(_done * 1.2f), dt * 0.8f);
                Cell.SetCaption(_front < 0f ? (_melting ? "melting: lattice falls apart" : "supercooled liquid: ions\nwander, nothing to copy")
                    : "seed = template: molecules\nlock on, latent heat released");
            }
            _tick += dt;
            if (_tick > 0.15f && _thermo != null)
            {
                _tick = 0f;
                string state = _melting ? "<color=#FFB84A>re-melting</color>" : (_front < 0f ? (_temp < 40f ? "supersaturated (clear)" : "warm: seeding gives a slow front") :
                    (_done < 1f ? "<color=#7CFF9A>CRYSTALLISING</color>" : "solid, cooling"));
                _thermo.text = "<b>SODIUM ACETATE 'HOT ICE'</b>\nT " + _temp.ToString("0.0") + " C   (melts 58 C)\n" + state
                    + "\nsupercooling " + SubCool.ToString("0") + " K   front " + (KFront * Mathf.Max(2f, SubCool) * 1000.0).ToString("0.0") + " mm/s"
                    + "\nfront " + (_done * 100f).ToString("0") + "%   solid " + (MassSolid * 100f).ToString("0") + "% by mass";
            }
        }

        protected override CrystalGuideCard.Content GetContent()
        {
            return new CrystalGuideCard.Content
            {
                Title = "6  CRYSTAL GROWTH  - 'hot ice' that freezes warm",
                ChallengeId = ChallengeId,
                What =
                    "This clear liquid is sodium acetate (vinegar + baking soda) in its own crystal water. It should be solid below 58 C, but it is 22 C and still liquid: "
                    + "supercooled. The molecules need a pattern to copy before they can line up.\n\n"
                    + "Touch it with a seed crystal and they suddenly have one. A crystal front races outwards, needles fill the beaker - and it gets HOT. "
                    + "Lining up into a crystal releases the energy that melting once absorbed (latent heat). Reusable hand-warmers work exactly like this (the click disc makes the seed).\n\n"
                    + "<color=#9FB4C4>Model: spherical front from the touch point; speed tuned to demo videos; cooling time-lapsed x20.</color>",
                Math =
                    "<b>Front speed</b>   v ~ k (T_melt - T),   k ~ 0.3 mm/s per K (approx., tuned)\n"
                    + "<b>Heat balance</b>   x L = c (T_final - T_start)\n"
                    + "   L = 264 kJ/kg, c = 2.8 kJ/kgK, dT = 36 K -> x = c dT / L ~ 0.38\n"
                    + "   so only ~40% crystallises fast; the rest waits until the heat leaks away\n"
                    + "<b>Final T</b>   just below 58 C (cannot exceed the melting point)\n"
                    + "<b>Cooling</b>   dT/dt = -(T - T_room) / tau,   tau ~ 10 min\n\n"
                    + "[Green 1908 / CRC Handbook (NaOAc.3H2O, mp 58 C); Wada et al. 1984 (latent heat 264 kJ/kg); hand-warmer data, Sandnes 2008 Phys. Educ.]",
                TryThis =
                    "1. Grab the seed rod from its rack (left).\n"
                    + "2. Dip the white tip into the clear solution. Watch the needles race out from where you touched.\n"
                    + "3. Watch the thermometer: 22 C to ~54 C in seconds.\n"
                    + "4. CHALLENGE: crystallise it completely while above 50 C.\n"
                    + "5. Re-melt (heats to 70 C) and let it cool (time-lapse) to make it supersaturated again.\n"
                    + "6. Try seeding while it is still above ~40 C: less supercooling = slower front.",
                Lattice =
                    "In the liquid (model) the ions and water molecules tumble randomly. In the crystal each Na(+), acetate(-) and three waters sit in a fixed repeating pattern.\n\n"
                    + "A brand-new crystal must start from a tiny cluster, and tiny clusters are unstable (too much surface). A clean, smooth container gives nowhere easy to start - so the liquid stays supercooled.\n\n"
                    + "The seed is a ready-made, big enough piece of lattice: new molecules just click on, layer after layer, and each click releases energy (the heat you feel)."
            };
        }

        protected override void AddControls(CrystalGuideCard card)
        {
            card.AddControl("Seed centre", () => Seed(_beaker.TransformPoint(new Vector3(0f, LiquidH, 0f))));
            card.AddControl("Seed edge", () => Seed(_beaker.TransformPoint(new Vector3(BeakerR * 0.9f, LiquidH * 0.5f, 0f))));
            card.AddControl("Re-melt", Remelt);
            card.AddControl("Fresh batch", () => { _melting = false; _temp = Ambient; _tPeak = Ambient; _front = -1f; _done = 0f; });
            card.AddControl("Seed rod home", () =>
            {
                if (_rod == null) { SpawnRod(); return; }
                var h = _rod.GetComponent<CrystalHomeReturn>();
                if (h != null) h.PutBack();
            });
            card.AddControl("Spawn seed", () => SeedCrystal.Create(transform.TransformPoint(OnTop(-0.4f, 0.12f, -0.15f)), SeedCrystal.RootName));
        }

        protected override string LiveLine()
        {
            return "T " + _temp.ToString("0.0") + " C   supercooling " + SubCool.ToString("0") + " K   front " + (_front < 0f ? "-" : (_front * 1000f).ToString("0") + " mm")
                + "   front " + (_done * 100f).ToString("0") + "%   solid " + (MassSolid * 100f).ToString("0") + "% by mass\nlatent heat 264 kJ/kg   c 2.8 kJ/kgK   x(adiabatic) = c dT / L = " + (C.NaOAcCp * SubCool / C.NaOAcLatent).ToString("0.00");
        }
    }

    public sealed class HotIceInspect : MonoBehaviour, ICrystalInspectable
    {
        public GrowthStation Station;
        public string InspectStats(bool held) => "Sodium acetate solution | " + Station.Temperature.ToString("0.0") + " C | solid " + (Station.MassSolid * 100f).ToString("0") + "%";
        public string InspectDetail() => "melts 58 C | L 264 kJ/kg | supercooled liquid needs a seed";
        public string InspectHint(bool held) => "Dip the seed rod tip into it";
    }
}
