using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using RealityEngine.Visualization;

namespace RealityEngine.Core
{
    public enum ScaleLevel
    {
        Human = 0,
        Material = 1,
        Molecular = 2,
        Atomic = 3
    }

    /// <summary>
    /// Reality Engine v0.5 Scale Engine. Changes which physical MODEL is appropriate as the player
    /// steps scale. Not camera zoom. Never parents or moves XR Origin / main camera.
    /// Human-scale Faraday sim (magnet grab + coil flux) keeps running underneath.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(30)]
    public sealed class ScaleEngine : MonoBehaviour
    {
        public const int StepCount = 4;

        public static readonly string[] StepNames =
        {
            "Human",
            "Material",
            "Molecular",
            "Atomic"
        };

        public static readonly string[] HonestyTags =
        {
            "Classical model",
            "Classical model",
            "Conceptual visualization",
            "Conceptual visualization"
        };

        public static readonly string[] EquationsInForce =
        {
            "IN FORCE: EMF = -N dPhi/dt ; I = EMF/R ; two-pole B",
            "IN FORCE: EMF = -N dPhi/dt ; I = EMF/R ; bulk copper resistivity ; NdFeB as bulk magnet",
            "IN FORCE (underneath): lumped Faraday + two-pole B. VISUAL: conceptual lattice / molecular schematic",
            "IN FORCE (underneath): lumped Faraday + two-pole B. VISUAL: conceptual atoms / charge"
        };

        public static readonly string[] EquationsParked =
        {
            "PARKED: material microstructure, molecular dynamics, electronic structure",
            "PARKED: molecular dynamics, electronic structure / QM",
            "PARKED: molecular dynamics is not solved — schematic only",
            "PARKED: electronic structure not simulated — conceptual. Do not claim QM"
        };

        [SerializeField]
        [Tooltip("Current scale step (0 Human ... 3 Atomic). Down from human; not camera zoom.")]
        int currentScale;

        [SerializeField]
        [Tooltip("Editor/desktop: -/= or ,/. steps scale. XR uses world Scale + / - buttons.")]
        bool enableKeyboard = true;

        readonly List<ScaleAwareTarget> _targets = new List<ScaleAwareTarget>(8);
        InductionReadout _readout;
        ModelCard _modelCard;

        public event Action<int> ScaleChanged;

        public int CurrentScale => currentScale;
        public ScaleLevel CurrentScaleEnum => (ScaleLevel)Mathf.Clamp(currentScale, 0, StepCount - 1);
        public string CurrentScaleName => NameOf(currentScale);
        public string CurrentHonestyTag => HonestyOf(currentScale);
        public string CurrentInForce => InForceOf(currentScale);
        public string CurrentParked => ParkedOf(currentScale);

        public static string NameOf(int scale)
        {
            int i = Mathf.Clamp(scale, 0, StepCount - 1);
            return StepNames[i];
        }

        public static string HonestyOf(int scale)
        {
            int i = Mathf.Clamp(scale, 0, StepCount - 1);
            return HonestyTags[i];
        }

        public static string InForceOf(int scale)
        {
            int i = Mathf.Clamp(scale, 0, StepCount - 1);
            return EquationsInForce[i];
        }

        public static string ParkedOf(int scale)
        {
            int i = Mathf.Clamp(scale, 0, StepCount - 1);
            return EquationsParked[i];
        }

        public void Register(ScaleAwareTarget target)
        {
            if (target == null)
                return;
            if (!_targets.Contains(target))
                _targets.Add(target);
            target.ApplyScale(currentScale);
        }

        public void Unregister(ScaleAwareTarget target)
        {
            if (target == null)
                return;
            _targets.Remove(target);
        }

        public void BindReadout(InductionReadout readout)
        {
            _readout = readout;
            if (_readout != null)
                _readout.SetScaleEngine(this);
        }

        public void BindModelCard(ModelCard card)
        {
            _modelCard = card;
            PushHonestyToCard();
        }

        public void StepIn()
        {
            SetScale(currentScale + 1);
        }

        public void StepOut()
        {
            SetScale(currentScale - 1);
        }

        public void SetScale(int scale)
        {
            int clamped = Mathf.Clamp(scale, 0, StepCount - 1);
            currentScale = clamped;
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] != null)
                    _targets[i].ApplyScale(currentScale);
            }

            PushHonestyToCard();
            ScaleChanged?.Invoke(currentScale);
        }

        void Update()
        {
            HandleKeyboard();
        }

        void HandleKeyboard()
        {
            if (!enableKeyboard)
                return;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.equalsKey.wasPressedThisFrame || kb.periodKey.wasPressedThisFrame)
                    StepIn();
                if (kb.minusKey.wasPressedThisFrame || kb.commaKey.wasPressedThisFrame)
                    StepOut();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Period))
                StepIn();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Comma))
                StepOut();
#endif
        }

        void PushHonestyToCard()
        {
            if (_modelCard == null)
                return;
            _modelCard.SetScaleLine(
                "Scale Engine: " + CurrentScaleName + "  [" + CurrentHonestyTag + "]\n" +
                CurrentInForce + "\n" + CurrentParked);
        }
    }
}
