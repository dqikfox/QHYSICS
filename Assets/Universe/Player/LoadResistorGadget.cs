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
    /// Grabbable CIRCUIT resistor: while near an InductionCoil, applies a classical series load R.
    /// Honesty: lumped R_load only - not a carbon-film part, not temperature-dependent.
    /// XR activate / N cycles presets; P steps backward.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadResistorGadget : MonoBehaviour
    {
        public const string RootName = "Gadget_Resistor";
        public const string Honesty = "Classical series R_load on InductionCoil. Lumped ohmic model.";

        static readonly float[] Presets = { 2f, 8f, 50f, 1e6f };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-coil binding.")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.05f);

        [SerializeField, Tooltip("Max distance (m) to stay connected and drive load R.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCoil _coil;
        InductionCoil[] _coils;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 1; // default 8 ohm
        bool _applied;
        float _savedLoad = 8f;
        InductionCoil _appliedCoil;
        XRGrabInteractable _grab;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveOhms => Presets[Mathf.Clamp(_presetIndex, 0, Presets.Length - 1)];

        public void EnsureBuilt()
        {
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
            // Only while this gadget is selected / near camera (held-ish): tip within 1.2m of main camera.
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
            // Re-apply immediately if currently connected.
            if (_applied && _appliedCoil != null)
                _appliedCoil.SetLoadResistance(ActiveOhms);
            RefreshText();
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

            if (!_applied || _appliedCoil != _coil)
            {
                RestoreApplied();
                _savedLoad = _coil.LoadResistance;
                _appliedCoil = _coil;
                _applied = true;
            }

            _appliedCoil.SetLoadResistance(ActiveOhms);
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
            _readout.color = new Color(0.95f, 0.7f, 0.35f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "RESISTOR\nseeking coil...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string rLine = FormatOhms(ActiveOhms);
            if (_coil == null)
            {
                _readout.text = "LOAD " + rLine + "\nno InductionCoil\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _coil.transform.position);
            string target = _coil.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _applied ? "ON coil" : (dist > connectWithinMeters ? "far" : "near");
            _readout.text =
                "LOAD " + rLine + "\n"
                + link + " " + dist.ToString("0.00") + "m\n"
                + target + "\n"
                + "N/P or VR trigger cycle\n"
                + "[classical R_load]";
        }

        static string FormatOhms(float r)
        {
            if (r >= 1e5f)
                return "OPEN";
            if (r >= 1000f)
                return (r * 0.001f).ToString("0.##") + " kOhm";
            return r.ToString("0.##") + " Ohm";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.55f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
