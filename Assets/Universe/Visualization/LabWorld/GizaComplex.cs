using UnityEngine;
using TMPro;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Giza necropolis at 1:1. Offsets from Khufu centre are approx. WGS84 at lat 30°.
    /// Architectural local space: origin at Khufu base centre, +Y up, +Z north, +X east.
    /// </summary>
    public static class GizaComplex
    {
        public const float Cubit = 0.5236f;
        public const float MarginM = 80f;
        public const float KhafreWestM = 323f;
        public const float KhafreSouthM = 342f;
        public const float KhafreBedrockM = 10f;
        public const float MenkaureWestM = 563f;
        public const float MenkaureSouthM = 743f;
        public const float SphinxEastM = 347f;
        public const float SphinxSouthM = 430f;
        public const string HonestyPrefix =
            "Reconstructed original (undamaged). Published dimensions (Petrie/Lehner). Not photogrammetry. Not the stripped modern ruin.";

        [System.Flags]
        public enum Spawn
        {
            None = 0,
            Khufu = 1,
            Khafre = 2,
            Menkaure = 4,
            Sphinx = 8,
            All = Khufu | Khafre | Menkaure | Sphinx
        }

        public struct Pose
        {
            public Transform parent;
            public Vector3 khufuCenter;
            public Quaternion rot;
            public float surfaceY;
            public bool comfortScale;
        }

        public static Vector3 LocalOffset(float eastM, float northM, float upM)
        {
            return new Vector3(eastM, upM, northM);
        }

        public static Vector3 WorldFromKhufu(Pose pose, float eastM, float northM, float upM)
        {
            return pose.khufuCenter + pose.rot * LocalOffset(eastM, northM, upM);
        }

        public static void LocalExtents(out float xMin, out float xMax, out float zMin, out float zMax)
        {
            float kh = KhufuPyramid.BaseMeters * 0.5f;
            float hf = KhafrePyramid.BaseMeters * 0.5f;
            float mn = MenkaurePyramid.BaseMeters * 0.5f;
            float q = 16f;
            xMin = -MenkaureWestM - mn - 8f;
            xMax = Mathf.Max(kh, SphinxEastM + GizaSphinx.LengthM * 0.5f);
            zMin = -MenkaureSouthM - mn - 8f - 28f - q;
            zMax = kh;
        }

        public static void Ensure(Pose pose, Spawn which)
        {
            if ((which & Spawn.Khufu) != 0)
                EnsureNamed(KhufuPyramid.RootName, pose, (p) => KhufuPyramid.Build(p.parent, p.khufuCenter, p.rot, p.comfortScale), pose.surfaceY);
            if ((which & Spawn.Khafre) != 0)
            {
                Vector3 c = WorldFromKhufu(pose, -KhafreWestM, -KhafreSouthM, 0f);
                EnsureNamed(KhafrePyramid.RootName, pose, (p) => KhafrePyramid.Build(p.parent, c, p.rot, p.comfortScale), pose.surfaceY);
            }
            if ((which & Spawn.Menkaure) != 0)
            {
                Vector3 c = WorldFromKhufu(pose, -MenkaureWestM, -MenkaureSouthM, 0f);
                EnsureNamed(MenkaurePyramid.RootName, pose, (p) => MenkaurePyramid.Build(p.parent, c, p.rot, p.comfortScale), pose.surfaceY);
            }
            if ((which & Spawn.Sphinx) != 0)
            {
                Vector3 c = WorldFromKhufu(pose, SphinxEastM, -SphinxSouthM, 0f);
                EnsureNamed(GizaSphinx.RootName, pose, (p) => GizaSphinx.Build(p.parent, c, p.rot), pose.surfaceY);
            }
        }

        public static GameObject FindNamed(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            GameObject go = GameObject.Find(name);
            if (go != null)
                return go;
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i].gameObject;
            }
            return null;
        }

        static GameObject EnsureNamed(string name, Pose pose, System.Func<Pose, GameObject> build, float sitY)
        {
            GameObject existing = FindNamed(name);
            if (existing != null)
                return existing;
            GameObject go = build(pose);
            GizaBuild.SitOn(go != null ? go.transform : null, sitY);
            return go;
        }

        public static bool IsMonumentName(string lower)
        {
            if (string.IsNullOrEmpty(lower))
                return false;
            return lower == "khufu" || lower.StartsWith("khufu")
                || lower == "khafre" || lower.StartsWith("khafre")
                || lower == "menkaure" || lower.StartsWith("menkaure")
                || lower == "sphinx" || lower.StartsWith("sphinx")
                || lower == "gizacomplex" || lower.StartsWith("giza")
                || lower == "g3a" || lower == "g3b" || lower == "g3c"
                || lower.StartsWith("lablandscape");
        }
    }

    /// <summary>
    /// Shared undamaged true-pyramid casing, pyramidion, pavement, honesty plate.
    /// 4-face shells only — no filled core (walkable interiors do not clip solid rock).
    /// </summary>
    public static class GizaBuild
    {
        public static Material TuraCasing()
        {
            Material mat = LabWorldMeshes.MakeLit("RELab_TuraCasing", new Color(0.86f, 0.81f, 0.70f, 1f), 0.03f, 0.82f, false);
            Texture2D courses = LabWorldMeshes.MakeCourseTexture();
            if (mat != null && courses != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", courses);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", courses);
                mat.SetTextureScale("_BaseMap", new Vector2(1f, 10f));
                mat.SetTextureScale("_MainTex", new Vector2(1f, 10f));
            }
            return mat;
        }

        public static Material InteriorLime()
        {
            return LabWorldMeshes.MakeLit("RELab_GizaCore", new Color(0.58f, 0.53f, 0.46f, 1f), 0.04f, 0.14f, false);
        }

        public static Material Granite()
        {
            return LabWorldMeshes.MakeLit("RELab_GizaGranite", new Color(0.30f, 0.22f, 0.22f, 1f), 0.22f, 0.32f, false);
        }

        public static Material Aswan()
        {
            return LabWorldMeshes.MakeLit("RELab_AswanGranite", new Color(0.46f, 0.24f, 0.20f, 1f), 0.28f, 0.38f, false);
        }

        public static Material Electrum()
        {
            return LabWorldMeshes.MakeLit("RELab_Pyramidion", new Color(0.78f, 0.66f, 0.32f, 1f), 0.82f, 0.62f, false);
        }

        public static Material Pavement()
        {
            return LabWorldMeshes.MakeLit("RELab_GizaPavement", new Color(0.72f, 0.68f, 0.58f, 1f), 0.06f, 0.22f, false);
        }

        public static Material Bedrock()
        {
            return LabWorldMeshes.MakeLit("RELab_GizaBedrock", new Color(0.42f, 0.38f, 0.32f, 1f), 0.05f, 0.12f, false);
        }

        public static Material Emit()
        {
            Material emit = LabWorldMeshes.MakeLit("RELab_GizaEmit", new Color(0.22f, 0.20f, 0.16f, 1f), 0.0f, 0.08f, true);
            if (emit != null && emit.HasProperty("_EmissionColor"))
                emit.SetColor("_EmissionColor", new Color(0.16f, 0.14f, 0.10f, 1f));
            return emit;
        }

        public static Material Plate()
        {
            return LabWorldMeshes.MakeLit("RELab_GizaPlate", new Color(0.10f, 0.11f, 0.12f, 1f), 0.2f, 0.18f, false);
        }

        public static void Casing(Transform parent, string name, float baseM, float heightM, Material mat,
            bool northDoor, float doorX, float doorY, float doorW, float doorH, float capH,
            float door2X = 0f, float door2Y = 0f, float door2W = 0f, float door2H = 0f)
        {
            float half = baseM * 0.5f;
            float tCap = capH > 0.05f ? 1f - Mathf.Clamp01(capH / heightM) : 1f;
            var b = new LabMeshBuilder(2800, 8400);
            b.AddPyramidCasing(half, heightM, Color.white, 0f, tCap, 3, 4, northDoor, doorX, doorY, doorW, doorH, door2X, door2Y, door2W, door2H);
            SpawnMesh(parent, name, b.Build(name), mat, true);
        }

        public static void CasingBand(Transform parent, string name, float baseM, float heightM, Material mat,
            float t0, float t1, bool northDoor, float doorX, float doorY, float doorW, float doorH, int uDiv, int vDiv)
        {
            float half = baseM * 0.5f;
            var b = new LabMeshBuilder(2400, 7200);
            b.AddPyramidCasing(half, heightM, Color.white, t0, t1, uDiv, vDiv, northDoor, doorX, doorY, doorW, doorH);
            SpawnMesh(parent, name, b.Build(name), mat, true);
        }

        public static void Pyramidion(Transform parent, string name, float baseM, float heightM, float capH, Material mat)
        {
            capH = Mathf.Max(0.4f, capH);
            float y0 = heightM - capH;
            float half0 = (baseM * 0.5f) * (capH / heightM);
            var b = new LabMeshBuilder(16, 24);
            Color c = Color.white;
            Vector3 a = new Vector3(-half0, y0, half0);
            Vector3 br = new Vector3(half0, y0, half0);
            Vector3 cr = new Vector3(half0, y0, -half0);
            Vector3 d = new Vector3(-half0, y0, -half0);
            Vector3 apex = new Vector3(0f, heightM, 0f);
            b.AddTri(a, br, apex, Vector3.forward, Vector2.zero, Vector2.right, Vector2.one, c);
            b.AddTri(br, cr, apex, Vector3.right, Vector2.zero, Vector2.right, Vector2.one, c);
            b.AddTri(cr, d, apex, Vector3.back, Vector2.zero, Vector2.right, Vector2.one, c);
            b.AddTri(d, a, apex, Vector3.left, Vector2.zero, Vector2.right, Vector2.one, c);
            SpawnMesh(parent, name, b.Build(name), mat, false);
        }

        public static void PavementRing(Transform parent, string name, float baseM, float widthM, Material mat)
        {
            float inner = baseM * 0.5f;
            float outer = inner + widthM;
            float y0 = -0.35f;
            float y1 = 0f;
            var b = new LabMeshBuilder(32, 48);
            Color c = Color.white;
            Vector3[] inn = Ring(inner, y1);
            Vector3[] outt = Ring(outer, y1);
            Vector3[] outB = Ring(outer, y0);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                b.AddQuad(inn[i], outt[i], outt[j], inn[j], Vector3.up, c);
                b.AddQuad(outt[i], outB[i], outB[j], outt[j], (outt[i] + outt[j]).normalized, c);
            }
            SpawnMesh(parent, name, b.Build(name), mat, true);
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

        public static void EntranceLedge(Transform parent, string name, float x, float y, float zFace, float pw, float ph, Material mat)
        {
            var b = new LabMeshBuilder(8, 12);
            Vector3 c = new Vector3(x, y - ph * 0.5f - 0.08f, zFace + 1.15f);
            b.AddBox(c, new Vector3(Mathf.Max(2.2f, pw * 2.2f), 0.22f, 2.2f), Color.white);
            SpawnMesh(parent, name, b.Build(name), mat, true);
        }

        public static float FaceZ(float half, float height, float y)
        {
            return half * (1f - Mathf.Clamp01(y / Mathf.Max(0.01f, height)));
        }

        public static Vector3 PassageDir(float angleDeg, bool down)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            float s = Mathf.Sin(a);
            float c = Mathf.Cos(a);
            return down ? new Vector3(0f, -s, -c) : new Vector3(0f, s, -c);
        }

        public static void CeilingStrip(LabMeshBuilder b, Vector3 start, Vector3 end, float width, float height)
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
            b.AddQuad(c0 - right * width, c0 + right * width, c1 + right * width, c1 - right * width, -up, Color.white);
        }

        public static void HonestyPlate(Transform parent, string name, string text, float baseM)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            float z = baseM * 0.5f + 8f;
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
                mr.sharedMaterial = Plate();
            else
                Debug.LogError("GizaBuild: honesty plate renderer missing.");

            var tmpGo = new GameObject("Text");
            tmpGo.transform.SetParent(go.transform, false);
            tmpGo.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            var tmp = tmpGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 0.11f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.86f, 0.84f, 0.76f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(3.4f, 1.55f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
        }

        public static GameObject SpawnMesh(Transform parent, string name, Mesh mesh, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            if (mat != null)
            {
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            else
                Debug.LogError("GizaBuild: skipped renderer on '" + name + "' (no URP Lit from RELab_Graphite).");
            if (collider && mesh != null && mesh.vertexCount > 0)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
            }
            return go;
        }

        public static void SitOn(Transform t, float surfaceY)
        {
            if (t == null)
                return;
            Renderer r = RendererNamedContains(t, "_Bedrock");
            if (r == null)
                r = RendererNamedContains(t, "_CasingGranite");
            if (r == null)
                r = RendererNamedContains(t, "_Pavement");
            if (r == null)
                r = RendererNamedContains(t, "_Casing");
            if (r == null)
                r = RendererNamedContains(t, "_Body");
            if (r == null)
            {
                Vector3 p = t.position;
                p.y = surfaceY;
                t.position = p;
                return;
            }
            t.position += Vector3.up * (surfaceY - r.bounds.min.y);
        }

        static Renderer RendererNamedContains(Transform root, string token)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Renderer r = all[i].GetComponent<Renderer>();
                if (r != null)
                    return r;
            }
            return null;
        }

        public static GameObject Root(string name, Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            return go;
        }
    }
}
