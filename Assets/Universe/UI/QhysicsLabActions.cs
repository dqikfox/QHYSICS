using UnityEngine;

namespace RealityEngine.UI
{
    /// <summary>Shared lab actions: reset CircuitLab and cycle sky lighting without fake stubs.</summary>
    public static class QhysicsLabActions
    {
        static readonly string[] SkyNames = { "Day", "Golden", "Dusk", "Night" };
        static readonly Color[] SunColors =
        {
            new Color(1.00f, 0.96f, 0.90f),
            new Color(1.00f, 0.72f, 0.42f),
            new Color(0.85f, 0.45f, 0.35f),
            new Color(0.55f, 0.65f, 0.95f)
        };
        static readonly float[] SunIntensities = { 1.15f, 0.95f, 0.55f, 0.18f };
        static readonly Color[] AmbientColors =
        {
            new Color(0.55f, 0.60f, 0.68f),
            new Color(0.55f, 0.42f, 0.32f),
            new Color(0.28f, 0.30f, 0.42f),
            new Color(0.08f, 0.10f, 0.16f)
        };
        static readonly float[] AmbientIntensities = { 1.00f, 0.85f, 0.55f, 0.25f };

        static int _skyIndex;

        public static bool ResetCircuitLab()
        {
            var lab = UnityEngine.Object.FindAnyObjectByType<CircuitLab>(FindObjectsInactive.Include);
            if (lab == null)
            {
                Debug.LogWarning("QHYSICS: CircuitLab not found — cannot reset experiment.");
                return false;
            }
            lab.Reset();
            Time.timeScale = 1f;
            Debug.Log("QHYSICS: CircuitLab.Reset() done.");
            return true;
        }

        /// <summary>
        /// Cycles Day / Golden / Dusk / Night via main directional + ambient.
        /// Never disables MountainScene or swaps the skybox asset.
        /// </summary>
        public static string CycleSky()
        {
            _skyIndex = (_skyIndex + 1) % SkyNames.Length;
            ApplySkyPreset(_skyIndex);
            string name = SkyNames[_skyIndex];
            Debug.Log("QHYSICS: Sky -> " + name + " (MountainScene left on).");
            return name;
        }

        static void ApplySkyPreset(int idx)
        {
            Light sun = FindMainDirectional();
            if (sun != null)
            {
                sun.color = SunColors[idx];
                sun.intensity = SunIntensities[idx];
            }

            RenderSettings.ambientLight = AmbientColors[idx];
            RenderSettings.ambientIntensity = AmbientIntensities[idx];
        }

        static Light FindMainDirectional()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Light best = null;
            for (int i = 0; i < lights.Length; i++)
            {
                Light L = lights[i];
                if (L == null || L.type != LightType.Directional)
                    continue;
                string n = L.name ?? string.Empty;
                if (n.IndexOf("directional", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("sun", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return L;
                if (best == null)
                    best = L;
            }
            return best;
        }
    }
}
