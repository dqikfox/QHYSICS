using UnityEditor;
using UnityEngine;

namespace RealityEngine.EditorTools
{
    /// <summary>
    /// Places / ensures a breadboard Resistor dispenser beside existing circuit dispensers.
    /// Does not require saving Faraday.unity — runtime AutoEnsure also covers Play Mode.
    /// </summary>
    public static class EnsureResistorDispenserOnce
    {
        const string MenuPath = "Reality Engine/Ensure Breadboard Resistor Dispenser";

        [MenuItem(MenuPath)]
        public static void Ensure()
        {
            EnsureBreadboardResistorDispenser.EnsureResistorTagInTagManager();
            Dispenser d = EnsureBreadboardResistorDispenser.Ensure();
            if (d != null)
            {
                Selection.activeGameObject = d.gameObject;
                EditorGUIUtility.PingObject(d);
                if (d.gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(d.gameObject.scene);
                Debug.Log("Breadboard Resistor dispenser ensured: " + d.gameObject.name, d);
            }
            else
            {
                Debug.LogWarning("Ensure Breadboard Resistor Dispenser: failed (no CircuitLab / donor dispenser?).");
            }
        }

        [MenuItem(MenuPath, true)]
        public static bool EnsureValidate() => true;
    }
}
