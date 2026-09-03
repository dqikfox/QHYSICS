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
        const string KhafrePath = "Reality Engine/Place Khafre";
        const string MenkaurePath = "Reality Engine/Place Menkaure";
        const string ComplexPath = "Reality Engine/Place Giza Complex";

        [MenuItem(LandscapePath)]
        public static void ApplyLabLandscape()
        {
            LabLandscapeApplier applier = LabLandscapeApplier.EnsureApplied();
            applier.ApplyNow(true);
            Mark(applier);
            Selection.activeGameObject = applier != null ? applier.gameObject : null;
            Debug.Log("Applied Reality Engine Giza plateau (meadow/terrain hidden, MountainScene left on). Play Faraday.unity or use Place Giza Complex. Khufu north face ~17 m; Khafre 11.54 m + ground; Menkaure 4.2 m.");
        }

        [MenuItem(LandscapePath, true)]
        public static bool ApplyLabLandscapeValidate()
        {
            return true;
        }

        [MenuItem(KhufuPath)]
        public static void PlaceKhufu()
        {
            Place(GizaComplex.Spawn.Khufu, "Khufu. Teleport to the north face; original entrance ~17 m up, 7.29 m east of centreline.");
        }

        [MenuItem(KhufuPath, true)]
        public static bool PlaceKhufuValidate()
        {
            return true;
        }

        [MenuItem(KhafrePath)]
        public static void PlaceKhafre()
        {
            Place(GizaComplex.Spawn.Khafre, "Khafre. North face two-entrance system: 11.54 m up and ground-level, 12 m east of centreline. Bedrock +10 m.");
        }

        [MenuItem(KhafrePath, true)]
        public static bool PlaceKhafreValidate()
        {
            return true;
        }

        [MenuItem(MenkaurePath)]
        public static void PlaceMenkaure()
        {
            Place(GizaComplex.Spawn.Menkaure, "Menkaure. North face original entrance ~4.2 m up on the centreline. Lower 16 courses Aswan granite.");
        }

        [MenuItem(MenkaurePath, true)]
        public static bool PlaceMenkaureValidate()
        {
            return true;
        }

        [MenuItem(ComplexPath)]
        public static void PlaceGizaComplex()
        {
            Place(GizaComplex.Spawn.All, "full Giza complex (Khufu + G1a-c queens, mortuary temples, causeways, boat pits, Khafre valley/Sphinx temples, Menkaure precinct). Dedupe by child names if already spawned.");
        }

        [MenuItem(ComplexPath, true)]
        public static bool PlaceGizaComplexValidate()
        {
            return true;
        }

        static void Place(GizaComplex.Spawn which, string detail)
        {
            LabLandscapeApplier applier = LabLandscapeApplier.EnsureApplied();
            if (which == GizaComplex.Spawn.All)
                applier.ApplyNow(true);
            else
                applier.PlaceMonuments(which);
            Mark(applier);
            Selection.activeGameObject = applier != null ? applier.gameObject : null;
            Debug.Log("Reality Engine: placed " + detail);
        }

        static void Mark(LabLandscapeApplier applier)
        {
            if (applier != null && applier.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);
        }
    }
}
