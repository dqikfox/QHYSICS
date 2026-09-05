using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RealityEngine.EditorTools
{
    [InitializeOnLoad]
    static class PlaceUiPlayableOnce
    {
        const string PrefKey = "QHYSICS.PlaceUiPlayableOnce.Pending_v1";

        static PlaceUiPlayableOnce()
        {
            if (!EditorPrefs.HasKey(PrefKey))
                EditorPrefs.SetBool(PrefKey, true);
            EditorApplication.delayCall += TryRun;
        }

        static void TryRun()
        {
            if (!EditorPrefs.GetBool(PrefKey, false))
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                return;

            EditorPrefs.SetBool(PrefKey, false);
            try
            {
                QhysicsUiMenu.PlaceQhysicsUi();
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveOpenScenes();
                Debug.Log("PlaceUiPlayableOnce: QHYSICS UI refreshed (menu/onboarding/settings). SaveOpenScenes=" + saved + " path=" + scene.path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("PlaceUiPlayableOnce failed: " + ex);
                EditorPrefs.SetBool(PrefKey, true);
            }
        }
    }
}