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
    /// Grabbable CIRCUIT function generator: while near an InductionCoil, drives classical ideal
    /// series EMF waveforms (sine / square / triangle) at discrete freq+amp presets.
    /// Honesty: lumped ideal AWG EMF on InductionCoil - NOT a real DDS, not output impedance,
    /// not frequency response of the coil, not triggered sync.
    /// XR activate / N cycles presets; P steps backward. DefaultExecutionOrder(10) so EMF is set
    /// before coil LateUpdate(20). Pair with MEASURE Oscilloscope for EXPERIMENT loop.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class LoadFunctionGeneratorGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_FunctionGenerator";
        public const string Honesty =
            "Classical ideal waveform series EMF on InductionCoil. Not real DDS/AWG, not Zout, not coil frequency response, not sync/trigger.";

        enum Wave { Off, Sine, Square, Triangle }

        static readonly Wave[] PresetWaves =
        {
            Wave.Off,
            Wave.Sine, Wave.Sine, Wave.Sine,
            Wave.Square, Wave.Square,
            Wave.Triangle, Wave.Triangle
        };
        static readonly float[] PresetHz =
        {
            0f,
            1f, 5f, 10f,
            5f, 10f,
            5f, 10f
        };
        static readonly float[] PresetVolts =
        {
            0f,
            1.5f, 1.5f, 3f,
            1.5f, 3f,
            1.5f, 3f
        };
        static readonly string[] PresetLabels =
        {
            "OFF",
            "SIN1", "SIN5", "SIN10",
            "SQR5", "SQR10",
            "TRI5", "TRI10"
        };

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
        int _presetIndex = 2; // default SIN5 = 5 Hz @ 1.5 V
        bool _applied;
        float _savedEmf;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;
        Renderer _renderer;
        Material _mat;
        Transform _panel;
        float _phase; // radians
        float _liveEmf;
        Color _offColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        Color _teal = new Color(0.15f, 0.75f, 0.65f, 1f);
        Color _violet = new Color(0.55f, 0.35f, 0.95f, 1f);

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        public float ActiveHz => PresetHz[Mathf.Clamp(_presetIndex, 0, PresetHz.Length - 1)];
        public float ActiveVoltsPeak => PresetVolts[Mathf.Clamp(_presetIndex, 0, PresetVolts.Length - 1)];
        Wave ActiveWave => PresetWaves[Mathf.Clamp(_presetIndex, 0, PresetWaves.Length - 1)];

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
            if (Time.unscaledTime >= _cacheAt || _coils == null)
                EnsureCoils(force: false);

            BindNearest();
            if (_coil == null)
                EnsureLabCoil();

            AdvanceWaveform();
            ApplyOrRelease();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void AdvanceWaveform()
        {
            Wave w = ActiveWave;
            float hz = ActiveHz;
            float peak = ActiveVoltsPeak;
            if (w == Wave.Off || hz <= 1e-4f || Mathf.Abs(peak) < 1e-4f)
            {
                _liveEmf = 0f;
                return;
            }

            float dt = Time.deltaTime;
            if (dt > 0.05f)
                dt = 0.05f;
            _phase += 2f * Mathf.PI * hz * dt;
            if (_phase > 2f * Mathf.PI * 8f)
                _phase -= 2f * Mathf.PI * 8f;

            float unit;
            switch (w)
            {
                case Wave.Square:
                    unit = Mathf.Sin(_phase) >= 0f ? 1f : -1f;
                    break;
                case Wave.Triangle:
                {
                    // triangle from phase:  -1..1 saw mapped to triangle
                    float u = (_phase / (2f * Mathf.PI)) % 1f;
                    if (u < 0f) u += 1f;
                    unit = u < 0.5f ? (u * 4f - 1f) : (3f - u * 4f);
                    break;
                }
                default:
                    unit = Mathf.Sin(_phase);
                    break;
            }
            _liveEmf = peak * unit;
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
            _presetIndex = (_presetIndex + delta + PresetLabels.Length) % PresetLabels.Length;
            _phase = 0f;
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
                    panelGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                    panelGo.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                    panelGo.transform.localScale = new Vector3(0.14f, 0.04f, 0.1f);
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
                Color c = Color.Lerp(_teal, _violet, 0.35f);
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

            Wave w = ActiveWave;
            if (w == Wave.Off)
            {
                // Stay "applied" with restored baseline so leaving OFF does not leave stale drive.
                if (!_applied || _appliedCoil != _coil)
                {
                    RestoreApplied();
                    _savedEmf = _coil.ExternalSeriesEmf;
                    _appliedCoil = _coil;
                    _applied = true;
                }
                _appliedCoil.SetExternalSeriesEmf(_savedEmf);
                return;
            }

            if (!_applied || _appliedCoil != _coil)
            {
                RestoreApplied();
                _savedEmf = _coil.ExternalSeriesEmf;
                _appliedCoil = _coil;
                _applied = true;
            }

            _appliedCoil.SetExternalSeriesEmf(_liveEmf);
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

            Wave w = ActiveWave;
            bool off = w == Wave.Off;
            float t = off ? 0f : Mathf.Clamp01(ActiveVoltsPeak / 3f);
            Color c = off ? _offColor : Color.Lerp(_teal, _violet, t);
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", c);
            _mat.color = c;
            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                // Pulse emission with |live EMF| so player sees drive without scope
                float pulse = off ? 0f : Mathf.Clamp01(Mathf.Abs(_liveEmf) / Mathf.Max(0.25f, ActiveVoltsPeak));
                Color e = off ? Color.black : Color.Lerp(_teal, _violet, t) * (0.15f + 1.2f * pulse);
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
                go.transform.localPosition = new Vector3(0f, 0.07f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 24f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 0.95f, 0.85f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 20f);
            _readout.text = "FGEN\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            string hzLabel = ActiveWave == Wave.Off ? "-" : ActiveHz.ToString("0.#") + "Hz";
            string vLabel = ActiveWave == Wave.Off ? "0Vpk" : ActiveVoltsPeak.ToString("0.#") + "Vpk";
            string live = _liveEmf.ToString("+0.00;-0.00;0.00") + "V";

            if (_coil == null)
            {
                _readout.text = "FGEN " + label + "\n" + hzLabel + " " + vLabel + "\nno InductionCoil\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _coil.transform.position);
            string target = _coil.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link;
            if (dist > connectWithinMeters)
                link = "far";
            else if (_applied && ActiveWave != Wave.Off)
                link = "DRIVE";
            else if (_applied)
                link = "near";
            else
                link = "near";

            _readout.text =
                "FGEN " + label + "\n"
                + hzLabel + " " + vLabel + "\n"
                + "live " + live + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger\n"
                + "[ideal AWG EMF]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.7f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
