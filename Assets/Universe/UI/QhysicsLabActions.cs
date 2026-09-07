using UnityEngine;
using RealityEngine.Player;
using RealityEngine.Physics.Electromagnetism;

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
            ClearSpawnedExperimentProps();
            ResetInductionCapacitors();

            var lab = UnityEngine.Object.FindAnyObjectByType<CircuitLab>(FindObjectsInactive.Include);
            if (lab == null)
            {
                Debug.LogWarning("QHYSICS: CircuitLab not found — cannot reset table (spawned gadgets still cleared).");
                Time.timeScale = 1f;
                return false;
            }
            lab.Reset();
            Time.timeScale = 1f;
            Debug.Log("QHYSICS: CircuitLab.Reset() done.");
            return true;
        }

        /// <summary>
        /// Destroys toolbelt/hotbar-spawned experiment props (Gadget_* and *_Desktop roots).
        /// Never touches Dispenser templates or scene lab content under a Dispenser.
        /// </summary>
        public static int ClearSpawnedExperimentProps()
        {
            var desktop = Object.FindFirstObjectByType<DesktopInteractor>(FindObjectsInactive.Include);
            if (desktop != null)
                desktop.ReleaseHeld();

            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var doomed = new System.Collections.Generic.List<GameObject>(32);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                    continue;
                GameObject go = t.gameObject;
                string n = go.name;
                if (string.IsNullOrEmpty(n))
                    continue;

                bool gadget = n.StartsWith("Gadget_", System.StringComparison.Ordinal);
                bool desktopSpawn = n.EndsWith("_Desktop", System.StringComparison.Ordinal);
                if (!gadget && !desktopSpawn)
                    continue;

                // Leave Dispenser template children alone (CircuitLab.Reset handles those).
                if (go.GetComponentInParent<Dispenser>() != null)
                    continue;

                doomed.Add(go);
            }

            int cleared = 0;
            for (int i = 0; i < doomed.Count; i++)
            {
                GameObject go = doomed[i];
                if (go == null)
                    continue;
                Object.Destroy(go);
                cleared++;
            }

            if (cleared > 0)
                Debug.Log("QHYSICS: New Run cleared " + cleared + " spawned gadget(s).");
            return cleared;
        }


        static void ResetInductionCapacitors()
        {
            var circuits = Object.FindObjectsByType<InductionCircuit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < circuits.Length; i++)
            {
                if (circuits[i] == null)
                    continue;
                circuits[i].ResetCapacitorVoltage();
                n++;
            }
            if (n > 0)
                Debug.Log("QHYSICS: New Run cleared Vc on " + n + " InductionCircuit(s).");
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
