using UnityEngine;
using TMPro;

namespace RealityEngine.Experiments
{
    /// <summary>
    /// World-space TMP board for the v0.6 scientific workflow.
    /// Honesty: readings are from the lumped Faraday model, not a lab instrument.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(55)]
    public sealed class ExperimentBoard : MonoBehaviour
    {
        public const string Honesty =
            "Honesty: measurements are from the lumped Faraday model (EMF = -N dPhi/dt, resistive loop), not a lab instrument.";

        [SerializeField]
        TextMeshPro text;

        [SerializeField]
        bool billboardYaw = true;

        ExperimentRunner _runner;
        TextMeshPro _text;
        Camera _camera;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(1600);

        public void Bind(ExperimentRunner runner, TextMeshPro tmp)
        {
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
            _sb.Append("EXPERIMENT  v0.6\n");
            _sb.Append("Q -> H -> RUN -> MEASURE -> RESULT -> COMPARE\n");
            if (_runner == null)
            {
                _sb.Append("no runner\n");
                _sb.Append(Honesty);
                _text.text = _sb.ToString();
                return;
            }

            ExperimentDefinition def = _runner.Definition;
            _sb.Append("State  ").Append(_runner.State).Append('\n');
            if (def != null)
            {
                _sb.Append("Q  ").Append(string.IsNullOrEmpty(def.question) ? def.title : def.question).Append('\n');
                _sb.Append("Hint  ").Append(def.hypothesisHint).Append('\n');
                _sb.Append("Params  N=").Append(def.turnsN);
                _sb.Append("  Rw=").Append(def.windingR.ToString("0.###"));
                _sb.Append("  Rload=").Append(def.loadR.ToString("0.###")).Append('\n');
                if (def.hypothesisSet)
                    _sb.Append("H  peak |EMF| = ").Append(def.hypothesisEmf.ToString("0.000e+0")).Append(" V\n");
                else
                    _sb.Append("H  (empty — set hypothesisEmf to COMPARE)\n");
            }

            _sb.Append("Samples ").Append(_runner.SampleCount).Append("  t=");
            _sb.Append(_runner.ElapsedSeconds.ToString("0.00")).Append(" s\n");

            ExperimentResult result = _runner.LastResult;
            if (result != null && _runner.State == ExperimentState.Complete)
            {
                _sb.Append("RESULT  peak|EMF| ").Append(Sci(result.peakAbsEmf)).Append(" V\n");
                _sb.Append("        peak|I|   ").Append(Sci(result.peakAbsI)).Append(" A\n");
                _sb.Append("        mean|dPhi/dt| ").Append(Sci(result.meanAbsDPhiDt)).Append(" Wb/s\n");
            }

            ExperimentCompare cmp = _runner.LastCompare;
            if (cmp != null && cmp.performed)
                _sb.Append("COMPARE  ").Append(cmp.summary).Append('\n');
            else if (_runner.State == ExperimentState.Complete)
                _sb.Append("COMPARE  skipped (no hypothesis EMF)\n");

            _sb.Append("Keys  R record  T stop  Y save  U load  I repeat\n");
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
