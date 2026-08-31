using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Experiments;
using RealityEngine.Visualization;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// Places the Reality Engine v0.4 induction lab (with Field Lens) into the currently open scene.
    /// Use this while Faraday.unity is open, then save the scene.
    /// </summary>
    public static class RealityEngineInductionLabMenu
    {
        const string MenuPath = "Reality Engine/Place Induction Lab in Open Scene";

        [MenuItem(MenuPath)]
        public static void PlaceInductionLab()
        {
            InductionLabBootstrap existing = Object.FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.EnsureFieldLens();
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                if (existing.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Debug.Log("Induction Lab already exists. Field Lens v0.4 ensured on: " + existing.gameObject.name, existing);
                return;
            }

            var go = new GameObject(InductionLabBootstrap.LabRootName);
            Undo.RegisterCreatedObjectUndo(go, "Place Induction Lab");
            if (go.GetComponent<ModelCard>() == null)
                go.AddComponent<ModelCard>();
            var bootstrap = go.AddComponent<InductionLabBootstrap>();
            bootstrap.BuildLab();
            Selection.activeGameObject = go;
            if (go.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log(
                "Placed Induction Lab v0.4 (Field Lens) in scene '" + go.scene.name +
                "'. Save Faraday.unity to keep it. On Play, the bootstrap also builds if Magnet is missing.",
                go);
        }

        [MenuItem(MenuPath, true)]
        public static bool PlaceInductionLabValidate()
        {
            return true;
        }
    }
}