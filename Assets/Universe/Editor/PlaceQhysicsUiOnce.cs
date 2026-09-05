using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealityEngine.UI;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// One-shot: after domain reload, Place QHYSICS UI in Edit mode then disarm.
    /// </summary>
    [InitializeOnLoad]
    static class PlaceQhysicsUiOnce
    {
        const string PrefKey = "QHYSICS.PlaceQhysicsUiOnce.Pending1";

        static PlaceQhysicsUiOnce()
        {
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
                QhysicsUiMenu.PlaceQhysicsUi();
                Debug.Log("PlaceQhysicsUiOnce: placed QHYSICS UI v0.1 in Edit mode. Pref disarmed.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("PlaceQhysicsUiOnce failed: " + ex);
                EditorPrefs.SetBool(PrefKey, true);
            }
        }

        [MenuItem("Reality Engine/Place QHYSICS UI Once (arm)")]
        static void Arm()
        {
            EditorPrefs.SetBool(PrefKey, true);
            Debug.Log("PlaceQhysicsUiOnce armed — will run on next delayCall/domain reload (Edit mode only).");
            EditorApplication.delayCall += TryPlace;
        }
    }
}
