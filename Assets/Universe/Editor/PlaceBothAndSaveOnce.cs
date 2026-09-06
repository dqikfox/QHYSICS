using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Visualization;
using RealityEngine.XR;
using System.IO;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// Throne fix: reopen Faraday, Place Giza + UI, Fix Player Spawn, persist meshes/mats, Save.
    /// Edit-mode hierarchy must show LabLandscape without Play; XR Origin on plaza Y~5.75.
    /// </summary>
    [InitializeOnLoad]
    static class PlaceBothAndSaveOnce /* CompileKick_ThroneEmptyPlasticSpawn_v2 */
    {
        const string PrefKey = "QHYSICS.PlaceBothAndSaveOnce.Pending2_ThroneEmptyPlasticSpawn";
        const string FaradayPath = "Assets/Scenes/Faraday.unity";
        const string DoneMarker = "Library/QHYSICS_ThronePersistDone.txt";

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
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            EditorPrefs.SetBool(PrefKey, false);
            try
            {
                var open = EditorSceneManager.OpenScene(FaradayPath, OpenSceneMode.Single);
                if (!open.IsValid())
                    throw new System.Exception("Failed to open " + FaradayPath);

                LabLandscapeMenu.PlaceGizaComplex();
                PersistLabAssets();
                QhysicsUiMenu.PlaceQhysicsUi();
                LabPlayerSpawnMenu.FixPlayerSpawn();
                PersistLabAssets();
                FrameLab();

                EditorSceneManager.MarkSceneDirty(open);
                bool saved = EditorSceneManager.SaveOpenScenes();

                Transform origin = null;
                GameObject originGo = GameObject.Find(LabPlayerSpawn.OriginName);
                if (originGo != null)
                    origin = originGo.transform;
                GameObject lab = GameObject.Find(LabLandscapeApplier.RootName);
                string spawn = origin != null
                    ? origin.position.ToString("F2") + " parent=" + (origin.parent != null ? origin.parent.name : "null")
                    : "MISSING";
                int kids = lab != null ? lab.transform.childCount : -1;
                string summary =
                    "PlaceBothAndSaveOnce Throne: Place Giza + UI + Fix Spawn. SaveOpenScenes=" + saved +
                    " LabLandscape kids=" + kids +
                    " XR Origin=" + spawn +
                    " path=" + open.path;
                Debug.Log(summary);
                try
                {
                    File.WriteAllText(DoneMarker, summary + "\n" + System.DateTime.UtcNow.ToString("o") + "\n");
                }
                catch (System.Exception writeEx)
                {
                    Debug.LogWarning("PlaceBothAndSaveOnce: marker write failed: " + writeEx.Message);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("PlaceBothAndSaveOnce failed: " + ex);
                EditorPrefs.SetBool(PrefKey, true);
                try
                {
                    File.WriteAllText(DoneMarker, "FAIL: " + ex + "\n");
                }
                catch { /* ignore */ }
            }
        }

        static void PersistLabAssets()
        {
            GameObject lab = GameObject.Find(LabLandscapeApplier.RootName);
            if (lab == null)
                return;
            // Strip DontSave leftovers so Scene View keeps meshes/mats after domain reload.
            MeshFilter[] mfs = lab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
            {
                MeshFilter mf = mfs[i];
                if (mf == null || mf.sharedMesh == null)
                    continue;
                if ((mf.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                    mf.sharedMesh.hideFlags = HideFlags.None;
            }
            MeshRenderer[] mrs = lab.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < mrs.Length; i++)
            {
                MeshRenderer mr = mrs[i];
                if (mr == null)
                    continue;
                if (!mr.enabled)
                    mr.enabled = true;
                if (!mr.gameObject.activeSelf)
                    mr.gameObject.SetActive(true);
                Material mat = mr.sharedMaterial;
                if (mat == null)
                    continue;
                if ((mat.hideFlags & HideFlags.DontSave) != 0)
                    mat.hideFlags = HideFlags.None;
                Texture main = null;
                if (mat.HasProperty("_BaseMap"))
                    main = mat.GetTexture("_BaseMap");
                if (main == null && mat.HasProperty("_MainTex"))
                    main = mat.GetTexture("_MainTex");
                if (main != null && (main.hideFlags & HideFlags.DontSave) != 0)
                    main.hideFlags = HideFlags.None;
            }
            if (!lab.activeSelf)
                lab.SetActive(true);
            GizaBuild.StripBlackOrphans(lab.transform);
            GizaBuild.ReapplyMaterials(lab.transform);
        }

        static void FrameLab()
        {
            GameObject root = GameObject.Find(LabLandscapeApplier.RootName);
            if (root == null)
                return;
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        [MenuItem("Reality Engine/Place Both And Save Once (arm)")]
        static void Arm()
        {
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("PlaceBothAndSaveOnce armed — will Place Giza + UI + Fix Spawn and SaveOpenScenes on next delayCall (Edit mode only).");
            EditorApplication.delayCall += TryRun;
        }
    }
}
