using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Visualization;

namespace RealityEngine.EditorTools
{
    public static class LabLandscapeMenu
    {
        const string LandscapePath = "Reality Engine/Apply Lab Landscape";
        const string KhufuPath = "Reality Engine/Place Khufu";

        [MenuItem(LandscapePath)]
        public static void ApplyLabLandscape()
        {
            LabLandscapeApplier applier = LabLandscapeApplier.EnsureApplied();
            applier.ApplyNow(true);
            Mark(applier);
            Selection.activeGameObject = applier != null ? applier.gameObject : null;
            Debug.Log("Applied Reality Engine Giza plateau + Khufu replica. Play Faraday.unity, look past the circuit table (+Z). Teleport to the north face; entrance is ~17 m up.");
        }

        [MenuItem(LandscapePath, true)]
        public static bool ApplyLabLandscapeValidate()
        {
            return true;
        }

        [MenuItem(KhufuPath)]
        public static void PlaceKhufu()
        {
            ApplyLabLandscape();
        }

        [MenuItem(KhufuPath, true)]
        public static bool PlaceKhufuValidate()
        {
            return true;
        }

        static void Mark(LabLandscapeApplier applier)
        {
            if (applier != null && applier.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);
        }
    }
}
