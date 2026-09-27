using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace RealityEngine.Audio
{
    /// <summary>
    /// Audio bus classification for runtime routing of AudioSources into the QhysicsMixer.
    /// </summary>
    public enum AudioBus
    {
        SFX,
        Ambience,
        Voice
    }

    /// <summary>
    /// Static runtime router that loads the QhysicsMixer, classifies every AudioSource
    /// by heuristic, and assigns <see cref="AudioSource.outputAudioMixerGroup"/>.
    /// Also provides volume control helpers (dB conversion) and PlayerPrefs persistence.
    /// Re-scans on every scene load.
    /// Honesty: Master bus gain via Unity AudioMixer exposed params — not per-SFX routing and not a full EQ.
    /// </summary>
    public static class AudioRouter
    {
        private const string MixerRefResourceName = "QhysicsAudioMixerRef";
        private const string FallbackMixerResourceName = "QhysicsMixer";

        private const string PrefsMaster = "QhysicsAudio_Master";
        private const string PrefsSfx = "QhysicsAudio_SFX";
        private const string PrefsAmbience = "QhysicsAudio_Ambience";
        private const string PrefsVoice = "QhysicsAudio_Voice";

        // Exposed parameter names on the mixer (must match the .mixer asset exactly).
        private const string ParamMaster = "MasterVolume";
        private const string ParamSfx = "SfxVolume";
        private const string ParamAmbience = "AmbienceVolume";
        private const string ParamVoice = "VoiceVolume";

        /// <summary>Near-silence floor for Master/bus faders (Unity mixer practical mute).</summary>
        public const float MinDb = -80f;

        private static AudioMixer _mixer;
        private static AudioMixerGroup _masterGroup;
        private static AudioMixerGroup _sfxGroup;
        private static AudioMixerGroup _ambienceGroup;
        private static AudioMixerGroup _voiceGroup;
        private static bool _initialized;

        // --- Heuristic keyword tables (all lower-case) ---

        private static readonly string[] AmbienceKeywords =
        {
            "environmental", "ambience", "ambient", "wind", "birds", "river",
            "water", "rain", "forest", "nature", "background", "atmos", "loop"
        };

        private static readonly string[] VoiceKeywords =
        {
            "tts", "scientist", "speech", "voice", "narrator", "dialogue",
            "dialog", "spoken", "announcer", "guide", "instructor", "stt"
        };

        // --- Lifecycle ---

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            EnsureInitialized();
            ApplySavedVolumes();
            RouteAll();
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadMixer();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RouteAll();
        }

        // --- Mixer loading ---

        private static void LoadMixer()
        {
            // Primary: load via the ScriptableObject reference in Resources.
            var mixerRef = Resources.Load<QhysicsAudioMixerRef>(MixerRefResourceName);
            if (mixerRef != null && mixerRef.Mixer != null)
            {
                _mixer = mixerRef.Mixer;
                CacheGroups();
                return;
            }

            // Fallback: try a direct Resources load (works if the mixer is ever
            // moved into a Resources folder).
            _mixer = Resources.Load<AudioMixer>(FallbackMixerResourceName);
            if (_mixer != null)
            {
                CacheGroups();
                return;
            }

            Debug.LogWarning("[AudioRouter] Could not load QhysicsMixer. Audio will play without mixer routing.");
        }

        private static void CacheGroups()
        {
            if (_mixer == null) return;
            AudioMixerGroup[] groups = _mixer.FindMatchingGroups(string.Empty);
            if (groups == null) return;
            for (int i = 0; i < groups.Length; i++)
            {
                AudioMixerGroup g = groups[i];
                if (g == null) continue;
                if (g.name == "Master") _masterGroup = g;
                else if (g.name == "SFX") _sfxGroup = g;
                else if (g.name == "Ambience") _ambienceGroup = g;
                else if (g.name == "Voice") _voiceGroup = g;
            }
        }

        // --- Routing ---

        /// <summary>
        /// Scans every AudioSource in loaded scenes and routes unrouterd sources
        /// to the appropriate mixer group via heuristic classification.
        /// Sources that already have an <see cref="AudioSource.outputAudioMixerGroup"/>
        /// are left untouched (explicit routing is respected).
        /// </summary>
        public static void RouteAll()
        {
            EnsureInitialized();
            if (_mixer == null) return;

            AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null) continue;
                if (source.outputAudioMixerGroup != null) continue;
                AudioBus bus = Classify(source);
                RouteInternal(source, bus);
            }
        }

        /// <summary>
        /// Explicitly route a single AudioSource to a specific bus.
        /// Use this from gameplay code when the heuristic is not sufficient.
        /// </summary>
        public static void Register(AudioSource source, AudioBus bus)
        {
            if (source == null) return;
            EnsureInitialized();
            RouteInternal(source, bus);
        }

        /// <summary>The AudioMixer currently in use (may be null if loading failed).</summary>
        public static AudioMixer Mixer
        {
            get
            {
                EnsureInitialized();
                return _mixer;
            }
        }

        /// <summary>True when QhysicsMixer loaded and MasterVolume can be driven in dB.</summary>
        public static bool HasMixer
        {
            get
            {
                EnsureInitialized();
                return _mixer != null;
            }
        }

        private static void RouteInternal(AudioSource source, AudioBus bus)
        {
            AudioMixerGroup group;
            switch (bus)
            {
                case AudioBus.Ambience: group = _ambienceGroup; break;
                case AudioBus.Voice: group = _voiceGroup; break;
                default: group = _sfxGroup; break;
            }
            if (group != null)
                source.outputAudioMixerGroup = group;
        }

        /// <summary>
        /// Classify an AudioSource into a bus based on its GameObject name and clip name.
        /// </summary>
        private static AudioBus Classify(AudioSource source)
        {
            string goName = source.gameObject != null ? source.gameObject.name.ToLowerInvariant() : "";
            string clipName = source.clip != null ? source.clip.name.ToLowerInvariant() : "";

            for (int i = 0; i < AmbienceKeywords.Length; i++)
            {
                if (goName.Contains(AmbienceKeywords[i]) || clipName.Contains(AmbienceKeywords[i]))
                    return AudioBus.Ambience;
            }

            for (int i = 0; i < VoiceKeywords.Length; i++)
            {
                if (goName.Contains(VoiceKeywords[i]) || clipName.Contains(VoiceKeywords[i]))
                    return AudioBus.Voice;
            }

            return AudioBus.SFX;
        }

        // --- Volume control (dB conversion + persistence) ---

        /// <summary>
        /// Convert a 0–1 normalized volume to decibels.
        /// Floor is <see cref="MinDb"/> (~mute) when near 0; unity gain is 0 dB at 1.
        /// </summary>
        public static float ToDb(float normalized)
        {
            float v = Mathf.Clamp01(normalized);
            if (v <= 0.0001f)
                return MinDb;
            return Mathf.Log10(v) * 20f;
        }

        /// <summary>Convert decibels back to a 0–1 normalized volume.</summary>
        private static float FromDb(float db)
        {
            if (db <= MinDb)
                return 0f;
            return Mathf.Pow(10f, db / 20f);
        }

        /// <summary>
        /// Set the Master bus volume (0–1) on the QhysicsMixer MasterVolume param.
        /// When the mixer is present, AudioListener.volume is pinned to 1 so the mixer
        /// is the single gain control (Settings chips do not drive AudioListener).
        /// </summary>
        public static void SetMasterVolume(float normalized)
        {
            EnsureInitialized();
            normalized = Mathf.Clamp01(normalized);
            if (_mixer != null)
            {
                // Mixer Master is authoritative; keep the listener wide open.
                AudioListener.volume = 1f;
                _mixer.SetFloat(ParamMaster, ToDb(normalized));
            }
            else
            {
                // Fallback only when QhysicsAudioMixerRef / mixer failed to load.
                AudioListener.volume = normalized;
            }
        }

        /// <summary>Set a child bus volume (0–1) on the mixer.</summary>
        public static void SetBusVolume(AudioBus bus, float normalized)
        {
            EnsureInitialized();
            if (_mixer == null) return;
            string param;
            switch (bus)
            {
                case AudioBus.Ambience: param = ParamAmbience; break;
                case AudioBus.Voice: param = ParamVoice; break;
                default: param = ParamSfx; break;
            }
            _mixer.SetFloat(param, ToDb(normalized));
        }

        /// <summary>Read the Master bus volume (0–1) from the mixer.</summary>
        public static float GetMasterVolume()
        {
            EnsureInitialized();
            if (_mixer == null) return 1f;
            float db;
            return _mixer.GetFloat(ParamMaster, out db) ? FromDb(db) : 1f;
        }

        /// <summary>Read a child bus volume (0–1) from the mixer.</summary>
        public static float GetBusVolume(AudioBus bus)
        {
            EnsureInitialized();
            if (_mixer == null) return 1f;
            string param;
            switch (bus)
            {
                case AudioBus.Ambience: param = ParamAmbience; break;
                case AudioBus.Voice: param = ParamVoice; break;
                default: param = ParamSfx; break;
            }
            float db;
            return _mixer.GetFloat(param, out db) ? FromDb(db) : 1f;
        }

        // --- PlayerPrefs persistence ---

        /// <summary>Apply all saved volumes from PlayerPrefs to the mixer.</summary>
        private static void ApplySavedVolumes()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(PrefsMaster, 1f));
            SetBusVolume(AudioBus.SFX, PlayerPrefs.GetFloat(PrefsSfx, 1f));
            SetBusVolume(AudioBus.Ambience, PlayerPrefs.GetFloat(PrefsAmbience, 1f));
            SetBusVolume(AudioBus.Voice, PlayerPrefs.GetFloat(PrefsVoice, 1f));
        }

        /// <summary>Persist the Master volume and apply it to the mixer.</summary>
        public static void SaveMasterVolume(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            PlayerPrefs.SetFloat(PrefsMaster, normalized);
            PlayerPrefs.Save();
            SetMasterVolume(normalized);
        }

        /// <summary>Persist a child bus volume and apply it to the mixer.</summary>
        public static void SaveBusVolume(AudioBus bus, float normalized)
        {
            string key;
            switch (bus)
            {
                case AudioBus.Ambience: key = PrefsAmbience; break;
                case AudioBus.Voice: key = PrefsVoice; break;
                default: key = PrefsSfx; break;
            }
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(normalized));
            PlayerPrefs.Save();
            SetBusVolume(bus, normalized);
        }

        /// <summary>Load the persisted Master volume (default 1).</summary>
        public static float LoadMasterVolume() => PlayerPrefs.GetFloat(PrefsMaster, 1f);

        /// <summary>Load a persisted child bus volume (default 1).</summary>
        public static float LoadBusVolume(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Ambience: return PlayerPrefs.GetFloat(PrefsAmbience, 1f);
                case AudioBus.Voice: return PlayerPrefs.GetFloat(PrefsVoice, 1f);
                default: return PlayerPrefs.GetFloat(PrefsSfx, 1f);
            }
        }
    }
}
