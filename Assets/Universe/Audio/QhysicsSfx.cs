using System.Collections.Generic;
using UnityEngine;

namespace RealityEngine.Audio
{
    /// <summary>Procedurally generated SFX ids (no imported clips required).</summary>
    public enum SfxId
    {
        UiTick = 0,
        ObjectiveDone = 1,
        ChallengeComplete = 2,
        ChapterComplete = 3,
        Unlock = 4,
        LevelUp = 5,
        HitBlunt = 6,
        HitSlash = 7,
        HitPierce = 8,
        Parry = 9,
        Block = 10,
        Whoosh = 11,
        FireCast = 12,
        FireImpact = 13,
        Lightning = 14,
        ForcePush = 15,
        ChargeStart = 16,
        PlayerHurt = 17,
        EnemyDeath = 18,
        Stick = 19,
        Unstick = 20,
        FocusOn = 21,
        FocusOff = 22,
        Denied = 23
    }

    /// <summary>
    /// Runtime-generated tones/noise SFX routed through the QhysicsMixer SFX bus (via <see cref="AudioRouter"/>).
    /// Small pooled AudioSource set (perf: no per-hit allocation). Honesty: synthesized placeholders, not recorded foley.
    /// </summary>
    public sealed class QhysicsSfx : MonoBehaviour
    {
        const int SampleRate = 44100;
        const int PoolSize = 12;

        static QhysicsSfx _instance;
        static readonly Dictionary<SfxId, AudioClip> Clips = new Dictionary<SfxId, AudioClip>();

        AudioSource[] _pool;
        int _next;

        public static QhysicsSfx Ensure()
        {
            if (_instance != null)
                return _instance;
            _instance = UnityEngine.Object.FindAnyObjectByType<QhysicsSfx>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;
            var go = new GameObject("QhysicsSfx");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            _instance = go.AddComponent<QhysicsSfx>();
            return _instance;
        }

        void Awake()
        {
            _instance = this;
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Sfx_" + i);
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.5f;
                src.maxDistance = 30f;
                src.dopplerLevel = 0f;
                AudioRouter.Register(src, AudioBus.SFX);
                _pool[i] = src;
            }
        }

        /// <summary>Play a non-spatial UI sound.</summary>
        public static void Play2D(SfxId id, float volume = 0.7f, float pitch = 1f)
        {
            Ensure().PlayInternal(id, Vector3.zero, false, volume, pitch);
        }

        /// <summary>Play a spatial sound at a world position.</summary>
        public static void PlayAt(SfxId id, Vector3 pos, float volume = 0.8f, float pitch = 1f)
        {
            Ensure().PlayInternal(id, pos, true, volume, pitch);
        }

        void PlayInternal(SfxId id, Vector3 pos, bool spatial, float volume, float pitch)
        {
            if (_pool == null)
                return;
            AudioClip clip = GetClip(id);
            if (clip == null)
                return;
            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.transform.position = pos;
            src.spatialBlend = spatial ? 1f : 0f;
            src.pitch = Mathf.Clamp(pitch, 0.3f, 3f) * Mathf.Max(0.3f, Time.timeScale > 0.01f ? Mathf.Lerp(0.75f, 1f, Time.timeScale) : 1f);
            src.volume = Mathf.Clamp01(volume);
            src.clip = clip;
            src.Play();
        }

        public static AudioClip GetClip(SfxId id)
        {
            if (Clips.TryGetValue(id, out AudioClip c) && c != null)
                return c;
            c = Generate(id);
            Clips[id] = c;
            return c;
        }

        // ── Synthesis ──────────────────────────────────────────────

