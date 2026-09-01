using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Experiments;
using RealityEngine.Visualization;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// Places the Reality Engine v1.0 induction lab (gradient ledger + toy heat path) into the currently open scene.
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
                existing.EnsureScaleEngine();
                existing.EnsureExperimentFramework();
                existing.EnsureScientist();
                existing.EnsureChemistry();
                existing.EnsureBiology();
                existing.EnsureLabStyle();
                existing.EnsureThermo();
                existing.EnsureLedger();
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                if (existing.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Debug.Log("Induction Lab already exists. Field Lens + Scale Engine + Experiment + AI Scientist + Cu chemistry + biology ensured on: " + existing.gameObject.name, existing);
                return;
            }

            var go = new GameObject(InductionLabBootstrap.RealityEngineRootName);
            Undo.RegisterCreatedObjectUndo(go, "Place Induction Lab");
            if (go.GetComponent<ModelCard>() == null)
                go.AddComponent<ModelCard>();
            var bootstrap = go.AddComponent<InductionLabBootstrap>();
            bootstrap.BuildLab();
            bootstrap.EnsureScientist();
            bootstrap.EnsureChemistry();
            bootstrap.EnsureBiology();
            bootstrap.EnsureLabStyle();
            bootstrap.EnsureThermo();
            bootstrap.EnsureLedger();
            Selection.activeGameObject = go;
            if (go.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log(
                "Placed Induction Lab v1.0 (gradient ledger + toy heat path) in scene '" + go.scene.name +
                "'. Save Faraday.unity to keep it. On Play, the bootstrap also builds if Magnet is missing.",
                go);
        }


        const string Ensure10Path = "Reality Engine/Ensure 1.0 Gradient Lab";

        [MenuItem(Ensure10Path)]
        public static void EnsureGradientLab()
        {
            InductionLabBootstrap existing = Object.FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
            if (existing == null)
            {
                PlaceInductionLab();
                existing = Object.FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
            }
            if (existing == null)
                return;
            existing.EnsureFieldLens();
            existing.EnsureScaleEngine();
            existing.EnsureExperimentFramework();
            existing.EnsureScientist();
            existing.EnsureChemistry();
            existing.EnsureBiology();
            existing.EnsureLabStyle();
            existing.EnsureThermo();
            existing.EnsureLedger();
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing);
            if (existing.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
            Debug.Log("Reality Engine 1.0 gradient lab ensured (heat path + conservation ledger) on: " + existing.gameObject.name, existing);
        }

        [MenuItem(Ensure10Path, true)]
        public static bool EnsureGradientLabValidate()
        {
            return true;
        }

        [MenuItem(MenuPath, true)]
        public static bool PlaceInductionLabValidate()
        {
            return true;
        }
    }
}