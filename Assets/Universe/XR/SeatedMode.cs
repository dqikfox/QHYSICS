using UnityEngine;
using Unity.XR.CoreUtils;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.XR
{
    /// <summary>
    /// Headset comfort / accessibility: "Seated boost" raises your viewpoint and hands by a fixed amount so you can
    /// play QHYSICS sitting down (or with limited mobility) and still see over the lab benches and reach the toolbelt,
    /// gadgets and weapons at standing height. Settings > "Seated boost" cycles Off (default) / +30 cm / +50 cm,
    /// saved in PlayerPrefs, applied immediately (with a teleport blink, if that is on, to hide the height pop).
    /// How: adds the boost to the XR Origin's Camera Offset local Y every LateUpdate while an XR display is running
    /// with Floor tracking, so the head, both controllers and the body capsule move up together. When switched Off it
    /// puts Camera Offset back to 0 once and then leaves it alone. Desktop and the Device Simulator (no XR display)
    /// are never touched, so the desktop eye height / crouch logic is unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9990)] // after LabPlayerSpawn / DesktopPlayerController floor resets, before TeleportBlink
    public sealed class SeatedMode : MonoBehaviour
    {
        const string PrefKey = "qhysics.xr.seatedBoostM";
        static readonly float[] Presets = { 0f, 0.30f, 0.50f };

        static SeatedMode _instance;
        static float _boost = -1f;

        XROrigin _origin;
        Transform _offset;
        float _nextResolve;
        bool _applied;      // we have written a non-zero boost into Camera Offset
        bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _boost = -1f; // re-read PlayerPrefs each Play (domain reload may be off)
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsSeatedMode");
            _instance = host.AddComponent<SeatedMode>();
        }

        /// <summary>Current boost in metres (0 = off).</summary>
        public static float BoostM
        {
            get
            {
                if (_boost < 0f)
                    _boost = Nearest(PlayerPrefs.GetFloat(PrefKey, 0f));
                return _boost;
            }
        }

        public static string Label
        {
            get
            {
                float b = BoostM;
                return b <= 0.001f ? "Off" : "+" + Mathf.RoundToInt(b * 100f) + " cm";
            }
        }

        /// <summary>Step Off -> +30 cm -> +50 cm -> Off, save, and apply on the next frame.</summary>
        public static void Cycle()
        {
            float cur = BoostM;
            int idx = 0;
            for (int i = 0; i < Presets.Length; i++)
            {
                if (Mathf.Approximately(Presets[i], cur)) { idx = i; break; }
            }
            _boost = Presets[(idx + 1) % Presets.Length];
            PlayerPrefs.SetFloat(PrefKey, _boost);
            PlayerPrefs.Save();
            if (DesktopPlayerController.IsXrDisplayRunning())
                TeleportBlink.Blink(); // hide the vertical pop (no-op if Teleport blink is Off)
            Debug.Log("SeatedMode: seated boost " + Label
                + (DesktopPlayerController.IsXrDisplayRunning() ? "" : " (saved; applies in the headset only)") + ".");
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

        void LateUpdate()
        {
            if (!DesktopPlayerController.IsXrDisplayRunning())
            {
                _applied = false; // desktop owns Camera Offset height (eye height / crouch)
                return;
            }

            if ((_origin == null || _offset == null) && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.5f;
                Resolve();
            }
            if (_offset == null)
                return;
            if (_origin != null && _origin.CurrentTrackingOriginMode != UnityEngine.XR.TrackingOriginModeFlags.Floor
                && _origin.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Floor)
                return; // Device mode uses CameraYOffset; do not stack a boost on top

            float boost = BoostM;
            Vector3 lp = _offset.localPosition;
            if (boost > 0.001f)
            {
                if (Mathf.Abs(lp.y - boost) > 0.001f)
                {
                    lp.y = boost;
                    _offset.localPosition = lp;
                }
                _applied = true;
                if (!_logged)
                {
                    _logged = true;
                    Debug.Log("SeatedMode: applying seated boost " + Label + " to " + _offset.name + ".");
                }
            }
            else if (_applied)
            {
                // Switched Off: hand Floor tracking back (Camera Offset 0) once, then stop touching it.
                lp.y = 0f;
                _offset.localPosition = lp;
                _applied = false;
            }
        }

        void Resolve()
        {
            XROrigin origin = null;
            GameObject named = GameObject.Find(LabPlayerSpawn.OriginName);
            if (named != null)
                origin = named.GetComponent<XROrigin>();
            if (origin == null)
            {
                Camera cam = QhysicsUiBuilder.ResolveXrCamera();
                if (cam != null)
                    origin = cam.GetComponentInParent<XROrigin>();
            }
            if (origin == null)
                origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
                return;
            _origin = origin;
            Transform off = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
            if (off == null)
                off = origin.transform.Find(LabPlayerSpawn.CameraOffsetName);
            _offset = off;
        }

        static float Nearest(float v)
        {
            float best = 0f;
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