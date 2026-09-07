using UnityEngine;

namespace RealityEngine.Physics.Electromagnetism
{
    /// <summary>
    /// Resistive (optional series-C / series-L) loop attached to <see cref="InductionCoil"/>.
    /// Without L/C: I = EMF / R_total, P = I² R_load.
    /// With series C (CIRCUIT Capacitor): I = (EMF − V_c) / R_total, dV_c/dt = I/C.
    /// With series L (CIRCUIT Inductor): L dI/dt = EMF − R I − V_c (V_c=0 if no C).
    /// With L+C: classical series RLC. Honesty: lumped only — not skin effect, not core saturation, not parasitics.
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
        float _inductorCurrent;

        public InductionCoil Coil => _coil != null ? _coil : coil;
        public float EmfVolts { get; private set; }
        public float CurrentAmperes { get; private set; }
        public float LoadPowerWatts { get; private set; }
        public float TotalResistanceOhms { get; private set; }
        public float FluxWebers { get; private set; }
        public float FluxRateWebersPerSecond { get; private set; }
        public float CapacitorVolts => _capacitorVolts;
        public float InductorCurrentAmperes => _inductorCurrent;

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

        /// <summary>Clear stored inductor current (New Run / experiment reset).</summary>
        public void ResetInductorCurrent()
        {
            _inductorCurrent = 0f;
            CurrentAmperes = 0f;
        }

        /// <summary>Clear Vc and inductor I (New Run).</summary>
        public void ResetEnergyStorage()
        {
            ResetCapacitorVoltage();
            ResetInductorCurrent();
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
            float L = _coil.SeriesInductance;
            float dt = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
            bool hasC = C >= 1e-9f;
            bool hasL = L >= 1e-9f;

            if (!hasC)
                _capacitorVolts = 0f;

            if (!hasL)
            {
                // Algebraic I (resistive or RC) — inductor bypassed.
                if (!hasC)
                {
                    CurrentAmperes = EmfVolts / TotalResistanceOhms;
                }
                else
                {
                    CurrentAmperes = (EmfVolts - _capacitorVolts) / TotalResistanceOhms;
                    if (dt > 1e-6f)
                        _capacitorVolts += CurrentAmperes * dt / C;
                }
                _inductorCurrent = CurrentAmperes;
            }
            else
            {
                // Series RL or RLC: L dI/dt = EMF - R I - Vc
                float vc = hasC ? _capacitorVolts : 0f;
                float dIdt = (EmfVolts - TotalResistanceOhms * _inductorCurrent - vc) / L;
                if (dt > 1e-6f)
                    _inductorCurrent += dIdt * dt;
                CurrentAmperes = _inductorCurrent;
                if (hasC && dt > 1e-6f)
                    _capacitorVolts += CurrentAmperes * dt / C;
            }

            LoadPowerWatts = CurrentAmperes * CurrentAmperes * rLoad;
        }
    }
}
