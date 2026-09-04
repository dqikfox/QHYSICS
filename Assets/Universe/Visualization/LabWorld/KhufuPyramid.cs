using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Khufu (Great Pyramid) undamaged 1:1 reconstruction. Tura casing ON, walkable interior.
    /// Royal cubit 0.5236 m. Not photogrammetry. Not the stripped modern ruin.
    /// Local space: origin at base centre, +Y up, +Z architectural north (entrance face).
    /// </summary>
    public static class KhufuPyramid
    {
        public const float Cubit = GizaComplex.Cubit;
        public const float BaseCubits = 440f;
        public const float HeightCubits = 280f;
        public const float BaseMeters = BaseCubits * Cubit;
        public const float HeightMeters = HeightCubits * Cubit;
        public const float SlopeDeg = 51f + 50f / 60f + 40f / 3600f;
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
        public const float GrandGalleryLengthM = 46.68f;
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
            GizaComplex.HonestyPrefix + "\n" +
            "Khufu — Great Pyramid. Tura limestone casing ON all four faces. Intact electrum pyramidion (reconstructed).\n" +
            "Royal cubit 0.5236 m. Base 440 cubits (230.38 m). Height 280 cubits (146.61 m). Seked 5.5 palms = 51° 50' 40\".\n" +
            "North-face original entrance only: 17 m up, 7.29 m east of centreline (Petrie). Passages 1.05 × 1.20 m at 26.5°. Comfort scale default OFF.\n" +
            "Descending 105 m to unfinished subterranean 14.1 × 8.3 × 3.5 m. Junction 28.2 m; well shaft Grand Gallery → descending; ascending 37.76 m.\n" +
            "Queen's chamber 5.8 × 5.3 × 6.2 m gabled, east-wall niche, horizontal 38.15 m. Grand Gallery 46.68 × 8.6 × 2.1 m corbelled.\n" +
            "Antechamber with 3 portcullis slots. King's chamber 20 × 10 cubits (10.47 × 5.24 × 5.84 m) granite, lidless coffer, 5 intact relieving chambers. Air shafts thin channels.";

        public static GameObject Build(Transform parent, Vector3 worldBaseCenter, Quaternion worldRot, bool comfortScale)
        {
            GameObject root = GizaBuild.Root(RootName, parent, worldBaseCenter, worldRot);

            float scale = comfortScale ? 1.6f : 1f;
            float pw = PassageWidthM * scale;
            float ph = PassageHeightM * scale;
            float capH = PyramidionCubits * Cubit;
            float half = BaseMeters * 0.5f;
            float zFace = GizaBuild.FaceZ(half, HeightMeters, EntranceHeightM);
            float holeW = PassageWidthM * 1.55f;
            float holeH = PassageHeightM * 1.65f;

            Material tura = GizaBuild.TuraCasing();
            Material lime = GizaBuild.InteriorLime();
            Material granite = GizaBuild.Granite();
            Material pavement = GizaBuild.Pavement();
            Material gold = GizaBuild.Electrum();
            Material emit = GizaBuild.Emit();

            GizaBuild.Casing(root.transform, "Khufu_Casing", BaseMeters, HeightMeters, tura,
                true, EntranceEastOffsetM, EntranceHeightM, holeW, holeH, capH);
            GizaBuild.Pyramidion(root.transform, "Khufu_Pyramidion", BaseMeters, HeightMeters, capH, gold);
            GizaBuild.PavementRing(root.transform, "Khufu_Pavement", BaseMeters, PavementWidthM, pavement);
            GizaBuild.EntranceLedge(root.transform, "Khufu_Ledge", EntranceEastOffsetM, EntranceHeightM, zFace, pw, ph, pavement);
            BuildInterior(root.transform, lime, granite, emit, pw, ph);
            GizaBuild.HonestyPlate(root.transform, "Khufu_Honesty", Honesty, BaseMeters);
            return root;
        }

        static void BuildInterior(Transform parent, Material lime, Material granite, Material emit, float pw, float ph)
        {
            float half = BaseMeters * 0.5f;
            float zC = GizaBuild.FaceZ(half, HeightMeters, EntranceHeightM);
            Vector3 entrance = new Vector3(EntranceEastOffsetM, EntranceHeightM, zC);
            Vector3 descDir = GizaBuild.PassageDir(PassageAngleDeg, true);
            Vector3 ascDir = GizaBuild.PassageDir(PassageAngleDeg, false);

            Vector3 mouth = entrance - descDir * 1.8f;
            Vector3 junction = entrance + descDir * JunctionFromEntranceM;
            Vector3 subEnd = entrance + descDir * DescendLengthM;
            Vector3 ascEnd = junction + ascDir * AscendLengthM;
            Vector3 ggEnd = ascEnd + ascDir * GrandGalleryLengthM;
            Vector3 queenDoor = new Vector3(ascEnd.x, ascEnd.y, ascEnd.z - QueenPassageM);

            var passages = new LabMeshBuilder(9000, 27000);
            Color stone = new Color(0.72f, 0.68f, 0.60f, 1f);
            Color dim = new Color(0.55f, 0.52f, 0.46f, 1f);
            passages.AddTunnel(mouth, subEnd, pw, ph, stone, 18);
            passages.AddTunnel(junction, ascEnd, pw, ph, stone, 10);
            passages.AddTunnel(ascEnd, queenDoor, pw, ph, dim, 8);
            AddGrandGallery(passages, ascEnd, ggEnd, pw, ph, stone);
            AddWellShaft(passages, ascEnd, entrance, descDir, pw, ph, dim);
            Vector3 anteStart = ggEnd;
            Vector3 anteEnd = ggEnd + new Vector3(0f, 0f, -2.1f);
            passages.AddTunnel(anteStart, anteEnd, pw, ph, stone, 2);
            GizaBuild.SpawnMesh(parent, "Khufu_Passages", passages.Build("Khufu_Passages"), lime, true);

            var emitMesh = new LabMeshBuilder(320, 640);
            GizaBuild.CeilingStrip(emitMesh, mouth, subEnd, pw * 0.15f, ph);
            GizaBuild.CeilingStrip(emitMesh, junction, ggEnd, pw * 0.15f, GrandGalleryHeightM);
            GizaBuild.SpawnMesh(parent, "Khufu_Emit", emitMesh.Build("Khufu_Emit"), emit, false);

            BuildSubterranean(parent, subEnd, pw, ph, lime);
            BuildQueenChamber(parent, queenDoor, pw, ph, lime);
            BuildKingSuite(parent, anteEnd, pw, ph, granite);
            BuildAirShafts(parent, anteEnd, lime);
        }

        static void AddWellShaft(LabMeshBuilder b, Vector3 ggStart, Vector3 entrance, Vector3 descDir, float pw, float ph, Color color)
        {
            Vector3 top = new Vector3(ggStart.x - pw * 0.65f, ggStart.y, ggStart.z);
            float ca = Mathf.Max(0.2f, Mathf.Abs(descDir.z));
            float t = (entrance.z - top.z) / ca;
            t = Mathf.Clamp(t, JunctionFromEntranceM + 2f, DescendLengthM - 4f);
            Vector3 onDesc = entrance + descDir * t;
            Vector3 bottom = new Vector3(onDesc.x - pw * 0.15f, onDesc.y, onDesc.z);
            b.AddTunnel(top, bottom, pw * 0.85f, ph, color, 10);
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

        static void BuildSubterranean(Transform parent, Vector3 passageEnd, float pw, float ph, Material mat)
        {
            Vector3 floor = passageEnd - Vector3.up * (ph * 0.5f);
            Vector3 center = new Vector3(passageEnd.x, floor.y + SubH * 0.5f, passageEnd.z - SubNS * 0.5f);
            var b = new LabMeshBuilder(64, 96);
            Color c = new Color(0.50f, 0.46f, 0.40f, 1f);
            b.AddRoom(center, new Vector3(SubEW, SubH, SubNS), c, false, true, false, false);
            var pit = new Vector3(center.x, floor.y - 0.4f, center.z);
            b.AddRoom(pit, new Vector3(2.5f, 0.8f, 2.5f), c, false, false, false, false);
            GizaBuild.SpawnMesh(parent, "Khufu_Subterranean", b.Build("Khufu_Subterranean"), mat, true);
        }

        static void BuildQueenChamber(Transform parent, Vector3 door, float pw, float ph, Material mat)
        {
            Vector3 floor = door - Vector3.up * (ph * 0.5f);
            float wallH = 4.5f;
            Vector3 floorC = new Vector3(door.x, floor.y, door.z - QueenNS * 0.5f);
            Vector3 center = floorC + Vector3.up * (wallH * 0.5f);
            var b = new LabMeshBuilder(128, 220);
            Color c = new Color(0.70f, 0.66f, 0.58f, 1f);
            b.AddRoom(center, new Vector3(QueenEW, wallH, QueenNS), c, false, true, false, false);
            b.AddGableRoof(floorC, QueenEW, QueenNS, wallH, QueenPeakH, c);
            Vector3 nicheC = new Vector3(center.x + QueenEW * 0.5f + 0.52f, floor.y + 2.33f, center.z);
            b.AddRoom(nicheC, new Vector3(1.04f, 4.67f, 1.57f), c, false, false, true, false);
            GizaBuild.SpawnMesh(parent, "Khufu_QueenChamber", b.Build("Khufu_QueenChamber"), mat, true);
        }

        static void BuildKingSuite(Transform parent, Vector3 anteStart, float pw, float ph, Material granite)
        {
            Vector3 floor = anteStart - Vector3.up * (ph * 0.5f);
            float anteL = 3.1f;
            float anteW = 1.65f;
            float anteH = 3.7f;
            Vector3 anteC = new Vector3(anteStart.x, floor.y + anteH * 0.5f, anteStart.z - anteL * 0.5f);
            var ante = new LabMeshBuilder(96, 160);
            Color g = new Color(0.32f, 0.26f, 0.26f, 1f);
            ante.AddRoom(anteC, new Vector3(anteW, anteH, anteL), g, true, true, false, false);
            for (int i = 0; i < 3; i++)
            {
                float z = anteStart.z - (i + 1) * (anteL / 4f);
                Vector3 west = new Vector3(anteC.x - anteW * 0.5f - 0.12f, anteC.y, z);
                Vector3 east = new Vector3(anteC.x + anteW * 0.5f + 0.12f, anteC.y, z);
                ante.AddRoom(west, new Vector3(0.24f, anteH * 0.92f, 0.38f), g, false, false, false, true);
                ante.AddRoom(east, new Vector3(0.24f, anteH * 0.92f, 0.38f), g, false, false, true, false);
            }
            GizaBuild.SpawnMesh(parent, "Khufu_Antechamber", ante.Build("Khufu_Antechamber"), granite, true);

            Vector3 kingDoor = new Vector3(anteStart.x, anteStart.y, anteStart.z - anteL);
            Vector3 kingFloor = new Vector3(kingDoor.x, floor.y, kingDoor.z - KingNS * 0.5f);
            Vector3 kingC = kingFloor + Vector3.up * (KingH * 0.5f);
            var kc = new LabMeshBuilder(48, 72);
            kc.AddRoom(kingC, new Vector3(KingEW, KingH, KingNS), g, false, true, false, false);
            GizaBuild.SpawnMesh(parent, "Khufu_KingChamber", kc.Build("Khufu_KingChamber"), granite, true);

            var sarc = new LabMeshBuilder(32, 48);
            Vector3 sarcC = new Vector3(kingC.x - 2.6f, floor.y + 0.525f, kingC.z);
            sarc.AddBox(sarcC, new Vector3(2.28f, 1.05f, 0.98f), g);
            sarc.AddRoom(sarcC + Vector3.up * 0.08f, new Vector3(1.98f, 0.72f, 0.68f), g, false, false, false, false);
            GizaBuild.SpawnMesh(parent, "Khufu_Sarcophagus", sarc.Build("Khufu_Sarcophagus"), granite, true);

            var rel = new LabMeshBuilder(220, 360);
            Color rg = new Color(0.30f, 0.24f, 0.24f, 1f);
            float y = floor.y + KingH;
            for (int i = 0; i < 5; i++)
            {
                float ch = i == 4 ? 1.8f : 1.15f;
                rel.AddBox(new Vector3(kingC.x, y + 0.22f, kingC.z), new Vector3(KingEW, 0.44f, KingNS), rg);
                y += 0.44f;
                Vector3 rc = new Vector3(kingC.x, y + ch * 0.5f, kingC.z);
                rel.AddRoom(rc, new Vector3(KingEW, ch, KingNS), rg, false, false, false, false);
                y += ch;
            }
            Vector3 topFloor = new Vector3(kingC.x, y, kingC.z);
            rel.AddGableRoof(topFloor, KingEW, KingNS, 0f, 1.6f, rg);
            GizaBuild.SpawnMesh(parent, "Khufu_Relieving", rel.Build("Khufu_Relieving"), granite, true);
            BuildRelievingGraffiti(parent, kingC, floor.y + KingH, KingEW, KingNS);
        }

        /// <summary>
        /// Petrie/Vyse-attested quarry marks / crew graffiti in the relieving chambers above the King's Chamber.
        /// Latin transliteration + cartouche note only (TMP LiberationSans lacks Egyptian Hieroglyphs).
        /// Names verified from standard Egyptology: Friends of Khufu; White Crown of Khnum-Khufu is Powerful.
        /// Schematic placement on honesty plaques next to the relieving stack — not invented glyph sequences.
        /// </summary>
        static void BuildRelievingGraffiti(Transform parent, Vector3 kingC, float relBaseY, float ew, float ns)
        {
            // Marker for force-rebuild when graffiti missing on older Khufu roots.
            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(kingC.x, relBaseY + 0.2f, kingC.z), new Vector3(0.35f, 0.12f, 0.35f), Color.white);
            GizaBuild.SpawnMesh(parent, "Khufu_RelievingGraffiti", mark.Build("Khufu_RelievingGraffiti"),
                GizaBuild.Pavement(), false);

            // Attested gang names (Vyse 1837 / Petrie documentation of relieving-chamber red ochre marks).
            const string friends =
                "Petrie relieving-chamber marks — schematic placement.\n" +
                "Attested crew graffiti (red ochre quarry marks), Vyse/Petrie:\n" +
                "Friends of Khufu\n" +
                "Transliteration: smrw nw Hwfw (Friends of Khufu)\n" +
                "Cartouche of Khufu: Hwfw / translit. khwfw (Latin only — TMP has no Egyptian Hieroglyphs font).\n" +
                "NOT invented glyph sequences. Placement schematic next to relieving stack.";
            const string whiteCrown =
                "Petrie relieving-chamber marks — schematic placement.\n" +
                "Attested gang name in Khufu relieving chambers (standard Egyptology / Vyse finds):\n" +
                "The White Crown of Khnum-Khufu is Powerful\n" +
                "Transliteration: hedjet khnemu-khwfw (White Crown of Khnum-Khufu)\n" +
                "Cartouche of Khufu: Hwfw / translit. khwfw (Latin transliteration only).\n" +
                "NOT invented. Schematic plaque beside granite relieving chambers.";

            GizaBuild.HonestyPlate(parent, "Khufu_Graffiti_FriendsOfKhufu", friends, 8f);
            Transform f = parent.Find("Khufu_Graffiti_FriendsOfKhufu");
            if (f != null)
            {
                f.localPosition = new Vector3(kingC.x + ew * 0.5f + 1.8f, relBaseY + 1.2f, kingC.z);
                f.localRotation = Quaternion.Euler(0f, 90f, 0f);
                f.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            }

            GizaBuild.HonestyPlate(parent, "Khufu_Graffiti_WhiteCrown", whiteCrown, 8f);
            Transform w = parent.Find("Khufu_Graffiti_WhiteCrown");
            if (w != null)
            {
                w.localPosition = new Vector3(kingC.x - ew * 0.5f - 1.8f, relBaseY + 2.4f, kingC.z);
                w.localRotation = Quaternion.Euler(0f, -90f, 0f);
                w.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            }
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
            GizaBuild.SpawnMesh(parent, "Khufu_AirShafts", b.Build("Khufu_AirShafts"), mat, false);
        }
    }
}
