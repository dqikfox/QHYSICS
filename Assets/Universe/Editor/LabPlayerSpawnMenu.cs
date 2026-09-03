using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.XR;

namespace RealityEngine.EditorTools
{
    public static class LabPlayerSpawnMenu
    {
        const string MenuPath = "Reality Engine/Reset Player at Lab";

        [MenuItem(MenuPath)]
        public static void ResetPlayerAtLab()
        {
            LabPlayerSpawn applier = LabPlayerSpawn.EnsureApplied();
            applier.ApplyNow(true);

            if (applier != null && applier.gameObject.scene.IsValid() && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);

            Selection.activeGameObject = applier != null ? applier.gameObject : null;
            Debug.Log("Reality Engine: Reset Player at Lab. XR Origin unparented from MountainScene, feet on the plaza, north of the circuit table, facing Khufu. Teleport rewired. Ctrl+R then Play if the open Editor overwrote pose.");
        }

        [MenuItem(MenuPath, true)]
        public static bool ResetPlayerAtLabValidate()
        {
            return true;
        }
    }
}
