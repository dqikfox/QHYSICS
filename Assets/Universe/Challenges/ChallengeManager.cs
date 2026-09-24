using System;
using System.Reflection;
using UnityEngine;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Stations;

namespace RealityEngine.Challenges
{
    /// <summary>
    /// Reflection-based reader for CircuitComponent protected members.
    /// Avoids modifying the simulation scripts (CircuitLab.cs, Bulb.cs, Motor.cs, etc.).
    /// </summary>
    internal static class CircuitReader
    {
        const double SignificantCurrent = 0.0000001;

        static PropertyInfo _isActiveProp;
        static FieldInfo _motorSpeedField;
        static FieldInfo _motorNormalSpeedField;
        static FieldInfo _motorBaseCurrentField;
        static bool _motorFieldsResolved;

        static PropertyInfo IsActiveProp
        {
            get
            {
                if (_isActiveProp == null)
                    _isActiveProp = typeof(CircuitComponent).GetProperty(
                        "IsActive", BindingFlags.NonPublic | BindingFlags.Instance);
                return _isActiveProp;
            }
        }

        /// <summary>True if the component is currently active in a completed circuit.</summary>
        public static bool GetIsActive(CircuitComponent component)
        {
            if (component == null)
                return false;
            if (IsActiveProp != null)
                return (bool)IsActiveProp.GetValue(component);
            // Fallback: check current significance
            return component.GetCurrentValue() > SignificantCurrent;
        }

        /// <summary>True if current exceeds the CircuitLab significance threshold.</summary>
        public static bool IsCurrentSignificant(CircuitComponent component)
        {
            if (component == null)
                return false;
            return component.GetCurrentValue() > SignificantCurrent;
        }

        /// <summary>
        /// Compute motor RPM from live current. Motor.speed = normalSpeed * (current / baseCurrent).
        /// Defaults: normalSpeed=600, baseCurrent=0.005. Uses reflection for actual field values.
        /// </summary>
        public static float GetMotorRpm(Motor motor)
        {
            if (motor == null)
                return 0f;
            if (!GetIsActive(motor))
                return 0f;
            double current = motor.GetCurrentValue();
            if (current <= SignificantCurrent)
                return 0f;

            ResolveMotorFields();
            float normalSpeed = 600f;
            float baseCurrent = 0.005f;
            if (_motorNormalSpeedField != null)
                normalSpeed = (float)_motorNormalSpeedField.GetValue(motor);
            if (_motorBaseCurrentField != null)
                baseCurrent = (float)_motorBaseCurrentField.GetValue(motor);
            if (baseCurrent <= 0f)
                baseCurrent = 0.005f;

            return normalSpeed * ((float)current / baseCurrent);
        }

