using System;
using UnityEngine;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Visualization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Experiments
{
    /// <summary>
    /// Reality Engine v0.6 experiment runner. Player-driven:
    /// QUESTION -> HYPOTHESIS -> RUN -> MEASUREMENT -> RESULT -> COMPARE.
    /// Samples the existing lumped Faraday lab. Does not auto-move the magnet.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class ExperimentRunner : MonoBehaviour
    {
        public const int SampleCap = 2000;
        public const string Honesty =
            "Measurements are from the lumped Faraday model (EMF = -N dPhi/dt, resistive loop), not a lab instrument.";

        [SerializeField]
        ExperimentDefinition definition = new ExperimentDefinition();

        [SerializeField]
        InductionCoil coil;

        [SerializeField]
        InductionCircuit circuit;

        [SerializeField]
        MagneticDipole magnet;

        [SerializeField]
        [Tooltip("Recording length in unscaled seconds. Stop ends early.")]
        float durationSeconds = 8f;

        [SerializeField]
        [Tooltip("Sample cap rate in Hz while Recording.")]
        float sampleHz = 30f;

        [SerializeField]
        bool enableKeyboard = true;

        readonly ExperimentSample[] _ring = new ExperimentSample[SampleCap];
        int _write;
        int _count;
        ExperimentState _state = ExperimentState.Idle;
        float _elapsed;
        float _sampleAccum;
        float _sumAbsDPhiDt;
        float _peakAbsEmf;
        float _peakAbsI;
        float _peakMagnetSpeed;
        Rigidbody _magnetBody;
        Vector3 _lastMagnetPos;
        bool _hasLastMagnetPos;
        InductionCoil _coil;
        InductionCircuit _circuit;
        MagneticDipole _magnet;
        ExperimentBoard _board;
        InductionReadout _readout;
        ModelCard _modelCard;
        ExperimentResult _lastResult;
        ExperimentCompare _lastCompare;
        string _lastSavePath;
        string _status = "Idle";

        public ExperimentState State => _state;
        public ExperimentDefinition Definition => definition;
        public ExperimentResult LastResult => _lastResult;
        public ExperimentCompare LastCompare => _lastCompare;
        public int SampleCount => _count;
        public float ElapsedSeconds => _elapsed;
        public string StatusLine => _status;
        public string LastSavePath => _lastSavePath;
        public string HonestyTag => definition != null ? definition.honestyTag : Honesty;

        public void SetLab(InductionCoil coilValue, InductionCircuit circuitValue, MagneticDipole magnetValue)
        {
            coil = coilValue;
            circuit = circuitValue;
            magnet = magnetValue;
            CacheRefs();
        }

        public void BindBoard(ExperimentBoard board)
        {
            _board = board;
        }

        public void BindReadout(InductionReadout readout)
        {
            _readout = readout;
            if (_readout != null)
                _readout.SetExperimentRunner(this);
        }

        public void BindModelCard(ModelCard card)
        {
            _modelCard = card;
            PushHonesty();
        }

        public void ApplyScientistHypothesis(string question, string hint, float predictedPeakEmf, bool hypothesisSet)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id))
                ApplyCannedDefinition();
            if (!string.IsNullOrEmpty(question))
            {
                definition.question = question;
                definition.title = question;
            }
            if (!string.IsNullOrEmpty(hint))
                definition.hypothesisHint = hint;
            definition.hypothesisEmf = predictedPeakEmf;
            definition.hypothesisSet = hypothesisSet;
            PushHonesty();
        }

        public void ApplyCannedDefinition()
        {
            definition = ExperimentDefinition.CannedMagnetFaster();
            SyncParametersFromCoil();
            PushHonesty();
        }

        public void Arm()
        {
            CacheRefs();
            SyncParametersFromCoil();
            ResetBuffer();
            _elapsed = 0f;
            _sampleAccum = 0f;
            _lastResult = null;
            _lastCompare = null;
            _state = ExperimentState.Armed;
            _status = "Armed — grab the magnet, then Record. No auto-move.";
            PushHonesty();
        }

        public void Record()
        {
            if (_state == ExperimentState.Recording)
                return;
            CacheRefs();
            SyncParametersFromCoil();
            ResetBuffer();
            _elapsed = 0f;
            _sampleAccum = 0f;
            _hasLastMagnetPos = false;
            _state = ExperimentState.Recording;
            _status = "Recording";
        }

        public void Stop()
        {
            if (_state != ExperimentState.Recording)
                return;
            Complete();
        }

        public void Repeat()
        {
            Arm();
        }

        public string Save()
        {
            ExperimentRunRecord record = BuildRecord();
            _lastSavePath = ExperimentStore.Save(record);
            _status = "Saved " + (_lastSavePath ?? "(failed)");
            return _lastSavePath;
        }

        public bool LoadLatest()
        {
            ExperimentRunRecord record = ExperimentStore.LoadLatest();
            if (record == null)
            {
                _status = "Load: no files in " + ExperimentStore.DirectoryPath;
                return false;
            }

            if (record.definition != null)
                definition = record.definition;
            _lastResult = record.result;
            _lastCompare = record.compare;
            ResetBuffer();
            if (record.samples != null)
            {
                int n = Mathf.Min(record.samples.Length, SampleCap);
                for (int i = 0; i < n; i++)
                    PushSample(record.samples[i]);
            }

            _state = ExperimentState.Complete;
            _status = "Loaded latest (" + _count + " samples)";
            PushHonesty();
            return true;
        }

        public string[] ListSaves()
        {
            return ExperimentStore.ListFiles();
        }

        public string CompareLine()
        {
            if (_lastCompare != null && _lastCompare.performed)
                return _lastCompare.summary;
            if (_state == ExperimentState.Complete)
                return "COMPARE skipped (hypothesis EMF empty)";
            return string.Empty;
        }

        void Awake()
        {
            CacheRefs();
            if (definition == null || string.IsNullOrEmpty(definition.id))
                ApplyCannedDefinition();
            PushHonesty();
        }

        void Update()
        {
            HandleKeyboard();
        }

        void LateUpdate()
        {
            if (_state != ExperimentState.Recording)
                return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            _elapsed += dt;
            _sampleAccum += dt;
            float interval = 1f / Mathf.Max(1f, sampleHz);
            if (_sampleAccum >= interval)
            {
                _sampleAccum -= interval;
                CaptureSample();
            }

            if (_elapsed >= Mathf.Max(0.1f, durationSeconds))
                Complete();
        }

        void HandleKeyboard()
        {
            if (!enableKeyboard)
                return;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame)
                    Record();
                if (kb.tKey.wasPressedThisFrame)
                    Stop();
                if (kb.yKey.wasPressedThisFrame)
                    Save();
                bool shift = (kb.leftShiftKey != null && kb.leftShiftKey.isPressed) ||
                             (kb.rightShiftKey != null && kb.rightShiftKey.isPressed);
                if (shift && kb.sKey.wasPressedThisFrame)
                    Save();
                if (kb.uKey.wasPressedThisFrame)
                    LoadLatest();
                if (kb.iKey.wasPressedThisFrame)
                    Repeat();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.R))
                Record();
            if (Input.GetKeyDown(KeyCode.T))
                Stop();
            if (Input.GetKeyDown(KeyCode.Y))
                Save();
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.S))
                Save();
            if (Input.GetKey(KeyCode.RightShift) && Input.GetKeyDown(KeyCode.S))
                Save();
            if (Input.GetKeyDown(KeyCode.U))
                LoadLatest();
            if (Input.GetKeyDown(KeyCode.I))
                Repeat();
