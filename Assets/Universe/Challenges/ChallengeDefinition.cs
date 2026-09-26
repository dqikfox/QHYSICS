using System;
using UnityEngine;

namespace RealityEngine.Challenges
{
    /// <summary>
    /// Type of objective that can be evaluated by polling CircuitLab / InductionCircuit public state.
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>At least one bulb with significant current flowing (filament lit).</summary>
        BulbLit = 0,
        /// <summary>At least one resistor placed and in an active circuit alongside a lit bulb.</summary>
        ResistorInCircuit = 1,
        /// <summary>At least one motor spinning at or above targetValue RPM.</summary>
        MotorRpmThreshold = 2,
        /// <summary>targetCount bulbs lit simultaneously in active circuits.</summary>
        MultipleBulbsLit = 3,
        /// <summary>A solar panel in an active circuit with a load (bulb or motor) also active.</summary>
        SolarPoweringLoad = 4,
        /// <summary>Induced EMF (|EmfVolts|) from InductionCircuit >= targetValue volts.</summary>
        InducedEmfThreshold = 5,
        /// <summary>At least one placed Switch clone is closed AND at least one placed Bulb clone is lit.</summary>
        SwitchClosedBulbLit = 6,
        /// <summary>At least one placed Switch clone is closed AND a placed Motor clone reaches targetValue RPM.</summary>
        SwitchClosedMotorSpinning = 7,
        /// <summary>At least one placed load (bulb or motor) delivering |V|*|I| power >= targetValue watts.</summary>
        CircuitPowerThreshold = 8,
        /// <summary>At least targetCount spawned lab roots whose name starts with targetTag (e.g. "Gadget_Multimeter"; default "Gadget_").</summary>
        GadgetPresent = 9,
        /// <summary>A spawned LoadCompassGadget reads horizontal |B| >= targetValue tesla (Earth ~5e-5 T, so this needs a magnet nearby).</summary>
        CompassFieldThreshold = 10,
        /// <summary>A LoadCrankGeneratorGadget is cranking with nearby peak |EMF| >= targetValue volts, held for targetCount seconds (sustained).</summary>
        CrankEmfThreshold = 11,
        /// <summary>A coupled LoadMutualCouplerGadget latched peak secondary |EMF| >= targetValue volts.</summary>
        MutualEmfThreshold = 12
    }

    /// <summary>
    /// A single measurable objective within a challenge.
    /// </summary>
    [Serializable]
    public class ChallengeObjective
    {
        public ObjectiveType type = ObjectiveType.BulbLit;
        [Tooltip("Human-readable instruction shown in the objective overlay.")]
        public string displayText = "Complete the objective";
        [Tooltip("Numeric threshold (e.g. RPM for MotorRpmThreshold, volts for InducedEmfThreshold).")]
        public float targetValue = 0f;
        [Tooltip("Count threshold (e.g. number of bulbs for MultipleBulbsLit).")]
        public int targetCount = 1;
        [Tooltip("Optional name/tag filter (e.g. GadgetPresent root-name prefix).")]
        public string targetTag = "";

        /// <summary>Runtime completion state â€” not serialized.</summary>
        [NonSerialized] public bool completed;
        /// <summary>Runtime hold timer for sustained objectives â€” not serialized.</summary>
        [NonSerialized] public float heldSeconds;

        public ChallengeObjective Clone()
        {
            return new ChallengeObjective
            {
                type = type,
                displayText = displayText,
                targetValue = targetValue,
                targetCount = targetCount,
                targetTag = targetTag
            };
        }
    }

    /// <summary>
    /// Star rating thresholds.
    /// 1 star  = complete all objectives.
    /// 2 stars = complete all objectives within twoStarTimeSeconds.
    /// 3 stars = complete within threeStarTimeSeconds AND (if threeStarMaxComponents > 0)
    ///           using threeStarMaxComponents or fewer placed components.
    /// </summary>
    [Serializable]
    public class StarThresholds
    {
        public float twoStarTimeSeconds = 90f;
        public float threeStarTimeSeconds = 45f;
        [Tooltip("Max placed components for 3 stars. 0 = no component limit.")]
        public int threeStarMaxComponents;
    }

    /// <summary>
    /// Serializable challenge definition: id, title, description, ordered objectives,
    /// star thresholds, prerequisite challenge id (null/empty = unlocked from start).
    /// </summary>
    [Serializable]
    public class ChallengeDefinition
    {
        public string id = "challenge";
        public string title = "Challenge";
        [TextArea(2, 6)] public string description = "Complete the objectives.";
        [TextArea(1, 3)] public string mentorHint = "";
        [Tooltip("Prerequisite challenge id. Empty/null = unlocked from start.")]
        public string prerequisiteId = "";
        public ChallengeObjective[] objectives;
        public StarThresholds starThresholds = new StarThresholds();

        public bool IsUnlockedFromStart => string.IsNullOrEmpty(prerequisiteId);

        /// <summary>Deep-clone the objectives array for runtime evaluation (preserves original definitions).</summary>
        public ChallengeObjective[] CloneObjectives()
        {
            if (objectives == null)
                return new ChallengeObjective[0];
            var arr = new ChallengeObjective[objectives.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = objectives[i] != null ? objectives[i].Clone() : new ChallengeObjective();
            return arr;
        }
    }
}
