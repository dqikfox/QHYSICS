using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS inclined plane: ideal block-on-ramp a = g(sin θ − μ_k cos θ).
    /// Honesty: lumped ideal inclined plane — NOT rolling, not air drag, not variable μ, not 3D tipping.
    /// XR activate / N cycles θ; P steps back; Shift+N/P cycles μ. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadInclinedPlaneGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_InclinedPlane";
        public const string Honesty =
            "Lumped ideal inclined plane a=g(sinθ-μ cosθ). NOT rolling, not air drag, not variable μ, not 3D tipping.";

        // Angle presets (degrees)
        static readonly float[] PresetsThetaDeg = { 15f, 30f, 45f, 60f };
        static readonly string[] PresetThetaLabels = { "LO", "MED", "STEEP", "CLIFF" };

        // Kinetic friction μ (μ_s ≈ μ_k for simple STICK check)
        static readonly float[] PresetsMu = { 0f, 0.2f, 0.4f };
        static readonly string[] PresetMuLabels = { "OFF", "LIGHT", "HEAVY" };

        const float Gravity = 9.81f;
        const float PlaneLength = 0.55f;   // travel along incline (m)
        const float SoftStop = 0.02f;
        const float BlockHalf = 0.03f;
        const float PlankThick = 0.02f;
        const float PlankWidth = 0.12f;

        TextMeshPro _readout;
        Transform _base;
        Transform _plank;
        Transform _block;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _plankMat;
        Material _blockMat;
        float _s;       // distance along plane from bottom (m)
        float _v;       // along-plane velocity (m/s), positive uphill
        float _refreshAt;
        float _inputCooldown;
        int _thetaIndex = 1; // default MED 30°
        int _muIndex = 1;    // default LIGHT 0.2
        bool _started;
        bool _stuck;

        public float Acceleration => ComputeA();
        public float Velocity => _v;
        public float Distance => _s;
        public float ActiveThetaDeg => PresetsThetaDeg[Mathf.Clamp(_thetaIndex, 0, PresetsThetaDeg.Length - 1)];
        public float ActiveThetaRad => ActiveThetaDeg * Mathf.Deg2Rad;
        public float ActiveMu => PresetsMu[Mathf.Clamp(_muIndex, 0, PresetsMu.Length - 1)];
        public string ActiveThetaLabel => PresetThetaLabels[Mathf.Clamp(_thetaIndex, 0, PresetThetaLabels.Length - 1)];
        public string ActiveMuLabel => PresetMuLabels[Mathf.Clamp(_muIndex, 0, PresetMuLabels.Length - 1)];
        public bool IsStuck => _stuck;

        /// <summary>
        /// Downhill acceleration along the plane. Returns 0 when static friction holds (STICK).
        /// Sign convention in integrator: positive _v is uphill, so we apply -a_down.
        /// </summary>
        float ComputeADown()
        {
            float theta = ActiveThetaRad;
            float mu = ActiveMu;
            float sinT = Mathf.Sin(theta);
            float cosT = Mathf.Cos(theta);
            // Static check: μ_s cos θ ≥ sin θ  →  STICK (μ_s ≈ μ_k)
            if (mu * cosT >= sinT - 1e-6f)
                return 0f;
            float a = Gravity * (sinT - mu * cosT);
            return Mathf.Max(0f, a);
        }

        float ComputeA()
        {
            // Signed acceleration along plane (positive uphill). Sliding down → negative a.
            return -ComputeADown();
        }

        bool WouldStick()
        {
            float theta = ActiveThetaRad;
            float mu = ActiveMu;
            return mu * Mathf.Cos(theta) >= Mathf.Sin(theta) - 1e-6f;
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
                // Start near top so the block slides down under gravity when not stuck.
                _s = PlaneLength - SoftStop - BlockHalf;
                _v = 0f;
                _started = true;
            }
            _stuck = WouldStick();
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
            DestroyMat(ref _baseMat);
            DestroyMat(ref _plankMat);
            DestroyMat(ref _blockMat);
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

            _stuck = WouldStick();
            if (_stuck)
            {
                _v = 0f;
            }
            else
            {
                // Kinetic friction opposes velocity; when descending (v≤0) use downhill a.
                float aDown = ComputeADown();
                float a;
                if (_v > 0.02f)
                {
                    // Moving uphill: gravity + friction both pull downhill
                    float theta = ActiveThetaRad;
                    float mu = ActiveMu;
                    a = -Gravity * (Mathf.Sin(theta) + mu * Mathf.Cos(theta));
                }
                else
                {
                    // At rest or sliding down: a = -aDown (downhill positive in aDown)
                    a = -aDown;
                }

                _v += a * dt;
                _s += _v * dt;
            }

            float sMin = SoftStop + BlockHalf;
            float sMax = PlaneLength - SoftStop - BlockHalf;
            if (_s > sMax)
            {
                _s = sMax;
                if (_v > 0f) _v = 0f;
            }
            else if (_s < sMin)
            {
                _s = sMin;
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
            CycleTheta(+1);
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
                if (shift) CycleMu(+1);
                else CycleTheta(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleMu(-1);
                else CycleTheta(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleTheta(int delta)
        {
            if (PresetThetaLabels.Length == 0)
                return;
            _thetaIndex = (_thetaIndex + delta) % PresetThetaLabels.Length;
            if (_thetaIndex < 0)
                _thetaIndex += PresetThetaLabels.Length;
            Nudge();
            RefreshText();
            ApplyVisualPose();
        }

        public void CycleMu(int delta)
        {
            if (PresetsMu.Length == 0)
                return;
            _muIndex = (_muIndex + delta) % PresetsMu.Length;
            if (_muIndex < 0)
                _muIndex += PresetsMu.Length;
            Nudge();
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            CycleTheta(delta);
        }

        void Nudge()
        {
            // Re-place near top so angle/μ changes stay playable after parking at the bottom.
            _stuck = WouldStick();
            if (_stuck)
            {
                _v = 0f;
                return;
            }
            if (Mathf.Abs(_v) < 0.05f && _s < SoftStop + BlockHalf + 0.05f)
            {
                _s = PlaneLength - SoftStop - BlockHalf;
                _v = 0f;
            }
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
                baseGo.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                baseGo.transform.localRotation = Quaternion.identity;
                baseGo.transform.localScale = new Vector3(0.28f, 0.03f, 0.18f);
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

            _plank = transform.Find("Plank");
            if (_plank == null)
            {
                var plankGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plankGo.name = "Plank";
                plankGo.transform.SetParent(transform, false);
                plankGo.transform.localScale = new Vector3(PlaneLength, PlankThick, PlankWidth);
                Object.Destroy(plankGo.GetComponent<Collider>());
                _plank = plankGo.transform;
            }
            var pr = _plank.GetComponent<Renderer>();
            if (pr != null)
            {
                if (_plankMat == null)
                {
                    _plankMat = new Material(sh);
                    var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
                    if (_plankMat.HasProperty("_BaseColor"))
                        _plankMat.SetColor("_BaseColor", cyan);
                    _plankMat.color = cyan;
                    if (_plankMat.HasProperty("_Metallic"))
                        _plankMat.SetFloat("_Metallic", 0.45f);
                    if (_plankMat.HasProperty("_Smoothness"))
                        _plankMat.SetFloat("_Smoothness", 0.55f);
                    if (_plankMat.HasProperty("_EmissionColor"))
                    {
                        _plankMat.EnableKeyword("_EMISSION");
                        _plankMat.SetColor("_EmissionColor", cyan * 0.25f);
                    }
                }
                pr.sharedMaterial = _plankMat;
            }

            _block = transform.Find("Block");
            if (_block == null)
            {
                var blockGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blockGo.name = "Block";
                blockGo.transform.SetParent(transform, false);
                blockGo.transform.localScale = new Vector3(BlockHalf * 2f, BlockHalf * 2f, BlockHalf * 2f);
                Object.Destroy(blockGo.GetComponent<Collider>());
                _block = blockGo.transform;
            }
            var blr = _block.GetComponent<Renderer>();
            if (blr != null)
            {
                if (_blockMat == null)
                {
                    _blockMat = new Material(sh);
                    var teal = new Color(0.15f, 0.70f, 0.78f, 1f);
                    if (_blockMat.HasProperty("_BaseColor"))
                        _blockMat.SetColor("_BaseColor", teal);
                    _blockMat.color = teal;
                    if (_blockMat.HasProperty("_Metallic"))
                        _blockMat.SetFloat("_Metallic", 0.4f);
                    if (_blockMat.HasProperty("_Smoothness"))
                        _blockMat.SetFloat("_Smoothness", 0.5f);
                }
                blr.sharedMaterial = _blockMat;
            }
        }

        void ApplyVisualPose()
        {
            float theta = ActiveThetaDeg;
            // Plank: bottom hinge near origin, rises along +X rotated by -θ about Z
            // Local: plank center sits at mid-length along incline
            float half = PlaneLength * 0.5f;
            float rad = theta * Mathf.Deg2Rad;
            float cosT = Mathf.Cos(rad);
            float sinT = Mathf.Sin(rad);

            if (_plank != null)
            {
                // Center of plank along incline from bottom pivot at (0, PlankThick*0.5, 0)
                float cx = half * cosT;
                float cy = half * sinT + PlankThick * 0.5f;
                _plank.localPosition = new Vector3(cx, cy, 0f);
                _plank.localRotation = Quaternion.Euler(0f, 0f, theta);
                _plank.localScale = new Vector3(PlaneLength, PlankThick, PlankWidth);
            }

            if (_block != null)
            {
                // Block sits on top of plank surface at distance _s from bottom
                float along = _s;
                float bx = along * cosT;
                float by = along * sinT + PlankThick + BlockHalf;
                _block.localPosition = new Vector3(bx, by, 0f);
                _block.localRotation = Quaternion.Euler(0f, 0f, theta);
            }
        }

        void ApplyGlow()
        {
            if (_plankMat == null || !_plankMat.HasProperty("_EmissionColor"))
                return;
            float aAbs = Mathf.Clamp01(Mathf.Abs(ComputeADown()) / Gravity);
            var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
            _plankMat.EnableKeyword("_EMISSION");
            float glow = _stuck ? 0.12f : (0.2f + 1.0f * aAbs);
            _plankMat.SetColor("_EmissionColor", cyan * glow);
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
                go.transform.localPosition = new Vector3(0.1f, 0.42f, 0f);
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
            _readout.rectTransform.sizeDelta = new Vector2(52f, 28f);
            _readout.text = "INCLINE\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            float aDown = ComputeADown();
            string thetaLabel = ActiveThetaLabel;
            string muLabel = ActiveMuLabel;
            string status = _stuck ? "STICK" : "SLIDE";
            string aStr = "a " + aDown.ToString("0.00") + "m/s2";
            string vStr = "v " + _v.ToString("+0.00;-0.00;0.00") + "m/s";
            string sStr = "s " + _s.ToString("0.000") + "m";
            string thStr = "θ " + ActiveThetaDeg.ToString("0") + "°";
            string muStr = "μ " + ActiveMu.ToString("0.0");

            _readout.text =
                "INCLINE " + thetaLabel + "/" + muLabel + " " + status + "\n"
                + thStr + "  " + muStr + "\n"
                + aStr + "  " + vStr + "\n"
                + sStr + "\n"
                + "N/P angle  Shift μ\n"
                + "[ideal incline]";
        }
    }
}
