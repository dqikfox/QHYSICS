using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Great Sphinx schematic massing (not a scan). Lehner / ARCE proportions.
    /// ~73.5 m long, ~20 m high, limestone bedrock core. Faces east. No interior.
    /// Offset from Khufu centre: 347 m east, 430 m south (approx. WGS84, Giza lat 30°).
    /// </summary>
    public static class GizaSphinx
    {
        public const float LengthM = 73.5f;
        public const float HeightM = 20.22f;
        public const float WidthM = 19.3f;
        public const string RootName = "Sphinx";
        public const string DreamSteleName = "Sphinx_DreamStele";

        public const string Honesty =
            GizaComplex.HonestyPrefix + "\n" +
            "Great Sphinx. Schematic Lehner/ARCE massing (boxes + quads), not a scan, not photogrammetry.\n" +
            "~73.5 m long, ~20.2 m high, ~19.3 m wide. Limestone bedrock core. Faces east. Associated with Khafre.\n" +
            "Body, haunches, chest, nemes head, forepaws with toe pads, hind paws, tail curl. No portrait face.\n" +
            "Granite Dream Stele (Thutmose IV) between the forepaws — schematic slab, not the Cairo Museum original.\n" +
            "Offset from Khufu centre (approx. WGS84, lat 30°): 347 m east, 430 m south. No interior.";

        // Local axes: +X face/east, -X haunches/west, +Y up, ±Z north/south.
        public static GameObject Build(Transform parent, Vector3 worldBaseCenter, Quaternion worldRot)
        {
            GameObject root = GizaBuild.Root(RootName, parent, worldBaseCenter, worldRot);
            Material lime = GizaBuild.SphinxLime();
            var b = new LabMeshBuilder(220, 360);
            Color c = Color.white;

            float hx = LengthM * 0.5f; // 36.75
            // Bedrock plinth (Terrace II floor under statue).
            b.AddBox(new Vector3(0f, 0.35f, 0f), new Vector3(LengthM + 2f, 0.7f, WidthM + 3.2f), c);

            // --- Haunches / rear (west) ---
            float haunchLen = 16f;
            float haunchH = 13.6f;
            float haunchW = 19.0f;
            float haunchCx = -hx + haunchLen * 0.5f + 1.2f;
            b.AddBox(new Vector3(haunchCx, haunchH * 0.5f + 0.7f, 0f), new Vector3(haunchLen, haunchH, haunchW), c);
            // Rump dome (slightly higher centre)
            b.AddBox(new Vector3(haunchCx - 1.5f, haunchH + 0.7f + 1.1f, 0f), new Vector3(9.5f, 2.2f, 14.5f), c);

            // --- Lion torso (mid body, tapers east) ---
            float torsoLen = 28f;
            float torsoH = 11.4f;
            float torsoW = 16.8f;
            float torsoCx = haunchCx + haunchLen * 0.5f + torsoLen * 0.5f - 1.5f;
            b.AddBox(new Vector3(torsoCx, torsoH * 0.5f + 0.7f, 0f), new Vector3(torsoLen, torsoH, torsoW), c);
            // Spine ridge
            b.AddBox(new Vector3(torsoCx - 2f, torsoH + 0.7f + 0.55f, 0f), new Vector3(torsoLen - 6f, 1.1f, 6.5f), c);

            // --- Chest (rises toward neck) ---
            float chestLen = 12f;
            float chestH = 14.2f;
            float chestW = 17.6f;
            float chestCx = torsoCx + torsoLen * 0.5f + chestLen * 0.5f - 2.5f;
            b.AddBox(new Vector3(chestCx, chestH * 0.5f + 0.7f, 0f), new Vector3(chestLen, chestH, chestW), c);

            // --- Neck / collar ---
            float neckLen = 5.5f;
            float neckH = 8.5f;
            float neckW = 12.5f;
            float neckCx = chestCx + chestLen * 0.5f + neckLen * 0.5f - 1.2f;
            float neckY = 10.2f;
            b.AddBox(new Vector3(neckCx, neckY, 0f), new Vector3(neckLen, neckH, neckW), c);

            // --- Head + nemes headdress ---
            // Head mass ~10 m tall; face plane east; nemes flares west/back.
            float headW = 10.0f;
            float headD = 9.2f; // E-W depth of skull
            float headH = 9.6f;
            float headCx = neckCx + neckLen * 0.5f + headD * 0.35f;
            float headBaseY = 12.4f;
            // Skull / face block
            b.AddBox(new Vector3(headCx, headBaseY + headH * 0.5f, 0f), new Vector3(headD, headH, headW), c);
            // Nemes side lappets (flare down beside ears)
            b.AddBox(new Vector3(headCx - 0.8f, headBaseY + 4.2f, 5.6f), new Vector3(7.5f, 8.4f, 2.2f), c);
            b.AddBox(new Vector3(headCx - 0.8f, headBaseY + 4.2f, -5.6f), new Vector3(7.5f, 8.4f, 2.2f), c);
            // Nemes crown flare (wider top)
            b.AddBox(new Vector3(headCx - 0.4f, headBaseY + headH + 0.55f, 0f), new Vector3(10.4f, 1.1f, 11.8f), c);
            // Schematic face plane (flat, honest — no portrait)
            float faceX = headCx + headD * 0.5f + 0.15f;
            b.AddBox(new Vector3(faceX, headBaseY + 4.8f, 0f), new Vector3(0.35f, 7.2f, 8.6f), c);
            // Brow ridge + chin suggestion
            b.AddBox(new Vector3(faceX + 0.25f, headBaseY + 7.6f, 0f), new Vector3(0.5f, 1.0f, 7.8f), c);
            b.AddBox(new Vector3(faceX + 0.2f, headBaseY + 1.6f, 0f), new Vector3(0.45f, 1.4f, 4.2f), c);
            // Uraeus stub on forehead
            b.AddBox(new Vector3(faceX + 0.1f, headBaseY + headH + 0.9f, 0f), new Vector3(0.6f, 1.4f, 0.7f), c);

            // --- Forepaws (stretch east, in front of chest) ---
            float pawLen = 21.5f;
            float pawH = 3.8f;
            float pawW = 5.2f;
            float pawEast = hx - pawLen * 0.5f - 1.5f;
            float pawY = 0.7f + pawH * 0.5f;
            b.AddBox(new Vector3(pawEast, pawY, 5.4f), new Vector3(pawLen, pawH, pawW), c);
            b.AddBox(new Vector3(pawEast, pawY, -5.4f), new Vector3(pawLen, pawH, pawW), c);
            // Toe pads (five stubs each paw tip)
            float toeX = pawEast + pawLen * 0.5f - 1.1f;
            for (int i = -2; i <= 2; i++)
            {
                float tz = 5.4f + i * 0.95f;
                b.AddBox(new Vector3(toeX, 1.15f, tz), new Vector3(2.0f, 1.5f, 0.85f), c);
                b.AddBox(new Vector3(toeX, 1.15f, -tz), new Vector3(2.0f, 1.5f, 0.85f), c);
            }
            // Paw bridge between chest and paw roots
            b.AddBox(new Vector3(chestCx + chestLen * 0.35f, 4.2f, 5.4f), new Vector3(8f, 5.5f, 4.8f), c);
            b.AddBox(new Vector3(chestCx + chestLen * 0.35f, 4.2f, -5.4f), new Vector3(8f, 5.5f, 4.8f), c);

            // --- Hind paws under haunches ---
            b.AddBox(new Vector3(haunchCx - 2f, 2.0f, 6.2f), new Vector3(10f, 3.2f, 5.0f), c);
            b.AddBox(new Vector3(haunchCx - 2f, 2.0f, -6.2f), new Vector3(10f, 3.2f, 5.0f), c);

            // --- Tail curl on north haunch (schematic) ---
            float tailBaseX = haunchCx - 5f;
            b.AddBox(new Vector3(tailBaseX, 7.5f, 9.2f), new Vector3(4.5f, 2.2f, 2.0f), c);
            b.AddBox(new Vector3(tailBaseX - 3.5f, 9.8f, 9.2f), new Vector3(3.2f, 2.0f, 2.0f), c);
            b.AddBox(new Vector3(tailBaseX - 5.5f, 12.2f, 7.5f), new Vector3(2.4f, 2.4f, 5.5f), c);

            GizaBuild.SpawnMesh(root.transform, "Sphinx_Body", b.Build("Sphinx_Body"), lime, true);
            BuildDreamStele(root.transform, pawEast, pawLen);
            GizaBuild.HonestyPlate(root.transform, "Sphinx_Honesty", Honesty, WidthM);
            Transform plate = root.transform.Find("Sphinx_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 8f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        /// <summary>
        /// Thutmose IV Dream Stele between the forepaws (schematic granite, faces east).
        /// ~3.6 m tall reconstructed standing slab — not the museum original.
        /// </summary>
        static void BuildDreamStele(Transform parent, float pawEast, float pawLen)
        {
            Material gran = GizaBuild.Granite();
            var s = new LabMeshBuilder(24, 36);
            Color c = Color.white;
            // Sit mid-gap between paws, a few metres west of the toe tips.
            float steleX = pawEast + pawLen * 0.12f;
            const float steleH = 3.6f;
            const float steleW = 2.15f;
            const float steleT = 0.42f;
            const float baseH = 0.55f;
            // Pedestal
            s.AddBox(new Vector3(steleX, 0.7f + baseH * 0.5f, 0f), new Vector3(steleT + 0.55f, baseH, steleW + 0.7f), c);
            // Main slab (thin E-W, wide N-S, tall) — face plane toward temple / east
            s.AddBox(new Vector3(steleX, 0.7f + baseH + steleH * 0.5f, 0f), new Vector3(steleT, steleH, steleW), c);
            // Cornice / cavetto stub
            s.AddBox(new Vector3(steleX, 0.7f + baseH + steleH + 0.22f, 0f), new Vector3(steleT + 0.18f, 0.35f, steleW + 0.35f), c);
            GizaBuild.SpawnMesh(parent, DreamSteleName, s.Build(DreamSteleName), gran, true);

            const string steleHonesty =
                GizaComplex.HonestyPrefix + "\n" +
                "Dream Stele of Thutmose IV. Granite slab between the Sphinx forepaws (schematic reconstruction).\n" +
                "~3.6 m tall. Faces east toward the Sphinx temple. Not the Cairo Museum original. Not photogrammetry.";
            GizaBuild.HonestyPlate(parent, DreamSteleName + "_Honesty", steleHonesty, steleW + 4f);
            Transform plate = parent.Find(DreamSteleName + "_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(steleX + 2.4f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }
    }
}
