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
    /// Grabbable CIRCUIT inductor: while near an InductionCoil, applies classical series L.
    /// Honesty: lumped RL/RLC (L dI/dt = EMF - R I - Vc) — not core saturation, not skin effect, not mutual M.
    /// XR activate / N cycles presets; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// DefaultExecutionOrder(15) so L is set before circuit LateUpdate(30).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(15)]
    public sealed class LoadInductorGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Inductor";
        public const string Honesty = "Classical lumped series L on InductionCoil. RL/RLC only — not core sat, not skin, not mutual M.";

        // Presets: SHORT(bypass) / 1mH / 10mH / 100mH
        static readonly float[] Presets = { 0f, 0.001f, 0.01f, 0.1f };
        static readonly string[] PresetLabels = { "SHORT", "1mH", "10mH", "100mH" };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.05f);

        [SerializeField, Tooltip("Max distance (m) to stay connected and drive series L.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCoil _coil;
        InductionCoil[] _coils;
        InductionCircuit _circuit;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 2; // default 10mH
        bool _applied;
        float _savedL;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;
        Renderer _renderer;
        Material _mat;
        Color _idleColor = new Color(0.25f, 0.45f, 0.7f, 1f);
        Color _hotColor = new Color(0.35f, 0.95f, 1f, 1f);

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveHenries => Presets[Mathf.Clamp(_presetIndex, 0, Presets.Length - 1)];
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureBody();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
            WireGrab();
            EnsureCoils(force: true);
            BindNearest();
            ApplyVisual(0f);
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

            ResolveCircuit();
            ApplyOrRelease();
            PollDesktopCycle();

            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            ApplyVisual(i);
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
            ApplyOrRelease();
            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            ApplyVisual(i);
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsureBody()
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
            if (_renderer == null)
                return;
            if (_mat == null)
            {
                _mat = new Material(_renderer.sharedMaterial != null ? _renderer.sharedMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit")));
                _renderer.material = _mat;
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

        void ResolveCircuit()
        {
            _circuit = null;
            if (_coil == null)
                return;
            _circuit = _coil.GetComponent<InductionCircuit>();
            if (_circuit == null)
                _circuit = _coil.GetComponentInChildren<InductionCircuit>();
            if (_circuit == null)
                _circuit = _coil.GetComponentInParent<InductionCircuit>();
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
            ResolveCircuit();
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

            float henries = ActiveHenries;
            if (!_applied || _appliedCoil != _coil)
            {
                RestoreApplied();
                _savedL = _coil.SeriesInductance;
                _appliedCoil = _coil;
                _applied = true;
            }

            _appliedCoil.SetSeriesInductance(henries);
        }

        void RestoreApplied()
        {
            if (!_applied || _appliedCoil == null)
            {
                _applied = false;
                _appliedCoil = null;
                return;
            }
            _appliedCoil.SetSeriesInductance(_savedL);
            _applied = false;
            _appliedCoil = null;
        }

        void ApplyVisual(float amps)
        {
            EnsureBody();
            if (_mat == null)
                return;

            bool shorted = ActiveHenries < 1e-9f;
            float t = shorted ? 0f : Mathf.Clamp01(Mathf.Abs(amps) / 0.05f);
            Color c = shorted ? _idleColor : Color.Lerp(_idleColor, _hotColor, t);
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", c);
            _mat.color = c;
            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                Color e = shorted ? Color.black : _hotColor * (0.1f + 1.4f * t);
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
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "IND\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string lLabel = ActiveLabel;
            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            string iLabel = i.ToString("+0.000;-0.000;0.000") + "A";

            if (_coil == null)
            {
                _readout.text = "IND " + lLabel + "\nno InductionCoil\nEnter Sandbox / Induction";
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
                "IND " + lLabel + "\n"
                + "I " + iLabel + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger cycle\n"
                + "[lumped RL/RLC]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
