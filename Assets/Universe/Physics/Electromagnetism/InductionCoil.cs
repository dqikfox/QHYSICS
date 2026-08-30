using System;
using UnityEngine;

namespace RealityEngine.Physics.Electromagnetism
{
    /// <summary>
    /// Lumped Faraday-law coil. Samples <see cref="MagneticDipole.CalculateFieldAt"/> over the
    /// coil disk, integrates flux Φ = ∫ B·dA, then EMF = −N dΦ/dt.
    /// This is not a Maxwell solver and does not model the coil's own field.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class InductionCoil : MonoBehaviour
    {
        public const string ModelDisclaimer =
            "Classical model: two-pole magnet, lumped Faraday law. Visualization of B is a sampled field, not a photograph of reality.";

        [SerializeField]
        [Tooltip("Bar magnet (or other two-pole source) whose B field is sampled across this coil's disk.")]
        MagneticDipole magnet;

        [SerializeField]
        [Tooltip("Optional extra dipoles whose B is summed with the primary magnet. Leave empty for a single bar magnet.")]
        MagneticDipole[] additionalMagnets;

        [SerializeField]
        [Tooltip("Number of turns N in the lumped coil. EMF = −N dΦ/dt.")]
        int turns = 80;

        [SerializeField]
        [Tooltip("Coil radius in meters (world scale: 1 unit = 1 meter). Disk lies in the local XZ plane; local +Y is the area normal.")]
        float radius = 0.15f;

        [SerializeField]
        [Tooltip("Ohmic resistance of the winding itself, in ohms. Does not include the load.")]
        float resistance = 2.0f;

        [SerializeField]
        [Tooltip("Load resistance in ohms, in series with the winding. Total R = winding + load.")]
        float loadResistance = 8.0f;

        [SerializeField]
        [Tooltip("Number of concentric sample rings on the coil disk (plus a center sample). Keep small for Quest 3S.")]
        int radialSamples = 4;

        [SerializeField]
        [Tooltip("Angular samples per ring. Total B evaluations ≈ radialSamples × angularSamples + 1.")]
        int angularSamples = 12;

        [SerializeField]
        [Tooltip("If true, dΦ/dt uses unscaled time so handheld XR motion matches Faraday's law. Pause (timeScale 0) still freezes derivatives.")]
        bool useUnscaledTimeForDerivative = true;

        float _flux;
        float _dFluxDt;
        float _emf;
        float _previousFlux;
        bool _hasPreviousFlux;
        MagneticDipole[] _sources;

        public MagneticDipole Magnet
        {
            get => magnet;
            set
            {
                magnet = value;
                CacheSources();
            }
        }

        public int Turns => Mathf.Max(1, turns);
        public float Radius => Mathf.Max(0.001f, radius);
        public float Resistance => Mathf.Max(0f, resistance);
        public float LoadResistance => Mathf.Max(0f, loadResistance);
        public float Flux => _flux;
        public float FluxRate => _dFluxDt;
        public float Emf => _emf;
        public Vector3 AreaNormal => transform.up;

        public void SetMagnets(MagneticDipole primary, MagneticDipole[] extras)
        {
            magnet = primary;
            additionalMagnets = extras;
            CacheSources();
        }

        public void Configure(int turnCount, float radiusMeters, float windingOhms, float loadOhms)
        {
            turns = Mathf.Max(1, turnCount);
            radius = Mathf.Max(0.001f, radiusMeters);
            resistance = Mathf.Max(0f, windingOhms);
            loadResistance = Mathf.Max(0f, loadOhms);
        }

        void Awake()
        {
            CacheSources();
        }

        void OnEnable()
        {
            CacheSources();
            _hasPreviousFlux = false;
            _dFluxDt = 0f;
            _emf = 0f;
        }

        void CacheSources()
        {
            int extra = additionalMagnets != null ? additionalMagnets.Length : 0;
            int count = (magnet != null ? 1 : 0) + extra;
            if (_sources == null || _sources.Length != Mathf.Max(1, count))
                _sources = new MagneticDipole[Mathf.Max(1, count)];

            int i = 0;
            if (magnet != null)
                _sources[i++] = magnet;
            if (additionalMagnets != null)
            {
                for (int k = 0; k < additionalMagnets.Length; k++)
                {
                    if (additionalMagnets[k] != null)
                        _sources[i++] = additionalMagnets[k];
                }
            }

            if (i < _sources.Length)
                Array.Resize(ref _sources, i);
        }

        void LateUpdate()
        {
            _flux = IntegrateFlux();

            float dt = useUnscaledTimeForDerivative ? Time.unscaledDeltaTime : Time.deltaTime;
            bool paused = Time.timeScale <= 0f;

            if (paused)
            {
                _dFluxDt = 0f;
                _emf = 0f;
                _previousFlux = _flux;
                _hasPreviousFlux = true;
                return;
            }

            if (_hasPreviousFlux && dt > 1e-6f)
                _dFluxDt = (_flux - _previousFlux) / dt;
            else
                _dFluxDt = 0f;

            // Lumped Faraday law. Sign: EMF opposes flux increase (Lenz).
            _emf = -Turns * _dFluxDt;
            _previousFlux = _flux;
            _hasPreviousFlux = true;
        }

        /// <summary>
        /// Φ = ∫ B·dA over the coil disk using concentric rings in the local XZ plane.
        /// </summary>
        public float IntegrateFlux()
        {
            if (_sources == null || _sources.Length == 0)
                CacheSources();
            if (_sources == null || _sources.Length == 0)
                return 0f;

            Vector3 normal = transform.up;
            float R = Radius;
            int nR = Mathf.Max(1, radialSamples);
            int nA = Mathf.Max(3, angularSamples);
            float dr = R / nR;
            float flux = 0f;

            // Center disk of radius dr/2.
            Vector3 center = transform.position;
            Vector3 b0 = SampleB(center);
            float area0 = Mathf.PI * (0.5f * dr) * (0.5f * dr);
            flux += Vector3.Dot(b0, normal) * area0;

            for (int i = 0; i < nR; i++)
            {
                float r = (i + 0.5f) * dr;
                // Ring area 2π r dr, split across angular samples.
                float dA = (2f * Mathf.PI * r * dr) / nA;
                for (int j = 0; j < nA; j++)
                {
                    float theta = (j + 0.5f) * (2f * Mathf.PI / nA);
                    Vector3 local = new Vector3(r * Mathf.Cos(theta), 0f, r * Mathf.Sin(theta));
                    Vector3 world = transform.TransformPoint(local);
                    Vector3 b = SampleB(world);
                    flux += Vector3.Dot(b, normal) * dA;
                }
            }

            return flux;
        }

        Vector3 SampleB(Vector3 worldPosition)
        {
            Vector3 sum = Vector3.zero;
            MagneticDipole[] src = _sources;
            if (src == null)
                return sum;
            for (int i = 0; i < src.Length; i++)
            {
                MagneticDipole d = src[i];
                if (d != null)
                    sum += d.CalculateFieldAt(worldPosition);
            }
            return sum;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.55f, 0.15f, 0.9f);
            Vector3 n = transform.up;
            Vector3 origin = transform.position;
            Vector3 x = transform.right * Radius;
            Vector3 z = transform.forward * Radius;
            const int segs = 32;
            Vector3 prev = origin + x;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs * Mathf.PI * 2f;
                Vector3 p = origin + x * Mathf.Cos(t) + z * Mathf.Sin(t);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawLine(origin, origin + n * 0.05f);
        }
#endif
    }
}
