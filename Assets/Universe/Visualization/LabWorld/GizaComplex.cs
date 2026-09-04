using UnityEngine;
using TMPro;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Giza necropolis at 1:1. Offsets from Khufu centre are approx. WGS84 at lat 30Ã‚Â°.
    /// Architectural local space: origin at Khufu base centre, +Y up, +Z north, +X east.
    /// </summary>
    public static class GizaComplex
    {
        public const float Cubit = 0.5236f;
        public const float MarginM = 80f;
        public const float CliffHeightM = 32f;
        public const float SphinxCourtDropM = 12f;
        public const float DesertSizeM = 2600f;
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


        public static float CourtY(Pose pose)
        {
            return pose.surfaceY - SphinxCourtDropM;
        }

        public static float TerraceY(Pose pose)
        {
            return pose.surfaceY + KhafreBedrockM;
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
            GizaPrecinct.ExpandExtents(ref xMin, ref xMax, ref zMin, ref zMax);
        }

        public static void Ensure(Pose pose, Spawn which)
        {
            if ((which & Spawn.Khufu) != 0)
            {
                EnsureNamed(KhufuPyramid.RootName, pose, (p) => KhufuPyramid.Build(p.parent, p.khufuCenter, p.rot, p.comfortScale), pose.surfaceY);
                GizaPrecinct.EnsureKhufu(pose);
            }
            if ((which & Spawn.Khafre) != 0)
            {
                Vector3 c = WorldFromKhufu(pose, -KhafreWestM, -KhafreSouthM, 0f);
                EnsureNamed(KhafrePyramid.RootName, pose, (p) => KhafrePyramid.Build(p.parent, c, p.rot, p.comfortScale), pose.surfaceY);
                GizaPrecinct.EnsureKhafre(pose);
            }
            if ((which & Spawn.Menkaure) != 0)
            {
                Vector3 c = WorldFromKhufu(pose, -MenkaureWestM, -MenkaureSouthM, 0f);
                EnsureNamed(MenkaurePyramid.RootName, pose, (p) => MenkaurePyramid.Build(p.parent, c, p.rot, p.comfortScale), pose.surfaceY);
                GizaPrecinct.EnsureMenkaure(pose);
            }
            if ((which & Spawn.Sphinx) != 0)
            {
                // Force rebuild when Dream Stele is missing (pre-stele body massing).
                GameObject oldSphinx = FindNamed(GizaSphinx.RootName);
                if (oldSphinx != null && oldSphinx.transform.Find(GizaSphinx.DreamSteleName) == null)
                {
                    oldSphinx.name = oldSphinx.name + "_Obsolete";
                    if (Application.isPlaying)
                        Object.Destroy(oldSphinx);
                    else
                        Object.DestroyImmediate(oldSphinx);
                }
                Vector3 c = WorldFromKhufu(pose, SphinxEastM, -SphinxSouthM, 0f);
                EnsureNamed(GizaSphinx.RootName, pose, (p) => GizaSphinx.Build(p.parent, c, p.rot), CourtY(pose));
                GizaPrecinct.EnsureSphinx(pose);
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
                || lower == "g1a" || lower == "g1b" || lower == "g1c"
                || lower.StartsWith("g1a") || lower.StartsWith("g1b") || lower.StartsWith("g1c")
                || lower == "g3a" || lower == "g3b" || lower == "g3c"
                || lower.Contains("mortuary") || lower.Contains("causeway") || lower.Contains("boatpit")
                || lower.Contains("enclosure") || lower.Contains("valleytemple")
                || lower.Contains("nile") || lower.Contains("floodplain") || lower.Contains("harbor")
                || lower.Contains("village") || lower.Contains("settlement")
                || lower.StartsWith("lablandscape");
        }
    }

    /// <summary>
    /// Shared undamaged true-pyramid casing, pyramidion, pavement, honesty plate.
    /// 4-face shells only Ã¢â‚¬â€ no filled core (walkable interiors do not clip solid rock).
    /// </summary>
    public static class GizaBuild
    {
        static Material _tura;
        static Material _lime;
        static Material _gran;
        static Material _aswan;
        static Material _electrum;
        static Material _pav;
        static Material _rock;
        static Material _emit;
        static Material _plate;
        static Material _sand;
        static Material _cliff;
        static Material _sphinx;
        static Material _silt;
        static Material _nileWater;
        static Material _mudbrick;
        static Material _field;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            _tura = null;
            _lime = null;
            _gran = null;
            _aswan = null;
            _electrum = null;
            _pav = null;
            _rock = null;
            _emit = null;
            _plate = null;
            _sand = null;
            _cliff = null;
            _sphinx = null;
            _silt = null;
            _nileWater = null;
            _mudbrick = null;
            _field = null;
            _stoneMatRev = -1;
        }

        static int _stoneMatRev = -1;

        static void EnsureFreshStoneMats()
        {
            int rev = LabWorldMeshes.StoneTexRev;
            if (_stoneMatRev == rev)
                return;
            _tura = null;
            _lime = null;
            _gran = null;
            _aswan = null;
            _pav = null;
            _rock = null;
            _cliff = null;
            _sphinx = null;
            LabWorldMeshes.InvalidateProcTextures();
            _stoneMatRev = rev;
        }

        static Material CachedLit(ref Material slot, string name, Color color, float metallic, float smoothness,
            Texture2D map, Vector2 scale, Texture2D bump = null, float bumpScale = 1f)
        {
            if (slot != null)
                return slot;
            slot = LabWorldMeshes.MakeLit(name, color, metallic, smoothness, false);
            LabWorldMeshes.ApplyAlbedo(slot, map, scale);
            if (bump != null)
                LabWorldMeshes.ApplyBump(slot, bump, scale, bumpScale);
            return slot;
        }

        public static Material TuraCasing()
        {
            EnsureFreshStoneMats();
            // Near-white tint so cooler ivory albedo reads; matte ~0.15 (not glossy plastic).
            return CachedLit(ref _tura, "RELab_TuraCasing", new Color(0.94f, 0.92f, 0.88f, 1f), 0.02f, 0.15f,
                LabWorldMeshes.MakeTuraBlockTexture(), Vector2.one,
                LabWorldMeshes.MakeTuraBlockNormal(), 1.15f);
        }

        public static Material InteriorLime()
        {
            EnsureFreshStoneMats();
            return CachedLit(ref _lime, "RELab_GizaCore", new Color(0.64f, 0.57f, 0.48f, 1f), 0.02f, 0.12f,
                LabWorldMeshes.MakeLimestoneTexture(), Vector2.one,
                LabWorldMeshes.MakeLimestoneNormal(), 1.25f);
        }

        public static Material Granite()
        {
            EnsureFreshStoneMats();
            return CachedLit(ref _gran, "RELab_GizaGranite", new Color(0.40f, 0.30f, 0.28f, 1f), 0.06f, 0.18f,
                LabWorldMeshes.MakeGraniteTexture(), Vector2.one);
        }

        public static Material Aswan()
        {
            EnsureFreshStoneMats();
            return CachedLit(ref _aswan, "RELab_AswanGranite", new Color(0.52f, 0.30f, 0.26f, 1f), 0.08f, 0.20f,
                LabWorldMeshes.MakeGraniteTexture(), Vector2.one);
        }

        public static Material Electrum()
        {
            if (_electrum != null)
                return _electrum;
            _electrum = LabWorldMeshes.MakeLit("RELab_Pyramidion", new Color(0.78f, 0.66f, 0.32f, 1f), 0.82f, 0.62f, false);
            return _electrum;
        }

        public static Material Pavement()
        {
            EnsureFreshStoneMats();
            return CachedLit(ref _pav, "RELab_GizaPavement", new Color(0.76f, 0.71f, 0.62f, 1f), 0.03f, 0.13f,
                LabWorldMeshes.MakeLimestoneTexture(), Vector2.one,
                LabWorldMeshes.MakeLimestoneNormal(), 1.05f);
        }

        public static Material Bedrock()
        {
            EnsureFreshStoneMats();
            // Plateau top: limestone courses, not Oasis sand.
            return CachedLit(ref _rock, "RELab_GizaBedrock", new Color(0.54f, 0.49f, 0.42f, 1f), 0.02f, 0.11f,
                LabWorldMeshes.MakeCliffTexture(), Vector2.one,
                LabWorldMeshes.MakeCliffNormal(), 1.35f);
        }

        const string OasisSandResource = "RELab_OasisSand";
        const string OasisSandPath = "Assets/Universe/Visualization/LabWorld/OasisSand/Resources/RELab_OasisSand.mat";
        const string OasisGravelResource = "RELab_OasisGravel";
        const string OasisGravelPath = "Assets/Universe/Visualization/LabWorld/OasisSand/Resources/RELab_OasisGravel.mat";

        static Material LoadOasisAsset(string resource, string path)
        {
            Material mat = Resources.Load<Material>(resource);
#if UNITY_EDITOR
            if (mat == null)
                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
#endif
            return mat;
        }

        public static Material DesertSand()
        {
            if (_sand != null)
                return _sand;

            Material oasis = LoadOasisAsset(OasisSandResource, OasisSandPath);
            if (oasis != null && !LabWorldMeshes.MaterialLooksPink(oasis))
            {
                _sand = new Material(oasis)
                {
                    name = "RELab_OasisSand",
                    hideFlags = HideFlags.DontSave
                };
                if (_sand.HasProperty("_Glitter_On"))
                    _sand.SetFloat("_Glitter_On", 1f);
                if (_sand.HasProperty("_Glitter_Strength"))
                    _sand.SetFloat("_Glitter_Strength", 0.22f);
                if (_sand.HasProperty("_Glitter_Strength_in_Shadow"))
                    _sand.SetFloat("_Glitter_Strength_in_Shadow", 0.35f);
                return _sand;
            }

            Material gravel = LoadOasisAsset(OasisGravelResource, OasisGravelPath);
            if (gravel != null && !LabWorldMeshes.MaterialLooksPink(gravel))
            {
                _sand = new Material(gravel)
                {
                    name = "RELab_OasisSand",
                    hideFlags = HideFlags.DontSave
                };
                Texture2D albedo = null;
                if (_sand.HasProperty("_BaseMap"))
                    albedo = _sand.GetTexture("_BaseMap") as Texture2D;
                LabWorldMeshes.ApplyAlbedo(_sand, albedo, new Vector2(0.12f, 0.12f));
                return _sand;
            }

            return CachedLit(ref _sand, "RELab_GizaSand", new Color(0.78f, 0.66f, 0.44f, 1f), 0.02f, 0.10f,
                LabWorldMeshes.MakeDesertSandTexture(), Vector2.one);
        }

        public static Material NileSilt()
        {
            return CachedLit(ref _silt, "RELab_NileSilt", new Color(0.28f, 0.32f, 0.18f, 1f), 0.02f, 0.12f,
                LabWorldMeshes.MakeNileSiltTexture(), Vector2.one);
        }

        public static Material NileField()
        {
            return CachedLit(ref _field, "RELab_NileField", new Color(0.22f, 0.40f, 0.16f, 1f), 0.02f, 0.14f,
                LabWorldMeshes.MakeNileSiltTexture(), new Vector2(2f, 2f));
        }

        public static Material NileWater()
        {
            if (_nileWater != null)
                return _nileWater;
            _nileWater = LabWorldMeshes.MakeLit("RELab_NileWater", new Color(0.10f, 0.24f, 0.30f, 1f), 0.04f, 0.62f, false);
            return _nileWater;
        }

        public static Material Mudbrick()
        {
            if (_mudbrick != null)
                return _mudbrick;
            _mudbrick = LabWorldMeshes.MakeLit("RELab_Mudbrick", new Color(0.55f, 0.38f, 0.26f, 1f), 0.02f, 0.10f, false);
            return _mudbrick;
        }

        public static Material CliffRock()
        {
            EnsureFreshStoneMats();
            return CachedLit(ref _cliff, "RELab_HillRock", new Color(0.46f, 0.42f, 0.36f, 1f), 0.03f, 0.12f,
                LabWorldMeshes.MakeCliffTexture(), Vector2.one,
                LabWorldMeshes.MakeCliffNormal(), 1.45f);
        }

        public static Material SphinxLime()
        {
            EnsureFreshStoneMats();
            // Weathered limestone bedrock with readable joints (cliff courses).
            return CachedLit(ref _sphinx, "RELab_SphinxLimestone", new Color(0.78f, 0.73f, 0.64f, 1f), 0.03f, 0.13f,
                LabWorldMeshes.MakeCliffTexture(), Vector2.one,
                LabWorldMeshes.MakeCliffNormal(), 1.40f);
        }

        public static Material Emit()
        {
            if (_emit != null)
                return _emit;
            _emit = LabWorldMeshes.MakeLit("RELab_GizaEmit", new Color(0.22f, 0.20f, 0.16f, 1f), 0.0f, 0.08f, true);
            if (_emit != null && _emit.HasProperty("_EmissionColor"))
                _emit.SetColor("_EmissionColor", new Color(0.16f, 0.14f, 0.10f, 1f));
            return _emit;
        }

        public static Material Plate()
        {
            if (_plate != null)
                return _plate;
            _plate = LabWorldMeshes.MakeLit("RELab_GizaPlate", new Color(0.10f, 0.11f, 0.12f, 1f), 0.2f, 0.18f, false);
            return _plate;
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
            float tile = LabWorldMeshes.StoneTileM;
            float uLen = (inner + outer) * 0.5f * 2f;
            float vLen = widthM;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector2 u00 = new Vector2(0f, 0f);
                Vector2 u10 = new Vector2(vLen / tile, 0f);
                Vector2 u11 = new Vector2(vLen / tile, uLen / tile);
                Vector2 u01 = new Vector2(0f, uLen / tile);
                b.AddQuad(inn[i], outt[i], outt[j], inn[j], Vector3.up, u00, u10, u11, u01, c);
                b.AddQuad(outt[i], outB[i], outB[j], outt[j], (outt[i] + outt[j]).normalized, u00, u10, u11, u01, c);
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

        public static void ReapplyMaterials(Transform root)
        {
            if (root == null)
                return;
            EnsureFreshStoneMats();
            Material tura = TuraCasing();
            Material lime = InteriorLime();
            Material gran = Granite();
            Material aswan = Aswan();
            Material pav = Pavement();
            Material rock = Bedrock();
            Material gold = Electrum();
            Material sphinx = SphinxLime();
            Material sand = DesertSand();
            Material cliff = CliffRock();
            MeshRenderer[] mrs = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < mrs.Length; i++)
            {
                MeshRenderer mr = mrs[i];
                if (mr == null)
                    continue;
                string obj = mr.gameObject.name;
                string l = string.IsNullOrEmpty(obj) ? "" : obj.ToLowerInvariant();
                string mn = mr.sharedMaterial != null ? mr.sharedMaterial.name : "";
                if (l.Contains("labplaza") || l.Contains("honesty") || l.Contains("emit") || l.Contains("hull"))
                    continue;
                if (l.Contains("plate") && !l.Contains("plateau") && !l.Contains("gizaplateau"))
                    continue;
                if (l.Contains("floodplain") || l.Contains("nile") || l.Contains("harbor")
                    || l.Contains("village") || l.Contains("silt") || l.Contains("settlement")
                    || l.Contains("field") || l.Contains("mudbrick") || l.Contains("yard")
                    || l.Contains("house"))
                    continue;
                if (l.Contains("pyramidion") || mn.Contains("Pyramidion"))
                    mr.sharedMaterial = gold;
                else if (l.Contains("gizadesert") || l.Contains("gizadune") || l.Contains("sandwash")
                    || mn.Contains("GizaSand") || mn.Contains("DesertSand") || mn.Contains("OasisSand")
                    || mn.Contains("OasisTerrain"))
                    mr.sharedMaterial = sand;
                else if (l.Contains("cliff") || mn.Contains("HillRock") || mn.Contains("Cliff"))
                    mr.sharedMaterial = cliff;
                else if (l == "gizaplateau" || l.Contains("gizaplateautop"))
                    mr.sharedMaterial = rock;
                else if (l.Contains("casinggranite") || l.Contains("aswan") || mn.Contains("Aswan"))
                    mr.sharedMaterial = aswan;
                else if (l.Contains("casing") || mn.Contains("TuraCasing"))
                    mr.sharedMaterial = tura;
                else if (l.Contains("sphinxenclosure") && l.Contains("_ditch"))
                    mr.sharedMaterial = rock;
                else if (l.Contains("sphinxenclosure") && l.Contains("_floor"))
                    mr.sharedMaterial = pav;
                else if (l.Contains("sphinxenclosure") && l.Contains("_lining"))
                    mr.sharedMaterial = sphinx;
                else if (l.Contains("sphinx") || mn.Contains("SphinxLimestone"))
                    mr.sharedMaterial = sphinx;
                else if (l.Contains("sarcophagus") || l.Contains("kingchamber") || l.Contains("pillar")
                    || (l.Contains("antechamber") && l.Contains("khufu"))
                    || mn.Contains("GizaGranite"))
                    mr.sharedMaterial = gran;
                else if (l.Contains("valley") && l.Contains("wall"))
                    mr.sharedMaterial = gran;
                else if (l.Contains("boat") && l.Contains("pavement"))
                    mr.sharedMaterial = tura;
                else if (l.Contains("_walls") || l.Contains("enclosure"))
                    mr.sharedMaterial = tura;
                else if (l.Contains("bedrock") || l.Contains("terrace") || mn.Contains("GizaBedrock"))
                    mr.sharedMaterial = rock;
                else if (l.Contains("pavement") || l.Contains("_floor") || l.Contains("_court") || l.Contains("_deck")
                    || l.Contains("ledge") || l.Contains("terminal") || mn.Contains("GizaPavement"))
                    mr.sharedMaterial = pav;
                else if (mn.Contains("GizaCore") || l.Contains("passage") || l.Contains("chamber")
                    || l.Contains("burial") || l.Contains("sanctum") || l.Contains("_interior") || l.Contains("antechamber") || l.Contains("subterranean")
                    || l.Contains("reliev") || l.Contains("airshaft") || l.Contains("hall"))
                    mr.sharedMaterial = lime;
            }
        }

        public static void SitExisting(GizaComplex.Pose pose)
        {
            float top = pose.surfaceY;
            float court = GizaComplex.CourtY(pose);
            float terrace = GizaComplex.TerraceY(pose);
            SitFound(KhufuPyramid.RootName, top);
            SitFound(KhafrePyramid.RootName, top);
            SitFound(MenkaurePyramid.RootName, top);
            SitFound(GizaSphinx.RootName, court);
            SitFound("G1a", top);
            SitFound("G1b", top);
            SitFound("G1c", top);
            SitFound("KhufuMortuary", top);
            SitFound("KhufuBoatPits", top);
            SitFound("KhufuEnclosure", top);
            SitFound("KhafreMortuary", terrace);
            SitFound("KhafreValleyTemple", court);
            SitFound("KhafreEnclosure", terrace);
            SitFound("SphinxTemple", court);
            SitFound("SphinxValleyLink", court);
            SitFound("SphinxEnclosure", court);
            SitFound("MenkaureMortuary", top);
            SitFound("MenkaureEnclosure", top);
            float flood = pose.surfaceY - GizaComplex.CliffHeightM + GizaNile.SitAboveDesertM;
            SitFound("KhufuValleyTemple", flood);
            SitFound("MenkaureValleyTemple", flood);
        }

        static void SitFound(string name, float sitY)
        {
            GameObject go = GizaComplex.FindNamed(name);
            if (go != null)
                SitOn(go.transform, sitY);
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
                r = RendererNamedContains(t, "_Floor");
            if (r == null)
                r = RendererNamedContains(t, "_Deck");
            if (r == null)
                r = RendererNamedContains(t, "_Rim");
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

