#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Player;
using RealityEngine.UI;
using RealityEngine.XR;
using System.IO;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// One-shot after compile: Place Desktop Player + ensure QHYSICS UI, Save Faraday.
    /// </summary>
    [InitializeOnLoad]
    static class PlaceDesktopPlayableOnce
    {
        const string PrefKey = "QHYSICS.PlaceDesktopPlayableOnce.Pending_v3_AfterRefresh";
        const string FaradayPath = "Assets/Scenes/Faraday.unity";
        const string DoneMarker = "Library/QHYSICS_PlaceDesktopPlayableDone.txt";

        static PlaceDesktopPlayableOnce()
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
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            EditorPrefs.SetBool(PrefKey, false);
            try
            {
                var open = EditorSceneManager.GetActiveScene();
                if (!open.IsValid() || open.path != FaradayPath)
                    open = EditorSceneManager.OpenScene(FaradayPath, OpenSceneMode.Single);
                if (!open.IsValid())
                    throw new System.Exception("Failed to open " + FaradayPath);

                LabPlayerSpawn.EnsureApplied();
                var boot = QhysicsDesktopBootstrap.Ensure();
                QhysicsUiBootstrap.EnsurePlaced();

                if (boot != null)
                    EditorUtility.SetDirty(boot.gameObject);

                EditorSceneManager.MarkSceneDirty(open);
                bool saved = EditorSceneManager.SaveOpenScenes();

                string host = GameObject.Find(QhysicsDesktopBootstrap.HostName) != null ? "OK" : "MISSING";
                string ui = GameObject.Find(QhysicsUiBootstrap.RootName) != null ? "OK" : "MISSING";
                string summary =
                    "PlaceDesktopPlayableOnce: Desktop=" + host + " UI=" + ui +
                    " SaveOpenScenes=" + saved + " path=" + open.path;
                Debug.Log(summary);
                try { File.WriteAllText(DoneMarker, summary + "\n" + System.DateTime.UtcNow.ToString("o") + "\n"); }
                catch { /* ignore */ }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("PlaceDesktopPlayableOnce failed: " + ex);
                EditorPrefs.SetBool(PrefKey, true);
                try { File.WriteAllText(DoneMarker, "FAIL: " + ex + "\n"); } catch { /* ignore */ }
            }
        }

        [MenuItem("Reality Engine/Place Desktop Playable Once (arm)")]
        static void Arm()
        {
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("PlaceDesktopPlayableOnce armed Ã¢â‚¬â€ will Place Desktop + UI and Save on next delayCall (Edit mode).");
            EditorApplication.delayCall += TryRun;
        }
    }
}
#endif

