using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.Visualization;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// One-shot: after domain reload, Place Giza Complex in Edit mode then disarm.
    /// </summary>
    [InitializeOnLoad]
    static class PlaceGizaOnce
    {
        const string PrefKey = "QHYSICS.PlaceGizaOnce.Pending3";

        static PlaceGizaOnce()
        {
            // Arm on first import of this file; subsequent domain reloads honor the pref.
            if (!EditorPrefs.HasKey(PrefKey))
                EditorPrefs.SetBool(PrefKey, true);
            EditorApplication.delayCall += TryPlace;
        }

        static void TryPlace()
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
                FrameGiza();
                Debug.Log("PlaceGizaOnce: placed Giza Complex in Edit mode (warm URP Lit sand/limestone). Pref disarmed.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("PlaceGizaOnce failed: " + ex);
                EditorPrefs.SetBool(PrefKey, true);
            }
        }

        static void FrameGiza()
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

        [MenuItem("Reality Engine/Place Giza Once (arm)")]
        static void Arm()
        {
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("PlaceGizaOnce armed — will run on next delayCall/domain reload (Edit mode only).");
            EditorApplication.delayCall += TryPlace;
        }
    }
}


