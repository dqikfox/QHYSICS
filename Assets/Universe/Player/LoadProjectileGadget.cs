using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS projectile / ballistic range: ideal 2D no-drag on flat ground.
    /// Honesty: ideal kinematic projectile — NOT 3D wind, not Magnus, not bouncing, not Unity rigid-body ball sim.
    /// XR activate launches / resets; N/P cycles angle; Shift+N/P cycles speed. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadProjectileGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Projectile";
        public const string Honesty =
            "Ideal no-drag 2D projectile on flat ground: R=v^2 sin(2theta)/g, T=2v sin(theta)/g, H=(v sin(theta))^2/(2g). NOT 3D wind, not Magnus, not bouncing, not rigid-body Unity physics sim of the ball (kinematic visual).";

        // Launch speed presets (m/s) — lab-scaled
        static readonly float[] PresetsV = { 2f, 4f, 6f, 8f };
        static readonly string[] PresetVLabels = { "SLOW", "MED", "FAST", "XFAST" };

        // Launch angle presets (degrees)
        static readonly float[] PresetsThetaDeg = { 15f, 30f, 45f, 75f };
        static readonly string[] PresetThetaLabels = { "LO", "MED", "HI", "VERT" };

        const float Gravity = 9.81f;
        const float VisualScale = 0.12f; // compress physical meters into ~0.6–1.0 m lab prop
        const float MuzzleLocalY = 0.08f;
        const int ArcMarkers = 10;
        const float BallRadius = 0.025f;

        enum FlightState { Ready, Flight, Landed }

        TextMeshPro _readout;
        Transform _base;
        Transform _barrel;
        Transform _ball;
        Transform _arcRoot;
        Transform[] _markers;
        LineRenderer _arcLine;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _barrelMat;
        Material _ballMat;
        Material _markerMat;
        float _refreshAt;
        float _inputCooldown;
        int _vIndex = 1;     // default MED 4 m/s
        int _thetaIndex = 2; // default HI 45° (max range interest)
        FlightState _state = FlightState.Ready;
        float _flightT;
        float _rangePhys;
        float _tofPhys;
        float _maxHPhys;

        public float ActiveV => PresetsV[Mathf.Clamp(_vIndex, 0, PresetsV.Length - 1)];
        public float ActiveThetaDeg => PresetsThetaDeg[Mathf.Clamp(_thetaIndex, 0, PresetsThetaDeg.Length - 1)];
        public float ActiveThetaRad => ActiveThetaDeg * Mathf.Deg2Rad;
        public string ActiveVLabel => PresetVLabels[Mathf.Clamp(_vIndex, 0, PresetVLabels.Length - 1)];
        public string ActiveThetaLabel => PresetThetaLabels[Mathf.Clamp(_thetaIndex, 0, PresetThetaLabels.Length - 1)];
        public float RangeMeters => _rangePhys;
        public float TimeOfFlightSec => _tofPhys;
        public float MaxHeightMeters => _maxHPhys;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            RecomputeIdeal();
            ApplyArcVisual();
            ApplyBallPose();
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
            DestroyMat(ref _barrelMat);
            DestroyMat(ref _ballMat);
            DestroyMat(ref _markerMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }

        void RecomputeIdeal()
        {
            float v = ActiveV;
            float th = ActiveThetaRad;
            float s2 = Mathf.Sin(2f * th);
            float s = Mathf.Sin(th);
            _rangePhys = (v * v * s2) / Gravity;
            _tofPhys = (2f * v * s) / Gravity;
            _maxHPhys = (v * s) * (v * s) / (2f * Gravity);
            if (_rangePhys < 0f) _rangePhys = 0f;
            if (_tofPhys < 0f) _tofPhys = 0f;
            if (_maxHPhys < 0f) _maxHPhys = 0f;
        }

        static Vector2 PhysPos(float v, float th, float t)
        {
            float x = v * Mathf.Cos(th) * t;
            float y = v * Mathf.Sin(th) * t - 0.5f * Gravity * t * t;
            return new Vector2(x, Mathf.Max(0f, y));
        }

        void FixedUpdate()
        {
            if (_state != FlightState.Flight)
                return;
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
                return;
            _flightT += dt;
            if (_flightT >= _tofPhys - 1e-4f)
            {
                _flightT = _tofPhys;
                _state = FlightState.Landed;
            }
            ApplyBallPose();
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
            LaunchOrReset();
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
                else CycleAngle(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleSpeed(-1);
                else CycleAngle(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleAngle(int delta)
        {
            if (PresetThetaLabels.Length == 0)
                return;
            _thetaIndex = (_thetaIndex + delta) % PresetThetaLabels.Length;
            if (_thetaIndex < 0)
                _thetaIndex += PresetThetaLabels.Length;
            ResetToReady();
            RefreshText();
        }

        public void CycleSpeed(int delta)
        {
            if (PresetsV.Length == 0)
                return;
            _vIndex = (_vIndex + delta) % PresetsV.Length;
            if (_vIndex < 0)
                _vIndex += PresetsV.Length;
            ResetToReady();
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            if (delta != 0)
                CycleAngle(delta);
            LaunchOrReset();
        }

        void LaunchOrReset()
        {
            if (_state == FlightState.Flight)
            {
                ResetToReady();
                return;
            }
            RecomputeIdeal();
            ApplyArcVisual();
            _flightT = 0f;
            _state = FlightState.Flight;
            ApplyBallPose();
            RefreshText();
        }

        void ResetToReady()
        {
            _state = FlightState.Ready;
            _flightT = 0f;
            RecomputeIdeal();
            ApplyArcVisual();
            ApplyBallPose();
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
                baseGo.transform.localPosition = new Vector3(0.15f, 0.02f, 0f);
                baseGo.transform.localRotation = Quaternion.identity;
                baseGo.transform.localScale = new Vector3(0.50f, 0.04f, 0.18f);
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

            _barrel = transform.Find("Barrel");
            if (_barrel == null)
            {
                var barrelGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrelGo.name = "Barrel";
                barrelGo.transform.SetParent(transform, false);
                barrelGo.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);
                Object.Destroy(barrelGo.GetComponent<Collider>());
                _barrel = barrelGo.transform;
            }
            var bar = _barrel.GetComponent<Renderer>();
            if (bar != null)
            {
                if (_barrelMat == null)
                {
                    _barrelMat = new Material(sh);
                    var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
                    if (_barrelMat.HasProperty("_BaseColor"))
                        _barrelMat.SetColor("_BaseColor", cyan);
                    _barrelMat.color = cyan;
                    if (_barrelMat.HasProperty("_Metallic"))
                        _barrelMat.SetFloat("_Metallic", 0.5f);
                    if (_barrelMat.HasProperty("_Smoothness"))
                        _barrelMat.SetFloat("_Smoothness", 0.55f);
                    if (_barrelMat.HasProperty("_EmissionColor"))
                    {
                        _barrelMat.EnableKeyword("_EMISSION");
                        _barrelMat.SetColor("_EmissionColor", cyan * 0.3f);
                    }
                }
                bar.sharedMaterial = _barrelMat;
            }

            _arcRoot = transform.Find("Arc");
            if (_arcRoot == null)
            {
                var arcGo = new GameObject("Arc");
                arcGo.transform.SetParent(transform, false);
                arcGo.transform.localPosition = Vector3.zero;
                _arcRoot = arcGo.transform;
            }

            if (_markers == null || _markers.Length != ArcMarkers)
                _markers = new Transform[ArcMarkers];
            if (_markerMat == null)
            {
                _markerMat = new Material(sh);
                var soft = new Color(0.25f, 0.75f, 0.85f, 1f);
                if (_markerMat.HasProperty("_BaseColor"))
                    _markerMat.SetColor("_BaseColor", soft);
                _markerMat.color = soft;
                if (_markerMat.HasProperty("_EmissionColor"))
                {
                    _markerMat.EnableKeyword("_EMISSION");
                    _markerMat.SetColor("_EmissionColor", soft * 0.4f);
                }
            }
            for (int i = 0; i < ArcMarkers; i++)
            {
                string name = "Marker" + i;
                Transform m = _arcRoot.Find(name);
                if (m == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = name;
                    go.transform.SetParent(_arcRoot, false);
                    go.transform.localScale = Vector3.one * 0.018f;
                    Object.Destroy(go.GetComponent<Collider>());
                    m = go.transform;
                }
                var mr = m.GetComponent<Renderer>();
                if (mr != null)
                    mr.sharedMaterial = _markerMat;
                _markers[i] = m;
            }

            _arcLine = _arcRoot.GetComponent<LineRenderer>();
            if (_arcLine == null)
                _arcLine = _arcRoot.gameObject.AddComponent<LineRenderer>();
            _arcLine.useWorldSpace = false;
            _arcLine.widthMultiplier = 0.008f;
            _arcLine.positionCount = ArcMarkers;
            if (_arcLine.sharedMaterial == null && _markerMat != null)
                _arcLine.sharedMaterial = _markerMat;
            _arcLine.startColor = new Color(0.3f, 0.85f, 0.95f, 0.9f);
            _arcLine.endColor = new Color(0.2f, 0.6f, 0.75f, 0.5f);

            _ball = transform.Find("Ball");
            if (_ball == null)
            {
                var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballGo.name = "Ball";
                ballGo.transform.SetParent(transform, false);
                ballGo.transform.localScale = Vector3.one * (BallRadius * 2f);
                Object.Destroy(ballGo.GetComponent<Collider>());
                _ball = ballGo.transform;
            }
            var blr = _ball.GetComponent<Renderer>();
            if (blr != null)
            {
                if (_ballMat == null)
                {
                    _ballMat = new Material(sh);
                    var teal = new Color(0.15f, 0.80f, 0.88f, 1f);
                    if (_ballMat.HasProperty("_BaseColor"))
                        _ballMat.SetColor("_BaseColor", teal);
                    _ballMat.color = teal;
                    if (_ballMat.HasProperty("_Metallic"))
                        _ballMat.SetFloat("_Metallic", 0.35f);
                    if (_ballMat.HasProperty("_Smoothness"))
                        _ballMat.SetFloat("_Smoothness", 0.6f);
                    if (_ballMat.HasProperty("_EmissionColor"))
                    {
                        _ballMat.EnableKeyword("_EMISSION");
                        _ballMat.SetColor("_EmissionColor", teal * 0.5f);
                    }
                }
                blr.sharedMaterial = _ballMat;
            }

            ApplyBarrelPose();
        }

        void ApplyBarrelPose()
        {
            if (_barrel == null)
                return;
            float th = ActiveThetaDeg;
            // Cylinder default is Y-up; tip along +X after rotate about Z.
            float halfLen = 0.08f;
            float rad = th * Mathf.Deg2Rad;
            float cx = halfLen * Mathf.Cos(rad);
            float cy = MuzzleLocalY + halfLen * Mathf.Sin(rad);
            _barrel.localPosition = new Vector3(cx, cy, 0f);
            _barrel.localRotation = Quaternion.Euler(0f, 0f, th - 90f);
        }

        void ApplyArcVisual()
        {
            ApplyBarrelPose();
            float v = ActiveV;
            float th = ActiveThetaRad;
            float T = Mathf.Max(_tofPhys, 1e-4f);
            for (int i = 0; i < ArcMarkers; i++)
            {
                float u = (ArcMarkers == 1) ? 0f : (float)i / (ArcMarkers - 1);
                float t = u * T;
                Vector2 p = PhysPos(v, th, t);
                Vector3 local = new Vector3(p.x * VisualScale, MuzzleLocalY + p.y * VisualScale, 0f);
                if (_markers != null && i < _markers.Length && _markers[i] != null)
                    _markers[i].localPosition = local;
                if (_arcLine != null)
                    _arcLine.SetPosition(i, local);
            }
        }

        void ApplyBallPose()
        {
            if (_ball == null)
                return;
            float v = ActiveV;
            float th = ActiveThetaRad;
            float t;
            if (_state == FlightState.Ready)
                t = 0f;
            else if (_state == FlightState.Landed)
                t = _tofPhys;
            else
                t = _flightT;
            Vector2 p = PhysPos(v, th, t);
            _ball.localPosition = new Vector3(p.x * VisualScale, MuzzleLocalY + p.y * VisualScale, 0f);
        }

        void ApplyGlow()
        {
            if (_barrelMat == null || !_barrelMat.HasProperty("_EmissionColor"))
                return;
            var cyan = new Color(0.18f, 0.55f, 0.62f, 1f);
            _barrelMat.EnableKeyword("_EMISSION");
            float glow = _state == FlightState.Flight ? 1.2f : (_state == FlightState.Landed ? 0.55f : 0.3f);
            _barrelMat.SetColor("_EmissionColor", cyan * glow);
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
                go.transform.localPosition = new Vector3(0.15f, 0.42f, 0f);
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
            _readout.text = "PROJECTILE\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            RecomputeIdeal();
            string status = _state == FlightState.Ready ? "READY"
                : (_state == FlightState.Flight ? "FLIGHT" : "LANDED");
            string vStr = "v " + ActiveV.ToString("0") + "m/s";
            string thStr = "θ " + ActiveThetaDeg.ToString("0") + "°";
            string rStr = "R " + _rangePhys.ToString("0.00") + "m";
            string tStr = "T " + _tofPhys.ToString("0.00") + "s";
            string hStr = "H " + _maxHPhys.ToString("0.00") + "m";

            _readout.text =
                "PROJ " + ActiveVLabel + "/" + ActiveThetaLabel + " " + status + "\n"
                + vStr + "  " + thStr + "\n"
                + rStr + "  " + tStr + "\n"
                + hStr + "\n"
                + "N/P angle  Shift v\n"
                + "activate launch\n"
                + "[ideal projectile]";
        }
    }
}
