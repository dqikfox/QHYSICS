#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Player;
using RealityEngine.UI;
using RealityEngine.XR;

namespace RealityEngine.EditorTools
{
    public static class DesktopPlayerMenu
    {
        const string PathPlace = "Reality Engine/Place Desktop Player";
        const string PathEnsure = "Reality Engine/Ensure Player Character";

        [MenuItem(PathPlace)]
        [MenuItem(PathEnsure)]
        public static void PlaceDesktopPlayer()
        {
            LabPlayerSpawn.EnsureApplied();
            var boot = QhysicsDesktopBootstrap.Ensure();
            QhysicsUiBootstrap.EnsurePlaced();
            if (boot != null && boot.gameObject.scene.IsValid() && !Application.isPlaying)
            {
                EditorUtility.SetDirty(boot.gameObject);
                EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            }

            Selection.activeGameObject = boot != null ? boot.gameObject : null;
            Debug.Log(
                "Reality Engine: Ensure Player Character. XR Origin on plaza + desktop body/hands under Origin. " +
                "WASD + mouse look when no XR headset. Hotbar 1-8. E grab / F drop. Ctrl+S only if you want scene persist; Ctrl+P to test.");
        }

        [MenuItem(PathPlace, true)]
        [MenuItem(PathEnsure, true)]
        public static bool PlaceDesktopPlayerValidate() => true;
    }
}
#endif
