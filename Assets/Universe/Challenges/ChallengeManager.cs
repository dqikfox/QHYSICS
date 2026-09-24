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
    /// progress and exposes mentor context for the AI scientist. Additive Ã¢â‚¬â€ does not modify sim scripts.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    public sealed class ChallengeManager : MonoBehaviour
    {
        const float PollInterval = 0.2f;       // 5 Hz
        const float ComponentRefreshInterval = 2f;

        static ChallengeManager _instance;

        public static ChallengeManager Instance => _instance;

        // Ã¢â€â‚¬Ã¢â€â‚¬ Public state Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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

        // Ã¢â€â‚¬Ã¢â€â‚¬ Events Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

        /// <summary>Fires when a single objective is completed. (objective, index)</summary>
        public event Action<ChallengeObjective, int> OnObjectiveCompleted;

        /// <summary>Fires when the active challenge is completed. (definition, stars, componentCount)</summary>
        public event Action<ChallengeDefinition, int, int> OnChallengeCompleted;

        /// <summary>Fires when the active challenge is abandoned.</summary>
        public event Action OnChallengeAbandoned;

        // Ã¢â€â‚¬Ã¢â€â‚¬ Progress Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

        ChallengeProgressFile _progress;
        float _pollTimer;
        float _componentRefreshTimer;

        // Cached sim refs (refreshed periodically)
        Bulb[] _bulbs;
        Motor[] _motors;
        Resistor[] _resistors;
        Solar[] _solars;
        Switch[] _switches;
        InductionCircuit _inductionCircuit;
        bool _refsValid;

        /// <summary>Peak |EMF| seen while the active challenge runs (latched; cleared on start/RESET).</summary>
        float _peakInducedEmf;

        // Ã¢â€â‚¬Ã¢â€â‚¬ Self-spawning Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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

        // Compatibility wrapper Ã¢â‚¬â€ FindFirstObjectByType is Unity 2023+,
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
            // Sample every frame so brief Faraday spikes are not missed by the 5 Hz poll.
            SamplePeakInducedEmf();
            _pollTimer += Time.deltaTime;

            if (_pollTimer >= PollInterval)
            {
                _pollTimer = 0f;
                EvaluateObjectives();
            }
        }

        // Ã¢â€â‚¬Ã¢â€â‚¬ Campaign Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

        /// <summary>
        /// Build the starter campaign of 33 challenges with real, detectable conditions.
        /// Thresholds chosen from what the sims actually expose.
        /// </summary>
        static ChallengeDefinition[] BuildStarterCampaign()
        {
            return new[]
            {
                // 1. First Light Ã¢â‚¬â€ close a battery-bulb circuit; bulb lit
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

                // 2. Current Control Ã¢â‚¬â€ add a resistor; bulb stays lit
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

                // 3. Spin Up Ã¢â‚¬â€ motor reaches RPM threshold
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

                // 4. Double Trouble Ã¢â‚¬â€ two bulbs lit simultaneously
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

                // 5. Sun Power Ã¢â‚¬â€ solar panel powers a load
                new ChallengeDefinition
                {
                    id = "sun_power",
                    title = "Sun Power",
                    description = "Grab a Solar from Dispenser11, wire it to a bulb or motor (no battery). Rotate the panel toward the MiniatureSun until wattage rises and the load runs.",
                    mentorHint = "Desktop: hold Solar + LMB/scroll to rotate. VR: pinch the panel. Face the sun (live W meter); close a Solar+Wire+Bulb/Motor loop - battery not required.",
                    prerequisiteId = "double_trouble",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 120f,
                        threeStarTimeSeconds = 60f,
                        threeStarMaxComponents = 6
                    }
                },

                // 6. Induction Ã¢â‚¬â€ induce EMF >= threshold using magnet + coil
                new ChallengeDefinition
                {
                    id = "induction",
                    title = "Induction",
                    description = "At the Induction Lab coil station, grab the bar magnet and thrust it through the coil until peak |EMF| >= 0.05 V (Faraday's law).",
                    mentorHint = "Desktop: hold Magnet + LMB/scroll to impulse along N-S. VR: grab and throw through the coil. EMF = -N dPhi/dt; faster pass = higher peak (live pk meter).",
                    prerequisiteId = "sun_power",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 120f,
                        threeStarTimeSeconds = 60f,
                        threeStarMaxComponents = 0
                    }
                },

                // 7. Make and Break - close a knife switch to light a bulb
                new ChallengeDefinition
                {
                    id = "make_and_break",
                    title = "Make and Break",
                    description = "Build a Battery + Wire(s) + Switch + Bulb loop on the breadboard, then close the knife switch so the bulb lights.",
                    mentorHint = "Close the knife switch to complete the loop; open = no current. Grab Switch from Dispenser3 (Switch shelf).",
                    prerequisiteId = "induction",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedBulbLit,
                            displayText = "Close the switch and light the bulb"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 90f,
                        threeStarTimeSeconds = 45f,
                        threeStarMaxComponents = 8
                    }
                },

                // 8. Gear Up - close switch to spin a motor
                new ChallengeDefinition
                {
                    id = "gear_up",
                    title = "Gear Up",
                    description = "Build a Battery + Wire(s) + Switch + Motor loop on the breadboard, close the knife switch, and spin the motor to 120+ RPM.",
                    mentorHint = "Same knife-switch skill as Make and Break, but the load is a Motor (Dispenser2). Close the switch to complete the loop; open = no spin. Overlay shows sw + RPM.",
                    prerequisiteId = "make_and_break",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedMotorSpinning,
                            displayText = "Close the switch and spin a motor to 120+ RPM",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 90f,
                        threeStarTimeSeconds = 45f,
                        threeStarMaxComponents = 8
                    }
                },

                // 9. Light and Spin - bulb lit AND motor spinning (dual load)
                new ChallengeDefinition
                {
                    id = "light_and_spin",
                    title = "Light and Spin",
                    description = "Power a bulb and a motor from the same breadboard build (series or parallel). Light the bulb and spin the motor to 120+ RPM at the same time.",
                    mentorHint = "Grab Bulb (Bulb shelf) and Motor (Dispenser2) plus Battery and Wire(s). Parallel = each load its own path; series = one loop through both. Overlay shows lit + RPM; RESET retries cleanly.",
                    prerequisiteId = "gear_up",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.BulbLit,
                            displayText = "Light up a bulb"
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MotorRpmThreshold,
                            displayText = "Get a motor spinning at 120+ RPM",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 120f,
                        threeStarTimeSeconds = 60f,
                        threeStarMaxComponents = 10
                    }
                },
                // 10. Power Play - lit bulb delivering measurable load power
                new ChallengeDefinition
                {
                    id = "power_play",
                    title = "Power Play",
                    description = "Light a bulb and deliver at least 0.05 W of electrical power to a load (bulb or motor). Power P = |V| * |I| from CircuitLab.",
                    mentorHint = "Battery + Wire(s) + Bulb closes a loop (~0.1 W on a 10 V / 1 kOhm bulb). Overlay shows live watts; add Motor for more loads. RESET retries cleanly.",
                    prerequisiteId = "light_and_spin",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.BulbLit,
                            displayText = "Light up a bulb"
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.CircuitPowerThreshold,
                            displayText = "Deliver >= 0.05 W to a load (bulb or motor)",
                            targetValue = 0.05f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 90f,
                        threeStarTimeSeconds = 45f,
                        threeStarMaxComponents = 8
                    }
                },

                // 11. Three Lights - three bulbs lit simultaneously
                new ChallengeDefinition
                {
                    id = "three_lights",
                    title = "Three Lights",
                    description = "Grab three bulbs from the Bulb dispenser and light all three at once (series or parallel) with a battery and wires.",
                    mentorHint = "Dispenser restocks after each grab. Parallel keeps bulbs bright; series splits voltage. Overlay shows N/3 lit. RESET retries cleanly.",
                    prerequisiteId = "power_play",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 3 bulbs simultaneously",
                            targetCount = 3
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 150f,
                        threeStarTimeSeconds = 75f,
                        threeStarMaxComponents = 12
                    }
                },

                // 12. Throttle Up - resistor in circuit (with lit bulb) AND motor spinning
                new ChallengeDefinition
                {
                    id = "throttle_up",
                    title = "Throttle Up",
                    description = "Limit current with a resistor while powering a lit bulb and spinning a motor to 120+ RPM at the same time.",
                    mentorHint = "Grab Resistor (Resistor shelf), Bulb, Motor (Dispenser2), Battery, and Wire(s). Series resistor throttles current to both loads. Overlay shows R/lit + RPM. RESET retries cleanly.",
                    prerequisiteId = "three_lights",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.ResistorInCircuit,
                            displayText = "Add a resistor while keeping a bulb lit"
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MotorRpmThreshold,
                            displayText = "Get a motor spinning at 120+ RPM",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 150f,
                        threeStarTimeSeconds = 75f,
                        threeStarMaxComponents = 12
                    }
                },

                // 13. Sun Drive - solar powers a load AND motor spins (no battery needed)
                new ChallengeDefinition
                {
                    id = "sun_drive",
                    title = "Sun Drive",
                    description = "Power a motor from the solar panel alone: face the sun until wattage rises and spin the motor to 120+ RPM â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Motor (Dispenser2), Wire(s). No battery. Rotate Solar toward MiniatureSun (LMB/scroll). Overlay shows solar W/load + RPM. RESET retries cleanly.",
                    prerequisiteId = "throttle_up",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MotorRpmThreshold,
                            displayText = "Get a motor spinning at 120+ RPM",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 150f,
                        threeStarTimeSeconds = 75f,
                        threeStarMaxComponents = 10
                    }
                },

                // 14. Sun Lab - solar powers BOTH a lit bulb and a spinning motor (multi-load renewable)
                new ChallengeDefinition
                {
                    id = "sun_lab",
                    title = "Sun Lab",
                    description = "Power a lit bulb and a spinning motor from the solar panel alone â€” multi-load from one renewable source, no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Bulb, Motor (Dispenser2), Wire(s). No battery. Parallel loads share the panel; face MiniatureSun. Overlay shows solar W/load + lit + RPM. RESET retries cleanly.",
                    prerequisiteId = "sun_drive",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.BulbLit,
                            displayText = "Light up a bulb from the solar panel"
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MotorRpmThreshold,
                            displayText = "Get a motor spinning at 120+ RPM",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 12
                    }
                },

                // 15. Sun Gate - solar + knife switch gates a lit bulb (renewable source + control)
                new ChallengeDefinition
                {
                    id = "sun_gate",
                    title = "Sun Gate",
                    description = "Gate solar power with a knife switch: face the sun, close the switch, and light a bulb from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Switch (Dispenser3), Bulb, Wire(s). No battery. Open switch first (dark), face MiniatureSun, then close switch so bulb lights. Overlay shows solar W/load + sw/lit. RESET retries cleanly.",
                    prerequisiteId = "sun_lab",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedBulbLit,
                            displayText = "Close a switch and light a bulb from the solar panel"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 12
                    }
                },

                // 16. Sun Switch Drive - solar + knife switch gates a spinning motor (renewable + control)
                new ChallengeDefinition
                {
                    id = "sun_switch_drive",
                    title = "Sun Switch Drive",
                    description = "Gate solar power with a knife switch: face the sun, close the switch, and spin a motor to 120+ RPM from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Switch (Dispenser3), Motor (Dispenser2), Wire(s). No battery. Open switch first (no spin), face MiniatureSun, then close switch so motor spins. Overlay shows solar W/load + sw/RPM. RESET retries cleanly.",
                    prerequisiteId = "sun_gate",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedMotorSpinning,
                            displayText = "Close a switch and spin a motor to 120+ RPM from the solar panel",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 12
                    }
                },

                // 17. Sun Switch Lab - solar + knife switch gates BOTH lit bulb and spinning motor
                new ChallengeDefinition
                {
                    id = "sun_switch_lab",
                    title = "Sun Switch Lab",
                    description = "Gate solar power with a knife switch: face the sun, close the switch, and light a bulb while spinning a motor to 120+ RPM from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Switch (Dispenser3), Bulb, Motor (Dispenser2), Wire(s). No battery. Open switch first (dark/still), face MiniatureSun, then close switch so bulb lights and motor spins. Overlay shows solar W/load + sw/lit + RPM. RESET retries cleanly.",
                    prerequisiteId = "sun_switch_drive",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedBulbLit,
                            displayText = "Close a switch and light a bulb from the solar panel"
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedMotorSpinning,
                            displayText = "Close a switch and spin a motor to 120+ RPM from the solar panel",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 14
                    }
                },

                // 18. Sun Twin Gate - solar + knife switch gates TWO lit bulbs (series/parallel multi-bulb renewable)
                new ChallengeDefinition
                {
                    id = "sun_twin_gate",
                    title = "Sun Twin Gate",
                    description = "Gate solar power with a knife switch and light two bulbs at once from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Switch (Dispenser3), two Bulbs, Wire(s). No battery. Open switch first (both dark), face MiniatureSun, then close switch so both bulbs light. Parallel keeps them bright; series splits voltage. Overlay shows solar W/load + N/2 lit + sw/lit. RESET retries cleanly.",
                    prerequisiteId = "sun_switch_lab",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 2 bulbs from the solar panel simultaneously",
                            targetCount = 2
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedBulbLit,
                            displayText = "Close a switch and light a bulb from the solar panel"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 14
                    }
                },

                // 19. Sun Twin Lab - solar + knife switch gates TWO lit bulbs AND a spinning motor
                new ChallengeDefinition
                {
                    id = "sun_twin_lab",
                    title = "Sun Twin Lab",
                    description = "Gate solar power with a knife switch: face the sun, close the switch, light two bulbs at once, and spin a motor to 120+ RPM from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Switch (Dispenser3), two Bulbs, Motor (Dispenser2), Wire(s). No battery. Open switch first (dark/still), face MiniatureSun, then close switch so both bulbs light and motor spins. Overlay shows solar W/load + N/2 lit + sw/RPM. RESET retries cleanly.",
                    prerequisiteId = "sun_twin_gate",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 2 bulbs from the solar panel simultaneously",
                            targetCount = 2
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedMotorSpinning,
                            displayText = "Close a switch and spin a motor to 120+ RPM from the solar panel",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 20. Sun Throttle - solar + resistor (with lit bulb) + knife-switch gated motor (mirrors Throttle Up on renewable)
                new ChallengeDefinition
                {
                    id = "sun_throttle",
                    title = "Sun Throttle",
                    description = "Throttle solar current with a resistor while keeping a bulb lit, and gate a motor to 120+ RPM with a knife switch â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Resistor (Resistor shelf), Switch (Dispenser3), Bulb, Motor (Dispenser2), Wire(s). No battery. Series resistor throttles current; face MiniatureSun; close switch so motor spins. Overlay shows solar W/load + R/lit + sw/RPM. RESET retries cleanly.",
                    prerequisiteId = "sun_twin_lab",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.ResistorInCircuit,
                            displayText = "Add a resistor while keeping a bulb lit from the solar panel"
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedMotorSpinning,
                            displayText = "Close a switch and spin a motor to 120+ RPM from the solar panel",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 21. Sun Power Lab - solar + measurable load power P=|V|*|I| + knife-switch gated motor
                new ChallengeDefinition
                {
                    id = "sun_power_lab",
                    title = "Sun Power Lab",
                    description = "Face the sun until the panel powers a load, deliver at least 0.05 W of measurable load power (P = |V| * |I|), and gate a motor to 120+ RPM with a knife switch â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Switch (Dispenser3), Bulb and/or Motor (Dispenser2), Wire(s). No battery. Face MiniatureSun; overlay shows solar W/load + circuit W/lit + sw/RPM. Close switch so motor spins. RESET retries cleanly.",
                    prerequisiteId = "sun_throttle",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.CircuitPowerThreshold,
                            displayText = "Deliver >= 0.05 W to a load (bulb or motor) from the solar panel",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedMotorSpinning,
                            displayText = "Close a switch and spin a motor to 120+ RPM from the solar panel",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 22. Sun Induction - solar load + Faraday peak EMF + knife-switch gated motor
                new ChallengeDefinition
                {
                    id = "sun_induction",
                    title = "Sun Induction",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil (thrust the magnet through), and gate a motor to 120+ RPM with a knife switch â€” no battery.",
                    mentorHint = "Solar circuit: Dispenser11 Solar + Dispenser3 Switch + Bulb/Motor + Wire(s), face MiniatureSun. Induction Lab: grab bar magnet, thrust through coil for peak EMF. Overlay shows solar W/load + pk EMF + sw/RPM. RESET retries cleanly.",
                    prerequisiteId = "sun_power_lab",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedMotorSpinning,
                            displayText = "Close a switch and spin a motor to 120+ RPM from the solar panel",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 23. Sun Faraday Lab - solar load + Faraday peak EMF + measurable circuit power P=|V|*|I|
                new ChallengeDefinition
                {
                    id = "sun_faraday_lab",
                    title = "Sun Faraday Lab",
                    description = "Keep a solar panel powering a load with measurable P=|V|*|I| >= 0.05 W, and induce peak |EMF| >= 0.05 V at the Faraday coil â€” no battery.",
                    mentorHint = "Solar (Dispenser11) + Bulb/Motor + Wire(s), face MiniatureSun; Induction Lab magnet thrust for peak EMF; overlay solar W/load + pk EMF + circuit W/lit; RESET retries cleanly.",
                    prerequisiteId = "sun_induction",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.CircuitPowerThreshold,
                            displayText = "Deliver >= 0.05 W to a load (bulb or motor) from the solar panel",
                            targetValue = 0.05f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 24. Sun Faraday Drive - solar load + Faraday peak EMF + series resistor throttle (Solar+EMF+SwitchMotor is sun_induction)
                new ChallengeDefinition
                {
                    id = "sun_faraday_drive",
                    title = "Sun Faraday Drive",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and throttle the renewable loop with a series resistor while keeping a bulb lit â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Resistor (Resistor shelf), Bulb, Wire(s). No battery. Face MiniatureSun; series resistor throttles current with bulb lit; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + R/lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_lab",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.ResistorInCircuit,
                            displayText = "Add a resistor while keeping a bulb lit from the solar panel"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 25. Sun Faraday Gate - solar load + Faraday peak EMF + knife-switch gated lit bulb
                // NOTE: Solar+EMF+SwitchMotor already exists as sun_induction (ch22); Solar+EMF+Resistor is sun_faraday_drive (ch24).
                new ChallengeDefinition
                {
                    id = "sun_faraday_gate",
                    title = "Sun Faraday Gate",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and gate a lit bulb with a knife switch â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Switch (Dispenser3), Bulb, Wire(s). No battery. Face MiniatureSun; open switch first (dark), then close so bulb lights; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + sw/lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_drive",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SwitchClosedBulbLit,
                            displayText = "Close a switch and light a bulb from the solar panel"
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 26. Sun Faraday Twin - solar load + Faraday peak EMF + two bulbs lit (Solar+EMF+SwitchBulb is sun_faraday_gate ch25; Solar+EMF+SwitchMotor is sun_induction ch22)
                new ChallengeDefinition
                {
                    id = "sun_faraday_twin",
                    title = "Sun Faraday Twin",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and light two bulbs at once from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), two Bulbs, Wire(s). No battery. Face MiniatureSun; parallel keeps both bright; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + N/2 lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_gate",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 2 bulbs from the solar panel simultaneously",
                            targetCount = 2
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },

                // 27. Sun Faraday Trio - solar load + Faraday peak EMF + three bulbs lit
                // NOTE: Solar+EMF+MultipleBulbsLit(2) is sun_faraday_twin ch26; Solar+EMF+SwitchMotor is sun_induction ch22;
                // Solar+EMF+SwitchBulb is sun_faraday_gate ch25; Solar+EMF+Resistor is sun_faraday_drive ch24;
                // Solar+EMF+CircuitPower is sun_faraday_lab ch23.
                new ChallengeDefinition
                {
                    id = "sun_faraday_trio",
                    title = "Sun Faraday Trio",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and light three bulbs at once from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), three Bulbs, Wire(s). No battery. Face MiniatureSun; parallel keeps all three bright; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + N/3 lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_twin",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 3 bulbs from the solar panel simultaneously",
                            targetCount = 3
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 18
                    }
                },

                // 28. Sun Faraday Spin - solar load + Faraday peak EMF + motor RPM (no switch gate)
                // NOTE: Solar+EMF+SwitchMotor is sun_induction ch22; Solar+EMF+MultipleBulbsLit(3) is sun_faraday_trio ch27;
                // Solar+EMF+MultipleBulbsLit(2) is sun_faraday_twin ch26; Solar+EMF+SwitchBulb is sun_faraday_gate ch25;
                // Solar+EMF+Resistor is sun_faraday_drive ch24; Solar+EMF+CircuitPower is sun_faraday_lab ch23;
                // Solar+MotorRpm (no EMF) is sun_drive ch13.
                new ChallengeDefinition
                {
                    id = "sun_faraday_spin",
                    title = "Sun Faraday Spin",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and spin a motor to 120+ RPM from the panel alone - no battery.",
                    mentorHint = "Grab Solar (Dispenser11), Motor (Dispenser2), Wire(s). No battery. Face MiniatureSun until motor spins; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + RPM. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_trio",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MotorRpmThreshold,
                            displayText = "Get a motor spinning at 120+ RPM",
                            targetValue = 120f
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 16
                    }
                },
                // 29. Sun Faraday Quad - solar load + Faraday peak EMF + four bulbs lit
                // NOTE: Solar+EMF+MultipleBulbsLit(3) is sun_faraday_trio ch27; Solar+EMF+MultipleBulbsLit(2) is sun_faraday_twin ch26;
                // Solar+EMF+MotorRpm is sun_faraday_spin ch28; Solar+EMF+SwitchMotor is sun_induction ch22;
                // Solar+EMF+SwitchBulb is sun_faraday_gate ch25; Solar+EMF+Resistor is sun_faraday_drive ch24;
                // Solar+EMF+CircuitPower is sun_faraday_lab ch23.
                new ChallengeDefinition
                {
                    id = "sun_faraday_quad",
                    title = "Sun Faraday Quad",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and light four bulbs at once from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), four Bulbs, Wire(s). No battery. Face MiniatureSun; parallel keeps all four bright; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + N/4 lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_spin",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 4 bulbs from the solar panel simultaneously",
                            targetCount = 4
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 20
                    }
                },
                // 30. Sun Faraday Penta - solar load + Faraday peak EMF + five bulbs lit
                // NOTE: Solar+EMF+MultipleBulbsLit(4) is sun_faraday_quad ch29; Solar+EMF+MultipleBulbsLit(3) is sun_faraday_trio ch27;
                // Solar+EMF+MultipleBulbsLit(2) is sun_faraday_twin ch26; Solar+EMF+MotorRpm is sun_faraday_spin ch28;
                // Solar+EMF+SwitchMotor is sun_induction ch22; Solar+EMF+SwitchBulb is sun_faraday_gate ch25;
                // Solar+EMF+Resistor is sun_faraday_drive ch24; Solar+EMF+CircuitPower is sun_faraday_lab ch23.
                new ChallengeDefinition
                {
                    id = "sun_faraday_penta",
                    title = "Sun Faraday Penta",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and light five bulbs at once from the panel alone â€” no battery.",
                    mentorHint = "Grab Solar (Dispenser11), five Bulbs, Wire(s). No battery. Face MiniatureSun; parallel keeps all five bright; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + N/5 lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_quad",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 5 bulbs from the solar panel simultaneously",
                            targetCount = 5
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 180f,
                        threeStarTimeSeconds = 90f,
                        threeStarMaxComponents = 24
                    }
                },
                // 31. Sun Faraday Hex - solar load + Faraday peak EMF + six bulbs lit
                // NOTE: Solar+EMF+MultipleBulbsLit(5) is sun_faraday_penta ch30; Solar+EMF+MultipleBulbsLit(4) is sun_faraday_quad ch29;
                // Solar+EMF+MultipleBulbsLit(3) is sun_faraday_trio ch27; Solar+EMF+MultipleBulbsLit(2) is sun_faraday_twin ch26;
                // Solar+EMF+MotorRpm is sun_faraday_spin ch28; Solar+EMF+SwitchMotor is sun_induction ch22;
                // Solar+EMF+SwitchBulb is sun_faraday_gate ch25; Solar+EMF+Resistor is sun_faraday_drive ch24;
                // Solar+EMF+CircuitPower is sun_faraday_lab ch23.
                new ChallengeDefinition
                {
                    id = "sun_faraday_hex",
                    title = "Sun Faraday Hex",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and light six bulbs at once from the panel alone — no battery.",
                    mentorHint = "Grab Solar (Dispenser11), six Bulbs, Wire(s). No battery. Face MiniatureSun; parallel keeps all six bright; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + N/6 lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_penta",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 6 bulbs from the solar panel simultaneously",
                            targetCount = 6
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 200f,
                        threeStarTimeSeconds = 100f,
                        threeStarMaxComponents = 28
                    }
                },
                // 32. Sun Faraday Hept - solar load + Faraday peak EMF + seven bulbs lit
                // NOTE: Solar+EMF+MultipleBulbsLit(6) is sun_faraday_hex ch31; Solar+EMF+MultipleBulbsLit(5) is sun_faraday_penta ch30;
                // Solar+EMF+MultipleBulbsLit(4) is sun_faraday_quad ch29; Solar+EMF+MultipleBulbsLit(3) is sun_faraday_trio ch27;
                // Solar+EMF+MultipleBulbsLit(2) is sun_faraday_twin ch26; Solar+EMF+MotorRpm is sun_faraday_spin ch28;
                // Solar+EMF+SwitchMotor is sun_induction ch22; Solar+EMF+SwitchBulb is sun_faraday_gate ch25;
                // Solar+EMF+Resistor is sun_faraday_drive ch24; Solar+EMF+CircuitPower is sun_faraday_lab ch23.
                new ChallengeDefinition
                {
                    id = "sun_faraday_hept",
                    title = "Sun Faraday Hept",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and light seven bulbs at once from the panel alone — no battery.",
                    mentorHint = "Grab Solar (Dispenser11), seven Bulbs, Wire(s). No battery. Face MiniatureSun; parallel keeps all seven bright; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + N/7 lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_hex",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 7 bulbs from the solar panel simultaneously",
                            targetCount = 7
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 220f,
                        threeStarTimeSeconds = 110f,
                        threeStarMaxComponents = 32
                    }
                },
                // 33. Sun Faraday Oct - solar load + Faraday peak EMF + eight bulbs lit
                // NOTE: Solar+EMF+MultipleBulbsLit(7) is sun_faraday_hept ch32; Solar+EMF+MultipleBulbsLit(6) is sun_faraday_hex ch31;
                // Solar+EMF+MultipleBulbsLit(5) is sun_faraday_penta ch30; Solar+EMF+MultipleBulbsLit(4) is sun_faraday_quad ch29;
                // Solar+EMF+MultipleBulbsLit(3) is sun_faraday_trio ch27; Solar+EMF+MultipleBulbsLit(2) is sun_faraday_twin ch26;
                // Solar+EMF+MotorRpm is sun_faraday_spin ch28; Solar+EMF+SwitchMotor is sun_induction ch22;
                // Solar+EMF+SwitchBulb is sun_faraday_gate ch25; Solar+EMF+Resistor is sun_faraday_drive ch24;
                // Solar+EMF+CircuitPower is sun_faraday_lab ch23.
                new ChallengeDefinition
                {
                    id = "sun_faraday_oct",
                    title = "Sun Faraday Oct",
                    description = "Keep a solar panel powering a load, induce peak |EMF| >= 0.05 V at the Faraday coil, and light eight bulbs at once from the panel alone - no battery.",
                    mentorHint = "Grab Solar (Dispenser11), eight Bulbs, Wire(s). No battery. Face MiniatureSun; parallel keeps all eight bright; Induction Lab magnet thrust for peak EMF. Overlay shows solar W/load + pk EMF + N/8 lit. RESET retries cleanly.",
                    prerequisiteId = "sun_faraday_hept",
                    objectives = new[]
                    {
                        new ChallengeObjective
                        {
                            type = ObjectiveType.SolarPoweringLoad,
                            displayText = "Solar >= 0.05 W powering a bulb or motor",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.InducedEmfThreshold,
                            displayText = "Peak |EMF| >= 0.05 V through the coil",
                            targetValue = 0.05f
                        },
                        new ChallengeObjective
                        {
                            type = ObjectiveType.MultipleBulbsLit,
                            displayText = "Light up 8 bulbs from the solar panel simultaneously",
                            targetCount = 8
                        }
                    },
                    starThresholds = new StarThresholds
                    {
                        twoStarTimeSeconds = 240f,
                        threeStarTimeSeconds = 120f,
                        threeStarMaxComponents = 36
                    }
                }

            };

        }

        // Ã¢â€â‚¬Ã¢â€â‚¬ Progress / unlock logic Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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

        // Ã¢â€â‚¬Ã¢â€â‚¬ Challenge lifecycle Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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
            _peakInducedEmf = 0f;
            _refsValid = false; // force refresh on first poll
            if (def.id == "induction")
                EnsureInductionBound();
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
            _peakInducedEmf = 0f;
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
            _peakInducedEmf = 0f;
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

        // Ã¢â€â‚¬Ã¢â€â‚¬ Objective evaluation Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

        void RefreshSimRefs()
        {
            _bulbs = UnityEngine.Object.FindObjectsByType<Bulb>(FindObjectsInactive.Exclude);
            _motors = UnityEngine.Object.FindObjectsByType<Motor>(FindObjectsInactive.Exclude);
            _resistors = UnityEngine.Object.FindObjectsByType<Resistor>(FindObjectsInactive.Exclude);
            _solars = UnityEngine.Object.FindObjectsByType<Solar>(FindObjectsInactive.Exclude);
            _switches = UnityEngine.Object.FindObjectsByType<Switch>(FindObjectsInactive.Exclude);
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
                    return IsSolarPoweringLoad(obj.targetValue > 0f ? obj.targetValue : 0.05f);

                case ObjectiveType.InducedEmfThreshold:
                    return GetInducedEmf() >= obj.targetValue;

                case ObjectiveType.SwitchClosedBulbLit:
                    return HasClosedSwitch() && CountActiveBulbs() >= 1;

                case ObjectiveType.SwitchClosedMotorSpinning:
                    return HasClosedSwitch() && GetMaxMotorRpm() >= obj.targetValue;

                case ObjectiveType.CircuitPowerThreshold:
                    return GetMaxCircuitPower() >= (obj.targetValue > 0f ? obj.targetValue : 0.05f);

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

        /// <summary>Placed Switch clones with IsClosed == true (knife switch closed).</summary>
        int CountClosedSwitches()
        {
            if (_switches == null)
                return 0;
            int count = 0;
            for (int i = 0; i < _switches.Length; i++)
            {
                Switch sw = _switches[i];
                if (sw == null || !sw.IsPlaced || !sw.IsClone)
                    continue;
                if (sw.IsClosed)
                    count++;
            }
            return count;
        }

        bool HasClosedSwitch()
        {
            return CountClosedSwitches() >= 1;
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

        /// <summary>
        /// Max |V|*|I| watts across placed active bulb/motor clones (CircuitLab load power).
        /// </summary>
        float GetMaxCircuitPower()
        {
            float maxW = 0f;
            if (_bulbs != null)
            {
                for (int i = 0; i < _bulbs.Length; i++)
                {
                    Bulb bulb = _bulbs[i];
                    if (bulb == null || !bulb.IsPlaced || !bulb.IsClone)
                        continue;
                    if (!CircuitReader.GetIsActive(bulb) || !CircuitReader.IsCurrentSignificant(bulb))
                        continue;
                    float w = (float)(System.Math.Abs(bulb.GetVoltage()) * System.Math.Abs(bulb.GetCurrentValue()));
                    if (w > maxW)
                        maxW = w;
                }
            }
            if (_motors != null)
            {
                for (int i = 0; i < _motors.Length; i++)
                {
                    Motor motor = _motors[i];
                    if (motor == null || !motor.IsPlaced || !motor.IsClone)
                        continue;
                    if (!CircuitReader.GetIsActive(motor) || !CircuitReader.IsCurrentSignificant(motor))
                        continue;
                    float w = (float)(System.Math.Abs(motor.GetVoltage()) * System.Math.Abs(motor.GetCurrentValue()));
                    if (w > maxW)
                        maxW = w;
                }
            }
            return maxW;
        }

        float GetMaxSolarWattage()
        {
            if (_solars == null)
                return 0f;
            float maxW = 0f;
            for (int i = 0; i < _solars.Length; i++)
            {
                Solar solar = _solars[i];
                if (solar == null || !solar.IsPlaced || !solar.IsClone)
                    continue;
                if (solar.SolarWattage > maxW)
                    maxW = solar.SolarWattage;
            }
            return maxW;
        }

        bool HasActiveLoad()
        {
            if (CountActiveBulbs() >= 1)
                return true;
            if (_motors == null)
                return false;
            for (int i = 0; i < _motors.Length; i++)
            {
                Motor motor = _motors[i];
                if (motor == null || !motor.IsPlaced || !motor.IsClone)
                    continue;
                if (CircuitReader.GetIsActive(motor) && CircuitReader.IsCurrentSignificant(motor))
                    return true;
            }
            return false;
        }

        bool IsSolarPoweringLoad(float minWattage = 0.05f)
        {
            if (_solars == null)
                return false;
            if (GetMaxSolarWattage() < minWattage)
                return false;

            bool solarActive = false;
            for (int i = 0; i < _solars.Length; i++)
            {
                Solar solar = _solars[i];
                if (solar == null || !solar.IsPlaced || !solar.IsClone)
                    continue;
                if (solar.SolarWattage < minWattage)
                    continue;
                if (CircuitReader.GetIsActive(solar) && CircuitReader.IsCurrentSignificant(solar))
                {
                    solarActive = true;
                    break;
                }
            }
            if (!solarActive)
                return false;

            return HasActiveLoad();
        }

        float GetLiveInducedEmf()
        {
            if (_inductionCircuit == null)
                return 0f;
            return Mathf.Abs(_inductionCircuit.EmfVolts);
        }

        /// <summary>
        /// Peak |EMF| latched while the challenge is active. Instantaneous Faraday spikes
        /// last < one poll interval; evaluating the peak makes Induction completable.
        /// </summary>
        float GetInducedEmf()
        {
            SamplePeakInducedEmf();
            return _peakInducedEmf;
        }

        void SamplePeakInducedEmf()
        {
            if (_inductionCircuit == null)
            {
                _inductionCircuit = UnityEngine.Object.FindAnyObjectByType<InductionCircuit>(FindObjectsInactive.Exclude);
                if (_inductionCircuit == null)
                    return;
            }
            float live = GetLiveInducedEmf();
            if (live > _peakInducedEmf)
                _peakInducedEmf = live;
        }

        void EnsureInductionBound()
        {
            RefreshSimRefs();
            if (_inductionCircuit == null)
                return;
            InductionCoil coil = _inductionCircuit.Coil;
            if (coil != null)
                coil.BindAllSceneDipoles();
            SamplePeakInducedEmf();
        }

        /// <summary>
        /// Live measured progress for the objective overlay (ACTION â†’ MEASUREMENT â†’ FEEDBACK).
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
                {
                    float w = GetMaxSolarWattage();
                    float need = obj.targetValue > 0f ? obj.targetValue : 0.05f;
                    string load = HasActiveLoad() ? "load=1" : "load=0";
                    return w.ToString("0.00") + "/" + need.ToString("0.00") + " W " + load;
                }
                case ObjectiveType.InducedEmfThreshold:
                {
                    SamplePeakInducedEmf();
                    float live = GetLiveInducedEmf();
                    float pk = _peakInducedEmf;
                    return "pk " + pk.ToString("0.000") + "/" + obj.targetValue.ToString("0.00") + " V live=" + live.ToString("0.000");
                }
                case ObjectiveType.SwitchClosedBulbLit:
                {
                    int sw = CountClosedSwitches();
                    int lit = CountActiveBulbs();
                    return "sw=" + (sw >= 1 ? "1" : "0") + " lit=" + (lit >= 1 ? "1" : "0");
                }
                case ObjectiveType.SwitchClosedMotorSpinning:
                {
                    int sw = CountClosedSwitches();
                    float rpm = GetMaxMotorRpm();
                    float need = obj.targetValue > 0f ? obj.targetValue : 120f;
                    return "sw=" + (sw >= 1 ? "1" : "0") + " " + rpm.ToString("0") + "/" + need.ToString("0") + " RPM";
                }
                case ObjectiveType.CircuitPowerThreshold:
                {
                    float w = GetMaxCircuitPower();
                    float need = obj.targetValue > 0f ? obj.targetValue : 0.05f;
                    int lit = CountActiveBulbs();
                    return w.ToString("0.00") + "/" + need.ToString("0.00") + " W lit=" + (lit >= 1 ? "1" : "0");
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
            _peakInducedEmf = 0f;
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

        // Ã¢â€â‚¬Ã¢â€â‚¬ Mentor context Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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


