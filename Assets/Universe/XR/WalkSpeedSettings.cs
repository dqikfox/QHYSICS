using UnityEngine;
using Unity.XR.CoreUtils;

namespace RealityEngine.XR
{
    /// <summary>
    /// Headset comfort: left-stick walk speed chosen in Settings > "Walk speed" (Slow 1.2 / Normal 2.0 default /
    /// Fast 3.0 m/s), saved in PlayerPrefs and applied live to every <see cref="SmoothMovementController"/> under the
    /// XR Origin(s). Slow is gentler for new or motion-sensitive players and fine bench work; Fast covers the Giza
    /// plaza and arena quicker. <see cref="LabPlayerSpawn"/> uses <see cref="Speed"/> when it wires locomotion.
    /// Desktop WASD (DesktopPlayerController) and the Device Simulator are unaffected.
    /// </summary>
    public static class WalkSpeedSettings
    {
        const string PrefKey = "qhysics.xr.walkSpeedMps";
        public const float DefaultSpeed = 2.0f;
        static readonly float[] Presets = { 1.2f, 2.0f, 3.0f };
        static readonly string[] Names = { "Slow", "Normal", "Fast" };
        static float _speed = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _speed = -1f; // re-read PlayerPrefs each Play (domain reload may be off)
        }

        /// <summary>Current stick-walk speed in metres per second.</summary>
        public static float Speed
        {
            get
            {
                if (_speed < 0f)
                    _speed = Presets[NearestIndex(PlayerPrefs.GetFloat(PrefKey, DefaultSpeed))];
                return _speed;
            }
        }

        public static string Label
        {
            get
            {
                int i = NearestIndex(Speed);
                return Names[i] + " (" + Presets[i].ToString("0.0") + " m/s)";
            }
        }

        /// <summary>Step Slow -> Normal -> Fast -> Slow, save, and apply to the live rig right away.</summary>
        public static void Cycle()
        {
            int idx = (NearestIndex(Speed) + 1) % Presets.Length;
            _speed = Presets[idx];
            PlayerPrefs.SetFloat(PrefKey, _speed);
            PlayerPrefs.Save();
            int n = ApplyToScene();
            Debug.Log("WalkSpeedSettings: walk speed " + Label + " (" + n + " move provider(s) updated).");
        }

        /// <summary>Push the saved speed onto every stick-walk provider under the XR Origin(s). Returns how many were set.</summary>
        public static int ApplyToScene()
        {
            int count = 0;
            float s = Speed;
            XROrigin[] origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include);
            for (int o = 0; o < origins.Length; o++)
            {
                if (origins[o] == null)
                    continue;
                var moves = origins[o].GetComponentsInChildren<SmoothMovementController>(true);
                for (int i = 0; i < moves.Length; i++)
                {
                    if (moves[i] == null) continue;
                    moves[i].speed = s;
                    count++;
                }
            }
            return count;
        }

        static int NearestIndex(float v)
        {
            int best = 1;
            float bestD = float.MaxValue;
            for (int i = 0; i < Presets.Length; i++)
            {
                float d = Mathf.Abs(Presets[i] - v);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}