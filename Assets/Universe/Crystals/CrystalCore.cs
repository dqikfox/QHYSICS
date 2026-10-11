using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using RealityEngine.Audio;
using RealityEngine.Combat;
using RealityEngine.Player;
using RealityEngine.Physics.Thermo;
using RealityEngine.XR;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// Physical constants used by the Crystal Lab, with sources. Every station reads its numbers from here so the
    /// guide cards and the simulation can never disagree. Values are room-temperature textbook/datasheet values.
    /// </summary>
    public static class CrystalConstants
    {
        /// <summary>Vacuum permittivity, F/m (CODATA 2018).</summary>
        public const double Eps0 = 8.8541878128e-12;

        // ── Piezoelectricity ──
        /// <summary>alpha-quartz |d11| = 2.3 pC/N (IEEE Std 176-1987; Bechmann, Phys. Rev. 110, 1060 (1958)).</summary>
        public const double QuartzD11 = 2.3e-12;
        /// <summary>alpha-quartz relative permittivity eps11/eps0 = 4.5 (4.52; IEEE Std 176).</summary>
        public const double QuartzEpsR = 4.5;
        /// <summary>PZT-5A (Navy Type II) d33 = 374 pC/N (Morgan / APC International datasheets).</summary>
        public const double PztD33 = 374e-12;
        /// <summary>PZT-5A relative permittivity K33T ~ 1700 (datasheets).</summary>
        public const double PztEpsR = 1700.0;
        /// <summary>Air breakdown, uniform field rule of thumb ~3 MV/m (~3 kV/mm; Paschen gives more for sub-mm gaps).</summary>
        public const double AirBreakdown = 3.0e6;
        /// <summary>Quartz longitudinal sound speed along X ~5720 m/s (thickness-extension resonance f = v / 2t).</summary>
        public const double QuartzVx = 5720.0;

        // ── Pyroelectricity ──
        /// <summary>LiTaO3 pyroelectric coefficient p = 2.3e-4 C m^-2 K^-1 (S. B. Lang, Physics Today 58(8) 31 (2005)).</summary>
        public const double LiTaO3P = 2.3e-4;
        public const double LiTaO3EpsR = 43.0;
        /// <summary>LiTaO3 Curie temperature ~603 C.</summary>
        public const double LiTaO3CurieC = 603.0;
        /// <summary>Tourmaline pyroelectric coefficient p ~ 4e-6 C m^-2 K^-1 (Lang 2005).</summary>
        public const double TourmalineP = 4.0e-6;
        public const double TourmalineEpsR = 7.0;

        // ── Quartz resonators ──
        /// <summary>Quartz Young's modulus along X ~78 GPa (1/s11, s11 = 12.77e-12 /Pa).</summary>
        public const double QuartzE = 78.0e9;
        /// <summary>Quartz density 2648 kg/m^3.</summary>
        public const double QuartzRho = 2648.0;
        /// <summary>32.768 kHz tuning-fork parabolic temperature coefficient -0.034 ppm/K^2 about ~25 C (typical datasheets).</summary>
        public const double ForkBeta = -0.034e-6;
        /// <summary>AT-cut third-order temperature coefficient ~1.09e-4 ppm/K^3 = 1.09e-10 /K^3 (Bechmann; inflection ~26 C).</summary>
        public const double AtCutC3 = 1.09e-10;
        /// <summary>AT-cut thickness-shear frequency constant N = 1.661 MHz*mm = 1661 Hz*m (f = N / t).</summary>
        public const double AtCutN = 1661.0;
        /// <summary>Cantilever first-mode eigenvalue (beta1 L)^2 = 3.516.</summary>
        public const double Beta1Sq = 3.516;

        // ── Calcite ──
        /// <summary>Calcite refractive indices at 589 nm: no = 1.658, ne = 1.486 (Hecht, Optics; Ghosh, Opt. Commun. 163 (1999)).</summary>
        public const double CalciteNo = 1.658;
        public const double CalciteNe = 1.486;

        // ── Hot ice (sodium acetate trihydrate) ──
        public const double NaOAcMeltC = 58.0;
        /// <summary>Latent heat of fusion ~264 kJ/kg (literature 264-289 kJ/kg).</summary>
        public const double NaOAcLatent = 264e3;
        /// <summary>Liquid specific heat ~2.8 kJ/(kg K) (approx).</summary>
        public const double NaOAcCp = 2.8e3;

        // ── Ferroelectric ──
        /// <summary>BaTiO3 Curie temperature ~120-130 C (use 125 C).</summary>
        public const double BaTiO3CurieC = 125.0;
        /// <summary>BaTiO3 spontaneous polarisation at room temperature ~0.26 C/m^2.</summary>
        public const double BaTiO3Ps = 0.26;

        // ── Liquid crystal 5CB (4-cyano-4'-pentylbiphenyl), ~25 C ──
        /// <summary>5CB birefringence dn = ne - no ~ 0.18 at 589 nm (ne ~1.71, no ~1.53; Li, Gauza & Wu, J. Appl. Phys. 96 (2004)).</summary>
        public const double Lc5cbDn = 0.18;
        /// <summary>5CB dielectric anisotropy d_eps = eps_par - eps_perp ~ +11.5 at 1 kHz (reported +11 to +13).</summary>
        public const double Lc5cbDeps = 11.5;
        /// <summary>5CB Frank constants splay / twist / bend (pN), Bradshaw et al., J. Physique 46, 1513 (1985); values vary ~15% between sources.</summary>
        public const double Lc5cbK11 = 6.4e-12;
        public const double Lc5cbK22 = 3.0e-12;
        public const double Lc5cbK33 = 10.0e-12;
        /// <summary>5CB nematic-isotropic (clearing) point 35.3 C (Gray, Harrison & Nash, Electron. Lett. 9, 130 (1973)).</summary>
        public const double Lc5cbTniC = 35.3;
        /// <summary>5CB rotational viscosity gamma1 ~ 0.08 Pa s at 25 C (order of magnitude; literature 0.07-0.10).</summary>
        public const double Lc5cbGamma1 = 0.08;
        /// <summary>Haller order-parameter exponent S = S0 (1 - T/T*)^beta, beta ~ 0.2 (Haller, Prog. Solid State Chem. 10, 103 (1975)).</summary>
        public const double HallerBeta = 0.2;

        // ── Bragg diffraction ──
        /// <summary>Rock-salt lattice constants at room temperature (Intl. Tables for Crystallography / standard XRD reference data).</summary>
        public const double NaClA = 564.02e-12;
        public const double LiFA = 402.7e-12;
        public const double KClA = 629.3e-12;
        /// <summary>Cu K-alpha1 154.06 pm and K-beta 139.22 pm (Intl. Tables Vol. C); unfiltered K-beta / K-alpha intensity ~0.18.</summary>
        public const double CuKa1 = 154.06e-12;
        public const double CuKb = 139.22e-12;
        public const double CuKbRatio = 0.18;
        /// <summary>Opal: amorphous silica spheres n ~ 1.45, fcc packing fraction 0.74 (Sanders, Nature 204, 1151 (1964)).</summary>
        public const double SilicaN = 1.45;
        public const double OpalFill = 0.74;
        public const double WaterN = 1.333;
        public const double EthanolN = 1.361;

        // ── PZT igniter element (Piezo Strike) ──
        /// <summary>Model element: a 3 mm diameter x 6 mm long PZT-5A cylinder, as in push-button / gas igniters.</summary>
        public const double PztIgniterDia = 3.0e-3;
        public const double PztIgniterLen = 6.0e-3;
        /// <summary>Stress cap ~50 MPa: soft PZT starts to depole under higher dynamic compression (order of magnitude from datasheets).</summary>
        public const double PztMaxStress = 50e6;
    }

    /// <summary>Crystal objects can feed the INSPECT strip with live physics lines.</summary>
    public interface ICrystalInspectable
    {
        string InspectStats(bool held);
        string InspectDetail();
        string InspectHint(bool held);
    }

    /// <summary>
    /// Latches for Crystal Lab challenge objectives (ObjectiveType.CrystalGoal). Stations call Latch / Report;
    /// ChallengeManager clears on challenge start/reset and polls IsMet. "Ever" survives clears (card ticks).
    /// </summary>
    public static class CrystalGoals
    {
        static readonly HashSet<string> Latched = new HashSet<string>();
        static readonly HashSet<string> Ever = new HashSet<string>();
        static readonly Dictionary<string, float> Best = new Dictionary<string, float>();

        public static void Latch(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (Latched.Add(id))
                Debug.Log("CrystalLab: goal '" + id + "' reached.");
            Ever.Add(id);
        }

        public static void Report(string id, float value)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (!Best.TryGetValue(id, out float b) || value > b)
                Best[id] = value;
        }

        public static float Value(string id)
        {
            return id != null && Best.TryGetValue(id, out float b) ? b : 0f;
        }

        public static bool IsMet(string id, float target)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (target > 0f)
                return Value(id) >= target;
            return Latched.Contains(id);
        }

        public static bool EverDone(string id) => id != null && Ever.Contains(id);

        public static void Clear()
        {
            Latched.Clear();
            Best.Clear();
        }

        public static string Readout(string id, float target)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;
            if (target > 0f)
            {
                if (id.EndsWith("_v", StringComparison.Ordinal))
                    return CrystalFx.FormatVolts(Value(id)) + " / " + CrystalFx.FormatVolts(target);
                return Value(id).ToString("0.##") + " / " + target.ToString("0.##");
            }
            return Latched.Contains(id) ? "done" : "not yet";
        }
    }

    /// <summary>Shared materials, meshes, primitives, text, haptics, flashes and sparks for the Crystal Lab.</summary>
    public static class CrystalFx
    {
        static readonly Dictionary<string, Material> Glass = new Dictionary<string, Material>();
        static Mesh _lowSphere;
        static Shader _lit;

        static Shader Lit
        {
            get
            {
                if (_lit == null)
                    _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null)
                    _lit = Shader.Find("Standard");
                return _lit;
            }
        }

        public static Material Solid(Color c, float metal = 0f, float smooth = 0.4f, Color? emission = null)
        {
            return CombatMaterials.Get(c, metal, smooth, emission);
        }

        public static Material Emissive(Color c, float strength = 1f)
        {
            return CombatMaterials.Get(c * 0.4f + new Color(0f, 0f, 0f, 1f), 0f, 0.5f, c * strength);
        }

        public static Material Line(Color c)
        {
            return CombatMaterials.UnlitLine(c);
        }

        /// <summary>Translucent crystal: URP Lit transparent (alpha blend), very smooth, optional faint glow. Shared per tint.</summary>
        public static Material Crystal(Color tint, Color? glow = null)
        {
            string key = ColorUtility.ToHtmlStringRGBA(tint) + (glow.HasValue ? ColorUtility.ToHtmlStringRGB(glow.Value) : "");
            if (Glass.TryGetValue(key, out Material m) && m != null)
                return m;
            m = new Material(Lit) { name = "Crystal_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            m.color = tint;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.93f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.05f);
            if (glow.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow.Value);
            }
            Glass[key] = m;
            return m;
        }

        /// <summary>Shared low-poly UV sphere (8 x 6, 63 verts) for ions, beads and flashes.</summary>
        public static Mesh LowSphere
        {
            get
            {
                if (_lowSphere != null)
                    return _lowSphere;
                const int seg = 8, rings = 6;
                var v = new List<Vector3>();
                var n = new List<Vector3>();
                var t = new List<int>();
                for (int r = 0; r <= rings; r++)
                {
                    float phi = Mathf.PI * r / rings;
                    for (int s = 0; s <= seg; s++)
                    {
                        float th = 2f * Mathf.PI * s / seg;
                        var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                        v.Add(p * 0.5f);
                        n.Add(p);
                    }
                }
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < seg; s++)
                    {
                        int a = r * (seg + 1) + s, b = a + seg + 1;
                        t.Add(a); t.Add(a + 1); t.Add(b);
                        t.Add(b); t.Add(a + 1); t.Add(b + 1);
                    }
                }
                _lowSphere = new Mesh { name = "Crystal_LowSphere" };
                _lowSphere.SetVertices(v);
                _lowSphere.SetNormals(n);
                _lowSphere.SetTriangles(t, 0);
                _lowSphere.RecalculateBounds();
                return _lowSphere;
            }
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 lp, Vector3 ls, Material mat, bool collider = true)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = ls;
            var r = g.GetComponent<Renderer>();
            if (r != null)
            {
                if (mat != null)
                    r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            if (!collider)
                UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>Swap a flattened cylinder's capsule (which becomes a big sphere) for a snug box collider.</summary>
        public static void FlatCollider(GameObject g)
        {
            var cap = g.GetComponent<CapsuleCollider>();
            if (cap != null)
                UnityEngine.Object.DestroyImmediate(cap);
            if (g.GetComponent<BoxCollider>() == null)
                g.AddComponent<BoxCollider>();
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 lp, Quaternion lr, Vector3 ls)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localRotation = lr;
            g.transform.localScale = ls;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }

        public static GameObject Bead(string name, Transform parent, Vector3 lp, float diameter, Material mat)
        {
            return MeshObject(name, parent, LowSphere, mat, lp, Quaternion.identity, Vector3.one * diameter);
        }

        /// <summary>World TMP label. Readable by a viewer looking along the label's local +Z (identity in a station frame).</summary>
        public static TextMeshPro Text(Transform parent, string name, string text, float size, Color color, Vector3 lp, Vector2 rect,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.rectTransform.sizeDelta = rect;
            if (RealityEngine.UI.QhysicsUiStyle.Font != null)
                t.font = RealityEngine.UI.QhysicsUiStyle.Font;
            return t;
        }

        /// <summary>Grabbable rigidbody with an XRI grab (works with XrGrabFeel / XrGrabHaptics / LostItemReturn).</summary>
        public static XRGrabInteractable MakeGrabbable(GameObject go, float mass)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var g = go.GetComponent<XRGrabInteractable>();
            if (g == null)
                g = go.AddComponent<XRGrabInteractable>();
            return g;
        }

        /// <summary>Buzz the hand(s) currently holding this interactable (XR only, no-op on desktop).</summary>
        public static void PulseHolders(XRBaseInteractable ix, float amp, float sec)
        {
            if (ix == null || !ix.isSelected)
                return;
            var list = ix.interactorsSelecting;
            for (int i = 0; i < list.Count; i++)
            {
                XRNode? node = XrGrabHaptics.NodeFor(list[i] as XRBaseInteractor);
                if (node.HasValue)
                    XrGrabHaptics.Pulse(node.Value, amp, sec);
            }
        }

        /// <summary>Buzz both controllers (XR only).</summary>
        public static void PulseBoth(float amp, float sec)
        {
            XrGrabHaptics.Pulse(XRNode.LeftHand, amp, sec);
            XrGrabHaptics.Pulse(XRNode.RightHand, amp, sec);
        }

        public static string FormatVolts(double v)
        {
            double a = Math.Abs(v);
            if (a >= 1e3) return (v / 1e3).ToString("0.00") + " kV";
            if (a >= 1.0) return v.ToString("0.0") + " V";
            if (a >= 1e-3) return (v * 1e3).ToString("0.0") + " mV";
            return (v * 1e6).ToString("0") + " uV";
        }

        public static string FormatCharge(double q)
        {
            double a = Math.Abs(q);
            if (a >= 1e-6) return (q * 1e6).ToString("0.00") + " uC";
            if (a >= 1e-9) return (q * 1e9).ToString("0.00") + " nC";
            return (q * 1e12).ToString("0.0") + " pC";
        }

        public static string FormatCap(double c)
        {
            if (c >= 1e-9) return (c * 1e9).ToString("0.00") + " nF";
            return (c * 1e12).ToString("0.0") + " pF";
        }

        public static string FormatHz(double f)
        {
            if (f >= 1e6) return (f / 1e6).ToString("0.000000") + " MHz";
            if (f >= 1e3) return f.ToString("0.000") + " Hz";
            return f.ToString("0.00") + " Hz";
        }

        // ── Flash + spark pools ──

        public static void Flash(Vector3 pos, Color color, float intensity, float seconds, float size)
        {
            CrystalFlashPool.Ensure().Flash(pos, color, intensity, seconds, size);
        }

        public static void Spark(Vector3 a, Vector3 b, Color color)
        {
            CrystalFlashPool.Ensure().Spark(a, b, color);
        }
    }

    /// <summary>Two pooled flash lights/beads and two spark lines (no per-event allocation, 1 light active at a time typical).</summary>
    public sealed class CrystalFlashPool : MonoBehaviour
    {
        static CrystalFlashPool _instance;
        Light[] _lights;
        Transform[] _beads;
        Renderer[] _beadR;
        float[] _until;
        float[] _dur;
        float[] _peak;
        LineRenderer[] _lines;
        float[] _lineUntil;
        int _next, _nextLine;
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        public static CrystalFlashPool Ensure()
        {
            if (_instance != null)
                return _instance;
            var go = new GameObject("QhysicsCrystalFlashPool");
            _instance = go.AddComponent<CrystalFlashPool>();
            _instance.Build();
            return _instance;
        }

        void Build()
        {
            _lights = new Light[2];
            _beads = new Transform[2];
            _beadR = new Renderer[2];
            _until = new float[2];
            _dur = new float[2];
            _peak = new float[2];
            Material bead = CrystalFx.Crystal(new Color(1f, 1f, 1f, 0.55f), Color.white * 2f);
            for (int i = 0; i < 2; i++)
            {
                var l = new GameObject("Flash" + i).AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.range = 1.2f;
                l.enabled = false;
                _lights[i] = l;
                var b = CrystalFx.Bead("FlashBead" + i, transform, Vector3.zero, 0.03f, bead);
                b.SetActive(false);
                _beads[i] = b.transform;
                _beadR[i] = b.GetComponent<Renderer>();
            }
            _lines = new LineRenderer[2];
            _lineUntil = new float[2];
            for (int i = 0; i < 2; i++)
            {
                var g = new GameObject("Spark" + i);
                g.transform.SetParent(transform, false);
                var lr = g.AddComponent<LineRenderer>();
                lr.positionCount = 7;
                lr.widthMultiplier = 0.0025f;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = CrystalFx.Line(new Color(0.75f, 0.9f, 1f, 1f));
                lr.enabled = false;
                _lines[i] = lr;
            }
        }

        public void Flash(Vector3 pos, Color color, float intensity, float seconds, float size)
        {
            int i = _next;
            _next = (_next + 1) % _lights.Length;
            Light l = _lights[i];
            l.transform.position = pos;
            l.color = color;
            l.range = Mathf.Clamp(0.4f + size * 6f, 0.4f, 2.5f);
            l.intensity = intensity;
            l.enabled = true;
            _beads[i].position = pos;
            _beads[i].localScale = Vector3.one * Mathf.Max(0.005f, size);
            _beads[i].gameObject.SetActive(true);
            _mpb.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.6f));
            _mpb.SetColor("_EmissionColor", color * 2.5f);
            _beadR[i].SetPropertyBlock(_mpb);
            _dur[i] = Mathf.Max(0.02f, seconds);
            _until[i] = Time.unscaledTime + _dur[i];
            _peak[i] = intensity;
        }

        public void Spark(Vector3 a, Vector3 b, Color color)
        {
            int i = _nextLine;
            _nextLine = (_nextLine + 1) % _lines.Length;
            LineRenderer lr = _lines[i];
            Vector3 d = b - a;
            Vector3 side = Vector3.Cross(d.normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Vector3 up2 = Vector3.Cross(side, d.normalized);
            float jag = Mathf.Max(0.001f, d.magnitude * 0.18f);
            for (int k = 0; k < lr.positionCount; k++)
            {
                float u = k / (float)(lr.positionCount - 1);
                Vector3 p = a + d * u;
                if (k > 0 && k < lr.positionCount - 1)
                    p += side * UnityEngine.Random.Range(-jag, jag) + up2 * UnityEngine.Random.Range(-jag, jag);
                lr.SetPosition(k, p);
            }
            lr.startColor = lr.endColor = color;
            lr.enabled = true;
            _lineUntil[i] = Time.unscaledTime + 0.09f;
            Flash((a + b) * 0.5f, color, 2.2f, 0.09f, 0.012f);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _lights.Length; i++)
            {
                if (!_lights[i].enabled)
                    continue;
                float left = _until[i] - now;
                if (left <= 0f)
                {
                    _lights[i].enabled = false;
                    _beads[i].gameObject.SetActive(false);
                    continue;
                }
                _lights[i].intensity = _peak[i] * Mathf.Clamp01(left / _dur[i]);
            }
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i].enabled && now > _lineUntil[i])
                    _lines[i].enabled = false;
            }
        }
    }

    /// <summary>
    /// Continuous procedural tone for resonances (quartz ring, piezo drive). One looping 1 s clip
    /// (220 Hz + glassy harmonics, integer cycles so it loops seamlessly); pitch scales it. SFX bus.
    /// </summary>
    public sealed class CrystalHum : MonoBehaviour
    {
        const float BaseHz = 220f;
        static AudioClip _clip;
        AudioSource _src;
        float _targetVol;

        public static CrystalHum Attach(GameObject host)
        {
            var h = host.GetComponent<CrystalHum>();
            if (h == null)
                h = host.AddComponent<CrystalHum>();
            return h;
        }

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = true;
            _src.spatialBlend = 1f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 0.8f;
            _src.maxDistance = 8f;
            _src.dopplerLevel = 0f;
            _src.volume = 0f;
            _src.clip = Clip();
            AudioRouter.Register(_src, AudioBus.SFX);
        }

        static AudioClip Clip()
        {
            if (_clip != null)
                return _clip;
            const int sr = 44100;
            var d = new float[sr];
            for (int i = 0; i < sr; i++)
            {
                float t = i / (float)sr;
                d[i] = 0.55f * Mathf.Sin(2f * Mathf.PI * BaseHz * t)
                    + 0.22f * Mathf.Sin(2f * Mathf.PI * BaseHz * 2f * t)
                    + 0.10f * Mathf.Sin(2f * Mathf.PI * BaseHz * 3f * t)
                    + 0.05f * Mathf.Sin(2f * Mathf.PI * BaseHz * 5f * t);
            }
            _clip = AudioClip.Create("crystal_hum", sr, 1, sr, false);
            _clip.SetData(d, 0);
            return _clip;
        }

        /// <summary>Set the audible pitch (Hz, clamped to the clip's pitch range) and volume 0..1. Volume 0 stops.</summary>
        public void SetTone(float hz, float volume)
        {
            if (_src == null)
                return;
            _src.pitch = Mathf.Clamp(hz / BaseHz, 0.25f, 3f);
            _targetVol = Mathf.Clamp01(volume);
            if (_targetVol > 0.001f && !_src.isPlaying)
                _src.Play();
        }

        void Update()
        {
            if (_src == null)
                return;
            _src.volume = Mathf.MoveTowards(_src.volume, _targetVol, Time.unscaledDeltaTime * 2.5f);
            if (_src.volume <= 0.0005f && _targetVol <= 0.001f && _src.isPlaying)
                _src.Stop();
        }
    }

    /// <summary>
    /// Heat / cold source the crystals feel (heat lamp, cold plate, burner flame). Existing thermo HeatReservoirs
    /// are also sampled (polled once a second). Simplification: lumped Newton heating toward each source's
    /// effective temperature with a linear distance falloff; not a radiation / conduction solver.
    /// </summary>
    public sealed class CrystalHeatSource : MonoBehaviour
    {
        public static readonly List<CrystalHeatSource> All = new List<CrystalHeatSource>();
        public float TempC = 120f;
        public float Radius = 0.3f;
        public float Strength = 1f;
        public bool On = true;

        static HeatReservoir[] _reservoirs;
        static float _nextReservoirScan;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Effective surrounding temperature at p (C) and a coupling multiplier (1 = ambient only).</summary>
        public static float EnvironmentAt(Vector3 p, float ambientC, out float coupling)
        {
            float wSum = 1f, tSum = ambientC;
            for (int i = 0; i < All.Count; i++)
            {
                CrystalHeatSource s = All[i];
                if (s == null || !s.On || !s.isActiveAndEnabled)
                    continue;
                float d = Vector3.Distance(p, s.transform.position);
                float w = Mathf.Clamp01(1f - d / Mathf.Max(0.01f, s.Radius)) * s.Strength * 3f;
                if (w <= 0f)
                    continue;
                wSum += w;
                tSum += w * s.TempC;
            }
            if (Time.unscaledTime >= _nextReservoirScan)
            {
                _nextReservoirScan = Time.unscaledTime + 1f;
                _reservoirs = UnityEngine.Object.FindObjectsByType<HeatReservoir>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
            if (_reservoirs != null)
            {
                for (int i = 0; i < _reservoirs.Length; i++)
                {
                    HeatReservoir r = _reservoirs[i];
                    if (r == null)
                        continue;
                    Bounds b = r.WorldBounds;
                    float d = Mathf.Sqrt(b.SqrDistance(p));
                    float w = Mathf.Clamp01(1f - d / 0.3f) * 3f;
                    if (w <= 0f)
                        continue;
                    wSum += w;
                    tSum += w * (r.Temperature - 273.15f);
                }
            }
            coupling = wSum;
            return tSum / wSum;
        }
    }

    /// <summary>
    /// Twist dial: grab (ray or direct) and roll your wrist clockwise to raise the value, anticlockwise to lower it.
    /// Desktop: E on the dial steps it up (wraps). Used for the press screw, turntables and polarizer rings.
    /// </summary>
    public sealed class CrystalDial : MonoBehaviour, IQhysicsInteractable
    {
        public float Value01;
        public float TurnsForFull = 1.25f;
        public float DesktopStep = 0.1f;
        public string Label = "Turn";
        public Action<float> Changed;
        public bool Wrap;

        XRSimpleInteractable _ix;
        Transform _hand;
        Vector3 _lastUp;
        float _hapticAcc;

        public bool Turning => _hand != null;

        void Awake()
        {
            _ix = GetComponent<XRSimpleInteractable>();
            if (_ix == null)
                _ix = gameObject.AddComponent<XRSimpleInteractable>();
            _ix.selectEntered.AddListener(OnGrab);
            _ix.selectExited.AddListener(OnRelease);
        }

        void OnDestroy()
        {
            if (_ix != null)
            {
                _ix.selectEntered.RemoveListener(OnGrab);
                _ix.selectExited.RemoveListener(OnRelease);
            }
        }

        void OnGrab(SelectEnterEventArgs e)
        {
            _hand = e.interactorObject != null ? e.interactorObject.transform : null;
            if (_hand != null)
                _lastUp = _hand.up;
            QhysicsSfx.PlayAt(SfxId.UiClick, transform.position, 0.4f);
        }

        void OnRelease(SelectExitEventArgs e)
        {
            _hand = null;
        }

        void Update()
        {
            if (_hand == null)
                return;
            Vector3 fwd = _hand.forward;
            Vector3 a = Vector3.ProjectOnPlane(_lastUp, fwd);
            Vector3 b = Vector3.ProjectOnPlane(_hand.up, fwd);
            _lastUp = _hand.up;
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f)
                return;
            float d = Vector3.SignedAngle(a, b, fwd); // + = anticlockwise as seen by the user
            if (Mathf.Abs(d) < 0.05f)
                return;
            Set(Value01 - d / (360f * Mathf.Max(0.05f, TurnsForFull)));
            _hapticAcc += Mathf.Abs(d);
            if (_hapticAcc > 12f)
            {
                _hapticAcc = 0f;
                CrystalFx.PulseHolders(_ix, 0.08f, 0.01f);
            }
        }

        public void Set(float v)
        {
            float nv = Wrap ? Mathf.Repeat(v, 1f) : Mathf.Clamp01(v);
            if (Mathf.Approximately(nv, Value01))
                return;
            Value01 = nv;
            if (Changed != null)
                Changed(Value01);
        }

        public string InteractLabel => Label;
        public bool CanInteract(GameObject actor) => true;

        public void Interact(GameObject actor)
        {
            float nv = Value01 + DesktopStep;
            if (nv > 1.0001f)
                nv = 0f;
            Set(nv);
            QhysicsSfx.PlayAt(SfxId.UiClick, transform.position, 0.4f);
        }

        public void SetHover(bool hovered) { }
    }
}
