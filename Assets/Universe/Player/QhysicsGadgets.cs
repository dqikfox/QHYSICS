using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using RealityEngine.Survey;
using RealityEngine.Visualization;
using RealityEngine.Experiments;
using RealityEngine.Core;
using RealityEngine.UI;
using RealityEngine.XR;
using RealityEngine.Physics.Electromagnetism;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Spawns CircuitLab parts + survey/physics gadgets for desktop hotbar and VR toolbelt chips.
    /// Prefers cloning a live Dispenser child; falls back to Resources/CircuitLab then Editor Assets/Models.
    /// </summary>
    public static class QhysicsGadgets
    {
        static float _cooldown;

        public static GameObject SpawnSelected(Vector3 worldPos)
        {
            if (QhysicsInventory.Instance == null)
                return null;
            return SpawnByLabel(QhysicsInventory.Instance.SelectedLabel, worldPos);
        }

        public static GameObject SpawnByLabel(string label, Vector3 worldPos)
        {
            if (string.IsNullOrEmpty(label))
                return null;
            if (Time.unscaledTime < _cooldown)
                return null;
            _cooldown = Time.unscaledTime + 0.15f;

            string key = label.Trim().ToLowerInvariant();
            if (key == "delete" || key == "empty")
                return null;

            // Physics / measure gadgets (not CircuitLab Dispenser tags)
            if (key == "field lens" || key == "fieldlens" || key == "lens")
                return SpawnFieldLensGadget(worldPos);
            if (key == "cubit rod" || key == "cubit" || key == "cubitrod")
                return SpawnCubitRod(worldPos);
            if (key == "multimeter")
                return SpawnMultimeterStub(worldPos);
            if (key == "lamp" || key == "led" || key == "glow" || key == "bulb")
                return SpawnLoadBulb(worldPos);
            if (key == "magnet")
                return SpawnHandheldMagnet(worldPos, pureDipole: false);
            if (key == "dipole")
                return SpawnHandheldMagnet(worldPos, pureDipole: true);
            if (key == "coil")
                return SpawnHandheldCoil(worldPos);
            if (key == "stopwatch")
                return SpawnStopwatch(worldPos);
            if (key == "probe" || key == "field probe" || key == "bprobe" || key == "b-probe")
                return SpawnFieldProbe(worldPos);
            if (key == "switch")
                return SpawnLoadSwitch(worldPos);
            if (key == "battery" || key == "cell" || key == "emf")
                return SpawnLoadBattery(worldPos);
            if (key == "wire" || key == "long wire" || key == "longwire" || key == "jumper")
                return SpawnLoadWire(worldPos);
            if (key == "resistor")
                return SpawnLoadResistor(worldPos);
            if (key == "motor" || key == "dc motor" || key == "dcmotor")
                return SpawnLoadMotor(worldPos);
            if (key == "solar" || key == "solar panel" || key == "solarpanel" || key == "pv" || key == "photocell")
                return SpawnLoadSolar(worldPos);
            if (key == "capacitor" || key == "cap" || key == "condenser")
                return SpawnLoadCapacitor(worldPos);


            // World / experiment chips - real actions (not spawns)
            if (TryWorldOrExperimentAction(key, label))
                return null;


            Dispenser.ComponentTag tag;
            if (!TryMapTag(key, out tag))
            {
                Debug.LogWarning("QhysicsGadgets: unknown tool '" + label + "'.");
                return null;
            }

            GameObject template = FindDispenserTemplate(tag);
            if (template == null)
                template = LoadPrefab(tag);

            if (template == null)
            {
                Debug.LogWarning("QhysicsGadgets: no prefab/template for " + tag);
                return SpawnPrimitiveProxy(tag.ToString(), worldPos, new Color(0.3f, 0.7f, 0.9f));
            }

            GameObject go = Object.Instantiate(template);
            go.name = "Component" + tag + "_Desktop";
            go.SetActive(true);
            go.transform.SetParent(null, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            EnableColliders(go);
            return go;
        }

        static bool TryMapTag(string key, out Dispenser.ComponentTag tag)
        {
            switch (key)
            {
                case "wire": tag = Dispenser.ComponentTag.Wire; return true;
                case "longwire":
                case "long wire": tag = Dispenser.ComponentTag.LongWire; return true;
                case "battery": tag = Dispenser.ComponentTag.Battery; return true;
                case "switch": tag = Dispenser.ComponentTag.Switch; return true;
                case "bulb": tag = Dispenser.ComponentTag.Bulb; return true;
                case "resistor": tag = Dispenser.ComponentTag.Resistor; return true;
                case "motor": tag = Dispenser.ComponentTag.Motor; return true;
                case "solar": tag = Dispenser.ComponentTag.Solar; return true;
                case "timer": tag = Dispenser.ComponentTag.Timer; return true;
                case "button": tag = Dispenser.ComponentTag.Button; return true;
                default:
                    tag = Dispenser.ComponentTag.Wire;
                    return false;
            }
        }

        static GameObject FindDispenserTemplate(Dispenser.ComponentTag tag)
        {
            var dispensers = Object.FindObjectsByType<Dispenser>(FindObjectsInactive.Include);
            for (int i = 0; i < dispensers.Length; i++)
            {
                var d = dispensers[i];
                if (d == null || d.componentTag != tag)
                    continue;
                if (d.transform.childCount <= 0)
                    continue;
                return d.transform.GetChild(0).gameObject;
            }
            string tagName = tag.ToString();
            try
            {
                var tagged = GameObject.FindGameObjectsWithTag(tagName);
                for (int i = 0; i < tagged.Length; i++)
                {
                    if (tagged[i] == null)
                        continue;
                    if (tagged[i].transform.parent != null && tagged[i].transform.parent.GetComponent<Dispenser>() != null)
                        return tagged[i];
                }
            }
            catch
            {
                // Tag may be undefined in TagManager for some builds
            }
            return null;
        }

        static GameObject LoadPrefab(Dispenser.ComponentTag tag)
        {
            string resName = "CircuitLab/Component" + tag;
            var fromRes = Resources.Load<GameObject>(resName);
            if (fromRes != null)
                return fromRes;

#if UNITY_EDITOR
            string path = "Assets/Models/Component" + tag + ".prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }


        static bool TryWorldOrExperimentAction(string key, string label)
        {
            switch (key)
            {
                case "new run":
                case "reset":
                    QhysicsLabActions.ResetCircuitLab();
                    Debug.Log("QhysicsGadgets: New Run -> CircuitLab.Reset() + clear spawned gadgets");
                    return true;
                case "reset pose":
                {
                    LabPlayerSpawn.EnsureApplied();
                    var desktop = DesktopPlayerController.Instance;
                    if (desktop != null)
                    {
                        var origin = GameObject.Find(LabPlayerSpawn.OriginName);
                        if (origin != null)
                            desktop.Bind(origin.transform);
                    }
                    Debug.Log("QhysicsGadgets: Reset Pose -> plaza spawn");
                    return true;
                }
                case "induction":
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var lab = InductionLabBootstrap.EnsureLabInScene(scene);
                    if (lab != null)
                        lab.BuildLab();
                    Debug.Log("QhysicsGadgets: Induction lab ensured");
                    return true;
                }
                case "scale":
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var lab = InductionLabBootstrap.EnsureLabInScene(scene);
                    if (lab != null)
                        lab.BuildLab();
                    var scale = Object.FindAnyObjectByType<ScaleEngine>(FindObjectsInactive.Include);
                    if (scale == null)
                    {
                        Debug.LogWarning("QhysicsGadgets: Scale Engine missing after Induction ensure.");
                        return true;
                    }
                    scale.StepIn();
                    Debug.Log("QhysicsGadgets: Scale -> " + scale.CurrentScaleName);
                    return true;
                }
                case "save":
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var lab = InductionLabBootstrap.EnsureLabInScene(scene);
                    if (lab != null)
                        lab.BuildLab();
                    var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
                    if (runner == null)
                    {
                        Debug.LogWarning("QhysicsGadgets: Save - ExperimentRunner missing after Induction ensure.");
                        return true;
                    }
                    string path = runner.Save();
                    Debug.Log("QhysicsGadgets: Save -> " + (path ?? "(failed)"));
                    return true;
                }
                case "load":
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var lab = InductionLabBootstrap.EnsureLabInScene(scene);
                    if (lab != null)
                        lab.BuildLab();
                    var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
                    if (runner == null)
                    {
                        Debug.LogWarning("QhysicsGadgets: Load - ExperimentRunner missing after Induction ensure.");
                        return true;
                    }
                    bool ok = runner.LoadLatest();
                    Debug.Log("QhysicsGadgets: LoadLatest -> " + (ok ? runner.StatusLine : "no run file"));
                    return true;
                }
                case "teleport":
                {
                    LabPlayerSpawn.EnsureApplied();
                    var desktop = DesktopPlayerController.Instance;
                    if (desktop != null)
                    {
                        var origin = GameObject.Find(LabPlayerSpawn.OriginName);
                        if (origin != null)
                            desktop.Bind(origin.transform);
                    }
                    Debug.Log("QhysicsGadgets: Teleport -> plaza spawn");
                    return true;
                }
                case "sky":
                {
                    string sky = QhysicsLabActions.CycleSky();
                    Debug.Log("QhysicsGadgets: Sky -> " + sky);
                    return true;
                }
                default:
                    return false;
            }
        }

        static GameObject SpawnCubitRod(Vector3 worldPos)
        {
            // Always build a fresh rod so we never steal/duplicate the table original visuals.
            var go = new GameObject(CubitRod.RootName + "_Desktop");
            go.transform.position = worldPos;
            var rod = go.AddComponent<CubitRod>();
            rod.EnsureBuilt();
            EnsureGrabPhysics(go, 0.15f);
            return go;
        }

        static GameObject SpawnFieldLensGadget(Vector3 worldPos)
        {
            // Ensure scene FieldLens exists (on RealityEngine / Induction lab), then spawn a handheld proxy.
            FieldLens lens = Object.FindFirstObjectByType<FieldLens>(FindObjectsInactive.Include);
            if (lens == null)
            {
                var lab = Object.FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
                if (lab != null)
                {
                    lab.EnsureFieldLens();
                    lens = Object.FindFirstObjectByType<FieldLens>(FindObjectsInactive.Include);
                }
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Gadget_FieldLens";
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(0.12f, 0.02f, 0.12f);
            Object.Destroy(go.GetComponent<Collider>());
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(sh);
                Color c = new Color(0.2f, 0.75f, 0.95f, 0.85f);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", c);
                mat.color = c;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 0.55f));
                }
                r.sharedMaterial = mat;
            }

            var driver = go.AddComponent<FieldLensHandheld>();
            driver.Bind(lens);
            driver.EnsureBuilt();
            EnsureGrabPhysics(go, 0.12f);
            return go;
        }

        static GameObject SpawnHandheldMagnet(Vector3 worldPos, bool pureDipole)
        {
            // Prefer cloning a live lab magnet that already carries MagneticDipole.
            GameObject clone = FindNamedLoose(pureDipole ? "Dipole" : "Magnet");
            if (clone == null)
                clone = FindNamedLoose(pureDipole ? "Magnet" : "Dipole");
            if (clone != null && clone.GetComponent<MagneticDipole>() != null)
            {
                GameObject go = Object.Instantiate(clone);
                go.name = pureDipole ? HandheldMagnet.DipoleRootName : HandheldMagnet.MagnetRootName;
                go.SetActive(true);
                go.transform.SetParent(null, true);
                go.transform.position = worldPos;
                EnableColliders(go);
                EnsureGrabPhysics(go, 0.25f);
                return go;
            }

            // Fallback: build a real classical dipole gadget (never a dead cube proxy).
            var root = new GameObject(pureDipole ? HandheldMagnet.DipoleRootName : HandheldMagnet.MagnetRootName);
            root.transform.position = worldPos;
            var hm = root.AddComponent<HandheldMagnet>();
            hm.Configure(pureDipole);
            hm.EnsureBuilt();
            EnsureGrabPhysics(root, 0.2f);
            return root;
        }

        static GameObject SpawnHandheldCoil(Vector3 worldPos)
        {
            // Prefer cloning a live lab Coil that already carries InductionCoil - never steal the original.
            GameObject clone = FindNamedLoose("Coil");
            if (clone != null && clone.GetComponent<InductionCoil>() != null)
            {
                GameObject go = Object.Instantiate(clone);
                go.name = HandheldCoil.RootName;
                go.SetActive(true);
                go.transform.SetParent(null, true);
                go.transform.position = worldPos;
                EnableColliders(go);
                EnsureGrabPhysics(go, 0.25f);
                var coil = go.GetComponent<InductionCoil>();
                HandheldCoil.BindMagnetsTo(coil);
                return go;
            }

            // Fallback: build a real classical InductionCoil + InductionCircuit gadget (never a dead cube).
            var root = new GameObject(HandheldCoil.RootName);
            root.transform.position = worldPos;
            var hc = root.AddComponent<HandheldCoil>();
            hc.EnsureBuilt();
            EnsureGrabPhysics(root, 0.25f);
            return root;
        }

        static GameObject SpawnNamedCloneOrProxy(string name, Vector3 worldPos, Color color)
        {
            GameObject src = FindNamedLoose(name);
            if (src != null)
            {
                GameObject go = Object.Instantiate(src);
                go.name = "Gadget_" + name;
                go.SetActive(true);
                go.transform.SetParent(null, true);
                go.transform.position = worldPos;
                EnableColliders(go);
                EnsureGrabPhysics(go, 0.2f);
                return go;
            }
            return SpawnPrimitiveProxy(name, worldPos, color);
        }

        static GameObject FindNamedLoose(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
                return found;
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                    continue;
                if (string.Equals(all[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                    return all[i].gameObject;
            }
            return null;
        }


        static GameObject SpawnLoadResistor(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical series R_load on nearest InductionCoil.
            var go = SpawnPrimitiveProxy("Resistor", worldPos, new Color(0.75f, 0.45f, 0.2f));
            go.name = LoadResistorGadget.RootName;
            go.transform.localScale = new Vector3(0.14f, 0.04f, 0.06f);
            var lr = go.GetComponent<LoadResistorGadget>();
            if (lr == null)
                lr = go.AddComponent<LoadResistorGadget>();
            lr.EnsureBuilt();
            return go;
        }

        static GameObject SpawnLoadSwitch(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical series open/closed on nearest InductionCoil.
            var go = SpawnPrimitiveProxy("Switch", worldPos, new Color(0.35f, 0.55f, 0.35f));
            go.name = LoadSwitchGadget.RootName;
            go.transform.localScale = new Vector3(0.1f, 0.05f, 0.14f);
            var sw = go.GetComponent<LoadSwitchGadget>();
            if (sw == null)
                sw = go.AddComponent<LoadSwitchGadget>();
            sw.EnsureBuilt();
            return go;
        }

        static GameObject SpawnLoadBattery(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical series EMF on nearest InductionCoil.
            var go = SpawnPrimitiveProxy("Battery", worldPos, new Color(0.85f, 0.65f, 0.2f));
            go.name = LoadBatteryGadget.RootName;
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.16f);
            var bat = go.GetComponent<LoadBatteryGadget>();
            if (bat == null)
                bat = go.AddComponent<LoadBatteryGadget>();
            bat.EnsureBuilt();
            return go;
        }

        static GameObject SpawnLoadWire(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical series jumper R_load on nearest InductionCoil.
            var go = SpawnPrimitiveProxy("Wire", worldPos, new Color(0.72f, 0.45f, 0.18f));
            go.name = LoadWireGadget.RootName;
            go.transform.localScale = new Vector3(0.22f, 0.025f, 0.025f);
            var w = go.GetComponent<LoadWireGadget>();
            if (w == null)
                w = go.AddComponent<LoadWireGadget>();
            w.EnsureBuilt();
            return go;
        }


        static GameObject SpawnLoadMotor(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical |I| spin on nearest InductionCircuit (Resistor owns R_load).
            var go = SpawnPrimitiveProxy("Motor", worldPos, new Color(0.35f, 0.55f, 0.85f));
            go.name = LoadMotorGadget.RootName;
            go.transform.localScale = new Vector3(0.1f, 0.08f, 0.12f);
            var m = go.GetComponent<LoadMotorGadget>();
            if (m == null)
                m = go.AddComponent<LoadMotorGadget>();
            m.EnsureBuilt();
            return go;
        }

        static GameObject SpawnLoadSolar(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical irradiance/photocurrent series EMF on nearest InductionCoil.
            var go = SpawnPrimitiveProxy("Solar", worldPos, new Color(0.25f, 0.75f, 0.9f));
            go.name = LoadSolarGadget.RootName;
            go.transform.localScale = new Vector3(0.2f, 0.02f, 0.14f);
            var s = go.GetComponent<LoadSolarGadget>();
            if (s == null)
                s = go.AddComponent<LoadSolarGadget>();
            s.EnsureBuilt();
            return go;
        }

        static GameObject SpawnLoadCapacitor(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical series C on nearest InductionCoil (RC with Battery/Resistor).
            var go = SpawnPrimitiveProxy("Capacitor", worldPos, new Color(0.55f, 0.5f, 0.25f));
            go.name = LoadCapacitorGadget.RootName;
            go.transform.localScale = new Vector3(0.1f, 0.08f, 0.06f);
            var c = go.GetComponent<LoadCapacitorGadget>();
            if (c == null)
                c = go.AddComponent<LoadCapacitorGadget>();
            c.EnsureBuilt();
            return go;
        }

        static GameObject SpawnLoadBulb(Vector3 worldPos)
        {
            // Real CIRCUIT tool: classical |I| glow on nearest InductionCircuit (Resistor owns R_load).
            var go = SpawnPrimitiveProxy("Lamp", worldPos, new Color(0.55f, 0.45f, 0.2f));
            go.name = LoadBulbGadget.RootName;
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            var lb = go.GetComponent<LoadBulbGadget>();
            if (lb == null)
                lb = go.AddComponent<LoadBulbGadget>();
            lb.EnsureBuilt();
            return go;
        }

        static GameObject SpawnMultimeterStub(Vector3 worldPos)
        {
            // Real MEASURE tool: live InductionCircuit EMF/I/Phi (classical Faraday).
            var go = SpawnPrimitiveProxy("Multimeter", worldPos, new Color(0.18f, 0.22f, 0.28f));
            go.name = MultimeterProbe.RootName;
            go.transform.localScale = new Vector3(0.12f, 0.04f, 0.18f);
            var probe = go.GetComponent<MultimeterProbe>();
            if (probe == null)
                probe = go.AddComponent<MultimeterProbe>();
            probe.EnsureBuilt();
            return go;
        }

        static GameObject SpawnStopwatch(Vector3 worldPos)
        {
            // Real MEASURE tool: wall-clock timer for magnet sweeps / experiment runs.
            var go = SpawnPrimitiveProxy("Stopwatch", worldPos, new Color(0.22f, 0.2f, 0.12f));
            go.name = ExperimentStopwatch.RootName;
            go.transform.localScale = new Vector3(0.1f, 0.04f, 0.1f);
            var sw = go.GetComponent<ExperimentStopwatch>();
            if (sw == null)
                sw = go.AddComponent<ExperimentStopwatch>();
            sw.EnsureBuilt();
            return go;
        }

        static GameObject SpawnFieldProbe(Vector3 worldPos)
        {
            // Real MEASURE tool: classical B(r) at tip from MagneticDipole sources.
            var go = SpawnPrimitiveProxy("FieldProbe", worldPos, new Color(0.25f, 0.45f, 0.7f));
            go.name = FieldProbe.RootName;
            go.transform.localScale = new Vector3(0.04f, 0.04f, 0.16f);
            var probe = go.GetComponent<FieldProbe>();
            if (probe == null)
                probe = go.AddComponent<FieldProbe>();
            probe.EnsureBuilt();
            return go;
        }
        static GameObject SpawnPrimitiveProxy(string name, Vector3 worldPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Gadget_" + name;
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(0.1f, 0.05f, 0.15f);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                mat.color = color;
                r.sharedMaterial = mat;
            }
            EnsureGrabPhysics(go, 0.2f);
            return go;
        }

        static void EnsureGrabPhysics(GameObject go, float mass)
        {
            if (go == null)
                return;
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = false;
            rb.useGravity = true;
            if (go.GetComponent<XRGrabInteractable>() == null)
                go.AddComponent<XRGrabInteractable>();
            EnableColliders(go);
        }

        static void EnableColliders(GameObject go)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    cols[i].enabled = true;
            }
        }
    }

    /// <summary>
    /// Handheld Field Lens proxy: while held (or near camera), XR activate / N/P, or desktop LMB/scroll steps FieldLens layers.
    /// Drives the existing scene FieldLens host ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â does not spawn a second lens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldLensHandheld : MonoBehaviour, IDesktopActivatable
    {
        public const string Honesty = "Classical / conceptual layer peel. Not a microscope.";

        FieldLens _lens;
        XRGrabInteractable _grab;
        TextMeshPro _readout;
        float _inputCooldown;
        float _nextRefresh;

        public void Bind(FieldLens lens)
        {
            _lens = lens;
            _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
        }

        public void EnsureBuilt()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_lens == null)
                _lens = Object.FindFirstObjectByType<FieldLens>(FindObjectsInactive.Include);
            WireGrab();
            if (_readout == null)
                BuildReadout();
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            WireGrab();
        }

        void OnDisable()
        {
            UnwireGrab();
        }

        void Update()
        {
            if (_lens == null)
                _lens = Object.FindFirstObjectByType<FieldLens>(FindObjectsInactive.Include);

            if (IsActiveContext() && Time.unscaledTime >= _inputCooldown)
            {
                if (WasNextPressed())
                {
                    _inputCooldown = Time.unscaledTime + 0.2f;
                    StepNext();
                }
                else if (WasPrevPressed())
                {
                    _inputCooldown = Time.unscaledTime + 0.2f;
                    StepPrev();
                }
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.2f;
                RefreshText();
            }
        }

        void WireGrab()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }

        void UnwireGrab()
        {
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
        }

        void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
        {
            if (Time.unscaledTime < _inputCooldown)
                return;
            _inputCooldown = Time.unscaledTime + 0.2f;
            StepNext();
        }

        public void DesktopActivate(int delta)
        {
            if (delta == 0)
                return;
            if (Time.unscaledTime < _inputCooldown)
                return;
            _inputCooldown = Time.unscaledTime + 0.15f;
            if (delta > 0)
                StepNext();
            else
                StepPrev();
        }

        void StepNext()
        {
            if (_lens == null)
                return;
            _lens.StepNext();
            RefreshText();
            Debug.Log("QHYSICS: Field Lens -> " + _lens.CurrentLayerName + " [" + _lens.CurrentHonestyTag + "]");
        }

        void StepPrev()
        {
            if (_lens == null)
                return;
            _lens.StepPrevious();
            RefreshText();
            Debug.Log("QHYSICS: Field Lens -> " + _lens.CurrentLayerName + " [" + _lens.CurrentHonestyTag + "]");
        }

        bool IsActiveContext()
        {
            if (_grab != null && _grab.isSelected)
                return true;
            Camera cam = Camera.main;
            if (cam == null)
                return false;
            return (transform.position - cam.transform.position).sqrMagnitude < 2.5f * 2.5f;
        }

        static bool WasNextPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null
                && (Keyboard.current.nKey.wasPressedThisFrame || Keyboard.current.rightBracketKey.wasPressedThisFrame))
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightBracket))
                return true;
#endif
            return false;
        }

        static bool WasPrevPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null
                && (Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.leftBracketKey.wasPressedThisFrame))
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.LeftBracket))
                return true;
#endif
            return false;
        }

        void BuildReadout()
        {
            Transform existing = transform.Find("Readout");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("Readout");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.08f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 24f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.35f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(36f, 18f);
            _readout.text = "FIELD LENS\nNormal\nN/P LMB/scroll";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;
            if (_lens == null)
            {
                _readout.text = "FIELD LENS\n(no host)\nspawn Induction first";
                return;
            }
            _readout.text =
                "FIELD LENS\n"
                + _lens.CurrentLayerName + "\n"
                + "[" + _lens.CurrentHonestyTag + "]\n"
                + "N/P LMB/scroll";
        }
    }
}

