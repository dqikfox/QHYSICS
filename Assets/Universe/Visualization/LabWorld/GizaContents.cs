using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Attested Giza tomb / temple contents (Reisner, Lehner/ARCE, Petrie/Vyse).
    /// Honesty rules: physical props only when found in situ; robbed chambers stay empty of treasure;
    /// reconstructed kit clearly labeled RECONSTRUCTION; speculative overlays OFF by default.
    /// </summary>
    public static class GizaContents
    {
        public const string HetepheresName = "Hetepheres";
        public const string MeresankhName = "Meresankh";
        public const string AmenhotepIIName = "AmenhotepIITemple";
        public const string BigVoidName = "Khufu_ScanPyramidsBigVoid";
        public const string KhufuShipName = "KhufuShip_Reconstruction";
        public const string ContentsMarker = "_ContentsHonesty";
        public const string MassingMarker = "_Massing";

        // Hetepheres I shaft G 7000 X — SE of Khufu, east of queens (Reisner / Lehner schematic).
        public const float HetepheresEastOfKhufuHalfM = 48f;
        public const float HetepheresSouthOfKhufuM = 38f;
        public const float HetepheresShaftWM = 4.2f;
        public const float HetepheresShaftDepthM = 27f;

        // Meresankh III G 7530-7540 — East Field south of Ankhhaf (Lehner schematic).
        public const float MeresankhWestInsetM = 28f;
        public const float MeresankhNorthFrac = 0.38f;
        public const float MeresankhBodyEW = 36f;
        public const float MeresankhBodyNS = 18f;
        public const float MeresankhBodyHM = 8.2f;

        // Amenhotep II temple (New Kingdom) north of Sphinx court — labeled NEW KINGDOM.
        public const float AmenhotepNorthOfSphinxM = 42f;
        public const float AmenhotepEWM = 22f;
        public const float AmenhotepNSM = 16f;

        public static void ExpandExtents(ref float xMin, ref float xMax, ref float zMin, ref float zMax)
        {
            LayoutHetepheres(out float hE, out float hN);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, hE, hN, 18f, 18f);
            LayoutMeresankh(out float mE, out float mN);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, mE, mN,
                MeresankhBodyEW * 0.5f + 16f, MeresankhBodyNS * 0.5f + 14f);
            LayoutAmenhotep(out float aE, out float aN);
            Enc(ref xMin, ref xMax, ref zMin, ref zMax, aE, aN, AmenhotepEWM * 0.5f + 6f, AmenhotepNSM * 0.5f + 6f);
        }

        static void Enc(ref float xMin, ref float xMax, ref float zMin, ref float zMax,
            float east, float north, float rE, float rN)
        {
            xMin = Mathf.Min(xMin, east - rE);
            xMax = Mathf.Max(xMax, east + rE);
            zMin = Mathf.Min(zMin, north - rN);
            zMax = Mathf.Max(zMax, north + rN);
        }

        public static void ForceRebuildAll()
        {
            DestroyNamed(GizaComplex.FindNamed(HetepheresName));
            DestroyNamed(GizaComplex.FindNamed(MeresankhName));
            DestroyNamed(GizaComplex.FindNamed(AmenhotepIIName));
            DestroyNamed(GizaComplex.FindNamed(BigVoidName));
            DestroyNamed(GizaComplex.FindNamed(KhufuShipName));
        }

        public static void EnsureAll(GizaComplex.Pose pose)
        {
            EnsureHetepheres(pose);
            EnsureMeresankh(pose);
            EnsureAmenhotepII(pose);
            EnsureBigVoidOverlay(pose);
            EnsureKhufuShipReconstruction(pose);
        }

        public static void EnsureHetepheres(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(HetepheresName);
            if (old != null && (old.transform.Find(HetepheresName + ContentsMarker) == null
                || old.transform.Find(HetepheresName + "_Furniture") == null))
                DestroyNamed(old);
            Ensure(HetepheresName, pose, BuildHetepheres, pose.surfaceY, true);
        }

        public static void EnsureMeresankh(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(MeresankhName);
            if (old != null && (old.transform.Find(MeresankhName + ContentsMarker) == null
                || old.transform.Find(MeresankhName + "_Sarcophagus") == null))
                DestroyNamed(old);
            Ensure(MeresankhName, pose, BuildMeresankh, pose.surfaceY, true);
        }

        public static void EnsureAmenhotepII(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(AmenhotepIIName);
            if (old != null && old.transform.Find(AmenhotepIIName + ContentsMarker) == null)
                DestroyNamed(old);
            Ensure(AmenhotepIIName, pose, BuildAmenhotepII, GizaComplex.CourtY(pose), true);
        }

        public static void EnsureBigVoidOverlay(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(BigVoidName);
            if (old != null && old.transform.Find(BigVoidName + "_Honesty") == null)
                DestroyNamed(old);
            Ensure(BigVoidName, pose, BuildBigVoid, pose.surfaceY, true);
        }

        public static void EnsureKhufuShipReconstruction(GizaComplex.Pose pose)
        {
            GameObject old = GizaComplex.FindNamed(KhufuShipName);
            if (old != null && old.transform.Find(KhufuShipName + ContentsMarker) == null)
                DestroyNamed(old);
            Ensure(KhufuShipName, pose, BuildKhufuShip, pose.surfaceY, true);
        }

        /// <summary>
        /// Place Khafre valley-temple diorite statue replica (attested find, now museum).
        /// Called from GizaPrecinct after valley temple Ensure.
        /// </summary>
        public static void EnsureKhafreDioriteStatue(Transform valleyRoot)
        {
            if (valleyRoot == null)
                return;
            if (valleyRoot.Find("Khafre_DioriteStatue_Replica") != null)
                return;

            Material diorite = LabWorldMeshes.MakeLit("RELab_KhafreDiorite",
                new Color(0.22f, 0.24f, 0.28f, 1f), 0.12f, 0.28f, false);
            var body = new LabMeshBuilder(48, 72);
            Color c = Color.white;
            // Seated king schematic on pedestal — not a portrait scan.
            body.AddBox(new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 1.4f), c);
            body.AddBox(new Vector3(0f, 1.35f, 0.1f), new Vector3(0.95f, 1.3f, 0.85f), c);
            body.AddBox(new Vector3(0f, 2.35f, 0.15f), new Vector3(0.55f, 0.7f, 0.55f), c);
            body.AddBox(new Vector3(0f, 0.95f, 0.55f), new Vector3(0.7f, 0.55f, 0.9f), c);
            GameObject go = GizaBuild.SpawnMesh(valleyRoot, "Khafre_DioriteStatue_Replica",
                body.Build("Khafre_DioriteStatue_Replica"), diorite, true);
            if (go != null)
                go.transform.localPosition = new Vector3(-4.5f, 0.4f, 0f);

            const string honesty =
                "ATTESTED FIND (now museum) — replica.\n" +
                "Khafre seated statue in diorite. Famous find from Khafre Valley Temple (Mariette 1860).\n" +
                "Original in Egyptian Museum, Cairo (CG 14 / JE 10062). This mesh is a schematic replica on an empty niche pedestal.\n" +
                "NOT the museum original. NOT photogrammetry. Empty niches elsewhere remain empty (colossi missing).";
            GizaBuild.HonestyPlate(valleyRoot, "Khafre_DioriteStatue_Honesty", honesty, 10f);
            Transform plate = valleyRoot.Find("Khafre_DioriteStatue_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(-4.5f, 1.7f, 2.8f);
                plate.localRotation = Quaternion.Euler(0f, 180f, 0f);
                plate.localScale = new Vector3(0.65f, 0.65f, 0.65f);
            }
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

        static void LayoutHetepheres(out float east, out float north)
        {
            float khHalf = KhufuPyramid.BaseMeters * 0.5f;
            east = khHalf + HetepheresEastOfKhufuHalfM;
            north = -HetepheresSouthOfKhufuM;
        }

        static void LayoutMeresankh(out float east, out float north)
        {
            // East Field layout (mirror GizaField.LayoutEast constants).
            float khHalf = KhufuPyramid.BaseMeters * 0.5f;
            float queenEast = khHalf + 8f + 28f; // approx G1 queens east of Khufu face
            float east0 = queenEast + GizaField.EastFieldGapFromQueensM;
            float east1 = east0 + GizaField.EastFieldDepthM;
            float north0 = -khHalf - GizaField.EastFieldSouthPadM;
            float north1 = GizaField.EastFieldNorthPadM;
            east = east0 + MeresankhWestInsetM + MeresankhBodyEW * 0.5f;
            north = north0 + (north1 - north0) * MeresankhNorthFrac;
        }

        static void LayoutAmenhotep(out float east, out float north)
        {
            east = GizaComplex.SphinxEastM;
            north = -GizaComplex.SphinxSouthM + AmenhotepNorthOfSphinxM;
        }

        static GameObject BuildHetepheres(GizaComplex.Pose pose)
        {
            LayoutHetepheres(out float east, out float north);
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(HetepheresName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material rock = GizaBuild.Bedrock();
            Material pav = GizaBuild.Pavement();
            Material sand = GizaBuild.DesertSand();
            Material alabaster = LabWorldMeshes.MakeLit("RELab_Alabaster",
                new Color(0.92f, 0.90f, 0.84f, 1f), 0.04f, 0.22f, false);
            Material wood = LabWorldMeshes.MakeLit("RELab_GildedWood",
                new Color(0.55f, 0.38f, 0.18f, 1f), 0.08f, 0.20f, false);
            Material gold = GizaBuild.Electrum();

            float sw = HetepheresShaftWM;
            float depth = HetepheresShaftDepthM;

            var apron = new LabMeshBuilder(8, 12);
            apron.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(22f, 0.12f, 22f), Color.white);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Apron",
                apron.Build(HetepheresName + "_Apron"), sand, true);

            // Surface collar / cutting rim.
            var rim = new LabMeshBuilder(32, 48);
            rim.AddBox(new Vector3(0f, 0.35f, sw * 0.5f + 0.55f), new Vector3(sw + 2.2f, 0.7f, 1.1f), Color.white);
            rim.AddBox(new Vector3(0f, 0.35f, -(sw * 0.5f + 0.55f)), new Vector3(sw + 2.2f, 0.7f, 1.1f), Color.white);
            rim.AddBox(new Vector3(sw * 0.5f + 0.55f, 0.35f, 0f), new Vector3(1.1f, 0.7f, sw), Color.white);
            rim.AddBox(new Vector3(-(sw * 0.5f + 0.55f), 0.35f, 0f), new Vector3(1.1f, 0.7f, sw), Color.white);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Rim",
                rim.Build(HetepheresName + "_Rim"), rock, true);

            // Shaft walls (open top, walkable ledge near bottom via stairs stub).
            var shaft = new LabMeshBuilder(64, 96);
            float wallT = 0.85f;
            float hy = -depth * 0.5f;
            shaft.AddBox(new Vector3(0f, hy, sw * 0.5f - wallT * 0.5f), new Vector3(sw, depth, wallT), Color.white);
            shaft.AddBox(new Vector3(0f, hy, -(sw * 0.5f - wallT * 0.5f)), new Vector3(sw, depth, wallT), Color.white);
            shaft.AddBox(new Vector3(sw * 0.5f - wallT * 0.5f, hy, 0f), new Vector3(wallT, depth, sw - wallT * 2f), Color.white);
            shaft.AddBox(new Vector3(-(sw * 0.5f - wallT * 0.5f), hy, 0f), new Vector3(wallT, depth, sw - wallT * 2f), Color.white);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Shaft",
                shaft.Build(HetepheresName + "_Shaft"), rock, true);

            // Burial niche chamber at shaft foot (attested empty alabaster sarcophagus + furniture cache).
            float chamberY = -depth + 1.6f;
            var chamber = new LabMeshBuilder(48, 72);
            chamber.AddRoom(new Vector3(3.2f, chamberY, 0f), new Vector3(5.2f, 2.8f, 3.6f), Color.white, true, false, false, false);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Chamber",
                chamber.Build(HetepheresName + "_Chamber"), lime, true);

            // Stairs stub down west face of shaft.
            var stairs = new LabMeshBuilder(80, 120);
            int steps = 22;
            for (int i = 0; i < steps; i++)
            {
                float y = -i * (depth / steps) - 0.2f;
                float z = sw * 0.5f - 0.9f - i * 0.12f;
                stairs.AddBox(new Vector3(-sw * 0.25f, y, z), new Vector3(1.4f, 0.28f, 0.55f), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Stairs",
                stairs.Build(HetepheresName + "_Stairs"), pav, true);

            // Empty alabaster sarcophagus (Reisner — found empty; canopic equipment separate).
            var sarc = new LabMeshBuilder(24, 36);
            Vector3 sarcC = new Vector3(3.2f, chamberY - 0.9f, 0f);
            sarc.AddBox(sarcC, new Vector3(2.05f, 0.95f, 0.92f), Color.white);
            sarc.AddRoom(sarcC + Vector3.up * 0.08f, new Vector3(1.72f, 0.62f, 0.62f), Color.white, false, false, false, false);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Sarcophagus",
                sarc.Build(HetepheresName + "_Sarcophagus"), alabaster, true);

            // Attested furniture assemblage (Reisner) — physical props found in shaft (now museum; schematic here).
            var furn = new LabMeshBuilder(96, 144);
            // Canopic chest
            furn.AddBox(new Vector3(1.4f, chamberY - 1.05f, 1.1f), new Vector3(0.72f, 0.7f, 0.72f), Color.white);
            // Bed frame
            furn.AddBox(new Vector3(4.6f, chamberY - 1.25f, -0.9f), new Vector3(2.0f, 0.18f, 0.95f), Color.white);
            furn.AddBox(new Vector3(3.75f, chamberY - 1.05f, -0.9f), new Vector3(0.12f, 0.45f, 0.12f), Color.white);
            furn.AddBox(new Vector3(5.45f, chamberY - 1.05f, -0.9f), new Vector3(0.12f, 0.45f, 0.12f), Color.white);
            furn.AddBox(new Vector3(3.75f, chamberY - 1.05f, -0.45f), new Vector3(0.12f, 0.45f, 0.12f), Color.white);
            furn.AddBox(new Vector3(5.45f, chamberY - 1.05f, -0.45f), new Vector3(0.12f, 0.45f, 0.12f), Color.white);
            // Chair
            furn.AddBox(new Vector3(1.6f, chamberY - 1.15f, -1.1f), new Vector3(0.55f, 0.12f, 0.5f), Color.white);
            furn.AddBox(new Vector3(1.6f, chamberY - 0.75f, -1.28f), new Vector3(0.55f, 0.7f, 0.1f), Color.white);
            // Canopy posts + beams (schematic carrying-chair / canopy frame)
            furn.AddBox(new Vector3(4.5f, chamberY - 0.2f, 1.0f), new Vector3(0.1f, 2.0f, 0.1f), Color.white);
            furn.AddBox(new Vector3(5.5f, chamberY - 0.2f, 1.0f), new Vector3(0.1f, 2.0f, 0.1f), Color.white);
            furn.AddBox(new Vector3(4.5f, chamberY - 0.2f, 0.2f), new Vector3(0.1f, 2.0f, 0.1f), Color.white);
            furn.AddBox(new Vector3(5.5f, chamberY - 0.2f, 0.2f), new Vector3(0.1f, 2.0f, 0.1f), Color.white);
            furn.AddBox(new Vector3(5.0f, chamberY + 0.85f, 0.6f), new Vector3(1.2f, 0.08f, 1.0f), Color.white);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Furniture",
                furn.Build(HetepheresName + "_Furniture"), wood, true);

            // Gold leaf accents on canopy (schematic)
            var gild = new LabMeshBuilder(8, 12);
            gild.AddBox(new Vector3(5.0f, chamberY + 0.92f, 0.6f), new Vector3(1.15f, 0.04f, 0.95f), Color.white);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + "_Gilding",
                gild.Build(HetepheresName + "_Gilding"), gold, false);

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + ContentsMarker,
                mark.Build(HetepheresName + ContentsMarker), pav, false);
            GizaBuild.SpawnMesh(root.transform, HetepheresName + MassingMarker,
                mark.Build(HetepheresName + MassingMarker), pav, false);

            const string honesty =
                "ATTESTED assemblage (Reisner) — schematic in situ placement.\n" +
                "Hetepheres I shaft tomb G 7000 X. Empty alabaster sarcophagus + famous furniture cache\n" +
                "(canopic chest, bed, chair, canopy/carrying frame). BEST complete Old Kingdom royal burial kit at Giza.\n" +
                "Furniture originals now in museums (Cairo / Boston MFA reconstructions from Reisner finds).\n" +
                "Shaft ~27 m schematic. NOT photogrammetry. NOT treasure invented for Khufu's King's Chamber.";
            GizaBuild.HonestyPlate(root.transform, HetepheresName + "_Honesty", honesty, 16f);
            Transform plate = root.transform.Find(HetepheresName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(8f, 1.6f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            SpawnTeleportPad(root.transform, HetepheresName + "_Teleport", new Vector3(6f, 0.1f, 4f), pav);
            return root;
        }

        static GameObject BuildMeresankh(GizaComplex.Pose pose)
        {
            LayoutMeresankh(out float east, out float north);
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(MeresankhName, pose.parent, world, pose.rot);
            Material lime = GizaBuild.InteriorLime();
            Material gran = GizaBuild.Granite();
            Material pav = GizaBuild.Pavement();
            Material sand = GizaBuild.DesertSand();
            Material mud = GizaBuild.Mudbrick();

            float bodyEW = MeresankhBodyEW;
            float bodyNS = MeresankhBodyNS;
            float bodyH = MeresankhBodyHM;
            float halfE = bodyEW * 0.5f;
            float chapelEW = 11f;
            float chapelNS = 8.5f;
            float chapelH = 4.8f;

            var apron = new LabMeshBuilder(8, 12);
            apron.AddBox(new Vector3(chapelEW * 0.35f, 0.06f, 0f),
                new Vector3(bodyEW + chapelEW + 16f, 0.12f, bodyNS + 14f), Color.white);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + "_Apron",
                apron.Build(MeresankhName + "_Apron"), sand, true);

            // Double-mastaba massing G 7530-7540.
            var body = new LabMeshBuilder(16, 24);
            body.AddBox(new Vector3(-bodyEW * 0.22f, bodyH * 0.5f, 0f),
                new Vector3(bodyEW * 0.55f, bodyH, bodyNS), Color.white);
            body.AddBox(new Vector3(bodyEW * 0.28f, bodyH * 0.45f, 0f),
                new Vector3(bodyEW * 0.42f, bodyH * 0.9f, bodyNS * 0.92f), Color.white);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + "_Body",
                body.Build(MeresankhName + "_Body"), lime, true);

            var cornice = new LabMeshBuilder(8, 12);
            cornice.AddBox(new Vector3(0f, bodyH + 0.55f, 0f),
                new Vector3(bodyEW * 0.95f, 1.1f, bodyNS * 0.88f), Color.white);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + "_Cornice",
                cornice.Build(MeresankhName + "_Cornice"), mud, true);

            float chapelX = halfE + chapelEW * 0.5f + 0.4f;
            float wallT = 0.7f;
            float doorW = 2.6f;
            float doorH = 3.0f;
            float deckY = 0f;
            float floorT = 0.28f;
            var chapel = new LabMeshBuilder(80, 120);
            chapel.AddBox(new Vector3(chapelX, deckY + floorT * 0.5f, 0f),
                new Vector3(chapelEW, floorT, chapelNS), Color.white);
            float wallY = deckY + chapelH * 0.5f;
            chapel.AddBox(new Vector3(chapelX, wallY, chapelNS * 0.5f - wallT * 0.5f),
                new Vector3(chapelEW, chapelH, wallT), Color.white);
            chapel.AddBox(new Vector3(chapelX, wallY, -(chapelNS * 0.5f - wallT * 0.5f)),
                new Vector3(chapelEW, chapelH, wallT), Color.white);
            chapel.AddBox(new Vector3(chapelX + chapelEW * 0.5f - wallT * 0.5f, wallY, 0f),
                new Vector3(wallT, chapelH, chapelNS), Color.white);
            float wing = (chapelNS - doorW) * 0.5f;
            float westX = chapelX - chapelEW * 0.5f + wallT * 0.5f;
            if (wing > 0.3f)
            {
                chapel.AddBox(new Vector3(westX, wallY, doorW * 0.5f + wing * 0.5f),
                    new Vector3(wallT, chapelH, wing), Color.white);
                chapel.AddBox(new Vector3(westX, wallY, -(doorW * 0.5f + wing * 0.5f)),
                    new Vector3(wallT, chapelH, wing), Color.white);
            }
            float lintelH = Mathf.Max(0.6f, chapelH - doorH);
            chapel.AddBox(new Vector3(westX, deckY + doorH + lintelH * 0.5f, 0f),
                new Vector3(wallT * 1.1f, lintelH, doorW + 0.8f), Color.white);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + "_Chapel",
                chapel.Build(MeresankhName + "_Chapel"), lime, true);

            var interior = new LabMeshBuilder(48, 72);
            interior.AddRoom(new Vector3(chapelX, deckY + floorT + 1.9f, 0f),
                new Vector3(chapelEW - wallT * 2f - 0.3f, 3.8f, chapelNS - wallT * 2f - 0.4f),
                Color.white, false, false, true, true);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + "_ChapelInterior",
                interior.Build(MeresankhName + "_ChapelInterior"), lime, true);

            // Black granite sarcophagus JE 54935 (attested) — schematic.
            var sarc = new LabMeshBuilder(24, 36);
            Color g = new Color(0.12f, 0.12f, 0.14f, 1f);
            Vector3 sarcC = new Vector3(chapelX + 1.2f, deckY + floorT + 0.5f, 0f);
            sarc.AddBox(sarcC, new Vector3(2.15f, 1.0f, 0.95f), g);
            sarc.AddRoom(sarcC + Vector3.up * 0.1f, new Vector3(1.8f, 0.65f, 0.65f), g, false, false, false, false);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + "_Sarcophagus",
                sarc.Build(MeresankhName + "_Sarcophagus"), gran, true);

            // Chapel statues / relief stubs (attested rock-cut statue niches — schematic massing).
            var statues = new LabMeshBuilder(40, 60);
            for (int i = 0; i < 4; i++)
            {
                float z = Mathf.Lerp(-2.4f, 2.4f, i / 3f);
                statues.AddBox(new Vector3(chapelX + chapelEW * 0.5f - wallT - 0.55f, deckY + floorT + 1.4f, z),
                    new Vector3(0.55f, 2.6f, 0.5f), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, MeresankhName + "_ChapelStatues",
                statues.Build(MeresankhName + "_ChapelStatues"), lime, true);

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), Color.white);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + ContentsMarker,
                mark.Build(MeresankhName + ContentsMarker), pav, false);
            GizaBuild.SpawnMesh(root.transform, MeresankhName + MassingMarker,
                mark.Build(MeresankhName + MassingMarker), pav, false);

            const string honesty =
                "ATTESTED (Reisner / Egyptian Museum).\n" +
                "Meresankh III mastaba G 7530-7540 (Eastern Cemetery). Black granite sarcophagus JE 54935.\n" +
                "Chapel rock-cut statues / reliefs attested — schematic massing here, not photogrammetry.\n" +
                "Walkable east chapel. Double-mastaba body. NOT invented treasure.";
            GizaBuild.HonestyPlate(root.transform, MeresankhName + "_Honesty", honesty, 18f);
            Transform plate = root.transform.Find(MeresankhName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(halfE + chapelEW + 3f, 1.55f, chapelNS * 0.5f + 2f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            SpawnTeleportPad(root.transform, MeresankhName + "_Teleport",
                new Vector3(chapelX - 2f, 0.1f, chapelNS * 0.5f + 2f), pav);
            return root;
        }

        static GameObject BuildAmenhotepII(GizaComplex.Pose pose)
        {
            LayoutAmenhotep(out float east, out float north);
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(AmenhotepIIName, pose.parent, world, pose.rot);
            Material mud = GizaBuild.Mudbrick();
            Material pav = GizaBuild.Pavement();
            Material lime = GizaBuild.InteriorLime();

            float ew = AmenhotepEWM;
            float ns = AmenhotepNSM;
            float wallH = 4.2f;
            float wallT = 0.9f;

            var floor = new LabMeshBuilder(8, 12);
            floor.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(ew, 0.3f, ns), Color.white);
            GizaBuild.SpawnMesh(root.transform, AmenhotepIIName + "_Floor",
                floor.Build(AmenhotepIIName + "_Floor"), pav, true);

            var walls = new LabMeshBuilder(32, 48);
            float y = wallH * 0.5f;
            walls.AddBox(new Vector3(0f, y, ns * 0.5f - wallT * 0.5f), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(0f, y, -(ns * 0.5f - wallT * 0.5f)), new Vector3(ew, wallH, wallT), Color.white);
            walls.AddBox(new Vector3(ew * 0.5f - wallT * 0.5f, y, 0f), new Vector3(wallT, wallH, ns), Color.white);
            // South open toward Sphinx — door gap.
            float doorW = 4.5f;
            float wing = (ew - doorW) * 0.5f;
            if (wing > 0.4f)
            {
                walls.AddBox(new Vector3(-(doorW * 0.5f + wing * 0.5f), y, -(ns * 0.5f - wallT * 0.5f)),
                    new Vector3(wing, wallH, wallT), Color.white);
                walls.AddBox(new Vector3(doorW * 0.5f + wing * 0.5f, y, -(ns * 0.5f - wallT * 0.5f)),
                    new Vector3(wing, wallH, wallT), Color.white);
            }
            GizaBuild.SpawnMesh(root.transform, AmenhotepIIName + "_Walls",
                walls.Build(AmenhotepIIName + "_Walls"), mud, true);

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(0.4f, 0.2f, 0.4f), Color.white);
            GizaBuild.SpawnMesh(root.transform, AmenhotepIIName + ContentsMarker,
                mark.Build(AmenhotepIIName + ContentsMarker), lime, false);

            const string honesty =
                "NEW KINGDOM — labeled later monument.\n" +
                "Amenhotep II temple north of the Sphinx (schematic footprint).\n" +
                "Not Old Kingdom. Not part of Khafre/Sphinx temple pair. Not photogrammetry.";
            GizaBuild.HonestyPlate(root.transform, AmenhotepIIName + "_Honesty", honesty, 12f);
            Transform plate = root.transform.Find(AmenhotepIIName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(0f, 1.55f, ns * 0.5f + 3f);
                plate.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            return root;
        }

        static GameObject BuildBigVoid(GizaComplex.Pose pose)
        {
            // ScanPyramids Big Void — speculative; root active for honesty, solid OFF.
            Vector3 world = GizaComplex.WorldFromKhufu(pose, 0f, 20f, 50f);
            GameObject root = GizaBuild.Root(BigVoidName, pose.parent, world, pose.rot);

            const string honesty =
                "SPECULATIVE — ScanPyramids Big Void. Honesty overlay OFF by default (like thermal/GPR).\n" +
                "Muon tomography anomaly above Grand Gallery (2017+). NOT excavated. NOT a treasure chamber.\n" +
                "Enable child SpeculativeSolid in Hierarchy to view schematic box. NOT attested architecture.";
            GizaBuild.HonestyPlate(root.transform, BigVoidName + "_Honesty", honesty, 14f);
            Transform plate = root.transform.Find(BigVoidName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(0f, 2f, 8f);
                plate.localRotation = Quaternion.Euler(0f, 180f, 0f);
                plate.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            }

            var speculative = new GameObject(BigVoidName + "_SpeculativeSolid");
            speculative.transform.SetParent(root.transform, false);
            speculative.SetActive(false);
            Material glow = LabWorldMeshes.MakeLit("RELab_BigVoid", new Color(0.55f, 0.15f, 0.85f, 1f), 0.05f, 0.15f, false);
            var box = new LabMeshBuilder(8, 12);
            box.AddBox(new Vector3(0f, 0f, 0f), new Vector3(30f, 8f, 6f), Color.white);
            GizaBuild.SpawnMesh(speculative.transform, BigVoidName + "_Box",
                box.Build(BigVoidName + "_Box"), glow, false);
            return root;
        }

        static GameObject BuildKhufuShip(GizaComplex.Pose pose)
        {
            // Place near southern Khufu boat pit (east of Khufu south face).
            float khHalf = KhufuPyramid.BaseMeters * 0.5f;
            float east = 12f;
            float north = -khHalf - 18f;
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            GameObject root = GizaBuild.Root(KhufuShipName, pose.parent, world, pose.rot);
            Material wood = LabWorldMeshes.MakeLit("RELab_CedarShip",
                new Color(0.48f, 0.32f, 0.18f, 1f), 0.05f, 0.16f, false);
            Material pav = GizaBuild.Pavement();

            var hull = new LabMeshBuilder(64, 96);
            // Primitive solar barque ~43 m class (Khufu ship museum length ~43.6 m).
            float len = 43.6f;
            hull.AddBox(new Vector3(0f, 0.9f, 0f), new Vector3(3.2f, 1.4f, len), Color.white);
            hull.AddBox(new Vector3(0f, 1.7f, 0f), new Vector3(2.6f, 0.35f, len * 0.92f), Color.white);
            hull.AddBox(new Vector3(0f, 1.2f, len * 0.48f), new Vector3(1.8f, 0.9f, 2.2f), Color.white);
            hull.AddBox(new Vector3(0f, 1.2f, -len * 0.48f), new Vector3(1.6f, 0.8f, 1.8f), Color.white);
            // Deck house stub
            hull.AddBox(new Vector3(0f, 2.4f, 2f), new Vector3(2.0f, 1.4f, 8f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhufuShipName + "_Hull",
                hull.Build(KhufuShipName + "_Hull"), wood, true);

            var mark = new LabMeshBuilder(8, 12);
            mark.AddBox(new Vector3(0f, 0.1f, 0f), new Vector3(0.4f, 0.2f, 0.4f), Color.white);
            GizaBuild.SpawnMesh(root.transform, KhufuShipName + ContentsMarker,
                mark.Build(KhufuShipName + ContentsMarker), pav, false);

            const string honesty =
                "RECONSTRUCTION (attested type / excavated Khufu ship).\n" +
                "Solar barque reconstructed from the sealed pit find south of Khufu (Khufu Ship Museum / Grand Egyptian Museum).\n" +
                "Pit lids remain on KhufuBoatPits. This hull is NOT found floating here — museum reconstruction mesh.\n" +
                "Length ~43.6 m schematic. NOT treasure inventing. NOT photogrammetry of the museum hull.";
            GizaBuild.HonestyPlate(root.transform, KhufuShipName + "_Honesty", honesty, 20f);
            Transform plate = root.transform.Find(KhufuShipName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(6f, 1.6f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
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
