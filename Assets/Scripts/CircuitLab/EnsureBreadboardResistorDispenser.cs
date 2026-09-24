using UnityEngine;

/// <summary>
/// Ensures a breadboard Resistor dispenser exists at runtime so Current Control
/// (challenge 2) is playable without editing Faraday.unity.
/// Clones an existing Bulb/Battery dispenser near the circuit table and attaches
/// a procedural ComponentResistor child tagged "Resistor".
/// </summary>
public static class EnsureBreadboardResistorDispenser
{
    const string DispenserName = "DispenserResistor";
    const float SiblingOffsetMeters = 0.18f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoEnsure()
    {
        if (!Application.isPlaying)
            return;
        CircuitLab lab = Object.FindAnyObjectByType<CircuitLab>();
        if (lab == null)
            return;
        Ensure();
    }

    /// <summary>Idempotent: creates at most one Resistor dispenser near existing dispensers.</summary>
    public static Dispenser Ensure()
    {
        EnsureResistorTagExists();

        Dispenser[] all = Object.FindObjectsByType<Dispenser>(FindObjectsInactive.Include);
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].componentTag == Dispenser.ComponentTag.Resistor)
                    return all[i];
            }
        }

        Dispenser donor = FindDonorDispenser(all);
        GameObject go;
        if (donor != null)
        {
            go = Object.Instantiate(donor.gameObject);
            go.name = DispenserName;
            // Offset beside the donor so it is visible on the circuit table shelf.
            go.transform.SetParent(donor.transform.parent, true);
            go.transform.position = donor.transform.position + donor.transform.right * SiblingOffsetMeters;
            go.transform.rotation = donor.transform.rotation;
            go.transform.localScale = donor.transform.localScale;
        }
        else
        {
            go = new GameObject(DispenserName);
            CircuitLab lab = Object.FindAnyObjectByType<CircuitLab>();
            if (lab != null)
            {
                go.transform.position = lab.transform.position + Vector3.up * 0.2f + Vector3.forward * 0.3f;
            }
        }

        try { go.tag = "Dispenser"; }
        catch (UnityException) { /* Dispenser tag should already exist */ }

        // Strip donor component children; keep visual mesh children that are not circuit parts.
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = go.transform.GetChild(i);
            if (child == null) continue;
            if (child.GetComponent<CircuitComponent>() != null
                || child.name.StartsWith("Component")
                || child.GetComponent<Resistor>() != null)
            {
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }

        Dispenser dispenser = go.GetComponent<Dispenser>();
        if (dispenser == null)
            dispenser = go.AddComponent<Dispenser>();
        dispenser.componentTag = Dispenser.ComponentTag.Resistor;

        // Ensure a template resistor child for the dispenser shelf.
        if (go.transform.childCount == 0 || go.GetComponentInChildren<Resistor>(true) == null)
        {
            Resistor part = Resistor.CreateBreadboardPart(go.transform, "ComponentResistor");
            var rb = part.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }
        }

        return dispenser;
    }

    static Dispenser FindDonorDispenser(Dispenser[] all)
    {
        if (all == null) return null;
        Dispenser bulb = null;
        Dispenser battery = null;
        Dispenser any = null;
        for (int i = 0; i < all.Length; i++)
        {
            Dispenser d = all[i];
            if (d == null) continue;
            any = d;
            if (d.componentTag == Dispenser.ComponentTag.Bulb && bulb == null)
                bulb = d;
            else if (d.componentTag == Dispenser.ComponentTag.Battery && battery == null)
                battery = d;
        }
        return bulb != null ? bulb : (battery != null ? battery : any);
    }

    /// <summary>
    /// TagManager must list "Resistor" or GameObject.tag / FindGameObjectsWithTag throw.
    /// Editor path mutates TagManager; play mode only logs if missing.
    /// </summary>
    public static void EnsureResistorTagExists()
    {
#if UNITY_EDITOR
        EnsureResistorTagInTagManager();
#else
        try
        {
            // Probe without allocating: CompareTag returns false if missing? Actually throws if undefined.
            var probe = new GameObject("~ResistorTagProbe");
            probe.tag = "Resistor";
            Object.Destroy(probe);
        }
        catch (UnityException e)
        {
            Debug.LogWarning("EnsureBreadboardResistorDispenser: 'Resistor' tag missing from TagManager. " + e.Message);
        }
#endif
    }

#if UNITY_EDITOR
    public static void EnsureResistorTagInTagManager()
    {
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
            return;
        var so = new UnityEditor.SerializedObject(assets[0]);
        var tags = so.FindProperty("tags");
        if (tags == null || !tags.isArray)
            return;
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == "Resistor")
                return;
        }
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "Resistor";
        so.ApplyModifiedProperties();
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("EnsureBreadboardResistorDispenser: added 'Resistor' tag to TagManager.");
    }
#endif
}
