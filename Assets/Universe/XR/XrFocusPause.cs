using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using RealityEngine.Combat;

namespace RealityEngine.XR
{
    /// <summary>
    /// Headset comfort / Meta Quest guideline: freezes the sim while the headset does not have input focus.
    /// When the Meta system menu (Meta button), the Link dashboard, a system dialog or a guardian prompt takes over,
    /// or the headset is taken off over Link, the OpenXR session leaves the FOCUSED state: the app keeps drawing but
    /// gets no controller input. Until now the sim kept running behind the menu (enemies kept attacking, gadgets kept
    /// moving, timers ran out). This watches OpenXRUtility.IsSessionFocused (10x/s) and, after 0.25 s without focus,
    /// sets Time.timeScale to 0 and pauses audio (AudioListener.pause); when focus comes back it restores the previous
    /// sim speed and audio state.
    /// Safe by design: only acts while the OpenXR loader is the active XR loader and its display is running (desktop,
    /// XR Device Simulator and other loaders are never touched), and only trusts "not focused" after the session has
    /// been focused at least once, so a runtime that never reports focus cannot leave the game frozen. Focus slow-mo
    /// is ended first so its 0.35x does not stick. On return the old speed is only put back if time is still at 0, so a
    /// pause menu, sim chip or level change made meanwhile wins. If the sim was already paused it stays paused.
    /// Runtime only, nothing saved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XrFocusPause : MonoBehaviour
    {
        const float LoseDelay = 0.25f;     // real seconds without focus before freezing (ignores blips)
        const float CheckInterval = 0.1f;  // real seconds between checks

        static XrFocusPause _instance;
        static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        bool _apiBroken;
        bool _seenFocused;
        bool _paused;
        bool _savedAudioPause;
        float _savedScale = 1f;
        float _unfocusedSince = -1f;
        float _nextCheck;

        /// <summary>True while the sim is frozen because the headset lost input focus.</summary>
        public static bool Paused => _instance != null && _instance._paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null; // domain reload may be off
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsXrFocusPause");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<XrFocusPause>();
        }

        void Update()
        {
            if (_apiBroken)
                return;
            float now = Time.unscaledTime;
            if (now < _nextCheck)
                return;
            _nextCheck = now + CheckInterval;

            bool xr = OpenXrDisplayRunning();
            bool focused = true;
            if (xr)
            {
                try
                {
                    focused = OpenXRUtility.IsSessionFocused;
                }
                catch (Exception e)
                {
                    _apiBroken = true;
                    Debug.LogWarning("XrFocusPause: OpenXR focus state not readable (" + e.GetType().Name + "); focus pause off for this session.");
                    if (_paused)
                        Resume("focus check unavailable");
                    return;
                }
                if (focused && !_seenFocused)
                {
                    _seenFocused = true;
                    Debug.Log("XrFocusPause: headset session focused; the sim will freeze while the system menu or Link dashboard has focus.");
                }
            }
            else
            {
                _seenFocused = false; // a new XR session must report focus again before we trust it
            }

            if (!xr || !_seenFocused)
                focused = true;

            if (focused)
            {
                _unfocusedSince = -1f;
                if (_paused)
                    Resume(xr ? "headset focus back" : "XR display stopped");
                return;
            }

            if (_unfocusedSince < 0f)
                _unfocusedSince = now;
            if (!_paused && now - _unfocusedSince >= LoseDelay)
                Pause();
        }

        void Pause()
        {
            if (FocusTime.Active)
                FocusTime.End(); // otherwise its 0.35x would be restored after focus returns
            _savedScale = Time.timeScale;
            _savedAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            _paused = true;
            Debug.Log("XrFocusPause: headset lost input focus (system menu, Link dashboard or headset off) - sim frozen and audio paused (was "
                + _savedScale.ToString("0.##") + "x).");
        }

        void Resume(string why)
        {
            _paused = false;
            bool restored = false;
            if (Time.timeScale <= 0f && _savedScale > 0f)
            {
                Time.timeScale = _savedScale;
                restored = true;
            }
            AudioListener.pause = _savedAudioPause;
            Debug.Log("XrFocusPause: " + why + " - "
                + (restored ? "sim resumed at " + _savedScale.ToString("0.##") + "x." : "sim speed left as is (" + Time.timeScale.ToString("0.##") + "x).")
                + " Audio " + (_savedAudioPause ? "stays paused." : "back on."));
        }

        void OnDisable()
        {
            if (_paused)
                Resume("focus pause shutting down");
        }

        static bool OpenXrDisplayRunning()
        {
            var settings = XRGeneralSettings.Instance;
            var manager = settings != null ? settings.Manager : null;
            if (manager == null || !(manager.activeLoader is OpenXRLoaderBase))
                return false;
            Displays.Clear();
            SubsystemManager.GetSubsystems(Displays);
            for (int i = 0; i < Displays.Count; i++)
            {
                if (Displays[i] != null && Displays[i].running)
                    return true;
            }
            return false;
        }
    }
}
