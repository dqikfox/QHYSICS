using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Atwood machine: ideal two-mass pulley a = g(m1-m2)/(m1+m2), T = 2 m1 m2 g/(m1+m2).
    /// Honesty: lumped ideal Atwood — NOT real pulley inertia, not string mass, not friction, not air drag, not 3D swinging.
    /// XR activate / N cycles mass presets; P steps backward. Shift+N/P cycles damp. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadAtwoodGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Atwood";
        public const string Honesty =
            "Lumped ideal Atwood a=g(m1-m2)/(m1+m2). NOT real pulley inertia, not string mass, not friction, not air drag, not 3D swinging.";

        // Mass pair presets (m1, m2) kg — m1 is left / "down-positive" side
        static readonly float[] PresetsM1 = { 0.20f, 0.25f, 0.30f, 0.40f };
        static readonly float[] PresetsM2 = { 0.20f, 0.15f, 0.15f, 0.10f };
        static readonly string[] PresetLabels = { "EQ", "LIGHT", "MED", "HEAVY" };

        // Linear damping on velocity (1/s) — optional second axis
        static readonly float[] PresetsC = { 0f, 0.4f, 2.5f };
        static readonly string[] PresetCLabels = { "OFF", "LIGHT", "HEAVY" };

        const float Gravity = 9.81f;
        const float CordHalfLen = 0.28f;   // half of total free cord (each side travel budget)
        const float PulleyLocalY = 0.55f;
        const float MassSepX = 0.12f;      // half-width between hanging masses
        const float FloorClearance = 0.06f;
        const float SoftStop = 0.02f;

        TextMeshPro _readout;
        Transform _stand;
        Transform _post;
        Transform _pulley;
        Transform _mass1;
        Transform _mass2;
        LineRenderer _cord;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _pulleyMat;
        Material _mass1Mat;
        Material _mass2Mat;
        float _x;       // displacement of m1 downward from equal-hang (m); m2 goes up by same
        float _v;
        float _refreshAt;
        float _inputCooldown;
        int _presetIndex = 1; // default LIGHT
        int _cIndex = 1;      // default LIGHT damp
        bool _started;

        public float Acceleration => ComputeA();
        public float Tension => ComputeT();
        public float Mass1 => ActiveM1;
        public float Mass2 => ActiveM2;
        public float Velocity => _v;
        public float Displacement => _x;
        public float ActiveM1 => PresetsM1[Mathf.Clamp(_presetIndex, 0, PresetsM1.Length - 1)];
        public float ActiveM2 => PresetsM2[Mathf.Clamp(_presetIndex, 0, PresetsM2.Length - 1)];
        public float ActiveC => PresetsC[Mathf.Clamp(_cIndex, 0, PresetsC.Length - 1)];
        public string ActivePresetLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        public string ActiveCLabel => PresetCLabels[Mathf.Clamp(_cIndex, 0, PresetCLabels.Length - 1)];

        float ComputeA()
        {
            float m1 = ActiveM1;
            float m2 = ActiveM2;
            float sum = m1 + m2;
            if (sum < 1e-6f)
                return 0f;
            return Gravity * (m1 - m2) / sum;
        }

        float ComputeT()
        {
            float m1 = ActiveM1;
            float m2 = ActiveM2;
            float sum = m1 + m2;
            if (sum < 1e-6f)
                return 0f;
            return 2f * m1 * m2 * Gravity / sum;
        }

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            if (!_started)
            {
                // Slight offset so unequal masses start moving immediately; EQ sits still.
                _x = ActiveM1 > ActiveM2 + 1e-4f ? 0.04f : (ActiveM1 < ActiveM2 - 1e-4f ? -0.04f : 0f);
                _v = 0f;
                _started = true;
            }
            ApplyVisualPose();
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
            DestroyMat(ref _pulleyMat);
            DestroyMat(ref _mass1Mat);
            DestroyMat(ref _mass2Mat);
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

            float a = ComputeA() - ActiveC * _v;
            _v += a * dt;
            _x += _v * dt;

            // Soft end-stops: masses must not pass through pulley or floor
            float xMax = CordHalfLen - SoftStop;
            float xMin = -(CordHalfLen - SoftStop);
            // Also keep mass bottoms above floor clearance visually
            float floorCap = PulleyLocalY - FloorClearance - 0.04f; // approx mass half-size
            float travelCap = Mathf.Min(xMax, CordHalfLen);
            travelCap = Mathf.Min(travelCap, floorCap - SoftStop);
            if (_x > travelCap)
            {
                _x = travelCap;
                if (_v > 0f) _v = 0f;
            }
            else if (_x < -travelCap)
            {
                _x = -travelCap;
                if (_v < 0f) _v = 0f;
            }

            ApplyVisualPose();
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
            CyclePreset(+1);
            Nudge();
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
                if (shift) CycleDamp(+1);
                else CyclePreset(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleDamp(-1);
                else CyclePreset(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CyclePreset(int delta)
        {
            if (PresetLabels.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % PresetLabels.Length;
            if (_presetIndex < 0)
                _presetIndex += PresetLabels.Length;
            Nudge();
            RefreshText();
        }

        public void CycleDamp(int delta)
        {
            if (PresetsC.Length == 0)
                return;
            _cIndex = (_cIndex + delta) % PresetsC.Length;
            if (_cIndex < 0)
                _cIndex += PresetsC.Length;
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            CyclePreset(delta);
        }

        void Nudge()
        {
            // Re-excite so preset changes stay playable when parked at an end-stop or EQ rest.
            if (Mathf.Abs(_v) < 0.05f && Mathf.Abs(_x) < 0.02f)
            {
                if (Mathf.Abs(ActiveM1 - ActiveM2) > 1e-4f)
                    _x = ActiveM1 > ActiveM2 ? 0.03f : -0.03f;
                _v = 0f;
            }
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _stand = transform.Find("Stand");
            if (_stand == null)
            {
                var standGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                standGo.name = "Stand";
                standGo.transform.SetParent(transform, false);
                standGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                standGo.transform.localRotation = Quaternion.identity;
                standGo.transform.localScale = new Vector3(0.22f, 0.04f, 0.14f);
                Object.Destroy(standGo.GetComponent<Collider>());
                _stand = standGo.transform;
            }
            var sr = _stand.GetComponent<Renderer>();
            if (sr != null)
            {
                if (_standMat == null)
                {
                    _standMat = new Material(sh);
                    var dark = new Color(0.12f, 0.14f, 0.16f, 1f);
                    if (_standMat.HasProperty("_BaseColor"))
                        _standMat.SetColor("_BaseColor", dark);
                    _standMat.color = dark;
                    if (_standMat.HasProperty("_Metallic"))
                        _standMat.SetFloat("_Metallic", 0.35f);
                    if (_standMat.HasProperty("_Smoothness"))
                        _standMat.SetFloat("_Smoothness", 0.45f);
                }
                sr.sharedMaterial = _standMat;
            }

            _post = transform.Find("Post");
            if (_post == null)
            {
                var postGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postGo.name = "Post";
                postGo.transform.SetParent(transform, false);
                postGo.transform.localPosition = new Vector3(0f, PulleyLocalY * 0.5f, 0f);
                postGo.transform.localRotation = Quaternion.identity;
                postGo.transform.localScale = new Vector3(0.028f, PulleyLocalY, 0.028f);
                Object.Destroy(postGo.GetComponent<Collider>());
                _post = postGo.transform;
            }
            var pr = _post.GetComponent<Renderer>();
            if (pr != null && _standMat != null)
                pr.sharedMaterial = _standMat;

            // Cross-arm under pulley
            Transform arm = transform.Find("Arm");
            if (arm == null)
            {
                var armGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                armGo.name = "Arm";
                armGo.transform.SetParent(transform, false);
                armGo.transform.localPosition = new Vector3(0f, PulleyLocalY - 0.01f, 0f);
                armGo.transform.localRotation = Quaternion.identity;
                armGo.transform.localScale = new Vector3(MassSepX * 2.2f, 0.018f, 0.018f);
                Object.Destroy(armGo.GetComponent<Collider>());
                arm = armGo.transform;
            }
            var ar = arm.GetComponent<Renderer>();
            if (ar != null && _standMat != null)
                ar.sharedMaterial = _standMat;

            _pulley = transform.Find("Pulley");
            if (_pulley == null)
            {
                var pulleyGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pulleyGo.name = "Pulley";
                pulleyGo.transform.SetParent(transform, false);
                pulleyGo.transform.localPosition = new Vector3(0f, PulleyLocalY, 0f);
                pulleyGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pulleyGo.transform.localScale = new Vector3(0.07f, 0.012f, 0.07f);
                Object.Destroy(pulleyGo.GetComponent<Collider>());
                _pulley = pulleyGo.transform;
            }
            var pvr = _pulley.GetComponent<Renderer>();
            if (pvr != null)
            {
                if (_pulleyMat == null)
                {
                    _pulleyMat = new Material(sh);
                    var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
                    if (_pulleyMat.HasProperty("_BaseColor"))
                        _pulleyMat.SetColor("_BaseColor", cyan);
                    _pulleyMat.color = cyan;
                    if (_pulleyMat.HasProperty("_Metallic"))
                        _pulleyMat.SetFloat("_Metallic", 0.55f);
                    if (_pulleyMat.HasProperty("_Smoothness"))
                        _pulleyMat.SetFloat("_Smoothness", 0.6f);
                    if (_pulleyMat.HasProperty("_EmissionColor"))
                    {
                        _pulleyMat.EnableKeyword("_EMISSION");
                        _pulleyMat.SetColor("_EmissionColor", cyan * 0.3f);
                    }
                }
                pvr.sharedMaterial = _pulleyMat;
            }

            _mass1 = transform.Find("Mass1");
            if (_mass1 == null)
            {
                var m1Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m1Go.name = "Mass1";
                m1Go.transform.SetParent(transform, false);
                m1Go.transform.localPosition = new Vector3(-MassSepX, PulleyLocalY - CordHalfLen, 0f);
                m1Go.transform.localRotation = Quaternion.identity;
                m1Go.transform.localScale = new Vector3(0.055f, 0.055f, 0.055f);
                Object.Destroy(m1Go.GetComponent<Collider>());
                _mass1 = m1Go.transform;
            }
            var m1r = _mass1.GetComponent<Renderer>();
            if (m1r != null)
            {
                if (_mass1Mat == null)
                {
                    _mass1Mat = new Material(sh);
                    var teal = new Color(0.15f, 0.65f, 0.72f, 1f);
                    if (_mass1Mat.HasProperty("_BaseColor"))
                        _mass1Mat.SetColor("_BaseColor", teal);
                    _mass1Mat.color = teal;
                    if (_mass1Mat.HasProperty("_Metallic"))
                        _mass1Mat.SetFloat("_Metallic", 0.4f);
                    if (_mass1Mat.HasProperty("_Smoothness"))
                        _mass1Mat.SetFloat("_Smoothness", 0.5f);
                }
                m1r.sharedMaterial = _mass1Mat;
            }

            _mass2 = transform.Find("Mass2");
            if (_mass2 == null)
            {
                var m2Go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m2Go.name = "Mass2";
                m2Go.transform.SetParent(transform, false);
                m2Go.transform.localPosition = new Vector3(MassSepX, PulleyLocalY - CordHalfLen, 0f);
                m2Go.transform.localRotation = Quaternion.identity;
                m2Go.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                Object.Destroy(m2Go.GetComponent<Collider>());
                _mass2 = m2Go.transform;
            }
            var m2r = _mass2.GetComponent<Renderer>();
            if (m2r != null)
            {
                if (_mass2Mat == null)
                {
                    _mass2Mat = new Material(sh);
                    var steel = new Color(0.45f, 0.52f, 0.58f, 1f);
                    if (_mass2Mat.HasProperty("_BaseColor"))
                        _mass2Mat.SetColor("_BaseColor", steel);
                    _mass2Mat.color = steel;
                    if (_mass2Mat.HasProperty("_Metallic"))
                        _mass2Mat.SetFloat("_Metallic", 0.65f);
                    if (_mass2Mat.HasProperty("_Smoothness"))
                        _mass2Mat.SetFloat("_Smoothness", 0.55f);
                }
                m2r.sharedMaterial = _mass2Mat;
            }

            EnsureCord(sh);
            ApplyMassScales();
        }

        void EnsureCord(Shader sh)
        {
            Transform cordT = transform.Find("Cord");
            GameObject cordGo;
            if (cordT != null)
                cordGo = cordT.gameObject;
            else
            {
                cordGo = new GameObject("Cord");
                cordGo.transform.SetParent(transform, false);
                cordGo.transform.localPosition = Vector3.zero;
                cordGo.transform.localRotation = Quaternion.identity;
                cordGo.transform.localScale = Vector3.one;
            }

            _cord = cordGo.GetComponent<LineRenderer>();
            if (_cord == null)
                _cord = cordGo.AddComponent<LineRenderer>();
            _cord.positionCount = 3;
            _cord.useWorldSpace = false;
            _cord.startWidth = 0.006f;
            _cord.endWidth = 0.006f;
            _cord.numCapVertices = 2;
            if (_cord.sharedMaterial == null)
            {
                var cordMat = new Material(sh);
                var cyan = new Color(0.25f, 0.75f, 0.85f, 1f);
                if (cordMat.HasProperty("_BaseColor"))
                    cordMat.SetColor("_BaseColor", cyan);
                cordMat.color = cyan;
                if (cordMat.HasProperty("_EmissionColor"))
                {
                    cordMat.EnableKeyword("_EMISSION");
                    cordMat.SetColor("_EmissionColor", cyan * 0.2f);
                }
                _cord.sharedMaterial = cordMat;
            }
        }

        void ApplyMassScales()
        {
            // Visual size cues from mass (cube edge ~ cbrt)
            if (_mass1 != null)
            {
                float s1 = 0.045f + 0.04f * Mathf.Clamp01(ActiveM1 / 0.45f);
                _mass1.localScale = new Vector3(s1, s1, s1);
            }
            if (_mass2 != null)
            {
                float s2 = 0.045f + 0.04f * Mathf.Clamp01(ActiveM2 / 0.45f);
                _mass2.localScale = new Vector3(s2, s2, s2);
            }
        }

        void ApplyVisualPose()
        {
            if (_mass1 == null || _mass2 == null)
                return;

            float y1 = PulleyLocalY - CordHalfLen - _x;
            float y2 = PulleyLocalY - CordHalfLen + _x;
            // Clamp visual Y above floor
            y1 = Mathf.Max(FloorClearance, y1);
            y2 = Mathf.Max(FloorClearance, y2);

            _mass1.localPosition = new Vector3(-MassSepX, y1, 0f);
            _mass2.localPosition = new Vector3(MassSepX, y2, 0f);
            ApplyMassScales();

            if (_cord != null)
            {
                _cord.SetPosition(0, new Vector3(-MassSepX, y1 + 0.02f, 0f));
                _cord.SetPosition(1, new Vector3(0f, PulleyLocalY, 0f));
                _cord.SetPosition(2, new Vector3(MassSepX, y2 + 0.02f, 0f));
            }

            // Spin pulley slightly with motion
            if (_pulley != null)
            {
                float spin = _x * 180f;
                _pulley.localRotation = Quaternion.Euler(spin, 0f, 90f);
            }
        }

        void ApplyGlow()
        {
            if (_pulleyMat == null || !_pulleyMat.HasProperty("_EmissionColor"))
                return;
            float aAbs = Mathf.Clamp01(Mathf.Abs(ComputeA()) / Gravity);
            var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
            _pulleyMat.EnableKeyword("_EMISSION");
            _pulleyMat.SetColor("_EmissionColor", cyan * (0.2f + 1.2f * aAbs));
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
                go.transform.localPosition = new Vector3(0f, PulleyLocalY + 0.12f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 22f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.65f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(48f, 26f);
            _readout.text = "ATWOOD\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            float a = ComputeA();
            float t = ComputeT();
            string label = ActivePresetLabel;
            string cLabel = ActiveCLabel;
            string aStr = "a " + a.ToString("+0.00;-0.00;0.00") + "m/s2";
            string tStr = "T " + t.ToString("0.000") + "N";
            string vStr = "v " + _v.ToString("+0.00;-0.00;0.00") + "m/s";
            string dhStr = "dh " + _x.ToString("+0.000;-0.000;0.000") + "m";
            string mStr = "m1=" + ActiveM1.ToString("0.00") + " m2=" + ActiveM2.ToString("0.00");

            _readout.text =
                "ATWOOD " + label + "/" + cLabel + "\n"
                + aStr + "  " + tStr + "\n"
                + vStr + "  " + dhStr + "\n"
                + mStr + "\n"
                + "N/P mass  Shift damp\n"
                + "[ideal Atwood]";
        }
    }
}
