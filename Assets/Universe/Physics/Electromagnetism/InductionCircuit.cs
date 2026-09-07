using UnityEngine;

namespace RealityEngine.Physics.Electromagnetism
{
    /// <summary>
    /// Resistive (optional series-C) loop attached to <see cref="InductionCoil"/>.
    /// Without C: I = EMF / R_total, P = I² R_load (v0.3 — no L).
    /// With series C (CIRCUIT Capacitor): I = (EMF − V_c) / R_total, dV_c/dt = I/C.
    /// Honesty: lumped RC only — not dielectric physics, not parasitic ESR/ESL.
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
        float _capacitorVolts;

        public InductionCoil Coil => _coil != null ? _coil : coil;
        public float EmfVolts { get; private set; }
        public float CurrentAmperes { get; private set; }
        public float LoadPowerWatts { get; private set; }
        public float TotalResistanceOhms { get; private set; }
        public float FluxWebers { get; private set; }
        public float FluxRateWebersPerSecond { get; private set; }
        public float CapacitorVolts => _capacitorVolts;

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

        /// <summary>Clear stored capacitor voltage (New Run / experiment reset).</summary>
        public void ResetCapacitorVoltage()
        {
            _capacitorVolts = 0f;
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

            float rWinding = _coil.Resistance;
            float rLoad = _coil.LoadResistance;
            TotalResistanceOhms = rWinding + rLoad;
            if (TotalResistanceOhms < 1e-6f)
            {
                CurrentAmperes = 0f;
                LoadPowerWatts = 0f;
                return;
            }

            float C = _coil.SeriesCapacitance;
            if (C < 1e-9f)
            {
                // Resistive loop only — bypass C.
                _capacitorVolts = 0f;
                CurrentAmperes = EmfVolts / TotalResistanceOhms;
            }
            else
            {
                // Lumped series RC: I = (EMF − Vc) / R; dVc/dt = I/C.
                CurrentAmperes = (EmfVolts - _capacitorVolts) / TotalResistanceOhms;
                float dt = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
                if (dt > 1e-6f)
                    _capacitorVolts += CurrentAmperes * dt / C;
            }

            LoadPowerWatts = CurrentAmperes * CurrentAmperes * rLoad;
        }
    }
}