#endif
        }

        void CacheRefs()
        {
            _coil = coil != null ? coil : _coil;
            _circuit = circuit != null ? circuit : _circuit;
            _magnet = magnet != null ? magnet : _magnet;
            if (_magnet != null)
            {
                if (_magnetBody == null || _magnetBody.gameObject != _magnet.gameObject)
                    _magnetBody = _magnet.GetComponent<Rigidbody>();
            }
        }

        void SyncParametersFromCoil()
        {
            if (definition == null)
                definition = ExperimentDefinition.CannedMagnetFaster();
            if (_coil == null)
                return;
            definition.turnsN = _coil.Turns;
            definition.windingR = _coil.Resistance;
            definition.loadR = _coil.LoadResistance;
        }

        void ResetBuffer()
        {
            _write = 0;
            _count = 0;
            _sumAbsDPhiDt = 0f;
            _peakAbsEmf = 0f;
            _peakAbsI = 0f;
            _peakMagnetSpeed = 0f;
        }

        void PushSample(ExperimentSample sample)
        {
            _ring[_write] = sample;
            _write++;
            if (_write >= SampleCap)
                _write = 0;
            if (_count < SampleCap)
                _count++;
        }

        void CaptureSample()
        {
            if (_circuit == null)
                CacheRefs();
            if (_circuit == null)
                return;

            float speed = MagnetSpeed();
            var sample = new ExperimentSample
            {
                t = _elapsed,
                phi = _circuit.FluxWebers,
                dPhiDt = _circuit.FluxRateWebersPerSecond,
                emf = _circuit.EmfVolts,
                i = _circuit.CurrentAmperes,
                magnetSpeed = speed
            };
            PushSample(sample);

            float absEmf = Mathf.Abs(sample.emf);
            float absI = Mathf.Abs(sample.i);
            float absD = Mathf.Abs(sample.dPhiDt);
            if (absEmf > _peakAbsEmf)
                _peakAbsEmf = absEmf;
            if (absI > _peakAbsI)
                _peakAbsI = absI;
            if (speed > _peakMagnetSpeed)
                _peakMagnetSpeed = speed;
            _sumAbsDPhiDt += absD;
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

        void Complete()
        {
            _lastResult = new ExperimentResult
            {
                durationSeconds = _elapsed,
                sampleCount = _count,
                peakAbsEmf = _peakAbsEmf,
                peakAbsI = _peakAbsI,
                meanAbsDPhiDt = _count > 0 ? _sumAbsDPhiDt / _count : 0f,
                peakMagnetSpeed = _peakMagnetSpeed
            };
            _lastCompare = BuildCompare(_lastResult);
            _state = ExperimentState.Complete;
            if (_lastCompare != null && _lastCompare.performed)
                _status = "Complete  " + _lastCompare.summary;
            else
                _status = "Complete  peak|EMF|=" + _peakAbsEmf.ToString("0.000e+0") + " V (no COMPARE)";
            PushHonesty();
        }

        ExperimentCompare BuildCompare(ExperimentResult result)
        {
            var cmp = new ExperimentCompare();
            if (definition == null || !definition.hypothesisSet || result == null)
            {
                cmp.performed = false;
                cmp.summary = "skipped (hypothesis EMF empty)";
                return cmp;
            }

            cmp.performed = true;
            cmp.predictedPeakEmf = definition.hypothesisEmf;
            cmp.measuredPeakEmf = result.peakAbsEmf;
            float denom = Mathf.Max(1e-12f, Mathf.Abs(cmp.predictedPeakEmf));
            cmp.relativeError = Mathf.Abs(cmp.measuredPeakEmf - cmp.predictedPeakEmf) / denom;
            cmp.summary = "pred " + Sci(cmp.predictedPeakEmf) + " V  meas " + Sci(cmp.measuredPeakEmf) +
                          " V  relErr " + (cmp.relativeError * 100f).ToString("0.0") + "%";
            return cmp;
        }

        ExperimentRunRecord BuildRecord()
        {
            if (_lastResult == null && _state == ExperimentState.Recording)
                Complete();

            return new ExperimentRunRecord
            {
                definition = definition != null ? definition.Clone() : ExperimentDefinition.CannedMagnetFaster(),
                result = _lastResult,
                compare = _lastCompare,
                savedUtc = DateTime.UtcNow.ToString("o"),
                state = _state.ToString(),
                samples = Snapshot()
            };
        }

        ExperimentSample[] Snapshot()
        {
            var arr = new ExperimentSample[_count];
            int start = (_write - _count);
            if (start < 0)
                start += SampleCap;
            for (int i = 0; i < _count; i++)
                arr[i] = _ring[(start + i) % SampleCap];
            return arr;
        }

        void PushHonesty()
        {
            if (_modelCard == null)
                return;
            string q = definition != null
                ? (string.IsNullOrEmpty(definition.question) ? definition.title : definition.question)
                : "";
            _modelCard.SetExperimentLine(
                "Experiment: " + _state + "  [" + HonestyTag + "]\n" +
                q + "\n" + Honesty);
        }

        static string Sci(float value)
        {
            float abs = Mathf.Abs(value);
            if (abs > 0f && (abs < 1e-3f || abs >= 1e3f))
                return value.ToString("0.000e+0");
            return value.ToString("0.0000");
        }
    }
}
