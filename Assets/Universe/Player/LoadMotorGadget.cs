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
    /// Grabbable CIRCUIT motor: while near an InductionCircuit, spins a rotor from classical |I|.
    /// Honesty: kinematic visual proxy — not torque, not back-EMF, not a DC machine model.
    /// Does not set R_load (use CIRCUIT Resistor). XR activate / N/P / desktop LMB/scroll cycles gear.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadMotorGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Motor";
        public const string Honesty = "Classical |I| spin proxy on InductionCircuit. Not torque/back-EMF.";

        // Gear multipliers: slow / normal / fast / reverse
        static readonly float[] GearPresets = { 0.5f, 1f, 2f, -1f };
        static readonly string[] GearLabels = { "0.5x", "1x", "2x", "REV" };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and drive spin.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Degrees per second at NormalizedLoadCurrent = 1 and gear = 1x.")]
        float baseSpinDegPerSec = 360f;

        TextMeshPro _readout;
        Transform _rotor;
        Renderer _rotorRenderer;
        Material _mat;
        Color _baseColor = new Color(0.35f, 0.55f, 0.85f, 1f);
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _gearIndex = 1;
        bool _linked;
        XRGrabInteractable _grab;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveGear => GearPresets[Mathf.Clamp(_gearIndex, 0, GearPresets.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCircuits(force: true);
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
            UnwireGrab();
        }

        void OnDestroy()
        {
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
            float dt = Time.deltaTime;
            if (Time.unscaledTime >= _cacheAt || _circuits == null)
                EnsureCircuits(force: false);

            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();

            float spin01 = 0f;
            _linked = false;
            if (_circuit != null)
            {
                float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
                if (dist <= connectWithinMeters)
                {
                    _linked = true;
                    // Signed current direction * gear (NormalizedLoadCurrent is |I| proxy — use CurrentAmperes sign)
                    float norm = _circuit.NormalizedLoadCurrent;
                    float sign = Mathf.Sign(_circuit.CurrentAmperes);
                    if (Mathf.Abs(_circuit.CurrentAmperes) < 1e-8f)
                        sign = 1f;
                    spin01 = norm * sign;
                }
            }

            if (_rotor != null && _linked && Mathf.Abs(spin01) > 1e-5f)
            {
                float deg = spin01 * ActiveGear * baseSpinDegPerSec * dt;
                _rotor.Rotate(0f, 0f, deg, Space.Self);
            }

            PollDesktopCycle();

            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.08f;
            ApplyVisual(Mathf.Abs(spin01));
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
            CycleGear(+1);
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
                CycleGear(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                CycleGear(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleGear(int delta)
        {
            if (GearPresets.Length == 0)
                return;
            _gearIndex = (_gearIndex + delta) % GearPresets.Length;
            if (_gearIndex < 0)
                _gearIndex += GearPresets.Length;
            RefreshText();
        }

        public void DesktopActivate(int delta) => CycleGear(delta);

        void EnsureVisual()
        {
            if (_rotor == null)
            {
                Transform existing = transform.Find("Rotor");
                if (existing != null)
                    _rotor = existing;
                else
                {
                    var rotorGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rotorGo.name = "Rotor";
                    rotorGo.transform.SetParent(transform, false);
                    rotorGo.transform.localPosition = new Vector3(0f, 0.02f, 0.04f);
                    rotorGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    rotorGo.transform.localScale = new Vector3(0.06f, 0.02f, 0.06f);
                    Object.Destroy(rotorGo.GetComponent<Collider>());
                    _rotor = rotorGo.transform;
                }
            }

            if (_rotorRenderer == null && _rotor != null)
                _rotorRenderer = _rotor.GetComponent<Renderer>();

            if (_mat == null && _rotorRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", _baseColor);
                _mat.color = _baseColor;
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", Color.black);
                }
                _rotorRenderer.sharedMaterial = _mat;
            }
        }

        void ApplyVisual(float spin01)
        {
            if (_mat == null)
                EnsureVisual();
            if (_mat == null)
                return;

            float g = Mathf.Clamp01(spin01);
            Color lit = Color.Lerp(_baseColor * 0.4f, _baseColor, 0.4f + 0.6f * g);
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", lit);
            _mat.color = lit;
            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                Color e = new Color(0.35f, 0.65f, 1f) * (g * 2.2f);
                _mat.SetColor("_EmissionColor", e);
            }
        }

        void EnsureCircuits(bool force)
        {
            _cacheAt = Time.unscaledTime + 1.25f;
            if (!force && _circuits != null && _circuits.Length > 0)
            {
                for (int i = 0; i < _circuits.Length; i++)
                {
                    if (_circuits[i] != null)
                        return;
                }
            }
            _circuits = Object.FindObjectsByType<InductionCircuit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        void BindNearest()
        {
            if (_circuits == null || _circuits.Length == 0)
            {
                _circuit = null;
                return;
            }

            Vector3 tip = TipWorld;
            InductionCircuit best = null;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < _circuits.Length; i++)
            {
                InductionCircuit c = _circuits[i];
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
            _circuit = best;
        }

        void EnsureLabCircuit()
        {
            if (_circuit != null)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            EnsureCircuits(force: true);
            BindNearest();
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
            _readout.color = new Color(0.55f, 0.8f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "MOTOR\nseeking I...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string gear = GearLabels[Mathf.Clamp(_gearIndex, 0, GearLabels.Length - 1)];
            if (_circuit == null)
            {
                _readout.text = "MOTOR " + gear + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON" : (dist > connectWithinMeters ? "far" : "near");
            float i = _linked ? _circuit.CurrentAmperes : 0f;
            _readout.text =
                "MOTOR " + gear + " " + link + "\n"
                + "I " + FormatAmps(i) + "\n"
                + target + " " + dist.ToString("0.00") + "m\n"
                + "N/P or VR trigger gear\n"
                + "[classical |I| spin]";
        }

        static string FormatAmps(float a)
        {
            float abs = Mathf.Abs(a);
            if (abs < 1e-4f)
                return "0 A";
            if (abs < 1e-2f)
                return (a * 1000f).ToString("0.##") + " mA";
            return a.ToString("0.####") + " A";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
