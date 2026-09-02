using System.Collections.Generic;
using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Runtime mesh helpers for the Giza plateau / Khufu replica. 1 unit = 1 meter.
    /// </summary>
    public static class LabWorldMeshes
    {
        const string LitShaderName = "Universal Render Pipeline/Lit";

        static Shader _lit;

        public static Shader LitShader
        {
            get
            {
                if (_lit == null)
                {
                    _lit = Shader.Find(LitShaderName);
                    if (_lit == null)
                        _lit = Shader.Find("Universal Render Pipeline/Simple Lit");
                    if (_lit == null)
                        _lit = Shader.Find("Sprites/Default");
                }
                return _lit;
            }
        }

        public static Material MakeLit(string name, Color color, float metallic, float smoothness, bool emission)
        {
            Shader sh = LitShader;
            if (sh == null)
                return null;
            var mat = new Material(sh)
            {
                name = name,
                hideFlags = HideFlags.DontSave
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 1f);
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return mat;
        }

        public static Texture2D MakeCourseTexture()
        {
            const int w = 64;
            const int h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                name = "RELab_KhufuCourses",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            var stone = new Color(0.78f, 0.73f, 0.64f, 1f);
            var course = new Color(0.62f, 0.57f, 0.50f, 1f);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                bool line = (y % 16) == 0 || (y % 16) == 15;
                Color c = line ? course : stone;
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        public static Texture2D MakePlazaTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "RELab_GizaPlaza",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            var sand = new Color(0.42f, 0.38f, 0.32f, 1f);
            var grout = new Color(0.28f, 0.26f, 0.23f, 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool grid = (x % 16 == 0) || (y % 16 == 0) || (x % 16 == 15) || (y % 16 == 15);
                    pixels[y * size + x] = grid ? grout : sand;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        public static Texture2D MakeGraphiteTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "RELab_GraphitePlaza",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            var dark = new Color(0.16f, 0.165f, 0.17f, 1f);
            var line = new Color(0.28f, 0.22f, 0.16f, 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool grid = (x % 16 == 0) || (y % 16 == 0) || (x % 16 == 15) || (y % 16 == 15);
                    pixels[y * size + x] = grid ? line : dark;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        public static Mesh BuildPlateau(float sizeX, float sizeZ, int div, float edgeLift)
        {
            int nx = Mathf.Max(4, div);
            int nz = Mathf.Max(4, div);
            var b = new LabMeshBuilder(nx * nz, (nx - 1) * (nz - 1) * 6);
            float hx = sizeX * 0.5f;
            float hz = sizeZ * 0.5f;
            var sand = new Color(0.46f, 0.41f, 0.34f, 1f);
            var edge = new Color(0.32f, 0.29f, 0.26f, 1f);
            for (int z = 0; z < nz - 1; z++)
            {
                float tz0 = z / (float)(nz - 1);
                float tz1 = (z + 1) / (float)(nz - 1);
                float z0 = Mathf.Lerp(-hz, hz, tz0);
                float z1 = Mathf.Lerp(-hz, hz, tz1);
                for (int x = 0; x < nx - 1; x++)
                {
                    float tx0 = x / (float)(nx - 1);
                    float tx1 = (x + 1) / (float)(nx - 1);
                    float x0 = Mathf.Lerp(-hx, hx, tx0);
                    float x1 = Mathf.Lerp(-hx, hx, tx1);
                    float y00 = EdgeHeight(tx0, tz0, edgeLift);
                    float y10 = EdgeHeight(tx1, tz0, edgeLift);
                    float y11 = EdgeHeight(tx1, tz1, edgeLift);
                    float y01 = EdgeHeight(tx0, tz1, edgeLift);
                    Color c00 = Color.Lerp(sand, edge, EdgeAmount(tx0, tz0));
                    Vector3 a = new Vector3(x0, y00, z0);
                    Vector3 br = new Vector3(x1, y10, z0);
                    Vector3 c = new Vector3(x1, y11, z1);
                    Vector3 d = new Vector3(x0, y01, z1);
                    Vector2 u00 = new Vector2(tx0 * 24f, tz0 * 24f);
                    Vector2 u10 = new Vector2(tx1 * 24f, tz0 * 24f);
                    Vector2 u11 = new Vector2(tx1 * 24f, tz1 * 24f);
                    Vector2 u01 = new Vector2(tx0 * 24f, tz1 * 24f);
                    b.AddQuad(a, br, c, d, Vector3.up, u00, u10, u11, u01, c00);
                }
            }
            return b.Build("RELab_GizaPlateau");
        }

        public static Mesh BuildFlatPad(float sizeX, float sizeZ, float uvScale)
        {
            var b = new LabMeshBuilder(4, 6);
            float hx = sizeX * 0.5f;
            float hz = sizeZ * 0.5f;
            Color c = Color.white;
            Vector3 a = new Vector3(-hx, 0f, -hz);
            Vector3 br = new Vector3(hx, 0f, -hz);
            Vector3 tr = new Vector3(hx, 0f, hz);
            Vector3 tl = new Vector3(-hx, 0f, hz);
            Vector2 u00 = new Vector2(0f, 0f);
            Vector2 u10 = new Vector2(uvScale, 0f);
            Vector2 u11 = new Vector2(uvScale, uvScale);
            Vector2 u01 = new Vector2(0f, uvScale);
            b.AddQuad(a, br, tr, tl, Vector3.up, u00, u10, u11, u01, c);
            return b.Build("RELab_Pad");
        }

        static float EdgeAmount(float tx, float tz)
        {
            float dx = Mathf.Abs(tx - 0.5f) * 2f;
            float dz = Mathf.Abs(tz - 0.5f) * 2f;
            float m = Mathf.Max(dx, dz);
            return Mathf.InverseLerp(0.72f, 1f, m);
        }

        static float EdgeHeight(float tx, float tz, float edgeLift)
        {
            float a = EdgeAmount(tx, tz);
            float n = Mathf.PerlinNoise(tx * 7.3f, tz * 6.1f);
            return a * a * edgeLift * (0.35f + 0.65f * n);
        }
    }

    public sealed class LabMeshBuilder
    {
        readonly List<Vector3> _v;
        readonly List<Vector2> _uv;
        readonly List<Color> _c;
        readonly List<int> _t;

        public LabMeshBuilder(int vertHint, int triHint)
        {
            _v = new List<Vector3>(vertHint);
            _uv = new List<Vector2>(vertHint);
            _c = new List<Color>(vertHint);
            _t = new List<int>(triHint);
        }

        public int VertCount => _v.Count;

        public void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector3 hintN, Vector2 ua, Vector2 ub, Vector2 uc, Color color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f)
                return;
            if (Vector3.Dot(n, hintN) < 0f)
            {
                Vector3 tmp = b;
                b = c;
                c = tmp;
                Vector2 ut = ub;
                ub = uc;
                uc = ut;
            }
            int i = _v.Count;
            _v.Add(a);
            _v.Add(b);
            _v.Add(c);
            _uv.Add(ua);
            _uv.Add(ub);
            _uv.Add(uc);
            _c.Add(color);
            _c.Add(color);
            _c.Add(color);
            _t.Add(i);
            _t.Add(i + 1);
            _t.Add(i + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hintN, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Color color)
        {
            AddTri(a, b, c, hintN, ua, ub, uc, color);
            AddTri(a, c, d, hintN, ua, uc, ud, color);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hintN, Color color)
        {
            AddQuad(a, b, c, d, hintN, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), color);
        }

        public void AddBox(Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);
            AddQuad(p000, p100, p110, p010, Vector3.back, color);
            AddQuad(p001, p011, p111, p101, Vector3.forward, color);
            AddQuad(p000, p010, p011, p001, Vector3.left, color);
            AddQuad(p100, p101, p111, p110, Vector3.right, color);
            AddQuad(p010, p110, p111, p011, Vector3.up, color);
            AddQuad(p000, p001, p101, p100, Vector3.down, color);
        }

        public void AddOpenBox(Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);
            AddQuad(p001, p000, p010, p011, Vector3.right, color);
            AddQuad(p100, p101, p111, p110, Vector3.left, color);
            AddQuad(p010, p110, p111, p011, Vector3.down, color);
            AddQuad(p000, p100, p101, p001, Vector3.up, color);
            AddQuad(p000, p010, p110, p100, Vector3.forward, color);
            AddQuad(p101, p111, p011, p001, Vector3.back, color);
        }

        /// <summary>
        /// Hollow room: inward-facing walls/floor/ceiling so the player stands inside.
        /// skipNegZ / skipPosZ leave a doorway on that face.
        /// </summary>
        public void AddRoom(Vector3 center, Vector3 size, Color color, bool skipNegZ, bool skipPosZ, bool skipNegX, bool skipPosX)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);
            AddQuad(p000, p100, p101, p001, Vector3.up, color);
            AddQuad(p010, p011, p111, p110, Vector3.down, color);
            if (!skipNegX)
                AddQuad(p000, p001, p011, p010, Vector3.right, color);
            if (!skipPosX)
                AddQuad(p100, p110, p111, p101, Vector3.left, color);
            if (!skipNegZ)
                AddQuad(p000, p010, p110, p100, Vector3.forward, color);
            if (!skipPosZ)
                AddQuad(p001, p101, p111, p011, Vector3.back, color);
        }

        public void AddTunnel(Vector3 start, Vector3 end, float width, float height, Color color, int segments)
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
            int segs = Mathf.Max(1, segments);
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs;
                float t1 = (i + 1) / (float)segs;
                Vector3 c0 = Vector3.Lerp(start, end, t0);
                Vector3 c1 = Vector3.Lerp(start, end, t1);
                Vector3 f0l = c0 - right * hw - up * hh;
                Vector3 f0r = c0 + right * hw - up * hh;
                Vector3 f0tr = c0 + right * hw + up * hh;
                Vector3 f0tl = c0 - right * hw + up * hh;
                Vector3 f1l = c1 - right * hw - up * hh;
                Vector3 f1r = c1 + right * hw - up * hh;
                Vector3 f1tr = c1 + right * hw + up * hh;
                Vector3 f1tl = c1 - right * hw + up * hh;
                float u0 = t0 * len * 0.25f;
                float u1 = t1 * len * 0.25f;
                Vector2 ua = new Vector2(u0, 0f);
                Vector2 ub = new Vector2(u1, 0f);
                Vector2 uc = new Vector2(u1, 1f);
                Vector2 ud = new Vector2(u0, 1f);
                AddQuad(f0l, f1l, f1r, f0r, up, ua, ub, uc, ud, color);
                AddQuad(f0tl, f0tr, f1tr, f1tl, -up, ua, ub, uc, ud, color);
                AddQuad(f0l, f0tl, f1tl, f1l, right, ua, ub, uc, ud, color);
                AddQuad(f0r, f1r, f1tr, f0tr, -right, ua, ub, uc, ud, color);
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                indexFormat = _v.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _uv);
            mesh.SetColors(_c);
            mesh.SetTriangles(_t, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
