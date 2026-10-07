using UnityEngine;
using Unity.XR.CoreUtils;

namespace RealityEngine.XR
{
    /// <summary>
    /// Headset comfort: snap-turn angle chosen in Settings > "Snap turn" (30 / 45 default / 90 degrees), saved in PlayerPrefs.
    /// Applies to every snap-turn provider under the XR Origin: the legacy DeviceBasedSnapTurnProvider that
    /// <see cref="LabPlayerSpawn"/> wires and any XRI 3 SnapTurnProvider. Turn-around (180) is unchanged.
    /// Desktop and the Device Simulator mouse look are unaffected (they do not use snap turn).
    /// </summary>
    public static class SnapTurnSettings
    {
        const string PrefKey = "qhysics.xr.snapTurnDegrees";
        public const float DefaultDegrees = 45f;
        static readonly float[] Presets = { 30f, 45f, 90f };
        static float _degrees = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _degrees = -1f; // re-read PlayerPrefs each Play (domain reload may be off)
        }

        public static float Degrees
        {
            get
            {
                if (_degrees < 0f)
                {
                    float saved = PlayerPrefs.GetFloat(PrefKey, DefaultDegrees);
                    _degrees = Nearest(saved);
                }
                return _degrees;
            }
        }

        public static string Label => Mathf.RoundToInt(Degrees) + " deg";

        /// <summary>Step 30 -> 45 -> 90 -> 30, save, and apply to the live rig right away.</summary>
        public static void Cycle()
        {
            float cur = Degrees;
            int idx = 0;
            for (int i = 0; i < Presets.Length; i++)
            {
                if (Mathf.Approximately(Presets[i], cur)) { idx = i; break; }
            }
            _degrees = Presets[(idx + 1) % Presets.Length];
            PlayerPrefs.SetFloat(PrefKey, _degrees);
            PlayerPrefs.Save();
            int n = ApplyToScene();
            Debug.Log("SnapTurnSettings: snap turn " + Label + " (" + n + " provider(s) updated).");
        }

        /// <summary>Push the saved angle onto every snap-turn provider under the XR Origin(s). Returns how many were set.</summary>
        public static int ApplyToScene()
        {
            int count = 0;
            float deg = Degrees;
            XROrigin[] origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include);
            for (int o = 0; o < origins.Length; o++)
            {
                if (origins[o] == null)
                    continue;
                Transform root = origins[o].transform;
#pragma warning disable CS0618
                var legacy = root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.DeviceBasedSnapTurnProvider>(true);
                for (int i = 0; i < legacy.Length; i++)
                {
                    if (legacy[i] == null) continue;
                    legacy[i].turnAmount = deg;
                    count++;
                }
#pragma warning restore CS0618
                var modern = root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>(true);
                for (int i = 0; i < modern.Length; i++)
                {
                    if (modern[i] == null) continue;
                    modern[i].turnAmount = deg;
                    count++;
                }
            }
            return count;
        }

        static float Nearest(float v)
        {
            float best = DefaultDegrees;
            float bestD = float.MaxValue;
            for (int i = 0; i < Presets.Length; i++)
            {
                float d = Mathf.Abs(Presets[i] - v);
                if (d < bestD) { bestD = d; best = Presets[i]; }
            }
            return best;
        }
    }
}