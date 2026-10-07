using UnityEngine;

namespace RealityEngine.XR
{
    /// <summary>
    /// Headset comfort: which way the left stick walks you, chosen in Settings > "Walk direction".
    /// Head (default, same as before) walks where you look; Hand walks where the stick's own controller points,
    /// so you can look around (or down at a bench) while walking straight. Saved in PlayerPrefs and read every
    /// physics step by <see cref="SmoothMovementController"/>, so it applies live. If the controller points almost
    /// straight up or down (or is not tracked) walking falls back to head direction for that step.
    /// Desktop WASD (DesktopPlayerController) and the Device Simulator are unaffected.
    /// </summary>
    public static class WalkDirectionSettings
    {
        const string PrefKey = "qhysics.xr.walkDirection";
        public enum Mode { Head = 0, Hand = 1 }
        static int _mode = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _mode = -1; // re-read PlayerPrefs each Play (domain reload may be off)
        }

        public static Mode Current
        {
            get
            {
                if (_mode < 0)
                    _mode = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)Mode.Head), 0, 1);
                return (Mode)_mode;
            }
        }

        /// <summary>True when the left stick should walk along the controller's pointing direction.</summary>
        public static bool HandRelative => Current == Mode.Hand;

        public static string Label => HandRelative ? "Left hand" : "Head";

        /// <summary>Toggle Head <-> Left hand and save. Takes effect on the next physics step.</summary>
        public static void Cycle()
        {
            _mode = HandRelative ? (int)Mode.Head : (int)Mode.Hand;
            PlayerPrefs.SetInt(PrefKey, _mode);
            PlayerPrefs.Save();
            Debug.Log("WalkDirectionSettings: walk direction " + Label + ".");
        }
    }
}