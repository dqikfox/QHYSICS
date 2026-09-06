#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Player;
using RealityEngine.XR;

namespace RealityEngine.EditorTools
{
    public static class DesktopPlayerMenu
    {
        const string Path = "Reality Engine/Place Desktop Player";

        [MenuItem(Path)]
        public static void PlaceDesktopPlayer()
        {
            LabPlayerSpawn.EnsureApplied();
            var boot = QhysicsDesktopBootstrap.Ensure();
            if (boot != null && boot.gameObject.scene.IsValid() && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);

            Selection.activeGameObject = boot != null ? boot.gameObject : null;
            Debug.Log(
                "Reality Engine: Place Desktop Player. Keyboard WASD + mouse look when no XR headset. " +
                "Hotbar 1-8 / Q / scroll. E grab, F/R drop. Ctrl+S then Ctrl+P without Link to test.");
        }

        [MenuItem(Path, true)]
        public static bool PlaceDesktopPlayerValidate() => true;
    }
}
#endif
