using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// West / East / Central / Menkaure Field mastaba cemeteries (west + east of Khufu,
    /// south of Khafre, south of Menkaure queens G3a-c),
    /// Cemetery en Echelon (staggered mastaba strip in the gap west of Khufu / east of West Field),
    /// Khentkawes I (LG100) rock-cut stepped tomb SE of Central Field / NE of Menkaure,
    /// Heit el-Ghurab workers' village (south apron / floodplain), Osiris Shaft-scale rock-cut
    /// complex near Sphinx, and an OFF-by-default SPECULATIVE fringe water-shaft diagram
    /// (not proven archaeology).
    /// Schematic Lehner / Google Earth density - not photogrammetry.
    /// Local +X east, +Z north, 1 unit = 1 m.
    /// </summary>
    public static class GizaField
    {
        public const string WestFieldName = "KhufuWestField";
        public const string EastFieldName = "KhufuEastField";
        public const string CentralFieldName = "GizaCentralField";
        public const string MenkaureFieldName = "MenkaureField";
        public const string WorkersVillageName = "GizaWorkersVillage";
        public const string OsirisShaftName = "OsirisShaft";
        public const string SpeculativeName = "SpeculativeUnderworld";
        public const string KhentkawesName = "Khentkawes";
        public const string CemeteryEnEchelonName = "CemeteryEnEchelon";

        public const string MastabasMarker = "_Mastabas";
        public const string VillageMarker = "_Village";
        public const string CrowWallMarker = "_CrowWall";
        public const string ShaftMarker = "_Shaft";
        public const string SpeculativeShaftsMarker = "_Shafts";
        public const string MassingMarker = "_Massing";

        // Fringe diagrams claim 33-39 ft shafts (~10-12 m). Schematic only - OFF by default.
        public const float SpeculativeShaftDepthM = 11.5f;
        public const float SpeculativeShaftWidthM = 3.2f;
        public const float SpeculativeShaftWallT = 0.35f;
        public const float SpeculativeGridPitchM = 18f;

        // West Field: ~90 m west of Khufu west face, dense N-S cemetery strip.
        public const float WestFieldGapFromFaceM = 28f;
        public const float WestFieldDepthM = 185f;
        public const float WestFieldNorthPadM = 40f;
        public const float WestFieldSouthPadM = 50f;

        // Cemetery en Echelon: staggered strip in the 28 m gap between Khufu west face and West Field.
        public const float EchelonGapFromFaceM = 4f;
        public const float EchelonDepthM = 22f;
        public const float EchelonNorthPadM = 20f;
        public const float EchelonSouthPadM = 10f;

        // East Field: east of queens G1a-c, dense mastaba streets (Lehner East Field schematic).
        public const float EastFieldGapFromQueensM = 18f;
        public const float EastFieldDepthM = 120f;
        public const float EastFieldNorthPadM = 48f;
        public const float EastFieldSouthPadM = 35f;

        // Central Field: south of Khafre (Lehner Central Field schematic rock-cut / mastaba streets).
        public const float CentralFieldGapFromKhafreSouthM = 22f;
        public const float CentralFieldDepthNS = 95f;
        public const float CentralFieldHalfEW = 80f;

        // Menkaure Field: south of queens G3a-c (Lehner Menkaure Field schematic).
        // Shifted slightly west to leave room for Heit el-Ghurab further SE on the floodplain.
        public const float MenkaureFieldGapFromQueensSouthM = 10f;
        public const float MenkaureFieldDepthNS = 68f;
        public const float MenkaureFieldHalfEW = 55f;
        public const float MenkaureFieldEastBiasM = -12f;

        // Heit el-Ghurab schematic south of Menkaure / plateau apron.
        public const float VillageEastOfMenkaureM = 120f;
        public const float VillageSouthOfMenkaureM = 110f;
        public const float VillageEW = 140f;
        public const float VillageNS = 95f;
        // Wall of the Crow (Heit el-Ghurab north edge) - Lehner schematic scale.
        public const float CrowWallLengthM = 200f;
        public const float CrowWallHeightM = 10f;
        public const float CrowWallThicknessM = 3.6f;
        public const float CrowGateWidthM = 7.2f;
        public const float CrowGateHeightM = 6.0f;

        // Khentkawes I (LG100): SE of Central Field / NE of Menkaure (Lehner schematic).
        // east = -MenkaureWestM + EastOfMenkaureM (~-468); north = -MenkaureSouthM + NorthOfMenkaureM (~-588).
        // Verified vs LayoutCentral: west of Central Field west edge (~42 m gap), south of its south edge.
        public const float KhentkawesEastOfMenkaureM = 95f;
        public const float KhentkawesNorthOfMenkaureM = 155f;
        public const float KhentkawesPodiumM = 45.5f;
        public const float KhentkawesPodiumHM = 10f;
        public const float KhentkawesUpperM = 37f;
        public const float KhentkawesUpperHM = 7f;
        public const float KhentkawesCapHM = 0.45f;
        public const float KhentkawesChapelEW = 12f;
        public const float KhentkawesChapelNS = 8f;
        public const float KhentkawesChapelHM = 4.5f;
        public const float KhentkawesBasinEW = 20f;
        public const float KhentkawesBasinNS = 5f;
        public const float KhentkawesBasinDepthM = 2f;

        // Osiris Shaft near Sphinx / Khafre valley (schematic).
        public const float ShaftEastOfSphinxM = -28f;
        public const float ShaftSouthOfSphinxM = 48f;
        public const float ShaftWidthM = 11.5f;
        public const float ShaftDepthM = 30f;
        public const float ShaftWallT = 1.1f;

        public static void ExpandExtents(ref float xMin, ref float xMax, ref float zMin, ref float zMax)
        {
            LayoutWest(out float wEast0, out float wEast1, out float wNorth0, out float wNorth1);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, (wEast0 + wEast1) * 0.5f, (wNorth0 + wNorth1) * 0.5f,
                (wEast1 - wEast0) * 0.5f + 8f, (wNorth1 - wNorth0) * 0.5f + 8f);

            LayoutEchelon(out float ecEast0, out float ecEast1, out float ecNorth0, out float ecNorth1);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, (ecEast0 + ecEast1) * 0.5f, (ecNorth0 + ecNorth1) * 0.5f,
                (ecEast1 - ecEast0) * 0.5f + 6f, (ecNorth1 - ecNorth0) * 0.5f + 6f);

            LayoutEast(out float eEast0, out float eEast1, out float eNorth0, out float eNorth1);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, (eEast0 + eEast1) * 0.5f, (eNorth0 + eNorth1) * 0.5f,
                (eEast1 - eEast0) * 0.5f + 8f, (eNorth1 - eNorth0) * 0.5f + 8f);

            LayoutCentral(out float cEast0, out float cEast1, out float cNorth0, out float cNorth1);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, (cEast0 + cEast1) * 0.5f, (cNorth0 + cNorth1) * 0.5f,
                (cEast1 - cEast0) * 0.5f + 8f, (cNorth1 - cNorth0) * 0.5f + 8f);

            LayoutMenkaureField(out float mfEast0, out float mfEast1, out float mfNorth0, out float mfNorth1);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, (mfEast0 + mfEast1) * 0.5f, (mfNorth0 + mfNorth1) * 0.5f,
                (mfEast1 - mfEast0) * 0.5f + 8f, (mfNorth1 - mfNorth0) * 0.5f + 8f);

            LayoutVillage(out float vEast, out float vNorth);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, vEast, vNorth,
                Mathf.Max(VillageEW, CrowWallLengthM) * 0.5f + 14f, VillageNS * 0.5f + CrowWallThicknessM + 16f);

            LayoutKhentkawes(out float kEast, out float kNorth);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, kEast, kNorth,
                KhentkawesPodiumM * 0.5f + 16f, KhentkawesPodiumM * 0.5f + KhentkawesBasinNS + 14f);

            LayoutShaft(out float sEast, out float sNorth);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, sEast, sNorth, ShaftWidthM * 0.5f + 10f, ShaftWidthM * 0.5f + 10f);
        }

        static void Enc(ref float xMin, ref float xMax, ref float zMin, ref float zMax,
            float east, float north, float rE, float rN)
        {
            xMin = Mathf.Min(xMin, east - rE);
            xMax = Mathf.Max(xMax, east + rE);
            zMin = Mathf.Min(zMin, north - rN);
            zMax = Mathf.Max(zMax, north + rN);
        }

        /// <summary>
        /// Destroy West Field / workers village / Osiris Shaft roots so Ensure* rebuilds them.
        /// Used by Place Giza Complex force path.
        /// </summary>
        public static void ForceRebuildAll()
        {
            DestroyNamed(GizaComplex.FindNamed(WestFieldName));
            DestroyNamed(GizaComplex.FindNamed(CemeteryEnEchelonName));
            DestroyNamed(GizaComplex.FindNamed(EastFieldName));
            DestroyNamed(GizaComplex.FindNamed(CentralFieldName));
            DestroyNamed(GizaComplex.FindNamed(MenkaureFieldName));
            DestroyNamed(GizaComplex.FindNamed(WorkersVillageName));
            DestroyNamed(GizaComplex.FindNamed(KhentkawesName));
            DestroyNamed(GizaComplex.FindNamed(OsirisShaftName));
        }

        public static void EnsureWestField(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(WestFieldName);
            if (old != null && (old.transform.Find(WestFieldName + MastabasMarker) == null
                || old.transform.Find(WestFieldName + "_SurveyHeatmap") == null))
                DestroyNamed(old);
            Ensure(WestFieldName, pose, BuildWestField, pose.surfaceY, true);
        }

        public static void EnsureCemeteryEnEchelon(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(CemeteryEnEchelonName);
            if (old != null && old.transform.Find(CemeteryEnEchelonName + MastabasMarker) == null)
                DestroyNamed(old);
            Ensure(CemeteryEnEchelonName, pose, BuildCemeteryEnEchelon, pose.surfaceY, true);
        }

        public static void EnsureEastField(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(EastFieldName);
            if (old != null && old.transform.Find(EastFieldName + MastabasMarker) == null)
                DestroyNamed(old);
            Ensure(EastFieldName, pose, BuildEastField, pose.surfaceY, true);
        }

        public static void EnsureCentralField(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(CentralFieldName);
            if (old != null && old.transform.Find(CentralFieldName + MastabasMarker) == null)
                DestroyNamed(old);
            // Sit on Khafre terrace height so streets meet the south apron.
            float terrace = pose.surfaceY + GizaComplex.KhafreBedrockM;
            Ensure(CentralFieldName, pose, BuildCentralField, terrace, true);
        }

        public static void EnsureMenkaureField(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(MenkaureFieldName);
            if (old != null && old.transform.Find(MenkaureFieldName + MastabasMarker) == null)
                DestroyNamed(old);
            Ensure(MenkaureFieldName, pose, BuildMenkaureField, pose.surfaceY, true);
        }

        public static void EnsureWorkersVillage(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(WorkersVillageName);
            if (old != null && (old.transform.Find(WorkersVillageName + VillageMarker) == null
                || old.transform.Find(WorkersVillageName + CrowWallMarker) == null))
                DestroyNamed(old);
            float floodY = pose.surfaceY - GizaComplex.CliffHeightM + GizaNile.SitAboveDesertM;
            Ensure(WorkersVillageName, pose, BuildWorkersVillage, floodY, true);
        }

        public static void EnsureKhentkawes(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(KhentkawesName);
            if (old != null && old.transform.Find(KhentkawesName + MassingMarker) == null)
                DestroyNamed(old);
            Ensure(KhentkawesName, pose, BuildKhentkawes, pose.surfaceY, true);
        }

        public static void EnsureOsirisShaft(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(OsirisShaftName);
            if (old != null && (old.transform.Find(OsirisShaftName + ShaftMarker) == null
                || old.transform.Find(SpeculativeName + "/" + SpeculativeName + SpeculativeShaftsMarker) == null))
                DestroyNamed(old);
            Ensure(OsirisShaftName, pose, BuildOsirisShaft, GizaComplex.CourtY(pose), true);
        }

        static GameObject Ensure(string name, GizaComplex.Pose pose,
            System.Func<GizaComplex.Pose, GameObject> build, float sitY, bool sit)
        {
            GameObject existing = GizaComplex.FindNamed(name);
            if (existing != null)
                return existing;
            GameObject go = build(pose);
            if (sit && go != null)
                GizaBuild.SitOn(go.transform, sitY);
            return go;
        }

        static void DestroyNamed(GameObject go)
        {
            if (go == null)
                return;
            go.name = go.name + "_Obsolete";
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        static void LayoutWest(out float east0, out float east1, out float north0, out float north1)
        {
            float khHalf = KhufuPyramid.BaseMeters * 0.5f;
            float westFace = -khHalf;
            east1 = westFace - WestFieldGapFromFaceM;
            east0 = east1 - WestFieldDepthM;
            north1 = khHalf + WestFieldNorthPadM;
            north0 = -khHalf - WestFieldSouthPadM;
        }

        static void LayoutEchelon(out float east0, out float east1, out float north0, out float north1)
        {
            float khHalf = KhufuPyramid.BaseMeters * 0.5f;
            float westFace = -khHalf;
            // Small apron from Khufu west face; strip fills most of WestFieldGapFromFaceM (~22 m deep).
            east1 = westFace - EchelonGapFromFaceM;
            east0 = east1 - EchelonDepthM;
            north1 = khHalf + EchelonNorthPadM;
            north0 = -khHalf - EchelonSouthPadM;
        }

        static void LayoutEast(out float east0, out float east1, out float north0, out float north1)
        {
            // Mirror GizaPrecinct queen east math: past mortuary + causeway start + G1a half-base.
            float khHalf = KhufuPyramid.BaseMeters * 0.5f;
            float pav = KhufuPyramid.PavementWidthM;
            const float templeEW = 40f;
            const float g1aBase = 49.5f;
            float causeStartEast = khHalf + pav + 2f + templeEW;
            float queenEast = causeStartEast + 12f + g1aBase * 0.5f;
            east0 = queenEast + g1aBase * 0.5f + EastFieldGapFromQueensM;
            east1 = east0 + EastFieldDepthM;
            north1 = EastFieldNorthPadM;
            north0 = -khHalf - EastFieldSouthPadM;
        }

        static void LayoutCentral(out float east0, out float east1, out float north0, out float north1)
        {
            float khafreHalf = KhafrePyramid.BaseMeters * 0.5f;
            float cx = -GizaComplex.KhafreWestM;
            east0 = cx - CentralFieldHalfEW;
            east1 = cx + CentralFieldHalfEW;
            float khafreSouth = -GizaComplex.KhafreSouthM - khafreHalf;
            north1 = khafreSouth - CentralFieldGapFromKhafreSouthM;
            north0 = north1 - CentralFieldDepthNS;
        }

        static void LayoutMenkaureField(out float east0, out float east1, out float north0, out float north1)
        {
            float mn = MenkaurePyramid.BaseMeters * 0.5f;
            float cx = -GizaComplex.MenkaureWestM + MenkaureFieldEastBiasM;
            east0 = cx - MenkaureFieldHalfEW;
            east1 = cx + MenkaureFieldHalfEW;
            // Past G3a-c massing south of Menkaure (same local math as MenkaurePyramid.BuildQueens).
            float queensSouth = -GizaComplex.MenkaureSouthM - mn - 8f - MenkaurePyramid.QueenBaseM;
            north1 = queensSouth - MenkaureFieldGapFromQueensSouthM;
            north0 = north1 - MenkaureFieldDepthNS;
        }

        static void LayoutVillage(out float east, out float north)
        {
            float mn = MenkaurePyramid.BaseMeters * 0.5f;
            east = -GizaComplex.MenkaureWestM + VillageEastOfMenkaureM;
            north = -GizaComplex.MenkaureSouthM - mn - VillageSouthOfMenkaureM - VillageNS * 0.5f;
        }

        static void LayoutKhentkawes(out float east, out float north)
        {
            east = -GizaComplex.MenkaureWestM + KhentkawesEastOfMenkaureM;
            north = -GizaComplex.MenkaureSouthM + KhentkawesNorthOfMenkaureM;
        }

        static void LayoutShaft(out float east, out float north)
        {
            east = GizaComplex.SphinxEastM + ShaftEastOfSphinxM;
            north = -GizaComplex.SphinxSouthM - ShaftSouthOfSphinxM;
        }

        static GameObject BuildWestField(GizaComplex.Pose pose)
        {
            LayoutWest(out float east0, out float east1, out float north0, out float north1);
            float cx = (east0 + east1) * 0.5f;
            float cz = (north0 + north1) * 0.5f;
            Vector3 world = GizaComplex.WorldFromKhufu(pose, cx, cz, 0f);
            GameObject root = GizaBuild.Root(WestFieldName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material mud = GizaBuild.Mudbrick();
            Material sand = GizaBuild.DesertSand();
            Material pav = GizaBuild.Pavement();

            float fieldEW = east1 - east0;
            float fieldNS = north1 - north0;

            // Drifted Oasis sand between tombs (teleportable street floor).
            var ground = new LabMeshBuilder(8, 12);
            ground.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(fieldEW + 6f, 0.12f, fieldNS + 6f), Color.white);
            GizaBuild.SpawnMesh(root.transform, WestFieldName + "_Sand", ground.Build(WestFieldName + "_Sand"), sand, true);

            // Dense mastaba grid - combine meshes per E-W row for batching.
            const float streetE = 3.0f;
            const float streetN = 2.8f;
            const float cellE = 11.5f;
            const float cellN = 9.0f;
            float pitchE = cellE + streetE;
            float pitchN = cellN + streetN;
            int cols = Mathf.Max(4, Mathf.FloorToInt((fieldEW - streetE) / pitchE));
            int rows = Mathf.Max(6, Mathf.FloorToInt((fieldNS - streetN) / pitchN));
            float usedE = cols * pitchE - streetE;
            float usedN = rows * pitchN - streetN;
            float x0 = -usedE * 0.5f + cellE * 0.5f;
            float z0 = -usedN * 0.5f + cellN * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                var row = new LabMeshBuilder(cols * 48, cols * 72);
                float z = z0 + r * pitchN;
                for (int c = 0; c < cols; c++)
                {
                    float x = x0 + c * pitchE;
                    // Vary size slightly (schematic density, not identical boxes).
                    float hash = ((r * 37 + c * 17) % 11) / 11f;
                    float ew = cellE * (0.78f + 0.28f * hash);
                    float ns = cellN * (0.82f + 0.22f * ((c * 13 + r) % 7) / 7f);
                    float h = 3.2f + 2.4f * (((r + c * 3) % 5) / 4f);
                    // Occasional larger "elite" mastaba every ~7th.
                    if ((r + c) % 7 == 0)
                    {
                        ew *= 1.45f;
                        ns *= 1.25f;
                        h += 1.4f;
                    }
                    row.AddBox(new Vector3(x, h * 0.5f, z), new Vector3(ew, h, ns), Color.white);
                    // Low coping / cornice hint.
                    row.AddBox(new Vector3(x, h + 0.18f, z), new Vector3(ew * 1.04f, 0.36f, ns * 1.04f), Color.white);
                }
                Material mat = (r % 3 == 0) ? mud : lime;
                string rowName = WestFieldName + "_Row" + r;
                GizaBuild.SpawnMesh(root.transform, rowName, row.Build(rowName), mat, true);
            }

            // Force-rebuild marker (Ensure looks for WestFieldName + "_Mastabas").
            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, WestFieldName + MastabasMarker, mark.Build(WestFieldName + MastabasMarker), pav, false);

            // Optional survey heatmap strip over western third.
            BuildSurveyHeatmap(root.transform, fieldEW, fieldNS);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "West Field cemetery west of Khufu. Dense reconstructed schematic mastaba grid (Lehner / Google Earth density).\n" +
                "Limestone / mudbrick massings with walkable sand streets between tombs. Not photogrammetry. Not excavated chamber interiors.";
            GizaBuild.HonestyPlate(root.transform, WestFieldName + "_Honesty", honesty, 40f);
            Transform plate = root.transform.Find(WestFieldName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(fieldEW * 0.5f + 6f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        static GameObject BuildCemeteryEnEchelon(GizaComplex.Pose pose)
        {
            LayoutEchelon(out float east0, out float east1, out float north0, out float north1);
            float cx = (east0 + east1) * 0.5f;
            float cz = (north0 + north1) * 0.5f;
            Vector3 world = GizaComplex.WorldFromKhufu(pose, cx, cz, 0f);
            GameObject root = GizaBuild.Root(CemeteryEnEchelonName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material mud = GizaBuild.Mudbrick();
            Material sand = GizaBuild.DesertSand();
            Material pav = GizaBuild.Pavement();

            float fieldEW = east1 - east0;
            float fieldNS = north1 - north0;

            var ground = new LabMeshBuilder(8, 12);
            ground.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(fieldEW + 4f, 0.12f, fieldNS + 4f), Color.white);
            GizaBuild.SpawnMesh(root.transform, CemeteryEnEchelonName + "_Sand",
                ground.Build(CemeteryEnEchelonName + "_Sand"), sand, true);

            // Narrow staggered strip - odd rows offset by half E-W pitch (en echelon signature).
            const float streetE = 2.2f;
            const float streetN = 2.4f;
            const float cellE = 7.0f;
            const float cellN = 7.2f;
            float pitchE = cellE + streetE;
            float pitchN = cellN + streetN;
            int cols = Mathf.Max(1, Mathf.FloorToInt((fieldEW - streetE) / pitchE));
            int rows = Mathf.Max(8, Mathf.FloorToInt((fieldNS - streetN) / pitchN));
            float usedE = cols * pitchE - streetE;
            float usedN = rows * pitchN - streetN;
            float x0 = -usedE * 0.5f + cellE * 0.5f;
            float z0 = -usedN * 0.5f + cellN * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                var row = new LabMeshBuilder(cols * 48, cols * 72);
                float z = z0 + r * pitchN;
                float stagger = ((r % 2) == 1) ? pitchE * 0.5f : 0f;
                for (int c = 0; c < cols; c++)
                {
                    float x = x0 + c * pitchE + stagger;
                    float hash = ((r * 29 + c * 23) % 11) / 11f;
                    float ew = cellE * (0.82f + 0.24f * hash);
                    float ns = cellN * (0.84f + 0.20f * ((c * 7 + r) % 9) / 8f);
                    float h = 2.6f + 1.8f * (((r + c * 2) % 5) / 4f);
                    if ((r + c) % 8 == 0)
                    {
                        ew *= 1.35f;
                        ns *= 1.20f;
                        h += 1.1f;
                    }
                    row.AddBox(new Vector3(x, h * 0.5f, z), new Vector3(ew, h, ns), Color.white);
                    row.AddBox(new Vector3(x, h + 0.14f, z), new Vector3(ew * 1.03f, 0.28f, ns * 1.03f), Color.white);
                }
                Material mat = (r % 2 == 0) ? lime : mud;
                string rowName = CemeteryEnEchelonName + "_Row" + r;
                GizaBuild.SpawnMesh(root.transform, rowName, row.Build(rowName), mat, true);
            }

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, CemeteryEnEchelonName + MastabasMarker,
                mark.Build(CemeteryEnEchelonName + MastabasMarker), pav, false);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Cemetery en Echelon west of Khufu. Staggered (en echelon) mastaba strip in the gap between the pyramid pavement and the West Field (Lehner / Reisner schematic).\n" +
                "Odd rows offset by half E-W pitch. Limestone / mudbrick massings with walkable sand streets. Not photogrammetry. Not excavated chamber interiors.";
            GizaBuild.HonestyPlate(root.transform, CemeteryEnEchelonName + "_Honesty", honesty, 28f);
            Transform plate = root.transform.Find(CemeteryEnEchelonName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(fieldEW * 0.5f + 4f, 1.45f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        static GameObject BuildEastField(GizaComplex.Pose pose)
        {
            LayoutEast(out float east0, out float east1, out float north0, out float north1);
            float cx = (east0 + east1) * 0.5f;
            float cz = (north0 + north1) * 0.5f;
            Vector3 world = GizaComplex.WorldFromKhufu(pose, cx, cz, 0f);
            GameObject root = GizaBuild.Root(EastFieldName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material mud = GizaBuild.Mudbrick();
            Material sand = GizaBuild.DesertSand();
            Material pav = GizaBuild.Pavement();

            float fieldEW = east1 - east0;
            float fieldNS = north1 - north0;

            var ground = new LabMeshBuilder(8, 12);
            ground.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(fieldEW + 6f, 0.12f, fieldNS + 6f), Color.white);
            GizaBuild.SpawnMesh(root.transform, EastFieldName + "_Sand", ground.Build(EastFieldName + "_Sand"), sand, true);

            // Slightly tighter cells than West Field (elite East Field density near queens).
            const float streetE = 2.6f;
            const float streetN = 2.4f;
            const float cellE = 10.0f;
            const float cellN = 8.0f;
            float pitchE = cellE + streetE;
            float pitchN = cellN + streetN;
            int cols = Mathf.Max(3, Mathf.FloorToInt((fieldEW - streetE) / pitchE));
            int rows = Mathf.Max(5, Mathf.FloorToInt((fieldNS - streetN) / pitchN));
            float usedE = cols * pitchE - streetE;
            float usedN = rows * pitchN - streetN;
            float x0 = -usedE * 0.5f + cellE * 0.5f;
            float z0 = -usedN * 0.5f + cellN * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                var row = new LabMeshBuilder(cols * 48, cols * 72);
                float z = z0 + r * pitchN;
                for (int c = 0; c < cols; c++)
                {
                    float x = x0 + c * pitchE;
                    float hash = ((r * 41 + c * 19) % 13) / 13f;
                    float ew = cellE * (0.80f + 0.26f * hash);
                    float ns = cellN * (0.84f + 0.20f * ((c * 11 + r) % 9) / 8f);
                    float h = 2.8f + 2.2f * (((r + c * 2) % 5) / 4f);
                    if ((r + c) % 6 == 0)
                    {
                        ew *= 1.55f;
                        ns *= 1.30f;
                        h += 1.6f;
                    }
                    row.AddBox(new Vector3(x, h * 0.5f, z), new Vector3(ew, h, ns), Color.white);
                    row.AddBox(new Vector3(x, h + 0.16f, z), new Vector3(ew * 1.03f, 0.32f, ns * 1.03f), Color.white);
                }
                Material mat = (r % 2 == 0) ? lime : mud;
                string rowName = EastFieldName + "_Row" + r;
                GizaBuild.SpawnMesh(root.transform, rowName, row.Build(rowName), mat, true);
            }

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, EastFieldName + MastabasMarker, mark.Build(EastFieldName + MastabasMarker), pav, false);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "East Field cemetery east of Khufu queens G1a-c. Dense reconstructed schematic mastaba grid (Lehner East Field density).\n" +
                "Limestone / mudbrick massings with walkable sand streets. Does not replace G1a-c. Not photogrammetry. Not excavated chamber interiors.";
            GizaBuild.HonestyPlate(root.transform, EastFieldName + "_Honesty", honesty, 36f);
            Transform plate = root.transform.Find(EastFieldName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(-fieldEW * 0.5f - 6f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            return root;
        }

        static GameObject BuildCentralField(GizaComplex.Pose pose)
        {
            LayoutCentral(out float east0, out float east1, out float north0, out float north1);
            float cx = (east0 + east1) * 0.5f;
            float cz = (north0 + north1) * 0.5f;
            Vector3 world = GizaComplex.WorldFromKhufu(pose, cx, cz, GizaComplex.KhafreBedrockM);
            GameObject root = GizaBuild.Root(CentralFieldName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material mud = GizaBuild.Mudbrick();
            Material sand = GizaBuild.DesertSand();
            Material pav = GizaBuild.Pavement();
            Material rock = GizaBuild.Bedrock();

            float fieldEW = east1 - east0;
            float fieldNS = north1 - north0;

            var ground = new LabMeshBuilder(8, 12);
            ground.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(fieldEW + 6f, 0.12f, fieldNS + 6f), Color.white);
            GizaBuild.SpawnMesh(root.transform, CentralFieldName + "_Sand", ground.Build(CentralFieldName + "_Sand"), sand, true);

            // Mixed rock-cut / mastaba massing (Central Field is partly cut into Khafre quarry ledge).
            const float streetE = 2.8f;
            const float streetN = 2.6f;
            const float cellE = 11.0f;
            const float cellN = 8.5f;
            float pitchE = cellE + streetE;
            float pitchN = cellN + streetN;
            int cols = Mathf.Max(4, Mathf.FloorToInt((fieldEW - streetE) / pitchE));
            int rows = Mathf.Max(4, Mathf.FloorToInt((fieldNS - streetN) / pitchN));
            float usedE = cols * pitchE - streetE;
            float usedN = rows * pitchN - streetN;
            float x0 = -usedE * 0.5f + cellE * 0.5f;
            float z0 = -usedN * 0.5f + cellN * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                var row = new LabMeshBuilder(cols * 48, cols * 72);
                float z = z0 + r * pitchN;
                for (int c = 0; c < cols; c++)
                {
                    float x = x0 + c * pitchE;
                    float hash = ((r * 37 + c * 23) % 17) / 17f;
                    float ew = cellE * (0.78f + 0.30f * hash);
                    float ns = cellN * (0.82f + 0.22f * ((c * 13 + r) % 11) / 10f);
                    float h = 2.4f + 2.6f * (((r * 3 + c) % 7) / 6f);
                    bool rockCut = (r + c) % 5 == 0;
                    if (rockCut)
                    {
                        // Low rock-cut mastaba shells sit closer to the ledge.
                        h = 1.6f + 1.4f * hash;
                        ew *= 1.15f;
                        ns *= 1.10f;
                    }
                    else if ((r + c) % 7 == 0)
                    {
                        ew *= 1.45f;
                        ns *= 1.25f;
                        h += 1.4f;
                    }
                    row.AddBox(new Vector3(x, h * 0.5f, z), new Vector3(ew, h, ns), Color.white);
                    row.AddBox(new Vector3(x, h + 0.14f, z), new Vector3(ew * 1.02f, 0.28f, ns * 1.02f), Color.white);
                }
                Material mat = ((r + 1) % 3 == 0) ? rock : ((r % 2 == 0) ? lime : mud);
                string rowName = CentralFieldName + "_Row" + r;
                GizaBuild.SpawnMesh(root.transform, rowName, row.Build(rowName), mat, true);
            }

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, CentralFieldName + MastabasMarker, mark.Build(CentralFieldName + MastabasMarker), pav, false);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Central Field cemetery south of Khafre. Dense reconstructed schematic mastaba / rock-cut tomb streets (Lehner Central Field).\n" +
                "Limestone, mudbrick, and bedrock massings with walkable sand corridors. Not photogrammetry. Not excavated chamber interiors.";
            GizaBuild.HonestyPlate(root.transform, CentralFieldName + "_Honesty", honesty, 34f);
            Transform plateC = root.transform.Find(CentralFieldName + "_Honesty");
            if (plateC != null)
            {
                plateC.localPosition = new Vector3(0f, 1.55f, fieldNS * 0.5f + 5f);
                plateC.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            return root;
        }

        static GameObject BuildMenkaureField(GizaComplex.Pose pose)
        {
            LayoutMenkaureField(out float east0, out float east1, out float north0, out float north1);
            float cx = (east0 + east1) * 0.5f;
            float cz = (north0 + north1) * 0.5f;
            Vector3 world = GizaComplex.WorldFromKhufu(pose, cx, cz, 0f);
            GameObject root = GizaBuild.Root(MenkaureFieldName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material mud = GizaBuild.Mudbrick();
            Material sand = GizaBuild.DesertSand();
            Material pav = GizaBuild.Pavement();
            Material rock = GizaBuild.Bedrock();

            float fieldEW = east1 - east0;
            float fieldNS = north1 - north0;

            var ground = new LabMeshBuilder(8, 12);
            ground.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(fieldEW + 6f, 0.12f, fieldNS + 6f), Color.white);
            GizaBuild.SpawnMesh(root.transform, MenkaureFieldName + "_Sand",
                ground.Build(MenkaureFieldName + "_Sand"), sand, true);

            // Mixed mastaba / rock-cut shells south of G3a-c (Lehner Menkaure Field density).
            const float streetE = 2.7f;
            const float streetN = 2.5f;
            const float cellE = 10.5f;
            const float cellN = 8.2f;
            float pitchE = cellE + streetE;
            float pitchN = cellN + streetN;
            int cols = Mathf.Max(3, Mathf.FloorToInt((fieldEW - streetE) / pitchE));
            int rows = Mathf.Max(4, Mathf.FloorToInt((fieldNS - streetN) / pitchN));
            float usedE = cols * pitchE - streetE;
            float usedN = rows * pitchN - streetN;
            float x0 = -usedE * 0.5f + cellE * 0.5f;
            float z0 = -usedN * 0.5f + cellN * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                var row = new LabMeshBuilder(cols * 48, cols * 72);
                float z = z0 + r * pitchN;
                for (int c = 0; c < cols; c++)
                {
                    float x = x0 + c * pitchE;
                    float hash = ((r * 31 + c * 29) % 19) / 19f;
                    float ew = cellE * (0.78f + 0.28f * hash);
                    float ns = cellN * (0.82f + 0.22f * ((c * 17 + r) % 11) / 10f);
                    float h = 2.6f + 2.4f * (((r * 2 + c) % 7) / 6f);
                    bool rockCut = (r + c) % 4 == 0;
                    if (rockCut)
                    {
                        h = 1.5f + 1.5f * hash;
                        ew *= 1.12f;
                        ns *= 1.08f;
                    }
                    else if ((r + c) % 6 == 0)
                    {
                        ew *= 1.40f;
                        ns *= 1.22f;
                        h += 1.3f;
                    }
                    row.AddBox(new Vector3(x, h * 0.5f, z), new Vector3(ew, h, ns), Color.white);
                    row.AddBox(new Vector3(x, h + 0.14f, z), new Vector3(ew * 1.02f, 0.28f, ns * 1.02f), Color.white);
                }
                Material mat = ((r + 1) % 3 == 0) ? rock : ((r % 2 == 0) ? lime : mud);
                string rowName = MenkaureFieldName + "_Row" + r;
                GizaBuild.SpawnMesh(root.transform, rowName, row.Build(rowName), mat, true);
            }

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, MenkaureFieldName + MastabasMarker,
                mark.Build(MenkaureFieldName + MastabasMarker), pav, false);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Menkaure Field cemetery south of queens G3a-c. Dense reconstructed schematic mastaba / rock-cut tomb streets (Lehner Menkaure Field).\n" +
                "Limestone, mudbrick, and bedrock massings with walkable sand corridors. Does not replace G3a-c. Not photogrammetry. Not excavated chamber interiors.";
            GizaBuild.HonestyPlate(root.transform, MenkaureFieldName + "_Honesty", honesty, 32f);
            Transform plateM = root.transform.Find(MenkaureFieldName + "_Honesty");
            if (plateM != null)
            {
                plateM.localPosition = new Vector3(0f, 1.55f, fieldNS * 0.5f + 5f);
                plateM.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            return root;
        }

        static GameObject BuildKhentkawes(GizaComplex.Pose pose)
        {
            LayoutKhentkawes(out float east, out float north);
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(KhentkawesName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material sand = GizaBuild.DesertSand();
            Material pav = GizaBuild.Pavement();
            Material rock = GizaBuild.Bedrock();

            float podium = KhentkawesPodiumM;
            float podiumH = KhentkawesPodiumHM;
            float upper = KhentkawesUpperM;
            float upperH = KhentkawesUpperHM;
            float capH = KhentkawesCapHM;
            float half = podium * 0.5f;

            // Sand apron teleportable pad around base.
            var apron = new LabMeshBuilder(8, 12);
            apron.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(podium + 18f, 0.12f, podium + 22f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + "_Apron",
                apron.Build(KhentkawesName + "_Apron"), sand, true);

            // Lower rock-cut podium ~45.5 x 45.5 x ~10 m (Bedrock).
            var podiumMesh = new LabMeshBuilder(8, 12);
            podiumMesh.AddBox(new Vector3(0f, podiumH * 0.5f, 0f), new Vector3(podium, podiumH, podium), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + "_Podium",
                podiumMesh.Build(KhentkawesName + "_Podium"), rock, true);

            // Upper stepped limestone mastaba recessed on top + thin cap course.
            float upperY0 = podiumH;
            var upperMesh = new LabMeshBuilder(16, 24);
            upperMesh.AddBox(new Vector3(0f, upperY0 + upperH * 0.5f, 0f), new Vector3(upper, upperH, upper), Color.white);
            upperMesh.AddBox(new Vector3(0f, upperY0 + upperH + capH * 0.5f, 0f),
                new Vector3(upper * 1.02f, capH, upper * 1.02f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + "_Upper",
                upperMesh.Build(KhentkawesName + "_Upper"), lime, true);

            // East chapel / vestibule shell with open door toward east + walkable pavement corridor.
            float chapelEW = KhentkawesChapelEW;
            float chapelNS = KhentkawesChapelNS;
            float chapelH = KhentkawesChapelHM;
            float wallT = 0.85f;
            float doorW = 2.6f;
            float doorH = 2.4f; // VR headroom >= 2.0 m
            float chapelX = half + chapelEW * 0.5f - 0.2f;
            float deckY = podiumH;

            var chapel = new LabMeshBuilder(64, 96);
            // Floor
            chapel.AddBox(new Vector3(chapelX, deckY + 0.12f, 0f), new Vector3(chapelEW, 0.24f, chapelNS), Color.white);
            // N/S/W walls; east face open as door (leave gap).
            float wallY = deckY + chapelH * 0.5f;
            chapel.AddBox(new Vector3(chapelX, wallY, chapelNS * 0.5f - wallT * 0.5f),
                new Vector3(chapelEW, chapelH, wallT), Color.white);
            chapel.AddBox(new Vector3(chapelX, wallY, -(chapelNS * 0.5f - wallT * 0.5f)),
                new Vector3(chapelEW, chapelH, wallT), Color.white);
            chapel.AddBox(new Vector3(chapelX - chapelEW * 0.5f + wallT * 0.5f, wallY, 0f),
                new Vector3(wallT, chapelH, chapelNS - wallT * 2f), Color.white);
            // East jambs around open door.
            float wing = (chapelNS - doorW) * 0.5f;
            chapel.AddBox(new Vector3(chapelX + chapelEW * 0.5f - wallT * 0.5f, wallY, doorW * 0.5f + wing * 0.5f),
                new Vector3(wallT, chapelH, wing), Color.white);
            chapel.AddBox(new Vector3(chapelX + chapelEW * 0.5f - wallT * 0.5f, wallY, -(doorW * 0.5f + wing * 0.5f)),
                new Vector3(wallT, chapelH, wing), Color.white);
            // Lintel above door.
            float lintelH = Mathf.Max(0.8f, chapelH - doorH);
            chapel.AddBox(new Vector3(chapelX + chapelEW * 0.5f - wallT * 0.5f, deckY + doorH + lintelH * 0.5f, 0f),
                new Vector3(wallT * 1.1f, lintelH, doorW + 1.0f), Color.white);
            // Thin roof slab.
            chapel.AddBox(new Vector3(chapelX, deckY + chapelH + 0.18f, 0f),
                new Vector3(chapelEW + 0.4f, 0.36f, chapelNS + 0.4f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + "_Chapel",
                chapel.Build(KhentkawesName + "_Chapel"), lime, true);

            // Walkable pavement corridor from apron up to chapel door (east approach).
            var corridor = new LabMeshBuilder(24, 36);
            float corrLen = 10f;
            corridor.AddBox(new Vector3(half + corrLen * 0.5f, deckY + 0.1f, 0f),
                new Vector3(corrLen, 0.2f, doorW + 1.6f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + "_Corridor",
                corridor.Build(KhentkawesName + "_Corridor"), pav, true);

            // Short east approach ramp/steps from apron up onto lower deck.
            int steps = 12;
            float rise = podiumH / steps;
            float run = 0.7f;
            float stepW = 5.5f;
            var stairs = new LabMeshBuilder(steps * 16, steps * 24);
            for (int i = 0; i < steps; i++)
            {
                // Low steps farther east on the apron; high steps meet the podium deck.
                float y = (i + 0.5f) * rise;
                float x = half + 0.15f + (steps - i - 0.5f) * run;
                stairs.AddBox(new Vector3(x, y, 0f), new Vector3(run * 0.95f, rise * 0.92f, stepW), Color.white);
            }
            // Top landing on podium edge.
            stairs.AddBox(new Vector3(half - 1.2f, podiumH + 0.08f, 0f),
                new Vector3(2.6f, 0.16f, stepW + 0.8f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + "_Steps",
                stairs.Build(KhentkawesName + "_Steps"), pav, true);

            // Optional small boat basin south of podium (honesty-labeled schematic).
            float basinEW = KhentkawesBasinEW;
            float basinNS = KhentkawesBasinNS;
            float basinD = KhentkawesBasinDepthM;
            float basinZ = -(half + basinNS * 0.5f + 2.5f);
            var basin = new LabMeshBuilder(48, 72);
            // Hollow recessed basin: floor at bottom + four rock walls (open interior).
            float wallT = 0.7f;
            float floorT = 0.35f;
            basin.AddBox(new Vector3(0f, -basinD + floorT * 0.5f, basinZ),
                new Vector3(basinEW - wallT * 2f, floorT, basinNS - wallT * 2f), Color.white);
            float wy = -basinD * 0.5f;
            basin.AddBox(new Vector3(0f, wy, basinZ + basinNS * 0.5f - wallT * 0.5f),
                new Vector3(basinEW, basinD, wallT), Color.white);
            basin.AddBox(new Vector3(0f, wy, basinZ - (basinNS * 0.5f - wallT * 0.5f)),
                new Vector3(basinEW, basinD, wallT), Color.white);
            basin.AddBox(new Vector3(basinEW * 0.5f - wallT * 0.5f, wy, basinZ),
                new Vector3(wallT, basinD, basinNS - wallT * 2f), Color.white);
            basin.AddBox(new Vector3(-(basinEW * 0.5f - wallT * 0.5f), wy, basinZ),
                new Vector3(wallT, basinD, basinNS - wallT * 2f), Color.white);
            // Surface rim.
            float rimT = 0.7f;
            float rimH = 0.55f;
            basin.AddBox(new Vector3(0f, rimH * 0.5f, basinZ + basinNS * 0.5f + rimT * 0.5f),
                new Vector3(basinEW + rimT * 2f, rimH, rimT), Color.white);
            basin.AddBox(new Vector3(0f, rimH * 0.5f, basinZ - (basinNS * 0.5f + rimT * 0.5f)),
                new Vector3(basinEW + rimT * 2f, rimH, rimT), Color.white);
            basin.AddBox(new Vector3(basinEW * 0.5f + rimT * 0.5f, rimH * 0.5f, basinZ),
                new Vector3(rimT, rimH, basinNS), Color.white);
            basin.AddBox(new Vector3(-(basinEW * 0.5f + rimT * 0.5f), rimH * 0.5f, basinZ),
                new Vector3(rimT, rimH, basinNS), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + "_BoatBasin",
                basin.Build(KhentkawesName + "_BoatBasin"), rock, true);

            // Force-rebuild marker.
            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhentkawesName + MassingMarker,
                mark.Build(KhentkawesName + MassingMarker), pav, false);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khentkawes I (LG100) rock-cut stepped tomb complex. Petrie/Lehner schematic massing SE of Central Field / NE of Menkaure.\n" +
                "Lower bedrock podium (~45.5 m square) + upper limestone mastaba, east chapel vestibule, approach steps, optional boat basin.\n" +
                "Schematic - not photogrammetry. Not excavated chamber interiors.";
            GizaBuild.HonestyPlate(root.transform, KhentkawesName + "_Honesty", honesty, 28f);
            Transform plate = root.transform.Find(KhentkawesName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(half + chapelEW + 4f, podiumH + 1.55f, chapelNS * 0.5f + 3f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // Basin honesty (schematic optional feature).
            const string basinHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Schematic boat basin south of Khentkawes I podium (Lehner-scale hint). Not surveyed excavation. Not photogrammetry.";
            GizaBuild.HonestyPlate(root.transform, KhentkawesName + "_BasinHonesty", basinHonesty, 16f);
            Transform bp = root.transform.Find(KhentkawesName + "_BasinHonesty");
            if (bp != null)
            {
                bp.localPosition = new Vector3(basinEW * 0.5f + 3f, 1.4f, basinZ);
                bp.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        static void BuildSurveyHeatmap(Transform parent, float fieldEW, float fieldNS)
        {
            float stripEW = fieldEW * 0.28f;
            float stripNS = fieldNS * 0.55f;
            float x = -fieldEW * 0.5f + stripEW * 0.5f + 4f;
            var quad = new LabMeshBuilder(8, 12);
            float y = 0.18f;
            float hx = stripEW * 0.5f;
            float hz = stripNS * 0.5f;
            Vector3 a = new Vector3(x - hx, y, -hz);
            Vector3 b = new Vector3(x + hx, y, -hz);
            Vector3 c = new Vector3(x + hx, y, hz);
            Vector3 d = new Vector3(x - hx, y, hz);
            quad.AddQuad(a, b, c, d, Vector3.up,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), Color.white);
            Material heat = MakeHeatmapMaterial();
            GizaBuild.SpawnMesh(parent, WestFieldName + "_SurveyHeatmap",
                quad.Build(WestFieldName + "_SurveyHeatmap"), heat, false);

            // Thin red survey outline (schematic AOI).
            Material outline = LabWorldMeshes.MakeLit("RELab_SurveyOutline", new Color(0.92f, 0.18f, 0.12f, 1f), 0.05f, 0.2f, false);
            var frame = new LabMeshBuilder(32, 48);
            const float t = 0.55f;
            float fy = y + 0.05f;
            frame.AddBox(new Vector3(x, fy, hz - t * 0.5f), new Vector3(stripEW, 0.08f, t), Color.white);
            frame.AddBox(new Vector3(x, fy, -hz + t * 0.5f), new Vector3(stripEW, 0.08f, t), Color.white);
            frame.AddBox(new Vector3(x - hx + t * 0.5f, fy, 0f), new Vector3(t, 0.08f, stripNS), Color.white);
            frame.AddBox(new Vector3(x + hx - t * 0.5f, fy, 0f), new Vector3(t, 0.08f, stripNS), Color.white);
            GizaBuild.SpawnMesh(parent, WestFieldName + "_SurveyFrame",
                frame.Build(WestFieldName + "_SurveyFrame"), outline, false);

            const string heatHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Geophysical survey schematic overlay (cyan→yellow→red). Not excavated chambers. Not a real GPR volume.";
            GizaBuild.HonestyPlate(parent, WestFieldName + "_SurveyHonesty", heatHonesty, 18f);
            Transform hp = parent.Find(WestFieldName + "_SurveyHonesty");
            if (hp != null)
            {
                hp.localPosition = new Vector3(x, 1.4f, hz + 5f);
                hp.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        static Material MakeHeatmapMaterial()
        {
            const int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "RELab_WestFieldHeatmap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = x / (float)(res - 1);
                    float v = y / (float)(res - 1);
                    float n = Mathf.PerlinNoise(u * 4.2f + 1.7f, v * 5.1f + 0.4f);
                    float n2 = Mathf.PerlinNoise(u * 9.5f + 3.1f, v * 8.3f + 2.2f);
                    float t = Mathf.Clamp01(0.25f + 0.55f * n + 0.35f * n2 * n2);
                    // Hotter band down the strip centre (survey AOI).
                    float band = 1f - Mathf.Abs(u - 0.48f) * 2.2f;
                    t = Mathf.Clamp01(t * 0.65f + Mathf.Max(0f, band) * 0.55f);
                    pixels[y * res + x] = HeatColor(t);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            Material mat = LabWorldMeshes.MakeLit("RELab_SurveyHeatmap", Color.white, 0.02f, 0.18f, false);
            LabWorldMeshes.ApplyAlbedo(mat, tex, Vector2.one);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f));
            return mat;
        }

        static Color HeatColor(float t)
        {
            // cyan → green → yellow → red
            if (t < 0.33f)
            {
                float u = t / 0.33f;
                return Color.Lerp(new Color(0.05f, 0.85f, 0.95f, 1f), new Color(0.15f, 0.9f, 0.25f, 1f), u);
            }
            if (t < 0.66f)
            {
                float u = (t - 0.33f) / 0.33f;
                return Color.Lerp(new Color(0.15f, 0.9f, 0.25f, 1f), new Color(0.98f, 0.92f, 0.12f, 1f), u);
            }
            float v = (t - 0.66f) / 0.34f;
            return Color.Lerp(new Color(0.98f, 0.92f, 0.12f, 1f), new Color(0.95f, 0.12f, 0.08f, 1f), v);
        }

        static GameObject BuildWorkersVillage(GizaComplex.Pose pose)
        {
            LayoutVillage(out float east, out float north);
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(WorkersVillageName, pose.parent, world, pose.rot);
            Material mud = GizaBuild.Mudbrick();
            Material silt = GizaBuild.NileSilt();
            Material pav = GizaBuild.Pavement();

            float ew = VillageEW;
            float ns = VillageNS;
            var yards = new LabMeshBuilder(8, 12);
            yards.AddBox(new Vector3(0f, 0.05f, 0f), new Vector3(ew + 8f, 0.1f, ns + 8f), Color.white);
            GizaBuild.SpawnMesh(root.transform, WorkersVillageName + "_Yards",
                yards.Build(WorkersVillageName + "_Yards"), silt, true);

            // Street grid + mudbrick house blocks + a few long gallery barracks.
            var village = new LabMeshBuilder(2400, 3600);
            const float street = 3.2f;
            const float blockE = 10.5f;
            const float blockN = 7.5f;
            float pitchE = blockE + street;
            float pitchN = blockN + street;
            int cols = Mathf.Max(6, Mathf.FloorToInt((ew - street) / pitchE));
            int rows = Mathf.Max(5, Mathf.FloorToInt((ns - street) / pitchN));
            float usedE = cols * pitchE - street;
            float usedN = rows * pitchN - street;
            float x0 = -usedE * 0.5f + blockE * 0.5f;
            float z0 = -usedN * 0.5f + blockN * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = x0 + c * pitchE;
                    float z = z0 + r * pitchN;
                    float h = 2.4f + ((r + c) % 3) * 0.35f;
                    village.AddBox(new Vector3(x, h * 0.5f, z), new Vector3(blockE * 0.92f, h, blockN * 0.88f), Color.white);
                    // Courtyard stub on south of each block.
                    village.AddBox(new Vector3(x, 0.35f, z - blockN * 0.55f), new Vector3(blockE * 0.7f, 0.7f, 0.35f), Color.white);
                }
            }
            // Gallery barracks (long E-W buildings) - Heit el-Ghurab signature.
            float[] galleryZ = { usedN * 0.38f, -usedN * 0.12f, -usedN * 0.42f };
            for (int g = 0; g < galleryZ.Length; g++)
            {
                float gh = 3.1f;
                float gl = ew * (0.55f + g * 0.08f);
                float gw = 6.5f + g * 0.8f;
                village.AddBox(new Vector3(-ew * 0.08f, gh * 0.5f, galleryZ[g]), new Vector3(gl, gh, gw), Color.white);
                // Internal divider walls hint.
                for (int k = 0; k < 5; k++)
                {
                    float lx = -gl * 0.4f + k * (gl * 0.8f / 4f);
                    village.AddBox(new Vector3(lx, gh * 0.45f, galleryZ[g]), new Vector3(0.35f, gh * 0.85f, gw * 0.85f), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, WorkersVillageName + VillageMarker,
                village.Build(WorkersVillageName + VillageMarker), mud, true);

            // Walkable street strips (teleport).
            var streets = new LabMeshBuilder(64, 96);
            for (int c = 0; c <= cols; c++)
            {
                float x = (c == 0) ? x0 - blockE * 0.5f - street * 0.5f
                    : (c == cols) ? x0 + (cols - 1) * pitchE + blockE * 0.5f + street * 0.5f
                    : x0 + (c - 1) * pitchE + blockE * 0.5f + street * 0.5f;
                streets.AddBox(new Vector3(x, 0.08f, 0f), new Vector3(street * 0.9f, 0.12f, usedN + street), Color.white);
            }
            for (int r = 0; r <= rows; r++)
            {
                float z = (r == 0) ? z0 - blockN * 0.5f - street * 0.5f
                    : (r == rows) ? z0 + (rows - 1) * pitchN + blockN * 0.5f + street * 0.5f
                    : z0 + (r - 1) * pitchN + blockN * 0.5f + street * 0.5f;
                streets.AddBox(new Vector3(0f, 0.09f, z), new Vector3(usedE + street, 0.12f, street * 0.9f), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, WorkersVillageName + "_Streets",
                streets.Build(WorkersVillageName + "_Streets"), pav, true);

            BuildWallOfTheCrow(root.transform, ew, ns);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Heit el-Ghurab (workers' village) schematic south of the plateau apron / near floodplain.\n" +
                "Mudbrick house grid + long gallery barracks + Wall of the Crow on the north edge (Lehner).\n" +
                "Walkable streets and crow-wall gateway. Not photogrammetry. Not modern Nazlet el-Samman.";
            GizaBuild.HonestyPlate(root.transform, WorkersVillageName + "_Honesty", honesty, 30f);
            Transform plate = root.transform.Find(WorkersVillageName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(ew * 0.5f + 5f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        /// <summary>
        /// Wall of the Crow: massive limestone wall along the north edge of Heit el-Ghurab
        /// with a walkable central gateway. Schematic Lehner scale, not survey points.
        /// </summary>
        static void BuildWallOfTheCrow(Transform parent, float villageEW, float villageNS)
        {
            Material lime = GizaBuild.CliffRock();
            Material pav = GizaBuild.Pavement();
            float len = Mathf.Max(CrowWallLengthM, villageEW * 1.15f);
            float h = CrowWallHeightM;
            float t = CrowWallThicknessM;
            float gateW = CrowGateWidthM;
            float gateH = CrowGateHeightM;
            // Sit just north of the house grid so the wall faces the plateau.
            float wallZ = villageNS * 0.5f + t * 0.55f + 2.5f;
            float wing = (len - gateW) * 0.5f;

            var wall = new LabMeshBuilder(96, 144);
            // East and west wings (leave central gateway open).
            wall.AddBox(new Vector3(-(gateW * 0.5f + wing * 0.5f), h * 0.5f, wallZ),
                new Vector3(wing, h, t), Color.white);
            wall.AddBox(new Vector3(gateW * 0.5f + wing * 0.5f, h * 0.5f, wallZ),
                new Vector3(wing, h, t), Color.white);
            // Lintel / upper course bridging the gate.
            float lintelH = Mathf.Max(1.2f, h - gateH);
            wall.AddBox(new Vector3(0f, gateH + lintelH * 0.5f, wallZ),
                new Vector3(gateW + 1.2f, lintelH, t * 1.05f), Color.white);
            // Gate jamb thickening.
            wall.AddBox(new Vector3(-(gateW * 0.5f + 0.55f), gateH * 0.5f, wallZ),
                new Vector3(1.1f, gateH, t * 1.15f), Color.white);
            wall.AddBox(new Vector3(gateW * 0.5f + 0.55f, gateH * 0.5f, wallZ),
                new Vector3(1.1f, gateH, t * 1.15f), Color.white);
            // Batter hint: low outer toe along both faces.
            wall.AddBox(new Vector3(0f, 0.45f, wallZ + t * 0.55f),
                new Vector3(len * 0.98f, 0.9f, 0.7f), Color.white);
            wall.AddBox(new Vector3(0f, 0.45f, wallZ - t * 0.55f),
                new Vector3(len * 0.98f, 0.9f, 0.7f), Color.white);
            GizaBuild.SpawnMesh(parent, WorkersVillageName + CrowWallMarker,
                wall.Build(WorkersVillageName + CrowWallMarker), lime, true);

            // Walkable gateway floor + short approach aprons N/S.
            var gate = new LabMeshBuilder(24, 36);
            gate.AddBox(new Vector3(0f, 0.1f, wallZ), new Vector3(gateW * 0.92f, 0.18f, t + 4.5f), Color.white);
            gate.AddBox(new Vector3(0f, 0.08f, wallZ - t * 0.5f - 3.2f), new Vector3(gateW * 1.4f, 0.14f, 3.5f), Color.white);
            gate.AddBox(new Vector3(0f, 0.08f, wallZ + t * 0.5f + 3.2f), new Vector3(gateW * 1.4f, 0.14f, 3.5f), Color.white);
            GizaBuild.SpawnMesh(parent, WorkersVillageName + "_CrowGate",
                gate.Build(WorkersVillageName + "_CrowGate"), pav, true);

            const string crowHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Wall of the Crow (Heit el-Ghurab). Massive limestone wall ~200 m E-W with a central gateway\n" +
                "separating the workers' town from the plateau (Lehner). Schematic massing, not measured courses.";
            GizaBuild.HonestyPlate(parent, WorkersVillageName + "_CrowHonesty", crowHonesty, 22f);
            Transform crowPlate = parent.Find(WorkersVillageName + "_CrowHonesty");
            if (crowPlate != null)
            {
                crowPlate.localPosition = new Vector3(len * 0.28f, 2.1f, wallZ + t * 0.5f + 1.2f);
                crowPlate.localRotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }

        static GameObject BuildOsirisShaft(GizaComplex.Pose pose)
        {
            LayoutShaft(out float east, out float north);
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(OsirisShaftName, pose.parent, world, pose.rot);
            Material rock = GizaBuild.CliffRock();
            Material lime = GizaBuild.InteriorLime();
            Material tura = GizaBuild.TuraCasing();
            Material pav = GizaBuild.Pavement();

            float w = ShaftWidthM;
            float depth = ShaftDepthM;
            float wallT = ShaftWallT;
            float hw = w * 0.5f;
            float inner = w - wallT * 2f;

            // Surface rim / collar around the shaft mouth.
            var rim = new LabMeshBuilder(48, 72);
            float rimH = 1.4f;
            float rimOut = hw + 3.5f;
            rim.AddBox(new Vector3(0f, rimH * 0.5f, rimOut - 0.6f), new Vector3(rimOut * 2f, rimH, 1.2f), Color.white);
            rim.AddBox(new Vector3(0f, rimH * 0.5f, -(rimOut - 0.6f)), new Vector3(rimOut * 2f, rimH, 1.2f), Color.white);
            rim.AddBox(new Vector3(rimOut - 0.6f, rimH * 0.5f, 0f), new Vector3(1.2f, rimH, rimOut * 2f - 2.4f), Color.white);
            rim.AddBox(new Vector3(-(rimOut - 0.6f), rimH * 0.5f, 0f), new Vector3(1.2f, rimH, rimOut * 2f - 2.4f), Color.white);
            GizaBuild.SpawnMesh(root.transform, OsirisShaftName + "_Rim", rim.Build(OsirisShaftName + "_Rim"), rock, true);

            // Walkable surface apron ring (leave shaft mouth open).
            var apronRing = new LabMeshBuilder(32, 48);
            float pad = 3.2f;
            apronRing.AddBox(new Vector3(0f, 0.08f, hw + pad * 0.5f + 0.2f), new Vector3(w + pad * 2f, 0.16f, pad), Color.white);
            apronRing.AddBox(new Vector3(0f, 0.08f, -(hw + pad * 0.5f + 0.2f)), new Vector3(w + pad * 2f, 0.16f, pad), Color.white);
            apronRing.AddBox(new Vector3(hw + pad * 0.5f + 0.2f, 0.08f, 0f), new Vector3(pad, 0.16f, w), Color.white);
            apronRing.AddBox(new Vector3(-(hw + pad * 0.5f + 0.2f), 0.08f, 0f), new Vector3(pad, 0.16f, w), Color.white);
            GizaBuild.SpawnMesh(root.transform, OsirisShaftName + "_Apron",
                apronRing.Build(OsirisShaftName + "_Apron"), pav, true);

            // Rock-cut shaft walls (four faces from rim down to bottom).
            var shaft = new LabMeshBuilder(96, 144);
            float wallH = depth;
            float wy = -wallH * 0.5f;
            shaft.AddBox(new Vector3(0f, wy, hw - wallT * 0.5f), new Vector3(w, wallH, wallT), Color.white);
            shaft.AddBox(new Vector3(0f, wy, -(hw - wallT * 0.5f)), new Vector3(w, wallH, wallT), Color.white);
            shaft.AddBox(new Vector3(hw - wallT * 0.5f, wy, 0f), new Vector3(wallT, wallH, w - wallT * 2f), Color.white);
            shaft.AddBox(new Vector3(-(hw - wallT * 0.5f), wy, 0f), new Vector3(wallT, wallH, w - wallT * 2f), Color.white);
            GizaBuild.SpawnMesh(root.transform, OsirisShaftName + ShaftMarker,
                shaft.Build(OsirisShaftName + ShaftMarker), rock, true);

            // Mid ledge / chamber (~halfway).
            float midY = -depth * 0.45f;
            var mid = new LabMeshBuilder(64, 96);
            float midFloorT = 0.45f;
            // Side alcove chambers on N and S.
            float alcD = 3.8f;
            float alcH = 2.8f;
            mid.AddBox(new Vector3(0f, midY + alcH * 0.35f, hw + alcD * 0.35f), new Vector3(inner * 0.7f, alcH, alcD), Color.white);
            mid.AddBox(new Vector3(0f, midY + alcH * 0.35f, -(hw + alcD * 0.35f)), new Vector3(inner * 0.7f, alcH, alcD), Color.white);
            GizaBuild.SpawnMesh(root.transform, OsirisShaftName + "_MidAlcoves",
                mid.Build(OsirisShaftName + "_MidAlcoves"), lime, true);
            // Opening in mid floor for continued descent (ring ledge).
            GizaBuild.SpawnMesh(root.transform, OsirisShaftName + "_MidLedge",
                BuildMidLedgeRing(inner, midY, midFloorT), lime, true);

            // Bottom white limestone block chamber / sarcophagus arrangement.
            float botY = -depth + 0.2f;
            var bottom = new LabMeshBuilder(96, 144);
            bottom.AddBox(new Vector3(0f, botY, 0f), new Vector3(inner - 0.2f, 0.4f, inner - 0.2f), Color.white);
            // Raised white block platform (sarcophagus-ish).
            float sarcEW = 4.2f;
            float sarcNS = 2.4f;
            float sarcH = 1.35f;
            bottom.AddBox(new Vector3(0f, botY + 0.4f + sarcH * 0.5f, 0f), new Vector3(sarcEW, sarcH, sarcNS), Color.white);
            // Perimeter white limestone course blocks.
            float courseH = 1.1f;
            float courseT = 0.85f;
            float cy = botY + 0.4f + courseH * 0.5f;
            float ch = (inner - 0.6f) * 0.5f;
            bottom.AddBox(new Vector3(0f, cy, ch - courseT * 0.5f), new Vector3(inner - 0.8f, courseH, courseT), Color.white);
            bottom.AddBox(new Vector3(0f, cy, -(ch - courseT * 0.5f)), new Vector3(inner - 0.8f, courseH, courseT), Color.white);
            bottom.AddBox(new Vector3(ch - courseT * 0.5f, cy, 0f), new Vector3(courseT, courseH, inner - 1.6f), Color.white);
            bottom.AddBox(new Vector3(-(ch - courseT * 0.5f), cy, 0f), new Vector3(courseT, courseH, inner - 1.6f), Color.white);
            // Lid / upper course stubs.
            bottom.AddBox(new Vector3(0f, botY + 0.4f + sarcH + 0.25f, 0f), new Vector3(sarcEW * 1.05f, 0.35f, sarcNS * 1.05f), Color.white);
            GizaBuild.SpawnMesh(root.transform, OsirisShaftName + "_BottomChamber",
                bottom.Build(OsirisShaftName + "_BottomChamber"), tura, true);

            // Stair / ramp descent along west wall (walkable).
            BuildShaftStairs(root.transform, inner, depth, wallT, hw, lime);

            // Teleport pads at surface, mid, bottom.
            SpawnTeleportPad(root.transform, OsirisShaftName + "_PadSurface",
                new Vector3(hw + 2.2f, 0.12f, 0f), pav);
            SpawnTeleportPad(root.transform, OsirisShaftName + "_PadMid",
                new Vector3(0f, midY + midFloorT * 0.5f + 0.05f, inner * 0.28f), pav);
            SpawnTeleportPad(root.transform, OsirisShaftName + "_PadBottom",
                new Vector3(0f, botY + 0.45f, -inner * 0.28f), pav);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Osiris Shaft-scale rock-cut complex near the Sphinx / Khafre valley area. Schematic ~11.5 m shaft, mid ledge, bottom white limestone chamber blocks.\n" +
                "Known rock-cut complex near Sphinx area (schematic). NOT a claim of a plateau-wide underground water city. Not photogrammetry.";
            GizaBuild.HonestyPlate(root.transform, OsirisShaftName + "_Honesty", honesty, 16f);
            Transform plate = root.transform.Find(OsirisShaftName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hw + 6f, 1.55f, hw + 2f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // SPECULATIVE fringe water-network diagram - OFF by default. Enable SpeculativeUnderworld in Hierarchy to view.
            // Not excavated archaeology. Shaft depths ~33-39 ft from popular fringe diagrams only.
            BuildSpeculativeUnderworld(root.transform, hw);

            return root;
        }

        static void BuildSpeculativeUnderworld(Transform osirisRoot, float osirisHalf)
        {
            var speculative = new GameObject(SpeculativeName);
            speculative.transform.SetParent(osirisRoot, false);
            speculative.SetActive(false);

            Material diagram = MakeSpeculativeDiagramMaterial();
            Material rock = GizaBuild.CliffRock();

            float depth = SpeculativeShaftDepthM;
            float sw = SpeculativeShaftWidthM;
            float wallT = SpeculativeShaftWallT;
            float pitch = SpeculativeGridPitchM;
            int cols = 4;
            int rows = 3;
            float gridEW = (cols - 1) * pitch;
            float gridNS = (rows - 1) * pitch;
            // Offset west/north of Osiris toward Khafre terrace fringe (local +X east, +Z north).
            float originX = -osirisHalf - 22f - gridEW * 0.5f;
            float originZ = osirisHalf + 16f;

            var shafts = new LabMeshBuilder(cols * rows * 48 + 64, cols * rows * 72 + 96);
            var links = new LabMeshBuilder(cols * rows * 24 + 32, cols * rows * 36 + 48);
            float half = sw * 0.5f;
            float inner = sw - wallT * 2f;
            float wy = -depth * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = originX + c * pitch;
                    float z = originZ + r * pitch;
                    // Four thin schematic shaft walls (open mouth, no floor plug - diagram not walkable maze).
                    shafts.AddBox(new Vector3(x, wy, z + half - wallT * 0.5f), new Vector3(sw, depth, wallT), Color.white);
                    shafts.AddBox(new Vector3(x, wy, z - (half - wallT * 0.5f)), new Vector3(sw, depth, wallT), Color.white);
                    shafts.AddBox(new Vector3(x + half - wallT * 0.5f, wy, z), new Vector3(wallT, depth, sw - wallT * 2f), Color.white);
                    shafts.AddBox(new Vector3(x - (half - wallT * 0.5f), wy, z), new Vector3(wallT, depth, sw - wallT * 2f), Color.white);
                    // Surface collar tick so the mouth reads in the diagram.
                    float rim = 0.55f;
                    shafts.AddBox(new Vector3(x, rim * 0.5f, z + half + 0.25f), new Vector3(sw + 0.8f, rim, 0.5f), Color.white);
                    shafts.AddBox(new Vector3(x, rim * 0.5f, z - (half + 0.25f)), new Vector3(sw + 0.8f, rim, 0.5f), Color.white);
                    shafts.AddBox(new Vector3(x + half + 0.25f, rim * 0.5f, z), new Vector3(0.5f, rim, sw), Color.white);
                    shafts.AddBox(new Vector3(x - (half + 0.25f), rim * 0.5f, z), new Vector3(0.5f, rim, sw), Color.white);

                    // Mid-depth link stubs toward neighbors (schematic tunnels, not proven galleries).
                    float linkY = -depth * 0.55f;
                    float linkH = 1.4f;
                    float linkT = 1.1f;
                    if (c < cols - 1)
                    {
                        float lx = x + pitch * 0.5f;
                        links.AddBox(new Vector3(lx, linkY, z), new Vector3(pitch - inner, linkH, linkT), Color.white);
                    }
                    if (r < rows - 1)
                    {
                        float lz = z + pitch * 0.5f;
                        links.AddBox(new Vector3(x, linkY, lz), new Vector3(linkT, linkH, pitch - inner), Color.white);
                    }
                }
            }

            GizaBuild.SpawnMesh(speculative.transform, SpeculativeName + SpeculativeShaftsMarker,
                shafts.Build(SpeculativeName + SpeculativeShaftsMarker), diagram, false);
            GizaBuild.SpawnMesh(speculative.transform, SpeculativeName + "_Links",
                links.Build(SpeculativeName + "_Links"), diagram, false);

            // Ground footprint frame so the grid extent is obvious when toggled on.
            var frame = new LabMeshBuilder(16, 24);
            float fx = originX + gridEW * 0.5f;
            float fz = originZ + gridNS * 0.5f;
            float fe = gridEW * 0.5f + pitch * 0.55f;
            float fn = gridNS * 0.5f + pitch * 0.55f;
            float ft = 0.35f;
            frame.AddBox(new Vector3(fx, 0.08f, fz + fn), new Vector3(fe * 2f, 0.16f, ft), Color.white);
            frame.AddBox(new Vector3(fx, 0.08f, fz - fn), new Vector3(fe * 2f, 0.16f, ft), Color.white);
            frame.AddBox(new Vector3(fx + fe, 0.08f, fz), new Vector3(ft, 0.16f, fn * 2f), Color.white);
            frame.AddBox(new Vector3(fx - fe, 0.08f, fz), new Vector3(ft, 0.16f, fn * 2f), Color.white);
            GizaBuild.SpawnMesh(speculative.transform, SpeculativeName + "_Frame",
                frame.Build(SpeculativeName + "_Frame"), rock, false);

            string fringe =
                "SPECULATIVE fringe diagram only - OFF by default.\n" +
                GizaComplex.HonestyPrefix + "\n" +
                "Popular fringe diagrams claim a plateau-wide underground water-shaft network (~33-39 ft / ~10-12 m shafts) under the Khafre / Sphinx area.\n" +
                "This 4x3 cyan schematic (~" + SpeculativeShaftDepthM.ToString("0.0") + " m shafts + mid-depth stubs) is THAT claim drawn as a toggleable diagram.\n" +
                "It is NOT excavated archaeology, NOT proven chambers, and NOT part of the Osiris Shaft reconstruction. Enable SpeculativeUnderworld in the Hierarchy to view.";
            GizaBuild.HonestyPlate(speculative.transform, SpeculativeName + "_Honesty", fringe, 22f);
            Transform sp = speculative.transform.Find(SpeculativeName + "_Honesty");
            if (sp != null)
            {
                sp.localPosition = new Vector3(originX + gridEW * 0.5f, 1.7f, originZ + gridNS + 10f);
                sp.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        static Material MakeSpeculativeDiagramMaterial()
        {
            // Distinct cyan diagram look so it never reads as real limestone archaeology.
            Material mat = LabWorldMeshes.MakeLit("RELab_SpeculativeUnderworld",
                new Color(0.12f, 0.55f, 0.72f, 1f), 0.05f, 0.35f, true);
            if (mat != null && mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", new Color(0.08f, 0.45f, 0.62f, 1f));
            if (mat != null && mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.18f, 0.62f, 0.78f, 0.92f));
            return mat;
        }

        static Mesh BuildMidLedgeRing(float inner, float midY, float thick)
        {
            var b = new LabMeshBuilder(32, 48);
            float hole = inner * 0.42f;
            float pad = (inner - hole) * 0.5f;
            float outer = inner * 0.5f;
            float innerH = hole * 0.5f;
            float cy = midY;
            b.AddBox(new Vector3(0f, cy, (outer + innerH) * 0.5f), new Vector3(inner - 0.2f, thick, pad), Color.white);
            b.AddBox(new Vector3(0f, cy, -(outer + innerH) * 0.5f), new Vector3(inner - 0.2f, thick, pad), Color.white);
            b.AddBox(new Vector3((outer + innerH) * 0.5f, cy, 0f), new Vector3(pad, thick, hole), Color.white);
            b.AddBox(new Vector3(-(outer + innerH) * 0.5f, cy, 0f), new Vector3(pad, thick, hole), Color.white);
            return b.Build(OsirisShaftName + "_MidLedge");
        }

        static void BuildShaftStairs(Transform parent, float inner, float depth, float wallT, float hw, Material mat)
        {
            int steps = 28;
            float rise = depth / steps;
            float run = 0.55f;
            float stairW = inner * 0.42f;
            float x = -(hw - wallT - stairW * 0.5f - 0.15f);
            var b = new LabMeshBuilder(steps * 16, steps * 24);
            for (int i = 0; i < steps; i++)
            {
                float y = -i * rise - rise * 0.5f;
                float z = hw - wallT - 0.2f - i * run;
                // Wrap: keep stairs along west then turn south face if past corner.
                if (z < -(hw - wallT - 0.5f))
                {
                    z = -(hw - wallT - 0.35f);
                    float x2 = -(hw - wallT - 0.2f) + (i - steps * 0.55f) * run;
                    b.AddBox(new Vector3(x2, y, z), new Vector3(run * 0.95f, rise * 0.92f, stairW * 0.7f), Color.white);
                }
                else
                {
                    b.AddBox(new Vector3(x, y, z), new Vector3(stairW, rise * 0.92f, run * 0.95f), Color.white);
                }
            }
            GizaBuild.SpawnMesh(parent, OsirisShaftName + "_Stairs", b.Build(OsirisShaftName + "_Stairs"), mat, true);
        }

        static void SpawnTeleportPad(Transform parent, string name, Vector3 localPos, Material mat)
        {
            var b = new LabMeshBuilder(8, 12);
            b.AddBox(Vector3.zero, new Vector3(2.4f, 0.18f, 2.4f), Color.white);
            GameObject go = GizaBuild.SpawnMesh(parent, name, b.Build(name), mat, true);
            if (go != null)
                go.transform.localPosition = localPos;
        }
    }
}
