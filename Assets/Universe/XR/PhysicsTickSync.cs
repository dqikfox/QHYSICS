using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace RealityEngine.XR
{
    /// <summary>
    /// Headset grab feel / Quest CPU: keeps the physics tick in step with the headset's display refresh rate.
    /// The project's Fixed Timestep is 1/90 s, but Quest usually runs at 72 Hz (Link / Air Link can be 72, 80, 90 or
    /// 120 Hz). A mismatch gives one physics step on some frames and two on others, so held weapons, physics hands,
    /// gadgets and ragdolls judder slightly against the smooth controller pose, and 90 steps for a 72 Hz display also
    /// wastes Quest CPU. This reads the running XR display's refresh rate (re-checked every 2 s, clamped 60-120 Hz) and
    /// uses one physics step per displayed frame; desktop / no XR keeps the project's own step.
    /// Sim speed: the step is scaled with Time.timeScale (base * timeScale), so slow sim speeds (0.1x, the sim chip,
    /// Focus slow-mo) still get one physics step per displayed frame instead of visibly stepping at a few Hz, and fast
    /// speeds keep the same steps per real second. Paused (timeScale 0) is left alone. Focus slow-mo already scales the
    /// step the same way, so the two agree instead of fighting.
    /// The project's TimeManager value is captured at start and put back when Play stops; nothing is saved to disk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhysicsTickSync : MonoBehaviour
    {
        const float MinHz = 60f, MaxHz = 120f;
        const float MinStep = 0.0005f, MaxStep = 0.05f;
        const float CheckInterval = 2f;

        static PhysicsTickSync _instance;
        static float _projectStep = -1f;
        static float _displayHz;
        static bool _logged;
        static bool _quitHooked;
        static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null; // domain reload may be off
            _displayHz = 0f;
            _logged = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CaptureProjectStep()
        {
            // Captured once, before anything in the scene can change it (kept across Plays when domain reload is off).
            if (_projectStep <= 0f)
                _projectStep = Time.fixedDeltaTime;
            if (!_quitHooked)
            {
                _quitHooked = true;
                Application.quitting += RestoreProjectStep;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsPhysicsTick");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PhysicsTickSync>();
            _instance.RefreshDisplayRate();
            ApplyNow();
        }

        static void RestoreProjectStep()
        {
            if (_projectStep > 0f)
                Time.fixedDeltaTime = _projectStep;
        }

        /// <summary>Headset refresh rate the physics tick follows (0 = no XR display; project step in use).</summary>
        public static float DisplayHz => _displayHz;

        /// <summary>Physics step in real seconds at sim speed 1x: one step per headset frame in XR, else the project's Fixed Timestep.</summary>
        public static float BaseStep
        {
            get
            {
                if (_displayHz > 0f)
                    return 1f / _displayHz;
                return _projectStep > 0f ? _projectStep : 1f / 90f;
            }
        }

        /// <summary>Time.fixedDeltaTime to use at a given timeScale (keeps one physics step per displayed frame).</summary>
        public static float StepFor(float timeScale)
        {
            if (timeScale <= 0f)
                return BaseStep;
            return Mathf.Clamp(BaseStep * timeScale, MinStep, MaxStep);
        }

        /// <summary>Short label for status lines, e.g. "72 Hz" or "project 90 Hz".</summary>
        public static string Label => _displayHz > 0f
            ? Mathf.RoundToInt(_displayHz) + " Hz"
            : "project " + Mathf.RoundToInt(1f / BaseStep) + " Hz";

        void Update()
        {
            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + CheckInterval;
                RefreshDisplayRate();
            }
            ApplyNow();
        }

        static void ApplyNow()
        {
            float ts = Time.timeScale;
            if (ts <= 0f)
                return; // paused: no steps run, leave it for whoever paused
            float want = StepFor(ts);
            if (Mathf.Abs(Time.fixedDeltaTime - want) > want * 0.002f)
                Time.fixedDeltaTime = want;
        }

        void RefreshDisplayRate()
        {
            float hz = 0f;
            Displays.Clear();
            SubsystemManager.GetSubsystems(Displays);
            for (int i = 0; i < Displays.Count; i++)
            {
                var d = Displays[i];
                if (d != null && d.running && d.TryGetDisplayRefreshRate(out float r) && r > 1f)
                {
                    hz = Mathf.Clamp(r, MinHz, MaxHz);
                    break;
                }
            }
            if (_logged && Mathf.Abs(hz - _displayHz) < 0.5f)
                return;
            _displayHz = hz;
            _logged = true;
            float step = BaseStep;
            if (hz > 0f)
                Debug.Log("PhysicsTickSync: physics step " + (step * 1000f).ToString("0.0") + " ms (one step per frame of the "
                    + Mathf.RoundToInt(hz) + " Hz headset display; scaled with sim speed).");
            else
                Debug.Log("PhysicsTickSync: no XR display running; physics step stays at the project's "
                    + (step * 1000f).ToString("0.0") + " ms (scaled with sim speed).");
        }
    }
}