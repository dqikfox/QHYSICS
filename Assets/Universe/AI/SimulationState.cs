using UnityEngine;

namespace RealityEngine.AI
{
    /// <summary>
    /// Live snapshot of the lumped Faraday lab. Values are copied from
    /// InductionCoil, InductionCircuit, and the magnet rigidbody — not invented.
    /// </summary>
    public struct SimulationState
    {
        public int turnsN;
        public float totalResistanceOhms;
        public float windingResistanceOhms;
        public float loadResistanceOhms;
        public float fluxWebers;
        public float dFluxDt;
        public float emfVolts;
        public float currentAmperes;
        public float magnetSpeed;
        public float timeScale;
        public string modelNotes;

        public static readonly string FaradayNotes =
            "EMF = -N dPhi/dt ; I = EMF / (R_winding + R_load). Resistive loop only (no L di/dt).";
    }

    public enum ScientistQuestion
    {
        DoubleMagnetVelocity = 0,
        DoubleTurnsN = 1,
        DoubleResistanceR = 2,
        WhyCopperConductor = 3,
        WhereMuscleEnergy = 4,
        IsEnergyCreated = 5,
        WhyKhufuSlope = 6
    }

    [System.Serializable]
    public struct Hypothesis
    {
        public string text;
        public float predictedPeakEmf;
        public string basedOn;
        public bool hasNumericPrediction;

        public static Hypothesis NeedBaseline(string questionText, string equation)
        {
            return new Hypothesis
            {
                text = questionText + " Need a baseline run before a numeric peak-|EMF| prediction.",
                predictedPeakEmf = 0f,
                basedOn = "no recorded run. Equation: " + equation,
                hasNumericPrediction = false
            };
        }
    }
}