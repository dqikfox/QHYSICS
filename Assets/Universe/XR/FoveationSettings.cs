using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace RealityEngine.XR
{
    /// <summary>
    /// Quest performance: fixed foveated rendering. The OpenXR "Foveated Rendering" feature is already enabled for
    /// Android, but nothing ever set a level, so the headset rendered the blurry lens edges at full resolution.
    /// This drives <see cref="XRDisplaySubsystem.foveatedRenderingLevel"/> on every running XR display so the GPU
    /// shades the outer edges of each eye at lower resolution (where the Quest 3S lenses are soft anyway) and spends
    /// the saved time on frame rate. Settings > "Foveation" cycles Off / Low / Medium (default) / High, saved in
    /// PlayerPrefs and applied live. Quest 3S has no eye tracking, so this is fixed (lens-centred) foveation only;
    /// gaze is never requested. Desktop and the Device Simulator have no XR display and are untouched; over Quest Link
    /// the PC runtime may ignore it (Link streams a full frame), so the real win is in an Android build on the headset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoveationSettings : MonoBehaviour
    {
        const string PrefKey = "qhysics.xr.foveationLevel";
        static readonly float[] Levels = { 0f, 0.33f, 0.66f, 1f };
        static readonly string[] Names = { "Off", "Low", "Medium", "High" };
        const int DefaultIndex = 2;

        static FoveationSettings _instance;
        static int _index = -1;
        static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        float _nextCheck;
        bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _index = -1; // re-read PlayerPrefs each Play (domain reload may be off)
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsFoveation");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<FoveationSettings>();
        }

        static int Index
        {
            get
            {
                if (_index < 0)
                    _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, DefaultIndex), 0, Levels.Length - 1);
                return _index;
            }
        }

        /// <summary>Requested level 0..1 (0 = off).</summary>
        public static float Level => Levels[Index];

        public static string Label => Names[Index];

        /// <summary>Step Off -> Low -> Medium -> High -> Off, save, and apply now.</summary>
        public static void Cycle()
        {
            _index = (Index + 1) % Levels.Length;
            PlayerPrefs.SetInt(PrefKey, _index);
            PlayerPrefs.Save();
            int applied = Apply();
            Debug.Log("FoveationSettings: foveation " + Label + " (level " + Level.ToString("0.00") + ")"
                + (applied > 0 ? " on " + applied + " XR display(s)." : " (saved; applies in the headset only)."));
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            // The XR display can start after scene load (and restart on Link reconnect), so keep re-asserting cheaply.
            if (Time.unscaledTime < _nextCheck)
                return;
            _nextCheck = Time.unscaledTime + 1f;
            int applied = Apply();
            if (applied > 0 && !_logged)
            {
                _logged = true;
                Debug.Log("FoveationSettings: fixed foveated rendering " + Label + " (level " + Level.ToString("0.00")
                    + ") on " + applied + " XR display(s).");
            }
        }

        /// <summary>Writes the current level to every running XR display. Returns how many were running.</summary>
        static int Apply()
        {
            Displays.Clear();
            SubsystemManager.GetSubsystems(Displays);
            int running = 0;
            float level = Level;
            for (int i = 0; i < Displays.Count; i++)
            {
                XRDisplaySubsystem d = Displays[i];
                if (d == null || !d.running)
                    continue;
                running++;
                if (d.foveatedRenderingFlags != XRDisplaySubsystem.FoveatedRenderingFlags.None)
                    d.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None; // fixed only, no gaze
                if (!Mathf.Approximately(d.foveatedRenderingLevel, level))
                    d.foveatedRenderingLevel = level;
            }
            return running;
        }
    }
}