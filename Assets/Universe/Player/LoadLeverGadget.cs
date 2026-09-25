using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS lever / torque balance: ideal massless beam on fulcrum, tau = F * d (perp).
    /// Honesty: ideal statics tau=mgd, g=9.81, point masses at arm ends. NOT beam flex, not fulcrum friction,
    /// not 3D tipping, not distributed beam mass, not dynamic oscillation (light damped tip visual only).
    /// XR activate / N cycles mass pair; P steps back; Shift+N/P cycles fulcrum offset. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadLeverGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Lever";
        public const string Honesty =
            "Ideal statics tau=mgd, point masses on massless beam. NOT beam flex, not fulcrum friction, not 3D tipping, not distributed beam mass.";

        // Mass pair presets (mL, mR) kg
        static readonly float[] PresetsML = { 1.0f, 1.2f, 1.5f, 2.0f };
        static readonly float[] PresetsMR = { 1.0f, 0.8f, 0.5f, 0.5f };
        static readonly string[] PresetMassLabels = { "BAL", "LIGHT", "MED", "HEAVY" };

        // Fulcrum offset: left/right arm lengths (m) — CENTER 1:1, NEAR_L 2:1, FAR_L 3:1, NEAR_R 1:2
        static readonly float[] PresetsDL = { 0.20f, 0.24f, 0.30f, 0.12f };
        static readonly float[] PresetsDR = { 0.20f, 0.12f, 0.10f, 0.24f };
        static readonly string[] PresetFulcrumLabels = { "CENTER", "NEAR_L", "FAR_L", "NEAR_R" };

        const float Gravity = 9.81f;
        const float BalanceEps = 0.05f;   // N*m — BALANCED when |tau_net| < eps
        const float TipAngleMax = 28f;    // deg soft clamp for tip pose
        const float AngleSpring = 18f;    // 1/s^2 toward tip target
        const float AngleDamp = 6f;       // 1/s
        const float BeamThick = 0.02f;
        const float BeamDepth = 0.04f;
        const float FulcrumY = 0.18f;
        const float MassHalf = 0.03f;

        TextMeshPro _readout;
        Transform _stand;
        Transform _post;
        Transform _fulcrum;
        Transform _beam;
        Transform _massL;
        Transform _massR;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _beamMat;
        Material _massLMat;
        Material _massRMat;
        float _thetaDeg;   // beam tip angle about Z (deg); + tips left mass down
        float _omega;      // deg/s
        float _refreshAt;
        float _inputCooldown;
        int _massIndex = 0;     // default BAL
        int _fulcrumIndex = 0;  // default CENTER
        bool _started;

        public float TorqueL => ComputeTauL();
        public float TorqueR => ComputeTauR();
        public float TorqueNet => ComputeTauNet();
        public float ActiveML => PresetsML[Mathf.Clamp(_massIndex, 0, PresetsML.Length - 1)];
        public float ActiveMR => PresetsMR[Mathf.Clamp(_massIndex, 0, PresetsMR.Length - 1)];
        public float ActiveDL => PresetsDL[Mathf.Clamp(_fulcrumIndex, 0, PresetsDL.Length - 1)];
        public float ActiveDR => PresetsDR[Mathf.Clamp(_fulcrumIndex, 0, PresetsDR.Length - 1)];
        public string ActiveMassLabel => PresetMassLabels[Mathf.Clamp(_massIndex, 0, PresetMassLabels.Length - 1)];
        public string ActiveFulcrumLabel => PresetFulcrumLabels[Mathf.Clamp(_fulcrumIndex, 0, PresetFulcrumLabels.Length - 1)];
        public bool IsBalanced => Mathf.Abs(ComputeTauNet()) < BalanceEps;
        public float BeamAngleDeg => _thetaDeg;

        // tau_L = - m_L * g * d_L  (left mass produces CCW / negative about +Z when beam along X)
        float ComputeTauL()
        {
            return -ActiveML * Gravity * ActiveDL;
        }

        // tau_R = + m_R * g * d_R
        float ComputeTauR()
        {
            return ActiveMR * Gravity * ActiveDR;
        }

        float ComputeTauNet()
        {
            return ComputeTauL() + ComputeTauR();
        }

        float TargetTipAngleDeg()
        {
            float net = ComputeTauNet();
            if (Mathf.Abs(net) < BalanceEps)
                return 0f;
            // net < 0 => left wins => tip left (+theta); net > 0 => tip right (-theta)
            return net < 0f ? TipAngleMax : -TipAngleMax;
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
                _thetaDeg = 0f;
                _omega = 0f;
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
            DestroyMat(ref _beamMat);
            DestroyMat(ref _massLMat);
            DestroyMat(ref _massRMat);
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

            float target = TargetTipAngleDeg();
            // Light damped angular motion toward static tip angle (visual only — not rigid-body dynamics)
            float alpha = AngleSpring * (target - _thetaDeg) - AngleDamp * _omega;
            _omega += alpha * dt;
            _thetaDeg += _omega * dt;
            _thetaDeg = Mathf.Clamp(_thetaDeg, -TipAngleMax, TipAngleMax);

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
            CycleMass(+1);
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
                if (shift) CycleFulcrum(+1);
                else CycleMass(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleFulcrum(-1);
                else CycleMass(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleMass(int delta)
        {
            if (PresetMassLabels.Length == 0)
                return;
            _massIndex = (_massIndex + delta) % PresetMassLabels.Length;
            if (_massIndex < 0)
                _massIndex += PresetMassLabels.Length;
            Nudge();
            RefreshText();
            ApplyMassScales();
        }

        public void CycleFulcrum(int delta)
        {
            if (PresetFulcrumLabels.Length == 0)
                return;
            _fulcrumIndex = (_fulcrumIndex + delta) % PresetFulcrumLabels.Length;
            if (_fulcrumIndex < 0)
                _fulcrumIndex += PresetFulcrumLabels.Length;
            Nudge();
            RefreshText();
            ApplyVisualPose();
        }

        public void DesktopActivate(int delta)
        {
            CycleMass(delta);
        }

        void Nudge()
        {
            // Small omega kick so tip motion is visible after a preset change at rest.
            if (Mathf.Abs(_omega) < 1f && Mathf.Abs(_thetaDeg) < 1f && !IsBalanced)
                _omega = TargetTipAngleDeg() > 0f ? 8f : -8f;
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
                postGo.transform.localPosition = new Vector3(0f, FulcrumY * 0.5f, 0f);
                postGo.transform.localRotation = Quaternion.identity;
                postGo.transform.localScale = new Vector3(0.028f, FulcrumY, 0.028f);
                Object.Destroy(postGo.GetComponent<Collider>());
                _post = postGo.transform;
            }
            var pr = _post.GetComponent<Renderer>();
            if (pr != null && _standMat != null)
                pr.sharedMaterial = _standMat;

            _fulcrum = transform.Find("Fulcrum");
            if (_fulcrum == null)
            {
                var fulGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fulGo.name = "Fulcrum";
                fulGo.transform.SetParent(transform, false);
                fulGo.transform.localPosition = new Vector3(0f, FulcrumY, 0f);
                fulGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                fulGo.transform.localScale = new Vector3(0.035f, 0.018f, 0.035f);
                Object.Destroy(fulGo.GetComponent<Collider>());
                _fulcrum = fulGo.transform;
            }
            var fr = _fulcrum.GetComponent<Renderer>();
            if (fr != null)
            {
                if (_beamMat == null)
                {
                    _beamMat = new Material(sh);
                    var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
                    if (_beamMat.HasProperty("_BaseColor"))
                        _beamMat.SetColor("_BaseColor", cyan);
                    _beamMat.color = cyan;
                    if (_beamMat.HasProperty("_Metallic"))
                        _beamMat.SetFloat("_Metallic", 0.5f);
                    if (_beamMat.HasProperty("_Smoothness"))
                        _beamMat.SetFloat("_Smoothness", 0.55f);
                    if (_beamMat.HasProperty("_EmissionColor"))
                    {
                        _beamMat.EnableKeyword("_EMISSION");
                        _beamMat.SetColor("_EmissionColor", cyan * 0.25f);
                    }
                }
                fr.sharedMaterial = _beamMat;
            }

            _beam = transform.Find("Beam");
            if (_beam == null)
            {
                var beamGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beamGo.name = "Beam";
                beamGo.transform.SetParent(transform, false);
                beamGo.transform.localPosition = new Vector3(0f, FulcrumY, 0f);
                beamGo.transform.localRotation = Quaternion.identity;
                beamGo.transform.localScale = new Vector3(0.40f, BeamThick, BeamDepth);
                Object.Destroy(beamGo.GetComponent<Collider>());
                _beam = beamGo.transform;
            }
            var br = _beam.GetComponent<Renderer>();
            if (br != null && _beamMat != null)
                br.sharedMaterial = _beamMat;

            // Masses are siblings of beam (avoid non-uniform parent scale); posed in ApplyVisualPose
            _massL = transform.Find("MassL");
            if (_massL == null)
            {
                var mlGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mlGo.name = "MassL";
                mlGo.transform.SetParent(transform, false);
                mlGo.transform.localPosition = new Vector3(-0.20f, FulcrumY - MassHalf - 0.01f, 0f);
                mlGo.transform.localRotation = Quaternion.identity;
                mlGo.transform.localScale = new Vector3(MassHalf * 2f, MassHalf * 2f, MassHalf * 2f);
                Object.Destroy(mlGo.GetComponent<Collider>());
                _massL = mlGo.transform;
            }
            var mlr = _massL.GetComponent<Renderer>();
            if (mlr != null)
            {
                if (_massLMat == null)
                {
                    _massLMat = new Material(sh);
                    var teal = new Color(0.15f, 0.70f, 0.78f, 1f);
                    if (_massLMat.HasProperty("_BaseColor"))
                        _massLMat.SetColor("_BaseColor", teal);
                    _massLMat.color = teal;
                    if (_massLMat.HasProperty("_Metallic"))
                        _massLMat.SetFloat("_Metallic", 0.4f);
                    if (_massLMat.HasProperty("_Smoothness"))
                        _massLMat.SetFloat("_Smoothness", 0.5f);
                }
                mlr.sharedMaterial = _massLMat;
            }

            _massR = transform.Find("MassR");
            if (_massR == null)
            {
                var mrGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mrGo.name = "MassR";
                mrGo.transform.SetParent(transform, false);
                mrGo.transform.localPosition = new Vector3(0.20f, FulcrumY - MassHalf - 0.01f, 0f);
                mrGo.transform.localRotation = Quaternion.identity;
                mrGo.transform.localScale = new Vector3(MassHalf * 2f, MassHalf * 2f, MassHalf * 2f);
                Object.Destroy(mrGo.GetComponent<Collider>());
                _massR = mrGo.transform;
            }
            var mrr = _massR.GetComponent<Renderer>();
            if (mrr != null)
            {
                if (_massRMat == null)
                {
                    _massRMat = new Material(sh);
                    var steel = new Color(0.45f, 0.52f, 0.58f, 1f);
                    if (_massRMat.HasProperty("_BaseColor"))
                        _massRMat.SetColor("_BaseColor", steel);
                    _massRMat.color = steel;
                    if (_massRMat.HasProperty("_Metallic"))
                        _massRMat.SetFloat("_Metallic", 0.65f);
                    if (_massRMat.HasProperty("_Smoothness"))
                        _massRMat.SetFloat("_Smoothness", 0.55f);
                }
                mrr.sharedMaterial = _massRMat;
            }

            ApplyMassScales();
        }

        void ApplyMassScales()
        {
            if (_massL != null)
            {
                float s1 = 0.045f + 0.035f * Mathf.Clamp01(ActiveML / 2.2f);
                _massL.localScale = new Vector3(s1, s1, s1);
            }
            if (_massR != null)
            {
                float s2 = 0.045f + 0.035f * Mathf.Clamp01(ActiveMR / 2.2f);
                _massR.localScale = new Vector3(s2, s2, s2);
            }
        }

        void ApplyVisualPose()
        {
            float dL = ActiveDL;
            float dR = ActiveDR;
            float beamLen = dL + dR;
            float rad = _thetaDeg * Mathf.Deg2Rad;
            float cosT = Mathf.Cos(rad);
            float sinT = Mathf.Sin(rad);

            // Beam geometric center offset so fulcrum stays at origin when arms unequal
            float centerOffset = (dR - dL) * 0.5f;
            if (_beam != null)
            {
                _beam.localPosition = new Vector3(centerOffset * cosT, FulcrumY + centerOffset * sinT, 0f);
                _beam.localRotation = Quaternion.Euler(0f, 0f, _thetaDeg);
                _beam.localScale = new Vector3(beamLen, BeamThick, BeamDepth);
            }

            // Point masses at arm ends, hanging just below beam surface; rotate with tip
            float hang = BeamThick * 0.5f + MassHalf + 0.005f;
            if (_massL != null)
            {
                float s1 = 0.045f + 0.035f * Mathf.Clamp01(ActiveML / 2.2f);
                float hx = hang; // drop perpendicular to beam (local -Y of beam)
                // Left along beam: (-dL, 0) then tip rotation about fulcrum
                float lx = -dL * cosT + hx * sinT;
                float ly = FulcrumY - dL * sinT - hx * cosT;
                _massL.localPosition = new Vector3(lx, ly, 0f);
                _massL.localRotation = Quaternion.Euler(0f, 0f, _thetaDeg);
                _massL.localScale = new Vector3(s1, s1, s1);
            }
            if (_massR != null)
            {
                float s2 = 0.045f + 0.035f * Mathf.Clamp01(ActiveMR / 2.2f);
                float hx = hang;
                float rx = dR * cosT + hx * sinT;
                float ry = FulcrumY + dR * sinT - hx * cosT;
                _massR.localPosition = new Vector3(rx, ry, 0f);
                _massR.localRotation = Quaternion.Euler(0f, 0f, _thetaDeg);
                _massR.localScale = new Vector3(s2, s2, s2);
            }
        }

        void ApplyGlow()
        {
            if (_beamMat == null || !_beamMat.HasProperty("_EmissionColor"))
                return;
            float imbalance = Mathf.Clamp01(Mathf.Abs(ComputeTauNet()) / 5f);
            var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
            _beamMat.EnableKeyword("_EMISSION");
            float glow = IsBalanced ? 0.15f : (0.25f + 1.0f * imbalance);
            _beamMat.SetColor("_EmissionColor", cyan * glow);
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
                go.transform.localPosition = new Vector3(0f, FulcrumY + 0.22f, 0f);
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
            _readout.rectTransform.sizeDelta = new Vector2(56f, 30f);
            _readout.text = "LEVER\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            float tL = ComputeTauL();
            float tR = ComputeTauR();
            float tNet = tL + tR;
            string massLabel = ActiveMassLabel;
            string fulLabel = ActiveFulcrumLabel;
            string status;
            if (IsBalanced)
                status = "BALANCED";
            else if (tNet < 0f)
                status = "TIP LEFT";
            else
                status = "TIP RIGHT";

            string tlStr = "tL " + tL.ToString("+0.00;-0.00;0.00") + "Nm";
            string trStr = "tR " + tR.ToString("+0.00;-0.00;0.00") + "Nm";
            string tnStr = "tNet " + tNet.ToString("+0.00;-0.00;0.00");
            string mStr = "mL=" + ActiveML.ToString("0.0") + " mR=" + ActiveMR.ToString("0.0");
            string dStr = "dL=" + ActiveDL.ToString("0.00") + " dR=" + ActiveDR.ToString("0.00");

            _readout.text =
                "LEVER " + massLabel + "/" + fulLabel + " " + status + "\n"
                + tlStr + "  " + trStr + "\n"
                + tnStr + "  ang " + _thetaDeg.ToString("+0.0;-0.0;0.0") + "deg\n"
                + mStr + "\n"
                + dStr + "\n"
                + "N/P mass  Shift fulcrum\n"
                + "[ideal lever]";
        }
    }
}