        static void ResolveMotorFields()
        {
            if (_motorFieldsResolved)
                return;
            _motorFieldsResolved = true;
            _motorSpeedField = typeof(Motor).GetField("speed", BindingFlags.NonPublic | BindingFlags.Instance);
            _motorNormalSpeedField = typeof(Motor).GetField("normalSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
            _motorBaseCurrentField = typeof(Motor).GetField("baseCurrent", BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    /// <summary>
    /// Singleton MonoBehaviour that self-spawns at runtime via RuntimeInitializeOnLoadMethod.
    /// Polls CircuitLab / InductionCircuit public state at ~5 Hz to evaluate objectives of the
    /// active challenge. Fires events for objective / challenge completion. Manages persistent
    /// progress and exposes mentor context for the AI scientist. Additive â€” does not modify sim scripts.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    public sealed class ChallengeManager : MonoBehaviour
    {
        const float PollInterval = 0.2f;       // 5 Hz
        const float ComponentRefreshInterval = 2f;

        static ChallengeManager _instance;

        public static ChallengeManager Instance => _instance;

        // â”€â”€ Public state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>All challenge definitions in the starter campaign (ordered by progression).</summary>
        public ChallengeDefinition[] Campaign { get; private set; }

        /// <summary>The currently active challenge definition, or null.</summary>
        public ChallengeDefinition ActiveChallenge { get; private set; }

        /// <summary>Runtime-cloned objectives of the active challenge (mutable completion state).</summary>
        public ChallengeObjective[] ActiveObjectives { get; private set; }

        public bool IsChallengeActive => ActiveChallenge != null;

        /// <summary>Elapsed seconds since the active challenge was started.</summary>
        public float ElapsedTime { get; private set; }

        /// <summary>Placed component count at the moment of completion (for star rating).</summary>
        public int LastComponentCount { get; private set; }

        // â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Fires when a single objective is completed. (objective, index)</summary>
        public event Action<ChallengeObjective, int> OnObjectiveCompleted;

        /// <summary>Fires when the active challenge is completed. (definition, stars, componentCount)</summary>
        public event Action<ChallengeDefinition, int, int> OnChallengeCompleted;

        /// <summary>Fires when the active challenge is abandoned.</summary>
        public event Action OnChallengeAbandoned;

        // â”€â”€ Progress â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        ChallengeProgressFile _progress;
        float _pollTimer;
        float _componentRefreshTimer;

        // Cached sim refs (refreshed periodically)
        Bulb[] _bulbs;
        Motor[] _motors;
        Resistor[] _resistors;
        Solar[] _solars;
        InductionCircuit _inductionCircuit;
        bool _refsValid;

        // â”€â”€ Self-spawning â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (!Application.isPlaying)
                return;
            if (_instance != null)
                return;
            // Avoid duplicate if one already exists in-scene
            ChallengeManager existing = FindObjectOfType<ChallengeManager>();
            if (existing != null)
                return;
            var go = new GameObject("ChallengeManager");
            go.AddComponent<ChallengeManager>();
        }

        // Compatibility wrapper â€” FindFirstObjectByType is Unity 2023+,
        // FindObjectOfType is deprecated but still compiles on 6000.x.
        static new T FindObjectOfType<T>() where T : Component
        {
            return UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                return;
            }
            _instance = this;
            Campaign = BuildStarterCampaign();
            _progress = ChallengeProgress.Load();
            ApplyUnlockState();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            if (!IsChallengeActive)
                return;

            ElapsedTime += Time.deltaTime;
            _pollTimer += Time.deltaTime;

            if (_pollTimer >= PollInterval)
            {
                _pollTimer = 0f;
                EvaluateObjectives();
            }
        }

        // â”€â”€ Campaign â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Build the starter campaign of 6 challenges with real, detectable conditions.
        /// Thresholds chosen from what the sims actually expose.
        /// </summary>
        static ChallengeDefinition[] BuildStarterCampaign()
        {
            return new[]
            {
                // 1. First Light â€” close a battery-bulb circuit; bulb lit
                new ChallengeDefinition
                {
                    id = "first_light",
                    title = "First Light",
                    description = "Connect a battery and a bulb with wires to light up the bulb.",
                    mentorHint = "A circuit needs a power source (battery), a load (bulb), and wires to connect them in a closed loop.",
                    prerequisiteId = "",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.BulbLit,
                            displayText = "Light up a bulb"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 60f,
                        threeStarTimeSeconds = 30f,
                        threeStarMaxComponents = 5
                    }
                },

                // 2. Current Control â€” add a resistor; bulb stays lit
                new ChallengeDefinition
                {
                    id = "current_control",
                    title = "Current Control",
                    description = "Add a resistor to your circuit while keeping the bulb lit. The resistor limits current flow.",
                    mentorHint = "A resistor adds resistance to the loop (R_total increases), which reduces current (I = V/R). The bulb still lights if current is significant.",
                    prerequisiteId = "first_light",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.ResistorInCircuit,
                            displayText = "Add a resistor to the circuit"
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.BulbLit,
                            displayText = "Keep the bulb lit with the resistor in place"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 90f,
                        threeStarTimeSeconds = 45f,
                        threeStarMaxComponents = 8
                    }
                },

                // 3. Spin Up â€” motor reaches RPM threshold
                new ChallengeDefinition
                {
                    id = "spin_up",
                    title = "Spin Up",
                    description = "Power a motor with a battery and wires. Get it spinning at 120 RPM or faster.",
                    mentorHint = "Motor RPM scales with current: RPM = 600 * (I / 0.005). More voltage or lower total resistance means higher RPM.",
                    prerequisiteId = "current_control",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MotorRpmThreshold,
                            displayText = "Get a motor spinning at 120+ RPM",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 90f,
                        threeStarTimeSeconds = 45f,
                        threeStarMaxComponents = 6
                    }
                },

                // 4. Double Trouble â€” two bulbs lit simultaneously
                new ChallengeDefinition
                {
                    id = "double_trouble",
                    title = "Double Trouble",
                    description = "Grab two bulbs from the Bulb dispenser and light both at once (series or parallel) with a battery and wires.",
                    mentorHint = "Grab two bulbs (dispenser restocks). Series = one loop through both; parallel = each bulb its own path from the battery.",
                    prerequisiteId = "spin_up",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 2 bulbs simultaneously",
                            targetCount = 2
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 120f,
                        threeStarTimeSeconds = 60f,
                        threeStarMaxComponents = 8
                    }
                },

