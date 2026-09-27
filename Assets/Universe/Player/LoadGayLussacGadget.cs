using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Gay-Lussac / Amontons rigid vessel: ideal isochoric P/T = k (fixed V).
    /// Honesty: ideal-gas isochoric P~T (fixed V). NOT Boyle (isothermal), NOT Charles (isobaric),
    /// not real gas, not phase change, not burst vessel.
    /// XR activate / N cycles temperature presets; P steps backward. Desktop via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadGayLussacGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_GayLussac";
        public const string Honesty =
            "Ideal isochoric Gay-Lussac P/T=k (fixed V). NOT Boyle (isothermal), NOT Charles (isobaric), not real gas, not phase change, not burst vessel.";

        // Absolute temperature presets (K)
        static readonly float[] PresetsTK = { 250f, 293f, 350f, 400f };
        static readonly string[] PresetTLabels = { "COLD", "ROOM", "WARM", "HOT" };

        // At ROOM (293 K) fix P0 = 1 atm, V = 20 mL -> P = P0 * (T / TRef)
        const float RefTempK = 293f;
        const float RefPressureAtm = 1f;
        const float VolumeMl = 20f;

        // Visual layout (local)
        const float BodyRadius = 0.035f;
        const float BodyLen = 0.22f;
        const float BodyLocalY = 0.16f;
        const float PressureBarMax = 0.20f;

        TextMeshPro _readout;
        Transform _stand;
        Transform _body;
        Transform _gas;
        Transform _cap;
        Transform _heater;
        Transform _pressureBar;
        Transform _needle;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _bodyMat;
        Material _gasMat;
        Material _capMat;
        Material _heaterMat;
        Material _pressureMat;
        Material _needleMat;
        float _refreshAt;
        float _inputCooldown;
        int _tIndex = 1; // default ROOM

        public float TempK => PresetsTK[Mathf.Clamp(_tIndex, 0, PresetsTK.Length - 1)];
        public float TempC => TempK - 273.15f;
        public float PressureAtm => RefPressureAtm * (TempK / RefTempK);
        public float PTConstant => TempK > 1e-6f ? PressureAtm / TempK : 0f;
        public string ActiveTLabel => PresetTLabels[Mathf.Clamp(_tIndex, 0, PresetTLabels.Length - 1)];
        public float TempFraction
        {
            get
            {
                float tmin = PresetsTK[0];
                float tmax = PresetsTK[PresetsTK.Length - 1];
                return Mathf.InverseLerp(tmin, tmax, TempK);
            }
        }

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            ApplyGasPose();
            ApplyPressureIndicator();
            ApplyHeatLook();
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
            DestroyMat(ref _standMat);
            DestroyMat(ref _bodyMat);
            DestroyMat(ref _gasMat);
            DestroyMat(ref _capMat);
            DestroyMat(ref _heaterMat);
            DestroyMat(ref _pressureMat);
            DestroyMat(ref _needleMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(m);
            else UnityEngine.Object.DestroyImmediate(m);
            m = null;
        }

        void Update()
        {
            PollDesktopCycle();
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.08f;
            ApplyGasPose();
            ApplyPressureIndicator();
            ApplyHeatLook();
            RefreshText();
            FaceReadout();
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
            CycleTemp(+1);
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
            if ((cam.transform.position - transform.position).sqrMagnitude > 2.25f)
                return;

            if (kb.nKey.wasPressedThisFrame)
            {
                CycleTemp(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                CycleTemp(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleTemp(int delta)
        {
            if (PresetsTK.Length == 0)
                return;
            _tIndex = (_tIndex + delta) % PresetsTK.Length;
            if (_tIndex < 0)
                _tIndex += PresetsTK.Length;
            ApplyGasPose();
            ApplyPressureIndicator();
            ApplyHeatLook();
            RefreshText();
        }

        public void CyclePreset(int delta) => CycleTemp(delta);

        public void DesktopActivate(int delta)
        {
            CycleTemp(delta == 0 ? 1 : delta);
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _stand = EnsurePrimitive("Stand", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f),
                new Vector3(0.12f, 0.04f, 0.12f), Quaternion.identity);
            _standMat = EnsureMat(ref _standMat, sh, new Color(0.12f, 0.14f, 0.16f, 1f), 0.35f, 0.45f);
            ApplyMat(_stand, _standMat);

            // Rigid sealed cylinder body (amber/steel) — volume FIXED
            _body = EnsurePrimitive("Body", PrimitiveType.Cylinder, new Vector3(0f, BodyLocalY, 0f),
                new Vector3(BodyRadius * 2f, BodyLen * 0.5f, BodyRadius * 2f), Quaternion.identity);
            _bodyMat = EnsureMat(ref _bodyMat, sh, new Color(0.62f, 0.48f, 0.38f, 1f), 0.45f, 0.55f, emit: 0.08f);
            ApplyMat(_body, _bodyMat);

            _cap = EnsurePrimitive("Cap", PrimitiveType.Cylinder, new Vector3(0f, BodyLocalY + BodyLen * 0.5f + 0.015f, 0f),
                new Vector3(BodyRadius * 2.1f, 0.015f, BodyRadius * 2.1f), Quaternion.identity);
            _capMat = EnsureMat(ref _capMat, sh, new Color(0.35f, 0.38f, 0.42f, 1f), 0.55f, 0.5f);
            ApplyMat(_cap, _capMat);

            // Fixed gas fill — volume does NOT change (unlike Charles)
            _gas = EnsurePrimitive("Gas", PrimitiveType.Cylinder, new Vector3(0f, BodyLocalY, 0f),
                new Vector3(BodyRadius * 1.55f, BodyLen * 0.42f, BodyRadius * 1.55f), Quaternion.identity);
            _gasMat = EnsureMat(ref _gasMat, sh, new Color(0.85f, 0.35f, 0.45f, 1f), 0.05f, 0.4f, emit: 0.45f);
            ApplyMat(_gas, _gasMat);

            // Heater ring under vessel — glow intensifies with T
            _heater = EnsurePrimitive("Heater", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0f),
                new Vector3(0.10f, 0.012f, 0.10f), Quaternion.identity);
            _heaterMat = EnsureMat(ref _heaterMat, sh, new Color(0.85f, 0.35f, 0.12f, 1f), 0.2f, 0.55f, emit: 0.4f);
            ApplyMat(_heater, _heaterMat);

            // Vertical pressure indicator bar — height tracks PressureAtm
            _pressureBar = EnsurePrimitive("PressureBar", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.018f, 0.05f, 0.018f), Quaternion.identity);
            _pressureMat = EnsureMat(ref _pressureMat, sh, new Color(0.95f, 0.25f, 0.35f, 1f), 0.1f, 0.5f, emit: 0.7f);
            ApplyMat(_pressureBar, _pressureMat);

            // Needle tip on the pressure bar
            _needle = EnsurePrimitive("Needle", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.04f, 0.008f, 0.008f), Quaternion.identity);
            _needleMat = EnsureMat(ref _needleMat, sh, new Color(0.95f, 0.9f, 0.35f, 1f), 0.2f, 0.6f, emit: 0.5f);
            ApplyMat(_needle, _needleMat);
        }

        void ApplyGasPose()
        {
            // Fixed fill — keep gas volume constant (isochoric)
            if (_gas == null)
                return;
            _gas.localPosition = new Vector3(0f, BodyLocalY, 0f);
            _gas.localScale = new Vector3(BodyRadius * 1.55f, BodyLen * 0.42f, BodyRadius * 1.55f);
        }

        void ApplyPressureIndicator()
        {
            if (_pressureBar == null)
                return;

            // Fraction 0 = COLD (low P) -> short bar; 1 = HOT -> tall bar
            float t = TempFraction;
            float barLen = Mathf.Lerp(0.04f, PressureBarMax, t);
            float barBaseY = 0.08f;
            float barCenterY = barBaseY + barLen * 0.5f;
            // Place beside the vessel
            _pressureBar.localPosition = new Vector3(0.075f, barCenterY, 0f);
            _pressureBar.localScale = new Vector3(0.018f, barLen * 0.5f, 0.018f);

            if (_needle != null)
            {
                _needle.localPosition = new Vector3(0.095f, barBaseY + barLen, 0f);
                _needle.localScale = new Vector3(0.04f, 0.008f, 0.008f);
            }
        }

        void ApplyHeatLook()
        {
            float tNorm = TempFraction;
            if (_gasMat != null)
            {
                // Cool blue -> warm rose/red as T (and P) rises
                Color c = Color.Lerp(new Color(0.35f, 0.55f, 0.95f, 1f), new Color(0.95f, 0.28f, 0.35f, 1f), tNorm);
                SetMatColor(_gasMat, c, emit: 0.3f + 1.1f * tNorm);
            }
            if (_heaterMat != null)
            {
                Color h = Color.Lerp(new Color(0.45f, 0.22f, 0.12f, 1f), new Color(1f, 0.35f, 0.08f, 1f), tNorm);
                SetMatColor(_heaterMat, h, emit: 0.2f + 1.4f * tNorm);
            }
            if (_bodyMat != null)
            {
                Color b = Color.Lerp(new Color(0.48f, 0.50f, 0.55f, 1f), new Color(0.72f, 0.42f, 0.35f, 1f), tNorm * 0.7f);
                SetMatColor(_bodyMat, b, emit: 0.05f + 0.3f * tNorm);
            }
            if (_pressureMat != null)
            {
                Color p = Color.Lerp(new Color(0.4f, 0.55f, 0.9f, 1f), new Color(0.98f, 0.22f, 0.32f, 1f), tNorm);
                SetMatColor(_pressureMat, p, emit: 0.4f + 1.2f * tNorm);
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
                go.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 20f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(1f, 0.65f, 0.55f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(48f, 30f);
            _readout.text = "GAY-LUSSAC\nidle";
        }

        void FaceReadout()
        {
            if (_readout == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector3 toCam = _readout.transform.position - cam.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-6f)
                _readout.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        void RefreshText()
        {
            if (_readout == null)
                return;
            float p = PressureAtm;
            float tK = TempK;
            float tC = TempC;
            float pt = PTConstant;
            _readout.text =
                "GAY-LUSSAC " + ActiveTLabel + "\n"
                + "T=" + tK.ToString("0") + "K (" + tC.ToString("0.0") + "C)\n"
                + "P=" + p.ToString("0.000") + "atm\n"
                + "P/T=" + pt.ToString("0.00000") + " atm/K\n"
                + "V=" + VolumeMl.ToString("0") + " mL (fixed)\n"
                + "N/P temp\n"
                + "[ideal isochoric]";
        }

        Transform EnsurePrimitive(string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Quaternion localRot)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(transform, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.Destroy(col);
                t = go.transform;
            }
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = localScale;
            return t;
        }

        static Material EnsureMat(ref Material mat, Shader sh, Color color, float metallic, float smooth, float emit = 0f)
        {
            if (mat == null)
                mat = new Material(sh);
            SetMatColor(mat, color, emit);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smooth);
            return mat;
        }

        static void SetMatColor(Material mat, Color color, float emit = 0f)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_EmissionColor"))
            {
                if (emit > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * emit);
                }
                else
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        static void ApplyMat(Transform t, Material mat)
        {
            if (t == null || mat == null)
                return;
            var r = t.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = mat;
        }
    }
}
