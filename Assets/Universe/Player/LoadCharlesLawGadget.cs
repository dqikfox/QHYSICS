using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Charles's Law heated volume: ideal isobaric V/T = k (fixed P).
    /// Honesty: ideal-gas isobaric - NOT Boyle (isothermal), not real gas, not phase change.
    /// XR activate / N cycles temperature presets; P steps backward. Desktop via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadCharlesLawGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_CharlesLaw";
        public const string Honesty =
            "Ideal isobaric Charles V/T=k (fixed P). NOT Boyle (isothermal), not real gas, not phase change.";

        // Absolute temperature presets (K)
        static readonly float[] PresetsTK = { 250f, 293f, 350f, 400f };
        static readonly string[] PresetTLabels = { "COLD", "ROOM", "WARM", "HOT" };

        // At ROOM (293 K) fix V0 = 20 mL, P = 1 atm -> V = V0 * (T / TRef)
        const float RefTempK = 293f;
        const float RefVolumeMl = 20f;
        const float PressureAtm = 1f;

        // Visual layout (local)
        const float BodyRadius = 0.035f;
        const float BodyLenMax = 0.28f;
        const float BodyLocalY = 0.16f;
        const float GasTravel = 0.18f;

        TextMeshPro _readout;
        Transform _stand;
        Transform _body;
        Transform _gas;
        Transform _cap;
        Transform _heater;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _bodyMat;
        Material _gasMat;
        Material _capMat;
        Material _heaterMat;
        float _refreshAt;
        float _inputCooldown;
        int _tIndex = 1; // default ROOM

        public float TempK => PresetsTK[Mathf.Clamp(_tIndex, 0, PresetsTK.Length - 1)];
        public float TempC => TempK - 273.15f;
        public float VolumeMl => RefVolumeMl * (TempK / RefTempK);
        public float VTConstant => TempK > 1e-6f ? VolumeMl / TempK : 0f;
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

            // Vertical cylinder body (heated volume chamber)
            _body = EnsurePrimitive("Body", PrimitiveType.Cylinder, new Vector3(0f, BodyLocalY, 0f),
                new Vector3(BodyRadius * 2f, BodyLenMax * 0.5f, BodyRadius * 2f), Quaternion.identity);
            _bodyMat = EnsureMat(ref _bodyMat, sh, new Color(0.72f, 0.58f, 0.42f, 1f), 0.15f, 0.65f, emit: 0.08f);
            ApplyMat(_body, _bodyMat);

            _cap = EnsurePrimitive("Cap", PrimitiveType.Cylinder, new Vector3(0f, BodyLocalY + BodyLenMax * 0.5f + 0.015f, 0f),
                new Vector3(BodyRadius * 2.1f, 0.015f, BodyRadius * 2.1f), Quaternion.identity);
            _capMat = EnsureMat(ref _capMat, sh, new Color(0.35f, 0.38f, 0.42f, 1f), 0.55f, 0.5f);
            ApplyMat(_cap, _capMat);

            // Gas column - height follows V (Charles V ∝ T)
            _gas = EnsurePrimitive("Gas", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(BodyRadius * 1.6f, 0.05f, BodyRadius * 1.6f), Quaternion.identity);
            _gasMat = EnsureMat(ref _gasMat, sh, new Color(0.95f, 0.55f, 0.25f, 1f), 0.05f, 0.4f, emit: 0.55f);
            ApplyMat(_gas, _gasMat);

            // Heater ring under chamber - glow intensifies with T
            _heater = EnsurePrimitive("Heater", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0f),
                new Vector3(0.10f, 0.012f, 0.10f), Quaternion.identity);
            _heaterMat = EnsureMat(ref _heaterMat, sh, new Color(0.85f, 0.35f, 0.12f, 1f), 0.2f, 0.55f, emit: 0.4f);
            ApplyMat(_heater, _heaterMat);
        }

        void ApplyGasPose()
        {
            if (_gas == null)
                return;

            // Fraction 0 = COLD (small V) -> short column; 1 = HOT -> tall column
            float t = TempFraction;
            float bodyBottom = BodyLocalY - BodyLenMax * 0.5f;
            float gasLen = Mathf.Lerp(0.04f, GasTravel, t);
            float gasCenterY = bodyBottom + gasLen * 0.5f + 0.02f;
            _gas.localPosition = new Vector3(0f, gasCenterY, 0f);
            _gas.localScale = new Vector3(BodyRadius * 1.6f, gasLen * 0.5f, BodyRadius * 1.6f);
        }

        void ApplyHeatLook()
        {
            float tNorm = TempFraction;
            if (_gasMat != null)
            {
                // Cool blue -> warm amber/orange as T rises
                Color c = Color.Lerp(new Color(0.35f, 0.55f, 0.95f, 1f), new Color(1f, 0.45f, 0.15f, 1f), tNorm);
                SetMatColor(_gasMat, c, emit: 0.35f + 1.2f * tNorm);
            }
            if (_heaterMat != null)
            {
                Color h = Color.Lerp(new Color(0.45f, 0.22f, 0.12f, 1f), new Color(1f, 0.35f, 0.08f, 1f), tNorm);
                SetMatColor(_heaterMat, h, emit: 0.2f + 1.4f * tNorm);
            }
            if (_bodyMat != null)
            {
                Color b = Color.Lerp(new Color(0.55f, 0.58f, 0.62f, 1f), new Color(0.85f, 0.52f, 0.32f, 1f), tNorm * 0.7f);
                SetMatColor(_bodyMat, b, emit: 0.05f + 0.35f * tNorm);
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
            _readout.color = new Color(1f, 0.78f, 0.45f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(48f, 30f);
            _readout.text = "CHARLES\nidle";
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
            float v = VolumeMl;
            float tK = TempK;
            float tC = TempC;
            float vt = VTConstant;
            _readout.text =
                "CHARLES " + ActiveTLabel + "\n"
                + "T=" + tK.ToString("0") + "K (" + tC.ToString("0.0") + "C)\n"
                + "V=" + v.ToString("0.0") + "mL\n"
                + "V/T=" + vt.ToString("0.0000") + " mL/K\n"
                + "P=" + PressureAtm.ToString("0") + " atm (fixed)\n"
                + "N/P temp\n"
                + "[ideal isobaric]";
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
