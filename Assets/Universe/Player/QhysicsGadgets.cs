using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Spawns CircuitLab parts for desktop hotbar + VR toolbelt Build chips.
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
            if (key == "multimeter")
                return SpawnMultimeterStub(worldPos);
            if (key == "resistor")
                return SpawnPrimitiveProxy("Resistor", worldPos, new Color(0.75f, 0.45f, 0.2f));
            if (key == "magnet")
                return SpawnPrimitiveProxy("Magnet", worldPos, new Color(0.7f, 0.15f, 0.15f));

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
            var dispensers = Object.FindObjectsByType<Dispenser>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < dispensers.Length; i++)
            {
                var d = dispensers[i];
                if (d == null || d.componentTag != tag)
                    continue;
                if (d.transform.childCount <= 0)
                    continue;
                return d.transform.GetChild(0).gameObject;
            }
            // Tagged loose components as last resort (do not prefer these)
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
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            return go;
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
}
