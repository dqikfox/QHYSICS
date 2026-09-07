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
    /// Grabbable CIRCUIT transformer: while near an InductionCoil, applies discrete lumped turns N
    /// (EMF = -N dPhi/dt). Honesty: single-coil turns tap — NOT mutual inductance, not dual winding,
    /// not core hysteresis / saturation.
    /// XR activate / N cycles presets; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(8)]
    public sealed class LoadTransformerGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Transformer";
        public const string Honesty = "Discrete lumped turns N on InductionCoil (EMF=-N dPhi/dt). NOT mutual inductance, not dual-winding transformer, not core hysteresis.";

        // Relative to lab default N=80: 1:4, 1:2, 1:1, 2:1, 4:1
        static readonly int[] Presets = { 20, 40, 80, 160, 320 };
        static readonly string[] PresetLabels = { "1:4", "1:2", "1:1", "2:1", "4:1" };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay connected and drive turns.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCoil _coil;
        InductionCoil[] _coils;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 2; // default 1:1 (N=80)
        bool _applied;
        int _savedTurns = 80;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;
        Renderer _coreRenderer;
        Material _mat;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public int ActiveTurns => Presets[Mathf.Clamp(_presetIndex, 0, Presets.Length - 1)];
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
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
            RestoreApplied();
            UnwireGrab();
        }

        void OnDestroy()
        {
            RestoreApplied();
            UnwireGrab();
            if (_mat != null)
            {
                if (Application.isPlaying) Destroy(_mat);
                else DestroyImmediate(_mat);
                _mat = null;
            }
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.08f;

            if (Time.unscaledTime >= _cacheAt || _coils == null)
                EnsureCoils(force: false);

            BindNearest();
            if (_coil == null)
                EnsureLabCoil();

            ApplyOrRelease();
            PollDesktopCycle();
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
            if ((cam.transform.position - TipWorld).sqrMagnitude > 1.44f)
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
            if (Presets.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % Presets.Length;
            if (_presetIndex < 0)
                _presetIndex += Presets.Length;
            if (_applied && _appliedCoil != null)
                _appliedCoil.SetTurns(ActiveTurns);
            RefreshText();
        }

        public void DesktopActivate(int delta) => CyclePreset(delta);

        void EnsureCoils(bool force)
        {
            _cacheAt = Time.unscaledTime + 1.25f;
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
            for (int i = 0; i < _coils.Length; i++)
            {
                InductionCoil c = _coils[i];
                if (c == null || !c.isActiveAndEnabled)
                    continue;
                if (c.transform == transform || c.transform.IsChildOf(transform))
                    continue;
                float sq = (c.transform.position - tip).sqrMagnitude;
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
            if (_coil != null)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            EnsureCoils(force: true);
            BindNearest();
        }

        void ApplyOrRelease()
        {
            if (_coil == null)
            {
                RestoreApplied();
                return;
            }

            float dist = Vector3.Distance(TipWorld, _coil.transform.position);
            if (dist > connectWithinMeters)
            {
                RestoreApplied();
                return;
            }

            if (!_applied || _appliedCoil != _coil)
            {
                RestoreApplied();
                _savedTurns = _coil.Turns;
                _appliedCoil = _coil;
                _applied = true;
            }

            _appliedCoil.SetTurns(ActiveTurns);
        }

        void RestoreApplied()
        {
            if (!_applied || _appliedCoil == null)
            {
                _applied = false;
                _appliedCoil = null;
                return;
            }
            _appliedCoil.SetTurns(_savedTurns);
            _applied = false;
            _appliedCoil = null;
        }

        void EnsureVisual()
        {
            if (_coreRenderer == null)
            {
                Transform core = transform.Find("Core");
                if (core != null)
                    _coreRenderer = core.GetComponent<Renderer>();
                if (_coreRenderer == null)
                    _coreRenderer = GetComponent<Renderer>();
            }

            if (_coreRenderer == null || transform.Find("Core") == null)
            {
                var coreGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coreGo.name = "Core";
                coreGo.transform.SetParent(transform, false);
                coreGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                coreGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                coreGo.transform.localScale = new Vector3(0.045f, 0.035f, 0.045f);
                Object.Destroy(coreGo.GetComponent<Collider>());
                _coreRenderer = coreGo.GetComponent<Renderer>();
            }

            if (_mat == null && _coreRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                // Iron / ferrite-ish
                Color baseC = new Color(0.42f, 0.38f, 0.34f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.35f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.4f);
                _coreRenderer.sharedMaterial = _mat;

                var rootR = GetComponent<Renderer>();
                if (rootR != null && rootR != _coreRenderer)
                    rootR.sharedMaterial = _mat;
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
                go.transform.localPosition = new Vector3(0f, 0.07f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.85f, 0.82f, 0.7f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "XFMR\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string ratio = ActiveLabel;
            string nLine = "N=" + ActiveTurns;
            if (_coil == null)
            {
                _readout.text = "XFMR " + ratio + "\n" + nLine + "\nno InductionCoil\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _coil.transform.position);
            string target = _coil.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _applied ? "ON coil" : (dist > connectWithinMeters ? "far" : "near");
            float emf = _coil.Emf;
            _readout.text =
                "XFMR " + ratio + " " + nLine + "\n"
                + "EMF " + emf.ToString("+0.00;-0.00;0.00") + "V\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger cycle\n"
                + "[lumped turns tap]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.5f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
