using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable MEASURE field probe: samples classical MagneticDipole B at the tip.
    /// Honesty: two-pole / dipole model in Tesla - not a Hall sensor or quantum state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldProbe : MonoBehaviour
    {
        public const string RootName = "Gadget_FieldProbe";
        public const string Honesty = "Classical dipole/two-pole B(r). Not a Hall probe.";

        [SerializeField, Tooltip("Tip offset in local space (meters).")]
        Vector3 tipLocal = new Vector3(0f, 0f, 0.08f);

        TextMeshPro _readout;
        MagneticDipole[] _dipoles;
        float _refreshAt;
        float _cacheAt;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);

        public void EnsureBuilt()
        {
            if (_readout == null)
                BuildReadout();
            EnsureDipoles(force: true);
            EnsureLabHasMagnet();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.08f;
            if (Time.unscaledTime >= _cacheAt || _dipoles == null)
                EnsureDipoles(force: false);
            RefreshText();
        }

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

        void EnsureLabHasMagnet()
        {
            if (_dipoles != null && _dipoles.Length > 0)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            EnsureDipoles(force: true);
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

            Vector3 tip = TipWorld;
            Vector3 b = Vector3.zero;
            int n = 0;
            if (_dipoles != null)
            {
                for (int i = 0; i < _dipoles.Length; i++)
                {
                    MagneticDipole d = _dipoles[i];
                    if (d == null || !d.isActive)
                        continue;
                    b += d.CalculateFieldAt(tip);
                    n++;
                }
            }

            if (n == 0)
            {
                _readout.text = "PROBE B\nno MagneticDipole\nEnter Sandbox / Induction";
                return;
            }

            float mag = b.magnitude;
            string magLine = FormatTesla(mag);
            _readout.text =
                "PROBE |B| " + magLine + "\n"
                + "Bx " + FormatTesla(b.x) + "\n"
                + "By " + FormatTesla(b.y) + "\n"
                + "Bz " + FormatTesla(b.z) + "\n"
                + n + " dipole(s) [classical]";
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