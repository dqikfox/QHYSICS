using System;
using System.IO;
using UnityEngine;
using RealityEngine.Physics.Thermo;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Core
{
    /// <summary>
    /// Light persistent lab: T_hot, T_cold, coupler closed, lab name. Not a scene serializer.
    /// JSON at Application.persistentDataPath / RealityEngine / lab.json
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(70)]
    public sealed class LabState : MonoBehaviour
    {
        public const string LabName = ThermoEnergy.LabName;
        public const string FileName = "lab.json";

        [SerializeField]
        bool enableKeyboard = true;

        bool _loaded;

        [Serializable]
        public class Data
        {
            public string labName = LabName;
            public float tHot = ThermoEnergy.DefaultTHot;
            public float tCold = ThermoEnergy.DefaultTCold;
            public bool couplerClosed;
        }

        public static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, "RealityEngine", FileName); }
        }

        public static string DirectoryPath
        {
            get { return Path.Combine(Application.persistentDataPath, "RealityEngine"); }
        }

        public static LabState EnsureOn(GameObject host)
        {
            if (host == null)
                return FindFirstObjectByType<LabState>(FindObjectsInactive.Include);
            LabState existing = host.GetComponent<LabState>();
            if (existing == null)
                existing = host.AddComponent<LabState>();
            return existing;
        }

        public static void SaveNow()
        {
            try
            {
                HeatCoupler heat = FindFirstObjectByType<HeatCoupler>(FindObjectsInactive.Include);
                var data = new Data
                {
                    labName = LabName,
                    tHot = ThermoEnergy.DefaultTHot,
                    tCold = ThermoEnergy.DefaultTCold,
                    couplerClosed = false
                };
                if (heat != null)
                {
                    data.tHot = heat.THot;
                    data.tCold = heat.TCold;
                    data.couplerClosed = heat.PathClosed;
                }
                string dir = DirectoryPath;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("LabState save skipped: " + ex.Message);
            }
        }

        public static Data TryLoad()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                    return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json))
                    return null;
                return JsonUtility.FromJson<Data>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("LabState load skipped: " + ex.Message);
                return null;
            }
        }

        public static void ApplyToScene(Data data)
        {
            if (data == null)
                return;
            HeatCoupler heat = FindFirstObjectByType<HeatCoupler>(FindObjectsInactive.Include);
            if (heat == null)
                return;
            float th = data.tHot > 1f ? data.tHot : ThermoEnergy.DefaultTHot;
            float tc = data.tCold > 1f ? data.tCold : ThermoEnergy.DefaultTCold;
            if (th < tc)
            {
                float tmp = th;
                th = tc;
                tc = tmp;
            }
            heat.ApplyTemperatures(th, tc);
            if (data.couplerClosed)
                heat.SnapBetweenReservoirs();
        }

        void Start()
        {
            if (_loaded)
                return;
            Data data = TryLoad();
            if (data != null)
                ApplyToScene(data);
            _loaded = true;
        }

        void Update()
        {
            if (!enableKeyboard)
                return;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame)
                SaveNow();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.P))
                SaveNow();
#endif
        }

        void OnApplicationQuit()
        {
            SaveNow();
        }

        void OnDisable()
        {
            if (Application.isPlaying)
                SaveNow();
        }
    }
}
