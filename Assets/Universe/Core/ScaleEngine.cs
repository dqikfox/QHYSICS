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
        Solar = -3,
        Planetary = -2,
        Room = -1,
        Human = 0,
        Material = 1,
        Molecular = 2,
        Atomic = 3
    }

    /// <summary>
    /// Reality Engine Scale Engine. Changes which physical MODEL is appropriate as the player
    /// steps scale. Not camera zoom. Never parents or moves XR Origin / main camera.
    /// Human-scale Faraday sim (magnet grab + coil flux) keeps running underneath.
    /// Cosmos levels (Room / Planetary / Solar) are classical tabletop toys — schematic only.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(30)]
    public sealed class ScaleEngine : MonoBehaviour
    {
        /// <summary>Micro-only step count (Human..Atomic). Cosmos uses negative scales; do not raise this.</summary>
        public const int StepCount = 4;
        public const int MinScale = -3;
        public const int MaxScale = 3;

        [SerializeField]
        [Tooltip("Current scale step (-3 Solar ... 0 Human ... 3 Atomic). Model switch; not camera zoom.")]
        int currentScale;

        [SerializeField]
        [Tooltip("Editor/desktop: -/= or ,/. steps scale. XR uses world Scale + / - buttons.")]
        bool enableKeyboard = true;

        readonly List<ScaleAwareTarget> _targets = new List<ScaleAwareTarget>(8);
        InductionReadout _readout;
        ModelCard _modelCard;

        public event Action<int> ScaleChanged;

        public int CurrentScale => currentScale;
        public ScaleLevel CurrentScaleEnum => (ScaleLevel)ClampScale(currentScale);
        public string CurrentScaleName => NameOf(currentScale);
        public string CurrentHonestyTag => HonestyOf(currentScale);
        public string CurrentInForce => InForceOf(currentScale);
        public string CurrentParked => ParkedOf(currentScale);

        public static int ClampScale(int s) => Mathf.Clamp(s, MinScale, MaxScale);
        public static int ClampMicro(int s) => Mathf.Clamp(s, 0, StepCount - 1);
        public static bool IsCosmos(int s) => s < 0;

        public static string NameOf(int scale)
        {
            switch ((ScaleLevel)ClampScale(scale))
            {
                case ScaleLevel.Solar: return "Solar";
                case ScaleLevel.Planetary: return "Planetary";
                case ScaleLevel.Room: return "Room";
                case ScaleLevel.Human: return "Human";
                case ScaleLevel.Material: return "Material";
                case ScaleLevel.Molecular: return "Molecular";
                case ScaleLevel.Atomic: return "Atomic";
                default: return "Human";
            }
        }

        public static string HonestyOf(int scale)
        {
            switch ((ScaleLevel)ClampScale(scale))
            {
                case ScaleLevel.Solar: return "Classical Kepler toy";
                case ScaleLevel.Planetary: return "Classical geographic toy";
                case ScaleLevel.Room: return "Classical room schematic";
                case ScaleLevel.Human: return "Classical model";
                case ScaleLevel.Material: return "Classical model";
                case ScaleLevel.Molecular: return "Conceptual visualization";
                case ScaleLevel.Atomic: return "Conceptual visualization";
                default: return "Classical model";
            }
        }

        public static string InForceOf(int scale)
        {
            switch ((ScaleLevel)ClampScale(scale))
            {
                case ScaleLevel.Solar:
                    return "IN FORCE: toy Kepler orbit (Sun + Earth schematic). Not N-body / not GR";
                case ScaleLevel.Planetary:
                    return "IN FORCE: geographic toy Earth + Giza marker schematic";
                case ScaleLevel.Room:
                    return "IN FORCE: lab table + local objects schematic";
                case ScaleLevel.Human:
                    return "IN FORCE: EMF = -N dPhi/dt ; I = EMF/R ; two-pole B";
                case ScaleLevel.Material:
                    return "IN FORCE: EMF = -N dPhi/dt ; I = EMF/R ; bulk copper resistivity ; NdFeB as bulk magnet";
                case ScaleLevel.Molecular:
                    return "IN FORCE (underneath): lumped Faraday + two-pole B. VISUAL: conceptual lattice / molecular schematic";
                case ScaleLevel.Atomic:
                    return "IN FORCE (underneath): lumped Faraday + two-pole B. VISUAL: conceptual atoms / charge";
                default:
                    return "IN FORCE: EMF = -N dPhi/dt ; I = EMF/R ; two-pole B";
            }
        }

        public static string ParkedOf(int scale)
        {
            switch ((ScaleLevel)ClampScale(scale))
            {
                case ScaleLevel.Solar:
                    return "PARKED: N-body, relativity, real ephemeris";
                case ScaleLevel.Planetary:
                    return "PARKED: geodesy / atmosphere / N-body";
                case ScaleLevel.Room:
                    return "PARKED: full architectural sim";
                case ScaleLevel.Human:
                    return "PARKED: material microstructure, molecular dynamics, electronic structure";
                case ScaleLevel.Material:
                    return "PARKED: molecular dynamics, electronic structure / QM";
                case ScaleLevel.Molecular:
                    return "PARKED: molecular dynamics is not solved — schematic only";
                case ScaleLevel.Atomic:
                    return "PARKED: electronic structure not simulated — conceptual. Do not claim QM";
                default:
                    return "PARKED: material microstructure, molecular dynamics, electronic structure";
            }
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
            int clamped = ClampScale(scale);
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
