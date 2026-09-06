using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS coil gadget with a live classical InductionCoil + InductionCircuit.
    /// Honesty: lumped Faraday coil - not a Maxwell solver / not the coil's self-field.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandheldCoil : MonoBehaviour
    {
        public const string RootName = "Gadget_Coil";
        public const string Honesty = "Classical lumped Faraday coil. Not a Maxwell solver / not the coil self-field.";

        [SerializeField] int turns = 80;
        [SerializeField] float radius = 0.12f;
        [SerializeField] float windingOhms = 2.0f;
        [SerializeField] float loadOhms = 8.0f;

        InductionCoil _coil;
        InductionCircuit _circuit;
        TextMeshPro _readout;
        float _refreshAt;
        float _bindAt;
        bool _built;

        public InductionCoil Coil => _coil;
        public InductionCircuit Circuit => _circuit;

        public void EnsureBuilt()
        {
            if (!_built)
                BuildVisuals();
            EnsurePhysics();
            if (_readout == null)
                BuildReadout();
            BindMagnets();
            RefreshText();
            _built = true;
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void Update()
        {
            if (Time.unscaledTime >= _bindAt)
            {
                _bindAt = Time.unscaledTime + 1.0f;
                BindMagnets();
            }

            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.1f;
            RefreshText();
        }

        public void BindMagnets()
        {
            if (_coil == null)
                _coil = GetComponent<InductionCoil>();
            BindMagnetsTo(_coil);
        }

        /// <summary>
        /// Wire an InductionCoil to every MagneticDipole currently in the scene (lab + handheld).
        /// Safe to call on a cloned lab Coil without rebuilding its mesh.
        /// </summary>
        public static void BindMagnetsTo(InductionCoil coil)
        {
            if (coil == null)
                return;

            MagneticDipole[] dips = Object.FindObjectsByType<MagneticDipole>(FindObjectsInactive.Include);
            MagneticDipole primary = null;
            int extraCount = 0;
            for (int i = 0; i < dips.Length; i++)
            {
                if (dips[i] == null)
                    continue;
                if (primary == null)
                    primary = dips[i];
                else
                    extraCount++;
            }

            if (primary == null)
            {
                coil.SetMagnets(null, null);
                return;
            }

            MagneticDipole[] extras = null;
            if (extraCount > 0)
            {
                extras = new MagneticDipole[extraCount];
                int e = 0;
                for (int i = 0; i < dips.Length; i++)
                {
                    if (dips[i] == null || dips[i] == primary)
                        continue;
                    extras[e++] = dips[i];
                }
            }

            coil.SetMagnets(primary, extras);
        }

        void EnsurePhysics()
        {
            _coil = GetComponent<InductionCoil>();
            if (_coil == null)
                _coil = gameObject.AddComponent<InductionCoil>();
            _coil.Configure(turns, radius, windingOhms, loadOhms);

            _circuit = GetComponent<InductionCircuit>();
            if (_circuit == null)
                _circuit = gameObject.AddComponent<InductionCircuit>();
            _circuit.SetCoil(_coil);
        }

        void BuildVisuals()
        {
            ClearChildren();
            gameObject.name = RootName;

            float R = Mathf.Max(0.05f, radius);
            float tube = 0.007f;
            const int segments = 16;
            const int rings = 4;
            float stack = 0.036f;
            Color copper = new Color(0.72f, 0.45f, 0.20f);

            for (int r = 0; r < rings; r++)
            {
                float y = (r / (float)(rings - 1) - 0.5f) * stack;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = (i / (float)segments) * Mathf.PI * 2f;
                    float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * R, y, Mathf.Sin(a0) * R);
                    Vector3 p1 = new Vector3(Mathf.Cos(a1) * R, y, Mathf.Sin(a1) * R);
                    Vector3 mid = (p0 + p1) * 0.5f;
                    Vector3 dir = p1 - p0;
                    var tubeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.Destroy(tubeGo.GetComponent<Collider>());
                    tubeGo.name = "Winding";
                    tubeGo.transform.SetParent(transform, false);
                    tubeGo.transform.localPosition = mid;
                    tubeGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                    tubeGo.transform.localScale = new Vector3(tube * 2f, dir.magnitude * 0.5f, tube * 2f);
                    ApplyLit(tubeGo, copper);
                }
            }

            // Grab volume: sphere covering the ring (Capsule also fine; sphere matches open coil aperture).
            var sphere = gameObject.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = R + tube * 2f;
            sphere.center = Vector3.zero;

            var capsule = gameObject.GetComponent<CapsuleCollider>();
            if (capsule != null)
                Object.Destroy(capsule);
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == "Readout")
                    continue;
                if (Application.isPlaying) Object.Destroy(c.gameObject);
                else Object.DestroyImmediate(c.gameObject);
            }
        }

        static void ApplyLit(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null)
                return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null)
                return;
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.85f);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.55f);
            r.sharedMaterial = mat;
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
                go.transform.localPosition = new Vector3(0f, radius + 0.06f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(1f, 0.78f, 0.45f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(32f, 18f);
            _readout.text = "COIL\n...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;
            if (_coil == null || _circuit == null)
                EnsurePhysics();

            _readout.text =
                "COIL N=" + _coil.Turns + "\n"
                + "R=" + (_coil.Resistance + _coil.LoadResistance).ToString("0.#") + " ohm\n"
                + "Emf " + _circuit.EmfVolts.ToString("0.###") + " V\n"
                + "I " + _circuit.CurrentAmperes.ToString("0.####") + " A\n"
                + "Phi " + _circuit.FluxWebers.ToString("0.####") + " Wb\n"
                + "[" + Honesty + "]";
        }
    }
}
