using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using RealityEngine.Survey;
using RealityEngine.Visualization;
using RealityEngine.Experiments;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
            if (key == "resistor")
                return SpawnPrimitiveProxy("Resistor", worldPos, new Color(0.75f, 0.45f, 0.2f));
            if (key == "magnet" || key == "dipole")
                return SpawnMagnetOrDipole(worldPos);
            if (key == "coil")
                return SpawnNamedCloneOrProxy("Coil", worldPos, new Color(0.85f, 0.65f, 0.2f));
            if (key == "probe" || key == "stopwatch")
                return SpawnPrimitiveProxy(label.Trim(), worldPos, new Color(0.35f, 0.55f, 0.75f));

            // World / experiment chips â€” soft no-ops that still give feedback
            if (key == "teleport" || key == "scale" || key == "sky" || key == "reset pose"
                || key == "induction" || key == "new run" || key == "save" || key == "load")
            {
                Debug.Log("QhysicsGadgets: '" + label + "' is a world/experiment action â€” use menu/SimChip (no spawn).");
                return null;
            }

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
            EnsureGrabPhysics(go, 0.12f);
            return go;
        }

        static GameObject SpawnMagnetOrDipole(Vector3 worldPos)
        {
            GameObject clone = FindNamedLoose("Dipole");
            if (clone == null)
                clone = FindNamedLoose("Magnet");
            if (clone != null)
            {
                GameObject go = Object.Instantiate(clone);
                go.name = "Gadget_Magnet";
                go.SetActive(true);
                go.transform.SetParent(null, true);
                go.transform.position = worldPos;
                EnableColliders(go);
                EnsureGrabPhysics(go, 0.25f);
                return go;
            }
            return SpawnPrimitiveProxy("Magnet", worldPos, new Color(0.7f, 0.15f, 0.15f));
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

        static GameObject SpawnMultimeterStub(Vector3 worldPos)
        {
            var go = SpawnPrimitiveProxy("Multimeter", worldPos, new Color(0.2f, 0.25f, 0.3f));
            go.transform.localScale = new Vector3(0.12f, 0.04f, 0.18f);
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
    /// Handheld Field Lens proxy: while held (or near camera), steps FieldLens layers with [ ] / N P.
    /// Does not replace the scene FieldLens host â€” drives the existing one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldLensHandheld : MonoBehaviour
    {
        FieldLens _lens;
        XRGrabInteractable _grab;

        public void Bind(FieldLens lens)
        {
            _lens = lens;
            _grab = GetComponent<XRGrabInteractable>();
        }

        void Update()
        {
            if (_lens == null)
                _lens = Object.FindFirstObjectByType<FieldLens>(FindObjectsInactive.Include);
            if (_lens == null)
                return;

            bool held = _grab != null && _grab.isSelected;
            // Desktop: also treat as "active" when close to camera and no XR select
            if (!held)
            {
                Camera cam = Camera.main;
                if (cam != null && (transform.position - cam.transform.position).sqrMagnitude < 2.5f * 2.5f)
                    held = true;
            }
            if (!held)
                return;

            // Keyboard step is already on FieldLens when enableKeyboard; keep a local hint once.
        }
    }
}
