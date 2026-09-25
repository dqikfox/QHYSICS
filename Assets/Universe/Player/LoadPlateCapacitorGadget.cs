using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS parallel-plate capacitor / RC demo: ideal C with optional series R charge/discharge.
    /// Honesty: lumped RC — V(t) approaches Vtarget via tau=RC; Q=CV. NOT dielectric physics, not ESR/ESL,
    /// not fringe fields, not breakdown, not live InductionCircuit wiring (see BUILD Capacitor for that).
    /// XR activate / desktop LMB cycles CHARGE target V (0/5/12/24). N/P cycles C; Shift+N/P cycles R.
    /// R=OPEN holds charge. Distinct from CIRCUIT LoadCapacitorGadget (Gadget_Capacitor on InductionCoil).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadPlateCapacitorGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_PlateCapacitor";
        public const string Honesty =
            "Ideal lumped RC: Q=CV, tau=RC, V(t)->Vtarget via exp. NOT dielectric, not ESR/ESL, not fringe, not breakdown, not InductionCircuit C (use BUILD Capacitor).";

        // Capacitance presets (F) — conceptual lab values, labeled clearly
        static readonly float[] PresetsC = { 1e-6f, 10e-6f, 100e-6f };
        static readonly string[] PresetCLabels = { "SMALL", "MED", "LARGE" };
        static readonly string[] PresetCSi = { "1uF", "10uF", "100uF" };

        // Series R presets (ohm). OPEN = hold (no current path).
        static readonly float[] PresetsR = { float.PositiveInfinity, 5e4f, 1e5f, 5e5f };
        static readonly string[] PresetRLabels = { "OPEN", "LIGHT", "MED", "HEAVY" };
        static readonly string[] PresetRSi = { "OPEN", "50kOhm", "100kOhm", "500kOhm" };

        // Charge target voltages (V)
        static readonly float[] PresetsV = { 0f, 5f, 12f, 24f };
        static readonly string[] PresetVLabels = { "0V", "5V", "12V", "24V" };

        const float Vmax = 24f;
        const float SettleEps = 0.02f; // V
        const float PlateLocalY = 0.16f;
        const float PlateHalfGap = 0.035f;
        const float PlateW = 0.18f;
        const float PlateH = 0.14f;
        const float PlateT = 0.012f;

        enum CapState { Hold, Charging, Discharge }

        TextMeshPro _readout;
        Transform _base;
        Transform _post;
        Transform _plateA;
        Transform _plateB;
        Transform _gapGlow;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _plateMat;
        Material _glowMat;
        float _v;
        float _refreshAt;
        float _inputCooldown;
        int _cIndex = 1; // default MED 10uF
        int _rIndex = 0; // default OPEN (hold)
        int _vIndex = 2; // default 12V target
        CapState _state = CapState.Hold;

        public float CapacitanceFarads => ActiveC;
        public float ResistanceOhms => ActiveR;
        public float VoltageVolts => _v;
        public float ChargeCoulombs => ActiveC * _v;
        public float TauSeconds => IsOpen ? float.PositiveInfinity : ActiveR * ActiveC;
        public float TargetVolts => ActiveVTarget;
        public string StateLabel => StateName(_state);
        public float ActiveC => PresetsC[Mathf.Clamp(_cIndex, 0, PresetsC.Length - 1)];
        public float ActiveR => PresetsR[Mathf.Clamp(_rIndex, 0, PresetsR.Length - 1)];
        public float ActiveVTarget => PresetsV[Mathf.Clamp(_vIndex, 0, PresetsV.Length - 1)];
        public string ActiveCLabel => PresetCLabels[Mathf.Clamp(_cIndex, 0, PresetCLabels.Length - 1)];
        public string ActiveRLabel => PresetRLabels[Mathf.Clamp(_rIndex, 0, PresetRLabels.Length - 1)];
        public string ActiveVLabel => PresetVLabels[Mathf.Clamp(_vIndex, 0, PresetVLabels.Length - 1)];
        public bool IsOpen => float.IsInfinity(ActiveR) || ActiveR > 1e12f;

        static string StateName(CapState s)
        {
            switch (s)
            {
                case CapState.Charging: return "CHARGING";
                case CapState.Discharge: return "DISCHARGE";
                default: return "HOLD";
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
            ApplyGlow();
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
            DestroyMat(ref _baseMat);
            DestroyMat(ref _plateMat);
            DestroyMat(ref _glowMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
                return;

            if (IsOpen)
            {
                // OPEN = hold charge (no RC path)
                if (_state != CapState.Hold)
                    _state = CapState.Hold;
                ApplyGlow();
                return;
            }

            float tau = Mathf.Max(1e-6f, ActiveR * ActiveC);
            float target = ActiveVTarget;

            if (_state == CapState.Hold)
            {
                // With R connected and no drive: if V drifts from target intent, leave as HOLD only when settled
                ApplyGlow();
                return;
            }

            if (_state == CapState.Charging)
            {
                // V(t) = Vtarget + (V0 - Vtarget) e^{-t/RC}  =>  dV/dt = (Vtarget - V)/tau
                _v += (target - _v) * (1f - Mathf.Exp(-dt / tau));
                if (Mathf.Abs(_v - target) < SettleEps)
                {
                    _v = target;
                    _state = CapState.Hold;
                }
            }
            else if (_state == CapState.Discharge)
            {
                // V(t) = V0 e^{-t/RC} toward 0
                _v *= Mathf.Exp(-dt / tau);
                if (Mathf.Abs(_v) < SettleEps)
                {
                    _v = 0f;
                    _state = CapState.Hold;
                }
            }

            ApplyGlow();
        }

        void Update()
        {
            PollDesktopCycle();
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.06f;
            RefreshText();
            ApplyGlow();
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
            CycleChargeTarget(+1);
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

            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.nKey.wasPressedThisFrame)
            {
                if (shift) CycleR(+1);
                else CycleC(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleR(-1);
                else CycleC(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleC(int delta)
        {
            if (PresetsC.Length == 0)
                return;
            _cIndex = (_cIndex + delta) % PresetsC.Length;
            if (_cIndex < 0)
                _cIndex += PresetsC.Length;
            // Q=CV conserved conceptually on C change is advanced; keep V (classic continuous voltage)
            RefreshText();
        }

        public void CycleR(int delta)
        {
            if (PresetsR.Length == 0)
                return;
            _rIndex = (_rIndex + delta) % PresetsR.Length;
            if (_rIndex < 0)
                _rIndex += PresetsR.Length;

            if (IsOpen)
            {
                _state = CapState.Hold;
            }
            else if (Mathf.Abs(ActiveVTarget) < 1e-4f && Mathf.Abs(_v) > SettleEps)
            {
                _state = CapState.Discharge;
            }
            else if (Mathf.Abs(_v - ActiveVTarget) > SettleEps)
            {
                _state = ActiveVTarget >= _v ? CapState.Charging : CapState.Discharge;
                if (ActiveVTarget < 1e-4f)
                    _state = CapState.Discharge;
                else
                    _state = CapState.Charging;
            }
            RefreshText();
        }

        public void CycleChargeTarget(int delta)
        {
            if (PresetsV.Length == 0)
                return;
            _vIndex = (_vIndex + delta) % PresetsV.Length;
            if (_vIndex < 0)
                _vIndex += PresetsV.Length;

            float target = ActiveVTarget;
            if (IsOpen)
            {
                // OPEN: instant set + HOLD
                _v = target;
                _state = CapState.Hold;
            }
            else if (Mathf.Abs(target) < 1e-4f)
            {
                _state = CapState.Discharge;
            }
            else
            {
                _state = CapState.Charging;
            }
            RefreshText();
            ApplyGlow();
        }

        public void CyclePreset(int delta) => CycleC(delta);

        public void DesktopActivate(int delta)
        {
            // LMB / scroll / VR activate share CHARGE target V cycle
            CycleChargeTarget(delta == 0 ? 1 : delta);
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _base = transform.Find("Base");
            if (_base == null)
            {
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseGo.name = "Base";
                baseGo.transform.SetParent(transform, false);
                baseGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                baseGo.transform.localRotation = Quaternion.identity;
                baseGo.transform.localScale = new Vector3(0.28f, 0.04f, 0.18f);
                Object.Destroy(baseGo.GetComponent<Collider>());
                _base = baseGo.transform;
            }
            var br = _base.GetComponent<Renderer>();
            if (br != null)
            {
                if (_baseMat == null)
                {
                    _baseMat = new Material(sh);
                    var dark = new Color(0.12f, 0.14f, 0.16f, 1f);
                    if (_baseMat.HasProperty("_BaseColor"))
                        _baseMat.SetColor("_BaseColor", dark);
                    _baseMat.color = dark;
                    if (_baseMat.HasProperty("_Metallic"))
                        _baseMat.SetFloat("_Metallic", 0.35f);
                    if (_baseMat.HasProperty("_Smoothness"))
                        _baseMat.SetFloat("_Smoothness", 0.45f);
                }
                br.sharedMaterial = _baseMat;
            }

            _post = transform.Find("Post");
            if (_post == null)
            {
                var postGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postGo.name = "Post";
                postGo.transform.SetParent(transform, false);
                postGo.transform.localPosition = new Vector3(0f, PlateLocalY * 0.5f, 0f);
                postGo.transform.localRotation = Quaternion.identity;
                postGo.transform.localScale = new Vector3(0.03f, PlateLocalY, 0.03f);
                Object.Destroy(postGo.GetComponent<Collider>());
                _post = postGo.transform;
            }
            var pr = _post.GetComponent<Renderer>();
            if (pr != null && _baseMat != null)
                pr.sharedMaterial = _baseMat;

            if (_plateMat == null)
            {
                _plateMat = new Material(sh);
                var cyan = new Color(0.35f, 0.85f, 0.95f, 1f);
                if (_plateMat.HasProperty("_BaseColor"))
                    _plateMat.SetColor("_BaseColor", cyan);
                _plateMat.color = cyan;
                if (_plateMat.HasProperty("_Metallic"))
                    _plateMat.SetFloat("_Metallic", 0.55f);
                if (_plateMat.HasProperty("_Smoothness"))
                    _plateMat.SetFloat("_Smoothness", 0.7f);
                if (_plateMat.HasProperty("_EmissionColor"))
                {
                    _plateMat.EnableKeyword("_EMISSION");
                    _plateMat.SetColor("_EmissionColor", cyan * 0.2f);
                }
            }

            _plateA = transform.Find("PlateA");
            if (_plateA == null)
            {
                var aGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                aGo.name = "PlateA";
                aGo.transform.SetParent(transform, false);
                aGo.transform.localPosition = new Vector3(-PlateHalfGap, PlateLocalY, 0f);
                aGo.transform.localRotation = Quaternion.identity;
                aGo.transform.localScale = new Vector3(PlateT, PlateH, PlateW);
                Object.Destroy(aGo.GetComponent<Collider>());
                _plateA = aGo.transform;
            }
            var ar = _plateA.GetComponent<Renderer>();
            if (ar != null)
                ar.sharedMaterial = _plateMat;

            _plateB = transform.Find("PlateB");
            if (_plateB == null)
            {
                var bGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bGo.name = "PlateB";
                bGo.transform.SetParent(transform, false);
                bGo.transform.localPosition = new Vector3(PlateHalfGap, PlateLocalY, 0f);
                bGo.transform.localRotation = Quaternion.identity;
                bGo.transform.localScale = new Vector3(PlateT, PlateH, PlateW);
                Object.Destroy(bGo.GetComponent<Collider>());
                _plateB = bGo.transform;
            }
            var brr = _plateB.GetComponent<Renderer>();
            if (brr != null)
                brr.sharedMaterial = _plateMat;

            _gapGlow = transform.Find("GapGlow");
            if (_gapGlow == null)
            {
                var gGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gGo.name = "GapGlow";
                gGo.transform.SetParent(transform, false);
                gGo.transform.localPosition = new Vector3(0f, PlateLocalY, 0f);
                gGo.transform.localRotation = Quaternion.identity;
                gGo.transform.localScale = new Vector3(PlateHalfGap * 1.6f, PlateH * 0.85f, PlateW * 0.85f);
                Object.Destroy(gGo.GetComponent<Collider>());
                _gapGlow = gGo.transform;
            }
            if (_glowMat == null)
            {
                _glowMat = new Material(sh);
                var ghost = new Color(0.25f, 0.75f, 0.95f, 1f);
                if (_glowMat.HasProperty("_BaseColor"))
                    _glowMat.SetColor("_BaseColor", ghost);
                _glowMat.color = ghost;
                if (_glowMat.HasProperty("_EmissionColor"))
                {
                    _glowMat.EnableKeyword("_EMISSION");
                    _glowMat.SetColor("_EmissionColor", ghost * 0.15f);
                }
            }
            var gr = _gapGlow.GetComponent<Renderer>();
            if (gr != null)
                gr.sharedMaterial = _glowMat;
        }

        void ApplyGlow()
        {
            float frac = Mathf.Clamp01(Mathf.Abs(_v) / Vmax);
            var cyan = new Color(0.35f, 0.85f, 0.95f, 1f);
            float plateEmit = 0.12f + frac * 1.35f;
            float gapEmit = 0.05f + frac * 0.95f;

            if (_plateMat != null && _plateMat.HasProperty("_EmissionColor"))
            {
                _plateMat.EnableKeyword("_EMISSION");
                _plateMat.SetColor("_EmissionColor", cyan * plateEmit);
                var baseCol = Color.Lerp(new Color(0.25f, 0.35f, 0.42f, 1f), cyan, 0.35f + 0.55f * frac);
                if (_plateMat.HasProperty("_BaseColor"))
                    _plateMat.SetColor("_BaseColor", baseCol);
                _plateMat.color = baseCol;
            }
            if (_glowMat != null && _glowMat.HasProperty("_EmissionColor"))
            {
                _glowMat.EnableKeyword("_EMISSION");
                _glowMat.SetColor("_EmissionColor", cyan * gapEmit);
                if (_gapGlow != null)
                    _gapGlow.gameObject.SetActive(frac > 0.02f);
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
                go.transform.localPosition = new Vector3(0f, PlateLocalY + 0.16f, 0f);
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
            _readout.rectTransform.sizeDelta = new Vector2(52f, 30f);
            _readout.text = "PLATE CAP\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string cLabel = ActiveCLabel;
            string rLabel = ActiveRLabel;
            string vTLabel = ActiveVLabel;
            string cSi = PresetCSi[Mathf.Clamp(_cIndex, 0, PresetCSi.Length - 1)];
            string rSi = PresetRSi[Mathf.Clamp(_rIndex, 0, PresetRSi.Length - 1)];
            float q = ActiveC * _v;
            float qDisplay = q * 1e6f; // uC
            string tauStr;
            if (IsOpen)
                tauStr = "tau=inf";
            else
                tauStr = "tau=" + TauSeconds.ToString("0.000") + "s";

            _readout.text =
                "CAP " + cLabel + "/" + rLabel + "\n"
                + "C=" + cSi + "  R=" + rSi + "\n"
                + "V=" + _v.ToString("0.00") + "V  Q=" + qDisplay.ToString("0.00") + "uC\n"
                + tauStr + "  tgt=" + vTLabel + "\n"
                + StateLabel + "\n"
                + "N/P C  Shift R  act V\n"
                + "[ideal RC]";
        }
    }
}
