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
    /// Grabbable CIRCUIT fuse: while near an InductionCoil, trips open when |I| exceeds the selected trip current.
    /// Honesty: ideal |I| threshold open — NOT I²t, not arc, not thermal fuse model.
    /// XR activate / N: if blown, rearm; else cycle trip. P steps trip backward. Desktop LMB/scroll via IDesktopActivatable.
    /// DefaultExecutionOrder(45) so OPEN overrides Switch/Resistor R after their Updates.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(45)]
    public sealed class LoadFuseGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Fuse";
        public const string Honesty = "Ideal |I| trip open on InductionCoil. NOT I2t, not arc, not thermal model.";

        static readonly float[] TripAmps = { 0.005f, 0.02f, 0.05f, float.PositiveInfinity };
        static readonly string[] TripLabels = { "5mA", "20mA", "50mA", "BYPASS" };

        const float OpenOhms = 1e6f;

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.05f);

        [SerializeField, Tooltip("Max distance (m) to stay connected and drive series open when blown.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCoil _coil;
        InductionCoil[] _coils;
        InductionCircuit _circuit;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 1; // default 20mA
        bool _blown;
        bool _applied;
        float _savedLoad = 8f;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;
        Renderer _renderer;
        Material _mat;
        Color _okColor = new Color(0.75f, 0.55f, 0.2f, 1f);
        Color _blownColor = new Color(0.85f, 0.15f, 0.12f, 1f);
        Color _bypassColor = new Color(0.45f, 0.45f, 0.5f, 1f);

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveTripAmps => TripAmps[Mathf.Clamp(_presetIndex, 0, TripAmps.Length - 1)];
        public string ActiveLabel => TripLabels[Mathf.Clamp(_presetIndex, 0, TripLabels.Length - 1)];
        public bool IsBlown => _blown;
        public bool IsBypass => float.IsPositiveInfinity(ActiveTripAmps);

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
            PollTrip();
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
            ActivateOrCycle(1);
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
                ActivateOrCycle(1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                Cycle(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void ActivateOrCycle(int delta)
        {
            if (_blown)
            {
                ResetFuse();
                return;
            }
            Cycle(delta);
        }

        public void Cycle(int delta)
        {
            _presetIndex = (_presetIndex + delta + TripAmps.Length) % TripAmps.Length;
            if (_blown)
                ResetFuse();
            else
            {
                ApplyOrRelease();
                float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
                ApplyVisual(i);
                RefreshText();
            }
        }

        public void DesktopActivate(int delta) => ActivateOrCycle(delta);

        public void ResetFuse()
        {
            _blown = false;
            ApplyOrRelease();
            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            ApplyVisual(i);
            RefreshText();
        }

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

        void PollTrip()
        {
            if (_blown || IsBypass || !_applied || _circuit == null)
                return;
            float i = Mathf.Abs(_circuit.CurrentAmperes);
            float trip = ActiveTripAmps;
            if (i >= trip)
            {
                _blown = true;
                if (_appliedCoil != null)
                    _appliedCoil.SetLoadResistance(OpenOhms);
            }
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
                _savedLoad = _coil.LoadResistance;
                _appliedCoil = _coil;
                _applied = true;
            }

            if (_blown)
                _appliedCoil.SetLoadResistance(OpenOhms);
            // Intact: leave R alone (Resistor/Switch/Wire own it); we only open when blown.
        }

        void RestoreApplied()
        {
            if (!_applied || _appliedCoil == null)
            {
                _applied = false;
                _appliedCoil = null;
                return;
            }
            _appliedCoil.SetLoadResistance(_savedLoad);
            _applied = false;
            _appliedCoil = null;
        }

        void ApplyVisual(float amps)
        {
            EnsureBody();
            if (_mat == null)
                return;

            Color c;
            if (_blown)
                c = _blownColor;
            else if (IsBypass)
                c = _bypassColor;
            else
            {
                float t = Mathf.Clamp01(Mathf.Abs(amps) / Mathf.Max(1e-6f, ActiveTripAmps));
                c = Color.Lerp(_okColor, _blownColor, t * 0.65f);
            }

            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", c);
            _mat.color = c;
            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                Color e = _blown ? _blownColor * 1.6f : (IsBypass ? Color.black : _okColor * (0.15f + 0.9f * Mathf.Clamp01(Mathf.Abs(amps) / 0.05f)));
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
            _readout.color = new Color(1f, 0.75f, 0.35f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "FUSE\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string tripLabel = ActiveLabel;
            float i = _circuit != null ? _circuit.CurrentAmperes : 0f;
            string iLabel = i.ToString("+0.000;-0.000;0.000") + "A";
            string state = _blown ? "BLOWN" : (IsBypass ? "BYPASS" : "OK");

            if (_coil == null)
            {
                _readout.text = "FUSE " + tripLabel + "\nno InductionCoil\nEnter Sandbox / Induction";
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

            string tip = _blown ? "N/trigger = rearm" : "N/P or VR trigger cycle";

            _readout.text =
                "FUSE " + tripLabel + " " + state + "\n"
                + "I " + iLabel + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + tip + "\n"
                + "[ideal |I| trip]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.45f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