        static AudioClip Generate(SfxId id)
        {
            switch (id)
            {
                case SfxId.UiTick: return Tone("sfx_tick", 0.06f, 1320f, 0f, 0.5f, 60f);
                case SfxId.ObjectiveDone: return Arp("sfx_objective", new[] { 880f, 1318.5f }, 0.09f, 0.35f);
                case SfxId.ChallengeComplete: return Arp("sfx_complete", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 0.5f);
                case SfxId.ChapterComplete: return Arp("sfx_chapter", new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.13f, 0.9f);
                case SfxId.Unlock: return Arp("sfx_unlock", new[] { 660f, 990f }, 0.08f, 0.3f);
                case SfxId.LevelUp: return Arp("sfx_levelup", new[] { 440f, 554.37f, 659.25f, 880f }, 0.09f, 0.6f);
                case SfxId.HitBlunt: return Impact("sfx_blunt", 0.22f, 90f, 0.55f, 900f);
                case SfxId.HitSlash: return Sweep("sfx_slash", 0.18f, 5200f, 1400f, 0.8f);
                case SfxId.HitPierce: return Impact("sfx_pierce", 0.14f, 240f, 0.35f, 3200f);
                case SfxId.Parry: return Metal("sfx_parry", 0.55f, new[] { 930f, 1397f, 2213f, 3120f });
                case SfxId.Block: return Metal("sfx_block", 0.3f, new[] { 610f, 1033f, 1720f });
                case SfxId.Whoosh: return Sweep("sfx_whoosh", 0.25f, 600f, 2600f, 0.45f);
                case SfxId.FireCast: return Sweep("sfx_firecast", 0.35f, 300f, 1800f, 0.7f);
                case SfxId.FireImpact: return Impact("sfx_fireimpact", 0.45f, 70f, 0.9f, 1800f);
                case SfxId.Lightning: return Crack("sfx_lightning", 0.4f);
                case SfxId.ForcePush: return Impact("sfx_force", 0.4f, 55f, 0.7f, 500f);
                case SfxId.ChargeStart: return Tone("sfx_charge", 0.25f, 220f, 440f, 0.35f, 8f);
                case SfxId.PlayerHurt: return Impact("sfx_hurt", 0.3f, 120f, 0.5f, 700f);
                case SfxId.EnemyDeath: return Tone("sfx_death", 0.6f, 330f, 110f, 0.5f, 4f);
                case SfxId.Stick: return Impact("sfx_stick", 0.16f, 180f, 0.25f, 2400f);
                case SfxId.Unstick: return Sweep("sfx_unstick", 0.14f, 2600f, 900f, 0.4f);
                case SfxId.FocusOn: return Tone("sfx_focus_on", 0.35f, 660f, 220f, 0.4f, 5f);
                case SfxId.FocusOff: return Tone("sfx_focus_off", 0.3f, 220f, 660f, 0.35f, 6f);
                case SfxId.Denied: return Tone("sfx_denied", 0.18f, 180f, 140f, 0.4f, 12f);
            }
            return Tone("sfx_default", 0.1f, 440f, 0f, 0.4f, 20f);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Sine glide f0→f1 with exponential decay.</summary>
        static AudioClip Tone(string name, float dur, float f0, float f1, float amp, float decay)
        {
            int n = Mathf.CeilToInt(dur * SampleRate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / dur;
                float f = f1 > 0f ? Mathf.Lerp(f0, f1, u) : f0;
                phase += 2.0 * Mathf.PI * f / SampleRate;
                float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(t * 400f) * Mathf.Clamp01((dur - t) * 60f);
                d[i] = (float)System.Math.Sin(phase) * env * amp;
            }
            return Make(name, d);
        }

        static AudioClip Arp(string name, float[] notes, float step, float amp)
        {
            float dur = step * notes.Length + 0.35f;
            int n = Mathf.CeilToInt(dur * SampleRate);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.FloorToInt(k * step * SampleRate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Mathf.Exp(-6f * t) * Mathf.Clamp01(t * 300f);
                    float s = Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[k] * t);
                    d[i] += s * env * amp / notes.Length * 2f;
                }
            }
            Normalize(d, amp);
            return Make(name, d);
        }

        /// <summary>Low sine thump + filtered noise burst.</summary>
        static AudioClip Impact(string name, float dur, float body, float amp, float noiseCutoff)
        {
            int n = Mathf.CeilToInt(dur * SampleRate);
            var d = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0f;
            float a = Mathf.Clamp01(2f * Mathf.PI * noiseCutoff / SampleRate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (noise - lp);
                float envN = Mathf.Exp(-40f * t);
                float envB = Mathf.Exp(-12f * t);
                float f = body * (1f + 1.5f * Mathf.Exp(-30f * t));
                d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * envB * 0.8f + lp * envN * 1.4f) * amp;
            }
            Normalize(d, amp);
            return Make(name, d);
        }

        /// <summary>Band-swept noise (swish).</summary>
        static AudioClip Sweep(string name, float dur, float fStart, float fEnd, float amp)
        {
            int n = Mathf.CeilToInt(dur * SampleRate);
            var d = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / dur;
                float fc = Mathf.Lerp(fStart, fEnd, u);
                float a = Mathf.Clamp01(2f * Mathf.PI * fc / SampleRate);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (noise - lp);
                lp2 += a * 0.5f * (lp - lp2);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) ;
                d[i] = (lp - lp2) * env * amp * 3f;
            }
            Normalize(d, amp);
            return Make(name, d);
        }

        /// <summary>Inharmonic partials (clang).</summary>
        static AudioClip Metal(string name, float dur, float[] partials)
        {
            int n = Mathf.CeilToInt(dur * SampleRate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;
                for (int k = 0; k < partials.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * partials[k] * t) * Mathf.Exp(-(6f + 4f * k) * t) / (1f + k * 0.6f);
                d[i] = s * Mathf.Clamp01(t * 2000f);
            }
            Normalize(d, 0.8f);
            return Make(name, d);
        }

        /// <summary>Crackling noise bursts (lightning).</summary>
        static AudioClip Crack(string name, float dur)
        {
            int n = Mathf.CeilToInt(dur * SampleRate);
            var d = new float[n];
            var rng = new System.Random(1234);
            float burst = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                if (rng.NextDouble() < 0.0025)
                    burst = 1f;
                burst *= 0.9985f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                d[i] = noise * (0.25f + burst) * Mathf.Exp(-5f * t);
            }
            Normalize(d, 0.85f);
            return Make(name, d);
        }

        static void Normalize(float[] d, float peak)
        {
            float max = 1e-5f;
            for (int i = 0; i < d.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++)
                d[i] *= k;
        }
    }
}
