using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS simple pendulum: planar theta'' = -(g/L) sin(theta) - c theta'.
    /// Honesty: lumped planar pendulum — NOT spherical pendulum, not rigid-body collision, not air drag Cd, not physical string stretch.
    /// XR activate / N cycles L presets; P steps backward. Shift+N/P cycles damp. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadPendulumGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Pendulum";
        public const string Honesty =
            "Lumped planar pendulum theta''=-(g/L)sin(theta)-c*theta'. NOT spherical pendulum, not rigid-body collision, not air drag Cd model, not physical string stretch.";

        // Length presets (m)
        static readonly float[] PresetsL = { 0.15f, 0.25f, 0.40f, 0.60f };
        static readonly string[] PresetLLabels = { "SHORT", "MED", "LONG", "XL" };

        // Angular damping presets (1/s on omega)
        static readonly float[] PresetsC = { 0f, 0.35f, 2.5f };
        static readonly string[] PresetCLabels = { "OFF", "LIGHT", "HEAVY" };

        const float MassKg = 0.25f;
        const float Gravity = 9.81f;
        const float InitialThetaRad = 25f * Mathf.Deg2Rad; // ~25 deg pluck
        const float PivotLocalY = 0.55f;
        const float VisualScale = 0.55f; // compress physical L into lab prop height

        TextMeshPro _readout;
        Transform _stand;
        Transform _pivot;
        Transform _rod;
        Transform _bob;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _rodMat;
        Material _bobMat;
        float _theta;
        float _omega;
        float _refreshAt;
        float _inputCooldown;
        int _lIndex = 1; // default MED
        int _cIndex = 1; // default LIGHT
        bool _plucked;

        public float ThetaRadians => _theta;
        public float ThetaDegrees => _theta * Mathf.Rad2Deg;
        public float OmegaRadPerSec => _omega;
        public float LengthMeters => ActiveL;
        public float PotentialEnergyJoules => MassKg * Gravity * ActiveL * (1f - Mathf.Cos(_theta));
        public float KineticEnergyJoules => 0.5f * MassKg * (ActiveL * _omega) * (ActiveL * _omega);
        public float SmallAnglePeriodSec => ActiveL > 1e-6f ? 2f * Mathf.PI * Mathf.Sqrt(ActiveL / Gravity) : 0f;
        public float ActiveL => PresetsL[Mathf.Clamp(_lIndex, 0, PresetsL.Length - 1)];
        public float ActiveC => PresetsC[Mathf.Clamp(_cIndex, 0, PresetsC.Length - 1)];
        public string ActiveLLabel => PresetLLabels[Mathf.Clamp(_lIndex, 0, PresetLLabels.Length - 1)];
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
                _theta = InitialThetaRad;
                _omega = 0f;
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
            DestroyMat(ref _rodMat);
            DestroyMat(ref _bobMat);
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

            float L = ActiveL;
            float c = ActiveC;
            // Semi-implicit Euler: theta'' = -(g/L) sin(theta) - c omega
            float a = -(Gravity / L) * Mathf.Sin(_theta) - c * _omega;
            _omega += a * dt;
            _theta += _omega * dt;

            // Keep angle in a readable wrap for readout (physics is continuous)
            if (_theta > Mathf.PI)
                _theta -= 2f * Mathf.PI;
            else if (_theta < -Mathf.PI)
                _theta += 2f * Mathf.PI;

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
            CycleL(+1);
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
                else CycleL(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleDamp(-1);
                else CycleL(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleL(int delta)
        {
            if (PresetsL.Length == 0)
                return;
            _lIndex = (_lIndex + delta) % PresetsL.Length;
            if (_lIndex < 0)
                _lIndex += PresetsL.Length;
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

        public void CyclePreset(int delta) => CycleL(delta);

        public void DesktopActivate(int delta)
        {
            CycleL(delta);
            if (delta != 0)
                Pluck();
        }

        void Pluck()
        {
            // Re-excite so preset changes stay playable at rest.
            if (Mathf.Abs(_theta) < 0.05f && Mathf.Abs(_omega) < 0.1f)
            {
                _theta = InitialThetaRad;
                _omega = 0f;
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
                standGo.transform.localScale = new Vector3(0.14f, 0.04f, 0.14f);
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

            // Vertical post up to pivot
            Transform post = transform.Find("Post");
            if (post == null)
            {
                var postGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postGo.name = "Post";
                postGo.transform.SetParent(transform, false);
                postGo.transform.localPosition = new Vector3(0f, PivotLocalY * 0.5f, 0f);
                postGo.transform.localRotation = Quaternion.identity;
                postGo.transform.localScale = new Vector3(0.028f, PivotLocalY, 0.028f);
                Object.Destroy(postGo.GetComponent<Collider>());
                post = postGo.transform;
            }
            var pr = post.GetComponent<Renderer>();
            if (pr != null && _standMat != null)
                pr.sharedMaterial = _standMat;

            _pivot = transform.Find("Pivot");
            if (_pivot == null)
            {
                var pivotGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pivotGo.name = "Pivot";
                pivotGo.transform.SetParent(transform, false);
                pivotGo.transform.localPosition = new Vector3(0f, PivotLocalY, 0f);
                pivotGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pivotGo.transform.localScale = new Vector3(0.04f, 0.02f, 0.04f);
                Object.Destroy(pivotGo.GetComponent<Collider>());
                _pivot = pivotGo.transform;
            }
            var pvr = _pivot.GetComponent<Renderer>();
            if (pvr != null && _standMat != null)
                pvr.sharedMaterial = _standMat;

            _rod = transform.Find("Rod");
            if (_rod == null)
            {
                var rodGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rodGo.name = "Rod";
                rodGo.transform.SetParent(transform, false);
                rodGo.transform.localPosition = new Vector3(0f, PivotLocalY - 0.1f, 0f);
                rodGo.transform.localRotation = Quaternion.identity;
                rodGo.transform.localScale = new Vector3(0.012f, 0.1f, 0.012f);
                Object.Destroy(rodGo.GetComponent<Collider>());
                _rod = rodGo.transform;
            }
            var rr = _rod.GetComponent<Renderer>();
            if (rr != null)
            {
                if (_rodMat == null)
                {
                    _rodMat = new Material(sh);
                    var cyan = new Color(0.2f, 0.75f, 0.85f, 1f);
                    if (_rodMat.HasProperty("_BaseColor"))
                        _rodMat.SetColor("_BaseColor", cyan);
                    _rodMat.color = cyan;
                    if (_rodMat.HasProperty("_Smoothness"))
                        _rodMat.SetFloat("_Smoothness", 0.6f);
                    if (_rodMat.HasProperty("_EmissionColor"))
                    {
                        _rodMat.EnableKeyword("_EMISSION");
                        _rodMat.SetColor("_EmissionColor", cyan * 0.25f);
                    }
                }
                rr.sharedMaterial = _rodMat;
            }

            _bob = transform.Find("Bob");
            if (_bob == null)
            {
                var bobGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bobGo.name = "Bob";
                bobGo.transform.SetParent(transform, false);
                bobGo.transform.localPosition = new Vector3(0f, PivotLocalY - 0.2f, 0f);
                bobGo.transform.localRotation = Quaternion.identity;
                bobGo.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);
                Object.Destroy(bobGo.GetComponent<Collider>());
                _bob = bobGo.transform;
            }
            var br = _bob.GetComponent<Renderer>();
            if (br != null)
            {
                if (_bobMat == null)
                {
                    _bobMat = new Material(sh);
                    var steel = new Color(0.55f, 0.58f, 0.62f, 1f);
                    if (_bobMat.HasProperty("_BaseColor"))
                        _bobMat.SetColor("_BaseColor", steel);
                    _bobMat.color = steel;
                    if (_bobMat.HasProperty("_Metallic"))
                        _bobMat.SetFloat("_Metallic", 0.7f);
                    if (_bobMat.HasProperty("_Smoothness"))
                        _bobMat.SetFloat("_Smoothness", 0.55f);
                }
                br.sharedMaterial = _bobMat;
            }
        }

        void ApplyVisualPose()
        {
            if (_bob == null || _rod == null)
                return;

            float visL = Mathf.Max(0.08f, ActiveL * VisualScale);
            float x = visL * Mathf.Sin(_theta);
            float y = PivotLocalY - visL * Mathf.Cos(_theta);

            _bob.localPosition = new Vector3(x, y, 0f);

            // Rod mid-point and tilt in XZ=0 plane (rotate about Z)
            float midX = x * 0.5f;
            float midY = (PivotLocalY + y) * 0.5f;
            _rod.localPosition = new Vector3(midX, midY, 0f);
            float angleDeg = -_theta * Mathf.Rad2Deg;
            _rod.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            // Unity cylinder default height 2 → scale.y = half-length
            _rod.localScale = new Vector3(0.012f, visL * 0.5f, 0.012f);
        }

        void ApplyGlow()
        {
            if (_rodMat == null || !_rodMat.HasProperty("_EmissionColor"))
                return;
            float eMax = MassKg * Gravity * ActiveL * (1f - Mathf.Cos(InitialThetaRad));
            float e = eMax > 1e-6f ? Mathf.Clamp01(PotentialEnergyJoules / eMax) : 0f;
            var cyan = new Color(0.2f, 0.75f, 0.85f, 1f);
            _rodMat.EnableKeyword("_EMISSION");
            _rodMat.SetColor("_EmissionColor", cyan * (0.15f + 1.4f * e));
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
                go.transform.localPosition = new Vector3(0f, PivotLocalY + 0.12f, 0f);
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
            _readout.rectTransform.sizeDelta = new Vector2(44f, 24f);
            _readout.text = "PENDULUM\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string lLabel = ActiveLLabel;
            string cLabel = ActiveCLabel;
            string thStr = ThetaDegrees.ToString("+0.0;-0.0;0.0") + "deg";
            string wStr = _omega.ToString("+0.00;-0.00;0.00") + "rad/s";
            string tStr = "T~" + SmallAnglePeriodSec.ToString("0.00") + "s";
            string peStr = "PE " + PotentialEnergyJoules.ToString("0.000") + "J";
            string keStr = "KE " + KineticEnergyJoules.ToString("0.000") + "J";
            string lStr = "L=" + ActiveL.ToString("0.00") + "m";

            _readout.text =
                "PEND " + lLabel + "/" + cLabel + "\n"
                + "th " + thStr + "  w " + wStr + "\n"
                + peStr + "  " + keStr + "\n"
                + tStr + "  " + lStr + "\n"
                + "N/P L  Shift damp\n"
                + "[planar lumped]";
        }
    }
}
