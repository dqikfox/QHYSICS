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
    /// Grabbable CIRCUIT LED: while near an InductionCoil, applies ideal series diode polarity
    /// and glows from forward |I| in a selectable color.
    /// Honesty: ideal LED = diode + emission proxy — NOT bandgap photons, not real I-V, not heat.
    /// XR activate / N cycles color; P steps backward. Desktop LMB/scroll via IDesktopActivatable.
    /// DefaultExecutionOrder(16) so polarity is set before circuit LateUpdate(30), after Diode(15).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(16)]
    public sealed class LoadLedGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_LED";
        public const string Honesty = "Ideal LED = series diode + |I| emission proxy. NOT bandgap photons, not real LED I-V, not thermal.";

        // Presets: RED / GREEN / BLUE / SHORT(bypass)
        static readonly int[] Polarities = { 1, 1, 1, 0 };
        static readonly string[] PresetLabels = { "RED", "GREEN", "BLUE", "SHORT" };
        static readonly Color[] GlowColors =
        {
            new Color(1f, 0.18f, 0.12f, 1f),
            new Color(0.15f, 0.95f, 0.28f, 1f),
            new Color(0.2f, 0.45f, 1f, 1f),
            new Color(0.45f, 0.45f, 0.5f, 1f),
        };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay connected and drive series diode + glow.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Emission intensity at ~50mA forward.")]
        float maxEmission = 5.5f;

        TextMeshPro _readout;
        InductionCoil _coil;
        InductionCoil[] _coils;
        InductionCircuit _circuit;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex; // default RED
        bool _applied;
        int _savedPolarity;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;
        Renderer _domeRenderer;
        Material _mat;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public int ActivePolarity => Polarities[Mathf.Clamp(_presetIndex, 0, Polarities.Length - 1)];
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        public Color ActiveGlow => GlowColors[Mathf.Clamp(_presetIndex, 0, GlowColors.Length - 1)];

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
            _refreshAt = Time.unscaledTime + 0.05f;

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
            _presetIndex = (_presetIndex + delta + Polarities.Length) % Polarities.Length;
            ApplyOrRelease();
            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            ApplyVisual(i);
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsureVisual()
        {
            if (_domeRenderer == null)
            {
                Transform dome = transform.Find("Dome");
                if (dome != null)
                    _domeRenderer = dome.GetComponent<Renderer>();
                if (_domeRenderer == null)
                    _domeRenderer = GetComponent<Renderer>();
            }

            if (_domeRenderer == null)
            {
                var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dome.name = "Dome";
                dome.transform.SetParent(transform, false);
                dome.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                dome.transform.localScale = Vector3.one * 0.055f;
                Object.Destroy(dome.GetComponent<Collider>());
                _domeRenderer = dome.GetComponent<Renderer>();
            }

            if (_mat == null && _domeRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                Color baseC = ActiveGlow * 0.35f;
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", Color.black);
                }
                _domeRenderer.sharedMaterial = _mat;
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

            int polarity = ActivePolarity;
            if (!_applied || _appliedCoil != _coil)
            {
                RestoreApplied();
                _savedPolarity = _coil.SeriesDiodePolarity;
                _appliedCoil = _coil;
                _applied = true;
            }

            _appliedCoil.SetSeriesDiodePolarity(polarity);
        }

        void RestoreApplied()
        {
            if (!_applied || _appliedCoil == null)
            {
                _applied = false;
                _appliedCoil = null;
                return;
            }
            _appliedCoil.SetSeriesDiodePolarity(_savedPolarity);
            _applied = false;
            _appliedCoil = null;
        }

        void ApplyVisual(float amps)
        {
            EnsureVisual();
            if (_mat == null)
                return;

            bool shorted = ActivePolarity == 0;
            Color glow = ActiveGlow;
            // Forward glow from |I|; reverse / short stay dim shell color.
            float forward = shorted ? 0f : Mathf.Clamp01(Mathf.Abs(amps) / 0.05f);
            if (!shorted && ActivePolarity != 0 && amps * ActivePolarity < 0f)
                forward = 0f; // reverse bias: dark

            Color baseLit = Color.Lerp(glow * 0.2f, glow, 0.25f + 0.75f * forward);
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", baseLit);
            _mat.color = baseLit;

            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                Color e = shorted ? Color.black : glow * (forward * maxEmission);
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
                go.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.9f, 0.95f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "LED\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string dLabel = ActiveLabel;
            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            string iLabel = i.ToString("+0.000;-0.000;0.000") + "A";

            if (_coil == null)
            {
                _readout.text = "LED " + dLabel + "\nno InductionCoil\nEnter Sandbox / Induction";
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
                "LED " + dLabel + "\n"
                + "I " + iLabel + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger cycle\n"
                + "[ideal diode+glow]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
