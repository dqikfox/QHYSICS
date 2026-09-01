using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Experiments;
using RealityEngine.Visualization;

namespace RealityEngine.EditorTools
{
    public static class CircuitLabStyleMenu
    {
        const string MenuPath = "Reality Engine/Apply Circuit Lab Style";

        [MenuItem(MenuPath)]
        public static void ApplyCircuitLabStyle()
        {
            CircuitLabStyleApplier applier = CircuitLabStyleApplier.EnsureApplied();
            applier.ApplyNow(true);

            InductionLabBootstrap bootstrap = Object.FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
            if (bootstrap != null)
                bootstrap.EnsureLabStyle();

            if (applier != null && applier.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);

            Selection.activeGameObject = applier != null ? applier.gameObject : null;
            Debug.Log("Applied Reality Engine Circuit Lab style (graphite bench, copper conductors, matte housings). Play Faraday to restyle spawned pegs/components.");
        }

        [MenuItem(MenuPath, true)]
        public static bool ApplyCircuitLabStyleValidate()
        {
            return true;
        }
    }
}
