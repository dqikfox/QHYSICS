using UnityEngine;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Biology;
using RealityEngine.Physics.Thermo;
using RealityEngine.Survey;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Per-object Scale Engine listener. Enables/disables scale-specific children by composing
    /// with FieldLensTarget (reuses 0.4 lattice / charge viz — no second lattice).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScaleAwareTarget : MonoBehaviour
    {
        ScaleEngine _engine;
        FieldLensTarget _lensTarget;
        TextMeshPro _honesty;
        int _scale;
        bool _configured;

        public int Scale => _scale;

        public void Configure(ScaleEngine engine, FieldLensTarget lensTarget)
        {
            _engine = engine;
            _lensTarget = lensTarget;
            if (!_configured)
            {
                BuildHonesty();
                _configured = true;
            }

            if (_engine != null)
                _engine.Register(this);
            ApplyScale(_engine != null ? _engine.CurrentScale : 0);
        }

        void OnDisable()
        {
            if (_engine != null)
                _engine.Unregister(this);
        }

        void OnEnable()
        {
            if (_configured && _engine != null)
                _engine.Register(this);
        }

        public void ApplyScale(int scale)
        {
            _scale = Mathf.Clamp(scale, 0, ScaleEngine.StepCount - 1);
            if (_lensTarget != null)
                _lensTarget.SetScale(_scale);
            UpdateHonesty();
        }

        void BuildHonesty()
        {
            Transform existing = transform.Find("ScaleHonesty");
            GameObject labelGo;
            if (existing != null)
                labelGo = existing.gameObject;
            else
            {
                labelGo = new GameObject("ScaleHonesty");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = HonestyOffset();
                labelGo.transform.localScale = Vector3.one * 0.02f;
            }

            _honesty = labelGo.GetComponent<TextMeshPro>();
            if (_honesty == null)
                _honesty = labelGo.AddComponent<TextMeshPro>();
            _honesty.fontSize = 5f;
            _honesty.alignment = TextAlignmentOptions.Center;
            _honesty.color = new Color(0.75f, 0.92f, 0.98f);
            _honesty.rectTransform.sizeDelta = new Vector2(16f, 4.5f);
            _honesty.raycastTarget = false;
            _honesty.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _honesty.font = font;
        }

        Vector3 HonestyOffset()
        {
            return new Vector3(0f, 0.18f, 0f);
        }

        void UpdateHonesty()
        {
            if (_honesty == null)
                return;
            string extra = "";
            ScaleLevel L = (ScaleLevel)_scale;
            MuscleCell bio = GetComponent<MuscleCell>();
            HeatCoupler heat = GetComponent<HeatCoupler>();
            HeatReservoir reservoir = GetComponent<HeatReservoir>();
            if (bio != null)
            {
                extra = "\n" + BioScale.RepresentationOf(L) + "\n" + BioEnergy.Honesty;
                if (L == ScaleLevel.Molecular)
                    extra += "\nNot molecular dynamics";
                else if (L == ScaleLevel.Atomic)
                    extra += "\nElectronic structure not simulated";
            }
            else if (heat != null || reservoir != null)
            {
                extra = "\n" + ThermoEnergy.Honesty;
                if (L == ScaleLevel.Molecular || L == ScaleLevel.Atomic)
                    extra += "\nnot a molecular sim";
                else if (L == ScaleLevel.Material)
                    extra += "\nhot / cold bodies";
            }
            else if (GetComponent<KhufuSurvey>() != null || GetComponentInParent<KhufuSurvey>() != null || GetComponent<KhafreSurvey>() != null || GetComponentInParent<KhafreSurvey>() != null || GetComponent<MenkaureSurvey>() != null || GetComponentInParent<MenkaureSurvey>() != null || GetComponent<SphinxSurvey>() != null || GetComponentInParent<SphinxSurvey>() != null)
            {
                extra = "\n" + GizaComplex.HonestyPrefix;
                if (L == ScaleLevel.Molecular)
                    extra += "\nCalcite lattice schematic. Conceptual / Classical crystal, not XRD.";
                else if (L == ScaleLevel.Atomic)
                    extra += "\nCa, C, O cards. Not QM.";
                else if (L == ScaleLevel.Material)
                    extra += "\nCourse banding / block outlines near look-hit.";
            }
            else if (L == ScaleLevel.Molecular)
                extra = "\nNot molecular dynamics";
            else if (L == ScaleLevel.Atomic)
                extra = "\nElectronic structure not simulated";
            else if (L == ScaleLevel.Material)
                extra = "\nBulk Cu / NdFeB - same Faraday underneath";
            _honesty.text = "Scale " + ScaleEngine.NameOf(_scale) + "\n" + ScaleEngine.HonestyOf(_scale) + extra;
        }
    }
}
