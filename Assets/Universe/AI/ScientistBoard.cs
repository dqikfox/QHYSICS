using UnityEngine;
using TMPro;
using RealityEngine.Experiments;

namespace RealityEngine.AI
{
    /// <summary>
    /// World-space TMP for the v0.7 analytical scientist.
    /// Honesty: Analytical Faraday collaborator, not an LLM.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(56)]
    public sealed class ScientistBoard : MonoBehaviour
    {
        public const string Honesty = Scientist.Honesty;

        [SerializeField]
        TextMeshPro text;

        [SerializeField]
        bool billboardYaw = true;

        Scientist _scientist;
        ExperimentRunner _runner;
        TextMeshPro _text;
        Camera _camera;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(2000);

        public void Bind(Scientist scientist, ExperimentRunner runner, TextMeshPro tmp)
        {
            _scientist = scientist;
            _runner = runner;
            text = tmp;
            _text = tmp;
        }

        public TextMeshPro Text => _text != null ? _text : text;

        void Awake()
        {
            _text = text != null ? text : GetComponent<TextMeshPro>();
            if (_text == null)
                _text = GetComponentInChildren<TextMeshPro>();
            _camera = Camera.main;
        }

        void LateUpdate()
        {
            if (_text == null)
                return;

            if (billboardYaw)
            {
                if (_camera == null)
                    _camera = Camera.main;
                if (_camera != null)
                {
                    Vector3 toCam = _text.transform.position - _camera.transform.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 1e-6f)
                        _text.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }
            }

            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.08f;
            Rebuild();
        }

        void Rebuild()
        {
            _sb.Length = 0;
            _sb.Append("SCIENTIST  v1.0\n");
            _sb.Append(Honesty).Append('\n');
            if (_scientist == null)
            {
                _sb.Append("no scientist\n");
                _text.text = _sb.ToString();
                return;
            }

            SimulationState s = _scientist.LiveState;
            _sb.Append("LIVE  N=").Append(s.turnsN);
            _sb.Append("  R=").Append(s.totalResistanceOhms.ToString("0.###")).Append(" ohm\n");
            _sb.Append("  Phi ").Append(Sci(s.fluxWebers)).Append(" Wb\n");
            _sb.Append("  dPhi/dt ").Append(Sci(s.dFluxDt)).Append(" Wb/s\n");
            _sb.Append("  EMF ").Append(Sci(s.emfVolts)).Append(" V");
            _sb.Append("  I ").Append(Sci(s.currentAmperes)).Append(" A\n");
            _sb.Append("  |v_magnet| ").Append(Sci(s.magnetSpeed)).Append(" m/s");
            _sb.Append("  timeScale ").Append(s.timeScale.ToString("0.###")).Append('\n');
            _sb.Append("Notes  ").Append(s.modelNotes).Append('\n');

            _sb.Append("Q  ").Append(_scientist.QuestionPrompt).Append('\n');

            if (_scientist.HypothesisFormed)
            {
                Hypothesis h = _scientist.LastHypothesis;
                _sb.Append("H  ").Append(h.text).Append('\n');
                if (h.hasNumericPrediction)
                    _sb.Append("H  peak |EMF| = ").Append(Sci(h.predictedPeakEmf)).Append(" V\n");
                else if (_scientist.Question != ScientistQuestion.WhyCopperConductor
                    && _scientist.Question != ScientistQuestion.WhereMuscleEnergy
                    && _scientist.Question != ScientistQuestion.IsEnergyCreated)
                    _sb.Append("H  (no numeric peak — need a baseline run)\n");
                _sb.Append("basedOn  ").Append(h.basedOn).Append('\n');
            }
            else
            {
                _sb.Append("H  (press Form hypothesis)\n");
            }

            if (_runner != null && _runner.State == ExperimentState.Complete)
            {
                ExperimentResult result = _runner.LastResult;
                Hypothesis h = _scientist.LastHypothesis;
                if (result != null)
                {
                    _sb.Append("AFTER RUN  meas peak |EMF| ").Append(Sci(result.peakAbsEmf)).Append(" V\n");
                    if (h.hasNumericPrediction)
                    {
                        float pred = h.predictedPeakEmf;
                        float denom = Mathf.Max(1e-12f, Mathf.Abs(pred));
                        float rel = Mathf.Abs(result.peakAbsEmf - pred) / denom;
                        _sb.Append("PRED vs MEAS  ").Append(Sci(pred)).Append(" V  vs  ");
                        _sb.Append(Sci(result.peakAbsEmf)).Append(" V  relErr ");
                        _sb.Append((rel * 100f).ToString("0.0")).Append("%\n");
                    }
                    else
                    {
                        _sb.Append("PRED vs MEAS  skipped (need a baseline run)\n");
                    }
                }

                ExperimentCompare cmp = _runner.LastCompare;
                if (cmp != null && cmp.performed)
                    _sb.Append("RUNNER COMPARE  ").Append(cmp.summary).Append('\n');
            }

            _sb.Append("Keys  4 Q1-v  5 Q2-N  6 Q3-R  7 Q4-Cu  8 Q5-muscle  9 Q6-energy  H hypothesis  J arm\n");
            _sb.Append("Grab the magnet and move it. Do not expect auto-motion.\n");
            _sb.Append(Honesty);
            _text.text = _sb.ToString();
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