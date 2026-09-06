using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable CIRCUIT lamp: while near an InductionCircuit, glows from classical |I|.
    /// Honesty: emission proxy from NormalizedLoadCurrent — not a blackbody filament spectrum.
    /// Does not set R_load (use CIRCUIT Resistor for that).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadBulbGadget : MonoBehaviour
    {
        public const string RootName = "Gadget_Lamp";
        public const string Honesty = "Classical |I| glow proxy on InductionCircuit. Not a real filament spectrum.";

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and drive glow.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Emission intensity at NormalizedLoadCurrent = 1.")]
        float maxEmission = 4.5f;

        TextMeshPro _readout;
        Renderer _bulbRenderer;
        Material _mat;
        Color _baseColor = new Color(0.95f, 0.85f, 0.45f, 1f);
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;
        bool _linked;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            EnsureCircuits(force: true);
            BindNearest();
            RefreshVisual(0f);
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnDestroy()
        {
            if (_mat != null)
            {
                if (Application.isPlaying) Destroy(_mat);
                else DestroyImmediate(_mat);
                _mat = null;
            }
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.05f;

            if (Time.unscaledTime >= _cacheAt || _circuits == null)
                EnsureCircuits(force: false);

            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();

            float glow = 0f;
            _linked = false;
            if (_circuit != null)
            {
                float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
                if (dist <= connectWithinMeters)
                {
                    _linked = true;
                    glow = _circuit.NormalizedLoadCurrent;
                }
            }

            RefreshVisual(glow);
            RefreshText();
        }

        void EnsureVisual()
        {
            if (_bulbRenderer == null)
            {
                Transform glass = transform.Find("Glass");
                if (glass != null)
                    _bulbRenderer = glass.GetComponent<Renderer>();
                if (_bulbRenderer == null)
                    _bulbRenderer = GetComponent<Renderer>();
            }

            if (_bulbRenderer == null)
            {
                var glass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glass.name = "Glass";
                glass.transform.SetParent(transform, false);
                glass.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                glass.transform.localScale = Vector3.one * 0.07f;
                Object.Destroy(glass.GetComponent<Collider>());
                _bulbRenderer = glass.GetComponent<Renderer>();
            }

            if (_mat == null && _bulbRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", _baseColor);
                _mat.color = _baseColor;
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", Color.black);
                }
                _bulbRenderer.sharedMaterial = _mat;
            }
        }

        void EnsureCircuits(bool force)
        {
            _cacheAt = Time.unscaledTime + 1.25f;
            if (!force && _circuits != null && _circuits.Length > 0)
            {
                for (int i = 0; i < _circuits.Length; i++)
                {
                    if (_circuits[i] != null)
                        return;
                }
            }
            _circuits = Object.FindObjectsByType<InductionCircuit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        void BindNearest()
        {
            if (_circuits == null || _circuits.Length == 0)
            {
                _circuit = null;
                return;
            }

            Vector3 tip = TipWorld;
            InductionCircuit best = null;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < _circuits.Length; i++)
            {
                InductionCircuit c = _circuits[i];
                if (c == null || !c.isActiveAndEnabled)
                    continue;
                if (c.transform == transform || c.transform.IsChildOf(transform))
                    continue;
                float sq = (c.transform.position - tip).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = c;
                }
            }
            _circuit = best;
        }

        void EnsureLabCircuit()
        {
            if (_circuit != null)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            EnsureCircuits(force: true);
            BindNearest();
        }

        void RefreshVisual(float glow01)
        {
            if (_mat == null)
                EnsureVisual();
            if (_mat == null)
                return;

            float g = Mathf.Clamp01(glow01);
            Color lit = Color.Lerp(_baseColor * 0.35f, _baseColor, 0.35f + 0.65f * g);
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", lit);
            _mat.color = lit;

            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                Color e = new Color(1f, 0.85f, 0.35f) * (g * maxEmission);
                _mat.SetColor("_EmissionColor", e);
            }
        }

        void BuildReadout()
        {
            Transform existing = transform.Find("Readout");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("Readout");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(1f, 0.9f, 0.45f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "LAMP\nseeking I...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            if (_circuit == null)
            {
                _readout.text = "LAMP\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON" : (dist > connectWithinMeters ? "far" : "near");
            float i = _linked ? _circuit.CurrentAmperes : 0f;
            float emf = _linked ? _circuit.EmfVolts : 0f;
            _readout.text =
                "LAMP " + link + "\n"
                + "I " + FormatAmps(i) + "\n"
                + "Emf " + emf.ToString("0.###") + " V\n"
                + target + " " + dist.ToString("0.00") + "m\n"
                + "[classical |I| glow]";
        }

        static string FormatAmps(float a)
        {
            float abs = Mathf.Abs(a);
            if (abs < 1e-4f)
                return "0 A";
            if (abs < 1e-2f)
                return (a * 1000f).ToString("0.##") + " mA";
            return a.ToString("0.####") + " A";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
