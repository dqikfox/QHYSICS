using UnityEngine;
using UnityEngine.Rendering;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Cheap VR field-line overlay. Each vertex is B from
    /// <see cref="MagneticDipole.CalculateFieldAt"/>, not decorative noise.
    /// Budget: a few dozen lines of short polylines.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagneticFieldViz : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Dipole whose sampled B field is drawn. Usually the grabbed bar magnet.")]
        MagneticDipole magnet;

        [SerializeField]
        [Tooltip("When disabled, line renderers are hidden. Toggle from the lab button or inspector.")]
        bool visible = true;

        [SerializeField]
        [Tooltip("How many field lines to integrate. Keep well under 100 for Quest 3S 90 Hz.")]
        int lineCount = 24;

        [SerializeField]
        [Tooltip("Integration steps per line (both directions from a seed point).")]
        int stepsPerLine = 10;

        [SerializeField]
        [Tooltip("Step length in meters along B-hat.")]
        float stepLength = 0.018f;

        [SerializeField]
        [Tooltip("Seed radius around the dipole origin, in meters.")]
        float seedRadius = 0.035f;

        [SerializeField]
        [Tooltip("World-space line width in meters.")]
        float lineWidth = 0.0035f;

        [SerializeField]
        [Tooltip("Stop integrating when |B| falls below this (tesla).")]
        float minFieldTesla = 1e-5f;

        MagneticDipole _magnet;
        LineRenderer[] _lines;
        Vector3[] _scratch;
        Material _lineMaterial;
        static Shader _cachedShader;
        bool _built;

        public MagneticDipole Magnet
        {
            get => _magnet != null ? _magnet : magnet;
            set => _magnet = magnet = value;
        }

        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                ApplyVisibility();
            }
        }

        public void ToggleVisible()
        {
            Visible = !visible;
        }

        void Awake()
        {
            _magnet = magnet;
            EnsureBuilt();
        }

        void OnEnable()
        {
            ApplyVisibility();
        }

        void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        void LateUpdate()
        {
            if (!visible)
                return;
            if (_magnet == null)
                _magnet = magnet;
            if (_magnet == null || !_built)
                return;
            RebuildLines();
        }

        public void EnsureBuilt()
        {
            if (_built)
                return;

            int n = Mathf.Clamp(lineCount, 4, 48);
            _scratch = new Vector3[Mathf.Max(2, stepsPerLine) * 2 + 1];
            _lines = new LineRenderer[n];
            _lineMaterial = CreateLineMaterial();

            for (int i = 0; i < n; i++)
            {
                var child = new GameObject("FieldLine_" + i);
                child.transform.SetParent(transform, false);
                var lr = child.AddComponent<LineRenderer>();
                lr.sharedMaterial = _lineMaterial;
                lr.widthMultiplier = lineWidth;
                lr.positionCount = 0;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 1;
                lr.textureMode = LineTextureMode.Stretch;
                lr.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                _lines[i] = lr;
            }

            _built = true;
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            if (_lines == null)
                return;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] != null)
                    _lines[i].enabled = visible;
            }
        }

        void RebuildLines()
        {
            Transform t = _magnet.transform;
            Vector3 origin = t.position;
            Vector3 axis = _magnet.GetWorldMomentVector();
            if (axis.sqrMagnitude < 1e-12f)
                axis = t.TransformDirection(_magnet.localAxis.sqrMagnitude > 0f ? _magnet.localAxis : Vector3.forward);
            axis.Normalize();
            Vector3 radial0 = Vector3.Cross(axis, t.right);
            if (radial0.sqrMagnitude < 1e-8f)
                radial0 = Vector3.Cross(axis, Vector3.forward);
            radial0.Normalize();
            Vector3 radial1 = Vector3.Cross(axis, radial0);

            int n = _lines.Length;
            int steps = Mathf.Max(2, stepsPerLine);
            float seed = Mathf.Max(0.005f, seedRadius);

            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n;
                float phi = u * Mathf.PI * 2f;
                // Seeds sit on a small ring offset toward +axis so lines leave the N side.
                Vector3 seedPos = origin
                    + axis * (seed * 0.35f)
                    + (radial0 * Mathf.Cos(phi) + radial1 * Mathf.Sin(phi)) * seed;

                int count = Trace(seedPos, steps);
                LineRenderer lr = _lines[i];
                lr.positionCount = count;
                if (count > 0)
                    lr.SetPositions(_scratch);
            }
        }

        int Trace(Vector3 seed, int steps)
        {
            // Integrate both ways from the seed so the polyline crosses the magnet.
            int backward = steps;
            Vector3[] back = new Vector3[backward];
            int bCount = Integrate(seed, -1f, back);

            int max = _scratch.Length;
            int w = 0;
            for (int i = bCount - 1; i >= 0; i--)
            {
                if (w < max)
                    _scratch[w++] = back[i];
            }

            if (w < max)
                _scratch[w++] = seed;

            Vector3[] fwd = new Vector3[steps];
            int fCount = Integrate(seed, 1f, fwd);
            for (int i = 0; i < fCount && w < max; i++)
                _scratch[w++] = fwd[i];

            return w;
        }

        int Integrate(Vector3 start, float direction, Vector3[] dest)
        {
            Vector3 p = start;
            int written = 0;
            float ds = Mathf.Max(0.002f, stepLength);
            float minB2 = minFieldTesla * minFieldTesla;

            for (int i = 0; i < dest.Length; i++)
            {
                Vector3 b = _magnet.CalculateFieldAt(p);
                float mag2 = b.sqrMagnitude;
                if (mag2 < minB2)
                    break;
                p += b * (direction * ds / Mathf.Sqrt(mag2));
                dest[written++] = p;
            }

            return written;
        }

        static Material CreateLineMaterial()
        {
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Sprites/Default");
                if (_cachedShader == null)
                    _cachedShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (_cachedShader == null)
                    _cachedShader = Shader.Find("Unlit/Color");
            }

            var mat = new Material(_cachedShader)
            {
                name = "MagneticFieldLine",
                color = new Color(0.25f, 0.95f, 0.55f, 0.95f),
                hideFlags = HideFlags.HideAndDontSave
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.25f, 0.95f, 0.55f, 0.95f));
            return mat;
        }
    }
}
