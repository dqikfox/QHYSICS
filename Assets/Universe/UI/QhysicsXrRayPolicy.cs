using UnityEngine;

namespace RealityEngine.UI
{
    /// <summary>
    /// XR ray visibility policy. The scene's only UI-capable XRRayInteractors are the teleport rays, and
    /// TeleportationController hides them unless A/X is held. While any menu is up we keep them visible so
    /// the boot menu, pause menu, toolbelt, challenge list and skills panel can be clicked with the trigger.
    /// </summary>
    public static class QhysicsXrRayPolicy
    {
        static QhysicsToolbelt _toolbelt;
        static float _nextToolbeltLookup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            TeleportationController.ForceVisible = AnyMenuOpen;
        }

        public static bool AnyMenuOpen()
        {
            if (QhysicsUiState.BootMenuOpen || QhysicsModal.AnyOpen || QhysicsPausePanel.AnyOpen || QhysicsControlsOverlay.IsOpen)
                return true;
            if (RealityEngine.Challenges.ChallengeUi.IsListOpen || RealityEngine.Combat.SkillsPanel.IsOpen)
                return true;
            return ToolbeltVisible();
        }

        public static bool ToolbeltVisible()
        {
            if (_toolbelt == null && Time.unscaledTime >= _nextToolbeltLookup)
            {
                _nextToolbeltLookup = Time.unscaledTime + 2f;
                _toolbelt = Object.FindAnyObjectByType<QhysicsToolbelt>(FindObjectsInactive.Include);
            }
            return _toolbelt != null && _toolbelt.IsVisible;
        }

        /// <summary>Lists that the thumbsticks scroll: suppress smooth move / snap turn while open.</summary>
        public static bool StickScrollActive()
        {
            return QhysicsUiScrollGate.IsCaptured || RealityEngine.Challenges.ChallengeUi.IsListOpen
                || RealityEngine.Combat.SkillsPanel.IsOpen;
        }
    }
}
