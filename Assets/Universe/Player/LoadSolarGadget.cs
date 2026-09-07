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
    /// Grabbable CIRCUIT solar: while near an InductionCoil, applies classical irradiance-driven series EMF (volts).
    /// Honesty: lumped photocurrent / irradiance EMF proxy — NOT a real PV I-V curve, not quantum, not MPPT.
    /// XR activate / N cycles presets; P steps backward. DefaultExecutionOrder(10) so EMF is set before coil LateUpdate(20).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class LoadSolarGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Solar";
        public const string Honesty = "Classical lumped photocurrent/irradiance EMF proxy on InductionCoil. Not PV I-V, not quantum, not MPPT.";

        // Irradiance presets: OFF / Dawn / Noon / Bright (volts)
        static readonly float[] Presets = { 0f, 1.5f, 3f, 6f };
        static readonly string[] PresetLabels = { "OFF", "Dawn", "Noon", "Bright" };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.05f);

        [SerializeField, Tooltip("Max distance (m) to stay connected and drive series EMF.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCoil _coil;
        InductionCoil[] _coils;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 2; // default Noon = 3 V
        bool _applied;
        float _savedEmf;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;
        Renderer _renderer;
        Material _mat;
        Transform _panel;
        Color _offColor = new Color(0.25f, 0.3f, 0.35f, 1f);
        Color _cyan = new Color(0.25f, 0.75f, 0.9f, 1f);
        Color _amber = new Color(1f, 0.72f, 0.2f, 1f);

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveVolts => Presets[Mathf.Clamp(_presetIndex, 0, Presets.Length - 1)];
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];

        public void EnsureBuilt()
        {
            EnsurePanel();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
            WireGrab();
            EnsureCoils(force: true);
            BindNearest();
            ApplyVisual();
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
            Cycle(1);
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
                Cycle(1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                Cycle(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void Cycle(int delta)
        {
            _presetIndex = (_presetIndex + delta + Presets.Length) % Presets.Length;
            ApplyVisual();
            ApplyOrRelease();
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsurePanel()
        {
            if (_panel == null)
            {
                Transform existing = transform.Find("Panel");
                if (existing != null)
                    _panel = existing;
                else
                {
                    var panelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    panelGo.name = "Panel";
                    panelGo.transform.SetParent(transform, false);
                    panelGo.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                    panelGo.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                    panelGo.transform.localScale = new Vector3(0.18f, 0.008f, 0.12f);
                    Object.Destroy(panelGo.GetComponent<Collider>());
                    _panel = panelGo.transform;
                }
            }

            Renderer panelR = _panel != null ? _panel.GetComponent<Renderer>() : null;
            if (panelR == null && _renderer == null)
                _renderer = GetComponent<Renderer>();
            if (_renderer == null && panelR != null)
                _renderer = panelR;

            if (_mat == null && _renderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                Color c = Color.Lerp(_cyan, _amber, 0.35f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", c);
                _mat.color = c;
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", Color.black);
                }
                _renderer.sharedMaterial = _mat;
            }
        }

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

            float volts = ActiveVolts;
            if (!_applied || _appliedCoil != _coil)
            {
                RestoreApplied();
                _savedEmf = _coil.ExternalSeriesEmf;
                _appliedCoil = _coil;
                _applied = true;
            }

            _appliedCoil.SetExternalSeriesEmf(volts);
        }

        void RestoreApplied()
        {
            if (!_applied || _appliedCoil == null)
            {
                _applied = false;
                _appliedCoil = null;
                return;
            }
            _appliedCoil.SetExternalSeriesEmf(_savedEmf);
            _applied = false;
            _appliedCoil = null;
        }

        void ApplyVisual()
        {
            if (_mat == null)
                EnsurePanel();
            if (_mat == null)
                return;

            float volts = ActiveVolts;
            bool off = Mathf.Abs(volts) < 1e-4f;
            // Map irradiance → cyan→amber blend; emission scales with volts
            float t = off ? 0f : Mathf.Clamp01(volts / 6f);
            Color c = off ? _offColor : Color.Lerp(_cyan, _amber, t);
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", c);
            _mat.color = c;
            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                Color e = off ? Color.black : Color.Lerp(_cyan, _amber, t) * (0.2f + 1.4f * t);
                _mat.SetColor("_EmissionColor", e);
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
                go.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.45f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "SOLAR\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string irr = ActiveLabel;
            string voltsLabel = Mathf.Abs(ActiveVolts) < 1e-4f
                ? "0V"
                : ActiveVolts.ToString("0.#") + "V";

            if (_coil == null)
            {
                _readout.text = "SOLAR " + irr + " " + voltsLabel + "\nno InductionCoil\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _coil.transform.position);
            string target = _coil.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link;
            if (dist > connectWithinMeters)
                link = "far";
            else if (_applied)
                link = "ON";
            else
                link = "near";

            _readout.text =
                "SOLAR " + irr + " " + voltsLabel + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger cycle\n"
                + "[irradiance EMF proxy]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
