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
            Debug.Log("Applied Reality Engine Giza plateau with Oasis sand skirts (meadow/terrain hidden, MountainScene left on). Ctrl+R, Play Faraday.unity. Sand around Khufu base; Tura courses still on the pyramid.");
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
            Place(GizaComplex.Spawn.All, "FULL Giza complex force-rebuild: pyramids (incl. G1-d), temples, causeways, Khufu Trial Passages, West/East/Central Field mastabas, Gisr el-Mudir, Khentkawes I, workers village + Wall of the Crow, Osiris Shaft, dunes, Nile harbor, desert dust. Reality Engine / Place Giza Complex.");
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
            Debug.Log("Reality Engine: placed " + detail + " Roots: KhufuWestField, GisrElMudir, KhufuTrialPassages, GizaCentralField, Khentkawes, GizaWorkersVillage, OsirisShaft, GizaDesertDust, GizaNileHarbor, LabLandscape.");
        }

        static void Mark(LabLandscapeApplier applier)
        {
            if (applier != null && applier.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);
        }
    }
}
