using UnityEngine;
using TMPro;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Khufu (Great Pyramid) complete replica at 1:1. Exterior Tura casing + walkable interior.
    /// Architectural reconstruction from published royal-cubit dimensions — not photogrammetry.
    /// Local space: origin at base centre, +Y up, +Z architectural north (entrance face).
    /// </summary>
    public static class KhufuPyramid
    {
        public const float Cubit = 0.5236f;
        public const float BaseCubits = 440f;
        public const float HeightCubits = 280f;
        public const float BaseMeters = BaseCubits * Cubit;
        public const float HeightMeters = HeightCubits * Cubit;
        public const float SlopeDeg = 51f + 50f / 60f;
        public const float EntranceHeightM = 17f;
        public const float EntranceEastOffsetM = 7.29f;
        public const float PassageWidthM = 1.05f;
        public const float PassageHeightM = 1.20f;
        public const float PassageAngleDeg = 26.5f;
        public const float DescendLengthM = 105f;
        public const float JunctionFromEntranceM = 28.2f;
        public const float AscendLengthM = 37.76f;
        public const float QueenPassageM = 38.15f;
        public const float QueenEW = 5.8f;
        public const float QueenNS = 5.3f;
        public const float QueenPeakH = 6.2f;
        public const float GrandGalleryLengthM = 46.7f;
        public const float GrandGalleryHeightM = 8.6f;
        public const float GrandGalleryFloorW = 2.1f;
        public const float KingEW = 10.47f;
        public const float KingNS = 5.24f;
        public const float KingH = 5.84f;
        public const float SubEW = 14.1f;
        public const float SubNS = 8.3f;
        public const float SubH = 3.5f;
        public const float PavementWidthM = 6.0f;
        public const float PyramidionCubits = 2f;
        public const string RootName = "Khufu";

        public const string Honesty =
            "Khufu — Great Pyramid. Reconstructed original (Tura limestone casing ON), not the stripped core, not a scan, not photogrammetry.\n" +
            "Royal cubit 0.5236 m. Base 440 cubits (230.38 m). Height 280 cubits (146.61 m). Slope 51° 50'. Pyramidion 2 cubits.\n" +
            "Entrance 17 m vertical on north face, 7.29 m east of centreline (Petrie). Passages 1.05 × 1.20 m at 26.5°.\n" +
            "Descending 105 m (published) to unfinished subterranean 14.1 × 8.3 × 3.5 m. Junction 28.2 m; ascending 37.76 m (Petrie).\n" +
            "Queen's chamber 5.8 × 5.3 × 6.2 m gabled; horizontal 38.15 m. Grand Gallery 46.7 × 8.6 × 2.1 m corbelled.\n" +
            "King's chamber 10.47 × 5.24 × 5.84 m granite + sarcophagus. Five relieving chambers schematic. Air shafts thin channels.\n" +
            "No fictional rooms. Interior axis offset 7.29 m east. Comfort scale defaults OFF.";

        public static GameObject Build(Transform parent, Vector3 worldBaseCenter, Quaternion worldRot, bool comfortScale)
        {
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(worldBaseCenter, worldRot);

            float scale = comfortScale ? 1.6f : 1f;
            float pw = PassageWidthM * scale;
            float ph = PassageHeightM * scale;

            Material limestone = LabWorldMeshes.MakeLit("RELab_KhufuLimestone", new Color(0.76f, 0.71f, 0.62f, 1f), 0.08f, 0.22f, false);
            Texture2D courses = LabWorldMeshes.MakeCourseTexture();
            if (limestone != null && courses != null)
            {
                if (limestone.HasProperty("_BaseMap"))
                    limestone.SetTexture("_BaseMap", courses);
                if (limestone.HasProperty("_MainTex"))
                    limestone.SetTexture("_MainTex", courses);
                limestone.SetTextureScale("_BaseMap", new Vector2(1f, 12f));
                limestone.SetTextureScale("_MainTex", new Vector2(1f, 12f));
            }
            Material limeDark = LabWorldMeshes.MakeLit("RELab_KhufuCore", new Color(0.55f, 0.50f, 0.44f, 1f), 0.04f, 0.12f, false);
            Material granite = LabWorldMeshes.MakeLit("RELab_KhufuGranite", new Color(0.28f, 0.22f, 0.22f, 1f), 0.18f, 0.28f, false);
            Material pavement = LabWorldMeshes.MakeLit("RELab_KhufuPavement", new Color(0.70f, 0.66f, 0.58f, 1f), 0.06f, 0.18f, false);
            Material gold = LabWorldMeshes.MakeLit("RELab_KhufuPyramidion", new Color(0.72f, 0.64f, 0.42f, 1f), 0.55f, 0.42f, false);
            Material emit = LabWorldMeshes.MakeLit("RELab_KhufuEmit", new Color(0.22f, 0.20f, 0.16f, 1f), 0.0f, 0.08f, true);
            if (emit != null && emit.HasProperty("_EmissionColor"))
                emit.SetColor("_EmissionColor", new Color(0.18f, 0.16f, 0.12f, 1f));

            BuildExterior(root.transform, limestone);
            BuildPyramidion(root.transform, gold);
            BuildPavement(root.transform, pavement);
            BuildInterior(root.transform, limeDark, granite, emit, pw, ph);
            BuildHonestyPlate(root.transform);

            return root;
        }

        static void BuildExterior(Transform parent, Material mat)
        {
            float half = BaseMeters * 0.5f;
            float H = HeightMeters;
            float entryY = EntranceHeightM;
            float entryX = EntranceEastOffsetM;
            float zFace = half * (1f - entryY / H);
            float holeW = PassageWidthM * 1.35f;
            float holeH = PassageHeightM * 1.45f;

            var b = new LabMeshBuilder(6000, 18000);
            Color c = Color.white;
            AddSlopedFace(b, new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(0f, H, 0f), Vector3.forward, c, 8, true, entryX, entryY, zFace, holeW, holeH);
            AddSlopedFace(b, new Vector3(half, 0f, half), new Vector3(half, 0f, -half), new Vector3(0f, H, 0f), Vector3.right, c, 48, false, 0f, 0f, 0f, 0f, 0f);
            AddSlopedFace(b, new Vector3(half, 0f, -half), new Vector3(-half, 0f, -half), new Vector3(0f, H, 0f), Vector3.back, c, 48, false, 0f, 0f, 0f, 0f, 0f);
            AddSlopedFace(b, new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(0f, H, 0f), Vector3.left, c, 48, false, 0f, 0f, 0f, 0f, 0f);

            SpawnMesh(parent, "Khufu_Casing", b.Build("Khufu_Casing"), mat, true);
        }

        static void AddSlopedFace(LabMeshBuilder b, Vector3 bl, Vector3 br, Vector3 apex, Vector3 hint, Color color, int courses, bool hole, float holeX, float holeY, float holeZ, float holeW, float holeH)
        {
            int uDiv = hole ? 28 : 4;
            int vDiv = hole ? 40 : Mathf.Max(8, courses);
            for (int v = 0; v < vDiv; v++)
            {
                float t0 = v / (float)vDiv;
                float t1 = (v + 1) / (float)vDiv;
                Vector3 a0 = Vector3.Lerp(bl, apex, t0);
                Vector3 b0 = Vector3.Lerp(br, apex, t0);
                Vector3 a1 = Vector3.Lerp(bl, apex, t1);
                Vector3 b1 = Vector3.Lerp(br, apex, t1);
                for (int u = 0; u < uDiv; u++)
                {
                    float s0 = u / (float)uDiv;
                    float s1 = (u + 1) / (float)uDiv;
                    Vector3 p00 = Vector3.Lerp(a0, b0, s0);
                    Vector3 p10 = Vector3.Lerp(a0, b0, s1);
                    Vector3 p11 = Vector3.Lerp(a1, b1, s1);
                    Vector3 p01 = Vector3.Lerp(a1, b1, s0);
                    if (hole)
                    {
                        Vector3 mid = (p00 + p10 + p11 + p01) * 0.25f;
                        if (Mathf.Abs(mid.x - holeX) < holeW * 0.5f &&
                            Mathf.Abs(mid.y - holeY) < holeH * 0.5f &&
                            mid.z > holeZ - 2.5f)
                            continue;
                    }
                    Vector2 uv00 = new Vector2(s0, t0 * 12f);
                    Vector2 uv10 = new Vector2(s1, t0 * 12f);
                    Vector2 uv11 = new Vector2(s1, t1 * 12f);
                    Vector2 uv01 = new Vector2(s0, t1 * 12f);
                    b.AddQuad(p00, p10, p11, p01, hint, uv00, uv10, uv11, uv01, color);
                }
            }
        }

        static void BuildPyramidion(Transform parent, Material mat)
        {
            float hCap = PyramidionCubits * Cubit;
            float y0 = HeightMeters - hCap;
            float half0 = (BaseMeters * 0.5f) * (hCap / HeightMeters);
            var b = new LabMeshBuilder(16, 24);
            Color c = Color.white;
            Vector3 a = new Vector3(-half0, y0, half0);
            Vector3 br = new Vector3(half0, y0, half0);
            Vector3 cr = new Vector3(half0, y0, -half0);
            Vector3 d = new Vector3(-half0, y0, -half0);
            Vector3 apex = new Vector3(0f, HeightMeters, 0f);
            b.AddTri(a, br, apex, Vector3.forward, Vector2.zero, Vector2.right, Vector2.one, c);
            b.AddTri(br, cr, apex, Vector3.right, Vector2.zero, Vector2.right, Vector2.one, c);
            b.AddTri(cr, d, apex, Vector3.back, Vector2.zero, Vector2.right, Vector2.one, c);
            b.AddTri(d, a, apex, Vector3.left, Vector2.zero, Vector2.right, Vector2.one, c);
            SpawnMesh(parent, "Khufu_Pyramidion", b.Build("Khufu_Pyramidion"), mat, false);
        }

        static void BuildPavement(Transform parent, Material mat)
        {
            float inner = BaseMeters * 0.5f;
            float outer = inner + PavementWidthM;
            float y0 = -0.35f;
            float y1 = 0f;
            var b = new LabMeshBuilder(32, 48);
            Color c = Color.white;
            Vector3[] inn = Ring(inner, y1);
            Vector3[] outt = Ring(outer, y1);
            Vector3[] innB = Ring(inner, y0);
            Vector3[] outB = Ring(outer, y0);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                b.AddQuad(inn[i], outt[i], outt[j], inn[j], Vector3.up, c);
                b.AddQuad(outt[i], outB[i], outB[j], outt[j], (outt[i] + outt[j]).normalized, c);
            }
            SpawnMesh(parent, "Khufu_Pavement", b.Build("Khufu_Pavement"), mat, true);
        }

        static Vector3[] Ring(float h, float y)
        {
            return new[]
            {
                new Vector3(-h, y, -h),
                new Vector3(h, y, -h),
                new Vector3(h, y, h),
                new Vector3(-h, y, h)
            };
        }

        static void BuildInterior(Transform parent, Material lime, Material granite, Material emit, float pw, float ph)
        {
            float half = BaseMeters * 0.5f;
            float H = HeightMeters;
            float ang = PassageAngleDeg * Mathf.Deg2Rad;
            float sa = Mathf.Sin(ang);
            float ca = Mathf.Cos(ang);
            float x = EntranceEastOffsetM;
            float yC = EntranceHeightM;
            float zC = half * (1f - yC / H);
            Vector3 entrance = new Vector3(x, yC, zC);
            Vector3 descDir = new Vector3(0f, -sa, -ca);
            Vector3 ascDir = new Vector3(0f, sa, -ca);

            Vector3 mouth = entrance - descDir * 1.6f;
            Vector3 junction = entrance + descDir * JunctionFromEntranceM;
            Vector3 subEnd = entrance + descDir * DescendLengthM;
            Vector3 ascEnd = junction + ascDir * AscendLengthM;
            Vector3 ggEnd = ascEnd + ascDir * GrandGalleryLengthM;
            Vector3 queenDoor = new Vector3(ascEnd.x, ascEnd.y, ascEnd.z - QueenPassageM);

            var passages = new LabMeshBuilder(8000, 24000);
            Color stone = new Color(0.72f, 0.68f, 0.60f, 1f);
            Color dim = new Color(0.55f, 0.52f, 0.46f, 1f);
            passages.AddTunnel(mouth, subEnd, pw, ph, stone, 18);
            passages.AddTunnel(junction, ascEnd, pw, ph, stone, 10);
            passages.AddTunnel(ascEnd, queenDoor, pw, ph, dim, 8);
            AddGrandGallery(passages, ascEnd, ggEnd, pw, ph, stone);
            Vector3 anteStart = ggEnd;
            Vector3 anteEnd = ggEnd + new Vector3(0f, 0f, -2.1f);
            passages.AddTunnel(anteStart, anteEnd, pw, ph, stone, 2);
            SpawnMesh(parent, "Khufu_Passages", passages.Build("Khufu_Passages"), lime, true);

            var emitMesh = new LabMeshBuilder(256, 512);
            AddCeilingStrip(emitMesh, mouth, subEnd, pw * 0.15f, ph);
            AddCeilingStrip(emitMesh, junction, ggEnd, pw * 0.15f, GrandGalleryHeightM);
            SpawnMesh(parent, "Khufu_Emit", emitMesh.Build("Khufu_Emit"), emit, false);

            BuildSubterranean(parent, subEnd, pw, ph, lime);
            BuildQueenChamber(parent, queenDoor, pw, ph, lime);
            BuildKingSuite(parent, anteEnd, pw, ph, granite, lime);
            BuildAirShafts(parent, anteEnd, lime);
        }

        static void AddGrandGallery(LabMeshBuilder b, Vector3 start, Vector3 end, float pw, float ph, Color color)
        {
            Vector3 delta = end - start;
            float len = delta.magnitude;
            if (len < 1e-4f)
                return;
            Vector3 fwd = delta / len;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right).normalized;
            const int tiers = 7;
            float floorW = GrandGalleryFloorW;
            float topW = 1.04f;
            float gh = GrandGalleryHeightM;
            int segs = 12;
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs;
                float t1 = (i + 1) / (float)segs;
                Vector3 c0 = Vector3.Lerp(start, end, t0);
                Vector3 c1 = Vector3.Lerp(start, end, t1);
                Vector3 f0 = c0 - up * (ph * 0.5f);
                Vector3 f1 = c1 - up * (ph * 0.5f);
                float slot = pw * 0.5f;
                b.AddQuad(f0 - right * slot, f1 - right * slot, f1 + right * slot, f0 + right * slot, up, color);
                b.AddQuad(f0 - right * (floorW * 0.5f), f1 - right * (floorW * 0.5f), f1 - right * slot, f0 - right * slot, up, color);
                b.AddQuad(f0 + right * slot, f1 + right * slot, f1 + right * (floorW * 0.5f), f0 + right * (floorW * 0.5f), up, color);
                for (int k = 0; k < tiers; k++)
                {
                    float u0 = k / (float)tiers;
                    float u1 = (k + 1) / (float)tiers;
                    float w0 = Mathf.Lerp(floorW, topW, u0);
                    float w1 = Mathf.Lerp(floorW, topW, u1);
                    float y0 = u0 * gh;
                    float y1 = u1 * gh;
                    Vector3 l00 = f0 - right * (w0 * 0.5f) + up * y0;
                    Vector3 l10 = f1 - right * (w0 * 0.5f) + up * y0;
                    Vector3 l11 = f1 - right * (w1 * 0.5f) + up * y1;
                    Vector3 l01 = f0 - right * (w1 * 0.5f) + up * y1;
                    Vector3 r00 = f0 + right * (w0 * 0.5f) + up * y0;
                    Vector3 r10 = f1 + right * (w0 * 0.5f) + up * y0;
                    Vector3 r11 = f1 + right * (w1 * 0.5f) + up * y1;
                    Vector3 r01 = f0 + right * (w1 * 0.5f) + up * y1;
                    b.AddQuad(l00, l01, l11, l10, right, color);
                    b.AddQuad(r00, r10, r11, r01, -right, color);
                }
                Vector3 t0l = f0 - right * (topW * 0.5f) + up * gh;
                Vector3 t1l = f1 - right * (topW * 0.5f) + up * gh;
                Vector3 t1r = f1 + right * (topW * 0.5f) + up * gh;
                Vector3 t0r = f0 + right * (topW * 0.5f) + up * gh;
                b.AddQuad(t0l, t0r, t1r, t1l, -up, color);
            }
        }

        static void AddCeilingStrip(LabMeshBuilder b, Vector3 start, Vector3 end, float width, float height)
        {
            Vector3 delta = end - start;
            float len = delta.magnitude;
            if (len < 1e-4f)
                return;
            Vector3 fwd = delta / len;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (right.sqrMagnitude < 1e-8f)
                right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(fwd, right).normalized;
            Vector3 c0 = start + up * (height * 0.48f);
            Vector3 c1 = end + up * (height * 0.48f);
            Vector3 a = c0 - right * width;
            Vector3 br = c0 + right * width;
            Vector3 c = c1 + right * width;
            Vector3 d = c1 - right * width;
            b.AddQuad(a, br, c, d, -up, new Color(1f, 1f, 1f, 1f));
        }

        static void BuildSubterranean(Transform parent, Vector3 passageEnd, float pw, float ph, Material mat)
        {
            Vector3 up = Vector3.up;
            Vector3 floor = passageEnd - up * (ph * 0.5f);
            Vector3 center = new Vector3(passageEnd.x, floor.y + SubH * 0.5f, passageEnd.z - SubNS * 0.5f);
            var b = new LabMeshBuilder(64, 96);
            Color c = new Color(0.50f, 0.46f, 0.40f, 1f);
            b.AddRoom(center, new Vector3(SubEW, SubH, SubNS), c, false, true, false, false);
            var pit = new Vector3(center.x, floor.y - 0.4f, center.z);
            b.AddRoom(pit, new Vector3(2.5f, 0.8f, 2.5f), c, false, false, false, false);
            SpawnMesh(parent, "Khufu_Subterranean", b.Build("Khufu_Subterranean"), mat, true);
        }

        static void BuildQueenChamber(Transform parent, Vector3 door, float pw, float ph, Material mat)
        {
            Vector3 floor = door - Vector3.up * (ph * 0.5f);
            float wallH = 4.5f;
            Vector3 center = new Vector3(door.x, floor.y + wallH * 0.5f, door.z - QueenNS * 0.5f);
            var b = new LabMeshBuilder(96, 160);
            Color c = new Color(0.70f, 0.66f, 0.58f, 1f);
            b.AddRoom(center, new Vector3(QueenEW, wallH, QueenNS), c, false, true, false, false);
            float peak = QueenPeakH;
            Vector3 nL = new Vector3(center.x - QueenEW * 0.5f, floor.y + wallH, center.z + QueenNS * 0.5f);
            Vector3 nR = new Vector3(center.x + QueenEW * 0.5f, floor.y + wallH, center.z + QueenNS * 0.5f);
            Vector3 sL = new Vector3(center.x - QueenEW * 0.5f, floor.y + wallH, center.z - QueenNS * 0.5f);
            Vector3 sR = new Vector3(center.x + QueenEW * 0.5f, floor.y + wallH, center.z - QueenNS * 0.5f);
            Vector3 ridgeN = new Vector3(center.x, floor.y + peak, center.z + QueenNS * 0.5f);
            Vector3 ridgeS = new Vector3(center.x, floor.y + peak, center.z - QueenNS * 0.5f);
            b.AddQuad(nL, sL, ridgeS, ridgeN, Vector3.right, c);
            b.AddQuad(nR, ridgeN, ridgeS, sR, Vector3.left, c);
            b.AddTri(nL, nR, ridgeN, Vector3.back, Vector2.zero, Vector2.right, Vector2.one, c);
            b.AddTri(sR, sL, ridgeS, Vector3.forward, Vector2.zero, Vector2.right, Vector2.one, c);
            SpawnMesh(parent, "Khufu_QueenChamber", b.Build("Khufu_QueenChamber"), mat, true);
        }

        static void BuildKingSuite(Transform parent, Vector3 anteStart, float pw, float ph, Material granite, Material lime)
        {
            Vector3 floor = anteStart - Vector3.up * (ph * 0.5f);
            float anteL = 3.1f;
            float anteW = 1.65f;
            float anteH = 3.7f;
            Vector3 anteC = new Vector3(anteStart.x, floor.y + anteH * 0.5f, anteStart.z - anteL * 0.5f);
            var ante = new LabMeshBuilder(48, 72);
            Color g = new Color(0.32f, 0.26f, 0.26f, 1f);
            ante.AddRoom(anteC, new Vector3(anteW, anteH, anteL), g, true, true, false, false);
            SpawnMesh(parent, "Khufu_Antechamber", ante.Build("Khufu_Antechamber"), granite, true);

            Vector3 kingDoor = new Vector3(anteStart.x, anteStart.y, anteStart.z - anteL);
            Vector3 kingFloor = new Vector3(kingDoor.x, floor.y, kingDoor.z - KingNS * 0.5f);
            Vector3 kingC = kingFloor + Vector3.up * (KingH * 0.5f);
            var kc = new LabMeshBuilder(48, 72);
            kc.AddRoom(kingC, new Vector3(KingEW, KingH, KingNS), g, false, true, false, false);
            SpawnMesh(parent, "Khufu_KingChamber", kc.Build("Khufu_KingChamber"), granite, true);

            var sarc = new LabMeshBuilder(32, 48);
            Vector3 sarcC = new Vector3(kingC.x - 2.6f, floor.y + 0.525f, kingC.z);
            sarc.AddBox(sarcC, new Vector3(2.28f, 1.05f, 0.98f), g);
            sarc.AddRoom(sarcC + Vector3.up * 0.08f, new Vector3(1.98f, 0.72f, 0.68f), g, false, false, false, false);
            SpawnMesh(parent, "Khufu_Sarcophagus", sarc.Build("Khufu_Sarcophagus"), granite, true);

            var rel = new LabMeshBuilder(160, 240);
            Color rg = new Color(0.30f, 0.24f, 0.24f, 1f);
            float y = floor.y + KingH;
            for (int i = 0; i < 5; i++)
            {
                float ch = i == 4 ? 1.8f : 1.15f;
                Vector3 rc = new Vector3(kingC.x, y + ch * 0.5f, kingC.z);
                rel.AddRoom(rc, new Vector3(KingEW, ch, KingNS), rg, false, false, false, false);
                y += ch + 0.55f;
            }
            SpawnMesh(parent, "Khufu_Relieving", rel.Build("Khufu_Relieving"), granite, true);
        }

        static void BuildAirShafts(Transform parent, Vector3 anteStart, Material mat)
        {
            Vector3 floor = anteStart - Vector3.up * (PassageHeightM * 0.5f);
            Vector3 kingC = new Vector3(anteStart.x, floor.y + KingH * 0.5f, anteStart.z - 3.1f - KingNS * 0.5f);
            float y = kingC.y + 0.8f;
            Vector3 n0 = new Vector3(kingC.x, y, kingC.z + KingNS * 0.5f);
            Vector3 s0 = new Vector3(kingC.x, y, kingC.z - KingNS * 0.5f);
            Vector3 n1 = n0 + new Vector3(0f, 18f, 22f);
            Vector3 s1 = s0 + new Vector3(0f, 22f, -28f);
            var b = new LabMeshBuilder(64, 96);
            Color c = new Color(0.40f, 0.36f, 0.32f, 1f);
            b.AddTunnel(n0, n1, 0.22f, 0.22f, c, 6);
            b.AddTunnel(s0, s1, 0.22f, 0.22f, c, 6);
            SpawnMesh(parent, "Khufu_AirShafts", b.Build("Khufu_AirShafts"), mat, false);
        }

        static void BuildHonestyPlate(Transform parent)
        {
            var go = new GameObject("Khufu_Honesty");
            go.transform.SetParent(parent, false);
            float z = BaseMeters * 0.5f + 8f;
            go.transform.localPosition = new Vector3(0f, 1.55f, z);
            go.transform.localRotation = Quaternion.identity;

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Plate";
            board.transform.SetParent(go.transform, false);
            board.transform.localPosition = Vector3.zero;
            board.transform.localScale = new Vector3(3.6f, 1.7f, 0.04f);
            Collider boardCol = board.GetComponent<Collider>();
            if (boardCol != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(boardCol);
                else
                    Object.DestroyImmediate(boardCol);
            }
            MeshRenderer mr = board.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = LabWorldMeshes.MakeLit("RELab_KhufuPlate", new Color(0.10f, 0.11f, 0.12f, 1f), 0.2f, 0.18f, false);

            var tmpGo = new GameObject("Text");
            tmpGo.transform.SetParent(go.transform, false);
            tmpGo.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            var tmp = tmpGo.AddComponent<TextMeshPro>();
            tmp.text = Honesty;
            tmp.fontSize = 0.12f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.86f, 0.84f, 0.76f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(3.4f, 1.55f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
        }

        static GameObject SpawnMesh(Transform parent, string name, Mesh mesh, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            if (collider && mesh != null && mesh.vertexCount > 0)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
            }
            return go;
        }
    }
}
