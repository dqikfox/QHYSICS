using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.EditorTools
{
    public static class QhysicsPlayerSystemsMenu
    {
        const string Path = "Reality Engine/QHYSICS/Ensure Player Systems";

        [MenuItem(Path)]
        public static void EnsurePlayerSystems()
        {
            QhysicsUiBootstrap.EnsurePlaced();
            var boot = QhysicsPlayerSystemsBootstrap.Ensure();
            if (boot != null && boot.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Selection.activeGameObject = boot != null ? boot.gameObject : null;
            Debug.Log("Reality Engine: ensured QHYSICS player systems (operator select, carry inventory, training combat, plaza pickups/drones). Runtime also auto-ensures on Play.");
        }

        [MenuItem(Path, true)]
        public static bool EnsurePlayerSystemsValidate() => true;
    }
}
