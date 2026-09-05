using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// One-shot: Place Giza Complex + Place QHYSICS UI in Edit mode, then SaveOpenScenes
    /// so LabLandscape + UI persist in Faraday.unity across sessions.
    /// </summary>
    [InitializeOnLoad]
    static class PlaceBothAndSaveOnce /* CompileKick */
    {
        const string PrefKey = "QHYSICS.PlaceBothAndSaveOnce.Pending1_PersistFaraday";

        static PlaceBothAndSaveOnce()
        {
            if (!EditorPrefs.HasKey(PrefKey))
                EditorPrefs.SetBool(PrefKey, true);
            EditorApplication.delayCall += TryRun;
        }

        static void TryRun()
        {
            if (!EditorPrefs.GetBool(PrefKey, false))
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (Application.isPlaying)
                return;

            EditorPrefs.SetBool(PrefKey, false);
            try
            {
                LabLandscapeMenu.PlaceGizaComplex();
                QhysicsUiMenu.PlaceQhysicsUi();

                var scene = EditorSceneManager.GetActiveScene();
                if (scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(scene);

                bool saved = EditorSceneManager.SaveOpenScenes();
                Debug.Log(
                    "PlaceBothAndSaveOnce: Place Giza Complex + Place QHYSICS UI done. " +
                    "SaveOpenScenes=" + saved + " path=" + scene.path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("PlaceBothAndSaveOnce failed: " + ex);
                EditorPrefs.SetBool(PrefKey, true);
            }
        }

        [MenuItem("Reality Engine/Place Both And Save Once (arm)")]
        static void Arm()
        {
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("PlaceBothAndSaveOnce armed — will Place Giza + UI and SaveOpenScenes on next delayCall (Edit mode only).");
            EditorApplication.delayCall += TryRun;
        }
    }
}
