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
    /// Grabbable PHYSICS mutual coupler: while near TWO InductionCoils, injects lumped
    /// Es = -M dIp/dt into the secondary from the primary's InductionCircuit current.
    /// Honesty: lumped mutual M only — NOT geometric flux linkage, not core hysteresis,
    /// not leakage inductance, not a dual-winding transformer model.
    /// XR activate / N cycles M presets; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// DefaultExecutionOrder(5) applies Es before coil LateUpdate(20); samples Ip one frame lagged.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(5)]
    public sealed class LoadMutualCouplerGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_MutualCoupler";
        public const string Honesty =
            "Lumped mutual M: Es=-M dIp/dt between two InductionCoils. NOT geometric flux linkage, not core hysteresis, not leakage, not dual-winding transformer.";

        // Mutual inductance presets (henries)
        static readonly float[] PresetsM = { 0f, 0.0005f, 0.002f, 0.01f };
        static readonly string[] PresetLabels = { "OFF", "WEAK", "MED", "STRONG" };

        const float ConnectWithinMeters = 1.15f;
        const float CacheSeconds = 1.25f;

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.05f, 0.06f);

        [SerializeField, Tooltip("Max distance (m) from tip to each coil to stay coupled.")]
        float connectWithinMeters = ConnectWithinMeters;

        TextMeshPro _readout;
        InductionCoil _primary;
        InductionCoil _secondary;
        InductionCircuit _primaryCircuit;
        InductionCircuit _secondaryCircuit;
        InductionCoil[] _coils;
        XRGrabInteractable _grab;
        Material _coreMat;
        Material _priMat;
        Material _secMat;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 2; // default MED
        bool _applied;
        float _savedSecondaryEmf;
        InductionCoil _appliedSecondary;
        float _prevIp;
        bool _hasPrevIp;
        float _dIpDt;
        float _mutualEmf;
        float _peakAbsMutualEmf;
        float _liveIp;
        bool _labEnsured;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveM => PresetsM[Mathf.Clamp(_presetIndex, 0, PresetsM.Length - 1)];
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        public float MutualEmfVolts => _mutualEmf;
        public float PrimaryCurrentAmperes => _liveIp;
        public bool IsCoupled => _applied && ActiveM > 1e-12f;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCoils(force: true);
            BindPair();
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
            RestoreApplied();
            UnwireGrab();
        }

        void OnDestroy()
        {
            RestoreApplied();
            UnwireGrab();
            DestroyMat(ref _coreMat);
            DestroyMat(ref _priMat);
            DestroyMat(ref _secMat);
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
            if (Time.unscaledTime >= _cacheAt || _coils == null)
                EnsureCoils(force: false);

            BindPair();
            if (_primary == null || _secondary == null)
                EnsureLabCoil();

            ResolveCircuits();
            // Apply Es from prior-frame dIp/dt before coil LateUpdate(20) this frame.
            ApplyOrRelease();
            PollDesktopCycle();
            SamplePrimaryCurrent();

            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.06f;
            RefreshText();
            ApplyGlow();
        }

        void SamplePrimaryCurrent()
        {
            // CurrentAmperes is from the previous frame's InductionCircuit LateUpdate(30).
            float ip = 0f;
            if (_primaryCircuit != null)
                ip = _primaryCircuit.CurrentAmperes;
            _liveIp = ip;

            float dt = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
            if (_hasPrevIp && dt > 1e-6f)
                _dIpDt = (ip - _prevIp) / dt;
            else
                _dIpDt = 0f;
            _prevIp = ip;
            _hasPrevIp = true;

            float m = ActiveM;
            if (m > 1e-12f && _applied)
                _mutualEmf = -m * _dIpDt;
            else
                _mutualEmf = 0f;

            float abs = Mathf.Abs(_mutualEmf);
            if (abs > _peakAbsMutualEmf)
                _peakAbsMutualEmf = abs;
            else
                _peakAbsMutualEmf = Mathf.Lerp(_peakAbsMutualEmf, abs, 0.35f);
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
            CyclePreset(+1);
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
            if (PresetsM.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % PresetsM.Length;
            if (_presetIndex < 0)
                _presetIndex += PresetsM.Length;
            ApplyOrRelease();
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

        void BindPair()
        {
            _primary = null;
            _secondary = null;
            if (_coils == null || _coils.Length == 0)
                return;

            Vector3 tip = TipWorld;
            float maxSq = connectWithinMeters * connectWithinMeters;
            InductionCoil best = null;
            InductionCoil second = null;
            float bestSq = float.PositiveInfinity;
            float secondSq = float.PositiveInfinity;

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
                    second = best;
                    secondSq = bestSq;
                    best = c;
                    bestSq = sq;
                }
                else if (sq < secondSq)
                {
                    second = c;
                    secondSq = sq;
                }
            }

            _primary = best;
            _secondary = second;
        }

        void ResolveCircuits()
        {
            _primaryCircuit = null;
            _secondaryCircuit = null;
            if (_primary != null)
            {
                _primaryCircuit = _primary.GetComponent<InductionCircuit>();
                if (_primaryCircuit == null)
                    _primaryCircuit = _primary.GetComponentInChildren<InductionCircuit>();
                if (_primaryCircuit == null)
                    _primaryCircuit = _primary.GetComponentInParent<InductionCircuit>();
            }
            if (_secondary != null)
            {
                _secondaryCircuit = _secondary.GetComponent<InductionCircuit>();
                if (_secondaryCircuit == null)
                    _secondaryCircuit = _secondary.GetComponentInChildren<InductionCircuit>();
                if (_secondaryCircuit == null)
                    _secondaryCircuit = _secondary.GetComponentInParent<InductionCircuit>();
            }
        }

        void EnsureLabCoil()
        {
            if (_primary != null && _secondary != null)
                return;
            if (!_labEnsured)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var lab = InductionLabBootstrap.EnsureLabInScene(scene);
                if (lab != null)
                    lab.BuildLab();
                _labEnsured = true;
                EnsureCoils(force: true);
                BindPair();
            }
            if (_primary != null && _secondary == null)
                EnsureBuddySecondary();
            ResolveCircuits();
        }

        void EnsureBuddySecondary()
        {
            Transform existing = transform.Find("BuddySecondary");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("BuddySecondary");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.28f, 0.04f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            var coil = go.GetComponent<InductionCoil>();
            if (coil == null)
                coil = go.AddComponent<InductionCoil>();
            coil.Configure(80, 0.08f, 2f, 8f);

            var circuit = go.GetComponent<InductionCircuit>();
            if (circuit == null)
                circuit = go.AddComponent<InductionCircuit>();
            circuit.SetCoil(coil);

            _secondary = coil;
            EnsureCoils(force: true);
        }

        void ApplyOrRelease()
        {
            float m = ActiveM;
            if (_primary == null || _secondary == null || m < 1e-12f)
            {
                RestoreApplied();
                return;
            }

            // One-frame delayed Es from LateUpdate dIp/dt (applied before coil LateUpdate next frame via Update).
            float es = -m * _dIpDt;
            _mutualEmf = es;

            if (!_applied || _appliedSecondary != _secondary)
            {
                RestoreApplied();
                _savedSecondaryEmf = _secondary.ExternalSeriesEmf;
                _appliedSecondary = _secondary;
                _applied = true;
            }

            _appliedSecondary.SetExternalSeriesEmf(es);
        }

        void RestoreApplied()
        {
            if (!_applied || _appliedSecondary == null)
            {
                _applied = false;
                _appliedSecondary = null;
                return;
            }
            _appliedSecondary.SetExternalSeriesEmf(_savedSecondaryEmf);
            _applied = false;
            _appliedSecondary = null;
        }

        void EnsureVisual()
        {
            var rootR = GetComponent<Renderer>();
            if (rootR != null && _coreMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _coreMat = new Material(sh);
                var iron = new Color(0.38f, 0.36f, 0.34f, 1f);
                if (_coreMat.HasProperty("_BaseColor"))
                    _coreMat.SetColor("_BaseColor", iron);
                _coreMat.color = iron;
                if (_coreMat.HasProperty("_Metallic"))
                    _coreMat.SetFloat("_Metallic", 0.45f);
                if (_coreMat.HasProperty("_Smoothness"))
                    _coreMat.SetFloat("_Smoothness", 0.4f);
                rootR.sharedMaterial = _coreMat;
            }

            Transform core = transform.Find("Core");
            if (core == null)
            {
                var coreGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                coreGo.name = "Core";
                coreGo.transform.SetParent(transform, false);
                coreGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                coreGo.transform.localRotation = Quaternion.identity;
                coreGo.transform.localScale = new Vector3(0.55f, 0.06f, 0.1f);
                Object.Destroy(coreGo.GetComponent<Collider>());
                core = coreGo.transform;
            }
            var cr = core.GetComponent<Renderer>();
            if (cr != null)
            {
                if (_coreMat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _coreMat = new Material(sh);
                    var iron = new Color(0.38f, 0.36f, 0.34f, 1f);
                    if (_coreMat.HasProperty("_BaseColor"))
                        _coreMat.SetColor("_BaseColor", iron);
                    _coreMat.color = iron;
                }
                cr.sharedMaterial = _coreMat;
            }

            EnsureCoilViz("PrimaryCoil", new Vector3(-0.22f, 0.04f, 0f), ref _priMat, new Color(0.2f, 0.55f, 0.85f, 1f));
            EnsureCoilViz("SecondaryCoil", new Vector3(0.22f, 0.04f, 0f), ref _secMat, new Color(0.85f, 0.55f, 0.2f, 1f));
        }

        void EnsureCoilViz(string name, Vector3 localPos, ref Material mat, Color color)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                go.transform.localScale = new Vector3(0.1f, 0.04f, 0.1f);
                Object.Destroy(go.GetComponent<Collider>());
                t = go.transform;
            }
            var r = t.GetComponent<Renderer>();
            if (r == null)
                return;
            if (mat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                mat.color = color;
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", 0.55f);
            }
            r.sharedMaterial = mat;
        }

        void ApplyGlow()
        {
            float t = Mathf.Clamp01(_peakAbsMutualEmf / 2f);
            if (_secMat != null && _secMat.HasProperty("_EmissionColor"))
            {
                _secMat.EnableKeyword("_EMISSION");
                _secMat.SetColor("_EmissionColor", new Color(0.85f, 0.55f, 0.2f, 1f) * (0.1f + 1.8f * t));
            }
            if (_priMat != null && _priMat.HasProperty("_EmissionColor"))
            {
                _priMat.EnableKeyword("_EMISSION");
                float pt = Mathf.Clamp01(Mathf.Abs(_liveIp) / 0.2f);
                _priMat.SetColor("_EmissionColor", new Color(0.2f, 0.55f, 0.85f, 1f) * (0.05f + 1.2f * pt));
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
                go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 22f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.7f, 0.85f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(38f, 20f);
            _readout.text = "MUTUAL\nseeking 2 coils...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            string mLabel = ActiveM < 1e-12f ? "M=0" : ("M=" + (ActiveM * 1000f).ToString("0.##") + "mH");

            if (_primary == null || _secondary == null)
            {
                _readout.text =
                    "MUTUAL " + label + "\n"
                    + mLabel + "\n"
                    + "need 2 coils in range\n"
                    + "Enter Sandbox / Induction\n"
                    + "[lumped M]";
                return;
            }

            string link = _applied ? "ON" : "near";
            string ip = _liveIp.ToString("+0.000;-0.000;0.000") + "A";
            string es = _mutualEmf.ToString("+0.00;-0.00;0.00") + "V";
            string pk = _peakAbsMutualEmf.ToString("0.00");

            string priName = _primary.name;
            if (priName.Length > 12) priName = priName.Substring(0, 10) + "..";
            string secName = _secondary.name;
            if (secName.Length > 12) secName = secName.Substring(0, 10) + "..";

            string circuitHint = _primaryCircuit == null ? " (no Ickt)" : "";

            _readout.text =
                "MUTUAL " + label + " " + mLabel + "\n"
                + "Ip " + ip + circuitHint + "\n"
                + "Es " + es + " pk|" + pk + "|\n"
                + link + " P:" + priName + " S:" + secName + "\n"
                + "N/P or VR trigger cycle\n"
                + "[lumped M]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawWireSphere(TipWorld, connectWithinMeters);
            if (_primary != null)
            {
                Gizmos.color = new Color(0.2f, 0.55f, 0.85f, 0.9f);
                Gizmos.DrawLine(TipWorld, _primary.transform.position);
            }
            if (_secondary != null)
            {
                Gizmos.color = new Color(0.85f, 0.55f, 0.2f, 0.9f);
                Gizmos.DrawLine(TipWorld, _secondary.transform.position);
            }
        }
#endif
    }
}
