using UnityEngine;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;
using RealityEngine.Chemistry;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.AI
{
    /// <summary>
    /// Analytical Faraday collaborator. Reads live induction state and quotes
    /// EMF = -N dPhi/dt. Not an LLM. Simulation is the source of truth.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(45)]
    public sealed class Scientist : MonoBehaviour
    {
        public const string Honesty = "Analytical Faraday collaborator, not an LLM.";
        public const string EqFaraday = "EMF = -N dPhi/dt";
        public const string EqOhm = "I = EMF / R_total";

        [SerializeField]
        InductionCoil coil;

        [SerializeField]
        InductionCircuit circuit;

        [SerializeField]
        MagneticDipole magnet;

        [SerializeField]
        ExperimentRunner runner;

        [SerializeField]
        bool enableKeyboard = true;

        InductionCoil _coil;
        InductionCircuit _circuit;
        MagneticDipole _magnet;
        Rigidbody _magnetBody;
        ExperimentRunner _runner;
        ScientistBoard _board;
        ScientistQuestion _question = ScientistQuestion.DoubleMagnetVelocity;
        Hypothesis _hypothesis;
        bool _hypothesisFormed;
        SimulationState _state;
        int _baseTurns = 80;
        float _baseWinding = 2f;
        float _baseLoad = 8f;
        float _baseRadius = 0.15f;
        bool _haveBase;
        Vector3 _lastMagnetPos;
        bool _hasLastMagnetPos;

        public ScientistQuestion Question => _question;
        public Hypothesis LastHypothesis => _hypothesis;
        public bool HypothesisFormed => _hypothesisFormed;
        public SimulationState LiveState => _state;
        public string HonestyTag => Honesty;
        public string QuestionPrompt => PromptFor(_question);

        public void SetLab(InductionCoil coilValue, InductionCircuit circuitValue, MagneticDipole magnetValue, ExperimentRunner runnerValue)
        {
            coil = coilValue;
            circuit = circuitValue;
            magnet = magnetValue;
            runner = runnerValue;
            CacheRefs();
            SnapshotBaseIfNeeded();
        }

        public void BindBoard(ScientistBoard board)
        {
            _board = board;
        }

        public void SelectQuestion(ScientistQuestion question)
        {
            _question = question;
            _hypothesisFormed = false;
            CaptureState();
        }

        public void SelectDoubleVelocity()
        {
            SelectQuestion(ScientistQuestion.DoubleMagnetVelocity);
        }

        public void SelectDoubleN()
        {
            SelectQuestion(ScientistQuestion.DoubleTurnsN);
        }

        public void SelectDoubleR()
        {
            SelectQuestion(ScientistQuestion.DoubleResistanceR);
        }

        public void SelectWhyCopper()
        {
            SelectQuestion(ScientistQuestion.WhyCopperConductor);
        }

        public SimulationState CaptureState()
        {
            CacheRefs();
            SnapshotBaseIfNeeded();

            _state = new SimulationState
            {
                turnsN = 0,
                totalResistanceOhms = 0f,
                windingResistanceOhms = 0f,
                loadResistanceOhms = 0f,
                fluxWebers = 0f,
                dFluxDt = 0f,
                emfVolts = 0f,
                currentAmperes = 0f,
                magnetSpeed = MagnetSpeed(),
                timeScale = Time.timeScale,
                modelNotes = SimulationState.FaradayNotes
            };

            if (_coil != null)
            {
                _state.turnsN = _coil.Turns;
                _state.windingResistanceOhms = _coil.Resistance;
                _state.loadResistanceOhms = _coil.LoadResistance;
                _state.fluxWebers = _coil.Flux;
                _state.dFluxDt = _coil.FluxRate;
                _state.emfVolts = _coil.Emf;
            }

            if (_circuit != null)
            {
                _state.fluxWebers = _circuit.FluxWebers;
                _state.dFluxDt = _circuit.FluxRateWebersPerSecond;
                _state.emfVolts = _circuit.EmfVolts;
                _state.currentAmperes = _circuit.CurrentAmperes;
                _state.totalResistanceOhms = _circuit.TotalResistanceOhms;
                if (_coil == null && _circuit.Coil != null)
                {
                    InductionCoil c = _circuit.Coil;
                    _state.turnsN = c.Turns;
                    _state.windingResistanceOhms = c.Resistance;
                    _state.loadResistanceOhms = c.LoadResistance;
                }
            }
            else if (_coil != null)
            {
                _state.totalResistanceOhms = _coil.Resistance + _coil.LoadResistance;
            }

            return _state;
        }

        public Hypothesis FormHypothesis()
        {
            CaptureState();
            ExperimentResult last = _runner != null ? _runner.LastResult : null;
            switch (_question)
            {
                case ScientistQuestion.DoubleTurnsN:
                    _hypothesis = PredictDoubleN(last);
                    break;
                case ScientistQuestion.DoubleResistanceR:
                    _hypothesis = PredictDoubleR(last);
                    break;
                case ScientistQuestion.WhyCopperConductor:
                    _hypothesis = PredictWhyCopper();
                    break;
                default:
                    _hypothesis = PredictDoubleVelocity(last);
                    break;
            }

            _hypothesisFormed = true;
            return _hypothesis;
        }

        public void ArmExperiment()
        {
            CacheRefs();
            if (!_hypothesisFormed)
                FormHypothesis();

            if (_question != ScientistQuestion.WhyCopperConductor)
                ApplyQuestionParameters();
            CaptureState();
            if (_question == ScientistQuestion.WhyCopperConductor)
                return;

            if (_runner == null)
                return;

            bool set = _hypothesis.hasNumericPrediction;
            _runner.ApplyScientistHypothesis(
                PromptFor(_question),
                _hypothesis.text,
                _hypothesis.predictedPeakEmf,
                set);
            _runner.Arm();
        }

        public static string PromptFor(ScientistQuestion question)
        {
            switch (question)
            {
                case ScientistQuestion.DoubleTurnsN:
                    return "Q2  What if I double N (turns)?";
                case ScientistQuestion.DoubleResistanceR:
                    return "Q3  What if I double R (total loop resistance)?";
                case ScientistQuestion.WhyCopperConductor:
                    return "Q4  Why is copper a conductor?";
                default:
                    return "Q1  What if I double magnet velocity? (B and coil unchanged)";
            }
        }

        void Awake()
        {
            CacheRefs();
            SnapshotBaseIfNeeded();
        }

        void Update()
        {
            HandleKeyboard();
        }

        void LateUpdate()
        {
            CaptureState();
        }

        void HandleKeyboard()
        {
            if (!enableKeyboard)
                return;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit4Key.wasPressedThisFrame)
                    SelectDoubleVelocity();
                if (kb.digit5Key.wasPressedThisFrame)
                    SelectDoubleN();
                if (kb.digit6Key.wasPressedThisFrame)
                    SelectDoubleR();
                if (kb.digit7Key.wasPressedThisFrame)
                    SelectWhyCopper();
                if (kb.hKey.wasPressedThisFrame)
                    FormHypothesis();
                if (kb.jKey.wasPressedThisFrame)
                    ArmExperiment();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha4))
                SelectDoubleVelocity();
            if (Input.GetKeyDown(KeyCode.Alpha5))
                SelectDoubleN();
            if (Input.GetKeyDown(KeyCode.Alpha6))
                SelectDoubleR();
            if (Input.GetKeyDown(KeyCode.Alpha7))
                SelectWhyCopper();
            if (Input.GetKeyDown(KeyCode.H))
                FormHypothesis();
            if (Input.GetKeyDown(KeyCode.J))
                ArmExperiment();
