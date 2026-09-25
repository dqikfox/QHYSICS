using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS centripetal / uniform circular motion: ideal horizontal UCM.
    /// Honesty: lumped ideal UCM — NOT conical pendulum, not banked curve, not friction-limited tire, not 3D rigid-body constraint solver.
    /// XR activate toggles RUNNING/PAUSED; N/P cycles radius; Shift+N/P cycles speed. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadCentripetalGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Centripetal";
        public const string Honesty =
            "Lumped ideal UCM: a=v^2/r, F=m*a, T=2*pi*r/v, omega=v/r. NOT conical pendulum, not banked curve, not friction-limited tire, not 3D rigid-body constraint solver.";

        // Radius presets (m)
        static readonly float[] PresetsR = { 0.10f, 0.20f, 0.35f, 0.50f };
        static readonly string[] PresetRLabels = { "SHORT", "MED", "LONG", "XL" };

        // Tangential speed presets (m/s)
        static readonly float[] PresetsV = { 0.5f, 1.0f, 2.0f, 4.0f };
        static readonly string[] PresetVLabels = { "SLOW", "MED", "FAST", "XFAST" };

        const float MassKg = 0.25f;
        const float VisualScale = 0.55f; // compress physical r into lab ring (like pendulum)
        const float BobRadius = 0.028f;
        const float OrbitHeight = 0.12f;
        const int CircleSegments = 48;

        enum OrbitState { Paused, Running }

        TextMeshPro _readout;
        Transform _base;
        Transform _pivot;
        Transform _ring;
        Transform _bob;
        Transform _spokeRoot;
        LineRenderer _circleLine;
        LineRenderer _spokeLine;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _ringMat;
        Material _bobMat;
        Material _lineMat;
        float _refreshAt;
        float _inputCooldown;
        int _rIndex = 1; // default MED 0.20 m
        int _vIndex = 1; // default MED 1.0 m/s
        OrbitState _state = OrbitState.Paused;
        float _phase; // radians

        public float ActiveR => PresetsR[Mathf.Clamp(_rIndex, 0, PresetsR.Length - 1)];
        public float ActiveV => PresetsV[Mathf.Clamp(_vIndex, 0, PresetsV.Length - 1)];
        public string ActiveRLabel => PresetRLabels[Mathf.Clamp(_rIndex, 0, PresetRLabels.Length - 1)];
        public string ActiveVLabel => PresetVLabels[Mathf.Clamp(_vIndex, 0, PresetVLabels.Length - 1)];
        public float Accel => ActiveR > 1e-6f ? (ActiveV * ActiveV) / ActiveR : 0f;
        public float Force => MassKg * Accel;
        public float Period => ActiveV > 1e-6f ? (2f * Mathf.PI * ActiveR) / ActiveV : 0f;
        public float Omega => ActiveR > 1e-6f ? ActiveV / ActiveR : 0f;
        public float Mass => MassKg;
        public bool IsRunning => _state == OrbitState.Running;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            ApplyRingVisual();
            ApplyBobPose();
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
            DestroyMat(ref _ringMat);
            DestroyMat(ref _bobMat);
            DestroyMat(ref _lineMat);
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
            if (_state != OrbitState.Running)
                return;
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
                return;
            float w = Omega;
            _phase += w * dt;
            if (_phase > Mathf.PI * 2f)
                _phase -= Mathf.PI * 2f;
            else if (_phase < 0f)
                _phase += Mathf.PI * 2f;
            ApplyBobPose();
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
            ToggleRun();
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
                if (shift) CycleSpeed(+1);
                else CycleRadius(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleSpeed(-1);
                else CycleRadius(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleRadius(int delta)
        {
            if (PresetsR.Length == 0)
                return;
            _rIndex = (_rIndex + delta) % PresetsR.Length;
            if (_rIndex < 0)
                _rIndex += PresetsR.Length;
            ApplyRingVisual();
            ApplyBobPose();
            RefreshText();
        }

        public void CycleSpeed(int delta)
        {
            if (PresetsV.Length == 0)
                return;
            _vIndex = (_vIndex + delta) % PresetsV.Length;
            if (_vIndex < 0)
                _vIndex += PresetsV.Length;
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            if (delta != 0)
                CycleRadius(delta);
            ToggleRun();
        }

        void ToggleRun()
        {
            if (_state == OrbitState.Running)
                _state = OrbitState.Paused;
            else
                _state = OrbitState.Running;
            ApplyBobPose();
            RefreshText();
        }

        public void ResetPhase()
        {
            _phase = 0f;
            _state = OrbitState.Paused;
            ApplyBobPose();
            RefreshText();
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _base = transform.Find("Base");
            if (_base == null)
            {
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseGo.name = "Base";
                baseGo.transform.SetParent(transform, false);
                baseGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                baseGo.transform.localRotation = Quaternion.identity;
                baseGo.transform.localScale = new Vector3(0.16f, 0.02f, 0.16f);
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

            _pivot = transform.Find("Pivot");
            if (_pivot == null)
            {
                var pivotGo = new GameObject("Pivot");
                pivotGo.transform.SetParent(transform, false);
                pivotGo.transform.localPosition = new Vector3(0f, OrbitHeight, 0f);
                pivotGo.transform.localRotation = Quaternion.identity;
                _pivot = pivotGo.transform;
            }

            _ring = transform.Find("Ring");
            if (_ring == null)
            {
                var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = "Ring";
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localPosition = new Vector3(0f, OrbitHeight - 0.01f, 0f);
                ringGo.transform.localRotation = Quaternion.identity;
                Object.Destroy(ringGo.GetComponent<Collider>());
                _ring = ringGo.transform;
            }
            var rr = _ring.GetComponent<Renderer>();
            if (rr != null)
            {
                if (_ringMat == null)
                {
                    _ringMat = new Material(sh);
                    var cyan = new Color(0.16f, 0.50f, 0.58f, 1f);
                    if (_ringMat.HasProperty("_BaseColor"))
                        _ringMat.SetColor("_BaseColor", cyan);
                    _ringMat.color = cyan;
                    if (_ringMat.HasProperty("_Metallic"))
                        _ringMat.SetFloat("_Metallic", 0.45f);
                    if (_ringMat.HasProperty("_Smoothness"))
                        _ringMat.SetFloat("_Smoothness", 0.5f);
                    if (_ringMat.HasProperty("_EmissionColor"))
                    {
                        _ringMat.EnableKeyword("_EMISSION");
                        _ringMat.SetColor("_EmissionColor", cyan * 0.25f);
                    }
                }
                rr.sharedMaterial = _ringMat;
            }

            if (_lineMat == null)
            {
                _lineMat = new Material(sh);
                var soft = new Color(0.25f, 0.75f, 0.85f, 1f);
                if (_lineMat.HasProperty("_BaseColor"))
                    _lineMat.SetColor("_BaseColor", soft);
                _lineMat.color = soft;
                if (_lineMat.HasProperty("_EmissionColor"))
                {
                    _lineMat.EnableKeyword("_EMISSION");
                    _lineMat.SetColor("_EmissionColor", soft * 0.4f);
                }
            }

            _circleLine = _pivot.GetComponent<LineRenderer>();
            if (_circleLine == null)
                _circleLine = _pivot.gameObject.AddComponent<LineRenderer>();
            _circleLine.useWorldSpace = false;
            _circleLine.widthMultiplier = 0.006f;
            _circleLine.loop = true;
            _circleLine.positionCount = CircleSegments;
            if (_circleLine.sharedMaterial == null)
                _circleLine.sharedMaterial = _lineMat;
            _circleLine.startColor = new Color(0.3f, 0.85f, 0.95f, 0.85f);
            _circleLine.endColor = new Color(0.2f, 0.6f, 0.75f, 0.55f);

            _spokeRoot = _pivot.Find("Spoke");
            if (_spokeRoot == null)
            {
                var spokeGo = new GameObject("Spoke");
                spokeGo.transform.SetParent(_pivot, false);
                spokeGo.transform.localPosition = Vector3.zero;
                _spokeRoot = spokeGo.transform;
            }
            _spokeLine = _spokeRoot.GetComponent<LineRenderer>();
            if (_spokeLine == null)
                _spokeLine = _spokeRoot.gameObject.AddComponent<LineRenderer>();
            _spokeLine.useWorldSpace = false;
            _spokeLine.widthMultiplier = 0.005f;
            _spokeLine.positionCount = 2;
            if (_spokeLine.sharedMaterial == null)
                _spokeLine.sharedMaterial = _lineMat;
            _spokeLine.startColor = new Color(0.35f, 0.80f, 0.90f, 0.9f);
            _spokeLine.endColor = new Color(0.15f, 0.55f, 0.70f, 0.7f);

            _bob = _pivot.Find("Bob");
            if (_bob == null)
            {
                var bobGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bobGo.name = "Bob";
                bobGo.transform.SetParent(_pivot, false);
                bobGo.transform.localScale = Vector3.one * (BobRadius * 2f);
                Object.Destroy(bobGo.GetComponent<Collider>());
                _bob = bobGo.transform;
            }
            var blr = _bob.GetComponent<Renderer>();
            if (blr != null)
            {
                if (_bobMat == null)
                {
                    _bobMat = new Material(sh);
                    var teal = new Color(0.15f, 0.78f, 0.86f, 1f);
                    if (_bobMat.HasProperty("_BaseColor"))
                        _bobMat.SetColor("_BaseColor", teal);
                    _bobMat.color = teal;
                    if (_bobMat.HasProperty("_Metallic"))
                        _bobMat.SetFloat("_Metallic", 0.35f);
                    if (_bobMat.HasProperty("_Smoothness"))
                        _bobMat.SetFloat("_Smoothness", 0.6f);
                    if (_bobMat.HasProperty("_EmissionColor"))
                    {
                        _bobMat.EnableKeyword("_EMISSION");
                        _bobMat.SetColor("_EmissionColor", teal * 0.45f);
                    }
                }
                blr.sharedMaterial = _bobMat;
            }
        }

        void ApplyRingVisual()
        {
            float visR = Mathf.Max(0.04f, ActiveR * VisualScale);
            if (_ring != null)
            {
                _ring.localScale = new Vector3(visR * 2f, 0.004f, visR * 2f);
                _ring.localPosition = new Vector3(0f, OrbitHeight - 0.01f, 0f);
            }
            if (_circleLine != null)
            {
                _circleLine.positionCount = CircleSegments;
                for (int i = 0; i < CircleSegments; i++)
                {
                    float u = (float)i / CircleSegments;
                    float ang = u * Mathf.PI * 2f;
                    _circleLine.SetPosition(i, new Vector3(Mathf.Cos(ang) * visR, 0f, Mathf.Sin(ang) * visR));
                }
            }
        }

        void ApplyBobPose()
        {
            if (_bob == null)
                return;
            float visR = Mathf.Max(0.04f, ActiveR * VisualScale);
            float x = Mathf.Cos(_phase) * visR;
            float z = Mathf.Sin(_phase) * visR;
            _bob.localPosition = new Vector3(x, 0f, z);
            if (_spokeLine != null)
            {
                _spokeLine.SetPosition(0, Vector3.zero);
                _spokeLine.SetPosition(1, new Vector3(x, 0f, z));
            }
        }

        void ApplyGlow()
        {
            if (_bobMat == null || !_bobMat.HasProperty("_EmissionColor"))
                return;
            var teal = new Color(0.15f, 0.78f, 0.86f, 1f);
            _bobMat.EnableKeyword("_EMISSION");
            float glow = _state == OrbitState.Running ? 0.9f : 0.35f;
            _bobMat.SetColor("_EmissionColor", teal * glow);
            if (_ringMat != null && _ringMat.HasProperty("_EmissionColor"))
            {
                var cyan = new Color(0.16f, 0.50f, 0.58f, 1f);
                _ringMat.EnableKeyword("_EMISSION");
                _ringMat.SetColor("_EmissionColor", cyan * (_state == OrbitState.Running ? 0.45f : 0.2f));
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
            _readout.rectTransform.sizeDelta = new Vector2(56f, 34f);
            _readout.text = "CENTRIPETAL\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string status = _state == OrbitState.Running ? "RUNNING" : "PAUSED";
            string rStr = "r " + ActiveR.ToString("0.00") + "m";
            string vStr = "v " + ActiveV.ToString("0.0") + "m/s";
            string aStr = "a " + Accel.ToString("0.00") + "m/s2";
            string fStr = "F " + Force.ToString("0.00") + "N";
            string tStr = "T " + Period.ToString("0.00") + "s";
            string wStr = "w " + Omega.ToString("0.00") + "rad/s";

            _readout.text =
                "UCM " + ActiveRLabel + "/" + ActiveVLabel + " " + status + "\n"
                + rStr + "  " + vStr + "\n"
                + aStr + "  " + fStr + "\n"
                + tStr + "  " + wStr + "\n"
                + "m " + MassKg.ToString("0.00") + "kg\n"
                + "N/P r  Shift v\n"
                + "activate run/pause\n"
                + "[ideal UCM]";
        }
    }
}