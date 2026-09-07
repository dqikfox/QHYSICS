using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable MEASURE galvanometer: while near an InductionCircuit, needle deflects from classical I.
    /// Honesty: ideal signed current meter needle — NOT coil torque dynamics, not damping, not shunt burden.
    /// Does not modify the circuit. XR activate / N cycles full-scale; P steps backward.
    /// Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadGalvanometerGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Galvanometer";
        public const string Honesty = "Ideal signed needle from classical InductionCircuit I. NOT coil torque / damping / shunt burden.";

        // Full-scale |I| in amperes: 1mA / 5mA / 20mA / 100mA
        static readonly float[] FullScaleAmps = { 0.001f, 0.005f, 0.02f, 0.1f };
        static readonly string[] PresetLabels = { "1mA", "5mA", "20mA", "100mA" };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and drive needle.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Max needle yaw degrees at full scale (signed).")]
        float maxNeedleDeg = 55f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        Transform _needle;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        float _needleAngle;
        int _presetIndex = 1; // default 5mA
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _needleMat;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveFullScale => FullScaleAmps[Mathf.Clamp(_presetIndex, 0, FullScaleAmps.Length - 1)];
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCircuits(force: true);
            BindNearest();
            ApplyNeedle(0f);
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            WireGrab();
        }

        void OnDisable()
        {
            UnwireGrab();
        }

        void OnDestroy()
        {
            UnwireGrab();
            DestroyMat(ref _mat);
            DestroyMat(ref _needleMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.04f;

            if (Time.unscaledTime >= _cacheAt || _circuits == null)
                EnsureCircuits(force: false);

            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();

            float i = 0f;
            _linked = false;
            if (_circuit != null)
            {
                float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
                if (dist <= connectWithinMeters)
                {
                    _linked = true;
                    i = _circuit.CurrentAmperes;
                }
            }

            ApplyNeedle(i);
            PollDesktopCycle();
            RefreshText();
        }

        void WireGrab()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }

        void UnwireGrab()
        {
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
        }

        void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
        {
            CyclePreset(+1);
        }

        void PollDesktopCycle()
        {
            if (Time.unscaledTime < _inputCooldown)
                return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            if ((cam.transform.position - TipWorld).sqrMagnitude > 1.44f)
                return;
            if (kb.nKey.wasPressedThisFrame)
            {
                CyclePreset(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                CyclePreset(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CyclePreset(int delta)
        {
            if (FullScaleAmps.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % FullScaleAmps.Length;
            if (_presetIndex < 0)
                _presetIndex += FullScaleAmps.Length;
            RefreshText();
        }

        public void DesktopActivate(int delta) => CyclePreset(delta);

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

        void ApplyNeedle(float amperes)
        {
            float fs = Mathf.Max(1e-6f, ActiveFullScale);
            float norm = Mathf.Clamp(amperes / fs, -1f, 1f);
            float target = norm * maxNeedleDeg;
            _needleAngle = Mathf.Lerp(_needleAngle, target, 0.35f);
            if (_needle != null)
                _needle.localRotation = Quaternion.Euler(0f, 0f, -_needleAngle);
        }

        void EnsureVisual()
        {
            if (_bodyRenderer == null)
                _bodyRenderer = GetComponent<Renderer>();

            if (_mat == null && _bodyRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                Color baseC = new Color(0.12f, 0.45f, 0.38f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.25f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.45f);
                _bodyRenderer.sharedMaterial = _mat;
            }

            Transform face = transform.Find("Face");
            if (face == null)
            {
                var faceGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                faceGo.name = "Face";
                faceGo.transform.SetParent(transform, false);
                faceGo.transform.localPosition = new Vector3(0f, 0.035f, 0.02f);
                faceGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                faceGo.transform.localScale = new Vector3(0.07f, 0.004f, 0.07f);
                Object.Destroy(faceGo.GetComponent<Collider>());
                var fr = faceGo.GetComponent<Renderer>();
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var fm = new Material(sh);
                Color faceC = new Color(0.92f, 0.9f, 0.82f, 1f);
                if (fm.HasProperty("_BaseColor"))
                    fm.SetColor("_BaseColor", faceC);
                fm.color = faceC;
                fr.sharedMaterial = fm;
                face = faceGo.transform;
            }

            _needle = transform.Find("Needle");
            if (_needle == null)
            {
                var needleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                needleGo.name = "Needle";
                needleGo.transform.SetParent(transform, false);
                needleGo.transform.localPosition = new Vector3(0f, 0.042f, 0.02f);
                needleGo.transform.localRotation = Quaternion.identity;
                needleGo.transform.localScale = new Vector3(0.004f, 0.004f, 0.055f);
                Object.Destroy(needleGo.GetComponent<Collider>());
                var nr = needleGo.GetComponent<Renderer>();
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _needleMat = new Material(sh);
                Color nc = new Color(0.75f, 0.12f, 0.1f, 1f);
                if (_needleMat.HasProperty("_BaseColor"))
                    _needleMat.SetColor("_BaseColor", nc);
                _needleMat.color = nc;
                nr.sharedMaterial = _needleMat;
                _needle = needleGo.transform;
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
            _readout.fontSize = 24f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.75f, 0.95f, 0.88f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(32f, 18f);
            _readout.text = "GALVO\nseeking circuit...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string scale = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "GALVO " + scale + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            float i = _linked ? _circuit.CurrentAmperes : 0f;
            string iStr;
            if (Mathf.Abs(i) < 0.001f)
                iStr = (i * 1000f).ToString("+0.00;-0.00;0.00") + "mA";
            else
                iStr = i.ToString("+0.000;-0.000;0.000") + "A";

            _readout.text =
                "GALVO FS=" + scale + "\n"
                + "I " + iStr + "\n"
                + "needle " + _needleAngle.ToString("+0.0;-0.0;0.0") + "deg\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger FS\n"
                + "[ideal needle]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 0.55f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
