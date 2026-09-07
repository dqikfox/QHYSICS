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
    /// Grabbable MEASURE field probe: samples classical MagneticDipole B at the tip.
    /// Honesty: two-pole / dipole model in Tesla - not a Hall sensor or quantum state.
    /// Binds nearest dipole by tip distance; N/P, VR trigger, or desktop LMB/scroll cycles NEAREST/ALL/COMP.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldProbe : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_FieldProbe";
        public const string Honesty = "Classical dipole/two-pole B(r). Not a Hall probe.";

        static readonly string[] ModeNames = { "NEAREST", "ALL", "COMP" };

        [SerializeField, Tooltip("Tip offset in local space (meters).")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.08f);

        [SerializeField, Tooltip("Advisory prefer distance (m); still binds closest beyond this.")]
        float preferWithinMeters = 1.25f;

        TextMeshPro _readout;
        MagneticDipole _nearest;
        MagneticDipole[] _dipoles;
        XRGrabInteractable _grab;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _modeIndex;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public MagneticDipole BoundDipole => _nearest;
        public string ActiveModeName => ModeNames[Mathf.Clamp(_modeIndex, 0, ModeNames.Length - 1)];

        public void EnsureBuilt()
        {
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureDipoles(force: true);
            BindNearest();
            if (_nearest == null)
                EnsureLabHasMagnet();
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
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.08f;
            if (Time.unscaledTime >= _cacheAt || _dipoles == null)
                EnsureDipoles(force: false);
            BindNearest();
            if (_nearest == null)
                EnsureLabHasMagnet();
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
            _modeIndex = (_modeIndex + delta + ModeNames.Length) % ModeNames.Length;
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsureDipoles(bool force)
        {
            _cacheAt = Time.unscaledTime + 1.5f;
            if (!force && _dipoles != null && _dipoles.Length > 0)
            {
                for (int i = 0; i < _dipoles.Length; i++)
                {
                    if (_dipoles[i] != null)
                        return;
                }
            }
            _dipoles = Object.FindObjectsByType<MagneticDipole>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        void BindNearest()
        {
            if (_dipoles == null || _dipoles.Length == 0)
            {
                _nearest = null;
                return;
            }

            Vector3 tip = TipWorld;
            MagneticDipole best = null;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < _dipoles.Length; i++)
            {
                MagneticDipole d = _dipoles[i];
                if (d == null || !d.isActive)
                    continue;
                if (d.transform == transform || d.transform.IsChildOf(transform))
                    continue;
                float sq = (d.transform.position - tip).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = d;
                }
            }

            _nearest = best;
        }

        void EnsureLabHasMagnet()
        {
            if (_nearest != null)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            EnsureDipoles(force: true);
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
                go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 0.85f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "PROBE\nseeking B...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string mode = ActiveModeName;
            if (_nearest == null && (_dipoles == null || CountActiveDipoles() == 0))
            {
                _readout.text =
                    "PROBE " + mode + "\n"
                    + "no MagneticDipole\n"
                    + "Enter Sandbox / Induction\n"
                    + "N/P or VR trigger cycle";
                return;
            }

            Vector3 tip = TipWorld;
            Vector3 b;
            string footer;
            int n;

            if (_modeIndex == 1) // ALL
            {
                b = SampleAll(tip, out n);
                footer = n + " dipole(s)";
            }
            else
            {
                if (_nearest == null)
                {
                    _readout.text =
                        "PROBE " + mode + "\n"
                        + "no nearest dipole\n"
                        + "move tip near Magnet\n"
                        + "N/P cycle";
                    return;
                }

                b = _nearest.CalculateFieldAt(tip);
                n = 1;
                float dist = Vector3.Distance(tip, _nearest.transform.position);
                string target = _nearest.name;
                if (target.Length > 18)
                    target = target.Substring(0, 16) + "..";
                string far = dist > preferWithinMeters ? " far" : "";
                footer = target + " " + dist.ToString("0.00") + "m" + far;
            }

            if (_modeIndex == 2) // COMP
            {
                _readout.text =
                    "PROBE COMP\n"
                    + "Bx " + FormatTesla(b.x) + "\n"
                    + "By " + FormatTesla(b.y) + "\n"
                    + "Bz " + FormatTesla(b.z) + "\n"
                    + footer + "\n"
                    + "[" + mode + " classical]";
                return;
            }

            float mag = b.magnitude;
            _readout.text =
                "PROBE |B| " + FormatTesla(mag) + "\n"
                + (_modeIndex == 1
                    ? ("sum " + n + " dipole(s)\n")
                    : ("Bx " + FormatTesla(b.x) + "\n"))
                + footer + "\n"
                + "[" + mode + " classical]";
        }

        Vector3 SampleAll(Vector3 tip, out int n)
        {
            Vector3 b = Vector3.zero;
            n = 0;
            if (_dipoles == null)
                return b;
            for (int i = 0; i < _dipoles.Length; i++)
            {
                MagneticDipole d = _dipoles[i];
                if (d == null || !d.isActive)
                    continue;
                if (d.transform == transform || d.transform.IsChildOf(transform))
                    continue;
                b += d.CalculateFieldAt(tip);
                n++;
            }
            return b;
        }

        int CountActiveDipoles()
        {
            if (_dipoles == null)
                return 0;
            int n = 0;
            for (int i = 0; i < _dipoles.Length; i++)
            {
                MagneticDipole d = _dipoles[i];
                if (d != null && d.isActive)
                    n++;
            }
            return n;
        }

        static string FormatTesla(float t)
        {
            float a = Mathf.Abs(t);
            if (a >= 1f)
                return t.ToString("0.###") + " T";
            if (a >= 1e-3f)
                return (t * 1e3f).ToString("0.##") + " mT";
            return (t * 1e6f).ToString("0.#") + " uT";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
