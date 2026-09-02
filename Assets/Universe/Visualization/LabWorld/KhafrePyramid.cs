using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Khafre (2nd pyramid) undamaged 1:1. Tura casing ON. Two-entrance system, burial in bedrock.
    /// Offset from Khufu centre: 323 m west, 342 m south (approx. WGS84, Giza lat 30°). Bedrock +10 m.
    /// </summary>
    public static class KhafrePyramid
    {
        public const float BaseMeters = 215.25f;
        public const float HeightMeters = 143.5f;
        public const float SlopeDeg = 53f + 10f / 60f;
        public const float UpperEntranceY = 11.54f;
        public const float EntranceEastOffsetM = 12f;
        public const float PassageWidthM = 1.05f;
        public const float PassageHeightM = 1.20f;
        public const float PassageAngleDeg = 26.5f;
        public const float BurialEW = 14.15f;
        public const float BurialNS = 5.05f;
        public const float BurialWallH = 4.4f;
        public const float BurialPeakH = 6.8f;
        public const float PyramidionM = 1.05f;
        public const string RootName = "Khafre";

        public const string Honesty =
            GizaComplex.HonestyPrefix + "\n" +
            "Khafre — second pyramid. Tura limestone casing ON all four faces. Intact electrum pyramidion (reconstructed).\n" +
            "Base 215.25 m. Original height 143.5 m. Slope 53° 10'. Bedrock terrace +10 m vs Khufu (why it looks taller).\n" +
            "Offset from Khufu centre (approx. WGS84, lat 30°): 323 m west, 342 m south.\n" +
            "Two-entrance system on the north face: upper 11.54 m up, and ground-level, both 12 m east of centreline.\n" +
            "Descending to burial chamber in bedrock (14.15 × 5.05 m, gabled limestone, granite sarcophagus). Undecorated walls.";

        public static GameObject Build(Transform parent, Vector3 worldBaseCenter, Quaternion worldRot, bool comfortScale)
        {
            GameObject root = GizaBuild.Root(RootName, parent, worldBaseCenter, worldRot);
            float scale = comfortScale ? 1.6f : 1f;
            float pw = PassageWidthM * scale;
            float ph = PassageHeightM * scale;
            float half = BaseMeters * 0.5f;
            float zUpper = GizaBuild.FaceZ(half, HeightMeters, UpperEntranceY);
            float zGround = half;
            float holeW = PassageWidthM * 1.55f;
            float holeH = PassageHeightM * 1.65f;

            Material tura = GizaBuild.TuraCasing();
            Material lime = GizaBuild.InteriorLime();
            Material granite = GizaBuild.Granite();
            Material pavement = GizaBuild.Pavement();
            Material gold = GizaBuild.Electrum();
            Material emit = GizaBuild.Emit();
            Material rock = GizaBuild.Bedrock();

            BuildBedrockTerrace(root.transform, rock);
            GizaBuild.Casing(root.transform, "Khafre_Casing", BaseMeters, HeightMeters, tura,
                true, EntranceEastOffsetM, UpperEntranceY, holeW, holeH, PyramidionM,
                EntranceEastOffsetM, ph * 0.55f, holeW, holeH);
            GizaBuild.Pyramidion(root.transform, "Khafre_Pyramidion", BaseMeters, HeightMeters, PyramidionM, gold);
            GizaBuild.PavementRing(root.transform, "Khafre_Pavement", BaseMeters, 5f, pavement);
            GizaBuild.EntranceLedge(root.transform, "Khafre_LedgeUpper", EntranceEastOffsetM, UpperEntranceY, zUpper, pw, ph, pavement);
            GizaBuild.EntranceLedge(root.transform, "Khafre_LedgeGround", EntranceEastOffsetM, ph * 0.55f, zGround, pw, ph, pavement);
            BuildInterior(root.transform, lime, granite, emit, pw, ph);
            GizaBuild.HonestyPlate(root.transform, "Khafre_Honesty", Honesty, BaseMeters);
            return root;
        }

        static void BuildBedrockTerrace(Transform parent, Material mat)
        {
            var b = new LabMeshBuilder(8, 12);
            float w = BaseMeters + 16f;
            b.AddBox(new Vector3(0f, -GizaComplex.KhafreBedrockM * 0.5f, 0f),
                new Vector3(w, GizaComplex.KhafreBedrockM, w), Color.white);
            GizaBuild.SpawnMesh(parent, "Khafre_Bedrock", b.Build("Khafre_Bedrock"), mat, true);
        }

        static void BuildInterior(Transform parent, Material lime, Material granite, Material emit, float pw, float ph)
        {
            float half = BaseMeters * 0.5f;
            float x = EntranceEastOffsetM;
            Vector3 descDir = GizaBuild.PassageDir(PassageAngleDeg, true);
            Vector3 ascDir = GizaBuild.PassageDir(PassageAngleDeg, false);
            float zUpper = GizaBuild.FaceZ(half, HeightMeters, UpperEntranceY);
            Vector3 upper = new Vector3(x, UpperEntranceY, zUpper);
            Vector3 upperMouth = upper - descDir * 1.6f;
            float horizY = -1.6f;
            float sinA = Mathf.Max(0.2f, Mathf.Abs(descDir.y));
            float tUpper = (upper.y - horizY) / sinA;
            Vector3 join = upper + descDir * tUpper;

            Vector3 lowerMouth = new Vector3(x, ph * 0.55f, half + 1.5f);
            Vector3 lowerBottom = lowerMouth + descDir * 22f;
            Vector3 lowerSouth = new Vector3(x, lowerBottom.y, join.z + 4f);
            Vector3 lowerRise = join;

            Vector3 burialDoor = new Vector3(x, join.y, BurialNS * 0.5f + 0.4f);

            var passages = new LabMeshBuilder(5000, 15000);
            Color stone = new Color(0.70f, 0.66f, 0.58f, 1f);
            passages.AddTunnel(upperMouth, join, pw, ph, stone, 10);
            passages.AddTunnel(lowerMouth, lowerBottom, pw, ph, stone, 8);
            passages.AddTunnel(lowerBottom, lowerSouth, pw, ph, stone, 6);
            passages.AddTunnel(lowerSouth, lowerRise, pw, ph, stone, 6);
            passages.AddTunnel(join, burialDoor, pw, ph, stone, 8);
            GizaBuild.SpawnMesh(parent, "Khafre_Passages", passages.Build("Khafre_Passages"), lime, true);

            var emitMesh = new LabMeshBuilder(160, 320);
            GizaBuild.CeilingStrip(emitMesh, upperMouth, join, pw * 0.15f, ph);
            GizaBuild.CeilingStrip(emitMesh, lowerMouth, burialDoor, pw * 0.15f, ph);
            GizaBuild.SpawnMesh(parent, "Khafre_Emit", emitMesh.Build("Khafre_Emit"), emit, false);

            Vector3 floor = new Vector3(x, join.y - ph * 0.5f, 0f);
            Vector3 floorC = new Vector3(x, floor.y, 0f);
            Vector3 burialC = floorC + Vector3.up * (BurialWallH * 0.5f);
            var room = new LabMeshBuilder(96, 160);
            Color c = new Color(0.62f, 0.58f, 0.50f, 1f);
            room.AddRoom(burialC, new Vector3(BurialEW, BurialWallH, BurialNS), c, false, true, false, false);
            room.AddGableRoof(floorC, BurialEW, BurialNS, BurialWallH, BurialPeakH, c);
            GizaBuild.SpawnMesh(parent, "Khafre_Burial", room.Build("Khafre_Burial"), lime, true);

            var sarc = new LabMeshBuilder(24, 36);
            Color g = new Color(0.18f, 0.16f, 0.16f, 1f);
            Vector3 sarcC = new Vector3(x - 4.2f, floor.y + 0.2f, 0f);
            sarc.AddBox(sarcC, new Vector3(2.6f, 1.0f, 1.05f), g);
            sarc.AddRoom(sarcC + Vector3.up * 0.12f, new Vector3(2.2f, 0.55f, 0.72f), g, false, false, false, false);
            GizaBuild.SpawnMesh(parent, "Khafre_Sarcophagus", sarc.Build("Khafre_Sarcophagus"), granite, true);
        }
    }
}
