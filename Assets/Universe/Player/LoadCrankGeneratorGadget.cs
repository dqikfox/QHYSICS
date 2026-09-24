using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS hand-crank generator: rotating MagneticDipole armature sweeps moment so a
    /// nearby InductionCoil sees changing flux (Faraday). Honesty: rotating dipole near coil -
    /// NOT a commutated dynamo / brushes / commercial alternator.
    /// XR activate (hold) cranks; release coasts with damping. N/P or desktop LMB/scroll cycle RPM.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    public sealed class LoadCrankGeneratorGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_CrankGenerator";
        public const string Honesty =
            "Rotating MagneticDipole near InductionCoil (Faraday). NOT a commutated dynamo / brushes / commercial alternator.";

        static readonly float[] RpmPresets = { 30f, 90f, 180f, 360f };
        static readonly string[] PresetLabels = { "SLOW", "MED", "FAST", "TURBO" };

        const float BindWithinMeters = 1.1f;
        const float CacheSeconds = 1.25f;
        const float CoastDamping = 2.8f;
        const float PeakEmfDecay = 0.35f;

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.05f, 0.08f);

        [SerializeField, Tooltip("Max distance (m) to report a nearby coil on the readout.")]
        float bindWithinMeters = BindWithinMeters;

        [SerializeField, Tooltip("Dipole moment magnitude (A*m^2) on the rotating armature.")]
        float magnetMoment = 2.5f;

        TextMeshPro _readout;
        Transform _armature;
        Transform _crankArm;
        MagneticDipole _dipole;
        InductionCoil _coil;
        InductionCoil[] _coils;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _armMat;
        Material _magnetMat;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        float _angleDeg;
        float _currentRpm;
        float _peakAbsEmf;
        int _presetIndex = 1;
        bool _wantCrank;
        bool _xrHeld;
        bool _labEnsured;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float Rpm => _currentRpm;
        public bool IsCranking => _wantCrank && _currentRpm > 1f;
        public float PeakAbsEmfNearby => _peakAbsEmf;
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        public float ActiveTargetRpm => RpmPresets[Mathf.Clamp(_presetIndex, 0, RpmPresets.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
            EnsureDipole();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCoils(force: true);
            BindNearest();
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
            _wantCrank = false;
            _xrHeld = false;
            UnwireGrab();
        }

        void OnDestroy()
        {
            UnwireGrab();
            DestroyMat(ref _baseMat);
            DestroyMat(ref _armMat);
            DestroyMat(ref _magnetMat);
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
            PollDesktopHold();
            IntegrateRotation();

            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.06f;

            if (Time.unscaledTime >= _cacheAt || _coils == null)
                EnsureCoils(force: false);

            BindNearest();
            if (_coil == null)
                EnsureLabCoil();

            SamplePeakEmf();
            PollDesktopCycle();
            RefreshText();
        }

        void IntegrateRotation()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            float target = _wantCrank ? ActiveTargetRpm : 0f;
            if (_wantCrank)
                _currentRpm = Mathf.MoveTowards(_currentRpm, target, Mathf.Max(target, 60f) * 2.5f * dt);
            else
                _currentRpm = Mathf.Exp(-CoastDamping * dt) * _currentRpm;

            if (_currentRpm < 0.05f)
                _currentRpm = 0f;

            if (_currentRpm > 0f && _armature != null)
            {
                float degPerSec = _currentRpm * 6f;
                _angleDeg = Mathf.Repeat(_angleDeg + degPerSec * dt, 360f);
                _armature.localRotation = Quaternion.Euler(0f, _angleDeg, 0f);
                if (_crankArm != null)
                    _crankArm.localRotation = Quaternion.Euler(0f, 0f, _angleDeg);
            }
        }

        void WireGrab()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.deactivated.RemoveListener(OnDeactivated);
            _grab.activated.AddListener(OnActivated);
            _grab.deactivated.AddListener(OnDeactivated);
        }

        void UnwireGrab()
        {
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.deactivated.RemoveListener(OnDeactivated);
        }

        void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
        {
            _xrHeld = true;
            _wantCrank = true;
            CyclePreset(+1);
        }

        void OnDeactivated(UnityEngine.XR.Interaction.Toolkit.DeactivateEventArgs _)
        {
            _xrHeld = false;
            _wantCrank = false;
        }

        void PollDesktopHold()
        {
            if (_xrHeld)
            {
                _wantCrank = true;
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            bool near = (cam.transform.position - TipWorld).sqrMagnitude <= 2.25f;
            if (near && mouse.leftButton.isPressed)
                _wantCrank = true;
            else if (!_xrHeld)
                _wantCrank = false;
#endif
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
            if ((cam.transform.position - TipWorld).sqrMagnitude > 2.25f)
                return;
            if (kb.nKey.wasPressedThisFrame)
            {
                CyclePreset(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                CyclePreset(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CyclePreset(int delta)
        {
            if (RpmPresets.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % RpmPresets.Length;
            if (_presetIndex < 0)
                _presetIndex += RpmPresets.Length;
            RefreshText();
        }

        public void DesktopActivate(int delta) => CyclePreset(delta);

        void EnsureCoils(bool force)
        {
            _cacheAt = Time.unscaledTime + CacheSeconds;
            if (!force && _coils != null && _coils.Length > 0)
            {
                for (int i = 0; i < _coils.Length; i++)
                {
                    if (_coils[i] != null)
                        return;
                }
            }
            _coils = Object.FindObjectsByType<InductionCoil>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        void BindNearest()
        {
            if (_coils == null || _coils.Length == 0)
            {
                _coil = null;
                return;
            }

            Vector3 tip = TipWorld;
            InductionCoil best = null;
            float bestSq = float.PositiveInfinity;
            float maxSq = bindWithinMeters * bindWithinMeters;
            for (int i = 0; i < _coils.Length; i++)
            {
                InductionCoil c = _coils[i];
                if (c == null || !c.isActiveAndEnabled)
                    continue;
                if (c.transform == transform || c.transform.IsChildOf(transform))
                    continue;
                float sq = (c.transform.position - tip).sqrMagnitude;
                if (sq > maxSq)
                    continue;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = c;
                }
            }
            _coil = best;
        }

        void EnsureLabCoil()
        {
            if (_coil != null || _labEnsured)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            _labEnsured = true;
            EnsureCoils(force: true);
            BindNearest();
        }

        void SamplePeakEmf()
        {
            if (_coil != null)
            {
                float abs = Mathf.Abs(_coil.Emf);
                if (abs > _peakAbsEmf)
                    _peakAbsEmf = abs;
                else
                    _peakAbsEmf = Mathf.Lerp(_peakAbsEmf, abs, PeakEmfDecay);
            }
            else
                _peakAbsEmf = Mathf.Lerp(_peakAbsEmf, 0f, PeakEmfDecay);
        }

        void EnsureVisual()
        {
            var rootR = GetComponent<Renderer>();
            if (rootR != null && _baseMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _baseMat = new Material(sh);
                var dark = new Color(0.08f, 0.12f, 0.14f, 1f);
                if (_baseMat.HasProperty("_BaseColor"))
                    _baseMat.SetColor("_BaseColor", dark);
                _baseMat.color = dark;
                if (_baseMat.HasProperty("_Smoothness"))
                    _baseMat.SetFloat("_Smoothness", 0.55f);
                rootR.sharedMaterial = _baseMat;
            }

            Transform baseT = transform.Find("Base");
            if (baseT == null)
            {
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseGo.name = "Base";
                baseGo.transform.SetParent(transform, false);
                baseGo.transform.localPosition = new Vector3(0f, -0.01f, 0f);
                baseGo.transform.localRotation = Quaternion.identity;
                baseGo.transform.localScale = new Vector3(0.85f, 0.12f, 0.85f);
                Object.Destroy(baseGo.GetComponent<Collider>());
                baseT = baseGo.transform;
            }
            var br = baseT.GetComponent<Renderer>();
            if (br != null)
            {
                if (_baseMat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _baseMat = new Material(sh);
                    var dark = new Color(0.08f, 0.12f, 0.14f, 1f);
                    if (_baseMat.HasProperty("_BaseColor"))
                        _baseMat.SetColor("_BaseColor", dark);
                    _baseMat.color = dark;
                }
                br.sharedMaterial = _baseMat;
            }

            _armature = transform.Find("Armature");
            if (_armature == null)
            {
                var armGo = new GameObject("Armature");
                armGo.transform.SetParent(transform, false);
                armGo.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                armGo.transform.localRotation = Quaternion.identity;
                armGo.transform.localScale = Vector3.one;
                _armature = armGo.transform;
            }

            _crankArm = transform.Find("CrankArm");
            if (_crankArm == null)
            {
                var crankGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crankGo.name = "CrankArm";
                crankGo.transform.SetParent(transform, false);
                crankGo.transform.localPosition = new Vector3(0.28f, 0.05f, 0f);
                crankGo.transform.localRotation = Quaternion.identity;
                crankGo.transform.localScale = new Vector3(0.55f, 0.06f, 0.08f);
                Object.Destroy(crankGo.GetComponent<Collider>());
                _crankArm = crankGo.transform;
            }
            var ar = _crankArm.GetComponent<Renderer>();
            if (ar != null && _armMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _armMat = new Material(sh);
                var cyan = new Color(0.15f, 0.7f, 0.75f, 1f);
                if (_armMat.HasProperty("_BaseColor"))
                    _armMat.SetColor("_BaseColor", cyan);
                _armMat.color = cyan;
                ar.sharedMaterial = _armMat;
            }

            Transform magnetBar = _armature.Find("MagnetBar");
            if (magnetBar == null)
            {
                var magGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                magGo.name = "MagnetBar";
                magGo.transform.SetParent(_armature, false);
                magGo.transform.localPosition = Vector3.zero;
                magGo.transform.localRotation = Quaternion.identity;
                magGo.transform.localScale = new Vector3(0.08f, 0.08f, 0.55f);
                Object.Destroy(magGo.GetComponent<Collider>());
                magnetBar = magGo.transform;
            }
            var mr = magnetBar.GetComponent<Renderer>();
            if (mr != null && _magnetMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _magnetMat = new Material(sh);
                var redish = new Color(0.55f, 0.18f, 0.16f, 1f);
                if (_magnetMat.HasProperty("_BaseColor"))
                    _magnetMat.SetColor("_BaseColor", redish);
                _magnetMat.color = redish;
                if (_magnetMat.HasProperty("_Metallic"))
                    _magnetMat.SetFloat("_Metallic", 0.4f);
                mr.sharedMaterial = _magnetMat;
            }
        }

        void EnsureDipole()
        {
            if (_armature == null)
                EnsureVisual();
            if (_armature == null)
                return;

            _dipole = _armature.GetComponentInChildren<MagneticDipole>(true);
            if (_dipole == null)
            {
                var dipGo = new GameObject("MagneticDipole");
                dipGo.transform.SetParent(_armature, false);
                dipGo.transform.localPosition = Vector3.zero;
                dipGo.transform.localRotation = Quaternion.identity;
                _dipole = dipGo.AddComponent<MagneticDipole>();
            }
            _dipole.magneticMoment = Mathf.Max(0.1f, magnetMoment);
            _dipole.magnetLength = 0.12f;
            _dipole.magnetRadius = 0.02f;
            _dipole.localAxis = Vector3.forward;
            _dipole.isActive = true;
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
                go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 22f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 0.85f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(36f, 18f);
            _readout.text = "CRANK\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            string rpm = Mathf.RoundToInt(_currentRpm).ToString();
            string coilTag = "-";
            float emfShow = _peakAbsEmf;
            if (_coil != null)
            {
                coilTag = "near";
                emfShow = Mathf.Max(_peakAbsEmf, Mathf.Abs(_coil.Emf));
            }

            string state = _wantCrank ? "CRANK" : (_currentRpm > 1f ? "COAST" : "IDLE");
            _readout.text =
                "CRANK " + label + "  " + rpm + "rpm\n"
                + "coil=" + coilTag + "  |EMF|~" + emfShow.ToString("0.00") + "\n"
                + state + "  hold act/LMB\n"
                + "[Faraday dipole]";
        }
    }
}