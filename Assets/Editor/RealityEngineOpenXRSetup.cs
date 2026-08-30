using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Reality Engine 0.2: assign Unity OpenXR as the XR loader for Windows and Android,
/// and remove the deprecated Oculus XR Plugin loader if it is still assigned.
/// Uses only public XR Plug-in Management 4.x / 6.x APIs verified in PackageCache.
/// </summary>
public static class RealityEngineOpenXRSetup
{
    const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
    const string OculusLoaderType = "Unity.XR.Oculus.OculusLoader";

    [MenuItem("Reality Engine/Enable OpenXR for Quest")]
    public static void EnableOpenXRForQuest()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            Debug.LogError("Reality Engine: cannot change XR loaders while in Play mode.");
            LogManualChecklist();
            return;
        }

        XRGeneralSettingsPerBuildTarget perTarget;
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out perTarget) || perTarget == null)
        {
            Debug.LogError(
                "Reality Engine: XRGeneralSettingsPerBuildTarget not found. " +
                "Open Edit > Project Settings > XR Plug-in Management, then check OpenXR on the Windows tab and the Android tab.");
            LogManualChecklist();
            return;
        }

        ConfigureGroup(perTarget, BuildTargetGroup.Standalone);
        ConfigureGroup(perTarget, BuildTargetGroup.Android);
        AssetDatabase.SaveAssets();
        LogManualChecklist();
    }

    static void ConfigureGroup(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group)
    {
        if (!perTarget.HasSettingsForBuildTarget(group))
            perTarget.CreateDefaultSettingsForBuildTarget(group);
        if (!perTarget.HasManagerSettingsForBuildTarget(group))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

        XRManagerSettings manager = perTarget.ManagerSettingsForBuildTarget(group);
        if (manager == null)
        {
            Debug.LogError(
                "Reality Engine: no XRManagerSettings for " + group + ". " +
                "Edit > Project Settings > XR Plug-in Management > " + GroupTabName(group) +
                " tab: check OpenXR, uncheck Oculus.");
            return;
        }

        bool assigned = XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, group);
        if (assigned)
            Debug.Log("Reality Engine: assigned OpenXR loader for " + group + ".");
        else
            Debug.LogWarning(
                "Reality Engine: XRPackageMetadataStore.AssignLoader did not assign OpenXR for " + group + ". " +
                "Edit > Project Settings > XR Plug-in Management > " + GroupTabName(group) +
                ": check OpenXR and uncheck Oculus / Oculus XR Plugin.");

        bool removed = XRPackageMetadataStore.RemoveLoader(manager, OculusLoaderType, group);
        if (removed)
            Debug.Log("Reality Engine: removed Oculus loader for " + group + ".");

        List<XRLoader> keep = new List<XRLoader>();
        foreach (XRLoader loader in manager.activeLoaders)
        {
            if (loader == null)
                continue;
            if (loader.GetType().FullName == OculusLoaderType)
                continue;
            keep.Add(loader);
        }

        if (keep.Count != manager.activeLoaders.Count)
        {
            manager.TrySetLoaders(keep);
            EditorUtility.SetDirty(manager);
        }

        bool openXrPresent = false;
        foreach (XRLoader loader in manager.activeLoaders)
        {
            if (loader != null && loader.GetType().FullName == OpenXRLoaderType)
                openXrPresent = true;
        }

        if (!openXrPresent)
        {
            Debug.LogWarning(
                "Reality Engine: OpenXR loader is not in the " + group + " list. " +
                "Project Settings > XR Plug-in Management > " + GroupTabName(group) +
                ": check OpenXR, uncheck Oculus / Oculus XR Plugin.");
        }
    }

    static string GroupTabName(BuildTargetGroup group)
    {
        if (group == BuildTargetGroup.Standalone)
            return "Windows";
        if (group == BuildTargetGroup.Android)
            return "Android";
        return group.ToString();
    }

    static void LogManualChecklist()
    {
        Debug.Log(
            "Reality Engine OpenXR remaining checks:\n" +
            "1. Edit > Project Settings > XR Plug-in Management > Windows: OpenXR checked, Oculus unchecked.\n" +
            "2. Edit > Project Settings > XR Plug-in Management > Android: OpenXR checked, Oculus unchecked.\n" +
            "3. XR Plug-in Management > OpenXR (Windows and Android): Meta Quest feature group enabled.\n" +
            "4. Interaction Profiles: Meta Quest Touch Plus Controller Profile (Quest 3S); add Oculus Touch Controller Profile if Link needs it.\n" +
            "5. Player Settings > Android: Scripting Backend IL2CPP, ARM64, Minimum API Level 29, Texture Compression ASTC.\n" +
            "6. Open Assets/Scenes/Faraday.unity, turn Game view Gizmos off, Play, Quest 3S via Link.");
    }
}
