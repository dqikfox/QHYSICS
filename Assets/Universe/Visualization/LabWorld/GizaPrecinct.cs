using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Rest of the undamaged Giza necropolis: Khufu queens G1a-c, mortuary temples,
    /// causeways, boat pits, Khufu/Khafre/Menkaure valley temples, Sphinx temple, Sphinx-Khafre link court, temenos walls.
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
        public const string KhufuValleyName = "KhufuValleyTemple";
        public const string KhufuBoatPitsName = "KhufuBoatPits";
        public const string KhufuEnclosureName = "KhufuEnclosure";
        public const string KhafreMortuaryName = "KhafreMortuary";
        public const string KhafreCausewayName = "KhafreCauseway";
        public const string KhafreValleyName = "KhafreValleyTemple";
        public const string KhafreEnclosureName = "KhafreEnclosure";
        public const string SphinxTempleName = "SphinxTemple";
        public const string SphinxEnclosureName = "SphinxEnclosure";
        public const string SphinxValleyLinkName = "SphinxValleyLink";
        public const string MenkaureMortuaryName = "MenkaureMortuary";
        public const string MenkaureCausewayName = "MenkaureCauseway";
        public const string MenkaureValleyName = "MenkaureValleyTemple";
        public const string MenkaureEnclosureName = "MenkaureEnclosure";

        public struct Layout
        {
            public float g1aBase, g1bBase, g1cBase;
            public float g1aHeight, g1bHeight, g1cHeight;
            public float g1aEast, g1bEast, g1cEast;
            public float g1aNorth, g1bNorth, g1cNorth;
            public float khufuTempleEW, khufuTempleNS, khufuTempleEast;
            public float khufuCauseStartEast, khufuCauseEndEast, khufuCauseLen, khufuCauseWid;
            public float khufuValleyEW, khufuValleyNS, khufuValleyEast, khufuValleyNorth;
            public float boatNorth, boatLen, boatWid;
            public float khafreTempleEW, khafreTempleNS, khafreTempleEast, khafreTempleNorth;
            public float khafreCauseStartEast, khafreCauseStartNorth, khafreCauseEndEast, khafreCauseEndNorth;
            public float valleyEW, valleyNS, valleyEast, valleyNorth;
            public float sphinxTempleEW, sphinxTempleNS, sphinxTempleEast, sphinxTempleNorth;
            public float menkaureTempleEW, menkaureTempleNS, menkaureTempleEast, menkaureTempleNorth;
            public float menCauseStartEast, menCauseEndEast, menCauseEndNorth;
            public float menValleyEW, menValleyNS, menValleyEast, menValleyNorth;
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
            L.khufuValleyEW = 52f;
            L.khufuValleyNS = 45f;
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
            L.menValleyEW = 44f;
            L.menValleyNS = 47f;

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
            // Valley terminus east/north filled in EnsureMenkaure from live plateau lip.
            L.menCauseEndEast = L.menCauseStartEast + 40f;
            L.menCauseEndNorth = L.menkaureTempleNorth;
            L.menValleyEast = L.menCauseEndEast + 22f;
            L.menValleyNorth = L.menkaureTempleNorth;
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
            // Cliff-lip pad east of Menkaure mortuary — do not pull the plateau under the valley descent.
            float menLipEast = L.menkaureTempleEast + L.menkaureTempleEW * 0.5f + 10f;
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, menLipEast, L.menkaureTempleNorth, 12f);
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
                "Open court (walkable), complete walls (not today's stubs). Causeway descends east from the east door down the escarpment to the Khufu valley temple on the floodplain.";
            Ensure(G1aName, pose, p => BuildQueen(p, G1aName, L.g1aEast, L.g1aNorth, L.g1aBase, L.g1aHeight, queensHonesty), pose.surfaceY, true);
            Ensure(G1bName, pose, p => BuildQueen(p, G1bName, L.g1bEast, L.g1bNorth, L.g1bBase, L.g1bHeight, null), pose.surfaceY, true);
            Ensure(G1cName, pose, p => BuildQueen(p, G1cName, L.g1cEast, L.g1cNorth, L.g1cBase, L.g1cHeight, null), pose.surfaceY, true);
            Ensure(KhufuMortuaryName, pose, p => BuildMortuary(p, KhufuMortuaryName, L.khufuTempleEast, 0f, 0f, L.khufuTempleEW, L.khufuTempleNS, false, templeHonesty), pose.surfaceY, true);

            // Descend to floodplain Khufu valley temple. Plateau extents no longer follow the full run.
            float floodY = pose.surfaceY - GizaComplex.CliffHeightM + GizaNile.SitAboveDesertM;
            const string valleyHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khufu valley temple. Floodplain-level limestone massing at the causeway foot (Lehner plan scale ~52 x 45 m).\n" +
                "Reconstructed schematic: original largely lost under Nazlet el-Samman. Dual east portals toward the harbor, walkable court + antechambers. Not photogrammetry.";

            GizaComplex.LocalExtents(out _, out float plateauEast, out _, out _);
            float cliffEast = plateauEast + GizaComplex.MarginM;
            L.khufuValleyEast = cliffEast + GizaNile.GapFromCliffM + 40f + L.khufuValleyEW * 0.5f;
            L.khufuValleyNorth = 0f;
            float causeEndEast = L.khufuValleyEast - L.khufuValleyEW * 0.5f;

            GameObject oldCause = GizaComplex.FindNamed(KhufuCausewayName);
            if (oldCause != null && (GizaComplex.FindNamed(KhufuValleyName) == null || !KhufuCausewayIsDescent(oldCause, floodY)
                || oldCause.transform.Find(KhufuCausewayName + "_Roof") == null))
                DestroyNamed(oldCause);
            GameObject oldValley = GizaComplex.FindNamed(KhufuValleyName);
            if (oldValley != null && (oldValley.transform.Find(KhufuValleyName + "_Halls") == null
                || oldValley.transform.Find(KhufuValleyName + "_Portals") == null))
                DestroyNamed(oldValley);

            Ensure(KhufuCausewayName, pose, p => BuildCauseway(p, KhufuCausewayName, L.khufuCauseStartEast, 0f, causeEndEast, 0f, pose.surfaceY, floodY, L.khufuCauseWid), pose.surfaceY, false);
            Ensure(KhufuValleyName, pose, p => BuildKhufuValleyTemple(p, L, valleyHonesty), floodY, true);
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
                "Khafre valley temple. Granite and limestone, T-shaped pillared halls (walkable block rooms). Dual north/south east entrances (vestibule portals) — Lehner / well-preserved Khafre valley temple.\n" +
                "Statue niches and empty pedestals along the T-hall (~Lehner seated-statue bays; colossi missing). North door opens onto the Sphinx-Khafre link court. Not photogrammetry. Causeway descends from the Khafre mortuary temple.";

            // Force rebuild when dual-portal marker or causeway Terminal is missing.
            GameObject oldValley = GizaComplex.FindNamed(KhafreValleyName);
            if (oldValley != null && (oldValley.transform.Find(KhafreValleyName + "_Portals") == null
                || oldValley.transform.Find(KhafreValleyName + "_LinkDoor") == null
                || oldValley.transform.Find(KhafreValleyName + "_Niches") == null))
                DestroyNamed(oldValley);
            GameObject oldCause = GizaComplex.FindNamed(KhafreCausewayName);
            if (oldCause != null && (oldCause.transform.Find(KhafreCausewayName + "_Terminal") == null
                || oldCause.transform.Find(KhafreCausewayName + "_Roof") == null))
                DestroyNamed(oldCause);

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
                "Sphinx temple. Immediately east of the Sphinx face. Twin of Khafre valley temple: dual N/S east portals, west door toward the Sphinx enclosure, south door into the Sphinx-Khafre link court.\n" +
                "Open central court with granite paving, granite colonnade with architraves/lintels, ten colossal niches (5 N / 5 S), west sanctuaries. Lehner / ARCE plan massing (walkable). Not photogrammetry.";
            GameObject oldTemple = GizaComplex.FindNamed(SphinxTempleName);
            if (oldTemple != null && (oldTemple.transform.Find(SphinxTempleName + "_Niches") == null
                || oldTemple.transform.Find(SphinxTempleName + "_Portals") == null
                || oldTemple.transform.Find(SphinxTempleName + "_LinkDoor") == null
                || oldTemple.transform.Find(SphinxTempleName + "_Architraves") == null))
                DestroyNamed(oldTemple);
            Ensure(SphinxTempleName, pose, p => BuildSphinxTemple(p, L, honesty), GizaComplex.CourtY(pose), true);

            const string linkHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Sphinx-Khafre valley link court. Walkable paved terrace in the ~8 m gap between the Sphinx temple south wall and the Khafre valley temple north wall.\n" +
                "Lehner twin-temple terrace (schematic). Facing doors connect the courts. Not photogrammetry.";
            Ensure(SphinxValleyLinkName, pose, p => BuildSphinxValleyLink(p, L, linkHonesty), GizaComplex.CourtY(pose), true);

            // Rock-cut Sphinx enclosure (quarry ditch). Force rebuild when marker missing.
            GameObject oldEnc = GizaComplex.FindNamed(SphinxEnclosureName);
            if (oldEnc != null && oldEnc.transform.Find(SphinxEnclosureName + "_Ditch") == null)
                DestroyNamed(oldEnc);
            Ensure(SphinxEnclosureName, pose, p => BuildSphinxEnclosure(p), GizaComplex.CourtY(pose), true);
        }

        public static void EnsureMenkaure(GizaComplex.Pose pose)
        {
            Layout L = Compute();
            float floodY = pose.surfaceY - GizaComplex.CliffHeightM + GizaNile.SitAboveDesertM;
            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Menkaure mortuary temple. Immediately east of Menkaure. Open limestone court, complete walls (reconstructed; historically unfinished granite conversion).\n" +
                "Causeway descends east down the escarpment to the Menkaure valley temple. Queens G3a-c already sit south of Menkaure — not duplicated.";
            const string valleyHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Menkaure valley temple. Floodplain-level mudbrick / limestone massing east of the cliff (Lehner plan scale ~44 x 47 m).\n" +
                "Reconstructed complete shell; historically unfinished in stone then finished in mudbrick (Shepseskaf). Walkable court + antechambers. Not photogrammetry.";

            Ensure(MenkaureMortuaryName, pose, p => BuildMortuary(p, MenkaureMortuaryName, L.menkaureTempleEast, L.menkaureTempleNorth, 0f, L.menkaureTempleEW, L.menkaureTempleNS, false, honesty), pose.surfaceY, true);

            GizaComplex.LocalExtents(out _, out float plateauEast, out _, out _);
            float cliffEast = plateauEast + GizaComplex.MarginM;
            L.menValleyEast = cliffEast + GizaNile.GapFromCliffM + 36f + L.menValleyEW * 0.5f;
            L.menValleyNorth = L.menkaureTempleNorth;
            L.menCauseEndEast = L.menValleyEast - L.menValleyEW * 0.5f;
            L.menCauseEndNorth = L.menValleyNorth;

            GameObject oldCause = GizaComplex.FindNamed(MenkaureCausewayName);
            if (oldCause != null && (!MenkaureCausewayIsDescent(oldCause, floodY)
                || oldCause.transform.Find(MenkaureCausewayName + "_Roof") == null))
                DestroyNamed(oldCause);
            GameObject oldValley = GizaComplex.FindNamed(MenkaureValleyName);
            if (oldValley != null && oldValley.transform.Find(MenkaureValleyName + "_Halls") == null)
                DestroyNamed(oldValley);

            Ensure(MenkaureCausewayName, pose, p => BuildCauseway(p, MenkaureCausewayName, L.menCauseStartEast, L.menkaureTempleNorth, L.menCauseEndEast, L.menCauseEndNorth, pose.surfaceY, floodY, 8f), pose.surfaceY, false);
            Ensure(MenkaureValleyName, pose, p => BuildMenkaureValleyTemple(p, L, valleyHonesty), floodY, true);
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

        static bool MenkaureCausewayIsDescent(GameObject go, float floodY)
        {
            if (go == null)
                return false;
            Transform term = go.transform.Find(MenkaureCausewayName + "_Terminal");
            if (term == null)
                return false;
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

            // Dual east facade doors (N/S of centerline) — iconic Khafre valley temple portals.
            const float portalDoorW = 3.5f;
            const float portalZ = 10.5f;
            var walls = new LabMeshBuilder(80, 120);
            // North wall door toward Sphinx temple / link court.
            WallDoorZ(walls, hz - wallT * 0.5f, ew, wallH, wallT, 4.8f);
            walls.AddBox(new Vector3(0f, y, -hz + wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(-hx + wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, ns), Color.white);
            float wallX = hx - wallT * 0.5f;
            float halfDoor = portalDoorW * 0.5f;
            float northTop = portalZ + halfDoor;
            float northRemain = hz - northTop;
            if (northRemain > 0.3f)
                walls.AddBox(new Vector3(wallX, y, northTop + northRemain * 0.5f), new Vector3(wallT, wallH, northRemain), Color.white);
            float southBot = -portalZ - halfDoor;
            float southRemain = southBot - (-hz);
            if (southRemain > 0.3f)
                walls.AddBox(new Vector3(wallX, y, southBot - southRemain * 0.5f), new Vector3(wallT, wallH, southRemain), Color.white);
            float centerLen = (portalZ - halfDoor) - (-portalZ + halfDoor);
            walls.AddBox(new Vector3(wallX, y, 0f), new Vector3(wallT, wallH, centerLen), Color.white);
            walls.AddBox(new Vector3(wallX, wallH - 0.4f, portalZ), new Vector3(wallT, 0.8f, portalDoorW + 0.5f), Color.white);
            walls.AddBox(new Vector3(wallX, wallH - 0.4f, -portalZ), new Vector3(wallT, 0.8f, portalDoorW + 0.5f), Color.white);
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

            // Seated-statue niches along T-hall (~Lehner ~23; schematic empty pedestals — missing colossi).
            var niches = new LabMeshBuilder(320, 480);
            float nicheD = 1.8f;
            float nicheW = 2.2f;
            float nicheH = 4.6f;
            float nicheY = floorT + nicheH * 0.5f;
            float pedH = 0.9f;
            float pedY = floorT + pedH * 0.5f;
            // Long N/S hall: center (11.5, 0), size 12 x 28 → x 5.5..17.5, z -14..14.
            float longWest = 5.5f + nicheD * 0.5f + 0.12f;
            float longEast = 17.5f - nicheD * 0.5f - 0.12f;
            for (int i = 0; i < 8; i++)
            {
                float nz = Mathf.Lerp(-12.2f, 12.2f, i / 7f);
                niches.AddRoom(new Vector3(longWest, nicheY, nz), new Vector3(nicheD, nicheH, nicheW), Color.white, false, false, false, true);
                niches.AddBox(new Vector3(longWest, pedY, nz), new Vector3(nicheD * 0.55f, pedH, nicheW * 0.72f), Color.white);
            }
            // East long-hall wall: keep dual-portal approach corridors at z≈±10.5 clear.
            float[] eastZs = { -13.0f, -7.5f, -4.0f, 0f, 4.0f, 7.5f, 13.0f };
            for (int i = 0; i < eastZs.Length; i++)
            {
                float nz = eastZs[i];
                niches.AddRoom(new Vector3(longEast, nicheY, nz), new Vector3(nicheD, nicheH, nicheW), Color.white, false, false, true, false);
                niches.AddBox(new Vector3(longEast, pedY, nz), new Vector3(nicheD * 0.55f, pedH, nicheW * 0.72f), Color.white);
            }
            // Cross-bar west wall (hall center -4.5, size 22 x 10 → west face x≈-15.5).
            float crossWest = -15.5f + nicheD * 0.5f + 0.12f;
            for (int i = 0; i < 5; i++)
            {
                float nz = Mathf.Lerp(-3.2f, 3.2f, i / 4f);
                niches.AddRoom(new Vector3(crossWest, nicheY, nz), new Vector3(nicheD, nicheH, nicheW), Color.white, false, false, false, true);
                niches.AddBox(new Vector3(crossWest, pedY, nz), new Vector3(nicheD * 0.55f, pedH, nicheW * 0.72f), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, KhafreValleyName + "_Niches", niches.Build(KhafreValleyName + "_Niches"), gran, true);

            // East-facade portal vestibules (N/S) protruding slightly outside the east wall.
            const float portalH = 5.0f;
            const float portalDepth = 3.0f;
            const float anteDepth = 3.2f;
            const float anteW = 4.2f;
            const float anteH = 4.8f;
            var portals = new LabMeshBuilder(96, 144);
            float portalHy = floorT + portalH * 0.5f;
            float anteHy = floorT + anteH * 0.5f;
            float vestibX = hx + portalDepth * 0.5f;
            float anteX = hx - wallT - anteDepth * 0.5f - 0.1f;
            // Vestibule corridors: open east (court) and west (into temple through door gaps).
            portals.AddRoom(new Vector3(vestibX, portalHy, portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(vestibX, portalHy, -portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            // Shallow antechamber stubs just inside each portal.
            portals.AddRoom(new Vector3(anteX, anteHy, portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(anteX, anteHy, -portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            GizaBuild.SpawnMesh(root.transform, KhafreValleyName + "_Portals", portals.Build(KhafreValleyName + "_Portals"), gran, true);

            // Marker: north link door toward Sphinx temple (Ensure rebuilds when missing).
            var linkMark = new LabMeshBuilder(8, 12);
            linkMark.AddBox(new Vector3(0f, 0.15f, hz - wallT), new Vector3(0.4f, 0.3f, 0.4f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhafreValleyName + "_LinkDoor", linkMark.Build(KhafreValleyName + "_LinkDoor"), gran, false);

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

            // Perimeter: dual east portals (twin to Khafre valley), west door toward Sphinx.
            const float portalDoorW = 3.2f;
            const float portalZ = 7.0f;
            var walls = new LabMeshBuilder(96, 144);
            walls.AddBox(new Vector3(0f, y, hz - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            // South wall door toward Khafre valley / link court.
            WallDoorZ(walls, -hz + wallT * 0.5f, ew, wallH, wallT, 4.5f);
            // West wall with center door toward Sphinx enclosure / forepaws.
            WallDoorX(walls, -hx + wallT * 0.5f, ns, wallH, wallT, 5.5f);
            // Dual east facade doors (N/S of centerline).
            float wallX = hx - wallT * 0.5f;
            float halfDoor = portalDoorW * 0.5f;
            float northTop = portalZ + halfDoor;
            float northRemain = hz - northTop;
            if (northRemain > 0.3f)
                walls.AddBox(new Vector3(wallX, y, northTop + northRemain * 0.5f), new Vector3(wallT, wallH, northRemain), Color.white);
            float southBot = -portalZ - halfDoor;
            float southRemain = southBot - (-hz);
            if (southRemain > 0.3f)
                walls.AddBox(new Vector3(wallX, y, southBot - southRemain * 0.5f), new Vector3(wallT, wallH, southRemain), Color.white);
            float centerLen = (portalZ - halfDoor) - (-portalZ + halfDoor);
            walls.AddBox(new Vector3(wallX, y, 0f), new Vector3(wallT, wallH, centerLen), Color.white);
            walls.AddBox(new Vector3(wallX, wallH - 0.4f, portalZ), new Vector3(wallT, 0.8f, portalDoorW + 0.5f), Color.white);
            walls.AddBox(new Vector3(wallX, wallH - 0.4f, -portalZ), new Vector3(wallT, 0.8f, portalDoorW + 0.5f), Color.white);
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

            // Outer pavement floor kept; granite court paving for the open central court (Lehner/ARCE).
            float courtEW = 18.5f;
            float courtNS = ns - wallT * 2f - 7.0f;
            float courtX = 3.0f;
            var graniteCourt = new LabMeshBuilder(8, 12);
            graniteCourt.AddBox(new Vector3(courtX, floorT * 0.5f + 0.04f, 0f), new Vector3(courtEW, floorT + 0.08f, courtNS), Color.white);
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_GraniteCourt", graniteCourt.Build(SphinxTempleName + "_GraniteCourt"), gran, true);

            // Granite colonnade framing the open court (2 rows x 6).
            var pillars = new LabMeshBuilder(128, 192);
            float ps = 1.05f;
            float ph = 5.6f;
            float py = floorT + ph * 0.5f;
            float rowX0 = -4.5f;
            float rowX1 = 10.5f;
            float z0 = -hz + wallT + 3.2f;
            float z1 = hz - wallT - 3.2f;
            for (int row = 0; row < 2; row++)
            {
                float px = Mathf.Lerp(rowX0, rowX1, row / 1f);
                for (int i = 0; i < 6; i++)
                {
                    float pz = Mathf.Lerp(z0, z1, i / 5f);
                    pillars.AddBox(new Vector3(px, py, pz), new Vector3(ps, ph, ps), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Pillars", pillars.Build(SphinxTempleName + "_Pillars"), gran, true);

            // Colonnade architraves / lintels atop the pillar rows (Ensure rebuild marker).
            const float beamH = 0.55f;
            const float beamW = 1.25f;
            float beamY = floorT + ph + beamH * 0.5f;
            float spanZ = (z1 - z0) + ps + 0.4f;
            var arch = new LabMeshBuilder(96, 144);
            arch.AddBox(new Vector3(rowX0, beamY, 0f), new Vector3(beamW, beamH, spanZ), Color.white);
            arch.AddBox(new Vector3(rowX1, beamY, 0f), new Vector3(beamW, beamH, spanZ), Color.white);
            for (int i = 0; i < 6; i++)
            {
                float pz = Mathf.Lerp(z0, z1, i / 5f);
                float midX = (rowX0 + rowX1) * 0.5f;
                float crossLen = (rowX1 - rowX0) + ps * 0.35f;
                arch.AddBox(new Vector3(midX, beamY, pz), new Vector3(crossLen, beamH * 0.85f, beamW * 0.72f), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Architraves", arch.Build(SphinxTempleName + "_Architraves"), gran, true);

            // East-facade portal vestibules (N/S) — twin temple pattern with Khafre valley.
            const float portalH = 4.6f;
            const float portalDepth = 2.8f;
            const float anteDepth = 2.8f;
            const float anteW = 3.8f;
            const float anteH = 4.4f;
            var portals = new LabMeshBuilder(96, 144);
            float portalHy = floorT + portalH * 0.5f;
            float anteHy = floorT + anteH * 0.5f;
            float vestibX = hx + portalDepth * 0.5f;
            float anteX = hx - wallT - anteDepth * 0.5f - 0.1f;
            portals.AddRoom(new Vector3(vestibX, portalHy, portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(vestibX, portalHy, -portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(anteX, anteHy, portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(anteX, anteHy, -portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_Portals", portals.Build(SphinxTempleName + "_Portals"), gran, true);

            // Marker: south link door toward Khafre valley (Ensure rebuilds when missing).
            var linkMark = new LabMeshBuilder(8, 12);
            linkMark.AddBox(new Vector3(0f, 0.15f, -hz + wallT), new Vector3(0.4f, 0.3f, 0.4f), Color.white);
            GizaBuild.SpawnMesh(root.transform, SphinxTempleName + "_LinkDoor", linkMark.Build(SphinxTempleName + "_LinkDoor"), gran, false);

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
            // Covered processional corridor (Lehner): side walls + stone roof, VR headroom ~3.2 m.
            const float wallH = 3.2f;
            const float wallT = 0.5f;
            const float roofT = 0.5f;
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

            // Stone roof / ceiling (Ensure rebuild marker: _Roof).
            float ry0 = deckH + wallH;
            float ry1 = deckH + dy + wallH;
            var roof = new LabMeshBuilder(16, 24);
            Vector3 rt00 = new Vector3(-hw, ry0 + roofT, 0f);
            Vector3 rt10 = new Vector3(hw, ry0 + roofT, 0f);
            Vector3 rt11 = new Vector3(hw, ry1 + roofT, len);
            Vector3 rt01 = new Vector3(-hw, ry1 + roofT, len);
            Vector3 rb00 = new Vector3(-hw, ry0, 0f);
            Vector3 rb10 = new Vector3(hw, ry0, 0f);
            Vector3 rb11 = new Vector3(hw, ry1, len);
            Vector3 rb01 = new Vector3(-hw, ry1, len);
            roof.AddQuad(rt00, rt10, rt11, rt01, Vector3.up, Color.white);
            roof.AddQuad(rb00, rb01, rb11, rb10, Vector3.down, Color.white);
            roof.AddQuad(rt00, rt01, rb01, rb00, Vector3.left, Color.white);
            roof.AddQuad(rt10, rb10, rb11, rt11, Vector3.right, Color.white);
            roof.AddQuad(rt00, rb00, rb10, rt10, Vector3.back, Color.white);
            roof.AddQuad(rt01, rt11, rb11, rb01, Vector3.forward, Color.white);
            GizaBuild.SpawnMesh(root.transform, name + "_Roof", roof.Build(name + "_Roof"), tura, true);

            if (name == KhufuCausewayName || name == MenkaureCausewayName || name == KhafreCausewayName)
            {
                var pad = new LabMeshBuilder(8, 12);
                pad.AddBox(new Vector3(0f, deckH * 0.5f + dy, len + 4f), new Vector3(width + 4f, deckH, 8f), Color.white);
                GizaBuild.SpawnMesh(root.transform, name + "_Terminal", pad.Build(name + "_Terminal"), pav, true);
                string causeHonesty;
                if (name == KhufuCausewayName)
                {
                    causeHonesty = GizaComplex.HonestyPrefix + "\n" +
                        "Khufu causeway. Descends from the mortuary east door down the east escarpment to the Khufu valley temple on the floodplain.\n" +
                        "Covered walkable corridor (deck + side walls + stone roof). Width ~10 m. Not photogrammetry.";
                }
                else if (name == MenkaureCausewayName)
                {
                    causeHonesty = GizaComplex.HonestyPrefix + "\n" +
                        "Menkaure causeway. Descends from the mortuary east door down the east escarpment to the Menkaure valley temple on the floodplain.\n" +
                        "Covered walkable corridor (deck + side walls + stone roof). Width ~8 m. Not photogrammetry.";
                }
                else
                {
                    causeHonesty = GizaComplex.HonestyPrefix + "\n" +
                        "Khafre causeway. Descends from the Khafre mortuary east door down to the Khafre valley temple / Sphinx court terrace.\n" +
                        "Covered walkable corridor (deck + side walls + stone roof). Width ~10 m. Not photogrammetry.";
                }
                GizaBuild.HonestyPlate(root.transform, name + "_Honesty", causeHonesty, width + 8f);
                Transform plate = root.transform.Find(name + "_Honesty");
                if (plate != null)
                {
                    plate.localPosition = new Vector3(0f, ry1 + roofT + 1.55f, len * 0.35f);
                    plate.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }
            return root;
        }

        static GameObject BuildMenkaureValleyTemple(GizaComplex.Pose pose, Layout L, string honesty)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, L.menValleyEast, L.menValleyNorth, 0f);
            GameObject root = GizaBuild.Root(MenkaureValleyName, pose.parent, c, pose.rot);
            Material mud = GizaBuild.Mudbrick();
            Material lime = GizaBuild.InteriorLime();
            Material pav = GizaBuild.Pavement();
            Material tura = GizaBuild.TuraCasing();
            float ew = L.menValleyEW;
            float ns = L.menValleyNS;
            float hx = ew * 0.5f;
            float hz = ns * 0.5f;
            const float wallH = 7.2f;
            const float wallT = 1.35f;
            const float floorT = 0.35f;
            float y = wallH * 0.5f;

            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, 0f), new Vector3(ew, floorT, ns), Color.white);
            GizaBuild.SpawnMesh(root.transform, MenkaureValleyName + "_Floor", floor.Build(MenkaureValleyName + "_Floor"), pav, true);

            var walls = new LabMeshBuilder(48, 72);
            walls.AddBox(new Vector3(0f, y, hz - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(0f, y, -hz + wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(hx - wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, ns), Color.white);
            WallDoorX(walls, -hx + wallT * 0.5f, ns, wallH, wallT, 6.0f);
            GizaBuild.SpawnMesh(root.transform, MenkaureValleyName + "_Walls", walls.Build(MenkaureValleyName + "_Walls"), mud, true);

            var halls = new LabMeshBuilder(160, 240);
            Color stone = Color.white;
            float hallH = 6.2f;
            float hy = floorT + hallH * 0.5f;
            // Open court band + west antechambers toward causeway door.
            halls.AddRoom(new Vector3(6.0f, hy, 0f), new Vector3(24.0f, hallH, 28.0f), stone, false, false, true, true);
            halls.AddRoom(new Vector3(-12.5f, hy, 0f), new Vector3(14.0f, hallH, 12.0f), stone, false, false, true, true);
            halls.AddRoom(new Vector3(-12.5f, hy, 12.5f), new Vector3(10.0f, hallH * 0.9f, 8.0f), stone, false, false, false, true);
            halls.AddRoom(new Vector3(-12.5f, hy, -12.5f), new Vector3(10.0f, hallH * 0.9f, 8.0f), stone, false, false, false, true);
            GizaBuild.SpawnMesh(root.transform, MenkaureValleyName + "_Halls", halls.Build(MenkaureValleyName + "_Halls"), lime, true);

            var pillars = new LabMeshBuilder(96, 144);
            float ps = 1.0f;
            float ph = 5.6f;
            for (int row = 0; row < 2; row++)
            {
                float x = 2.0f + row * 8.0f;
                for (int i = 0; i < 6; i++)
                {
                    float z = Mathf.Lerp(-11.5f, 11.5f, i / 5f);
                    pillars.AddBox(new Vector3(x, floorT + ph * 0.5f, z), new Vector3(ps, ph, ps), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, MenkaureValleyName + "_Pillars", pillars.Build(MenkaureValleyName + "_Pillars"), tura, true);

            GizaBuild.HonestyPlate(root.transform, MenkaureValleyName + "_Honesty", honesty, ns);
            Transform plate = root.transform.Find(MenkaureValleyName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 4f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }


        static GameObject BuildKhufuValleyTemple(GizaComplex.Pose pose, Layout L, string honesty)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, L.khufuValleyEast, L.khufuValleyNorth, 0f);
            GameObject root = GizaBuild.Root(KhufuValleyName, pose.parent, c, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material pav = GizaBuild.Pavement();
            Material tura = GizaBuild.TuraCasing();
            Material gran = GizaBuild.Granite();
            float ew = L.khufuValleyEW;
            float ns = L.khufuValleyNS;
            float hx = ew * 0.5f;
            float hz = ns * 0.5f;
            const float wallH = 7.6f;
            const float wallT = 1.4f;
            const float floorT = 0.35f;
            float y = wallH * 0.5f;

            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, 0f), new Vector3(ew, floorT, ns), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhufuValleyName + "_Floor", floor.Build(KhufuValleyName + "_Floor"), pav, true);

            // Dual east facade doors (N/S) toward harbor — twin pattern with Khafre valley.
            const float portalDoorW = 3.4f;
            const float portalZ = 9.5f;
            var walls = new LabMeshBuilder(80, 120);
            walls.AddBox(new Vector3(0f, y, hz - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(0f, y, -hz + wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            WallDoorX(walls, -hx + wallT * 0.5f, ns, wallH, wallT, 6.5f);
            float wallX = hx - wallT * 0.5f;
            float halfDoor = portalDoorW * 0.5f;
            float northTop = portalZ + halfDoor;
            float northRemain = hz - northTop;
            if (northRemain > 0.3f)
                walls.AddBox(new Vector3(wallX, y, northTop + northRemain * 0.5f), new Vector3(wallT, wallH, northRemain), Color.white);
            float southBot = -portalZ - halfDoor;
            float southRemain = southBot - (-hz);
            if (southRemain > 0.3f)
                walls.AddBox(new Vector3(wallX, y, southBot - southRemain * 0.5f), new Vector3(wallT, wallH, southRemain), Color.white);
            float centerLen = (portalZ - halfDoor) - (-portalZ + halfDoor);
            walls.AddBox(new Vector3(wallX, y, 0f), new Vector3(wallT, wallH, centerLen), Color.white);
            walls.AddBox(new Vector3(wallX, wallH - 0.4f, portalZ), new Vector3(wallT, 0.8f, portalDoorW + 0.5f), Color.white);
            walls.AddBox(new Vector3(wallX, wallH - 0.4f, -portalZ), new Vector3(wallT, 0.8f, portalDoorW + 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhufuValleyName + "_Walls", walls.Build(KhufuValleyName + "_Walls"), tura, true);

            var halls = new LabMeshBuilder(160, 240);
            Color stone = Color.white;
            float hallH = 6.4f;
            float hy = floorT + hallH * 0.5f;
            // Open court band + west antechambers toward causeway door.
            halls.AddRoom(new Vector3(7.0f, hy, 0f), new Vector3(28.0f, hallH, 30.0f), stone, false, false, true, true);
            halls.AddRoom(new Vector3(-14.0f, hy, 0f), new Vector3(16.0f, hallH, 14.0f), stone, false, false, true, true);
            halls.AddRoom(new Vector3(-14.0f, hy, 13.5f), new Vector3(12.0f, hallH * 0.92f, 9.0f), stone, false, false, false, true);
            halls.AddRoom(new Vector3(-14.0f, hy, -13.5f), new Vector3(12.0f, hallH * 0.92f, 9.0f), stone, false, false, false, true);
            GizaBuild.SpawnMesh(root.transform, KhufuValleyName + "_Halls", halls.Build(KhufuValleyName + "_Halls"), lime, true);

            var pillars = new LabMeshBuilder(96, 144);
            float ps = 1.05f;
            float ph = 5.8f;
            for (int row = 0; row < 2; row++)
            {
                float x = 2.5f + row * 9.0f;
                for (int i = 0; i < 6; i++)
                {
                    float z = Mathf.Lerp(-12.5f, 12.5f, i / 5f);
                    pillars.AddBox(new Vector3(x, floorT + ph * 0.5f, z), new Vector3(ps, ph, ps), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, KhufuValleyName + "_Pillars", pillars.Build(KhufuValleyName + "_Pillars"), gran, true);

            // East-facade portal vestibules (N/S) toward harbor.
            const float portalH = 4.8f;
            const float portalDepth = 3.0f;
            const float anteDepth = 3.0f;
            const float anteW = 4.0f;
            const float anteH = 4.6f;
            var portals = new LabMeshBuilder(96, 144);
            float portalHy = floorT + portalH * 0.5f;
            float anteHy = floorT + anteH * 0.5f;
            float vestibX = hx + portalDepth * 0.5f;
            float anteX = hx - wallT - anteDepth * 0.5f - 0.1f;
            portals.AddRoom(new Vector3(vestibX, portalHy, portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(vestibX, portalHy, -portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(anteX, anteHy, portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(anteX, anteHy, -portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            GizaBuild.SpawnMesh(root.transform, KhufuValleyName + "_Portals", portals.Build(KhufuValleyName + "_Portals"), gran, true);

            GizaBuild.HonestyPlate(root.transform, KhufuValleyName + "_Honesty", honesty, ns);
            Transform plate = root.transform.Find(KhufuValleyName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 4f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
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


        /// <summary>
        /// Walkable paved terrace in the gap between Sphinx temple (south) and Khafre valley temple (north).
        /// Facing doors on both temples open onto this court. Schematic Lehner twin-temple terrace.
        /// </summary>
        static GameObject BuildSphinxValleyLink(GizaComplex.Pose pose, Layout L, string honesty)
        {
            float southFace = L.sphinxTempleNorth - L.sphinxTempleNS * 0.5f;
            float northFace = L.valleyNorth + L.valleyNS * 0.5f;
            float gap = southFace - northFace;
            float midNorth = (southFace + northFace) * 0.5f;
            float ew = Mathf.Min(L.sphinxTempleEW, L.valleyEW) - 2f;
            Vector3 c = GizaComplex.WorldFromKhufu(pose, L.sphinxTempleEast, midNorth, 0f);
            GameObject root = GizaBuild.Root(SphinxValleyLinkName, pose.parent, c, pose.rot);
            Material pav = GizaBuild.Pavement();
            Material lime = GizaBuild.InteriorLime();
            Material gran = GizaBuild.Granite();

            const float floorT = 0.32f;
            float deckNS = Mathf.Max(6.5f, gap - 0.4f);
            float deckEW = Mathf.Max(18f, ew);

            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, 0f), new Vector3(deckEW, floorT, deckNS), Color.white);
            GizaBuild.SpawnMesh(root.transform, SphinxValleyLinkName + "_Floor", floor.Build(SphinxValleyLinkName + "_Floor"), pav, true);

            // Low side walls (east open toward harbor / dual portals, west toward Sphinx enclosure approach).
            const float wallH = 2.4f;
            const float wallT = 0.85f;
            float hx = deckEW * 0.5f;
            float hz = deckNS * 0.5f;
            float y = wallH * 0.5f;
            var walls = new LabMeshBuilder(32, 48);
            walls.AddBox(new Vector3(-hx + wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, deckNS), Color.white);
            // East stubs only — leave center open toward the twin east portal courts.
            walls.AddBox(new Vector3(hx - wallT * 0.5f, y, hz * 0.55f), new Vector3(wallT, wallH, deckNS * 0.35f), Color.white);
            walls.AddBox(new Vector3(hx - wallT * 0.5f, y, -hz * 0.55f), new Vector3(wallT, wallH, deckNS * 0.35f), Color.white);
            GizaBuild.SpawnMesh(root.transform, SphinxValleyLinkName + "_Walls", walls.Build(SphinxValleyLinkName + "_Walls"), lime, true);

            // Granite threshold pads at the facing doors.
            var pads = new LabMeshBuilder(16, 24);
            pads.AddBox(new Vector3(0f, floorT + 0.08f, hz - 0.35f), new Vector3(5.2f, 0.16f, 0.7f), Color.white);
            pads.AddBox(new Vector3(0f, floorT + 0.08f, -hz + 0.35f), new Vector3(5.2f, 0.16f, 0.7f), Color.white);
            GizaBuild.SpawnMesh(root.transform, SphinxValleyLinkName + "_Thresholds", pads.Build(SphinxValleyLinkName + "_Thresholds"), gran, true);

            GizaBuild.HonestyPlate(root.transform, SphinxValleyLinkName + "_Honesty", honesty, deckNS);
            Transform plate = root.transform.Find(SphinxValleyLinkName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 3.5f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        /// <summary>
        /// Limestone quarry enclosure around the Sphinx court. Walls rise from court floor
        /// toward the plateau lip (~SphinxCourtDropM). East open toward the Sphinx temple.
        /// Schematic Lehner ditch — not a scan.
        /// </summary>
        static GameObject BuildSphinxEnclosure(GizaComplex.Pose pose)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, GizaComplex.SphinxEastM, -GizaComplex.SphinxSouthM, 0f);
            GameObject root = GizaBuild.Root(SphinxEnclosureName, pose.parent, c, pose.rot);
            Material rock = GizaBuild.Bedrock();
            Material lime = GizaBuild.SphinxLime();
            Material pav = GizaBuild.Pavement();

            // Inner clear around Sphinx body; outer face meets the plateau cut.
            float halfE = GizaSphinx.LengthM * 0.5f + 14f; // ~50.75 m east/west from centre
            float halfN = GizaSphinx.WidthM * 0.5f + 18f;  // ~27.65 m north/south
            float wallH = GizaComplex.SphinxCourtDropM - 0.35f; // meet plateau lip
            float wallT = 2.6f;
            float ledgeH = 0.55f;
            float y = wallH * 0.5f;
            float hx = halfE;
            float hz = halfN;

            // Walkable court pavement strip inside the ditch (outside the Sphinx plinth).
            var floor = new LabMeshBuilder(16, 24);
            float floorT = 0.28f;
            float rimIn = 4.5f;
            floor.AddBox(new Vector3(0f, floorT * 0.5f, hz - rimIn * 0.5f), new Vector3(halfE * 2f - 2f, floorT, rimIn), Color.white);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, -(hz - rimIn * 0.5f)), new Vector3(halfE * 2f - 2f, floorT, rimIn), Color.white);
            floor.AddBox(new Vector3(-(hx - rimIn * 0.5f), floorT * 0.5f, 0f), new Vector3(rimIn, floorT, halfN * 2f - rimIn * 2f), Color.white);
            floor.AddBox(new Vector3(hx - rimIn * 0.5f - 1.5f, floorT * 0.5f, 0f), new Vector3(rimIn - 1f, floorT, halfN * 2f - rimIn * 2f - 8f), Color.white);
            GizaBuild.SpawnMesh(root.transform, SphinxEnclosureName + "_Floor", floor.Build(SphinxEnclosureName + "_Floor"), pav, true);

            // Quarry walls: N, S, W solid; east open toward Sphinx temple with door gap.
            var ditch = new LabMeshBuilder(96, 144);
            Color stone = Color.white;
            ditch.AddBox(new Vector3(0f, y, hz + wallT * 0.5f), new Vector3(halfE * 2f + wallT * 2f, wallH, wallT), stone);
            ditch.AddBox(new Vector3(0f, y, -(hz + wallT * 0.5f)), new Vector3(halfE * 2f + wallT * 2f, wallH, wallT), stone);
            ditch.AddBox(new Vector3(-(hx + wallT * 0.5f), y, 0f), new Vector3(wallT, wallH, halfN * 2f + wallT * 2f), stone);
            WallDoorX(ditch, hx + wallT * 0.5f, halfN * 2f + wallT * 2f, wallH, wallT, 18f);
            // Inner ledge / working berm at mid-height (schematic quarry shelf).
            float shelfY = 3.2f;
            ditch.AddBox(new Vector3(0f, shelfY, hz - 0.9f), new Vector3(halfE * 2f - 2f, ledgeH, 1.6f), stone);
            ditch.AddBox(new Vector3(0f, shelfY, -(hz - 0.9f)), new Vector3(halfE * 2f - 2f, ledgeH, 1.6f), stone);
            ditch.AddBox(new Vector3(-(hx - 0.9f), shelfY, 0f), new Vector3(1.6f, ledgeH, halfN * 2f - 4f), stone);
            // Plateau lip coping on top of walls.
            float copeY = wallH + 0.2f;
            ditch.AddBox(new Vector3(0f, copeY, hz + wallT * 0.5f), new Vector3(halfE * 2f + wallT * 2f + 0.8f, 0.4f, wallT + 0.6f), stone);
            ditch.AddBox(new Vector3(0f, copeY, -(hz + wallT * 0.5f)), new Vector3(halfE * 2f + wallT * 2f + 0.8f, 0.4f, wallT + 0.6f), stone);
            ditch.AddBox(new Vector3(-(hx + wallT * 0.5f), copeY, 0f), new Vector3(wallT + 0.6f, 0.4f, halfN * 2f + wallT * 2f + 0.8f), stone);
            GizaBuild.SpawnMesh(root.transform, SphinxEnclosureName + "_Ditch", ditch.Build(SphinxEnclosureName + "_Ditch"), rock, true);

            // Soft inner face lining so the ditch reads as cut bedrock.
            var lining = new LabMeshBuilder(48, 72);
            float linT = 0.45f;
            float linH = wallH - 0.8f;
            float ly = linH * 0.5f;
            lining.AddBox(new Vector3(0f, ly, hz - linT * 0.5f - 0.05f), new Vector3(halfE * 2f - 1f, linH, linT), stone);
            lining.AddBox(new Vector3(0f, ly, -(hz - linT * 0.5f - 0.05f)), new Vector3(halfE * 2f - 1f, linH, linT), stone);
            lining.AddBox(new Vector3(-(hx - linT * 0.5f - 0.05f), ly, 0f), new Vector3(linT, linH, halfN * 2f - 1f), stone);
            GizaBuild.SpawnMesh(root.transform, SphinxEnclosureName + "_Lining", lining.Build(SphinxEnclosureName + "_Lining"), lime, true);

            string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Sphinx enclosure. Rock-cut quarry ditch around the Sphinx court (~12 m below plateau). Limestone walls N/S/W; east open toward the Sphinx temple.\n" +
                "Schematic Lehner ditch massing (walkable floor bands + mid-height shelf). Not photogrammetry. Not the sand-filled modern ruin.";
            GizaBuild.HonestyPlate(root.transform, SphinxEnclosureName + "_Honesty", honesty, halfN);
            Transform plate = root.transform.Find(SphinxEnclosureName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 6f, 1.55f, hz * 0.35f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
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
