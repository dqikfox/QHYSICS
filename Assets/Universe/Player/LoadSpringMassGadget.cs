using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS spring-mass: classical Hooke oscillator m x'' = -k x - c x'.
    /// Honesty: lumped Hooke spring-mass — NOT continuum elasticity, not nonlinear, not collision contact spring.
    /// XR activate / N cycles k presets; P steps backward. Shift+N/P cycles damp. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadSpringMassGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_SpringMass";
        public const string Honesty =
            "Lumped Hooke spring-mass m*x''=-k*x-c*x'. NOT continuum elasticity, not nonlinear, not collision contact spring.";

        // Stiffness presets (N/m)
        static readonly float[] PresetsK = { 5f, 20f, 80f, 320f };
        static readonly string[] PresetKLabels = { "SOFT", "MED", "STIFF", "RIGID" };

        // Damping presets (N*s/m)
        static readonly float[] PresetsC = { 0f, 0.35f, 3.5f };
        static readonly string[] PresetCLabels = { "OFF", "LIGHT", "HEAVY" };

        const float MassKg = 0.25f;
        const float MaxAbsX = 0.14f;
        const float RestSpringLen = 0.18f;
        const float VisualScale = 1f;

        TextMeshPro _readout;
        Transform _stand;
        Transform _anchor;
        Transform _spring;
        Transform _massBob;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _springMat;
        Material _massMat;
        float _x;
        float _v;
        float _refreshAt;
        float _inputCooldown;
        int _kIndex = 1; // default MED
        int _cIndex = 1; // default LIGHT
        bool _plucked;

        public float DisplacementMeters => _x;
        public float VelocityMetersPerSec => _v;
        public float ForceNewtons => -ActiveK * _x;
        public float SpringEnergyJoules => 0.5f * ActiveK * _x * _x;
        public float KineticEnergyJoules => 0.5f * MassKg * _v * _v;
        public float NaturalFreqHz => ActiveK > 1e-6f ? (1f / (2f * Mathf.PI)) * Mathf.Sqrt(ActiveK / MassKg) : 0f;
        public float ActiveK => PresetsK[Mathf.Clamp(_kIndex, 0, PresetsK.Length - 1)];
        public float ActiveC => PresetsC[Mathf.Clamp(_cIndex, 0, PresetsC.Length - 1)];
        public string ActiveKLabel => PresetKLabels[Mathf.Clamp(_kIndex, 0, PresetKLabels.Length - 1)];
        public string ActiveCLabel => PresetCLabels[Mathf.Clamp(_cIndex, 0, PresetCLabels.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            if (!_plucked)
            {
                _x = 0.08f;
                _v = 0f;
                _plucked = true;
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
            DestroyMat(ref _springMat);
            DestroyMat(ref _massMat);
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

            float k = ActiveK;
            float c = ActiveC;
            // Semi-implicit Euler: m a = -k x - c v
            float a = (-k * _x - c * _v) / MassKg;
            _v += a * dt;
            _x += _v * dt;

            if (_x > MaxAbsX)
            {
                _x = MaxAbsX;
                if (_v > 0f) _v = 0f;
            }
            else if (_x < -MaxAbsX)
            {
                _x = -MaxAbsX;
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
            CycleK(+1);
            Pluck();
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
                else CycleK(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleDamp(-1);
                else CycleK(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleK(int delta)
        {
            if (PresetsK.Length == 0)
                return;
            _kIndex = (_kIndex + delta) % PresetsK.Length;
            if (_kIndex < 0)
                _kIndex += PresetsK.Length;
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

        public void CyclePreset(int delta) => CycleK(delta);

        public void DesktopActivate(int delta)
        {
            CycleK(delta);
            if (delta != 0)
                Pluck();
        }

        void Pluck()
        {
            // Re-excite oscillator so preset changes stay playable at rest.
            if (Mathf.Abs(_x) < 0.02f && Mathf.Abs(_v) < 0.05f)
            {
                _x = 0.08f;
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
                standGo.transform.localScale = new Vector3(0.12f, 0.04f, 0.12f);
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

            _anchor = transform.Find("Anchor");
            if (_anchor == null)
            {
                var anchorGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                anchorGo.name = "Anchor";
                anchorGo.transform.SetParent(transform, false);
                anchorGo.transform.localPosition = new Vector3(0f, 0.28f, 0f);
                anchorGo.transform.localRotation = Quaternion.identity;
                anchorGo.transform.localScale = new Vector3(0.06f, 0.015f, 0.06f);
                Object.Destroy(anchorGo.GetComponent<Collider>());
                _anchor = anchorGo.transform;
            }
            var ar = _anchor.GetComponent<Renderer>();
            if (ar != null && _standMat != null)
                ar.sharedMaterial = _standMat;

            // Thin post from stand to anchor
            Transform post = transform.Find("Post");
            if (post == null)
            {
                var postGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postGo.name = "Post";
                postGo.transform.SetParent(transform, false);
                postGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                postGo.transform.localRotation = Quaternion.identity;
                postGo.transform.localScale = new Vector3(0.025f, 0.26f, 0.025f);
                Object.Destroy(postGo.GetComponent<Collider>());
                post = postGo.transform;
            }
            var pr = post.GetComponent<Renderer>();
            if (pr != null && _standMat != null)
                pr.sharedMaterial = _standMat;

            _spring = transform.Find("Spring");
            if (_spring == null)
            {
                var springGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                springGo.name = "Spring";
                springGo.transform.SetParent(transform, false);
                springGo.transform.localPosition = new Vector3(0.08f, 0.19f, 0f);
                springGo.transform.localRotation = Quaternion.identity;
                springGo.transform.localScale = new Vector3(0.035f, RestSpringLen * 0.5f, 0.035f);
                Object.Destroy(springGo.GetComponent<Collider>());
                _spring = springGo.transform;
            }
            var spr = _spring.GetComponent<Renderer>();
            if (spr != null)
            {
                if (_springMat == null)
                {
                    _springMat = new Material(sh);
                    var cyan = new Color(0.2f, 0.75f, 0.85f, 1f);
                    if (_springMat.HasProperty("_BaseColor"))
                        _springMat.SetColor("_BaseColor", cyan);
                    _springMat.color = cyan;
                    if (_springMat.HasProperty("_Smoothness"))
                        _springMat.SetFloat("_Smoothness", 0.6f);
                    if (_springMat.HasProperty("_EmissionColor"))
                    {
                        _springMat.EnableKeyword("_EMISSION");
                        _springMat.SetColor("_EmissionColor", cyan * 0.25f);
                    }
                }
                spr.sharedMaterial = _springMat;
            }

            _massBob = transform.Find("MassBob");
            if (_massBob == null)
            {
                var massGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                massGo.name = "MassBob";
                massGo.transform.SetParent(transform, false);
                massGo.transform.localPosition = new Vector3(0.08f, 0.28f - RestSpringLen, 0f);
                massGo.transform.localRotation = Quaternion.identity;
                massGo.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
                Object.Destroy(massGo.GetComponent<Collider>());
                _massBob = massGo.transform;
            }
            var mr = _massBob.GetComponent<Renderer>();
            if (mr != null)
            {
                if (_massMat == null)
                {
                    _massMat = new Material(sh);
                    var steel = new Color(0.55f, 0.58f, 0.62f, 1f);
                    if (_massMat.HasProperty("_BaseColor"))
                        _massMat.SetColor("_BaseColor", steel);
                    _massMat.color = steel;
                    if (_massMat.HasProperty("_Metallic"))
                        _massMat.SetFloat("_Metallic", 0.7f);
                    if (_massMat.HasProperty("_Smoothness"))
                        _massMat.SetFloat("_Smoothness", 0.55f);
                }
                mr.sharedMaterial = _massMat;
            }
        }

        void ApplyVisualPose()
        {
            if (_massBob == null || _spring == null || _anchor == null)
                return;

            float len = Mathf.Max(0.04f, RestSpringLen + _x * VisualScale);
            float anchorY = 0.28f;
            float massY = anchorY - len;
            float xOff = 0.08f;

            _massBob.localPosition = new Vector3(xOff, massY, 0f);
            _spring.localPosition = new Vector3(xOff, anchorY - len * 0.5f, 0f);
            // Unity cylinder default height 2 → scale.y = half-length
            _spring.localScale = new Vector3(0.035f, len * 0.5f, 0.035f);
        }

        void ApplyGlow()
        {
            if (_springMat == null || !_springMat.HasProperty("_EmissionColor"))
                return;
            float e = Mathf.Clamp01(SpringEnergyJoules / 0.5f);
            var cyan = new Color(0.2f, 0.75f, 0.85f, 1f);
            _springMat.EnableKeyword("_EMISSION");
            _springMat.SetColor("_EmissionColor", cyan * (0.15f + 1.4f * e));
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
                go.transform.localPosition = new Vector3(0f, 0.38f, 0f);
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
            _readout.rectTransform.sizeDelta = new Vector2(40f, 22f);
            _readout.text = "SPRING\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string kLabel = ActiveKLabel;
            string cLabel = ActiveCLabel;
            string xStr = (_x * 100f).ToString("+0.0;-0.0;0.0") + "cm";
            string fStr = ForceNewtons.ToString("+0.00;-0.00;0.00") + "N";
            string eStr = SpringEnergyJoules.ToString("0.000") + "J";
            string freq = NaturalFreqHz.ToString("0.00") + "Hz";
            string kStr = "k=" + ActiveK.ToString("0.#");

            _readout.text =
                "SPRING " + kLabel + "/" + cLabel + "\n"
                + "x " + xStr + "  F " + fStr + "\n"
                + "E " + eStr + "  f0 " + freq + "\n"
                + kStr + "  N/P k  Shift damp\n"
                + "[Hooke lumped]";
        }
    }
}
