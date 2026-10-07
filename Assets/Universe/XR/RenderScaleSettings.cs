using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealityEngine.XR
{
    /// <summary>
    /// Quest performance / sharpness: URP render scale chosen in Settings > "Render scale" (0.8 Perf / 0.9 Balanced /
    /// 1.0 Native default / 1.2 Sharp), saved in PlayerPrefs and applied live. In XR, URP passes the active pipeline
    /// asset's renderScale to the XR system, so this resizes each eye's render target: below 1.0 frees GPU time for a
    /// steady frame rate in busy spots (Giza plaza, the arena), above 1.0 supersamples for crisper text and grid lines
    /// when there is headroom. Pairs with <see cref="FoveationSettings"/> (which only lowers the lens edges).
    /// The project's URP assets are never saved: the value is only changed in memory, the original is remembered per
    /// asset and put back when Play stops (Application.quitting), so nothing ends up dirty on disk. Until the player
    /// first touches the chip the project's own value is left alone. Re-asserted once a second so a quality-level /
    /// pipeline-asset switch picks it up. Also applies on desktop (same URP asset), which is handy on the laptop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RenderScaleSettings : MonoBehaviour
    {
        const string PrefKey = "qhysics.xr.renderScale";
        static readonly float[] Presets = { 0.8f, 0.9f, 1.0f, 1.2f };
        static readonly string[] Names = { "0.8 Perf", "0.9 Balanced", "1.0 Native", "1.2 Sharp" };
        const int DefaultIndex = 2;

        static RenderScaleSettings _instance;
        static int _index = -1;
        static bool _quitHooked;
        static readonly Dictionary<UniversalRenderPipelineAsset, float> Originals =
            new Dictionary<UniversalRenderPipelineAsset, float>();

        float _nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _index = -1; // re-read PlayerPrefs each Play (domain reload may be off)
            _instance = null;
            Originals.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsRenderScale");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<RenderScaleSettings>();
            if (!_quitHooked)
            {
                _quitHooked = true;
                Application.quitting += RestoreOriginals;
            }
            int n = Apply();
            if (n > 0)
                Debug.Log("RenderScaleSettings: render scale " + Label + " on the active URP asset.");
        }

        static int Index
        {
            get
            {
                if (_index < 0)
                    _index = NearestIndex(PlayerPrefs.GetFloat(PrefKey, Presets[DefaultIndex]));
                return _index;
            }
        }

        /// <summary>Chosen render scale (1 = native eye resolution).</summary>
        public static float Scale => Presets[Index];

        public static string Label => Names[Index];

        /// <summary>Step 0.8 -> 0.9 -> 1.0 -> 1.2 -> 0.8, save, and apply now.</summary>
        public static void Cycle()
        {
            _index = (Index + 1) % Presets.Length;
            PlayerPrefs.SetFloat(PrefKey, Presets[_index]);
            PlayerPrefs.Save();
            int n = Apply();
            Debug.Log("RenderScaleSettings: render scale " + Label
                + (n > 0 ? " on the active URP asset." : " (saved; no URP asset active)."));
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
            if (Time.unscaledTime < _nextCheck)
                return;
            _nextCheck = Time.unscaledTime + 1f;
            Apply();
        }

        /// <summary>Writes the chosen scale to the active URP asset (only once the player has picked one). Returns 1 if set.</summary>
        static int Apply()
        {
            if (!PlayerPrefs.HasKey(PrefKey))
                return 0; // never touched: keep the project's own render scale
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
                return 0;
            if (!Originals.ContainsKey(urp))
                Originals[urp] = urp.renderScale;
            float s = Scale;
            if (!Mathf.Approximately(urp.renderScale, s))
                urp.renderScale = s;
            return 1;
        }

        /// <summary>Puts every touched URP asset back to its original value so the Editor never keeps (or saves) the change.</summary>
        static void RestoreOriginals()
        {
            foreach (var kv in Originals)
            {
                if (kv.Key != null)
                    kv.Key.renderScale = kv.Value;
            }
            Originals.Clear();
        }

        static int NearestIndex(float v)
        {
            int best = DefaultIndex;
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