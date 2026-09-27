using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Boyle's Law syringe: ideal isothermal P*V = k (fixed T).
    /// Honesty: ideal-gas isothermal - NOT real syringe friction, leak, temperature change, or non-ideal gas.
    /// XR activate / N cycles volume presets; P steps backward. Desktop via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadBoyleLawGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_BoyleLaw";
        public const string Honesty =
            "Ideal isothermal Boyle P*V=k (fixed T). NOT real syringe friction/leak, not temperature change, not non-ideal gas.";

        // Volume presets (mL) - lab syringe scale
        static readonly float[] PresetsVMl = { 10f, 20f, 40f, 80f };
        static readonly string[] PresetVLabels = { "SMALL", "MED", "LARGE", "XL" };

        // At MED (20 mL) fix P = 101.325 kPa (1 atm) -> k = P*V
        const float RefPressureKPa = 101.325f;
        const float RefVolumeMl = 20f;
        const float PVkPaMl = RefPressureKPa * RefVolumeMl; // constant

        // Visual layout (local)
        const float BodyRadius = 0.035f;
        const float BodyLenMax = 0.28f;
        const float BodyLocalY = 0.16f;
        const float PlungerTravel = 0.18f;

        TextMeshPro _readout;
        Transform _stand;
        Transform _body;
        Transform _plunger;
        Transform _gas;
        Transform _tip;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _bodyMat;
        Material _plungerMat;
        Material _gasMat;
        Material _tipMat;
        float _refreshAt;
        float _inputCooldown;
        int _vIndex = 1; // default MED

        public float VolumeMl => PresetsVMl[Mathf.Clamp(_vIndex, 0, PresetsVMl.Length - 1)];
        public float PressureKPa => VolumeMl > 1e-6f ? PVkPaMl / VolumeMl : 0f;
        public float PressureAtm => PressureKPa / RefPressureKPa;
        public float ProductPVkPaMl => PressureKPa * VolumeMl;
        public string ActiveVLabel => PresetVLabels[Mathf.Clamp(_vIndex, 0, PresetVLabels.Length - 1)];
        public float PlungerFraction
        {
            get
            {
                float vmin = PresetsVMl[0];
                float vmax = PresetsVMl[PresetsVMl.Length - 1];
                return Mathf.InverseLerp(vmin, vmax, VolumeMl);
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
            ApplyPlungerPose();
            ApplyGasLook();
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
            DestroyMat(ref _plungerMat);
            DestroyMat(ref _gasMat);
            DestroyMat(ref _tipMat);
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
            ApplyPlungerPose();
            ApplyGasLook();
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
            CycleVolume(+1);
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
                CycleVolume(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                CycleVolume(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleVolume(int delta)
        {
            if (PresetsVMl.Length == 0)
                return;
            _vIndex = (_vIndex + delta) % PresetsVMl.Length;
            if (_vIndex < 0)
                _vIndex += PresetsVMl.Length;
            ApplyPlungerPose();
            ApplyGasLook();
            RefreshText();
        }

        public void CyclePreset(int delta) => CycleVolume(delta);

        public void DesktopActivate(int delta)
        {
            CycleVolume(delta == 0 ? 1 : delta);
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _stand = EnsurePrimitive("Stand", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f),
                new Vector3(0.12f, 0.04f, 0.12f), Quaternion.identity);
            _standMat = EnsureMat(ref _standMat, sh, new Color(0.12f, 0.14f, 0.16f, 1f), 0.35f, 0.45f);
            ApplyMat(_stand, _standMat);

            // Vertical syringe barrel (cylinder along Y)
            _body = EnsurePrimitive("Body", PrimitiveType.Cylinder, new Vector3(0f, BodyLocalY, 0f),
                new Vector3(BodyRadius * 2f, BodyLenMax * 0.5f, BodyRadius * 2f), Quaternion.identity);
            _bodyMat = EnsureMat(ref _bodyMat, sh, new Color(0.55f, 0.72f, 0.78f, 1f), 0.15f, 0.65f, emit: 0.08f);
            ApplyMat(_body, _bodyMat);

            _tip = EnsurePrimitive("Tip", PrimitiveType.Cylinder, new Vector3(0f, BodyLocalY + BodyLenMax * 0.5f + 0.025f, 0f),
                new Vector3(0.018f, 0.03f, 0.018f), Quaternion.identity);
            _tipMat = EnsureMat(ref _tipMat, sh, new Color(0.35f, 0.38f, 0.42f, 1f), 0.55f, 0.5f);
            ApplyMat(_tip, _tipMat);

            // Gas column inside barrel - length/position follow volume
            _gas = EnsurePrimitive("Gas", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(BodyRadius * 1.6f, 0.05f, BodyRadius * 1.6f), Quaternion.identity);
            _gasMat = EnsureMat(ref _gasMat, sh, new Color(0.25f, 0.85f, 0.95f, 1f), 0.05f, 0.4f, emit: 0.55f);
            ApplyMat(_gas, _gasMat);

            // Plunger head + rod (moves with volume)
            _plunger = EnsurePrimitive("Plunger", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(BodyRadius * 1.85f, 0.012f, BodyRadius * 1.85f), Quaternion.identity);
            _plungerMat = EnsureMat(ref _plungerMat, sh, new Color(0.85f, 0.35f, 0.28f, 1f), 0.2f, 0.55f, emit: 0.25f);
            ApplyMat(_plunger, _plungerMat);

            Transform rod = EnsurePrimitive("PlungerRod", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.016f, 0.08f, 0.016f), Quaternion.identity);
            ApplyMat(rod, _plungerMat);
        }

        void ApplyPlungerPose()
        {
            if (_plunger == null || _gas == null)
                return;

            // Fraction 0 = SMALL (compressed, high P) -> plunger near tip; 1 = XL -> plunger near base
            float t = PlungerFraction;
            float bodyBottom = BodyLocalY - BodyLenMax * 0.5f;
            float bodyTop = BodyLocalY + BodyLenMax * 0.5f;
            // Gas fills from tip downward; larger V -> taller gas column
            float gasLen = Mathf.Lerp(0.04f, PlungerTravel, t);
            float gasCenterY = bodyTop - gasLen * 0.5f - 0.01f;
            _gas.localPosition = new Vector3(0f, gasCenterY, 0f);
            _gas.localScale = new Vector3(BodyRadius * 1.6f, gasLen * 0.5f, BodyRadius * 1.6f);

            float plungerY = gasCenterY - gasLen * 0.5f - 0.01f;
            plungerY = Mathf.Max(plungerY, bodyBottom + 0.02f);
            _plunger.localPosition = new Vector3(0f, plungerY, 0f);

            Transform rod = transform.Find("PlungerRod");
            if (rod != null)
            {
                float rodLen = Mathf.Max(0.06f, plungerY - bodyBottom);
                rod.localPosition = new Vector3(0f, plungerY - rodLen * 0.5f, 0f);
                rod.localScale = new Vector3(0.016f, rodLen * 0.5f, 0.016f);
            }
        }

        void ApplyGasLook()
        {
            if (_gasMat == null)
                return;
            // Higher P -> hotter cyan/white emission
            float pNorm = Mathf.InverseLerp(PVkPaMl / PresetsVMl[PresetsVMl.Length - 1], PVkPaMl / PresetsVMl[0], PressureKPa);
            Color c = Color.Lerp(new Color(0.2f, 0.55f, 0.85f, 1f), new Color(0.55f, 0.95f, 1f, 1f), pNorm);
            SetMatColor(_gasMat, c, emit: 0.35f + 1.1f * pNorm);
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
            _readout.color = new Color(0.65f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(48f, 28f);
            _readout.text = "BOYLE\nidle";
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
            float pKpa = PressureKPa;
            float pAtm = PressureAtm;
            float pv = ProductPVkPaMl;
            _readout.text =
                "BOYLE " + ActiveVLabel + "\n"
                + "V=" + v.ToString("0") + "mL\n"
                + "P=" + pKpa.ToString("0.0") + "kPa (" + pAtm.ToString("0.00") + "atm)\n"
                + "PV=" + pv.ToString("0.0") + " kPa*mL\n"
                + "N/P volume\n"
                + "[ideal isothermal]";
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