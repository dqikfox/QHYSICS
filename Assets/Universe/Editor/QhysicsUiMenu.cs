using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.UI;
using TMPro;

namespace RealityEngine.EditorTools
{
    public static class QhysicsUiMenu
    {
        const string PlacePath = "Reality Engine/Place QHYSICS UI";
        const string FontResource = "Fonts & Materials/LiberationSans SDF";

        [MenuItem(PlacePath)]
        public static void PlaceQhysicsUi()
        {
            EnsureTmpFont();
            QhysicsUiBootstrap boot = QhysicsUiBootstrap.EnsurePlaced();
            if (boot != null && boot.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Selection.activeGameObject = boot != null ? boot.gameObject : null;
            Debug.Log("Reality Engine: placed QHYSICS UI v0.1 (world-space HUD, Toolbelt, ContextStrip, SimChip, PausePanel). Event Camera = XR camera; TrackedDeviceGraphicRaycaster. Toggle toolbelt with Menu/B/Grip or M/Tab.");
        }

        [MenuItem(PlacePath, true)]
        public static bool PlaceQhysicsUiValidate() => true;

        static void EnsureTmpFont()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontResource);
            if (font != null)
                return;
            string path = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
            {
                Debug.Log("QHYSICS UI: LiberationSans SDF found at " + path);
                return;
            }
            Debug.LogWarning("QHYSICS UI: LiberationSans SDF missing. Window -> TextMeshPro -> Import TMP Essential Resources.");
        }
    }
}