                // 5. Sun Power â€” solar panel powers a load
                new ChallengeDefinition
                {
                    id = "sun_power",
                    title = "Sun Power",
                    description = "Use a solar panel to power a bulb or motor. Angle the panel toward the sun for maximum wattage.",
                    mentorHint = "Solar panels convert light to electricity. The closer the panel faces the sun, the more voltage and current it produces.",
                    prerequisiteId = "double_trouble",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Power a load using a solar panel"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 120f,
                        threeStarTimeSeconds = 60f,
                        threeStarMaxComponents = 6
                    }
                },

                // 6. Induction â€” induce EMF >= threshold using magnet + coil
                new ChallengeDefinition
                {
                    id = "induction",
                    title = "Induction",
                    description = "Move a magnet through the coil to induce an EMF of 0.05 V or higher (Faraday's law).",
                    mentorHint = "EMF = -N * dPhi/dt. Move the magnet faster or closer to the coil to increase the rate of flux change.",
                    prerequisiteId = "sun_power",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Induce EMF >= 0.05 V",
                            targetValue = 0.05f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 120f,
                        threeStarTimeSeconds = 60f,
                        threeStarMaxComponents = 0
                    }
                }
            };
        }

        // â”€â”€ Progress / unlock logic â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        void ApplyUnlockState()
        {
            if (Campaign == null)
                return;
            foreach (ChallengeDefinition def in Campaign)
            {
                ChallengeEntry entry = ChallengeProgress.GetOrCreate(
                    _progress, def.id, def.IsUnlockedFromStart);

                // If prerequisite is completed, unlock this challenge
                if (!def.IsUnlockedFromStart)
                {
                    ChallengeEntry prereq = FindEntry(def.prerequisiteId);
                    if (prereq != null && prereq.stars > 0)
                        entry.unlocked = true;
                }
            }
            ChallengeProgress.Save(_progress);
        }

        ChallengeEntry FindEntry(string id)
        {
            if (_progress.entries == null)
                return null;
            for (int i = 0; i < _progress.entries.Length; i++)
            {
                if (_progress.entries[i] != null && _progress.entries[i].id == id)
                    return _progress.entries[i];
            }
            return null;
        }

        /// <summary>True if the challenge is unlocked (playable).</summary>
        public bool IsUnlocked(string challengeId)
        {
            ChallengeEntry entry = FindEntry(challengeId);
            return entry != null && entry.unlocked;
        }

        /// <summary>Stars earned for a challenge (0 = not completed, 1-3 = rating).</summary>
        public int GetStars(string challengeId)
        {
            ChallengeEntry entry = FindEntry(challengeId);
            return entry != null ? entry.stars : 0;
        }

        /// <summary>Best completion time for a challenge (0 = none).</summary>
        public float GetBestTime(string challengeId)
        {
            ChallengeEntry entry = FindEntry(challengeId);
            return entry != null ? entry.bestTimeSeconds : 0f;
        }

        /// <summary>Best component count for a challenge (0 = none).</summary>
        public int GetBestComponentCount(string challengeId)
        {
            ChallengeEntry entry = FindEntry(challengeId);
            return entry != null ? entry.bestComponentCount : 0;
        }

        /// <summary>Find a challenge definition by id.</summary>
        public ChallengeDefinition FindChallenge(string id)
        {
            if (Campaign == null)
                return null;
            foreach (ChallengeDefinition def in Campaign)
            {
                if (def.id == id)
                    return def;
            }
            return null;
        }

        // â”€â”€ Challenge lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Start a challenge by id. Returns false if locked or not found.</summary>
        public bool StartChallenge(string id)
        {
            ChallengeDefinition def = FindChallenge(id);
            if (def == null)
                return false;
            if (!IsUnlocked(id))
                return false;

            ActiveChallenge = def;
            ActiveObjectives = def.CloneObjectives();
            ElapsedTime = 0f;
            LastComponentCount = 0;
            _pollTimer = 0f;
            _refsValid = false; // force refresh on first poll
            return true;
        }

        /// <summary>Abandon the active challenge without completing it.</summary>
        public void AbandonChallenge()
        {
            if (!IsChallengeActive)
                return;
            ActiveChallenge = null;
            ActiveObjectives = null;
            ElapsedTime = 0f;
            if (OnChallengeAbandoned != null)
                OnChallengeAbandoned.Invoke();
        }

        /// <summary>
        /// Clears sticky objective completion and elapsed timer for the active challenge
        /// without abandoning it. Used by Experiment RESET so ACTION -> RESET -> CONTINUE retries cleanly.
        /// </summary>
        public void ResetActiveChallengeProgress()
        {
            if (!IsChallengeActive || ActiveChallenge == null)
                return;
            ActiveObjectives = ActiveChallenge.CloneObjectives();
            ElapsedTime = 0f;
            LastComponentCount = 0;
            _pollTimer = 0f;
            _refsValid = false;
        }

        /// <summary>
        /// Titles of challenges unlocked by completing <paramref name="completedId"/> (prerequisite match).
        /// Empty array when none.
        /// </summary>
        public string[] GetNextUnlockTitles(string completedId)
        {
            if (Campaign == null || string.IsNullOrEmpty(completedId))
                return System.Array.Empty<string>();
            var titles = new System.Collections.Generic.List<string>(2);
            for (int i = 0; i < Campaign.Length; i++)
            {
                ChallengeDefinition def = Campaign[i];
                if (def != null && def.prerequisiteId == completedId && !string.IsNullOrEmpty(def.title))
                    titles.Add(def.title);
            }
            return titles.ToArray();
        }

        // â”€â”€ Objective evaluation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        void RefreshSimRefs()
        {
            _bulbs = UnityEngine.Object.FindObjectsByType<Bulb>(FindObjectsInactive.Exclude);
            _motors = UnityEngine.Object.FindObjectsByType<Motor>(FindObjectsInactive.Exclude);
            _resistors = UnityEngine.Object.FindObjectsByType<Resistor>(FindObjectsInactive.Exclude);
            _solars = UnityEngine.Object.FindObjectsByType<Solar>(FindObjectsInactive.Exclude);
            _inductionCircuit = UnityEngine.Object.FindAnyObjectByType<InductionCircuit>(FindObjectsInactive.Exclude);
            _refsValid = true;
        }

        void EvaluateObjectives()
        {
            if (ActiveChallenge == null || ActiveObjectives == null)
                return;

            // Periodically refresh cached sim references
            _componentRefreshTimer += PollInterval;
            if (!_refsValid || _componentRefreshTimer >= ComponentRefreshInterval)
            {
                _componentRefreshTimer = 0f;
                RefreshSimRefs();
            }

            bool allComplete = true;
            for (int i = 0; i < ActiveObjectives.Length; i++)
            {
                ChallengeObjective obj = ActiveObjectives[i];
                if (obj.completed)
                    continue;

                bool met = EvaluateObjective(obj);
                if (met)
                {
                    obj.completed = true;
                    if (OnObjectiveCompleted != null)
                        OnObjectiveCompleted.Invoke(obj, i);
                }
                else
                {
                    allComplete = false;
                }
            }

            if (allComplete && ActiveObjectives.Length > 0)
                CompleteChallenge();
        }

        bool EvaluateObjective(ChallengeObjective obj)
        {
            switch (obj.type)
            {
                case ObjectiveType.BulbLit:
                    return CountActiveBulbs() >= 1;

                case ObjectiveType.ResistorInCircuit:
                    return CountActiveResistors() >= 1 && CountActiveBulbs() >= 1;

                case ObjectiveType.MotorRpmThreshold:
                    return GetMaxMotorRpm() >= obj.targetValue;

                case ObjectiveType.MultipleBulbsLit:
                    return CountActiveBulbs() >= obj.targetCount;

                case ObjectiveType.SolarPoweringLoad:
                    return IsSolarPoweringLoad();

                case ObjectiveType.InducedEmfThreshold:
                    return GetInducedEmf() >= obj.targetValue;

                default:
                    return false;
            }
        }

        int CountActiveBulbs()
        {
            if (_bulbs == null)
                return 0;
            int count = 0;
            for (int i = 0; i < _bulbs.Length; i++)
            {
                Bulb bulb = _bulbs[i];
                if (bulb == null || !bulb.IsPlaced || !bulb.IsClone)
                    continue;
                if (CircuitReader.GetIsActive(bulb) && CircuitReader.IsCurrentSignificant(bulb))
                    count++;
            }
            return count;
        }

        int CountActiveResistors()
        {
            if (_resistors == null)
                return 0;
            int count = 0;
            for (int i = 0; i < _resistors.Length; i++)
            {
                Resistor r = _resistors[i];
                if (r == null || !r.IsPlaced || !r.IsClone)
                    continue;
                if (CircuitReader.GetIsActive(r) && CircuitReader.IsCurrentSignificant(r))
                    count++;
            }
            return count;
        }

        float GetMaxMotorRpm()
        {
            if (_motors == null)
                return 0f;
            float maxRpm = 0f;
            for (int i = 0; i < _motors.Length; i++)
            {
                Motor motor = _motors[i];
                if (motor == null || !motor.IsPlaced || !motor.IsClone)
                    continue;
                float rpm = CircuitReader.GetMotorRpm(motor);
                if (rpm > maxRpm)
                    maxRpm = rpm;
            }
            return maxRpm;
        }

        bool IsSolarPoweringLoad()
        {
            if (_solars == null)
                return false;
            bool solarActive = false;
            for (int i = 0; i < _solars.Length; i++)
            {
                Solar solar = _solars[i];
                if (solar == null || !solar.IsPlaced || !solar.IsClone)
                    continue;
                if (CircuitReader.GetIsActive(solar) && CircuitReader.IsCurrentSignificant(solar))
                {
                    solarActive = true;
                    break;
                }
            }
            if (!solarActive)
                return false;

            // Check that a load (bulb or motor) is also active
            if (CountActiveBulbs() >= 1)
                return true;
            if (_motors != null)
            {
                for (int i = 0; i < _motors.Length; i++)
                {
                    Motor motor = _motors[i];
                    if (motor == null || !motor.IsPlaced || !motor.IsClone)
                        continue;
                    if (CircuitReader.GetIsActive(motor) && CircuitReader.IsCurrentSignificant(motor))
                        return true;
                }
            }
            return false;
        }

        float GetInducedEmf()
        {
            if (_inductionCircuit == null)
                return 0f;
            return Mathf.Abs(_inductionCircuit.EmfVolts);
        }

        /// <summary>
        /// Live measured progress for the objective overlay (ACTION → MEASUREMENT → FEEDBACK).
        /// Uses the same CircuitLab / InductionCircuit readers as objective evaluation.
        /// </summary>
        public string GetObjectiveLiveReadout(ChallengeObjective obj)
        {
            if (obj == null)
                return string.Empty;

            if (!_refsValid)
                RefreshSimRefs();

            switch (obj.type)
            {
                case ObjectiveType.BulbLit:
                {
                    int lit = CountActiveBulbs();
                    return lit.ToString() + "/1 lit";
                }
                case ObjectiveType.MultipleBulbsLit:
                {
                    int lit = CountActiveBulbs();
                    int need = obj.targetCount > 0 ? obj.targetCount : 1;
                    return lit.ToString() + "/" + need.ToString() + " lit";
                }
                case ObjectiveType.ResistorInCircuit:
                {
                    int r = CountActiveResistors();
                    int b = CountActiveBulbs();
                    return "R=" + r.ToString() + " lit=" + b.ToString();
                }
                case ObjectiveType.MotorRpmThreshold:
                {
                    float rpm = GetMaxMotorRpm();
                    return rpm.ToString("0") + "/" + obj.targetValue.ToString("0") + " RPM";
                }
                case ObjectiveType.SolarPoweringLoad:
                    return IsSolarPoweringLoad() ? "load powered" : "awaiting solar+load";
                case ObjectiveType.InducedEmfThreshold:
                {
                    float emf = GetInducedEmf();
                    return emf.ToString("0.000") + "/" + obj.targetValue.ToString("0.00") + " V";
                }
                default:
                    return string.Empty;
            }
        }

        int CountPlacedComponents()
        {
            var all = UnityEngine.Object.FindObjectsByType<CircuitComponent>(
                FindObjectsInactive.Exclude);
            if (all == null)
                return 0;
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].IsPlaced && all[i].IsClone)
                    count++;
            }
            return count;
        }

        void CompleteChallenge()
        {
            ChallengeDefinition def = ActiveChallenge;
            float time = ElapsedTime;
            int componentCount = CountPlacedComponents();
            LastComponentCount = componentCount;

            // Calculate stars
            int stars = CalculateStars(def, time, componentCount);

            // Save progress
            ChallengeEntry entry = ChallengeProgress.GetOrCreate(_progress, def.id, true);
            ChallengeProgress.RecordResult(entry, stars, time, componentCount);

            // Unlock the next challenge(s)
            UnlockNextChallenges(def.id);

            ChallengeProgress.Save(_progress);

            // Fire event before clearing state
            if (OnChallengeCompleted != null)
                OnChallengeCompleted.Invoke(def, stars, componentCount);

            // Clear active state
            ActiveChallenge = null;
            ActiveObjectives = null;
            ElapsedTime = 0f;
        }

        int CalculateStars(ChallengeDefinition def, float time, int componentCount)
        {
            StarThresholds t = def.starThresholds;
            if (t == null)
                return 1;

            // 1 star: completed
            int stars = 1;

            // 2 stars: within twoStarTimeSeconds
            if (time <= t.twoStarTimeSeconds)
                stars = 2;

            // 3 stars: within threeStarTimeSeconds AND component count constraint
            bool timeOk = time <= t.threeStarTimeSeconds;
            bool componentOk = t.threeStarMaxComponents <= 0 || componentCount <= t.threeStarMaxComponents;
            if (timeOk && componentOk)
                stars = 3;

            return stars;
        }

        void UnlockNextChallenges(string completedId)
        {
            if (Campaign == null)
                return;
            foreach (ChallengeDefinition def in Campaign)
            {
                if (def.prerequisiteId == completedId)
                {
                    ChallengeEntry entry = ChallengeProgress.GetOrCreate(_progress, def.id, false);
                    entry.unlocked = true;
                }
            }
        }

        // â”€â”€ Mentor context â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Short context string for the AI scientist: challenge title, objective states,
        /// and completion progress. Empty when no challenge is active. Graceful no-op when
        /// the scientist / LLM is unavailable.
        /// </summary>
        public string ChallengeContext
        {
            get
            {
                if (!IsChallengeActive || ActiveChallenge == null)
                    return string.Empty;

                var sb = new System.Text.StringBuilder(256);
                sb.Append("[Challenge: ").Append(ActiveChallenge.title).Append("] ");
                if (ActiveObjectives != null)
                {
                    int done = 0;
                    for (int i = 0; i < ActiveObjectives.Length; i++)
                    {
                        if (ActiveObjectives[i].completed)
                            done++;
                    }
                    sb.Append(done).Append('/').Append(ActiveObjectives.Length).Append(" objectives. ");
                    for (int i = 0; i < ActiveObjectives.Length; i++)
                    {
                        ChallengeObjective obj = ActiveObjectives[i];
                        sb.Append(obj.completed ? "[DONE] " : "[TODO] ");
                        sb.Append(obj.displayText);
                        if (i < ActiveObjectives.Length - 1)
                            sb.Append("; ");
                    }
                }
                if (!string.IsNullOrEmpty(ActiveChallenge.mentorHint))
                    sb.Append(" Hint: ").Append(ActiveChallenge.mentorHint);
                AppendNearestStationContext(sb);
                return sb.ToString();
            }
        }

        /// <summary>
        /// Appends "Near station: {displayName}" to the context StringBuilder when a
        /// LabStationHub is available and the player's camera is near a station.
        /// Graceful no-op when the hub is absent or no stations are registered.
        /// </summary>
        void AppendNearestStationContext(System.Text.StringBuilder sb)
        {
            LabStationHub hub = LabStationHub.Instance;
            if (hub == null)
                return;
            if (hub.Stations.Count == 0)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            LabStation nearest = hub.NearestStation(cam.transform.position);
            if (nearest == null)
                return;
            sb.Append(" Near station: ").Append(nearest.DisplayName);
        }
    }
}


