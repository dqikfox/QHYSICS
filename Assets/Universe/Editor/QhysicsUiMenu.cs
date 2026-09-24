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
            Debug.Log("Reality Engine: placed QHYSICS lab UI (HUD, Toolbelt, Inspect/ContextStrip, SimChip, Pause, Onboarding strip). Event Camera = XR camera; TrackedDeviceGraphicRaycaster. Toggle toolbelt with Menu/B/Grip or M/Tab.");
        }

        [MenuItem(PlacePath, true)]
        public static bool PlaceQhysicsUiValidate() => true;

        const string EnsurePath = "Reality Engine/QHYSICS/Ensure UI (runtime bootstrap)";

        [MenuItem(EnsurePath)]
        public static void EnsureQhysicsUi()
        {
            EnsureTmpFont();
            QhysicsUiBootstrap boot = QhysicsUiBootstrap.EnsurePlaced();
            if (boot != null && boot.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Selection.activeGameObject = boot != null ? boot.gameObject : null;
            Debug.Log("Reality Engine: ensured QHYSICS lab UI (HUD, Toolbelt hip, Inspect, SimChip, Pause, Onboarding strip). Runtime also auto-ensures on Play.");
        }

        [MenuItem(EnsurePath, true)]
        public static bool EnsureQhysicsUiValidate() => true;


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
