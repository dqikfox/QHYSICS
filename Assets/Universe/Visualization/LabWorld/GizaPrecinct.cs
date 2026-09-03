using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Rest of the undamaged Giza necropolis: Khufu queens G1a-c, mortuary temples,
    /// causeways, boat pits, Khafre valley temple, Sphinx temple, temenos walls.
    /// Reconstructed original massing from published plans (Lehner/Petrie). Not photogrammetry.
    /// Architectural local space: +X east, +Z north, 1 unit = 1 m.
    /// </summary>
    public static class GizaPrecinct
    {
        public const string G1aName = "G1a";
        public const string G1bName = "G1b";
        public const string G1cName = "G1c";
        public const string KhufuMortuaryName = "KhufuMortuary";
        public const string KhufuCausewayName = "KhufuCauseway";
        public const string KhufuBoatPitsName = "KhufuBoatPits";
        public const string KhufuEnclosureName = "KhufuEnclosure";
        public const string KhafreMortuaryName = "KhafreMortuary";
        public const string KhafreCausewayName = "KhafreCauseway";
        public const string KhafreValleyName = "KhafreValleyTemple";
        public const string KhafreEnclosureName = "KhafreEnclosure";
        public const string SphinxTempleName = "SphinxTemple";
        public const string MenkaureMortuaryName = "MenkaureMortuary";
        public const string MenkaureCausewayName = "MenkaureCauseway";
        public const string MenkaureEnclosureName = "MenkaureEnclosure";

        public struct Layout
        {
            public float g1aBase, g1bBase, g1cBase;
            public float g1aHeight, g1bHeight, g1cHeight;
            public float g1aEast, g1bEast, g1cEast;
            public float g1aNorth, g1bNorth, g1cNorth;
            public float khufuTempleEW, khufuTempleNS, khufuTempleEast;
            public float khufuCauseStartEast, khufuCauseEndEast, khufuCauseLen, khufuCauseWid;
            public float boatNorth, boatLen, boatWid;
            public float khafreTempleEW, khafreTempleNS, khafreTempleEast, khafreTempleNorth;
            public float khafreCauseStartEast, khafreCauseStartNorth, khafreCauseEndEast, khafreCauseEndNorth;
            public float valleyEW, valleyNS, valleyEast, valleyNorth;
            public float sphinxTempleEW, sphinxTempleNS, sphinxTempleEast, sphinxTempleNorth;
            public float menkaureTempleEW, menkaureTempleNS, menkaureTempleEast, menkaureTempleNorth;
            public float menCauseStartEast, menCauseEndEast;
        }

        public static Layout Compute()
        {
            var L = new Layout();
            L.g1aBase = 49.5f;
            L.g1aHeight = 30.25f;
            L.g1bBase = 49.0f;
            L.g1bHeight = 30.0f;
            L.g1cBase = 46.2f;
            L.g1cHeight = 29.6f;
            L.khufuTempleEW = 40f;
            L.khufuTempleNS = 52f;
            L.khufuCauseLen = 500f;
            L.khufuCauseWid = 10f;
            L.boatLen = 50f;
            L.boatWid = 7f;
            L.khafreTempleEW = 48f;
            L.khafreTempleNS = 56f;
            L.valleyEW = 45f;
            L.valleyNS = 40f;
            L.sphinxTempleEW = 40f;
            L.sphinxTempleNS = 28f;
            L.menkaureTempleEW = 36f;
            L.menkaureTempleNS = 42f;

            float kh = KhufuPyramid.BaseMeters * 0.5f;
            float khPav = KhufuPyramid.PavementWidthM;
            L.khufuTempleEast = kh + khPav + 2f + L.khufuTempleEW * 0.5f;
            L.khufuCauseStartEast = L.khufuTempleEast + L.khufuTempleEW * 0.5f;
            L.khufuCauseEndEast = L.khufuCauseStartEast + L.khufuCauseLen;

            float queenEast = L.khufuCauseStartEast + 12f + L.g1aBase * 0.5f;
            L.g1aEast = queenEast;
            L.g1bEast = queenEast;
            L.g1cEast = queenEast;
            L.g1aNorth = -(L.khufuCauseWid * 0.5f + 12f + L.g1aBase * 0.5f);
            L.g1bNorth = L.g1aNorth - L.g1aBase * 0.5f - 10f - L.g1bBase * 0.5f;
            L.g1cNorth = L.g1bNorth - L.g1bBase * 0.5f - 10f - L.g1cBase * 0.5f;

            L.boatNorth = -(kh + khPav + 4f + L.boatWid * 0.5f);

            float hf = KhafrePyramid.BaseMeters * 0.5f;
            L.khafreTempleEast = -GizaComplex.KhafreWestM + hf + 5f + 2f + L.khafreTempleEW * 0.5f;
            L.khafreTempleNorth = -GizaComplex.KhafreSouthM;
            L.khafreCauseStartEast = L.khafreTempleEast + L.khafreTempleEW * 0.5f;
            L.khafreCauseStartNorth = L.khafreTempleNorth;

            float sphinxEastEnd = GizaComplex.SphinxEastM + GizaSphinx.LengthM * 0.5f;
            L.sphinxTempleEast = sphinxEastEnd + 6f + L.sphinxTempleEW * 0.5f;
            L.sphinxTempleNorth = -GizaComplex.SphinxSouthM;
            L.valleyEast = L.sphinxTempleEast;
            L.valleyNorth = L.sphinxTempleNorth - L.sphinxTempleNS * 0.5f - 8f - L.valleyNS * 0.5f;
            L.khafreCauseEndEast = L.valleyEast - L.valleyEW * 0.5f;
            L.khafreCauseEndNorth = L.valleyNorth;

            float mn = MenkaurePyramid.BaseMeters * 0.5f;
            L.menkaureTempleEast = -GizaComplex.MenkaureWestM + mn + 4f + 2f + L.menkaureTempleEW * 0.5f;
            L.menkaureTempleNorth = -GizaComplex.MenkaureSouthM;
            L.menCauseStartEast = L.menkaureTempleEast + L.menkaureTempleEW * 0.5f;
            L.menCauseEndEast = L.menCauseStartEast + 180f;
            return L;
        }

        public static void ExpandExtents(ref float xMin, ref float xMax, ref float zMin, ref float zMax)
        {
            Layout L = Compute();
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g1aEast, L.g1aNorth, L.g1aBase * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g1bEast, L.g1bNorth, L.g1bBase * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g1cEast, L.g1cNorth, L.g1cBase * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.khufuTempleEast, 0f, L.khufuTempleEW * 0.5f + 2f, L.khufuTempleNS * 0.5f + 2f);
            // Cliff-lip pad east of mortuary/queens — do not pull the plateau under the full valley causeway run.
            float khufuLipEast = Mathf.Max(
                L.khufuTempleEast + L.khufuTempleEW * 0.5f + 10f,
                L.g1aEast + L.g1aBase * 0.5f + 10f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, khufuLipEast, 0f, 12f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, 0f, L.boatNorth, L.boatLen * 2.5f + 20f, L.boatWid * 0.5f + 6f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.khafreTempleEast, L.khafreTempleNorth, L.khafreTempleEW * 0.5f + 2f, L.khafreTempleNS * 0.5f + 2f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.valleyEast, L.valleyNorth, L.valleyEW * 0.5f + 4f, L.valleyNS * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.sphinxTempleEast, L.sphinxTempleNorth, L.sphinxTempleEW * 0.5f + 4f, L.sphinxTempleNS * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.menkaureTempleEast, L.menkaureTempleNorth, L.menkaureTempleEW * 0.5f + 2f, L.menkaureTempleNS * 0.5f + 2f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.menCauseEndEast, L.menkaureTempleNorth, 10f);
        }

        static void Enc(ref float xMin, ref float xMax, ref float zMin, ref float zMax, float east, float north, float r)
        {
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, east, north, r, r);
        }

        static void Enc(ref float xMin, ref float xMax, ref float zMin, ref float zMax, float east, float north, float rE, float rN)
        {
            xMin = Mathf.Min(xMin, east - rE);
            xMax = Mathf.Max(xMax, east + rE);
            zMin = Mathf.Min(zMin, north - rN);
            zMax = Mathf.Max(zMax, north + rN);
        }

        public static void EnsureKhufu(GizaComplex.Pose pose)
        {
            Layout L = Compute();
            const string queensHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khufu queens G1a (N), G1b, G1c (S). Tura casing ON, electrum pyramidia (reconstructed).\n" +
                "Bases 49.5 / 49.0 / 46.2 m, original heights ~30.25 / 30.0 / 29.6 m (Lehner). East of Khufu, N-S row south of the causeway.\n" +
                "No interiors. Not photogrammetry. Not the stripped modern ruin.";
            const string templeHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khufu mortuary temple. Immediately east of Khufu, between the pyramid and the queens. ~52 x 40 m limestone court + pillared massing.\n" +
                "Open court (walkable), complete walls (not today's stubs). Causeway descends east from the east door down the escarpment to the floodplain / harbor foot.";
            Ensure(G1aName, pose, p => BuildQueen(p, G1aName, L.g1aEast, L.g1aNorth, L.g1aBase, L.g1aHeight, queensHonesty), pose.surfaceY, true);
            Ensure(G1bName, pose, p => BuildQueen(p, G1bName, L.g1bEast, L.g1bNorth, L.g1bBase, L.g1bHeight, null), pose.surfaceY, true);
            Ensure(G1cName, pose, p => BuildQueen(p, G1cName, L.g1cEast, L.g1cNorth, L.g1cBase, L.g1cHeight, null), pose.surfaceY, true);
            Ensure(KhufuMortuaryName, pose, p => BuildMortuary(p, KhufuMortuaryName, L.khufuTempleEast, 0f, 0f, L.khufuTempleEW, L.khufuTempleNS, false, templeHonesty), pose.surfaceY, true);

            // Descend to Nile floodplain / harbor west rim. Plateau extents no longer follow the full run.
            float floodY = pose.surfaceY - GizaComplex.CliffHeightM + GizaNile.SitAboveDesertM;
            GameObject oldCause = GizaComplex.FindNamed(KhufuCausewayName);
            if (oldCause != null && !KhufuCausewayIsDescent(oldCause, floodY))
                DestroyNamed(oldCause);
            GizaComplex.LocalExtents(out _, out float plateauEast, out _, out _);
            float cliffEast = plateauEast + GizaComplex.MarginM;
            float causeEndEast = cliffEast + GizaNile.GapFromCliffM + GizaNile.HarborEastOfCliffM;
            Ensure(KhufuCausewayName, pose, p => BuildCauseway(p, KhufuCausewayName, L.khufuCauseStartEast, 0f, causeEndEast, 0f, pose.surfaceY, floodY, L.khufuCauseWid), pose.surfaceY, false);
            Ensure(KhufuBoatPitsName, pose, p => BuildBoatPits(p, L), pose.surfaceY, true);
            Ensure(KhufuEnclosureName, pose, p => BuildEnclosure(p, KhufuEnclosureName, pose.khufuCenter, 0f, KhufuPyramid.BaseMeters * 0.5f + KhufuPyramid.PavementWidthM, L.khufuTempleNS + 4f, true), pose.surfaceY, true);
        }

        public static void EnsureKhafre(GizaComplex.Pose pose)
        {
            Layout L = Compute();
            float terrace = pose.surfaceY + GizaComplex.KhafreBedrockM;
            const string mortHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khafre mortuary temple. Immediately east of Khafre. Open limestone court, granite pillars, complete walls. Walkable.";
            const string valleyHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khafre valley temple. Granite and limestone, T-shaped pillared halls (walkable block rooms). Undamaged reconstruction of the well-preserved temple beside the Sphinx.\n" +
                "Not photogrammetry. Causeway descends from the Khafre mortuary temple.";
            Ensure(KhafreMortuaryName, pose, p => BuildMortuary(p, KhafreMortuaryName, L.khafreTempleEast, L.khafreTempleNorth, GizaComplex.KhafreBedrockM, L.khafreTempleEW, L.khafreTempleNS, true, mortHonesty), terrace, true);
            Ensure(KhafreValleyName, pose, p => BuildValleyTemple(p, L, valleyHonesty), GizaComplex.CourtY(pose), true);
            Ensure(KhafreCausewayName, pose, p => BuildCauseway(p, KhafreCausewayName, L.khafreCauseStartEast, L.khafreCauseStartNorth, L.khafreCauseEndEast, L.khafreCauseEndNorth, terrace, GizaComplex.CourtY(pose), 10f), pose.surfaceY, false);
            Vector3 khafre = GizaComplex.WorldFromKhufu(pose, -GizaComplex.KhafreWestM, -GizaComplex.KhafreSouthM, GizaComplex.KhafreBedrockM);
            Ensure(KhafreEnclosureName, pose, p => BuildEnclosure(p, KhafreEnclosureName, khafre, GizaComplex.KhafreBedrockM, KhafrePyramid.BaseMeters * 0.5f + 5f, L.khafreTempleNS + 4f, false), terrace, true);
        }

        public static void EnsureSphinx(GizaComplex.Pose pose)
        {
            Layout L = Compute();
            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Sphinx temple. Immediately east of the Sphinx face. Open central court, granite colonnade, ten colossal niches (5 N / 5 S), west sanctuaries toward the Sphinx.\n" +
                "Lehner / ARCE plan massing (walkable). Associated with Khafre's valley complex. Not photogrammetry.";
            GameObject oldTemple = GizaComplex.FindNamed(SphinxTempleName);
            if (oldTemple != null && oldTemple.transform.Find(SphinxTempleName + "_Niches") == null)
                DestroyNamed(oldTemple);
            Ensure(SphinxTempleName, pose, p => BuildSphinxTemple(p, L, honesty), GizaComplex.CourtY(pose), true);
        }

        public static void EnsureMenkaure(GizaComplex.Pose pose)
        {
            Layout L = Compute();
            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Menkaure mortuary temple. Immediately east of Menkaure. Open limestone court, complete walls (reconstructed; historically unfinished granite conversion).\n" +
                "Short schematic causeway east. Queens G3a-c already sit south of Menkaure — not duplicated.";
            Ensure(MenkaureMortuaryName, pose, p => BuildMortuary(p, MenkaureMortuaryName, L.menkaureTempleEast, L.menkaureTempleNorth, 0f, L.menkaureTempleEW, L.menkaureTempleNS, false, honesty), pose.surfaceY, true);
            Ensure(MenkaureCausewayName, pose, p => BuildCauseway(p, MenkaureCausewayName, L.menCauseStartEast, L.menkaureTempleNorth, L.menCauseEndEast, L.menkaureTempleNorth, pose.surfaceY, pose.surfaceY, 8f), pose.surfaceY, false);
            Vector3 men = GizaComplex.WorldFromKhufu(pose, -GizaComplex.MenkaureWestM, -GizaComplex.MenkaureSouthM, 0f);
            Ensure(MenkaureEnclosureName, pose, p => BuildEnclosure(p, MenkaureEnclosureName, men, 0f, MenkaurePyramid.BaseMeters * 0.5f + 4f, L.menkaureTempleNS + 4f, false), pose.surfaceY, true);
        }

        static GameObject Ensure(string name, GizaComplex.Pose pose, System.Func<GizaComplex.Pose, GameObject> build, float sitY, bool sit)
        {
            GameObject existing = GizaComplex.FindNamed(name);
            if (existing != null)
                return existing;
            GameObject go = build(pose);
            if (sit && go != null)
                GizaBuild.SitOn(go.transform, sitY);
            return go;
        }

        static bool KhufuCausewayIsDescent(GameObject go, float floodY)
        {
            if (go == null)
                return false;
            Transform term = go.transform.Find(KhufuCausewayName + "_Terminal");
            if (term == null)
                return false;
            // Flat legacy deck sits near plateau; descent terminal sits near floodplain.
            return term.position.y < floodY + 8f;
        }

        static void DestroyNamed(GameObject go)
        {
            if (go == null)
                return;
            // Rename so FindNamed cannot early-out on a deferred Destroy.
            go.name = go.name + "_Obsolete";
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        static GameObject BuildQueen(GizaComplex.Pose pose, string name, float east, float north, float baseM, float heightM, string honesty)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(name, pose.parent, c, pose.rot);
            Material tura = GizaBuild.TuraCasing();
            Material gold = GizaBuild.Electrum();
            Material pav = GizaBuild.Pavement();
            GizaBuild.Casing(root.transform, name + "_Casing", baseM, heightM, tura, false, 0f, 0f, 0f, 0f, 0.5f);
            GizaBuild.Pyramidion(root.transform, name + "_Pyramidion", baseM, heightM, 0.5f, gold);
            GizaBuild.PavementRing(root.transform, name + "_Pavement", baseM, 3f, pav);
            if (!string.IsNullOrEmpty(honesty))
                GizaBuild.HonestyPlate(root.transform, name + "_Honesty", honesty, baseM);
            return root;
        }

        static GameObject BuildMortuary(GizaComplex.Pose pose, string name, float east, float north, float up,
            float ew, float ns, bool granitePillars, string honesty)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, east, north, up);
            GameObject root = GizaBuild.Root(name, pose.parent, c, pose.rot);
            Material tura = GizaBuild.TuraCasing();
            Material pav = GizaBuild.Pavement();
            Material gran = GizaBuild.Granite();
            Material lime = GizaBuild.InteriorLime();

            const float wallH = 5.2f;
            const float wallT = 1.2f;
            const float floorT = 0.35f;
            const float doorW = 8f;
            float hx = ew * 0.5f;
            float hz = ns * 0.5f;
            float y = wallH * 0.5f;

            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, 0f), new Vector3(ew, floorT, ns), Color.white);
            GizaBuild.SpawnMesh(root.transform, name + "_Floor", floor.Build(name + "_Floor"), pav, true);

            var walls = new LabMeshBuilder(80, 120);
            WallDoorZ(walls, hz - wallT * 0.5f, ew, wallH, wallT, 6f);
            WallDoorZ(walls, -hz + wallT * 0.5f, ew, wallH, wallT, 6f);
            walls.AddBox(new Vector3(-hx + wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, ns), Color.white);
            WallDoorX(walls, hx - wallT * 0.5f, ns, wallH, wallT, doorW);
            GizaBuild.SpawnMesh(root.transform, name + "_Walls", walls.Build(name + "_Walls"), tura, true);

            var pillars = new LabMeshBuilder(64, 96);
            float ph = 4.6f;
            float ps = granitePillars ? 1.15f : 1.0f;
            int nx = 3;
            int nz = 4;
            float px0 = -2f;
            float px1 = hx * 0.42f;
            float pz0 = -hz * 0.40f;
            float pz1 = hz * 0.40f;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float u = nx == 1 ? 0.5f : i / (float)(nx - 1);
                    float v = nz == 1 ? 0.5f : j / (float)(nz - 1);
                    pillars.AddBox(
                        new Vector3(Mathf.Lerp(px0, px1, u), floorT + ph * 0.5f, Mathf.Lerp(pz0, pz1, v)),
                        new Vector3(ps, ph, ps), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, name + "_Pillars", pillars.Build(name + "_Pillars"), granitePillars ? gran : tura, true);

            float sEW = Mathf.Min(10f, ew * 0.32f);
            float sNS = Mathf.Min(14f, ns * 0.42f);
            float sH = 4.8f;
            Vector3 sC = new Vector3(-hx + wallT + sEW * 0.5f + 0.35f, floorT + sH * 0.5f, 0f);
            var sanctum = new LabMeshBuilder(48, 72);
            sanctum.AddRoom(sC, new Vector3(sEW, sH, sNS), Color.white, false, false, false, true);
            GizaBuild.SpawnMesh(root.transform, name + "_Sanctum", sanctum.Build(name + "_Sanctum"), lime, true);

            if (!string.IsNullOrEmpty(honesty))
            {
                GizaBuild.HonestyPlate(root.transform, name + "_Honesty", honesty, Mathf.Max(ew, ns) * 0.4f);
                Transform plate = root.transform.Find(name + "_Honesty");
                if (plate != null)
                    plate.localPosition = new Vector3(0f, 1.55f, hz + 3.5f);
            }
            return root;
        }

        static GameObject BuildValleyTemple(GizaComplex.Pose pose, Layout L, string honesty)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, L.valleyEast, L.valleyNorth, 0f);
            GameObject root = GizaBuild.Root(KhafreValleyName, pose.parent, c, pose.rot);
            Material gran = GizaBuild.Granite();
            Material lime = GizaBuild.InteriorLime();
            Material pav = GizaBuild.Pavement();
            float ew = L.valleyEW;
            float ns = L.valleyNS;
            float hx = ew * 0.5f;
            float hz = ns * 0.5f;
            const float wallH = 8.0f;
            const float wallT = 1.4f;
            const float floorT = 0.4f;
            float y = wallH * 0.5f;

            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, 0f), new Vector3(ew, floorT, ns), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhafreValleyName + "_Floor", floor.Build(KhafreValleyName + "_Floor"), pav, true);

            var walls = new LabMeshBuilder(48, 72);
            walls.AddBox(new Vector3(0f, y, hz - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(0f, y, -hz + wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(-hx + wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, ns), Color.white);
            WallDoorX(walls, hx - wallT * 0.5f, ns, wallH, wallT, 6.5f);
            GizaBuild.SpawnMesh(root.transform, KhafreValleyName + "_Walls", walls.Build(KhafreValleyName + "_Walls"), gran, true);

            var halls = new LabMeshBuilder(160, 240);
            Color stone = Color.white;
            float hallH = 7.0f;
            float hy = floorT + hallH * 0.5f;
            halls.AddRoom(new Vector3(19.2f, hy, 0f), new Vector3(5.0f, hallH, 9.0f), stone, false, false, true, true);
            halls.AddRoom(new Vector3(11.5f, hy, 0f), new Vector3(12.0f, hallH, 28.0f), stone, false, false, true, true);
            halls.AddRoom(new Vector3(-4.5f, hy, 0f), new Vector3(22.0f, hallH, 10.0f), stone, false, false, true, true);
            halls.AddRoom(new Vector3(-19.0f, hy, 0f), new Vector3(7.0f, hallH, 8.0f), stone, false, false, false, true);
            GizaBuild.SpawnMesh(root.transform, KhafreValleyName + "_Halls", halls.Build(KhafreValleyName + "_Halls"), lime, true);

            var pillars = new LabMeshBuilder(128, 192);
            float ps = 1.1f;
            float ph = 6.4f;
            for (int row = 0; row < 2; row++)
            {
                float x = 8.5f + row * 6.0f;
                for (int i = 0; i < 8; i++)
                {
                    float z = Mathf.Lerp(-12.2f, 12.2f, i / 7f);
                    pillars.AddBox(new Vector3(x, floorT + ph * 0.5f, z), new Vector3(ps, ph, ps), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, KhafreValleyName + "_Pillars", pillars.Build(KhafreValleyName + "_Pillars"), gran, true);

            GizaBuild.HonestyPlate(root.transform, KhafreValleyName + "_Honesty", honesty, ns);
            Transform plate = root.transform.Find(KhafreValleyName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 4f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        static GameObject BuildSphinxTemple(GizaComplex.Pose pose, Layout L, string honesty)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, L.sphinxTempleEast, L.sphinxTempleNorth, 0f);
            GameObject root = GizaBuild.Root(SphinxTempleName, pose.parent, c, pose.rot);
            Material tura = GizaBuild.TuraCasing();
            Material pav = GizaBuild.Pavement();
            Material lime = GizaBuild.InteriorLime();
            Material gran = GizaBuild.Granite();
            float ew = L.sphinxTempleEW;
            float ns = L.sphinxTempleNS;
            float hx = ew * 0.5f;
            float hz = ns * 0.5f;
            const float wallH = 6.4f;
            const float wallT = 1.35f;
            const float floorT = 0.35f;
            float y = wallH * 0.5f;

            // Open court floor (sky open — no roof).
            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, 0f), new Vector3(ew, floorT, ns), Color.white);
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Floor", floor.Build(SphinxTempleName + "_Floor"), pav, true);

            // Perimeter walls with east door toward valley / harbor.
            var walls = new LabMeshBuilder(64, 96);
            walls.AddBox(new Vector3(0f, y, hz - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(0f, y, -hz + wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(-hx + wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, ns), Color.white);
            WallDoorX(walls, hx - wallT * 0.5f, ns, wallH, wallT, 7.5f);
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Walls", walls.Build(SphinxTempleName + "_Walls"), tura, true);

            // West sanctuaries facing the Sphinx (three chambers).
            float sEW = 9.5f;
            float sNS = 7.2f;
            float sH = 5.4f;
            float sX = -hx + wallT + sEW * 0.5f + 0.25f;
            float hy = floorT + sH * 0.5f;
            var sanctum = new LabMeshBuilder(96, 144);
            sanctum.AddRoom(new Vector3(sX, hy, 0f), new Vector3(sEW, sH, sNS), Color.white, false, false, false, true);
            sanctum.AddRoom(new Vector3(sX, hy, 8.2f), new Vector3(sEW * 0.85f, sH * 0.92f, 5.4f), Color.white, false, false, false, true);
            sanctum.AddRoom(new Vector3(sX, hy, -8.2f), new Vector3(sEW * 0.85f, sH * 0.92f, 5.4f), Color.white, false, false, false, true);
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Sanctum", sanctum.Build(SphinxTempleName + "_Sanctum"), lime, true);

            // Ten colossal niches (5 north / 5 south) — Lehner Sphinx temple statue bays.
            var niches = new LabMeshBuilder(160, 240);
            float nicheD = 2.4f;
            float nicheW = 3.6f;
            float nicheH = 5.8f;
            float nicheY = floorT + nicheH * 0.5f;
            float nicheZ = hz - wallT - nicheD * 0.5f - 0.15f;
            for (int i = 0; i < 5; i++)
            {
                float u = i / 4f;
                float nx = Mathf.Lerp(-hx + wallT + nicheW * 0.5f + 1.2f, hx - wallT - nicheW * 0.5f - 2.5f, u);
                niches.AddRoom(new Vector3(nx, nicheY, nicheZ), new Vector3(nicheW, nicheH, nicheD), Color.white, true, false, false, false);
                niches.AddRoom(new Vector3(nx, nicheY, -nicheZ), new Vector3(nicheW, nicheH, nicheD), Color.white, false, true, false, false);
                // Pedestal stub for missing colossus (honest empty niche).
                niches.AddBox(new Vector3(nx, floorT + 0.55f, nicheZ), new Vector3(nicheW * 0.72f, 1.1f, nicheD * 0.55f), Color.white);
                niches.AddBox(new Vector3(nx, floorT + 0.55f, -nicheZ), new Vector3(nicheW * 0.72f, 1.1f, nicheD * 0.55f), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Niches", niches.Build(SphinxTempleName + "_Niches"), gran, true);

            // Granite colonnade framing the open court (2 rows × 6).
            var pillars = new LabMeshBuilder(128, 192);
            float ps = 1.05f;
            float ph = 5.6f;
            float py = floorT + ph * 0.5f;
            for (int row = 0; row < 2; row++)
            {
                float px = Mathf.Lerp(-4.5f, 10.5f, row / 1f);
                for (int i = 0; i < 6; i++)
                {
                    float pz = Mathf.Lerp(-hz + wallT + 3.2f, hz - wallT - 3.2f, i / 5f);
                    pillars.AddBox(new Vector3(px, py, pz), new Vector3(ps, ph, ps), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Pillars", pillars.Build(SphinxTempleName + "_Pillars"), gran, true);

            GizaBuild.HonestyPlate(root.transform, SphinxTempleName + "_Honesty", honesty, ns);
            Transform plate = root.transform.Find(SphinxTempleName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 4f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        static GameObject BuildCauseway(GizaComplex.Pose pose, string name,
            float east0, float north0, float east1, float north1, float y0, float y1, float width)
        {
            Vector3 a = GizaComplex.WorldFromKhufu(pose, east0, north0, 0f);
            Vector3 b = GizaComplex.WorldFromKhufu(pose, east1, north1, 0f);
            a.y = y0;
            b.y = y1;
            Vector3 flat = b - a;
            flat.y = 0f;
            float len = flat.magnitude;
            if (len < 1f)
                return GizaBuild.Root(name, pose.parent, a, pose.rot);
            Quaternion rot = Quaternion.LookRotation(flat / len, Vector3.up);
            GameObject root = GizaBuild.Root(name, pose.parent, a, rot);
            Material tura = GizaBuild.TuraCasing();
            Material pav = GizaBuild.Pavement();
            const float deckH = 1.55f;
            const float wallH = 1.45f;
            const float wallT = 0.5f;
            float dy = y1 - y0;
            float hw = width * 0.5f;

            var deck = new LabMeshBuilder(16, 24);
            Vector3 t00 = new Vector3(-hw, deckH, 0f);
            Vector3 t10 = new Vector3(hw, deckH, 0f);
            Vector3 t11 = new Vector3(hw, deckH + dy, len);
            Vector3 t01 = new Vector3(-hw, deckH + dy, len);
            Vector3 b00 = new Vector3(-hw, 0f, 0f);
            Vector3 b10 = new Vector3(hw, 0f, 0f);
            Vector3 b11 = new Vector3(hw, dy, len);
            Vector3 b01 = new Vector3(-hw, dy, len);
            deck.AddQuad(t00, t10, t11, t01, Vector3.up, Color.white);
            deck.AddQuad(b00, b01, b11, b10, Vector3.down, Color.white);
            deck.AddQuad(t00, t01, b01, b00, Vector3.left, Color.white);
            deck.AddQuad(t10, b10, b11, t11, Vector3.right, Color.white);
            deck.AddQuad(t00, b00, b10, t10, Vector3.back, Color.white);
            deck.AddQuad(t01, t11, b11, b01, Vector3.forward, Color.white);
            GizaBuild.SpawnMesh(root.transform, name + "_Deck", deck.Build(name + "_Deck"), pav, true);

            var walls = new LabMeshBuilder(32, 48);
            SlopedRail(walls, -hw + wallT * 0.5f, deckH, deckH + dy, 0f, len, wallT, wallH);
            SlopedRail(walls, hw - wallT * 0.5f, deckH, deckH + dy, 0f, len, wallT, wallH);
            GizaBuild.SpawnMesh(root.transform, name + "_Walls", walls.Build(name + "_Walls"), tura, true);

            if (name == KhufuCausewayName)
            {
                var pad = new LabMeshBuilder(8, 12);
                pad.AddBox(new Vector3(0f, deckH * 0.5f + dy, len + 4f), new Vector3(width + 4f, deckH, 8f), Color.white);
                GizaBuild.SpawnMesh(root.transform, name + "_Terminal", pad.Build(name + "_Terminal"), pav, true);
                const string causeHonesty =
                    GizaComplex.HonestyPrefix + "\n" +
                    "Khufu causeway. Descends from the mortuary east door down the east escarpment to the floodplain / harbor foot (schematic valley terminus pad).\n" +
                    "Walkable deck + rails. Width ~10 m. Not photogrammetry.";
                GizaBuild.HonestyPlate(root.transform, name + "_Honesty", causeHonesty, width + 8f);
                Transform plate = root.transform.Find(name + "_Honesty");
                if (plate != null)
                {
                    plate.localPosition = new Vector3(0f, deckH + dy + 1.55f, len * 0.35f);
                    plate.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }
            return root;
        }

        static GameObject BuildBoatPits(GizaComplex.Pose pose, Layout L)
        {
            GameObject root = GizaBuild.Root(KhufuBoatPitsName, pose.parent, pose.khufuCenter, pose.rot);
            Material tura = GizaBuild.TuraCasing();
            Material pav = GizaBuild.Pavement();
            Material wood = LabWorldMeshes.MakeLit("RELab_BarqueHull", new Color(0.38f, 0.26f, 0.16f, 1f), 0.05f, 0.22f, false);
            const float rimH = 2.4f;
            const float wallT = 0.85f;
            const float gap = 8f;
            int n = 5;
            float span = n * L.boatLen + (n - 1) * gap;
            float x0 = -span * 0.5f + L.boatLen * 0.5f;
            int labelIndex = 3;

            var pits = new LabMeshBuilder(160, 240);
            var floors = new LabMeshBuilder(40, 60);
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * (L.boatLen + gap);
                Vector3 c = new Vector3(x, 0f, L.boatNorth);
                pits.AddBox(new Vector3(c.x, rimH * 0.5f, c.z + L.boatWid * 0.5f - wallT * 0.5f), new Vector3(L.boatLen, rimH, wallT), Color.white);
                pits.AddBox(new Vector3(c.x, rimH * 0.5f, c.z - L.boatWid * 0.5f + wallT * 0.5f), new Vector3(L.boatLen, rimH, wallT), Color.white);
                pits.AddBox(new Vector3(c.x + L.boatLen * 0.5f - wallT * 0.5f, rimH * 0.5f, c.z), new Vector3(wallT, rimH, L.boatWid), Color.white);
                pits.AddBox(new Vector3(c.x - L.boatLen * 0.5f + wallT * 0.5f, rimH * 0.5f, c.z), new Vector3(wallT, rimH, L.boatWid), Color.white);
                floors.AddBox(new Vector3(c.x, 0.14f, c.z), new Vector3(L.boatLen - wallT * 2f, 0.28f, L.boatWid - wallT * 2f), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_Pavement", pits.Build(KhufuBoatPitsName + "_Pavement"), tura, true);
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_Floor", floors.Build(KhufuBoatPitsName + "_Floor"), pav, true);

            float lx = x0 + labelIndex * (L.boatLen + gap);
            Vector3 hullC = new Vector3(lx, 1.05f, L.boatNorth);
            var hull = new LabMeshBuilder(24, 36);
            hull.AddBox(hullC, new Vector3(42f, 1.6f, 4.4f), Color.white);
            hull.AddBox(hullC + new Vector3(22.5f, 0.1f, 0f), new Vector3(5.5f, 1.1f, 2.6f), Color.white);
            hull.AddBox(hullC + new Vector3(-22.5f, 0.1f, 0f), new Vector3(5.5f, 1.1f, 2.6f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_Hull", hull.Build(KhufuBoatPitsName + "_Hull"), wood, true);

            const string barque =
                "Khufu solar barque pit (reconstructed pit, not the museum boat).\n" +
                GizaComplex.HonestyPrefix + "\n" +
                "Five schematic stone-lined pits along Khufu's south face, ~50 x 7 m (Lehner/Petrie south-side boat pits). Lined basins, not excavated cuttings.";
            GizaBuild.HonestyPlate(root.transform, KhufuBoatPitsName + "_Honesty", barque, 20f);
            Transform plate = root.transform.Find(KhufuBoatPitsName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(lx, 1.55f, L.boatNorth - L.boatWid * 0.5f - 6f);
                plate.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            return root;
        }

        static GameObject BuildEnclosure(GizaComplex.Pose pose, string name, Vector3 worldCenter, float upM, float inner, float openEastW, bool skipNorth)
        {
            Vector3 c = worldCenter;
            c.y = pose.khufuCenter.y + upM;
            GameObject root = GizaBuild.Root(name, pose.parent, c, pose.rot);
            Material tura = GizaBuild.TuraCasing();
            const float t = 1.15f;
            const float h = 2.05f;
            float y = h * 0.5f;
            float span = inner * 2f + t;
            var b = new LabMeshBuilder(32, 48);
            b.AddBox(new Vector3(0f, y, -inner), new Vector3(span, h, t), Color.white);
            b.AddBox(new Vector3(-inner, y, 0f), new Vector3(t, h, span), Color.white);
            WallDoorX(b, inner, span, h, t, Mathf.Max(8f, openEastW));
            if (!skipNorth)
                WallDoorZ(b, inner, span, h, t, 12f);
            GizaBuild.SpawnMesh(root.transform, name + "_Walls", b.Build(name + "_Walls"), tura, true);
            return root;
        }


        static void SlopedRail(LabMeshBuilder b, float x, float y0, float y1, float z0, float z1, float thick, float h)
        {
            float ht = thick * 0.5f;
            Vector3 a = new Vector3(x - ht, y0, z0);
            Vector3 br = new Vector3(x + ht, y0, z0);
            Vector3 c = new Vector3(x + ht, y1, z1);
            Vector3 d = new Vector3(x - ht, y1, z1);
            Vector3 a2 = a + Vector3.up * h;
            Vector3 b2 = br + Vector3.up * h;
            Vector3 c2 = c + Vector3.up * h;
            Vector3 d2 = d + Vector3.up * h;
            b.AddQuad(a2, b2, c2, d2, Vector3.up, Color.white);
            b.AddQuad(a, d, c, br, Vector3.down, Color.white);
            b.AddQuad(a, a2, d2, d, Vector3.left, Color.white);
            b.AddQuad(br, c, c2, b2, Vector3.right, Color.white);
            b.AddQuad(a, br, b2, a2, Vector3.back, Color.white);
            b.AddQuad(d, d2, c2, c, Vector3.forward, Color.white);
        }

        static void WallDoorX(LabMeshBuilder b, float wallX, float ns, float wallH, float wallT, float doorW)
        {
            float remain = (ns - doorW) * 0.5f;
            float y = wallH * 0.5f;
            if (remain > 0.3f)
            {
                float zOff = (doorW + remain) * 0.5f;
                b.AddBox(new Vector3(wallX, y, zOff), new Vector3(wallT, wallH, remain), Color.white);
                b.AddBox(new Vector3(wallX, y, -zOff), new Vector3(wallT, wallH, remain), Color.white);
            }
            b.AddBox(new Vector3(wallX, wallH - 0.4f, 0f), new Vector3(wallT, 0.8f, doorW + 0.5f), Color.white);
        }

        static void WallDoorZ(LabMeshBuilder b, float wallZ, float ew, float wallH, float wallT, float doorW)
        {
            float remain = (ew - doorW) * 0.5f;
            float y = wallH * 0.5f;
            if (remain > 0.3f)
            {
                float xOff = (doorW + remain) * 0.5f;
                b.AddBox(new Vector3(xOff, y, wallZ), new Vector3(remain, wallH, wallT), Color.white);
                b.AddBox(new Vector3(-xOff, y, wallZ), new Vector3(remain, wallH, wallT), Color.white);
            }
            b.AddBox(new Vector3(0f, wallH - 0.4f, wallZ), new Vector3(doorW + 0.5f, 0.8f, wallT), Color.white);
        }
    }
}
