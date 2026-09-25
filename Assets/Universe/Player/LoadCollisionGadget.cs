using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS 1D collision / momentum: elastic + perfectly inelastic along a track.
    /// Honesty: lumped 1D collisions along a track - NOT 2D/3D rigidbody contact, not friction, not rotation, not deformation.
    /// XR activate toggles ELASTIC/INELASTIC; N/P cycles mass ratio; Shift+N/P cycles approach speed. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadCollisionGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Collision";
        public const string Honesty =
            "Lumped 1D collisions along a track: elastic 1D formulas or sticky inelastic. NOT 2D/3D rigidbody contact, not friction, not rotation, not deformation.";

        // Mass ratio m2/m1 presets (m1 fixed)
        static readonly float[] PresetsRatio = { 1.0f, 0.5f, 2.0f, 4.0f };
        static readonly string[] PresetRatioLabels = { "EQ", "LIGHT", "HEAVY", "XL" };

        // Approach speed presets (m/s) - m1 approaches, m2 initially at rest
        static readonly float[] PresetsV = { 0.5f, 1.0f, 2.0f, 4.0f };
        static readonly string[] PresetVLabels = { "SLOW", "MED", "FAST", "XFAST" };

        const float Mass1Kg = 0.25f;
        const float BobRadius = 0.028f;
        const float TrackHalf = 0.22f;
        const float TrackY = 0.10f;
        const float VisualSpeedScale = 0.35f;
        const float SeparatingHold = 1.2f;
        const float ResetGap = 0.08f;

        enum RunState { Paused, Approaching, Separating }
        enum CollisionMode { Elastic, Inelastic }

        TextMeshPro _readout;
        Transform _base;
        Transform _track;
        Transform _bob1;
        Transform _bob2;
        LineRenderer _trackLine;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _trackMat;
        Material _bob1Mat;
        Material _bob2Mat;
        Material _lineMat;
        float _refreshAt;
        float _inputCooldown;
        int _ratioIndex = 0;
        int _vIndex = 1;
        RunState _state = RunState.Paused;
        CollisionMode _mode = CollisionMode.Elastic;
        float _x1;
        float _x2;
        float _v1;
        float _v2;
        float _pBefore;
        float _pAfter;
        float _keBefore;
        float _keAfter;
        float _separatingUntil;

        public float ActiveRatio => PresetsRatio[Mathf.Clamp(_ratioIndex, 0, PresetsRatio.Length - 1)];
        public float ActiveV => PresetsV[Mathf.Clamp(_vIndex, 0, PresetsV.Length - 1)];
        public string ActiveRatioLabel => PresetRatioLabels[Mathf.Clamp(_ratioIndex, 0, PresetRatioLabels.Length - 1)];
        public string ActiveVLabel => PresetVLabels[Mathf.Clamp(_vIndex, 0, PresetVLabels.Length - 1)];
        public float Mass1 => Mass1Kg;
        public float Mass2 => Mass1Kg * ActiveRatio;
        public bool IsRunning => _state != RunState.Paused;
        public bool IsElastic => _mode == CollisionMode.Elastic;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            if (_state == RunState.Paused)
                ResetPositionsIdle();
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
            DestroyMat(ref _trackMat);
            DestroyMat(ref _bob1Mat);
            DestroyMat(ref _bob2Mat);
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
            if (_state == RunState.Paused)
                return;
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
                return;

            if (_state == RunState.Approaching)
            {
                _x1 += _v1 * VisualSpeedScale * dt;
                _x2 += _v2 * VisualSpeedScale * dt;
                float contact = BobRadius * 2f * 0.95f;
                if (_x2 - _x1 <= contact)
                {
                    float mid = 0.5f * (_x1 + _x2);
                    _x1 = mid - contact * 0.5f;
                    _x2 = mid + contact * 0.5f;
                    ApplyCollisionFormulas();
                    _state = RunState.Separating;
                    _separatingUntil = Time.time + SeparatingHold;
                }
            }
            else if (_state == RunState.Separating)
            {
                _x1 += _v1 * VisualSpeedScale * dt;
                _x2 += _v2 * VisualSpeedScale * dt;
                float lim = TrackHalf - BobRadius;
                if (_x1 < -lim) { _x1 = -lim; _v1 = 0f; }
                if (_x1 > lim) { _x1 = lim; _v1 = 0f; }
                if (_x2 < -lim) { _x2 = -lim; _v2 = 0f; }
                if (_x2 > lim) { _x2 = lim; _v2 = 0f; }

                if (Time.time >= _separatingUntil)
                    BeginApproach();
            }

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
            ToggleModeOrRun();
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
                else CycleRatio(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleSpeed(-1);
                else CycleRatio(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleRatio(int delta)
        {
            if (PresetsRatio.Length == 0)
                return;
            _ratioIndex = (_ratioIndex + delta) % PresetsRatio.Length;
            if (_ratioIndex < 0)
                _ratioIndex += PresetsRatio.Length;
            ApplyBobScale();
            if (_state == RunState.Paused)
                ResetPositionsIdle();
            else
                BeginApproach();
            RefreshText();
        }

        public void CycleSpeed(int delta)
        {
            if (PresetsV.Length == 0)
                return;
            _vIndex = (_vIndex + delta) % PresetsV.Length;
            if (_vIndex < 0)
                _vIndex += PresetsV.Length;
            if (_state != RunState.Paused)
                BeginApproach();
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            if (delta != 0)
                CycleRatio(delta);
            ToggleModeOrRun();
        }

        // Activate: if paused, start run; if running, toggle elastic/inelastic and restart shot.
        void ToggleModeOrRun()
        {
            if (_state == RunState.Paused)
            {
                BeginApproach();
            }
            else
            {
                _mode = _mode == CollisionMode.Elastic ? CollisionMode.Inelastic : CollisionMode.Elastic;
                BeginApproach();
            }
            RefreshText();
        }

        public void ToggleMode()
        {
            _mode = _mode == CollisionMode.Elastic ? CollisionMode.Inelastic : CollisionMode.Elastic;
            if (_state != RunState.Paused)
                BeginApproach();
            RefreshText();
        }

        public void Pause()
        {
            _state = RunState.Paused;
            ResetPositionsIdle();
            ApplyBobPose();
            RefreshText();
        }

        void BeginApproach()
        {
            float gap = BobRadius * 2f + ResetGap;
            _x1 = -TrackHalf + BobRadius + 0.02f;
            _x2 = _x1 + gap + 0.12f;
            if (_x2 > TrackHalf - BobRadius)
                _x2 = TrackHalf - BobRadius;
            // Classic: m1 approaches from left at +V, m2 initially at rest.
            _v1 = ActiveV;
            _v2 = 0f;
            _pBefore = Mass1 * _v1 + Mass2 * _v2;
            _keBefore = 0.5f * Mass1 * _v1 * _v1 + 0.5f * Mass2 * _v2 * _v2;
            _pAfter = _pBefore;
            _keAfter = _keBefore;
            _state = RunState.Approaching;
            ApplyBobPose();
            RefreshText();
        }

        void ResetPositionsIdle()
        {
            float gap = BobRadius * 2f + ResetGap;
            _x1 = -0.10f;
            _x2 = _x1 + gap + 0.06f;
            _v1 = 0f;
            _v2 = 0f;
            _pBefore = 0f;
            _pAfter = 0f;
            _keBefore = 0f;
            _keAfter = 0f;
        }

        void ApplyCollisionFormulas()
        {
            float m1 = Mass1;
            float m2 = Mass2;
            float v1 = _v1;
            float v2 = _v2;
            _pBefore = m1 * v1 + m2 * v2;
            _keBefore = 0.5f * m1 * v1 * v1 + 0.5f * m2 * v2 * v2;

            float sum = m1 + m2;
            if (sum < 1e-8f)
            {
                _v1 = 0f;
                _v2 = 0f;
            }
            else if (_mode == CollisionMode.Elastic)
            {
                // Elastic 1D: v1' = (m1-m2)/(m1+m2)*v1 + 2*m2/(m1+m2)*v2
                //            v2' = 2*m1/(m1+m2)*v1 + (m2-m1)/(m1+m2)*v2
                float v1p = ((m1 - m2) / sum) * v1 + (2f * m2 / sum) * v2;
                float v2p = (2f * m1 / sum) * v1 + ((m2 - m1) / sum) * v2;
                _v1 = v1p;
                _v2 = v2p;
            }
            else
            {
                // Perfectly inelastic (sticky): common velocity
                float vp = (m1 * v1 + m2 * v2) / sum;
                _v1 = vp;
                _v2 = vp;
            }

            _pAfter = m1 * _v1 + m2 * _v2;
            _keAfter = 0.5f * m1 * _v1 * _v1 + 0.5f * m2 * _v2 * _v2;
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
                baseGo.transform.localScale = new Vector3(0.18f, 0.02f, 0.10f);
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

            _track = transform.Find("Track");
            if (_track == null)
            {
                var trackGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trackGo.name = "Track";
                trackGo.transform.SetParent(transform, false);
                trackGo.transform.localPosition = new Vector3(0f, TrackY - 0.02f, 0f);
                trackGo.transform.localRotation = Quaternion.identity;
                trackGo.transform.localScale = new Vector3(TrackHalf * 2f + 0.06f, 0.012f, 0.04f);
                Object.Destroy(trackGo.GetComponent<Collider>());
                _track = trackGo.transform;
            }
            var tr = _track.GetComponent<Renderer>();
            if (tr != null)
            {
                if (_trackMat == null)
                {
                    _trackMat = new Material(sh);
                    var cyan = new Color(0.16f, 0.50f, 0.58f, 1f);
                    if (_trackMat.HasProperty("_BaseColor"))
                        _trackMat.SetColor("_BaseColor", cyan);
                    _trackMat.color = cyan;
                    if (_trackMat.HasProperty("_Metallic"))
                        _trackMat.SetFloat("_Metallic", 0.45f);
                    if (_trackMat.HasProperty("_Smoothness"))
                        _trackMat.SetFloat("_Smoothness", 0.5f);
                    if (_trackMat.HasProperty("_EmissionColor"))
                    {
                        _trackMat.EnableKeyword("_EMISSION");
                        _trackMat.SetColor("_EmissionColor", cyan * 0.25f);
                    }
                }
                tr.sharedMaterial = _trackMat;
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

            Transform lineHost = transform.Find("TrackLine");
            if (lineHost == null)
            {
                var lineGo = new GameObject("TrackLine");
                lineGo.transform.SetParent(transform, false);
                lineGo.transform.localPosition = new Vector3(0f, TrackY, 0f);
                lineHost = lineGo.transform;
            }
            _trackLine = lineHost.GetComponent<LineRenderer>();
            if (_trackLine == null)
                _trackLine = lineHost.gameObject.AddComponent<LineRenderer>();
            _trackLine.useWorldSpace = false;
            _trackLine.widthMultiplier = 0.006f;
            _trackLine.loop = false;
            _trackLine.positionCount = 2;
            _trackLine.SetPosition(0, new Vector3(-TrackHalf, 0f, 0f));
            _trackLine.SetPosition(1, new Vector3(TrackHalf, 0f, 0f));
            if (_trackLine.sharedMaterial == null)
                _trackLine.sharedMaterial = _lineMat;
            _trackLine.startColor = new Color(0.3f, 0.85f, 0.95f, 0.85f);
            _trackLine.endColor = new Color(0.2f, 0.6f, 0.75f, 0.55f);

            _bob1 = transform.Find("Bob1");
            if (_bob1 == null)
            {
                var bobGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bobGo.name = "Bob1";
                bobGo.transform.SetParent(transform, false);
                Object.Destroy(bobGo.GetComponent<Collider>());
                _bob1 = bobGo.transform;
            }
            var b1r = _bob1.GetComponent<Renderer>();
            if (b1r != null)
            {
                if (_bob1Mat == null)
                {
                    _bob1Mat = new Material(sh);
                    var teal = new Color(0.15f, 0.78f, 0.86f, 1f);
                    if (_bob1Mat.HasProperty("_BaseColor"))
                        _bob1Mat.SetColor("_BaseColor", teal);
                    _bob1Mat.color = teal;
                    if (_bob1Mat.HasProperty("_Metallic"))
                        _bob1Mat.SetFloat("_Metallic", 0.35f);
                    if (_bob1Mat.HasProperty("_Smoothness"))
                        _bob1Mat.SetFloat("_Smoothness", 0.6f);
                    if (_bob1Mat.HasProperty("_EmissionColor"))
                    {
                        _bob1Mat.EnableKeyword("_EMISSION");
                        _bob1Mat.SetColor("_EmissionColor", teal * 0.45f);
                    }
                }
                b1r.sharedMaterial = _bob1Mat;
            }

            _bob2 = transform.Find("Bob2");
            if (_bob2 == null)
            {
                var bobGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bobGo.name = "Bob2";
                bobGo.transform.SetParent(transform, false);
                Object.Destroy(bobGo.GetComponent<Collider>());
                _bob2 = bobGo.transform;
            }
            var b2r = _bob2.GetComponent<Renderer>();
            if (b2r != null)
            {
                if (_bob2Mat == null)
                {
                    _bob2Mat = new Material(sh);
                    var amber = new Color(0.92f, 0.62f, 0.18f, 1f);
                    if (_bob2Mat.HasProperty("_BaseColor"))
                        _bob2Mat.SetColor("_BaseColor", amber);
                    _bob2Mat.color = amber;
                    if (_bob2Mat.HasProperty("_Metallic"))
                        _bob2Mat.SetFloat("_Metallic", 0.35f);
                    if (_bob2Mat.HasProperty("_Smoothness"))
                        _bob2Mat.SetFloat("_Smoothness", 0.6f);
                    if (_bob2Mat.HasProperty("_EmissionColor"))
                    {
                        _bob2Mat.EnableKeyword("_EMISSION");
                        _bob2Mat.SetColor("_EmissionColor", amber * 0.45f);
                    }
                }
                b2r.sharedMaterial = _bob2Mat;
            }

            ApplyBobScale();
        }

        void ApplyBobScale()
        {
            float r1 = BobRadius;
            float r2 = BobRadius * Mathf.Pow(Mathf.Max(0.25f, ActiveRatio), 1f / 3f);
            if (_bob1 != null)
                _bob1.localScale = Vector3.one * (r1 * 2f);
            if (_bob2 != null)
                _bob2.localScale = Vector3.one * (r2 * 2f);
        }

        void ApplyBobPose()
        {
            if (_bob1 != null)
                _bob1.localPosition = new Vector3(_x1, TrackY, 0f);
            if (_bob2 != null)
                _bob2.localPosition = new Vector3(_x2, TrackY, 0f);
        }

        void ApplyGlow()
        {
            float glow = _state == RunState.Paused ? 0.35f : (_state == RunState.Approaching ? 0.7f : 1.0f);
            if (_bob1Mat != null && _bob1Mat.HasProperty("_EmissionColor"))
            {
                var teal = new Color(0.15f, 0.78f, 0.86f, 1f);
                _bob1Mat.EnableKeyword("_EMISSION");
                _bob1Mat.SetColor("_EmissionColor", teal * glow);
            }
            if (_bob2Mat != null && _bob2Mat.HasProperty("_EmissionColor"))
            {
                var amber = new Color(0.92f, 0.62f, 0.18f, 1f);
                _bob2Mat.EnableKeyword("_EMISSION");
                _bob2Mat.SetColor("_EmissionColor", amber * glow);
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
            _readout.fontSize = 20f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.65f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(60f, 40f);
            _readout.text = "COLLISION\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string modeStr = _mode == CollisionMode.Elastic ? "ELASTIC" : "INELASTIC";
            string status;
            if (_state == RunState.Paused) status = "PAUSED";
            else if (_state == RunState.Approaching) status = "APPROACH";
            else status = "IMPACT";

            string mStr = "m1 " + Mass1.ToString("0.00") + "  m2 " + Mass2.ToString("0.00") + "kg";
            string vStr = "v_ap " + ActiveV.ToString("0.0") + "m/s";
            string pStr = "p " + _pBefore.ToString("0.000") + "->" + _pAfter.ToString("0.000");
            string keStr = "KE " + _keBefore.ToString("0.000") + "->" + _keAfter.ToString("0.000");
            string ratioStr = "ratio " + ActiveRatioLabel + " (" + ActiveRatio.ToString("0.0") + ")";

            _readout.text =
                "1D " + modeStr + " " + ActiveRatioLabel + "/" + ActiveVLabel + "\n"
                + status + "\n"
                + mStr + "\n"
                + vStr + "\n"
                + pStr + "\n"
                + keStr + "\n"
                + ratioStr + "\n"
                + "N/P mass  Shift v\n"
                + "activate mode/run\n"
                + "[ideal 1D]";
        }
    }
}
