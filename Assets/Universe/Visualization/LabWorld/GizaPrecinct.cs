using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Rest of the undamaged Giza necropolis: Khufu queens G1a-c, Khufu cult/satellite G1-d, Khafre cult/satellite G2-a,
    /// mortuary temples, causeways, Khufu + Khafre boat pits, Khufu Trial Passages, Khufu/Khafre/Menkaure valley temples, Sphinx temple,
    /// Sphinx-Khafre link court, temenos walls.
    /// Reconstructed original massing from published plans (Lehner/Petrie). Not photogrammetry.
    /// Architectural local space: +X east, +Z north, 1 unit = 1 m.
    /// </summary>
    public static class GizaPrecinct
    {
        public const string G1aName = "G1a";
        public const string G1bName = "G1b";
        public const string G1cName = "G1c";
        public const string G1dName = "G1d";
        public const string G2aName = "G2a";
        public const string KhufuMortuaryName = "KhufuMortuary";
        public const string KhufuCausewayName = "KhufuCauseway";
        public const string KhufuValleyName = "KhufuValleyTemple";
        public const string KhufuBoatPitsName = "KhufuBoatPits";
        public const string KhufuTrialPassagesName = "KhufuTrialPassages";
        public const string KhafreBoatPitsName = "KhafreBoatPits";
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
            public float g2aBase, g2aHeight, g2aEast, g2aNorth;
            public float g1dBase, g1dHeight, g1dEast, g1dNorth;
            public float khufuTempleEW, khufuTempleNS, khufuTempleEast;
            public float khufuCauseStartEast, khufuCauseEndEast, khufuCauseLen, khufuCauseWid;
            public float khufuValleyEW, khufuValleyNS, khufuValleyEast, khufuValleyNorth;
            public float boatNorth, boatLen, boatWid;
            public float trialEast, trialNorth, trialLen, trialWid;
            public float khafreBoatEast, khafreBoatNorth, khafreBoatLen, khafreBoatWid;
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

            // Khufu cult / satellite G1-d: SE of Khufu SE corner, west of queens (Lehner).
            L.g1dBase = 21.75f;
            L.g1dHeight = 13.2f;
            L.g1dEast = kh + khPav + 10f + L.g1dBase * 0.5f;
            L.g1dNorth = -(kh + khPav + 10f + L.g1dBase * 0.5f);

            L.boatNorth = -(kh + khPav + 4f + L.boatWid * 0.5f);

            // Khufu Trial Passages: unfinished rock-cut practice corridors south of Khufu (Lehner/Petrie schematic).
            // Sit west of centreline and south of the south boat-pit row so cuttings do not collide.
            L.trialLen = 30f;
            L.trialWid = 1.35f;
            L.trialEast = -42f;
            L.trialNorth = L.boatNorth - L.boatWid * 0.5f - 16f;

            float hf = KhafrePyramid.BaseMeters * 0.5f;
            L.khafreTempleEast = -GizaComplex.KhafreWestM + hf + 5f + 2f + L.khafreTempleEW * 0.5f;
            L.khafreTempleNorth = -GizaComplex.KhafreSouthM;
            L.khafreCauseStartEast = L.khafreTempleEast + L.khafreTempleEW * 0.5f;
            L.khafreCauseStartNorth = L.khafreTempleNorth;

            // Khafre cult / satellite G2-a: south of Khafre, slight SE bias (Lehner).
            L.g2aBase = 21f;
            L.g2aHeight = 12.5f;
            float khafrePav = 5f; // match KhafrePyramid pavement ring width in Build
            L.g2aEast = -GizaComplex.KhafreWestM + 18f; // slight SE of centerline
            L.g2aNorth = -GizaComplex.KhafreSouthM - hf - khafrePav - 4f - L.g2aBase * 0.5f;

            // Khafre boat pits: two rock-cut cuttings east of G2a on the south terrace (Lehner schematic).
            L.khafreBoatLen = 42f;
            L.khafreBoatWid = 6.5f;
            const float khafreBoatGap = 6f;
            float khafreBoatSpan = 2f * L.khafreBoatLen + khafreBoatGap;
            L.khafreBoatEast = L.g2aEast + L.g2aBase * 0.5f + 10f + khafreBoatSpan * 0.5f;
            L.khafreBoatNorth = L.g2aNorth; // same south terrace band as G2-a

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
            // +16 m east pad covers queen east chapels beyond pavement rings.
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g1aEast, L.g1aNorth, L.g1aBase * 0.5f + 16f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g1bEast, L.g1bNorth, L.g1bBase * 0.5f + 16f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g1cEast, L.g1cNorth, L.g1cBase * 0.5f + 16f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g1dEast, L.g1dNorth, L.g1dBase * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.khufuTempleEast, 0f, L.khufuTempleEW * 0.5f + 2f, L.khufuTempleNS * 0.5f + 2f);
            // Cliff-lip pad east of mortuary/queens — do not pull the plateau under the full valley causeway run.
            float khufuLipEast = Mathf.Max(
                L.khufuTempleEast + L.khufuTempleEW * 0.5f + 10f,
                L.g1aEast + L.g1aBase * 0.5f + 10f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, khufuLipEast, 0f, 12f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, 0f, L.boatNorth, L.boatLen * 2.5f + 20f, L.boatWid * 0.5f + 12f);
            // Trial Passages south pad (mouth at trialNorth, corridor runs further south).
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.trialEast, L.trialNorth - L.trialLen * 0.5f,
                L.trialWid * 0.5f + 14f, L.trialLen * 0.5f + 18f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.khafreTempleEast, L.khafreTempleNorth, L.khafreTempleEW * 0.5f + 2f, L.khafreTempleNS * 0.5f + 2f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.g2aEast, L.g2aNorth, L.g2aBase * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.khafreBoatEast, L.khafreBoatNorth,
                L.khafreBoatLen + 16f, L.khafreBoatWid * 0.5f + 12f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.valleyEast, L.valleyNorth, L.valleyEW * 0.5f + 4f, L.valleyNS * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.sphinxTempleEast, L.sphinxTempleNorth, L.sphinxTempleEW * 0.5f + 4f, L.sphinxTempleNS * 0.5f + 4f);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, L.menkaureTempleEast, L.menkaureTempleNorth, L.menkaureTempleEW * 0.5f + 2f, L.menkaureTempleNS * 0.5f + 2f);
            // Menkaure queens G3a-c south of Menkaure + east chapel pad (mirror G1).
            float g3South = -GizaComplex.MenkaureSouthM - MenkaurePyramid.BaseMeters * 0.5f - 8f - MenkaurePyramid.QueenBaseM * 0.5f;
            float g3Half = MenkaurePyramid.QueenBaseM * 0.5f + 16f;
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, -GizaComplex.MenkaureWestM + 32f, g3South, g3Half);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, -GizaComplex.MenkaureWestM, g3South, g3Half);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, -GizaComplex.MenkaureWestM - 32f, g3South, g3Half);
            // Cliff-lip pad east of Menkaure mortuary — do not pull the plateau under the valley descent.
            float menLipEast = L.menkaureTempleEast + L.menkaureTempleEW * 0.5f + 10f;
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, menLipEast, L.menkaureTempleNorth, 12f);
            GizaField.ExpandExtents(ref xMin, ref xMax, ref zMin, ref zMax);
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
                "East chapels: small walkable limestone shells (~9.5 x 7.5 m) east of each queen with west door from pavement (Lehner schematic). Not pyramid interiors. Not photogrammetry. Not the stripped modern ruin.";
            const string templeHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khufu mortuary temple. Immediately east of Khufu, between the pyramid and the queens. ~52 x 40 m limestone open court + pillared colonnade (Lehner).\n" +
                "Walkable: west door from pyramid pavement, antechamber, open court with pillars, east door into the covered causeway. Complete walls (not today's stubs). Not photogrammetry.";
            // Force rebuild when east chapel marker missing (casing-only queens).
            GameObject oldG1a = GizaComplex.FindNamed(G1aName);
            if (oldG1a != null && oldG1a.transform.Find(G1aName + "_Chapel") == null)
                DestroyNamed(oldG1a);
            GameObject oldG1b = GizaComplex.FindNamed(G1bName);
            if (oldG1b != null && oldG1b.transform.Find(G1bName + "_Chapel") == null)
                DestroyNamed(oldG1b);
            GameObject oldG1c = GizaComplex.FindNamed(G1cName);
            if (oldG1c != null && oldG1c.transform.Find(G1cName + "_Chapel") == null)
                DestroyNamed(oldG1c);
            Ensure(G1aName, pose, p => BuildQueen(p, G1aName, L.g1aEast, L.g1aNorth, L.g1aBase, L.g1aHeight, queensHonesty), pose.surfaceY, true);
            Ensure(G1bName, pose, p => BuildQueen(p, G1bName, L.g1bEast, L.g1bNorth, L.g1bBase, L.g1bHeight, null), pose.surfaceY, true);
            Ensure(G1cName, pose, p => BuildQueen(p, G1cName, L.g1cEast, L.g1cNorth, L.g1cBase, L.g1cHeight, null), pose.surfaceY, true);
            const string g1dHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khufu cult / satellite pyramid G1-d. SE of Khufu (SE corner). Tura casing ON, electrum pyramidion (reconstructed).\n" +
                "Base ~21.75 m / height ~13.2 m (Lehner schematic). Not a queen pyramid. Not photogrammetry.";
            GameObject oldG1d = GizaComplex.FindNamed(G1dName);
            if (oldG1d != null && oldG1d.transform.Find(G1dName + "_Casing") == null)
                DestroyNamed(oldG1d);
            Ensure(G1dName, pose, p => BuildQueen(p, G1dName, L.g1dEast, L.g1dNorth, L.g1dBase, L.g1dHeight, g1dHonesty), pose.surfaceY, true);
            // Force rebuild when open-court / interior markers missing (replaces closed box massing).
            GameObject oldKhufuMort = GizaComplex.FindNamed(KhufuMortuaryName);
            if (oldKhufuMort != null && (oldKhufuMort.transform.Find(KhufuMortuaryName + "_Court") == null
                || oldKhufuMort.transform.Find(KhufuMortuaryName + "_Interior") == null))
                DestroyNamed(oldKhufuMort);
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
            // Force rebuild when rock-cut cutting marker missing (replaces surface-lined basins).
            GameObject oldBoats = GizaComplex.FindNamed(KhufuBoatPitsName);
            if (oldBoats != null && oldBoats.transform.Find(KhufuBoatPitsName + "_Cutting") == null)
                DestroyNamed(oldBoats);
            Ensure(KhufuBoatPitsName, pose, p => BuildBoatPits(p, L), pose.surfaceY, true);
            // Force rebuild when Trial Passages cutting marker missing.
            GameObject oldTrials = GizaComplex.FindNamed(KhufuTrialPassagesName);
            if (oldTrials != null && oldTrials.transform.Find(KhufuTrialPassagesName + "_Cutting") == null)
                DestroyNamed(oldTrials);
            Ensure(KhufuTrialPassagesName, pose, p => BuildKhufuTrialPassages(p, L), pose.surfaceY, true);
            Ensure(KhufuEnclosureName, pose, p => BuildEnclosure(p, KhufuEnclosureName, pose.khufuCenter, 0f, KhufuPyramid.BaseMeters * 0.5f + KhufuPyramid.PavementWidthM, L.khufuTempleNS + 4f, true), pose.surfaceY, true);
            GizaField.EnsureWestField(pose);
            GizaField.EnsureGisrElMudir(pose);
            GizaField.EnsureHemiunu(pose);
            GizaField.EnsureSenedjemib(pose);
            GizaField.EnsureCemeteryEnEchelon(pose);
            GizaField.EnsureEastField(pose);
        }

        public static void EnsureKhafre(GizaComplex.Pose pose)
        {
            Layout L = Compute();
            float terrace = pose.surfaceY + GizaComplex.KhafreBedrockM;
            const string mortHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khafre mortuary temple. Immediately east of Khafre. Open limestone court, granite pillars, complete walls (Lehner).\n" +
                "Five west-range statue niches with empty pedestals (Lehner; colossi missing). Schematic not photogrammetry.\n" +
                "Walkable: west door from pyramid pavement, antechamber, open court with granite colonnade, east door into the covered causeway to the valley temple.";
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

            GameObject oldKhafreMort = GizaComplex.FindNamed(KhafreMortuaryName);
            if (oldKhafreMort != null && (oldKhafreMort.transform.Find(KhafreMortuaryName + "_Court") == null
                || oldKhafreMort.transform.Find(KhafreMortuaryName + "_Interior") == null
                || oldKhafreMort.transform.Find(KhafreMortuaryName + "_Niches") == null))
                DestroyNamed(oldKhafreMort);
            Ensure(KhafreMortuaryName, pose, p => BuildMortuary(p, KhafreMortuaryName, L.khafreTempleEast, L.khafreTempleNorth, GizaComplex.KhafreBedrockM, L.khafreTempleEW, L.khafreTempleNS, true, mortHonesty), terrace, true);
            Ensure(KhafreValleyName, pose, p => BuildValleyTemple(p, L, valleyHonesty), GizaComplex.CourtY(pose), true);
            Ensure(KhafreCausewayName, pose, p => BuildCauseway(p, KhafreCausewayName, L.khafreCauseStartEast, L.khafreCauseStartNorth, L.khafreCauseEndEast, L.khafreCauseEndNorth, terrace, GizaComplex.CourtY(pose), 10f), pose.surfaceY, false);
            Vector3 khafre = GizaComplex.WorldFromKhufu(pose, -GizaComplex.KhafreWestM, -GizaComplex.KhafreSouthM, GizaComplex.KhafreBedrockM);
            Ensure(KhafreEnclosureName, pose, p => BuildEnclosure(p, KhafreEnclosureName, khafre, GizaComplex.KhafreBedrockM, KhafrePyramid.BaseMeters * 0.5f + 5f, L.khafreTempleNS + 4f, false), terrace, true);

            // Khafre cult / satellite G2-a (south of Khafre, SE bias). Force rebuild if casing marker missing.
            const string g2aHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khafre cult / satellite pyramid G2-a. South of Khafre (SE bias). Tura casing ON, electrum pyramidion (reconstructed).\n" +
                "Base ~21 m, height ~12.5 m schematic (Lehner). No interior. Not photogrammetry.";
            GameObject oldG2a = GizaComplex.FindNamed(G2aName);
            if (oldG2a != null && oldG2a.transform.Find(G2aName + "_Casing") == null)
                DestroyNamed(oldG2a);
            Ensure(G2aName, pose, p => BuildQueen(p, G2aName, L.g2aEast, L.g2aNorth, L.g2aBase, L.g2aHeight, g2aHonesty), terrace, true);

            // Khafre boat pits east of G2-a. Force rebuild when rock-cut cutting marker missing.
            GameObject oldKhafreBoats = GizaComplex.FindNamed(KhafreBoatPitsName);
            if (oldKhafreBoats != null && oldKhafreBoats.transform.Find(KhafreBoatPitsName + "_Cutting") == null)
                DestroyNamed(oldKhafreBoats);
            Ensure(KhafreBoatPitsName, pose, p => BuildKhafreBoatPits(p, L), terrace, true);

            GizaField.EnsureCentralField(pose);
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
            GizaField.EnsureOsirisShaft(pose);
        }

        public static void EnsureMenkaure(GizaComplex.Pose pose)
        {
            Layout L = Compute();
            float floodY = pose.surfaceY - GizaComplex.CliffHeightM + GizaNile.SitAboveDesertM;
            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Menkaure mortuary temple. Immediately east of Menkaure. Open limestone court, complete walls (reconstructed; historically unfinished granite conversion — Lehner).\n" +
                "Walkable limestone interior: west door from pyramid pavement, antechamber, open court with pillars, east door into the covered causeway. Queens G3a-c already sit south of Menkaure — not duplicated. Not photogrammetry.";
            const string valleyHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Menkaure valley temple. Floodplain-level mudbrick / limestone massing east of the cliff (Lehner plan scale ~44 x 47 m).\n" +
                "Dual north/south east entrances (vestibule portals) toward the harbor — twin pattern with Khufu valley. Reconstructed complete shell; historically unfinished in stone then finished in mudbrick (Shepseskaf). Walkable court + antechambers. Not photogrammetry.";

            GameObject oldMenMort = GizaComplex.FindNamed(MenkaureMortuaryName);
            if (oldMenMort != null && (oldMenMort.transform.Find(MenkaureMortuaryName + "_Court") == null
                || oldMenMort.transform.Find(MenkaureMortuaryName + "_Interior") == null))
                DestroyNamed(oldMenMort);
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
            if (oldValley != null && (oldValley.transform.Find(MenkaureValleyName + "_Halls") == null
                || oldValley.transform.Find(MenkaureValleyName + "_Portals") == null))
                DestroyNamed(oldValley);

            Ensure(MenkaureCausewayName, pose, p => BuildCauseway(p, MenkaureCausewayName, L.menCauseStartEast, L.menkaureTempleNorth, L.menCauseEndEast, L.menCauseEndNorth, pose.surfaceY, floodY, 8f), pose.surfaceY, false);
            Ensure(MenkaureValleyName, pose, p => BuildMenkaureValleyTemple(p, L, valleyHonesty), floodY, true);
            Vector3 men = GizaComplex.WorldFromKhufu(pose, -GizaComplex.MenkaureWestM, -GizaComplex.MenkaureSouthM, 0f);
            Ensure(MenkaureEnclosureName, pose, p => BuildEnclosure(p, MenkaureEnclosureName, men, 0f, MenkaurePyramid.BaseMeters * 0.5f + 4f, L.menkaureTempleNS + 4f, false), pose.surfaceY, true);
            GizaField.EnsureWorkersVillage(pose);
            GizaField.EnsureKhentkawes(pose);
            GizaField.EnsureMenkaureField(pose);
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
            Material lime = GizaBuild.InteriorLime();
            GizaBuild.Casing(root.transform, name + "_Casing", baseM, heightM, tura, false, 0f, 0f, 0f, 0f, 0.5f);
            GizaBuild.Pyramidion(root.transform, name + "_Pyramidion", baseM, heightM, 0.5f, gold);
            GizaBuild.PavementRing(root.transform, name + "_Pavement", baseM, 3f, pav);
            // G1 queens only: east chapel shells. G1d/G2a cult pyramids stay casing-only.
            if (name == G1aName || name == G1bName || name == G1cName)
                BuildQueenEastChapel(root.transform, name, baseM, lime, tura, pav);
            if (!string.IsNullOrEmpty(honesty))
                GizaBuild.HonestyPlate(root.transform, name + "_Honesty", honesty, baseM);
            return root;
        }

        /// <summary>
        /// Small east chapel east of a queen pyramid (Lehner schematic). Walkable VR headroom; west door from pavement.
        /// Marker: name_Chapel (force-rebuild).
        /// </summary>
        public static void BuildQueenEastChapel(Transform parent, string name, float baseM, Material lime, Material tura, Material pav)
        {
            const float chapelEW = 9.5f;
            const float chapelNS = 7.5f;
            const float gap = 3.5f; // past 3 m pavement ring
            const float wallH = 4.2f;
            const float wallT = 0.95f;
            const float floorT = 0.30f;
            const float doorW = 3.2f;
            float hx = chapelEW * 0.5f;
            float hz = chapelNS * 0.5f;
            float cx = baseM * 0.5f + gap + hx;
            float y = wallH * 0.5f;

            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(cx, floorT * 0.5f, 0f), new Vector3(chapelEW, floorT, chapelNS), Color.white);
            GizaBuild.SpawnMesh(parent, name + "_ChapelFloor", floor.Build(name + "_ChapelFloor"), pav, true);

            var walls = new LabMeshBuilder(48, 72);
            walls.AddBox(new Vector3(cx, y, hz - wallT * 0.5f), new Vector3(chapelEW, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(cx, y, -hz + wallT * 0.5f), new Vector3(chapelEW, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(cx + hx - wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, chapelNS), Color.white);
            WallDoorX(walls, cx - hx + wallT * 0.5f, chapelNS, wallH, wallT, doorW);
            GizaBuild.SpawnMesh(parent, name + "_Chapel", walls.Build(name + "_Chapel"), tura, true);

            // Interior sanctum open west (door) — lime face so chapel reads as room not solid mass.
            float anteEW = chapelEW - wallT * 2f - 0.4f;
            float anteNS = chapelNS - wallT * 2f - 0.6f;
            float anteH = 3.6f;
            var interior = new LabMeshBuilder(40, 60);
            interior.AddRoom(new Vector3(cx, floorT + anteH * 0.5f, 0f), new Vector3(anteEW, anteH, anteNS), Color.white, false, false, true, false);
            GizaBuild.SpawnMesh(parent, name + "_ChapelInterior", interior.Build(name + "_ChapelInterior"), lime, true);

            // Short approach apron from pavement ring to chapel door.
            float apronEW = gap - 0.4f;
            float apronX = baseM * 0.5f + apronEW * 0.5f + 0.15f;
            var apron = new LabMeshBuilder(8, 12);
            apron.AddBox(new Vector3(apronX, floorT * 0.5f, 0f), new Vector3(apronEW, floorT, doorW + 1.2f), Color.white);
            GizaBuild.SpawnMesh(parent, name + "_ChapelApron", apron.Build(name + "_ChapelApron"), pav, true);
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
            Material pillarMat = granitePillars ? gran : tura;

            // Open-court shell: VR headroom well above 3 m; sky-open court (no roof).
            const float wallH = 5.6f;
            const float wallT = 1.25f;
            const float floorT = 0.35f;
            const float westDoorW = 6.5f;
            const float eastDoorW = 8.0f;
            float hx = ew * 0.5f;
            float hz = ns * 0.5f;
            float y = wallH * 0.5f;

            // Full footprint pavement (MeshCollider + TeleportationArea via Place Giza Complex).
            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, floorT * 0.5f, 0f), new Vector3(ew, floorT, ns), Color.white);
            GizaBuild.SpawnMesh(root.transform, name + "_Floor", floor.Build(name + "_Floor"), pav, true);

            // West range depth (antechamber + sanctum) then open court to the east.
            float westRange = Mathf.Clamp(ew * 0.30f, 9.5f, 13.5f);
            float courtPad = 0.6f;
            float courtEW = ew - wallT * 2f - westRange - courtPad;
            float courtNS = ns - wallT * 2f - 2.4f;
            float courtX = -hx + wallT + westRange + courtPad + courtEW * 0.5f;
            var court = new LabMeshBuilder(8, 12);
            court.AddBox(new Vector3(courtX, floorT * 0.5f + 0.04f, 0f), new Vector3(courtEW, floorT + 0.08f, courtNS), Color.white);
            GizaBuild.SpawnMesh(root.transform, name + "_Court", court.Build(name + "_Court"), pav, true);

            // Perimeter: solid N/S; west door toward pyramid pavement; east door into causeway.
            var walls = new LabMeshBuilder(96, 144);
            walls.AddBox(new Vector3(0f, y, hz - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(0f, y, -hz + wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            WallDoorX(walls, -hx + wallT * 0.5f, ns, wallH, wallT, westDoorW);
            WallDoorX(walls, hx - wallT * 0.5f, ns, wallH, wallT, eastDoorW);
            GizaBuild.SpawnMesh(root.transform, name + "_Walls", walls.Build(name + "_Walls"), tura, true);

            // Interior: west vestibule/antechamber on axis + flanking sanctum rooms (Ensure marker).
            float anteH = 4.8f;
            float anteEW = Mathf.Min(7.2f, westRange - 1.6f);
            float anteNS = Mathf.Min(9.5f, ns * 0.28f);
            float anteX = -hx + wallT + anteEW * 0.5f + 0.35f;
            float anteY = floorT + anteH * 0.5f;
            float sanctEW = anteEW * 0.92f;
            float sanctNS = Mathf.Min(8.5f, (ns - anteNS) * 0.38f);
            float sanctH = 4.6f;
            float sanctY = floorT + sanctH * 0.5f;
            float sanctZ = anteNS * 0.5f + sanctNS * 0.5f + 0.55f;
            var interior = new LabMeshBuilder(160, 240);
            Color stone = Color.white;
            // Vestibule open west (pyramid door) and east (into court).
            interior.AddRoom(new Vector3(anteX, anteY, 0f), new Vector3(anteEW, anteH, anteNS), stone, false, false, true, true);
            // Flanking sancta open east toward the court.
            interior.AddRoom(new Vector3(anteX, sanctY, sanctZ), new Vector3(sanctEW, sanctH, sanctNS), stone, false, false, false, true);
            interior.AddRoom(new Vector3(anteX, sanctY, -sanctZ), new Vector3(sanctEW, sanctH, sanctNS), stone, false, false, false, true);
            GizaBuild.SpawnMesh(root.transform, name + "_Interior", interior.Build(name + "_Interior"), lime, true);

            // Colonnade framing the open court (two N-S rows; processional aisle on axis).
            float ps = granitePillars ? 1.15f : 1.0f;
            float ph = 4.8f;
            float py = floorT + ph * 0.5f;
            float aisle = Mathf.Max(4.5f, courtEW * 0.18f);
            float rowX0 = courtX - aisle * 0.5f - ps * 0.5f;
            float rowX1 = courtX + aisle * 0.5f + ps * 0.5f;
            // Keep a second pair of rows toward the east if court is deep enough.
            bool deep = courtEW > 16f;
            float rowX2 = courtX + courtEW * 0.28f;
            float rowX3 = courtX - courtEW * 0.28f;
            int nz = Mathf.Clamp(Mathf.RoundToInt(courtNS / 9.5f), 4, 6);
            float z0 = -courtNS * 0.38f;
            float z1 = courtNS * 0.38f;
            var pillars = new LabMeshBuilder(128, 192);
            for (int j = 0; j < nz; j++)
            {
                float v = nz == 1 ? 0.5f : j / (float)(nz - 1);
                float pz = Mathf.Lerp(z0, z1, v);
                pillars.AddBox(new Vector3(rowX0, py, pz), new Vector3(ps, ph, ps), Color.white);
                pillars.AddBox(new Vector3(rowX1, py, pz), new Vector3(ps, ph, ps), Color.white);
                if (deep)
                {
                    pillars.AddBox(new Vector3(rowX2, py, pz), new Vector3(ps, ph, ps), Color.white);
                    pillars.AddBox(new Vector3(rowX3, py, pz), new Vector3(ps, ph, ps), Color.white);
                }
            }
            GizaBuild.SpawnMesh(root.transform, name + "_Pillars", pillars.Build(name + "_Pillars"), pillarMat, true);

            // Light architraves atop colonnade rows (still open court — no continuous roof).
            const float beamH = 0.5f;
            const float beamW = 1.2f;
            float beamY = floorT + ph + beamH * 0.5f;
            float spanZ = (z1 - z0) + ps + 0.35f;
            var arch = new LabMeshBuilder(48, 72);
            arch.AddBox(new Vector3(rowX0, beamY, 0f), new Vector3(beamW, beamH, spanZ), Color.white);
            arch.AddBox(new Vector3(rowX1, beamY, 0f), new Vector3(beamW, beamH, spanZ), Color.white);
            if (deep)
            {
                arch.AddBox(new Vector3(rowX2, beamY, 0f), new Vector3(beamW, beamH, spanZ), Color.white);
                arch.AddBox(new Vector3(rowX3, beamY, 0f), new Vector3(beamW, beamH, spanZ), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, name + "_Architraves", arch.Build(name + "_Architraves"), pillarMat, true);

            // Five west-range statue niches (Khafre only): empty pedestals, Lehner distinctive feature.
            if (granitePillars)
            {
                var niches = new LabMeshBuilder(80, 120);
                float nicheD = 1.8f;
                float nicheW = 2.6f;
                float nicheH = 4.5f;
                float nicheY = floorT + nicheH * 0.5f;
                float pedH = 0.9f;
                float pedY = floorT + pedH * 0.5f;
                // West face of open court (east face of west range) — recesses open east into court.
                float courtWest = courtX - courtEW * 0.5f;
                float nicheX = courtWest + nicheD * 0.5f + 0.12f;
                float nicheZ0 = -courtNS * 0.36f;
                float nicheZ1 = courtNS * 0.36f;
                for (int i = 0; i < 5; i++)
                {
                    float nz = Mathf.Lerp(nicheZ0, nicheZ1, i / 4f);
                    niches.AddRoom(new Vector3(nicheX, nicheY, nz), new Vector3(nicheD, nicheH, nicheW), Color.white, false, false, false, true);
                    niches.AddBox(new Vector3(nicheX, pedY, nz), new Vector3(nicheD * 0.55f, pedH, nicheW * 0.72f), Color.white);
                }
                GizaBuild.SpawnMesh(root.transform, name + "_Niches", niches.Build(name + "_Niches"), gran, true);
            }

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

            // Dual east facade doors (N/S) toward harbor — twin pattern with Khufu valley.
            const float portalDoorW = 3.2f;
            const float portalZ = 8.5f;
            var walls = new LabMeshBuilder(80, 120);
            walls.AddBox(new Vector3(0f, y, hz - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(0f, y, -hz + wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            WallDoorX(walls, -hx + wallT * 0.5f, ns, wallH, wallT, 6.0f);
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

            // East-facade portal vestibules (N/S) toward harbor.
            const float portalH = 4.6f;
            const float portalDepth = 2.8f;
            const float anteDepth = 2.8f;
            const float anteW = 3.8f;
            const float anteH = 4.4f;
            Material gran = GizaBuild.Granite();
            var portals = new LabMeshBuilder(96, 144);
            float portalHy = floorT + portalH * 0.5f;
            float anteHy = floorT + anteH * 0.5f;
            float vestibX = hx + portalDepth * 0.5f;
            float anteX = hx - wallT - anteDepth * 0.5f - 0.1f;
            portals.AddRoom(new Vector3(vestibX, portalHy, portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(vestibX, portalHy, -portalZ), new Vector3(portalDepth, portalH, portalDoorW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(anteX, anteHy, portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            portals.AddRoom(new Vector3(anteX, anteHy, -portalZ), new Vector3(anteDepth, anteH, anteW), Color.white, false, false, true, true);
            GizaBuild.SpawnMesh(root.transform, MenkaureValleyName + "_Portals", portals.Build(MenkaureValleyName + "_Portals"), gran, true);

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
            Material rock = GizaBuild.Bedrock();
            Material lime = GizaBuild.InteriorLime();
            Material pav = GizaBuild.Pavement();
            Material tura = GizaBuild.TuraCasing();

            // Schematic rock-cut depth: walkable rim + descend into one pit with headroom (open top).
            const float depth = 3.6f;
            const float wallT = 1.15f;
            const float floorT = 0.4f;
            const float rimH = 0.55f;
            const float rimT = 0.9f;
            const float gap = 8f;
            const float liningT = 0.35f;
            int n = 5;
            float span = n * L.boatLen + (n - 1) * gap;
            float x0 = -span * 0.5f + L.boatLen * 0.5f;
            int descendIndex = 2; // middle pit: stairs from south rim
            int coverIndex = 3;   // limestone cover-slab remnants (schematic)
            float z = L.boatNorth;
            float halfL = L.boatLen * 0.5f;
            float halfW = L.boatWid * 0.5f;

            var cutting = new LabMeshBuilder(320, 480);
            var lining = new LabMeshBuilder(160, 240);
            var rimPav = new LabMeshBuilder(120, 180);
            var stairs = new LabMeshBuilder(96, 144);
            var covers = new LabMeshBuilder(48, 72);
            Color stone = Color.white;

            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * (L.boatLen + gap);
                // Bedrock floor of cutting.
                cutting.AddBox(new Vector3(x, -depth + floorT * 0.5f, z),
                    new Vector3(L.boatLen - wallT * 2f, floorT, L.boatWid - wallT * 2f), stone);
                // Four rock-cut walls (hollow rectangular cutting into plateau limestone).
                float wy = -depth * 0.5f;
                cutting.AddBox(new Vector3(x, wy, z + halfW - wallT * 0.5f),
                    new Vector3(L.boatLen, depth, wallT), stone);
                // South wall: full length, or stair jambs with open gap on the descend pit.
                float southZ = z - (halfW - wallT * 0.5f);
                if (i == descendIndex)
                {
                    const float doorW = 3.6f;
                    float remain = (L.boatLen - doorW) * 0.5f;
                    if (remain > 0.3f)
                    {
                        float xOff = (doorW + remain) * 0.5f;
                        cutting.AddBox(new Vector3(x + xOff, wy, southZ),
                            new Vector3(remain, depth, wallT), stone);
                        cutting.AddBox(new Vector3(x - xOff, wy, southZ),
                            new Vector3(remain, depth, wallT), stone);
                    }
                }
                else
                {
                    cutting.AddBox(new Vector3(x, wy, southZ),
                        new Vector3(L.boatLen, depth, wallT), stone);
                }
                cutting.AddBox(new Vector3(x + halfL - wallT * 0.5f, wy, z),
                    new Vector3(wallT, depth, L.boatWid - wallT * 2f), stone);
                cutting.AddBox(new Vector3(x - (halfL - wallT * 0.5f), wy, z),
                    new Vector3(wallT, depth, L.boatWid - wallT * 2f), stone);
                // Surface rim coping (south rim opens at stair gap on descend pit).
                cutting.AddBox(new Vector3(x, rimH * 0.5f, z + halfW + rimT * 0.5f),
                    new Vector3(L.boatLen + rimT * 2f, rimH, rimT), stone);
                float southRimZ = z - (halfW + rimT * 0.5f);
                if (i == descendIndex)
                {
                    const float doorW = 3.6f;
                    float remain = (L.boatLen - doorW) * 0.5f;
                    if (remain > 0.3f)
                    {
                        float xOff = (doorW + remain) * 0.5f;
                        cutting.AddBox(new Vector3(x + xOff, rimH * 0.5f, southRimZ),
                            new Vector3(remain + rimT, rimH, rimT), stone);
                        cutting.AddBox(new Vector3(x - xOff, rimH * 0.5f, southRimZ),
                            new Vector3(remain + rimT, rimH, rimT), stone);
                    }
                }
                else
                {
                    cutting.AddBox(new Vector3(x, rimH * 0.5f, southRimZ),
                        new Vector3(L.boatLen + rimT * 2f, rimH, rimT), stone);
                }
                cutting.AddBox(new Vector3(x + halfL + rimT * 0.5f, rimH * 0.5f, z),
                    new Vector3(rimT, rimH, L.boatWid), stone);
                cutting.AddBox(new Vector3(x - (halfL + rimT * 0.5f), rimH * 0.5f, z),
                    new Vector3(rimT, rimH, L.boatWid), stone);

                // Soft inner limestone lining so the cut reads as worked bedrock.
                float linH = depth - 0.5f;
                float ly = -depth + linH * 0.5f + 0.15f;
                float innerN = halfW - wallT - liningT * 0.5f - 0.02f;
                float innerE = halfL - wallT - liningT * 0.5f - 0.02f;
                float innerEW = L.boatLen - wallT * 2f - 0.2f;
                float innerNS = L.boatWid - wallT * 2f - 0.2f;
                lining.AddBox(new Vector3(x, ly, z + innerN), new Vector3(innerEW, linH, liningT), stone);
                if (i != descendIndex)
                    lining.AddBox(new Vector3(x, ly, z - innerN), new Vector3(innerEW, linH, liningT), stone);
                lining.AddBox(new Vector3(x + innerE, ly, z), new Vector3(liningT, linH, innerNS - liningT * 2f), stone);
                lining.AddBox(new Vector3(x - innerE, ly, z), new Vector3(liningT, linH, innerNS - liningT * 2f), stone);
            }

            // Walkable rim pavement corridors between pits (east-west gaps) + N/S strips along the row.
            float corridorY = 0.14f;
            for (int i = 0; i < n - 1; i++)
            {
                float gapX = x0 + i * (L.boatLen + gap) + halfL + gap * 0.5f;
                rimPav.AddBox(new Vector3(gapX, corridorY, z),
                    new Vector3(gap - 0.4f, 0.28f, L.boatWid + rimT * 2f + 2.5f), stone);
            }
            // Continuous north strip (toward Khufu pavement) and south strip (approach).
            float stripZ_N = z + halfW + rimT + 2.0f;
            float stripZ_S = z - (halfW + rimT + 2.5f);
            rimPav.AddBox(new Vector3(0f, corridorY, stripZ_N),
                new Vector3(span + 6f, 0.28f, 3.6f), stone);
            rimPav.AddBox(new Vector3(0f, corridorY, stripZ_S),
                new Vector3(span + 6f, 0.28f, 4.2f), stone);
            // End pads east/west of the row.
            float endPadX = span * 0.5f + 3f;
            rimPav.AddBox(new Vector3(endPadX, corridorY, z),
                new Vector3(5f, 0.28f, L.boatWid + 6f), stone);
            rimPav.AddBox(new Vector3(-endPadX, corridorY, z),
                new Vector3(5f, 0.28f, L.boatWid + 6f), stone);

            // Stairs into middle pit from south rim through wall gap (VR-safe, open-top headroom).
            {
                float x = x0 + descendIndex * (L.boatLen + gap);
                int steps = 9;
                float rise = depth / steps; // ~0.4 m
                float run = 0.52f;          // total run ~4.7 m — fits inside ~4.7 m clear N-S
                float stepW = 3.2f;
                // s=0 at south rim (high); s increases northward into the pit while descending.
                float zTop = z - halfW - rimT * 0.15f;
                for (int s = 0; s < steps; s++)
                {
                    float sy = -(s + 0.5f) * rise;
                    float sz = zTop + (s + 0.5f) * run;
                    stairs.AddBox(new Vector3(x, sy, sz),
                        new Vector3(stepW, rise * 0.92f, run * 0.95f), stone);
                }
                // Top landing on south rim pavement.
                stairs.AddBox(new Vector3(x, 0.12f, z - halfW - rimT - 0.7f),
                    new Vector3(stepW + 0.8f, 0.24f, 1.5f), stone);
                // Bottom floor pad inside pit after last tread (short of north wall).
                float zBot = zTop + steps * run;
                stairs.AddBox(new Vector3(x, -depth + floorT + 0.06f, Mathf.Min(zBot + 0.35f, z + halfW - wallT - 1.2f)),
                    new Vector3(stepW + 0.6f, 0.12f, 1.6f), stone);
            }

            // Schematic limestone cover-block remnants on one pit (not the Cairo museum boat).
            {
                float x = x0 + coverIndex * (L.boatLen + gap);
                float slabY = 0.35f;
                float slabT = 0.55f;
                // Three partial transverse cover slabs leaving gaps (remnants, not sealed roof).
                covers.AddBox(new Vector3(x - 12f, slabY, z), new Vector3(8f, slabT, L.boatWid + 0.6f), stone);
                covers.AddBox(new Vector3(x + 2f, slabY, z), new Vector3(7f, slabT, L.boatWid + 0.6f), stone);
                covers.AddBox(new Vector3(x + 16f, slabY, z), new Vector3(6f, slabT, L.boatWid + 0.4f), stone);
                // Thin edge blocks / markers on rim.
                covers.AddBox(new Vector3(x - halfL + 1.5f, slabY + 0.15f, z + halfW + 0.3f),
                    new Vector3(2.2f, 0.4f, 0.7f), stone);
                covers.AddBox(new Vector3(x + halfL - 1.5f, slabY + 0.15f, z + halfW + 0.3f),
                    new Vector3(2.2f, 0.4f, 0.7f), stone);
            }

            // Named cutting mesh is the force-rebuild marker (_Cutting).
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_Cutting",
                cutting.Build(KhufuBoatPitsName + "_Cutting"), rock, true);
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_Lining",
                lining.Build(KhufuBoatPitsName + "_Lining"), lime, true);
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_RimPavement",
                rimPav.Build(KhufuBoatPitsName + "_RimPavement"), pav, true);
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_Stairs",
                stairs.Build(KhufuBoatPitsName + "_Stairs"), pav, true);
            GizaBuild.SpawnMesh(root.transform, KhufuBoatPitsName + "_CoverSlabs",
                covers.Build(KhufuBoatPitsName + "_CoverSlabs"), tura, true);

            const string barque =
                GizaComplex.HonestyPrefix + "\n" +
                "Khufu solar boat pits — reconstructed rock-cut cuttings along Khufu's south face, ~50 x 7 m each (Lehner/Petrie south-side boat pits).\n" +
                "Five schematic excavated limestone pits with walkable rim pavement; stairs into the middle pit (open-top headroom). Partial cover-slab remnants on one pit are schematic markers only.\n" +
                "Not the Cairo museum boat. Not photogrammetry. Not a sealed timber find.";
            GizaBuild.HonestyPlate(root.transform, KhufuBoatPitsName + "_Honesty", barque, 22f);
            Transform plate = root.transform.Find(KhufuBoatPitsName + "_Honesty");
            if (plate != null)
            {
                float lx = x0 + coverIndex * (L.boatLen + gap);
                plate.localPosition = new Vector3(lx, 1.55f, z - halfW - rimT - 7f);
                plate.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            return root;
        }

        /// <summary>
        /// Unfinished rock-cut Trial Passages south of Khufu (Lehner/Petrie schematic).
        /// Practice corridor slopes before the Great Pyramid interiors — not the pyramid itself.
        /// Mouth west of centreline, south of the south boat-pit row.
        /// </summary>
        static GameObject BuildKhufuTrialPassages(GizaComplex.Pose pose, Layout L)
        {
            GameObject root = GizaBuild.Root(KhufuTrialPassagesName, pose.parent, pose.khufuCenter, pose.rot);
            Material rock = GizaBuild.Bedrock();
            Material lime = GizaBuild.InteriorLime();
            Material pav = GizaBuild.Pavement();

            float x = L.trialEast;
            float zMouth = L.trialNorth;
            float len = L.trialLen;
            float wid = L.trialWid;
            float halfW = wid * 0.5f;
            const float depth = 7.2f;
            const float wallT = 0.9f;
            const float floorT = 0.38f;
            const float head = 2.2f;
            const float liningT = 0.28f;
            const float rimH = 0.5f;
            const float rimT = 0.85f;
            const float openLen = 8f; // open-top entrance for VR comfort
            Color stone = Color.white;

            var cutting = new LabMeshBuilder(420, 640);
            var lining = new LabMeshBuilder(220, 340);
            var floor = new LabMeshBuilder(180, 280);
            var rimPav = new LabMeshBuilder(80, 120);
            var stairs = new LabMeshBuilder(120, 180);

            // Stepped descending corridor: s=0 at mouth (north, high), s increases south while descending.
            int steps = 16;
            float run = len / steps;
            float rise = depth / steps;
            float zEnd = zMouth - len;

            // Corridor floor treads + side bedrock walls + lime lining.
            for (int s = 0; s < steps; s++)
            {
                float sy = -(s + 0.5f) * rise;
                float sz = zMouth - (s + 0.5f) * run;
                float clearW = wid;
                // Floor tread (walkable).
                floor.AddBox(new Vector3(x, sy + floorT * 0.5f, sz),
                    new Vector3(clearW, floorT, run * 0.98f), stone);
                // Bedrock side walls (full height of local trench).
                float wallH = head + (s + 1) * rise * 0.15f + 0.6f;
                if (wallH < head + 0.8f) wallH = head + 0.8f;
                float wy = sy + wallH * 0.35f;
                cutting.AddBox(new Vector3(x + halfW + wallT * 0.5f, wy, sz),
                    new Vector3(wallT, wallH, run * 1.02f), stone);
                cutting.AddBox(new Vector3(x - (halfW + wallT * 0.5f), wy, sz),
                    new Vector3(wallT, wallH, run * 1.02f), stone);
                // Soft limestone lining on inner faces.
                float ly = sy + head * 0.45f;
                lining.AddBox(new Vector3(x + halfW - liningT * 0.5f - 0.02f, ly, sz),
                    new Vector3(liningT, head * 0.9f, run * 0.95f), stone);
                lining.AddBox(new Vector3(x - (halfW - liningT * 0.5f - 0.02f), ly, sz),
                    new Vector3(liningT, head * 0.9f, run * 0.95f), stone);
                // Roof / ceiling only past open entrance (partially open OK further down — leave gaps).
                bool roofed = (s + 0.5f) * run > openLen;
                if (roofed && (s % 3 != 1))
                {
                    float cy = sy + head + 0.2f;
                    cutting.AddBox(new Vector3(x, cy, sz),
                        new Vector3(clearW + wallT * 2f, 0.45f, run * 0.95f), stone);
                }
            }

            // Surface entrance collar / open-top mouth rim (north).
            float mouthY = rimH * 0.5f;
            cutting.AddBox(new Vector3(x, mouthY, zMouth + 1.2f),
                new Vector3(wid + wallT * 2f + rimT * 2f, rimH, rimT), stone);
            cutting.AddBox(new Vector3(x + halfW + wallT + rimT * 0.5f, mouthY, zMouth - openLen * 0.35f),
                new Vector3(rimT, rimH, openLen * 0.8f), stone);
            cutting.AddBox(new Vector3(x - (halfW + wallT + rimT * 0.5f), mouthY, zMouth - openLen * 0.35f),
                new Vector3(rimT, rimH, openLen * 0.8f), stone);

            // Short horizontal mid chamber (~55% down).
            float midT = 0.55f;
            float midZ = zMouth - midT * len;
            float midY = -midT * depth;
            float midEW = 3.6f;
            float midNS = 4.0f;
            float midHead = 2.4f;
            floor.AddBox(new Vector3(x, midY + floorT * 0.5f, midZ),
                new Vector3(midEW - 0.3f, floorT, midNS - 0.3f), stone);
            float mwy = midY + midHead * 0.45f;
            // East/west chamber walls with stub-branch door gaps near midZ.
            float stubDoor = 1.35f;
            float ewRemain = (midNS - stubDoor) * 0.5f;
            float eastX = x + midEW * 0.5f - wallT * 0.5f;
            float westX = x - (midEW * 0.5f - wallT * 0.5f);
            if (ewRemain > 0.25f)
            {
                float zOff = (stubDoor + ewRemain) * 0.5f;
                cutting.AddBox(new Vector3(eastX, mwy, midZ + zOff), new Vector3(wallT, midHead, ewRemain), stone);
                cutting.AddBox(new Vector3(eastX, mwy, midZ - zOff), new Vector3(wallT, midHead, ewRemain), stone);
                cutting.AddBox(new Vector3(westX, mwy, midZ + zOff), new Vector3(wallT, midHead, ewRemain), stone);
                cutting.AddBox(new Vector3(westX, mwy, midZ - zOff), new Vector3(wallT, midHead, ewRemain), stone);
            }
            // North/south jambs leave corridor-width doorways so the descending lane continues.
            float doorGap = wid + 0.15f;
            float jambRemain = (midEW - doorGap) * 0.5f;
            if (jambRemain > 0.25f)
            {
                float xOff = (doorGap + jambRemain) * 0.5f;
                float nz = midZ + midNS * 0.5f - wallT * 0.5f;
                float sz = midZ - (midNS * 0.5f - wallT * 0.5f);
                cutting.AddBox(new Vector3(x + xOff, mwy, nz), new Vector3(jambRemain, midHead, wallT), stone);
                cutting.AddBox(new Vector3(x - xOff, mwy, nz), new Vector3(jambRemain, midHead, wallT), stone);
                cutting.AddBox(new Vector3(x + xOff, mwy, sz), new Vector3(jambRemain, midHead, wallT), stone);
                cutting.AddBox(new Vector3(x - xOff, mwy, sz), new Vector3(jambRemain, midHead, wallT), stone);
            }
            // Mid chamber lime lining on E/W faces.
            lining.AddBox(new Vector3(x + midEW * 0.5f - wallT - liningT * 0.5f, mwy, midZ),
                new Vector3(liningT, midHead * 0.85f, midNS - wallT * 2f - 0.2f), stone);
            lining.AddBox(new Vector3(x - (midEW * 0.5f - wallT - liningT * 0.5f), mwy, midZ),
                new Vector3(liningT, midHead * 0.85f, midNS - wallT * 2f - 0.2f), stone);
            // Partial mid ceiling (open gap over corridor centreline for comfort).
            cutting.AddBox(new Vector3(x + 1.1f, midY + midHead + 0.15f, midZ),
                new Vector3(1.2f, 0.4f, midNS * 0.7f), stone);
            cutting.AddBox(new Vector3(x - 1.1f, midY + midHead + 0.15f, midZ),
                new Vector3(1.2f, 0.4f, midNS * 0.7f), stone);

            // Stub unfinished east branch (dead-end practice cutting).
            float stubZ = midZ - 0.4f;
            float stubY = midY;
            float stubLen = 5.5f;
            float stubW = 1.2f;
            float stubX = x + midEW * 0.5f + stubLen * 0.5f - 0.2f;
            floor.AddBox(new Vector3(stubX, stubY + floorT * 0.5f, stubZ),
                new Vector3(stubLen - 0.4f, floorT, stubW), stone);
            float swy = stubY + head * 0.4f;
            cutting.AddBox(new Vector3(stubX, swy, stubZ + stubW * 0.5f + wallT * 0.4f),
                new Vector3(stubLen, head, wallT), stone);
            cutting.AddBox(new Vector3(stubX, swy, stubZ - (stubW * 0.5f + wallT * 0.4f)),
                new Vector3(stubLen, head, wallT), stone);
            // Unfinished dead-end face (thicker rough stub).
            cutting.AddBox(new Vector3(stubX + stubLen * 0.5f - wallT * 0.6f, swy, stubZ),
                new Vector3(wallT * 1.4f, head, stubW + wallT), stone);
            lining.AddBox(new Vector3(stubX, swy, stubZ + stubW * 0.5f - liningT * 0.5f),
                new Vector3(stubLen - 0.6f, head * 0.8f, liningT), stone);

            // Stub unfinished west branch (shorter dead-end).
            float wStubLen = 3.8f;
            float wStubX = x - midEW * 0.5f - wStubLen * 0.5f + 0.2f;
            floor.AddBox(new Vector3(wStubX, stubY + floorT * 0.5f, stubZ + 0.3f),
                new Vector3(wStubLen - 0.4f, floorT, stubW * 0.9f), stone);
            cutting.AddBox(new Vector3(wStubX, swy, stubZ + 0.3f + stubW * 0.45f + wallT * 0.4f),
                new Vector3(wStubLen, head * 0.9f, wallT), stone);
            cutting.AddBox(new Vector3(wStubX, swy, stubZ + 0.3f - (stubW * 0.45f + wallT * 0.4f)),
                new Vector3(wStubLen, head * 0.9f, wallT), stone);
            cutting.AddBox(new Vector3(wStubX - wStubLen * 0.5f + wallT * 0.7f, swy, stubZ + 0.3f),
                new Vector3(wallT * 1.5f, head * 0.9f, stubW), stone);

            // South dead-end unfinished face of main corridor.
            float endY = -depth + head * 0.4f;
            cutting.AddBox(new Vector3(x, endY, zEnd - wallT * 0.5f),
                new Vector3(wid + wallT * 2f, head + 1.2f, wallT * 1.3f), stone);

            // Stairs from surface apron down into open mouth (north approach).
            {
                int n = 7;
                float sRise = 0.38f;
                float sRun = 0.5f;
                float stepW = wid + 0.6f;
                float zTop = zMouth + 2.4f;
                for (int s = 0; s < n; s++)
                {
                    float sy = -(s + 0.5f) * sRise;
                    float sz = zTop - (s + 0.5f) * sRun;
                    stairs.AddBox(new Vector3(x, sy, sz),
                        new Vector3(stepW, sRise * 0.92f, sRun * 0.95f), stone);
                }
                // Top landing on rim pavement.
                stairs.AddBox(new Vector3(x, 0.12f, zMouth + 3.4f),
                    new Vector3(stepW + 1.2f, 0.24f, 1.6f), stone);
            }

            // Rim pavement apron around entrance mouth.
            float corridorY = 0.14f;
            rimPav.AddBox(new Vector3(x, corridorY, zMouth + 4.2f),
                new Vector3(wid + 8f, 0.28f, 4.5f), stone);
            rimPav.AddBox(new Vector3(x + halfW + 3.2f, corridorY, zMouth + 0.5f),
                new Vector3(3.5f, 0.28f, 6f), stone);
            rimPav.AddBox(new Vector3(x - (halfW + 3.2f), corridorY, zMouth + 0.5f),
                new Vector3(3.5f, 0.28f, 6f), stone);
            rimPav.AddBox(new Vector3(x, corridorY, zMouth - openLen * 0.5f),
                new Vector3(wid + 5.5f, 0.28f, openLen + 1.5f), stone);

            // Named cutting mesh is the force-rebuild marker (_Cutting).
            GizaBuild.SpawnMesh(root.transform, KhufuTrialPassagesName + "_Cutting",
                cutting.Build(KhufuTrialPassagesName + "_Cutting"), rock, true);
            GizaBuild.SpawnMesh(root.transform, KhufuTrialPassagesName + "_Lining",
                lining.Build(KhufuTrialPassagesName + "_Lining"), lime, true);
            GizaBuild.SpawnMesh(root.transform, KhufuTrialPassagesName + "_Floor",
                floor.Build(KhufuTrialPassagesName + "_Floor"), lime, true);
            GizaBuild.SpawnMesh(root.transform, KhufuTrialPassagesName + "_RimPavement",
                rimPav.Build(KhufuTrialPassagesName + "_RimPavement"), pav, true);
            GizaBuild.SpawnMesh(root.transform, KhufuTrialPassagesName + "_Stairs",
                stairs.Build(KhufuTrialPassagesName + "_Stairs"), pav, true);

            const string honesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Khufu Trial Passages — unfinished rock-cut trial corridors south of the Great Pyramid (Lehner/Petrie schematic).\n" +
                "Used to practice passage slopes before cutting Khufu interiors. Descending corridor, short mid chamber, stub unfinished branches.\n" +
                "Not photogrammetry. Not the Great Pyramid interior itself.";
            GizaBuild.HonestyPlate(root.transform, KhufuTrialPassagesName + "_Honesty", honesty, 16f);
            Transform plate = root.transform.Find(KhufuTrialPassagesName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(x + 4.5f, 1.55f, zMouth + 5.5f);
                plate.localRotation = Quaternion.Euler(0f, 200f, 0f);
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


        static GameObject BuildKhafreBoatPits(GizaComplex.Pose pose, Layout L)
        {
            // Root at Khufu centre so east/north layout matches other precinct monuments.
            GameObject root = GizaBuild.Root(KhafreBoatPitsName, pose.parent, pose.khufuCenter, pose.rot);
            Material rock = GizaBuild.Bedrock();
            Material lime = GizaBuild.InteriorLime();
            Material pav = GizaBuild.Pavement();
            Material tura = GizaBuild.TuraCasing();

            const float depth = 3.4f;
            const float wallT = 1.1f;
            const float floorT = 0.4f;
            const float rimH = 0.5f;
            const float rimT = 0.85f;
            const float gap = 6f;
            const float liningT = 0.35f;
            int n = 2;
            float span = n * L.khafreBoatLen + (n - 1) * gap;
            float x0 = L.khafreBoatEast - span * 0.5f + L.khafreBoatLen * 0.5f;
            int descendIndex = 0; // west pit: stairs from south rim
            int coverIndex = 1;   // east pit: cover-slab remnants
            float z = L.khafreBoatNorth;
            float halfL = L.khafreBoatLen * 0.5f;
            float halfW = L.khafreBoatWid * 0.5f;

            var cutting = new LabMeshBuilder(220, 340);
            var lining = new LabMeshBuilder(120, 180);
            var rimPav = new LabMeshBuilder(96, 144);
            var stairs = new LabMeshBuilder(80, 120);
            var covers = new LabMeshBuilder(40, 60);
            Color stone = Color.white;

            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * (L.khafreBoatLen + gap);
                cutting.AddBox(new Vector3(x, -depth + floorT * 0.5f, z),
                    new Vector3(L.khafreBoatLen - wallT * 2f, floorT, L.khafreBoatWid - wallT * 2f), stone);
                float wy = -depth * 0.5f;
                cutting.AddBox(new Vector3(x, wy, z + halfW - wallT * 0.5f),
                    new Vector3(L.khafreBoatLen, depth, wallT), stone);
                float southZ = z - (halfW - wallT * 0.5f);
                if (i == descendIndex)
                {
                    const float doorW = 3.4f;
                    float remain = (L.khafreBoatLen - doorW) * 0.5f;
                    if (remain > 0.3f)
                    {
                        float xOff = (doorW + remain) * 0.5f;
                        cutting.AddBox(new Vector3(x + xOff, wy, southZ),
                            new Vector3(remain, depth, wallT), stone);
                        cutting.AddBox(new Vector3(x - xOff, wy, southZ),
                            new Vector3(remain, depth, wallT), stone);
                    }
                }
                else
                {
                    cutting.AddBox(new Vector3(x, wy, southZ),
                        new Vector3(L.khafreBoatLen, depth, wallT), stone);
                }
                cutting.AddBox(new Vector3(x + halfL - wallT * 0.5f, wy, z),
                    new Vector3(wallT, depth, L.khafreBoatWid - wallT * 2f), stone);
                cutting.AddBox(new Vector3(x - (halfL - wallT * 0.5f), wy, z),
                    new Vector3(wallT, depth, L.khafreBoatWid - wallT * 2f), stone);

                cutting.AddBox(new Vector3(x, rimH * 0.5f, z + halfW + rimT * 0.5f),
                    new Vector3(L.khafreBoatLen + rimT * 2f, rimH, rimT), stone);
                float southRimZ = z - (halfW + rimT * 0.5f);
                if (i == descendIndex)
                {
                    const float doorW = 3.4f;
                    float remain = (L.khafreBoatLen - doorW) * 0.5f;
                    if (remain > 0.3f)
                    {
                        float xOff = (doorW + remain) * 0.5f;
                        cutting.AddBox(new Vector3(x + xOff, rimH * 0.5f, southRimZ),
                            new Vector3(remain + rimT, rimH, rimT), stone);
                        cutting.AddBox(new Vector3(x - xOff, rimH * 0.5f, southRimZ),
                            new Vector3(remain + rimT, rimH, rimT), stone);
                    }
                }
                else
                {
                    cutting.AddBox(new Vector3(x, rimH * 0.5f, southRimZ),
                        new Vector3(L.khafreBoatLen + rimT * 2f, rimH, rimT), stone);
                }
                cutting.AddBox(new Vector3(x + halfL + rimT * 0.5f, rimH * 0.5f, z),
                    new Vector3(rimT, rimH, L.khafreBoatWid), stone);
                cutting.AddBox(new Vector3(x - (halfL + rimT * 0.5f), rimH * 0.5f, z),
                    new Vector3(rimT, rimH, L.khafreBoatWid), stone);

                float linH = depth - 0.5f;
                float ly = -depth + linH * 0.5f + 0.15f;
                float innerN = halfW - wallT - liningT * 0.5f - 0.02f;
                float innerE = halfL - wallT - liningT * 0.5f - 0.02f;
                float innerEW = L.khafreBoatLen - wallT * 2f - 0.2f;
                float innerNS = L.khafreBoatWid - wallT * 2f - 0.2f;
                lining.AddBox(new Vector3(x, ly, z + innerN), new Vector3(innerEW, linH, liningT), stone);
                if (i != descendIndex)
                    lining.AddBox(new Vector3(x, ly, z - innerN), new Vector3(innerEW, linH, liningT), stone);
                lining.AddBox(new Vector3(x + innerE, ly, z), new Vector3(liningT, linH, innerNS - liningT * 2f), stone);
                lining.AddBox(new Vector3(x - innerE, ly, z), new Vector3(liningT, linH, innerNS - liningT * 2f), stone);
            }

            float corridorY = 0.14f;
            for (int i = 0; i < n - 1; i++)
            {
                float gapX = x0 + i * (L.khafreBoatLen + gap) + halfL + gap * 0.5f;
                rimPav.AddBox(new Vector3(gapX, corridorY, z),
                    new Vector3(gap - 0.4f, 0.28f, L.khafreBoatWid + rimT * 2f + 2.5f), stone);
            }
            float stripZ_N = z + halfW + rimT + 2.0f;
            float stripZ_S = z - (halfW + rimT + 2.5f);
            rimPav.AddBox(new Vector3(L.khafreBoatEast, corridorY, stripZ_N),
                new Vector3(span + 6f, 0.28f, 3.6f), stone);
            rimPav.AddBox(new Vector3(L.khafreBoatEast, corridorY, stripZ_S),
                new Vector3(span + 6f, 0.28f, 4.2f), stone);
            float endPadX = L.khafreBoatEast + span * 0.5f + 3f;
            float endPadX2 = L.khafreBoatEast - span * 0.5f - 3f;
            rimPav.AddBox(new Vector3(endPadX, corridorY, z),
                new Vector3(5f, 0.28f, L.khafreBoatWid + 6f), stone);
            rimPav.AddBox(new Vector3(endPadX2, corridorY, z),
                new Vector3(5f, 0.28f, L.khafreBoatWid + 6f), stone);

            {
                float x = x0 + descendIndex * (L.khafreBoatLen + gap);
                int steps = 8;
                float rise = depth / steps;
                float run = 0.5f;
                float stepW = 3.0f;
                float zTop = z - halfW - rimT * 0.15f;
                for (int s = 0; s < steps; s++)
                {
                    float sy = -(s + 0.5f) * rise;
                    float sz = zTop + (s + 0.5f) * run;
                    stairs.AddBox(new Vector3(x, sy, sz),
                        new Vector3(stepW, rise * 0.92f, run * 0.95f), stone);
                }
                stairs.AddBox(new Vector3(x, 0.12f, z - halfW - rimT - 0.7f),
                    new Vector3(stepW + 0.8f, 0.24f, 1.5f), stone);
                float zBot = zTop + steps * run;
                stairs.AddBox(new Vector3(x, -depth + floorT + 0.06f, Mathf.Min(zBot + 0.35f, z + halfW - wallT - 1.2f)),
                    new Vector3(stepW + 0.6f, 0.12f, 1.6f), stone);
            }

            {
                float x = x0 + coverIndex * (L.khafreBoatLen + gap);
                float slabY = 0.35f;
                float slabT = 0.55f;
                covers.AddBox(new Vector3(x - 10f, slabY, z), new Vector3(7f, slabT, L.khafreBoatWid + 0.5f), stone);
                covers.AddBox(new Vector3(x + 4f, slabY, z), new Vector3(6f, slabT, L.khafreBoatWid + 0.5f), stone);
                covers.AddBox(new Vector3(x + 14f, slabY, z), new Vector3(5f, slabT, L.khafreBoatWid + 0.3f), stone);
            }

            GizaBuild.SpawnMesh(root.transform, KhafreBoatPitsName + "_Cutting",
                cutting.Build(KhafreBoatPitsName + "_Cutting"), rock, true);
            GizaBuild.SpawnMesh(root.transform, KhafreBoatPitsName + "_Lining",
                lining.Build(KhafreBoatPitsName + "_Lining"), lime, true);
            GizaBuild.SpawnMesh(root.transform, KhafreBoatPitsName + "_RimPavement",
                rimPav.Build(KhafreBoatPitsName + "_RimPavement"), pav, true);
            GizaBuild.SpawnMesh(root.transform, KhafreBoatPitsName + "_Stairs",
                stairs.Build(KhafreBoatPitsName + "_Stairs"), pav, true);
            GizaBuild.SpawnMesh(root.transform, KhafreBoatPitsName + "_CoverSlabs",
                covers.Build(KhafreBoatPitsName + "_CoverSlabs"), tura, true);

            const string barque =
                GizaComplex.HonestyPrefix + "\n" +
                "Khafre solar boat pits — reconstructed rock-cut cuttings on the south terrace east of cult pyramid G2-a (Lehner schematic).\n" +
                "Two excavated limestone pits (~42 x 6.5 m) with walkable rim pavement; stairs into the west pit (open-top headroom). Partial cover-slab remnants on the east pit are schematic markers only.\n" +
                "Not a museum boat find. Not photogrammetry. Not sealed timber.";
            GizaBuild.HonestyPlate(root.transform, KhafreBoatPitsName + "_Honesty", barque, 18f);
            Transform plate = root.transform.Find(KhafreBoatPitsName + "_Honesty");
            if (plate != null)
            {
                float lx = x0 + coverIndex * (L.khafreBoatLen + gap);
                plate.localPosition = new Vector3(lx, 1.55f, z - halfW - rimT - 7f);
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
