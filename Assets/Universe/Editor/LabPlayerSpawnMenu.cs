using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.XR;

namespace RealityEngine.EditorTools
{
    public static class LabPlayerSpawnMenu
    {
        const string ResetPath = "Reality Engine/Reset Player at Lab";
        const string FixPath = "Reality Engine/Fix Player Spawn";

        [MenuItem(FixPath)]
        public static void FixPlayerSpawn()
        {
            ApplyAndMarkDirty("Fix Player Spawn");
        }

        [MenuItem(FixPath, true)]
        public static bool FixPlayerSpawnValidate()
        {
            return true;
        }

        [MenuItem(ResetPath)]
        public static void ResetPlayerAtLab()
        {
            ApplyAndMarkDirty("Reset Player at Lab");
        }

        [MenuItem(ResetPath, true)]
        public static bool ResetPlayerAtLabValidate()
        {
            return true;
        }

        static void ApplyAndMarkDirty(string label)
        {
            LabPlayerSpawn applier = LabPlayerSpawn.EnsureApplied();
            if (applier != null)
                applier.ApplyNow(true);

            Transform origin = null;
            GameObject originGo = GameObject.Find(LabPlayerSpawn.OriginName);
            if (originGo != null)
                origin = originGo.transform;

            if (origin != null && origin.gameObject.scene.IsValid() && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            else if (applier != null && applier.gameObject.scene.IsValid() && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);

            Selection.activeGameObject = origin != null ? origin.gameObject : (applier != null ? applier.gameObject : null);

            string y = origin != null ? origin.position.y.ToString("F2") : "?";
            Debug.Log(
                "Reality Engine: " + label +
                ". XR Origin unparented from MountainScene, feet on the plaza (Y=" + y +
                "), north of the circuit table, facing Khufu. Floor tracking + Main Camera enabled. LocomotionSystem/Teleport XR Origin wired. Ctrl+S then Ctrl+P. Quest needs Link — Ctrl+P is not an APK.");
        }
    }
}
