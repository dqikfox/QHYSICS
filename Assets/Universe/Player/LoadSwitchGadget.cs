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
    /// Grabbable CIRCUIT switch: while near an InductionCoil, toggles classical series load OPEN vs CLOSED.
    /// Honesty: lumped open/closed only - not a physical contact model.
    /// XR activate / N / P toggles. DefaultExecutionOrder(40) so OPEN overrides Resistor R after Resistor Update.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class LoadSwitchGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Switch";
        public const string Honesty = "Classical series open/closed on InductionCoil. Lumped switch model.";

        const float OpenOhms = 1e6f;

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.05f);

        [SerializeField, Tooltip("Max distance (m) to stay connected and drive load.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCoil _coil;
        InductionCoil[] _coils;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        bool _closed = true;
        bool _applied;
        float _savedLoad = 8f;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;
        Renderer _renderer;
        Color _closedColor = new Color(0.35f, 0.7f, 0.35f, 1f);
        Color _openColor = new Color(0.75f, 0.25f, 0.25f, 1f);

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public bool IsClosed => _closed;

        public void EnsureBuilt()
        {
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
            PollDesktopToggle();
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
            Toggle();
        }

        void PollDesktopToggle()
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
            if (kb.nKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            {
                Toggle();
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void Toggle()
        {
            _closed = !_closed;
            ApplyVisual();
            // Re-apply immediately if connected so OPEN/CLOSED takes effect this frame.
            ApplyOrRelease();
            RefreshText();
        }

        public void DesktopActivate(int delta) => Toggle();

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

            // Connected + CLOSED: do not force R; restore prior OPEN apply if any.
            if (_closed)
            {
                RestoreApplied();
                return;
            }

            // Connected + OPEN: force classical open-circuit ohms every refresh.
            if (!_applied || _appliedCoil != _coil)
            {
                RestoreApplied();
                _savedLoad = _coil.LoadResistance;
                _appliedCoil = _coil;
                _applied = true;
            }

            _appliedCoil.SetLoadResistance(OpenOhms);
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

        void ApplyVisual()
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
            if (_renderer == null)
                return;

            Color c = _closed ? _closedColor : _openColor;
            var mat = _renderer.material;
            if (mat == null)
                return;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
            mat.color = c;
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
                go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 0.9f, 0.55f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "SWITCH\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string state = _closed ? "CLOSED" : "OPEN";
            if (_coil == null)
            {
                _readout.text = "SWITCH " + state + "\nno InductionCoil\nEnter Sandbox / Induction";
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
                link = "OPEN on coil";
            else if (_closed)
                link = "near (pass)";
            else
                link = "near";

            _readout.text =
                "SWITCH " + state + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger toggle\n"
                + "[classical open/closed]";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.75f, 0.35f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}