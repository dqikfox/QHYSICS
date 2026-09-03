using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Great Sphinx schematic massing (not a scan). ~73.5 m long, ~20 m high, limestone.
    /// East of Khafre, facing east. No interior.
    /// Offset from Khufu centre: 347 m east, 430 m south (approx. WGS84, Giza lat 30°).
    /// </summary>
    public static class GizaSphinx
    {
        public const float LengthM = 73.5f;
        public const float HeightM = 20f;
        public const float WidthM = 19.3f;
        public const string RootName = "Sphinx";

        public const string Honesty =
            GizaComplex.HonestyPrefix + "\n" +
            "Great Sphinx. Schematic body (boxes), not a scan, not photogrammetry.\n" +
            "~73.5 m long, ~20 m high, ~19.3 m wide. Limestone. Faces east. Associated with Khafre's complex.\n" +
            "Offset from Khufu centre (approx. WGS84, lat 30°): 347 m east, 430 m south. No interior.";

        public static GameObject Build(Transform parent, Vector3 worldBaseCenter, Quaternion worldRot)
        {
            GameObject root = GizaBuild.Root(RootName, parent, worldBaseCenter, worldRot);
            Material lime = GizaBuild.SphinxLime();
            var b = new LabMeshBuilder(96, 160);
            Color c = Color.white;
            float hx = LengthM * 0.5f;
            float bodyLen = 52f;
            float bodyH = 12.2f;
            float bodyW = 17.4f;
            b.AddBox(new Vector3(-4f, bodyH * 0.5f, 0f), new Vector3(bodyLen, bodyH, bodyW), c);
            b.AddBox(new Vector3(-hx + 10f, 8.5f, 0f), new Vector3(18f, 14.5f, 18.5f), c);
            b.AddBox(new Vector3(hx - 18f, 2.1f, 5.2f), new Vector3(22f, 4.2f, 5.4f), c);
            b.AddBox(new Vector3(hx - 18f, 2.1f, -5.2f), new Vector3(22f, 4.2f, 5.4f), c);
            b.AddBox(new Vector3(hx - 6.5f, 4.6f, 0f), new Vector3(10f, 3.2f, 12f), c);
            b.AddBox(new Vector3(hx - 5.5f, 15.2f, 0f), new Vector3(8.4f, 9.2f, 8.4f), c);
            b.AddBox(new Vector3(hx - 5.5f, 20.2f, 0f), new Vector3(9.6f, 1.6f, 10.2f), c);
            b.AddBox(new Vector3(hx - 9.2f, 16.4f, 0f), new Vector3(3.2f, 6.5f, 8.8f), c);
            GizaBuild.SpawnMesh(root.transform, "Sphinx_Body", b.Build("Sphinx_Body"), lime, true);
            GizaBuild.HonestyPlate(root.transform, "Sphinx_Honesty", Honesty, WidthM);
            Transform plate = root.transform.Find("Sphinx_Honesty");
            if (plate != null)
            {
                plate.localPosition = new Vector3(hx + 6f, 1.55f, 0f);
                plate.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }
    }
}
