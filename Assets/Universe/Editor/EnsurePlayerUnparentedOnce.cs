#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Player;
using RealityEngine.XR;
using System.IO;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// One-shot: unparent XR Origin from any Giza/pyramid/MountainScene parent, strip monument
    /// children off the player, disable leftover BuildingBlock/OVR eye cameras, Save Faraday.
    /// </summary>
    [InitializeOnLoad]
    static class EnsurePlayerUnparentedOnce
    {
        const string PrefKey = "QHYSICS.EnsurePlayerUnparentedOnce.Pending_v1_PyramidMoveBug";
        const string FaradayPath = "Assets/Scenes/Faraday.unity";
        const string DoneMarker = "Library/QHYSICS_EnsurePlayerUnparentedDone.txt";

        static EnsurePlayerUnparentedOnce()
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

                GameObject originGo = GameObject.Find(LabPlayerSpawn.OriginName);
                Transform origin = originGo != null ? originGo.transform : null;
                if (origin != null)
                {
                    LabPlayerSpawnCompat.EnsurePlayerNotUnderMonument(origin);
                    LabPlayerSpawnCompat.StripMonumentChildren(origin);
                    LabPlayerSpawnCompat.EnsureDesktopViewAuthority(origin, null);
                    EditorUtility.SetDirty(origin.gameObject);
                }

                // Persist: disable BuildingBlock Camera Rig in the scene so Edit-mode hierarchy is clean.
                GameObject bb = GameObject.Find("[BuildingBlock] Camera Rig");
                if (bb != null && bb.activeSelf)
                {
                    bb.SetActive(false);
                    EditorUtility.SetDirty(bb);
                }

                // Strip MainCamera tag from OVR eye anchors if still present.
                StripRivalMainCameraTags();

                EditorSceneManager.MarkSceneDirty(open);
                bool saved = EditorSceneManager.SaveOpenScenes();

                string summary =
                    "EnsurePlayerUnparentedOnce: origin=" + (origin != null ? origin.position.ToString("F2") : "MISSING") +
                    " parent=" + (origin != null && origin.parent != null ? origin.parent.name : "null") +
                    " BuildingBlockDisabled=" + (bb != null && !bb.activeSelf) +
                    " Save=" + saved;
                Debug.Log(summary);
                try { File.WriteAllText(DoneMarker, summary + "\n" + System.DateTime.UtcNow.ToString("o") + "\n"); }
                catch { /* ignore */ }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("EnsurePlayerUnparentedOnce failed: " + ex);
                EditorPrefs.SetBool(PrefKey, true);
                try { File.WriteAllText(DoneMarker, "FAIL: " + ex + "\n"); } catch { /* ignore */ }
            }
        }

        static void StripRivalMainCameraTags()
        {
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                if (c == null)
                    continue;
                string n = c.gameObject.name ?? "";
                bool rival = n.IndexOf("CenterEye", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("LeftEye", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("RightEye", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!rival)
                    continue;
                if (c.CompareTag("MainCamera"))
                {
                    c.tag = "Untagged";
                    EditorUtility.SetDirty(c.gameObject);
                }
                c.enabled = false;
                AudioListener al = c.GetComponent<AudioListener>();
                if (al != null)
                    al.enabled = false;
            }
        }

        [MenuItem("Reality Engine/Ensure Player Unparented Once (arm)")]
        static void Arm()
        {
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("EnsurePlayerUnparentedOnce armed — will unparent XR Origin from monuments, disable BuildingBlock cameras, Save Faraday.");
            EditorApplication.delayCall += TryRun;
        }
    }
}
#endif
