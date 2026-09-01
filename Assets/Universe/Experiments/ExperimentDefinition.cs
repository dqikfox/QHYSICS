using System;
using UnityEngine;

namespace RealityEngine.Experiments
{
    public enum ExperimentState
    {
        Idle = 0,
        Armed = 1,
        Recording = 2,
        Complete = 3
    }

    /// <summary>
    /// Player-driven experiment card. No chatbot. Hypothesis EMF is optional;
    /// leave hypothesisSet false to skip COMPARE.
    /// </summary>
    [Serializable]
    public class ExperimentDefinition
    {
        public string id = "magnet-faster-001";
        public string title = "What happens if I move the magnet faster?";
        public string question = "What happens if I move the magnet faster?";
        public int turnsN = 80;
        public float windingR = 2f;
        public float loadR = 8f;
        public float hypothesisEmf;
        public bool hypothesisSet;
        public string hypothesisHint = "Induced EMF scales with dPhi/dt (classical Faraday).";
        public string modelNotes =
            "EMF = -N dPhi/dt ; I = EMF / (R_winding + R_load). Resistive loop only (no L di/dt).";
        public string honestyTag =
            "Classical model / lumped Faraday (not a lab instrument)";

        public static ExperimentDefinition CannedMagnetFaster()
        {
            return new ExperimentDefinition();
        }

        public ExperimentDefinition Clone()
        {
            return new ExperimentDefinition
            {
                id = id,
                title = title,
                question = question,
                turnsN = turnsN,
                windingR = windingR,
                loadR = loadR,
                hypothesisEmf = hypothesisEmf,
                hypothesisSet = hypothesisSet,
                hypothesisHint = hypothesisHint,
                modelNotes = modelNotes,
                honestyTag = honestyTag
            };
        }
    }

    [Serializable]
    public struct ExperimentSample
    {
        public float t;
        public float phi;
        public float dPhiDt;
        public float emf;
        public float i;
        public float magnetSpeed;
    }

    [Serializable]
    public class ExperimentResult
    {
        public float durationSeconds;
        public int sampleCount;
        public float peakAbsEmf;
        public float peakAbsI;
        public float meanAbsDPhiDt;
        public float peakMagnetSpeed;
    }

    [Serializable]
    public class ExperimentCompare
    {
        public bool performed;
        public float predictedPeakEmf;
        public float measuredPeakEmf;
        public float relativeError;
        public string summary = string.Empty;
    }

    [Serializable]
    public class ExperimentRunRecord
    {
        public ExperimentDefinition definition;
        public ExperimentResult result;
        public ExperimentCompare compare;
        public string savedUtc;
        public string state;
        public ExperimentSample[] samples;
    }
}
