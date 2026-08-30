using UnityEngine;

namespace RealityEngine.Physics.Electromagnetism
{
    /// <summary>
    /// Resistive loop attached to <see cref="InductionCoil"/>.
    /// I = EMF / R_total, P = I² R_load.
    /// v0.3 neglects inductance and the coil's self-flux (no RL lag, no L di/dt).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InductionCoil))]
    [DefaultExecutionOrder(30)]
    public sealed class InductionCircuit : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Coil that provides lumped EMF. Cached on Awake; do not swap per-frame.")]
        InductionCoil coil;

        [SerializeField]
        [Tooltip("Reference current (amperes) used only by visuals that scale glow with |I|. Does not affect the physics.")]
        float visualCurrentReference = 0.05f;

        InductionCoil _coil;

        public InductionCoil Coil => _coil != null ? _coil : coil;
        public float EmfVolts { get; private set; }
        public float CurrentAmperes { get; private set; }
        public float LoadPowerWatts { get; private set; }
        public float TotalResistanceOhms { get; private set; }
        public float FluxWebers { get; private set; }
        public float FluxRateWebersPerSecond { get; private set; }

        public float VisualCurrentReference => Mathf.Max(1e-6f, visualCurrentReference);

        public float NormalizedLoadCurrent
        {
            get
            {
                float iRef = VisualCurrentReference;
                return Mathf.Clamp01(Mathf.Abs(CurrentAmperes) / iRef);
            }
        }

        public void SetCoil(InductionCoil value)
        {
            coil = value;
            _coil = value;
        }

        void Awake()
        {
            _coil = coil != null ? coil : GetComponent<InductionCoil>();
        }

        void LateUpdate()
        {
            if (_coil == null)
                _coil = coil != null ? coil : GetComponent<InductionCoil>();
            if (_coil == null)
            {
                EmfVolts = 0f;
                CurrentAmperes = 0f;
                LoadPowerWatts = 0f;
                return;
            }

            FluxWebers = _coil.Flux;
            FluxRateWebersPerSecond = _coil.FluxRate;
            EmfVolts = _coil.Emf;

            // Resistive loop only. Self-inductance L and the coil's own B are omitted in v0.3,
            // so there is no RL time constant: I = EMF / (R_winding + R_load) instantaneously.
            float rWinding = _coil.Resistance;
            float rLoad = _coil.LoadResistance;
            TotalResistanceOhms = rWinding + rLoad;
            if (TotalResistanceOhms < 1e-6f)
            {
                CurrentAmperes = 0f;
                LoadPowerWatts = 0f;
                return;
            }

            CurrentAmperes = EmfVolts / TotalResistanceOhms;
            LoadPowerWatts = CurrentAmperes * CurrentAmperes * rLoad;
        }
    }
}
