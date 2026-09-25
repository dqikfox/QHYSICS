using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Snell's Law / Refraction: ideal planar interface n1 sin theta1 = n2 sin theta2 with TIR.
    /// Honesty: ideal planar Snell - NOT dispersion, not surface roughness, not 3D raytrace, not graded-index.
    /// N/P cycles theta1; Shift+N/P cycles medium pair; XR activate toggles RUN/PAUSE tracer. Desktop via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadSnellGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Snell";
        public const string Honesty =
            "Ideal planar Snell interface n1 sin theta1 = n2 sin theta2; TIR when n1>n2 and theta1>thetac. NOT dispersion, not surface roughness, not 3D raytrace, not graded-index.";

        static readonly string[] PairLabels = { "AIR->WATER", "AIR->GLASS", "WATER->GLASS", "GLASS->AIR" };
        static readonly float[] PairN1 = { 1.000f, 1.000f, 1.333f, 1.500f };
        static readonly float[] PairN2 = { 1.333f, 1.500f, 1.500f, 1.000f };

        static readonly float[] PresetsTheta1 = { 15f, 30f, 45f, 60f };

        const float InterfaceY = 0.12f;
        const float RayLen = 0.18f;
        const float RayRadius = 0.006f;

        enum RunState { Paused, Running }

        TextMeshPro _readout;
        Transform _base;
        Transform _slab;
        Transform _edge;
        Transform _normalTick;
        Transform _incident;
        Transform _reflected;
        Transform _refracted;
        Transform _tirMarker;
        Transform _tracer;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _slabMat;
        Material _edgeMat;
        Material _incidentMat;
        Material _reflectMat;
        Material _refractMat;
        Material _tirMat;
        Material _tracerMat;
        float _refreshAt;
        float _inputCooldown;
        int _thetaIndex = 1;
        int _pairIndex = 0;
        RunState _state = RunState.Paused;
        float _tracerT;
        bool _isTir;
        float _theta2Deg;
        float _thetaCDeg;

        public float ActiveN1 => PairN1[Mathf.Clamp(_pairIndex, 0, PairN1.Length - 1)];
        public float ActiveN2 => PairN2[Mathf.Clamp(_pairIndex, 0, PairN2.Length - 1)];
        public string ActivePairLabel => PairLabels[Mathf.Clamp(_pairIndex, 0, PairLabels.Length - 1)];
        public float ActiveTheta1 => PresetsTheta1[Mathf.Clamp(_thetaIndex, 0, PresetsTheta1.Length - 1)];
        public float ActiveTheta2 => _theta2Deg;
        public float ActiveThetaC => _thetaCDeg;
        public bool IsTir => _isTir;
        public bool IsRunning => _state == RunState.Running;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            Recompute();
            ApplyRays();
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
            DestroyMat(ref _slabMat);
            DestroyMat(ref _edgeMat);
            DestroyMat(ref _incidentMat);
            DestroyMat(ref _reflectMat);
            DestroyMat(ref _refractMat);
            DestroyMat(ref _tirMat);
            DestroyMat(ref _tracerMat);
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
                if (shift) CyclePair(+1);
                else CycleTheta(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CyclePair(-1);
                else CycleTheta(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleTheta(int delta)
        {
            if (PresetsTheta1.Length == 0)
                return;
            _thetaIndex = (_thetaIndex + delta) % PresetsTheta1.Length;
            if (_thetaIndex < 0)
                _thetaIndex += PresetsTheta1.Length;
            Recompute();
            ApplyRays();
            RefreshText();
        }

        public void CyclePair(int delta)
        {
            if (PairLabels.Length == 0)
                return;
            _pairIndex = (_pairIndex + delta) % PairLabels.Length;
            if (_pairIndex < 0)
                _pairIndex += PairLabels.Length;
            Recompute();
            ApplyRays();
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            if (delta != 0)
                CycleTheta(delta);
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
            if (_tracer != null)
                _tracer.gameObject.SetActive(_state == RunState.Running);
            RefreshText();
        }

        void Recompute()
        {
            float n1 = ActiveN1;
            float n2 = ActiveN2;
            float th1 = ActiveTheta1 * Mathf.Deg2Rad;

            if (n1 > n2 && n1 > 1e-6f)
                _thetaCDeg = Mathf.Asin(Mathf.Clamp(n2 / n1, 0f, 1f)) * Mathf.Rad2Deg;
            else
                _thetaCDeg = 90f;

            float arg = (n1 / Mathf.Max(1e-6f, n2)) * Mathf.Sin(th1);
            if (arg > 1f)
            {
                _isTir = true;
                _theta2Deg = float.NaN;
            }
            else
            {
                _isTir = false;
                _theta2Deg = Mathf.Asin(Mathf.Clamp(arg, -1f, 1f)) * Mathf.Rad2Deg;
            }
        }

        void AdvanceTracer(float dt)
        {
            if (_tracer == null)
                return;
            _tracerT += dt * 0.55f;
            if (_tracerT > 1f)
                _tracerT -= 1f;

            float th1 = ActiveTheta1 * Mathf.Deg2Rad;
            Vector3 hit = new Vector3(0f, InterfaceY, 0f);
            Vector3 incDir = new Vector3(Mathf.Sin(th1), -Mathf.Cos(th1), 0f);
            Vector3 refDir = new Vector3(Mathf.Sin(th1), Mathf.Cos(th1), 0f);

            if (_tracerT < 0.45f)
            {
                float u = _tracerT / 0.45f;
                _tracer.localPosition = Vector3.Lerp(hit - incDir * RayLen, hit, u);
            }
            else
            {
                float u = (_tracerT - 0.45f) / 0.55f;
                if (_isTir)
                    _tracer.localPosition = Vector3.Lerp(hit, hit + refDir * RayLen, u);
                else
                {
                    float th2 = _theta2Deg * Mathf.Deg2Rad;
                    Vector3 refrDir = new Vector3(Mathf.Sin(th2), -Mathf.Cos(th2), 0f);
                    _tracer.localPosition = Vector3.Lerp(hit, hit + refrDir * RayLen, u);
                }
            }
            _tracer.localScale = Vector3.one * 0.018f;
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

            _slab = EnsureBox("Interface", new Vector3(0f, InterfaceY, 0f), new Vector3(0.36f, 0.008f, 0.16f), sh, ref _slabMat, new Color(0.10f, 0.14f, 0.18f, 0.72f), transparent: true);
            _edge = EnsureBox("Edge", new Vector3(0f, InterfaceY, 0.082f), new Vector3(0.36f, 0.012f, 0.006f), sh, ref _edgeMat, new Color(0.25f, 0.90f, 1f, 1f), transparent: false, emit: true);

            _normalTick = EnsureRay("Normal", sh, ref _edgeMat, new Color(0.55f, 0.95f, 1f, 1f));
            _incident = EnsureRay("Incident", sh, ref _incidentMat, new Color(0.25f, 0.90f, 1f, 1f));
            _reflected = EnsureRay("Reflected", sh, ref _reflectMat, new Color(0.20f, 0.55f, 0.70f, 0.85f));
            _refracted = EnsureRay("Refracted", sh, ref _refractMat, new Color(0.35f, 0.98f, 1f, 1f));

            _tirMarker = transform.Find("TirMarker");
            if (_tirMarker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "TirMarker";
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.028f;
                Object.Destroy(go.GetComponent<Collider>());
                _tirMarker = go.transform;
            }
            var tirR = _tirMarker.GetComponent<Renderer>();
            if (tirR != null)
            {
                if (_tirMat == null)
                {
                    _tirMat = new Material(sh);
                    var amber = new Color(1f, 0.55f, 0.15f, 1f);
                    if (_tirMat.HasProperty("_BaseColor"))
                        _tirMat.SetColor("_BaseColor", amber);
                    _tirMat.color = amber;
                    if (_tirMat.HasProperty("_EmissionColor"))
                    {
                        _tirMat.EnableKeyword("_EMISSION");
                        _tirMat.SetColor("_EmissionColor", amber * 1.1f);
                    }
                }
                tirR.sharedMaterial = _tirMat;
            }
            _tirMarker.localPosition = new Vector3(0f, InterfaceY, 0f);

            _tracer = transform.Find("Tracer");
            if (_tracer == null)
            {
                var tGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tGo.name = "Tracer";
                tGo.transform.SetParent(transform, false);
                tGo.transform.localScale = Vector3.one * 0.018f;
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
            _tracer.localPosition = new Vector3(-RayLen * 0.5f, InterfaceY + RayLen * 0.5f, 0f);
            _tracer.gameObject.SetActive(false);
        }

        Transform EnsureBox(string name, Vector3 pos, Vector3 scale, Shader sh, ref Material sharedMat, Color tint, bool transparent, bool emit = false)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(transform, false);
                Object.Destroy(go.GetComponent<Collider>());
                t = go.transform;
            }
            t.localPosition = pos;
            t.localRotation = Quaternion.identity;
            t.localScale = scale;
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                if (sharedMat == null)
                {
                    sharedMat = new Material(sh);
                    if (transparent)
                        MakeTransparent(sharedMat, tint);
                    else
                    {
                        if (sharedMat.HasProperty("_BaseColor"))
                            sharedMat.SetColor("_BaseColor", tint);
                        sharedMat.color = tint;
                    }
                    if (sharedMat.HasProperty("_Metallic"))
                        sharedMat.SetFloat("_Metallic", 0.2f);
                    if (sharedMat.HasProperty("_Smoothness"))
                        sharedMat.SetFloat("_Smoothness", 0.7f);
                    if (emit && sharedMat.HasProperty("_EmissionColor"))
                    {
                        sharedMat.EnableKeyword("_EMISSION");
                        sharedMat.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 0.7f);
                    }
                }
                r.sharedMaterial = sharedMat;
            }
            return t;
        }

        Transform EnsureRay(string name, Shader sh, ref Material sharedMat, Color tint)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(transform, false);
                Object.Destroy(go.GetComponent<Collider>());
                t = go.transform;
            }
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                if (sharedMat == null)
                {
                    sharedMat = new Material(sh);
                    if (sharedMat.HasProperty("_BaseColor"))
                        sharedMat.SetColor("_BaseColor", tint);
                    sharedMat.color = tint;
                    if (sharedMat.HasProperty("_Metallic"))
                        sharedMat.SetFloat("_Metallic", 0.1f);
                    if (sharedMat.HasProperty("_Smoothness"))
                        sharedMat.SetFloat("_Smoothness", 0.8f);
                    if (sharedMat.HasProperty("_EmissionColor"))
                    {
                        sharedMat.EnableKeyword("_EMISSION");
                        sharedMat.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 0.85f);
                    }
                }
                r.sharedMaterial = sharedMat;
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

        static void PlaceRay(Transform t, Vector3 from, Vector3 to, float radius)
        {
            if (t == null)
                return;
            Vector3 mid = (from + to) * 0.5f;
            Vector3 delta = to - from;
            float len = delta.magnitude;
            if (len < 1e-5f)
            {
                t.gameObject.SetActive(false);
                return;
            }
            t.gameObject.SetActive(true);
            t.localPosition = mid;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            t.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
        }

        void ApplyRays()
        {
            float th1 = ActiveTheta1 * Mathf.Deg2Rad;
            Vector3 hit = new Vector3(0f, InterfaceY, 0f);
            Vector3 incDir = new Vector3(Mathf.Sin(th1), -Mathf.Cos(th1), 0f);
            Vector3 refDir = new Vector3(Mathf.Sin(th1), Mathf.Cos(th1), 0f);

            PlaceRay(_incident, hit - incDir * RayLen, hit, RayRadius);
            PlaceRay(_reflected, hit, hit + refDir * RayLen, RayRadius * 0.85f);
            PlaceRay(_normalTick, hit + Vector3.up * 0.045f, hit - Vector3.up * 0.045f, RayRadius * 0.55f);

            if (_isTir)
            {
                if (_refracted != null)
                    _refracted.gameObject.SetActive(false);
                if (_tirMarker != null)
                    _tirMarker.gameObject.SetActive(true);
            }
            else
            {
                float th2 = _theta2Deg * Mathf.Deg2Rad;
                Vector3 refrDir = new Vector3(Mathf.Sin(th2), -Mathf.Cos(th2), 0f);
                PlaceRay(_refracted, hit, hit + refrDir * RayLen, RayRadius);
                if (_tirMarker != null)
                    _tirMarker.gameObject.SetActive(false);
            }

            if (_tracer != null)
                _tracer.gameObject.SetActive(_state == RunState.Running);
        }

        void ApplyGlow()
        {
            if (_refractMat != null && _refractMat.HasProperty("_EmissionColor"))
            {
                float glow = _state == RunState.Running ? 1.2f : 0.7f;
                var c = new Color(0.35f, 0.98f, 1f);
                _refractMat.EnableKeyword("_EMISSION");
                _refractMat.SetColor("_EmissionColor", c * glow);
            }
            if (_tracer != null)
                _tracer.gameObject.SetActive(_state == RunState.Running);
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
            _readout.text = "SNELL\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string run = _state == RunState.Running ? "RUN" : "PAUSE";
            string pair = ActivePairLabel;
            string n = "n1 " + ActiveN1.ToString("0.000") + "  n2 " + ActiveN2.ToString("0.000");
            string th1 = "th1 " + ActiveTheta1.ToString("0") + " deg";
            string th2 = _isTir ? "th2 TIR" : ("th2 " + _theta2Deg.ToString("0.0") + " deg");
            string thc = "thc " + _thetaCDeg.ToString("0.0") + " deg";

            _readout.text =
                "SNELL " + pair + "\n"
                + run + "\n"
                + n + "\n"
                + th1 + "  " + th2 + "\n"
                + thc + "\n"
                + "N/P th1  Shift pair\n"
                + "activate run/pause\n"
                + "[Snell]";
        }
    }
}