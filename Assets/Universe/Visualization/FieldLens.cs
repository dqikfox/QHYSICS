using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Visualization
{
    public enum FieldLensLayer
    {
        Normal = 0,
        Material = 1,
        Atomic = 2,
        Charge = 3,
        Electric = 4,
        Magnetic = 5,
        EnergyFlow = 6,
        Mathematical = 7
    }

    /// <summary>
    /// Reality Engine v0.4 Field Lens. Peels one representation layer at a time on aimed targets.
    /// Does not move the XR Origin camera. Visuals sample the live lumped Faraday sim.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(35)]
    public sealed class FieldLens : MonoBehaviour
    {
        public const int LayerCount = 8;

        public static readonly string[] LayerNames =
        {
            "Normal",
            "Material",
            "Atomic",
            "Charge distribution",
            "Electric field",
            "Magnetic field",
            "Energy flow",
            "Mathematical model"
        };

        public static readonly string[] HonestyTags =
        {
            "Classical model",
            "Classical model",
            "Conceptual visualization",
            "Conceptual visualization",
            "Classical model / Numerical sample",
            "Numerical sample",
            "Classical model",
            "Classical model"
        };

        [SerializeField]
        [Tooltip("Current Field Lens layer (0 Normal ... 7 Mathematical).")]
        int currentLayer;

        [SerializeField]
        [Tooltip("Editor/desktop: [ ] or N/P steps the lens. XR uses world buttons + hover/activate.")]
        bool enableKeyboard = true;

        readonly List<FieldLensTarget> _targets = new List<FieldLensTarget>(8);
        FieldLensTarget _focused;
        Camera _camera;
        InductionReadout _readout;
        ModelCard _modelCard;
        MathModelViz _math;
        float _nextGaze;

        public int CurrentLayer => currentLayer;
        public FieldLensLayer CurrentLayerEnum => (FieldLensLayer)Mathf.Clamp(currentLayer, 0, LayerCount - 1);
        public FieldLensTarget Focused => _focused;
        public string CurrentLayerName => LayerNames[Mathf.Clamp(currentLayer, 0, LayerCount - 1)];
        public string CurrentHonestyTag => HonestyTags[Mathf.Clamp(currentLayer, 0, LayerCount - 1)];

        public static string NameOf(int layer)
        {
            int i = Mathf.Clamp(layer, 0, LayerCount - 1);
            return LayerNames[i];
        }

        public static string HonestyOf(int layer)
        {
            int i = Mathf.Clamp(layer, 0, LayerCount - 1);
            return HonestyTags[i];
        }

        public void Register(FieldLensTarget target)
        {
            if (target == null)
                return;
            if (!_targets.Contains(target))
                _targets.Add(target);
            target.ApplyLayer(currentLayer);
        }

        public void Unregister(FieldLensTarget target)
        {
            if (target == null)
                return;
            _targets.Remove(target);
            if (_focused == target)
                _focused = null;
        }

        public void BindReadout(InductionReadout readout)
        {
            _readout = readout;
            if (_readout != null)
                _readout.SetFieldLens(this);
        }

        public void BindModelCard(ModelCard card)
        {
            _modelCard = card;
            PushHonestyToCard();
        }

        public void BindMath(MathModelViz math)
        {
            _math = math;
            if (_math != null)
                _math.Visible = currentLayer == (int)FieldLensLayer.Mathematical;
        }

        public void Focus(FieldLensTarget target)
        {
            _focused = target;
        }

        public void StepNext()
        {
            SetLayer(currentLayer + 1);
        }

        public void StepPrevious()
        {
            SetLayer(currentLayer - 1);
        }

        public void SetLayer(int layer)
        {
            int wrapped = layer % LayerCount;
            if (wrapped < 0)
                wrapped += LayerCount;
            currentLayer = wrapped;
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] != null)
                    _targets[i].ApplyLayer(currentLayer);
            }

            if (_math != null)
                _math.Visible = currentLayer == (int)FieldLensLayer.Mathematical;
            PushHonestyToCard();
        }

        void Awake()
        {
            _camera = Camera.main;
        }

        void Update()
        {
            HandleKeyboard();
            if (Time.unscaledTime >= _nextGaze)
            {
                _nextGaze = Time.unscaledTime + 0.05f;
                UpdateFocus();
            }
        }

        void HandleKeyboard()
        {
            if (!enableKeyboard)
                return;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[Key.RightBracket].wasPressedThisFrame || kb.nKey.wasPressedThisFrame)
                    StepNext();
                if (kb[Key.LeftBracket].wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
                    StepPrevious();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.N))
                StepNext();
            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.P))
                StepPrevious();
#endif
        }

        void UpdateFocus()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                FieldLensTarget t = _targets[i];
                if (t != null && t.IsXrHovered)
                {
                    _focused = t;
                    return;
                }
            }

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            Vector3 origin = _camera.transform.position;
            Vector3 fwd = _camera.transform.forward;
            RaycastHit hit;
            if (Physics.Raycast(origin, fwd, out hit, 400f, ~0, QueryTriggerInteraction.Ignore))
            {
                FieldLensTarget rayTarget = hit.collider.GetComponentInParent<FieldLensTarget>();
                if (rayTarget != null)
                {
                    _focused = rayTarget;
                    return;
                }
            }

            FieldLensTarget best = null;
            float bestDot = 0.82f;
            for (int i = 0; i < _targets.Count; i++)
            {
                FieldLensTarget t = _targets[i];
                if (t == null)
                    continue;
                Vector3 to = t.transform.position - origin;
                float dist = to.magnitude;
                if (dist < 0.05f || dist > 3.5f)
                    continue;
                float dot = Vector3.Dot(fwd, to / dist);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = t;
                }
            }

            if (best != null)
                _focused = best;
        }

        void PushHonestyToCard()
        {
            if (_modelCard == null)
                return;
            _modelCard.SetLayerLine(
                "Field Lens: " + CurrentLayerName + "  [" + CurrentHonestyTag + "]");
        }
    }
}