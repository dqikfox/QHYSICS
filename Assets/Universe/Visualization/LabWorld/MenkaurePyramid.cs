using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Menkaure (3rd pyramid) undamaged 1:1. Lower 16 courses red Aswan granite, upper Tura limestone.
    /// Offset from Khufu centre: 563 m west, 743 m south (approx. WGS84, Giza lat 30°).
    /// Three schematic queens' pyramids on the south side.
    /// </summary>
    public static class MenkaurePyramid
    {
        public const float BaseMeters = 105.5f;
        public const float HeightMeters = 65.5f;
        public const float SlopeDeg = 51f + 20f / 60f + 25f / 3600f;
        public const float EntranceHeightM = 4.2f;
        public const float PassageWidthM = 1.05f;
        public const float PassageHeightM = 1.20f;
        public const float PassageAngleDeg = 26.2f;
        public const float GraniteCourses = 16f;
        public const float GraniteHeightM = 12.8f;
        public const float PyramidionM = 0.85f;
        public const float QueenBaseM = 28f;
        public const float QueenHeightM = 18f;
        public const string RootName = "Menkaure";

        public const string Honesty =
            GizaComplex.HonestyPrefix + "\n" +
            "Menkaure — third pyramid. Lower 16 courses red Aswan granite casing, upper Tura limestone. Intact pyramidion (reconstructed).\n" +
            "Base 105.5 m. Original height 65.5 m. Slope 51° 20' 25\".\n" +
            "Offset from Khufu centre (approx. WGS84, lat 30°): 563 m west, 743 m south.\n" +
            "North-face original entrance ~4.2 m up, on the centreline. Descending to antechamber + granite burial chamber (barrel vault, lidless coffer).\n" +
            "Three schematic queens' pyramids on the south side (G3a-c) with honesty labels: G3-a pink granite sarcophagus; G3-b young woman's bones + sarcophagus (bones label-only); G3-c unfinished. Basalt burial coffer = RECONSTRUCTION / LOST FIND (1838). East chapels walkable. Not photogrammetry.";

        public static GameObject Build(Transform parent, Vector3 worldBaseCenter, Quaternion worldRot, bool comfortScale)
        {
            GameObject root = GizaBuild.Root(RootName, parent, worldBaseCenter, worldRot);
            float scale = comfortScale ? 1.6f : 1f;
            float pw = PassageWidthM * scale;
            float ph = PassageHeightM * scale;
            float half = BaseMeters * 0.5f;
            float zFace = GizaBuild.FaceZ(half, HeightMeters, EntranceHeightM);
            float holeW = PassageWidthM * 1.55f;
            float holeH = PassageHeightM * 1.65f;
            float tCap = 1f - Mathf.Clamp01(PyramidionM / HeightMeters);
            float tGran = Mathf.Clamp01(GraniteHeightM / HeightMeters);

            Material tura = GizaBuild.TuraCasing();
            Material aswan = GizaBuild.Aswan();
            Material lime = GizaBuild.InteriorLime();
            Material granite = GizaBuild.Granite();
            Material pavement = GizaBuild.Pavement();
            Material gold = GizaBuild.Electrum();
            Material emit = GizaBuild.Emit();

            GizaBuild.CasingBand(root.transform, "Menkaure_CasingGranite", BaseMeters, HeightMeters, aswan,
                0f, tGran, true, 0f, EntranceHeightM, holeW, holeH, 4, 8);
            GizaBuild.CasingBand(root.transform, "Menkaure_Casing", BaseMeters, HeightMeters, tura,
                tGran, tCap, false, 0f, 0f, 0f, 0f, 3, 4);
            GizaBuild.Pyramidion(root.transform, "Menkaure_Pyramidion", BaseMeters, HeightMeters, PyramidionM, gold);
            GizaBuild.PavementRing(root.transform, "Menkaure_Pavement", BaseMeters, 4f, pavement);
            GizaBuild.EntranceLedge(root.transform, "Menkaure_Ledge", 0f, EntranceHeightM, zFace, pw, ph, pavement);
            BuildInterior(root.transform, lime, granite, emit, pw, ph);
            BuildQueens(root.transform, tura, gold);
            GizaBuild.HonestyPlate(root.transform, "Menkaure_Honesty", Honesty, BaseMeters);
            return root;
        }

        static void BuildInterior(Transform parent, Material lime, Material granite, Material emit, float pw, float ph)
        {
            float half = BaseMeters * 0.5f;
            Vector3 descDir = GizaBuild.PassageDir(PassageAngleDeg, true);
            float zFace = GizaBuild.FaceZ(half, HeightMeters, EntranceHeightM);
            Vector3 entrance = new Vector3(0f, EntranceHeightM, zFace);
            Vector3 mouth = entrance - descDir * 1.6f;
            Vector3 descEnd = entrance + descDir * 28f;
            Vector3 anteDoor = new Vector3(0f, descEnd.y, 10.5f);

            var passages = new LabMeshBuilder(2800, 8400);
            Color stone = new Color(0.70f, 0.66f, 0.58f, 1f);
            passages.AddTunnel(mouth, descEnd, pw, ph, stone, 10);
            passages.AddTunnel(descEnd, anteDoor, pw, ph, stone, 6);
            GizaBuild.SpawnMesh(parent, "Menkaure_Passages", passages.Build("Menkaure_Passages"), lime, true);

            var emitMesh = new LabMeshBuilder(96, 192);
            GizaBuild.CeilingStrip(emitMesh, mouth, anteDoor, pw * 0.15f, ph);
            GizaBuild.SpawnMesh(parent, "Menkaure_Emit", emitMesh.Build("Menkaure_Emit"), emit, false);

            Vector3 floor = anteDoor - Vector3.up * (ph * 0.5f);
            float anteL = 7.2f;
            float anteW = 3.6f;
            float anteH = 3.8f;
            Vector3 anteC = new Vector3(0f, floor.y + anteH * 0.5f, anteDoor.z - anteL * 0.5f);
            var ante = new LabMeshBuilder(48, 72);
            Color c = new Color(0.68f, 0.64f, 0.56f, 1f);
            ante.AddRoom(anteC, new Vector3(anteW, anteH, anteL), c, true, true, false, false);
            GizaBuild.SpawnMesh(parent, "Menkaure_Antechamber", ante.Build("Menkaure_Antechamber"), lime, true);

            Vector3 buryDoor = new Vector3(0f, anteC.y, anteC.z - anteL * 0.5f);
            Vector3 buryFloor = new Vector3(0f, floor.y - 0.4f, buryDoor.z - 3.5f);
            float bEW = 2.6f;
            float bNS = 6.6f;
            float bWall = 2.4f;
            float bPeak = 3.5f;
            Vector3 buryC = buryFloor + Vector3.up * (bWall * 0.5f);
            var bury = new LabMeshBuilder(96, 180);
            Color g = new Color(0.32f, 0.24f, 0.22f, 1f);
            bury.AddRoom(buryC, new Vector3(bEW, bWall, bNS), g, false, true, false, false);
            bury.AddBarrelVault(buryFloor, bEW, bNS, bWall, bPeak, g, 6);
            GizaBuild.SpawnMesh(parent, "Menkaure_Burial", bury.Build("Menkaure_Burial"), granite, true);

            var conn = new LabMeshBuilder(64, 96);
            Vector3 aEnd = new Vector3(0f, floor.y + ph * 0.5f, anteC.z - anteL * 0.5f);
            Vector3 bStart = new Vector3(0f, buryFloor.y + ph * 0.5f, buryC.z + bNS * 0.5f);
            conn.AddTunnel(aEnd, bStart, pw, ph, g, 4);
            GizaBuild.SpawnMesh(parent, "Menkaure_BurialDoor", conn.Build("Menkaure_BurialDoor"), granite, true);

            // Basalt sarcophagus — LOST FIND (shipwreck 1838). Schematic RECONSTRUCTION only.
            Material basalt = LabWorldMeshes.MakeLit("RELab_Basalt",
                new Color(0.16f, 0.16f, 0.18f, 1f), 0.08f, 0.18f, false);
            var sarc = new LabMeshBuilder(32, 48);
            Vector3 sarcC = new Vector3(0f, buryFloor.y + 0.45f, buryC.z - 1.6f);
            sarc.AddBox(sarcC, new Vector3(0.95f, 0.95f, 2.25f), g);
            // Palace-facade panel stubs (attested decoration type on lost coffer).
            sarc.AddBox(sarcC + new Vector3(0.5f, 0f, 0f), new Vector3(0.06f, 0.7f, 1.9f), g);
            sarc.AddBox(sarcC + new Vector3(-0.5f, 0f, 0f), new Vector3(0.06f, 0.7f, 1.9f), g);
            sarc.AddRoom(sarcC + Vector3.up * 0.08f, new Vector3(0.62f, 0.58f, 1.85f), g, false, false, false, false);
            GizaBuild.SpawnMesh(parent, "Menkaure_Sarcophagus", sarc.Build("Menkaure_Sarcophagus"), basalt, true);
            const string sarcHonesty =
                "RECONSTRUCTION / LOST FIND.\n" +
                "Menkaure basalt sarcophagus (palace-facade decoration). Found by Vyse; lost at sea 1838 en route to Britain.\n" +
                "This mesh is a labeled reconstruction of the lost find — NOT found here today. Chamber otherwise empty of treasure.";
            GizaBuild.HonestyPlate(parent, "Menkaure_Sarcophagus_Honesty", sarcHonesty, 8f);
            Transform sP = parent.Find("Menkaure_Sarcophagus_Honesty");
            if (sP != null)
            {
                sP.localPosition = new Vector3(sarcC.x + 1.5f, buryFloor.y + 1.5f, sarcC.z);
                sP.localRotation = Quaternion.Euler(0f, 90f, 0f);
                sP.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
        }

        static void BuildQueens(Transform parent, Material tura, Material gold)
        {
            float south = -BaseMeters * 0.5f - 8f - QueenBaseM * 0.5f;
            float[] xs = { 32f, 0f, -32f };
            string[] names = { "G3a", "G3b", "G3c" };
            Material pav = GizaBuild.Pavement();
            Material lime = GizaBuild.InteriorLime();
            Material granite = GizaBuild.Granite();
            for (int i = 0; i < 3; i++)
            {
                var q = new GameObject(names[i]);
                q.transform.SetParent(parent, false);
                q.transform.localPosition = new Vector3(xs[i], 0f, south);
                q.transform.localRotation = Quaternion.identity;
                GizaBuild.Casing(q.transform, names[i] + "_Casing", QueenBaseM, QueenHeightM, tura, false, 0f, 0f, 0f, 0f, 0.45f);
                GizaBuild.Pyramidion(q.transform, names[i] + "_Pyramidion", QueenBaseM, QueenHeightM, 0.45f, gold);
                GizaBuild.PavementRing(q.transform, names[i] + "_Pavement", QueenBaseM, 3f, pav);
                // East chapel shells (Lehner schematic) - marker names[i]_Chapel for force-rebuild.
                GizaPrecinct.BuildQueenEastChapel(q.transform, names[i], QueenBaseM, lime, tura, pav);
                BuildQueenContents(q.transform, names[i], pav, granite);
            }
        }

        static void BuildQueenContents(Transform queenRoot, string name, Material pav, Material granite)
        {
            // Honesty-only interiors for G3a-c — no invented treasure.
            string body;
            if (name == "G3a")
            {
                body =
                    "ATTESTED type / parallel.\n" +
                    "G3-a (Menkaure queen): pink granite sarcophagus attested (schematic). Chapel east of pyramid.\n" +
                    "Not a full walkable burial interior in this build — honesty label at chapel.";
                var sarc = new LabMeshBuilder(16, 24);
                Color pg = new Color(0.62f, 0.42f, 0.40f, 1f);
                Material pink = LabWorldMeshes.MakeLit("RELab_PinkGranite", pg, 0.06f, 0.18f, false);
                sarc.AddBox(new Vector3(QueenBaseM * 0.5f + 4.5f, 0.55f, 0f), new Vector3(1.8f, 0.9f, 0.85f), Color.white);
                GizaBuild.SpawnMesh(queenRoot, name + "_Sarcophagus", sarc.Build(name + "_Sarcophagus"), pink, true);
            }
            else if (name == "G3b")
            {
                body =
                    "ATTESTED find context.\n" +
                    "G3-b: sarcophagus + bones of a young woman (attested excavation). Schematic empty coffer here.\n" +
                    "Bones not modeled as props (human remains — label only). No invented jewelry cache.";
                var sarc = new LabMeshBuilder(16, 24);
                sarc.AddBox(new Vector3(QueenBaseM * 0.5f + 4.5f, 0.5f, 0f), new Vector3(1.7f, 0.85f, 0.8f), Color.white);
                GizaBuild.SpawnMesh(queenRoot, name + "_Sarcophagus", sarc.Build(name + "_Sarcophagus"), granite, true);
            }
            else
            {
                body =
                    "ATTESTED unfinished.\n" +
                    "G3-c: unfinished queen pyramid. No complete burial assemblage claimed here.\n" +
                    "Honesty label only — do not invent contents.";
            }
            GizaBuild.HonestyPlate(queenRoot, name + "_ContentsHonesty", body, 10f);
            Transform plate = queenRoot.Find(name + "_ContentsHonesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(QueenBaseM * 0.5f + 7f, 1.5f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
                plate.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            }
        }
    }
}