#endif
        }

        void CacheRefs()
        {
            _coil = coil != null ? coil : _coil;
            _circuit = circuit != null ? circuit : _circuit;
            _magnet = magnet != null ? magnet : _magnet;
            _runner = runner != null ? runner : _runner;
            if (_magnet != null)
            {
                if (_magnetBody == null || _magnetBody.gameObject != _magnet.gameObject)
                    _magnetBody = _magnet.GetComponent<Rigidbody>();
            }
        }

        void SnapshotBaseIfNeeded()
        {
            if (_haveBase || _coil == null)
                return;
            _baseTurns = _coil.Turns;
            _baseWinding = _coil.Resistance;
            _baseLoad = _coil.LoadResistance;
            _baseRadius = _coil.Radius;
            _haveBase = true;
        }

        void ApplyQuestionParameters()
        {
            if (_coil == null)
                return;
            SnapshotBaseIfNeeded();
            switch (_question)
            {
                case ScientistQuestion.DoubleTurnsN:
                    _coil.Configure(_baseTurns * 2, _baseRadius, _baseWinding, _baseLoad);
                    break;
                case ScientistQuestion.DoubleResistanceR:
                    _coil.Configure(_baseTurns, _baseRadius, _baseWinding * 2f, _baseLoad * 2f);
                    break;
                default:
                    _coil.Configure(_baseTurns, _baseRadius, _baseWinding, _baseLoad);
                    break;
            }
        }

        Hypothesis PredictDoubleVelocity(ExperimentResult last)
        {
            const string eq = EqFaraday + " with dPhi/dt proportional to v (B, coil fixed).";
            string body =
                "If |v_magnet| doubles and B and the coil are unchanged, lumped Faraday says |EMF| scales with |dPhi/dt| and dPhi/dt scales with v, so peak |EMF| doubles. Equation: " + eq;

            if (last == null)
                return Hypothesis.NeedBaseline(body, eq);

            float predicted;
            string basedOn;
            if (last.peakAbsEmf > 1e-12f)
            {
                predicted = last.peakAbsEmf * 2f;
                basedOn = "last run peak |EMF| x 2. Equation: " + eq;
            }
            else if (last.meanAbsDPhiDt > 1e-12f)
            {
                int n = Mathf.Max(1, _state.turnsN);
                predicted = n * last.meanAbsDPhiDt * 2f;
                basedOn = "last run mean |dPhi/dt| x 2 x N. Equation: |EMF| = N |dPhi/dt|; " + eq;
            }
            else
            {
                return Hypothesis.NeedBaseline(body, eq);
            }

            return new Hypothesis
            {
                text = body,
                predictedPeakEmf = predicted,
                basedOn = basedOn,
                hasNumericPrediction = true
            };
        }

        Hypothesis PredictDoubleN(ExperimentResult last)
        {
            const string eq = EqFaraday + " (same dPhi/dt).";
            string body = "Doubling N at the same dPhi/dt doubles EMF. Equation: " + eq;

            float predicted;
            string basedOn;
            bool numeric;
            if (last != null && last.peakAbsEmf > 1e-12f)
            {
                predicted = last.peakAbsEmf * 2f;
                basedOn = "last run peak |EMF| x 2. Equation: " + eq;
                numeric = true;
            }
            else
            {
                predicted = Mathf.Abs(_state.emfVolts) * 2f;
                basedOn = "live |EMF| x 2. Equation: " + eq;
                numeric = predicted > 1e-12f;
                if (!numeric && last != null && last.meanAbsDPhiDt > 1e-12f)
                {
                    predicted = Mathf.Max(1, _state.turnsN) * last.meanAbsDPhiDt * 2f;
                    basedOn = "last run mean |dPhi/dt| x 2 x N. Equation: |EMF| = N |dPhi/dt|.";
                    numeric = true;
                }
            }

            if (!numeric)
                return Hypothesis.NeedBaseline(body, eq);

            return new Hypothesis
            {
                text = body,
                predictedPeakEmf = predicted,
                basedOn = basedOn,
                hasNumericPrediction = true
            };
        }

        Hypothesis PredictDoubleR(ExperimentResult last)
        {
            const string eq = EqFaraday + " ; " + EqOhm + " (EMF independent of R).";
            string body = "Doubling R_total halves I; EMF is unchanged. Equation: " + eq;

            float predicted;
            string basedOn;
            bool numeric;
            if (last != null && last.peakAbsEmf > 1e-12f)
            {
                predicted = last.peakAbsEmf;
                basedOn = "last run peak |EMF| unchanged. Equation: " + eq;
                numeric = true;
            }
            else
            {
                predicted = Mathf.Abs(_state.emfVolts);
                basedOn = "live |EMF| unchanged. Equation: " + eq;
                numeric = predicted > 1e-12f;
                if (!numeric && last != null && last.meanAbsDPhiDt > 1e-12f)
                {
                    predicted = Mathf.Max(1, _state.turnsN) * last.meanAbsDPhiDt;
                    basedOn = "last run mean |dPhi/dt| x N (EMF same if R doubles). Equation: " + eq;
                    numeric = true;
                }
            }

            if (!numeric)
                return Hypothesis.NeedBaseline(body, eq);

            return new Hypothesis
            {
                text = body,
                predictedPeakEmf = predicted,
                basedOn = basedOn,
                hasNumericPrediction = true
            };
        }


        Hypothesis PredictWhyCopper()
        {
            string body = Element.CopperConductorAnswer();
            return new Hypothesis
            {
                text = body,
                predictedPeakEmf = 0f,
                basedOn = Element.ConceptualHonesty + " " + Element.ClassicalCoilHonesty,
                hasNumericPrediction = false
            };
        }

        float MagnetSpeed()
        {
            if (_magnetBody != null)
                return _magnetBody.linearVelocity.magnitude;
            if (_magnet != null)
            {
                Vector3 p = _magnet.transform.position;
                float speed = 0f;
                if (_hasLastMagnetPos)
                {
                    float dt = Time.unscaledDeltaTime;
                    if (dt > 1e-6f)
                        speed = (p - _lastMagnetPos).magnitude / dt;
                }
                _lastMagnetPos = p;
                _hasLastMagnetPos = true;
                return speed;
            }
            return 0f;
        }
    }
}