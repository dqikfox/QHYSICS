using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using RealityEngine.Player;

namespace RealityEngine.XR
{
    /// <summary>
    /// Headset comfort: how the right stick turns you, chosen in Settings > "Turn style":
    /// Snap (default, same as before: 30 / 45 / 90 degree steps from <see cref="SnapTurnSettings"/>),
    /// Smooth 60/s or Smooth 120/s (continuous turning, faster the further you push the stick). Saved in PlayerPrefs, applies live.
    /// In Smooth the rig's snap-turn providers keep running but with left/right snapping switched off, so stick down still
    /// turns you around 180. Smooth turning pauses exactly when snap turning would: desktop (no XR display), lost head
    /// tracking, while a stick scrolls a list, while the toolbelt is open (right stick = carry slots), or when
    /// <see cref="LabPlayerSpawn"/> has disabled the snap provider. Uses unscaled time so sim speed (0.5x / 2x) and Focus
    /// slow-mo do not change how fast you turn. The comfort vignette already reacts to smooth yaw.
    /// Desktop WASD / mouse look and the Device Simulator's mouse look are unaffected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurnStyleSettings : MonoBehaviour
    {
        const string PrefKey = "qhysics.xr.turnStyle";
        static readonly float[] Rates = { 0f, 60f, 120f }; // degrees per second at full stick; 0 = snap
        static readonly string[] Names = { "Snap", "Smooth 60/s", "Smooth 120/s" };
        const float Deadzone = 0.2f;
        const float RescanInterval = 1f;

        static TurnStyleSettings _instance;
        static int _index = -1;

        XROrigin _origin;
#pragma warning disable CS0618
        UnityEngine.XR.Interaction.Toolkit.DeviceBasedSnapTurnProvider[] _legacy =
            new UnityEngine.XR.Interaction.Toolkit.DeviceBasedSnapTurnProvider[0];
#pragma warning restore CS0618
        UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider[] _modern =
            new UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider[0];
        float _nextScan;
        bool _suppressed;

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
            var host = new GameObject("QhysicsTurnStyle");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<TurnStyleSettings>();
            if (Smooth)
                Debug.Log("TurnStyleSettings: turn style " + Label + " (saved).");
        }

        static int Index
        {
            get
            {
                if (_index < 0)
                    _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Rates.Length - 1);
                return _index;
            }
        }

        /// <summary>True when the right stick turns continuously instead of snapping.</summary>
        public static bool Smooth => Index > 0;

        /// <summary>Smooth turn speed at full stick (0 in Snap).</summary>
        public static float DegreesPerSecond => Rates[Index];

        public static string Label => Names[Index];

        /// <summary>Step Snap -> Smooth 60/s -> Smooth 120/s -> Snap, save, and apply now.</summary>
        public static void Cycle()
        {
            _index = (Index + 1) % Rates.Length;
            PlayerPrefs.SetInt(PrefKey, _index);
            PlayerPrefs.Save();
            if (_instance != null)
            {
                _instance.Rescan();
                _instance.ApplySnapSuppression();
            }
            Debug.Log("TurnStyleSettings: turn style " + Label + (Smooth
                ? " (right stick turns smoothly; snap left/right off, stick down still turns around)."
                : " (right stick snap-turns " + SnapTurnSettings.Label + ")."));
        }

        void OnDisable()
        {
            if (Smooth || _suppressed)
                SetSnapLeftRight(true);
            _suppressed = false;
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextScan)
                Rescan();
            ApplySnapSuppression();
            if (!Smooth || _origin == null || !_origin.isActiveAndEnabled)
                return;
            if (!DesktopPlayerController.IsXrDisplayRunning() || !LabPlayerSpawn.HeadTrackingValid())
                return;
            if (RealityEngine.UI.QhysicsXrRayPolicy.StickScrollActive() || RealityEngine.UI.QhysicsXrRayPolicy.ToolbeltVisible())
                return;
            if (HasSnapProvider() && !AnySnapProviderEnabled())
                return; // LabPlayerSpawn paused turning (tracking pause / UI owns the stick)
            float x = ReadRightStickX();
            if (x == 0f)
                return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _origin.RotateAroundCameraUsingOriginUp(x * DegreesPerSecond * dt);
        }

        void Rescan()
        {
            _nextScan = Time.unscaledTime + RescanInterval;
            _origin = null;
            XROrigin[] origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include);
            for (int i = 0; i < origins.Length; i++)
            {
                if (origins[i] != null && origins[i].isActiveAndEnabled)
                {
                    _origin = origins[i];
                    break;
                }
            }
            if (_origin == null)
            {
#pragma warning disable CS0618
                _legacy = new UnityEngine.XR.Interaction.Toolkit.DeviceBasedSnapTurnProvider[0];
#pragma warning restore CS0618
                _modern = new UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider[0];
                return;
            }
            Transform root = _origin.transform;
#pragma warning disable CS0618
            _legacy = root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.DeviceBasedSnapTurnProvider>(true);
#pragma warning restore CS0618
            _modern = root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>(true);
        }

        /// <summary>Smooth: keep snap left/right off on every provider (re-asserted each frame, LabPlayerSpawn re-wires it on).
        /// Snap: put it back once if we turned it off.</summary>
        void ApplySnapSuppression()
        {
            if (Smooth)
            {
                SetSnapLeftRight(false);
                _suppressed = true;
            }
            else if (_suppressed)
            {
                SetSnapLeftRight(true);
                _suppressed = false;
            }
        }

        void SetSnapLeftRight(bool on)
        {
            for (int i = 0; i < _legacy.Length; i++)
            {
                if (_legacy[i] != null && _legacy[i].enableTurnLeftRight != on)
                    _legacy[i].enableTurnLeftRight = on;
            }
            for (int i = 0; i < _modern.Length; i++)
            {
                if (_modern[i] != null && _modern[i].enableTurnLeftRight != on)
                    _modern[i].enableTurnLeftRight = on;
            }
        }

        bool HasSnapProvider()
        {
            for (int i = 0; i < _legacy.Length; i++)
                if (_legacy[i] != null) return true;
            for (int i = 0; i < _modern.Length; i++)
                if (_modern[i] != null) return true;
            return false;
        }

        bool AnySnapProviderEnabled()
        {
            for (int i = 0; i < _legacy.Length; i++)
                if (_legacy[i] != null && _legacy[i].isActiveAndEnabled) return true;
            for (int i = 0; i < _modern.Length; i++)
                if (_modern[i] != null && _modern[i].isActiveAndEnabled) return true;
            return false;
        }

        /// <summary>Right stick X with a 0.2 deadzone (rescaled to 0..1). Mostly-vertical pushes (stick down = turn around) count as 0.</summary>
        static float ReadRightStickX()
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!d.isValid || !d.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 v))
                return 0f;
            float ax = Mathf.Abs(v.x);
            if (ax < Deadzone || Mathf.Abs(v.y) > ax)
                return 0f;
            return Mathf.Sign(v.x) * Mathf.Clamp01((ax - Deadzone) / (1f - Deadzone));
        }
    }
}