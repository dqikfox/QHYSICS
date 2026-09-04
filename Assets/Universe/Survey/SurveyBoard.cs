using UnityEngine;
using TMPro;
using RealityEngine.Visualization;

namespace RealityEngine.Survey
{
    /// <summary>
    /// Small table-edge TMP for the Giza cubit survey (Khufu + Khafre). Honesty: reconstructed
    /// original (Petrie/Lehner), not a scan. Simulation/published dimensions are source of truth.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(57)]
    public sealed class SurveyBoard : MonoBehaviour
    {
        public const string RootName = "SurveyBoard";
        public const float WidthMeters = 0.70f;
        public const float HeightMeters = 0.50f;

        [SerializeField]
        TextMeshPro text;

        [SerializeField]
        bool billboardYaw = true;

        TextMeshPro _text;
        Camera _camera;
        float _nextRefresh;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(1200);

        public void Bind(TextMeshPro tmp)
        {
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
            float cubit = KhufuPyramid.Cubit;
            float khafreBaseCubits = KhafrePyramid.BaseMeters / cubit;
            float khafreHeightCubits = KhafrePyramid.HeightMeters / cubit;
            _sb.Length = 0;
            _sb.Append("GIZA SURVEY\n");
            _sb.Append(GizaComplex.HonestyPrefix).Append('\n');
            _sb.Append("cubit  ").Append(cubit.ToString("0.0000")).Append(" m  (royal)\n");
            _sb.Append("last  ");
            if (CubitRod.HasMeasurement)
                _sb.Append(CubitRod.DescribeLast());
            else
                _sb.Append("(grab the cubit rod, aim at Khufu or Khafre)");
            _sb.Append('\n');
            _sb.Append("Khufu  slope 51 deg 50' 40\"  seked 5.5  (14/11)\n");
            _sb.Append("  base 440 / height 280 cubits  King 20 x 10 (");
            _sb.Append(KhufuPyramid.KingEW.ToString("0.00")).Append(" x ");
            _sb.Append(KhufuPyramid.KingNS.ToString("0.00")).Append(" m)\n");
            _sb.Append("Khafre  slope 53 deg 10'  seked 5.25  (4/3)\n");
            _sb.Append("  base ≈ ").Append(khafreBaseCubits.ToString("0")).Append(" / height ≈ ");
            _sb.Append(khafreHeightCubits.ToString("0")).Append(" cubits  (");
            _sb.Append(KhafrePyramid.BaseMeters.ToString("0.00")).Append(" x ");
            _sb.Append(KhafrePyramid.HeightMeters.ToString("0.0")).Append(" m)\n");
            _sb.Append("Grab the rod. Field Lens on Khufu_Casing or Khafre_Casing. Key 0 = Q7.\n");
            _sb.Append(CubitRod.Honesty);
            _text.text = _sb.ToString();
        }
    }
}
