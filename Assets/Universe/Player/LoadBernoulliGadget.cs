using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Bernoulli / Venturi: ideal horizontal tube P + 1/2 rho v^2 = const with continuity.
    /// Honesty: ideal incompressible inviscid Venturi - NOT viscous losses, not compressible flow, not cavitation, not 3D CFD.
    /// XR activate toggles RUN/PAUSE; N/P cycles inlet speed; Shift+N/P cycles throat area ratio. Desktop via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadBernoulliGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Bernoulli";
        public const string Honesty =
            "Ideal horizontal Venturi/Bernoulli: P + 1/2 rho v^2 = const, A1 v1 = A2 v2. NOT viscous losses, not compressible flow, not cavitation, not 3D CFD.";

        // Inlet speed presets (m/s)
        static readonly float[] PresetsV = { 0.5f, 1.0f, 2.0f, 4.0f };
        static readonly string[] PresetVLabels = { "SLOW", "MED", "FAST", "XFAST" };

        // Throat / inlet area ratio A2/A1
        static readonly float[] PresetsRatio = { 0.75f, 0.50f, 0.35f, 0.20f };
        static readonly string[] PresetRatioLabels = { "WIDE", "MED", "NARROW", "PINCH" };

        const float Rho = 1000f; // water kg/m^3
        const float Patm = 101325f; // Pa absolute reference
        const float G = 9.81f;
        const float TubeLen = 0.36f;
        const float InletR = 0.045f;
        const float TubeY = 0.12f;

        enum RunState { Paused, Running }

        TextMeshPro _readout;
        Transform _base;
        Transform _inlet;
        Transform _throat;
        Transform _outlet;
        Transform _tracer;
        Transform _mano1;
        Transform _mano2;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _tubeMat;
        Material _throatMat;
        Material _tracerMat;
        Material _manoMat;
        float _refreshAt;
        float _inputCooldown;
        int _vIndex = 1;
        int _ratioIndex = 1;
        RunState _state = RunState.Paused;
        float _tracerT;

        public float ActiveV1 => PresetsV[Mathf.Clamp(_vIndex, 0, PresetsV.Length - 1)];
        public float ActiveRatio => PresetsRatio[Mathf.Clamp(_ratioIndex, 0, PresetsRatio.Length - 1)];
        public string ActiveVLabel => PresetVLabels[Mathf.Clamp(_vIndex, 0, PresetVLabels.Length - 1)];
        public string ActiveRatioLabel => PresetRatioLabels[Mathf.Clamp(_ratioIndex, 0, PresetRatioLabels.Length - 1)];
        public float V2 => ActiveV1 / Mathf.Max(0.05f, ActiveRatio);
        public float DeltaP => 0.5f * Rho * (V2 * V2 - ActiveV1 * ActiveV1);
        public float P1 => Patm;
        public float P2 => Patm - DeltaP;
        public float HeadM => DeltaP / (Rho * G);
        public bool IsRunning => _state == RunState.Running;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            ApplyThroatScale();
            ApplyManometers();
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
            DestroyMat(ref _tubeMat);
            DestroyMat(ref _throatMat);
            DestroyMat(ref _tracerMat);
            DestroyMat(ref _manoMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }

        void Update()
        {
            PollDesktopCycle();
            if (_state == RunState.Running)
                AdvanceTracer(Time.deltaTime);
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.06f;
            ApplyManometers();
            ApplyGlow();
            RefreshText();
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
                if (shift) CycleRatio(+1);
                else CycleSpeed(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleRatio(-1);
                else CycleSpeed(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
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

        public void CycleRatio(int delta)
        {
            if (PresetsRatio.Length == 0)
                return;
            _ratioIndex = (_ratioIndex + delta) % PresetsRatio.Length;
            if (_ratioIndex < 0)
                _ratioIndex += PresetsRatio.Length;
            ApplyThroatScale();
            ApplyManometers();
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            if (delta != 0)
                CycleSpeed(delta);
            ToggleRun();
        }

        void ToggleRun()
        {
            if (_state == RunState.Running)
            {
                _state = RunState.Paused;
            }
            else
            {
                _state = RunState.Running;
                _tracerT = 0f;
            }
            RefreshText();
        }

        void AdvanceTracer(float dt)
        {
            if (_tracer == null)
                return;
            // Faster at throat: travel param 0..1 with speed proportional to local v
            float speed = 0.35f + 0.25f * ActiveV1 * (1f / Mathf.Max(0.2f, ActiveRatio));
            _tracerT += dt * speed * 0.55f;
            if (_tracerT > 1f)
                _tracerT -= 1f;
            float x = Mathf.Lerp(-TubeLen * 0.45f, TubeLen * 0.45f, _tracerT);
            // Shrink slightly in throat zone
            float mid = Mathf.Abs(_tracerT - 0.5f);
            float scale = mid < 0.12f ? Mathf.Lerp(0.7f, 1f, mid / 0.12f) : 1f;
            _tracer.localPosition = new Vector3(x, TubeY, 0f);
            _tracer.localScale = Vector3.one * (0.022f * scale);
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
                baseGo.transform.localScale = new Vector3(0.22f, 0.02f, 0.12f);
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

            _inlet = EnsureTubeSeg("Inlet", new Vector3(-TubeLen * 0.28f, TubeY, 0f), InletR, TubeLen * 0.28f, sh, ref _tubeMat, new Color(0.20f, 0.55f, 0.72f, 0.55f));
            _outlet = EnsureTubeSeg("Outlet", new Vector3(TubeLen * 0.28f, TubeY, 0f), InletR, TubeLen * 0.28f, sh, ref _tubeMat, new Color(0.20f, 0.55f, 0.72f, 0.55f));
            _throat = EnsureTubeSeg("Throat", new Vector3(0f, TubeY, 0f), InletR * 0.55f, TubeLen * 0.18f, sh, ref _throatMat, new Color(0.15f, 0.78f, 0.90f, 0.70f));

            _tracer = transform.Find("Tracer");
            if (_tracer == null)
            {
                var tGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tGo.name = "Tracer";
                tGo.transform.SetParent(transform, false);
                tGo.transform.localScale = Vector3.one * 0.022f;
                Object.Destroy(tGo.GetComponent<Collider>());
                _tracer = tGo.transform;
            }
            var tr = _tracer.GetComponent<Renderer>();
            if (tr != null)
            {
                if (_tracerMat == null)
                {
                    _tracerMat = new Material(sh);
                    var cyan = new Color(0.35f, 0.95f, 1f, 1f);
                    if (_tracerMat.HasProperty("_BaseColor"))
                        _tracerMat.SetColor("_BaseColor", cyan);
                    _tracerMat.color = cyan;
                    if (_tracerMat.HasProperty("_EmissionColor"))
                    {
                        _tracerMat.EnableKeyword("_EMISSION");
                        _tracerMat.SetColor("_EmissionColor", cyan * 1.2f);
                    }
                }
                tr.sharedMaterial = _tracerMat;
            }
            _tracer.localPosition = new Vector3(-TubeLen * 0.45f, TubeY, 0f);

            _mano1 = EnsureMano("ManoInlet", new Vector3(-TubeLen * 0.28f, TubeY + InletR + 0.01f, 0f), sh);
            _mano2 = EnsureMano("ManoThroat", new Vector3(0f, TubeY + InletR + 0.01f, 0f), sh);
        }

        Transform EnsureTubeSeg(string name, Vector3 pos, float radius, float lengthX, Shader sh, ref Material sharedMat, Color tint)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(transform, false);
                // Default cylinder is Y-up; rotate to X
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Object.Destroy(go.GetComponent<Collider>());
                t = go.transform;
            }
            t.localPosition = pos;
            t.localScale = new Vector3(radius * 2f, lengthX * 0.5f, radius * 2f);
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                if (sharedMat == null)
                {
                    sharedMat = new Material(sh);
                    MakeTransparent(sharedMat, tint);
                    if (sharedMat.HasProperty("_Metallic"))
                        sharedMat.SetFloat("_Metallic", 0.15f);
                    if (sharedMat.HasProperty("_Smoothness"))
                        sharedMat.SetFloat("_Smoothness", 0.75f);
                    if (sharedMat.HasProperty("_EmissionColor"))
                    {
                        sharedMat.EnableKeyword("_EMISSION");
                        sharedMat.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 0.35f);
                    }
                }
                r.sharedMaterial = sharedMat;
            }
            return t;
        }

        Transform EnsureMano(string name, Vector3 basePos, Shader sh)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(transform, false);
                go.transform.localRotation = Quaternion.identity;
                Object.Destroy(go.GetComponent<Collider>());
                t = go.transform;
            }
            t.localPosition = basePos;
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                if (_manoMat == null)
                {
                    _manoMat = new Material(sh);
                    var amber = new Color(0.95f, 0.55f, 0.15f, 0.85f);
                    if (_manoMat.HasProperty("_BaseColor"))
                        _manoMat.SetColor("_BaseColor", amber);
                    _manoMat.color = amber;
                    if (_manoMat.HasProperty("_EmissionColor"))
                    {
                        _manoMat.EnableKeyword("_EMISSION");
                        _manoMat.SetColor("_EmissionColor", amber * 0.6f);
                    }
                }
                r.sharedMaterial = _manoMat;
            }
            return t;
        }

        static void MakeTransparent(Material mat, Color color)
        {
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
        }

        void ApplyThroatScale()
        {
            if (_throat == null)
                return;
            float r = InletR * Mathf.Sqrt(Mathf.Clamp(ActiveRatio, 0.05f, 1f));
            _throat.localScale = new Vector3(r * 2f, TubeLen * 0.09f, r * 2f);
        }

        void ApplyManometers()
        {
            // Gauge height proportional to absolute pressure (higher column = higher P)
            // P1 ~ Patm, P2 lower -> shorter throat column. Scale for lab table.
            float h1 = 0.08f;
            float h2 = Mathf.Clamp(0.08f * (P2 / Patm), 0.015f, 0.12f);
            // When DeltaP large, emphasize drop: use head relative
            float headVis = Mathf.Clamp(HeadM * 0.04f, 0f, 0.07f);
            h2 = Mathf.Max(0.015f, h1 - headVis);

            if (_mano1 != null)
            {
                _mano1.localScale = new Vector3(0.012f, h1 * 0.5f, 0.012f);
                _mano1.localPosition = new Vector3(-TubeLen * 0.28f, TubeY + InletR + 0.01f + h1 * 0.5f, 0f);
            }
            if (_mano2 != null)
            {
                _mano2.localScale = new Vector3(0.012f, h2 * 0.5f, 0.012f);
                _mano2.localPosition = new Vector3(0f, TubeY + InletR * Mathf.Sqrt(ActiveRatio) + 0.01f + h2 * 0.5f, 0f);
            }
        }

        void ApplyGlow()
        {
            if (_throatMat == null || !_throatMat.HasProperty("_EmissionColor"))
                return;
            float glow = _state == RunState.Running ? 1.1f : 0.35f;
            var c = new Color(0.15f, 0.78f, 0.90f);
            _throatMat.EnableKeyword("_EMISSION");
            _throatMat.SetColor("_EmissionColor", c * glow);
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
            _readout.fontSize = 20f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.65f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(64f, 44f);
            _readout.text = "BERNOULLI\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string run = _state == RunState.Running ? "RUN" : "PAUSE";
            string v1 = "v1 " + ActiveV1.ToString("0.00") + " " + ActiveVLabel;
            string v2 = "v2 " + V2.ToString("0.00") + " m/s";
            string ar = "A2/A1 " + ActiveRatio.ToString("0.00") + " " + ActiveRatioLabel;
            string dp = "dP " + (DeltaP / 1000f).ToString("0.00") + " kPa";
            string head = "head " + HeadM.ToString("0.000") + " m";
            string p2g = "P2g " + ((P2 - Patm) / 1000f).ToString("0.00") + " kPa";

            _readout.text =
                "VENTURI " + ActiveVLabel + "/" + ActiveRatioLabel + "\n"
                + run + "\n"
                + v1 + "\n"
                + v2 + "\n"
                + ar + "\n"
                + dp + "  " + head + "\n"
                + p2g + "\n"
                + "N/P v1  Shift throat\n"
                + "activate run/pause\n"
                + "[Bernoulli]";
        }
    }
}