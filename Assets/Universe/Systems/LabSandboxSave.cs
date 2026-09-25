using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.Systems
{
    /// <summary>
    /// EXPERIMENTS toolbelt Save/Load for spawned sandbox gadgets (single slot0 JSON).
    /// Honesty: saves spawned gadget poses only — NOT CircuitLab breadboard snaps,
    /// NOT challenge progress, NOT PlayerPrefs settings, NOT Faraday scene objects.
    /// </summary>
    public static class LabSandboxSave
    {
        public const int Version = 1;
        public const string SlotFileName = "sandbox_slot0.json";

        static readonly Dictionary<string, string> RootNameToLabel = BuildRootMap();

        [Serializable]
        public class SandboxSaveFile
        {
            public int version;
            public string savedUtc;
            public SandboxEntry[] entries;
        }

        [Serializable]
        public class SandboxEntry
        {
            public string spawnLabel;
            public float px;
            public float py;
            public float pz;
            public float ex;
            public float ey;
            public float ez;
        }

        public static string SlotPath
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "QHYSICS");
                return Path.Combine(dir, SlotFileName);
            }
        }

        /// <summary>Scan spawned experiment props and write slot0. Returns count saved.</summary>
        public static int SaveSlot0()
        {
            var roots = CollectSpawnedRoots();
            var list = new List<SandboxEntry>(roots.Count);
            for (int i = 0; i < roots.Count; i++)
            {
                GameObject go = roots[i];
                if (go == null)
                    continue;
                string label;
                if (!TryResolveSpawnLabel(go.name, out label))
                {
                    Debug.LogWarning("LabSandboxSave: skip unknown root '" + go.name + "' (no spawn label).");
                    continue;
                }
                Transform t = go.transform;
                Vector3 p = t.position;
                Vector3 e = t.eulerAngles;
                list.Add(new SandboxEntry
                {
                    spawnLabel = label,
                    px = p.x, py = p.y, pz = p.z,
                    ex = e.x, ey = e.y, ez = e.z
                });
            }

            var file = new SandboxSaveFile
            {
                version = Version,
                savedUtc = DateTime.UtcNow.ToString("o"),
                entries = list.ToArray()
            };

            string path = SlotPath;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(file, true);
            File.WriteAllText(path, json, Encoding.UTF8);
            Debug.Log("LabSandboxSave: Saved " + list.Count + " gadgets -> " + path);
            return list.Count;
        }

        /// <summary>
        /// Clear spawned props then restore slot0 over a few frames (SpawnByLabel cooldown-safe).
        /// Returns false if file missing; true if load kicked off.
        /// </summary>
        public static bool LoadSlot0()
        {
            string path = SlotPath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("LabSandboxSave: Load skipped — missing " + path);
                return false;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            SandboxSaveFile file = JsonUtility.FromJson<SandboxSaveFile>(json);
            if (file == null || file.entries == null)
            {
                Debug.LogWarning("LabSandboxSave: Load failed — empty/invalid JSON at " + path);
                return false;
            }

            var host = EnsureRunner();
            host.BeginLoad(file.entries, path);
            return true;
        }

        static LabSandboxSaveRunner EnsureRunner()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<LabSandboxSaveRunner>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;
            var go = new GameObject("LabSandboxSaveRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<LabSandboxSaveRunner>();
        }

        /// <summary>Same selection rules as QhysicsLabActions.ClearSpawnedExperimentProps, roots only.</summary>
        public static List<GameObject> CollectSpawnedRoots()
        {
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var candidates = new List<GameObject>(32);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                    continue;
                GameObject go = t.gameObject;
                string n = go.name;
                if (string.IsNullOrEmpty(n))
                    continue;

                bool gadget = n.StartsWith("Gadget_", StringComparison.Ordinal);
                bool desktopSpawn = n.EndsWith("_Desktop", StringComparison.Ordinal);
                if (!gadget && !desktopSpawn)
                    continue;

                if (go.GetComponentInParent<Dispenser>() != null)
                    continue;

                if (!IsSpawnRoot(t))
                    continue;

                candidates.Add(go);
            }
            return candidates;
        }

        static bool IsSpawnRoot(Transform t)
        {
            Transform p = t.parent;
            while (p != null)
            {
                string n = p.name;
                if (!string.IsNullOrEmpty(n)
                    && (n.StartsWith("Gadget_", StringComparison.Ordinal) || n.EndsWith("_Desktop", StringComparison.Ordinal)))
                    return false;
                p = p.parent;
            }
            return true;
        }

        public static bool TryResolveSpawnLabel(string rootName, out string label)
        {
            label = null;
            if (string.IsNullOrEmpty(rootName))
                return false;

            string key = rootName;
            int clone = key.IndexOf(" (", StringComparison.Ordinal);
            if (clone > 0)
                key = key.Substring(0, clone);

            if (RootNameToLabel.TryGetValue(key, out label))
                return true;

            if (key.StartsWith("Component", StringComparison.Ordinal) && key.EndsWith("_Desktop", StringComparison.Ordinal))
            {
                string mid = key.Substring("Component".Length, key.Length - "Component".Length - "_Desktop".Length);
                label = HumanizeToLabel(mid);
                return !string.IsNullOrEmpty(label);
            }

            if (key.StartsWith("Gadget_", StringComparison.Ordinal))
            {
                label = HumanizeToLabel(key.Substring("Gadget_".Length));
                return !string.IsNullOrEmpty(label);
            }

            if (key.EndsWith("_Desktop", StringComparison.Ordinal))
            {
                label = HumanizeToLabel(key.Substring(0, key.Length - "_Desktop".Length));
                return !string.IsNullOrEmpty(label);
            }

            label = HumanizeToLabel(key);
            return !string.IsNullOrEmpty(label);
        }

        static string HumanizeToLabel(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
            var sb = new StringBuilder(token.Length + 8);
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(token[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim().ToLowerInvariant();
        }

        static Dictionary<string, string> BuildRootMap()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Gadget_Bernoulli", "bernoulli" },
                { "Gadget_Snell", "snell" },
                { "Gadget_Buoyancy", "buoyancy" },
                { "Gadget_Centripetal", "centripetal" },
                { "Gadget_Collision", "collision" },
                { "Gadget_Projectile", "projectile" },
                { "Gadget_PlateCapacitor", "plate cap" },
                { "Gadget_Lever", "lever" },
                { "Gadget_InclinedPlane", "inclined plane" },
                { "Gadget_Atwood", "atwood" },
                { "Gadget_Pendulum", "pendulum" },
                { "Gadget_SpringMass", "spring mass" },
                { "Gadget_ThinLens", "thin lens" },
                { "Gadget_CrankGenerator", "crank generator" },
                { "Gadget_MutualCoupler", "mutual coupler" },
                { "Gadget_Magnet", "magnet" },
                { "Gadget_Dipole", "dipole" },
                { "Gadget_Coil", "coil" },
                { "Gadget_FieldLens", "field lens" },
                { "Gadget_Wire", "wire" },
                { "Gadget_Battery", "battery" },
                { "Gadget_Switch", "switch" },
                { "Gadget_Lamp", "lamp" },
                { "Gadget_Resistor", "resistor" },
                { "Gadget_Motor", "motor" },
                { "Gadget_Solar", "solar" },
                { "Gadget_Capacitor", "capacitor" },
                { "Gadget_Inductor", "inductor" },
                { "Gadget_Diode", "diode" },
                { "Gadget_Fuse", "fuse" },
                { "Gadget_LED", "led" },
                { "Gadget_Speaker", "speaker" },
                { "Gadget_Potentiometer", "potentiometer" },
                { "Gadget_Transformer", "transformer" },
                { "Gadget_FunctionGenerator", "function generator" },
                { "Gadget_Multimeter", "multimeter" },
                { "Gadget_Galvanometer", "galvanometer" },
                { "Gadget_Oscilloscope", "oscilloscope" },
                { "Gadget_FrequencyCounter", "frequency counter" },
                { "Gadget_PowerMeter", "power meter" },
                { "Gadget_FluxMeter", "flux meter" },
                { "Gadget_ChargeMeter", "charge meter" },
                { "Gadget_Voltmeter", "voltmeter" },
                { "Gadget_Ammeter", "ammeter" },
                { "Gadget_Ohmmeter", "ohmmeter" },
                { "Gadget_CapacitanceMeter", "capacitance meter" },
                { "Gadget_InductanceMeter", "inductance meter" },
                { "Gadget_ResonanceMeter", "resonance meter" },
                { "Gadget_ImpedanceMeter", "impedance meter" },
                { "Gadget_PowerFactorMeter", "power factor meter" },
                { "Gadget_QFactorMeter", "q factor meter" },
                { "Gadget_AdmittanceMeter", "admittance meter" },
                { "Gadget_DecibelMeter", "decibel meter" },
                { "Gadget_CrestFactorMeter", "crest factor meter" },
                { "Gadget_EnergyMeter", "energy meter" },
                { "Gadget_DutyCycleMeter", "duty cycle meter" },
                { "Gadget_SlewRateMeter", "slew rate meter" },
                { "Gadget_RiseFallMeter", "rise fall meter" },
                { "Gadget_OvershootMeter", "overshoot meter" },
                { "Gadget_PeakToPeakMeter", "peak to peak meter" },
                { "Gadget_MeanMeter", "mean meter" },
                { "Gadget_RippleMeter", "ripple meter" },
                { "Gadget_ThdMeter", "thd meter" },
                { "Gadget_Compass", "compass" },
                { "Gadget_Stopwatch", "stopwatch" },
                { "Gadget_FieldProbe", "probe" },
                { "CubitRod_Desktop", "cubit rod" },
                { "CubitRod", "cubit rod" },
            };
            return d;
        }
    }

    /// <summary>
    /// Loads sandbox entries across frames so Destroy() from Clear finishes and SpawnByLabel cooldown is fine.
    /// </summary>
    public sealed class LabSandboxSaveRunner : MonoBehaviour
    {
        Coroutine _running;

        public void BeginLoad(LabSandboxSave.SandboxEntry[] entries, string path)
        {
            if (_running != null)
                StopCoroutine(_running);
            _running = StartCoroutine(LoadRoutine(entries, path));
        }

        IEnumerator LoadRoutine(LabSandboxSave.SandboxEntry[] entries, string path)
        {
            int cleared = QhysicsLabActions.ClearSpawnedExperimentProps();
            Debug.Log("LabSandboxSave: cleared " + cleared + " before load from " + path);
            yield return null;
            yield return null;

            int loaded = 0;
            int failed = 0;
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    LabSandboxSave.SandboxEntry e = entries[i];
                    if (e == null || string.IsNullOrEmpty(e.spawnLabel))
                    {
                        failed++;
                        continue;
                    }

                    Vector3 pos = new Vector3(e.px, e.py, e.pz);
                    GameObject go = QhysicsGadgets.SpawnByLabel(e.spawnLabel, pos, ignoreCooldown: true);
                    if (go == null)
                    {
                        Debug.LogWarning("LabSandboxSave: spawn failed for label '" + e.spawnLabel + "'.");
                        failed++;
                    }
                    else
                    {
                        go.transform.position = pos;
                        go.transform.eulerAngles = new Vector3(e.ex, e.ey, e.ez);
                        loaded++;
                    }

                    if ((i & 1) == 1)
                        yield return null;
                }
            }

            Debug.Log("LabSandboxSave: Load done — " + loaded + " restored, " + failed + " failed (" + path + ").");
            _running = null;
        }
    }
}