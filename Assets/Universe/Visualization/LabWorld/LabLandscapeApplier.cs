using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Reality Engine outdoor lab campus: hide Faraday's broken meadow/terrain,
    /// spawn a Giza sand/stone plateau, and place the undamaged 1:1 Giza complex
    /// (Khufu, Khafre, Menkaure, Sphinx) beyond the circuit table. Play auto-applies.
    /// Does not move XR Origin. Does not disable MountainScene.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class LabLandscapeApplier : MonoBehaviour
    {
        public const string RootName = "LabLandscape";
        public const string HostName = "RealityEngine";
        const float LabPlazaSize = 32f;
        const float GapToPyramidM = 40f;
        const float PlateauPadM = GizaComplex.MarginM;

        static readonly string[] HideExact =
        {
            "Terrain", "Trees", "Rocks", "Details", "EnvironmentalSounds",
            "FlowingRiver", "WaterBlock_50m", "New Terrain"
        };

        static readonly string[] HideContains =
        {
            "waterblock", "meadow", "rhef_spruce", "rhef_pine", "rhef_tree"
        };

        readonly HashSet<Transform> _hidden = new HashSet<Transform>();
        bool _built;
        float _nextPass;

        [SerializeField]
        bool comfortScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookSceneLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoApplyAfterSceneLoad()
        {
            EnsureApplied();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            EnsureApplied();
        }

        public static LabLandscapeApplier EnsureApplied()
        {
            LabLandscapeApplier existing = Object.FindAnyObjectByType<LabLandscapeApplier>(FindObjectsInactive.Include);
            if (existing == null)
            {
                GameObject host = GameObject.Find(HostName);
                if (host == null)
                {
                    CircuitLab lab = Object.FindAnyObjectByType<CircuitLab>(FindObjectsInactive.Include);
                    host = lab != null ? lab.gameObject : new GameObject("LabLandscapeHost");
                }
                existing = host.GetComponent<LabLandscapeApplier>();
                if (existing == null)
                    existing = host.AddComponent<LabLandscapeApplier>();
            }

            if (!existing.isActiveAndEnabled && existing.gameObject.activeInHierarchy)
                existing.enabled = true;

            if (Application.isPlaying)
                existing.ApplyNow(false);
            return existing;
        }

        void Awake()
        {
            if (Application.isPlaying)
                ApplyNow(false);
        }

        void Start()
        {
            if (!Application.isPlaying)
                return;
            ApplyNow(false);
            StartCoroutine(ApplyDelayed());
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            if (Time.unscaledTime < _nextPass)
                return;
            _nextPass = Time.unscaledTime + 1.5f;
            HideBrokenEnvironment();
        }

        IEnumerator ApplyDelayed()
        {
            yield return null;
            yield return null;
            ApplyNow(false);
        }

        public void ApplyNow(bool force)
        {
            HideBrokenEnvironment();
            TintLightAndFog();
            if (force)
            {
                Transform old = transform.Find(RootName);
                if (old == null)
                {
                    GameObject named = GameObject.Find(RootName);
                    if (named != null)
                        old = named.transform;
                }
                if (old != null)
                    SafeDestroy(old.gameObject);
                _built = false;
            }

            if (_built && transform.Find(RootName) != null)
                return;

            BuildWorld(GizaComplex.Spawn.All);
            _built = true;
        }

        void HideBrokenEnvironment()
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                    continue;
                if (_hidden.Contains(t))
                    continue;
                if (ShouldKeep(t))
                    continue;
                if (!ShouldHide(t))
                    continue;
                DisableEnvironment(t);
                _hidden.Add(t);
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null)
                    continue;
                if (ShouldKeep(terrains[i].transform))
                    continue;
                terrains[i].enabled = false;
                TerrainCollider tc = terrains[i].GetComponent<TerrainCollider>();
                if (tc != null)
                    tc.enabled = false;
                _hidden.Add(terrains[i].transform);
            }
        }

        static bool ShouldKeep(Transform t)
        {
            Transform p = t;
            int guard = 0;
            while (p != null && guard++ < 24)
            {
                string n = SafeLower(p.name);
                if (n == "xr origin" || n.Contains("xr origin"))
                    return true;
                if (n == "circuitlab" || n == "circuittable" || n == "breadboard" || n == "circuitlabhandle")
                    return true;
                if (n == "realityengine" || n == "induction lab" || n == RootName.ToLowerInvariant() || n.Contains("mountainscene"))
                    return true;
                if (n == "lablandscape" || GizaComplex.IsMonumentName(n))
                    return true;
                p = p.parent;
            }
            return false;
        }

        static bool ShouldHide(Transform t)
        {
            string n = t.name;
            if (string.IsNullOrEmpty(n))
                return false;
            for (int i = 0; i < HideExact.Length; i++)
            {
                if (string.Equals(n, HideExact[i], System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            string lower = n.ToLowerInvariant();
            for (int i = 0; i < HideContains.Length; i++)
            {
                if (lower.Contains(HideContains[i]))
                    return true;
            }
            return false;
        }

        static void DisableEnvironment(Transform t)
        {
            Terrain terrain = t.GetComponent<Terrain>();
            if (terrain != null)
                terrain.enabled = false;
            TerrainCollider tc = t.GetComponent<TerrainCollider>();
            if (tc != null)
                tc.enabled = false;

            MeshRenderer[] mrs = t.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < mrs.Length; i++)
            {
                if (mrs[i] != null)
                    mrs[i].enabled = false;
            }
            LODGroup[] lods = t.GetComponentsInChildren<LODGroup>(true);
            for (int i = 0; i < lods.Length; i++)
            {
                if (lods[i] != null)
                    lods[i].enabled = false;
            }
            Terrain[] nested = t.GetComponentsInChildren<Terrain>(true);
            for (int i = 0; i < nested.Length; i++)
            {
                if (nested[i] != null)
                    nested[i].enabled = false;
            }
            AudioSource[] audio = t.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audio.Length; i++)
            {
                if (audio[i] != null)
                    audio[i].enabled = false;
            }

            if (t.childCount > 0 || terrain != null)
                t.gameObject.SetActive(false);
        }

        static void TintLightAndFog()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null || l.type != LightType.Directional)
                    continue;
                string n = SafeLower(l.gameObject.name);
                if (n.Contains("directional"))
                {
                    l.color = new Color(0.70f, 0.76f, 0.86f, 1f);
                    if (l.intensity > 1.25f)
                        l.intensity = 1.2f;
                    break;
                }
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            RenderSettings.fogDensity = 0.0024f;
        }

        public void PlaceMonuments(GizaComplex.Spawn which)
        {
            HideBrokenEnvironment();
            TintLightAndFog();
            Transform root = transform.Find(RootName);
            if (root == null)
            {
                GameObject found = GameObject.Find(RootName);
                if (found != null)
                    root = found.transform;
            }
            if (root == null)
            {
                BuildWorld(which);
                _built = true;
                return;
            }
            GizaComplex.Pose pose = ReadPose(root);
            GizaComplex.Ensure(pose, which);
            AddTeleports(root.gameObject);
        }

        void BuildWorld(GizaComplex.Spawn which)
        {
            Transform existing = transform.Find(RootName);
            GameObject found = existing != null ? existing.gameObject : GameObject.Find(RootName);
            if (found != null)
            {
                GizaComplex.Pose poseExisting = ReadPose(found.transform);
                GizaComplex.Ensure(poseExisting, which);
                AddTeleports(found);
                return;
            }

            Bounds table = ResolveTableBounds(out Transform tableXf, out Transform xr);
            Vector3 forward = ResolveForward(tableXf, xr, table);

            float surfaceY = table.min.y;
            if (xr != null)
                surfaceY = Mathf.Min(surfaceY, xr.position.y);
            if (table.size.y > 8f)
                surfaceY = xr != null ? xr.position.y : 0f;

            float halfBase = KhufuPyramid.BaseMeters * 0.5f;
            float tableFwd = ExtentsAlong(table, forward);
            float dist = tableFwd + GapToPyramidM + halfBase;
            Vector3 khufuCenter = new Vector3(table.center.x, surfaceY, table.center.z) + forward * dist;
            Vector3 plazaPos = new Vector3(table.center.x, surfaceY, table.center.z);
            Quaternion khufuRot = Quaternion.LookRotation(-forward, Vector3.up);

            GizaComplex.LocalExtents(out float xMin, out float xMax, out float zMin, out float zMax);
            Vector3[] localCorners =
            {
                new Vector3(xMin, 0f, zMin),
                new Vector3(xMin, 0f, zMax),
                new Vector3(xMax, 0f, zMin),
                new Vector3(xMax, 0f, zMax)
            };
            Vector3 wmin = plazaPos;
            Vector3 wmax = plazaPos;
            Encapsulate(ref wmin, ref wmax, plazaPos + new Vector3(LabPlazaSize, 0f, LabPlazaSize) * 0.5f);
            Encapsulate(ref wmin, ref wmax, plazaPos - new Vector3(LabPlazaSize, 0f, LabPlazaSize) * 0.5f);
            Encapsulate(ref wmin, ref wmax, khufuCenter);
            for (int i = 0; i < localCorners.Length; i++)
                Encapsulate(ref wmin, ref wmax, khufuCenter + khufuRot * localCorners[i]);
            wmin.x -= PlateauPadM;
            wmin.z -= PlateauPadM;
            wmax.x += PlateauPadM;
            wmax.z += PlateauPadM;
            Vector3 mid = (wmin + wmax) * 0.5f;
            float plateauX = Mathf.Max(40f, wmax.x - wmin.x);
            float plateauZ = Mathf.Max(40f, wmax.z - wmin.z);

            var root = new GameObject(RootName);
            root.transform.SetParent(transform, true);
            root.transform.position = new Vector3(mid.x, surfaceY, mid.z);
            root.transform.rotation = Quaternion.identity;

            Material sand = LabWorldMeshes.MakeLit("RELab_GizaSand", new Color(0.46f, 0.41f, 0.34f, 1f), 0.04f, 0.14f, false);
            Texture2D sandTex = LabWorldMeshes.MakePlazaTexture();
            if (sand != null && sandTex != null)
            {
                if (sand.HasProperty("_BaseMap"))
                    sand.SetTexture("_BaseMap", sandTex);
                if (sand.HasProperty("_MainTex"))
                    sand.SetTexture("_MainTex", sandTex);
            }
            Material graph = LabWorldMeshes.MakeLit("RELab_LabPlaza", new Color(0.16f, 0.165f, 0.17f, 1f), 0.62f, 0.30f, false);
            Texture2D gtex = LabWorldMeshes.MakeGraphiteTexture();
            if (graph != null && gtex != null)
            {
                if (graph.HasProperty("_BaseMap"))
                    graph.SetTexture("_BaseMap", gtex);
                if (graph.HasProperty("_MainTex"))
                    graph.SetTexture("_MainTex", gtex);
                graph.SetTextureScale("_BaseMap", new Vector2(8f, 8f));
                graph.SetTextureScale("_MainTex", new Vector2(8f, 8f));
            }
            Material hillMat = LabWorldMeshes.MakeLit("RELab_HillRock", new Color(0.22f, 0.21f, 0.20f, 1f), 0.12f, 0.16f, false);

            Mesh plateauMesh = LabWorldMeshes.BuildPlateau(plateauX, plateauZ, 40, 4.5f);
            GameObject plateau = SpawnLocal(root.transform, "GizaPlateau", plateauMesh, sand, Vector3.zero, true);

            Mesh plazaMesh = LabWorldMeshes.BuildFlatPad(LabPlazaSize, LabPlazaSize, 8f);
            Vector3 plazaLocal = root.transform.InverseTransformPoint(plazaPos + Vector3.up * 0.03f);
            GameObject plaza = SpawnLocal(root.transform, "LabPlaza", plazaMesh, graph, plazaLocal, true);

            var pose = new GizaComplex.Pose
            {
                parent = root.transform,
                khufuCenter = khufuCenter,
                rot = khufuRot,
                surfaceY = surfaceY,
                comfortScale = comfortScale
            };
            StorePose(root.transform, pose);
            GizaComplex.Ensure(pose, which);

            PlaceHills(root.transform, pose, plateauX, plateauZ, hillMat);
            AddTeleports(root);

            Debug.Log(
                "LabLandscapeApplier: Giza plateau " + plateauX.ToString("0") + "×" + plateauZ.ToString("0") +
                " m. Khufu 440×280 cubits (230.38×146.61 m) north entrance 17 m. Khafre 215.25×143.5 m, +10 m bedrock, 323 W / 342 S. " +
                "Menkaure 105.5×65.5 m, 563 W / 743 S, north entrance 4.2 m. Sphinx 73.5×20 m, 347 E / 430 S. " +
                "Ctrl+R then Play, or Reality Engine / Place Giza Complex. Teleport to each north face.");
        }

        static void Encapsulate(ref Vector3 min, ref Vector3 max, Vector3 p)
        {
            min.x = Mathf.Min(min.x, p.x);
            min.y = Mathf.Min(min.y, p.y);
            min.z = Mathf.Min(min.z, p.z);
            max.x = Mathf.Max(max.x, p.x);
            max.y = Mathf.Max(max.y, p.y);
            max.z = Mathf.Max(max.z, p.z);
        }

        static void StorePose(Transform root, GizaComplex.Pose pose)
        {
            var marker = root.Find("_GizaPose");
            if (marker == null)
            {
                var go = new GameObject("_GizaPose");
                go.transform.SetParent(root, false);
                marker = go.transform;
            }
            marker.position = pose.khufuCenter;
            marker.rotation = pose.rot;
        }

        GizaComplex.Pose ReadPose(Transform root)
        {
            Transform marker = root.Find("_GizaPose");
            Vector3 khufuCenter;
            Quaternion rot;
            if (marker != null)
            {
                khufuCenter = marker.position;
                rot = marker.rotation;
            }
            else
            {
                Transform khufu = root.Find(KhufuPyramid.RootName);
                if (khufu == null)
                {
                    GameObject named = GizaComplex.FindNamed(KhufuPyramid.RootName);
                    khufu = named != null ? named.transform : null;
                }
                khufuCenter = khufu != null ? khufu.position : root.position;
                rot = khufu != null ? khufu.rotation : Quaternion.identity;
            }
            return new GizaComplex.Pose
            {
                parent = root,
                khufuCenter = khufuCenter,
                rot = rot,
                surfaceY = root.position.y,
                comfortScale = comfortScale
            };
        }

        static void AddTeleports(GameObject root)
        {
            if (root == null)
                return;
            MeshCollider[] cols = root.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null)
                    continue;
                string n = cols[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("honesty") || n.Contains("plate") || n.Contains("airshaft") || n.Contains("emit") || n.Contains("sarcophagus"))
                    continue;
                AddTeleport(cols[i].gameObject);
            }
            Transform plaza = root.transform.Find("LabPlaza");
            if (plaza != null)
                AddTeleport(plaza.gameObject);
            Transform plateau = root.transform.Find("GizaPlateau");
            if (plateau != null)
                AddTeleport(plateau.gameObject);
        }

        static void PlaceHills(Transform parent, GizaComplex.Pose pose, float plateauX, float plateauZ, Material mat)
        {
            if (parent.Find("Hill_0") != null)
                return;
            float surfaceY = pose.surfaceY;
            float ring = Mathf.Max(plateauX, plateauZ) * 0.46f;
            int n = 10;
            Vector3 khafre = GizaComplex.WorldFromKhufu(pose, -GizaComplex.KhafreWestM, -GizaComplex.KhafreSouthM, 0f);
            Vector3 menkaure = GizaComplex.WorldFromKhufu(pose, -GizaComplex.MenkaureWestM, -GizaComplex.MenkaureSouthM, 0f);
            Vector3 sphinx = GizaComplex.WorldFromKhufu(pose, GizaComplex.SphinxEastM, -GizaComplex.SphinxSouthM, 0f);
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + 0.2f;
                Vector3 p = new Vector3(parent.position.x + Mathf.Cos(a) * ring, surfaceY, parent.position.z + Mathf.Sin(a) * ring);
                if (TooClose(p, pose.khufuCenter, KhufuPyramid.BaseMeters * 0.5f + 40f))
                    continue;
                if (TooClose(p, khafre, KhafrePyramid.BaseMeters * 0.5f + 40f))
                    continue;
                if (TooClose(p, menkaure, MenkaurePyramid.BaseMeters * 0.5f + 50f))
                    continue;
                if (TooClose(p, sphinx, 60f))
                    continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Hill_" + i;
                go.transform.SetParent(parent, true);
                float w = 18f + (i % 3) * 6f;
                float h = 7f + (i % 4) * 2.5f;
                float d = 12f + (i % 2) * 8f;
                go.transform.position = p + Vector3.up * (h * 0.5f);
                go.transform.localScale = new Vector3(w, h, d);
                go.transform.rotation = Quaternion.Euler(0f, i * 37f, 0f);
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (mat == null)
                    {
                        if (Application.isPlaying)
                            Object.Destroy(mr);
                        else
                            Object.DestroyImmediate(mr);
                        continue;
                    }
                    mr.sharedMaterial = mat;
                    float dy = surfaceY - mr.bounds.min.y;
                    go.transform.position += Vector3.up * dy;
                }
            }
        }

        static GameObject SpawnLocal(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            if (mat != null)
            {
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
            }
            else
                Debug.LogError("LabLandscapeApplier: skipped renderer on '" + name + "' (no URP Lit from RELab_Graphite).");
            if (collider)
            {
                var box = go.AddComponent<BoxCollider>();
                if (mesh != null)
                {
                    Bounds b = mesh.bounds;
                    box.center = b.center;
                    box.size = b.size;
                }
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
            }
            return go;
        }

        static void AddTeleport(GameObject go)
        {
            if (go == null)
                return;
            if (go.GetComponent<TeleportationArea>() == null)
                go.AddComponent<TeleportationArea>();
        }

        static bool TooClose(Vector3 a, Vector3 b, float radius)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude < radius * radius;
        }

        static void SitOnSurface(Transform t, float surfaceY)
        {
            if (t == null)
                return;
            Renderer[] rs = t.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0)
            {
                Vector3 p = t.position;
                p.y = surfaceY;
                t.position = p;
                return;
            }
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
            {
                if (rs[i] != null)
                    b.Encapsulate(rs[i].bounds);
            }
            float dy = surfaceY - b.min.y;
            t.position += Vector3.up * dy;
        }

        static Bounds ResolveTableBounds(out Transform tableXf, out Transform xr)
        {
            xr = FindNamedExact("XR Origin");
            tableXf = FindNamedContains("circuittable");
            Transform board = FindNamedContains("breadboard");
            if (tableXf == null)
                tableXf = FindNamedContains("circuitlab");
            if (tableXf != null)
            {
                Bounds b = CollectBounds(tableXf);
                if (board != null)
                    b.Encapsulate(CollectBounds(board));
                return b;
            }
            if (board != null)
            {
                tableXf = board;
                return CollectBounds(board);
            }
            Vector3 p = xr != null ? xr.position : new Vector3(0.4f, 0.75f, 0.55f);
            return new Bounds(p, new Vector3(2f, 1f, 2f));
        }

        static Vector3 ResolveForward(Transform table, Transform xr, Bounds tableBounds)
        {
            if (xr != null)
            {
                Vector3 toTable = tableBounds.center - xr.position;
                toTable.y = 0f;
                if (toTable.sqrMagnitude > 0.04f)
                    return toTable.normalized;
                Vector3 f = xr.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 1e-6f)
                    return f.normalized;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 1e-6f)
                    return f.normalized;
            }
            return Vector3.forward;
        }

        static float ExtentsAlong(Bounds b, Vector3 dir)
        {
            Vector3 e = b.extents;
            return Mathf.Abs(dir.x) * e.x + Mathf.Abs(dir.y) * e.y + Mathf.Abs(dir.z) * e.z;
        }

        static Transform FindNamedExact(string name)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        static Transform FindNamedContains(string token)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                    continue;
                string n = all[i].name;
                if (string.IsNullOrEmpty(n))
                    continue;
                if (n.ToLowerInvariant().Contains(token))
                    return all[i];
            }
            return null;
        }

        static Bounds CollectBounds(Transform root)
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0)
                return new Bounds(root.position, Vector3.one * 0.5f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
            {
                if (rs[i] != null)
                    b.Encapsulate(rs[i].bounds);
            }
            return b;
        }

        static string SafeLower(string n)
        {
            return string.IsNullOrEmpty(n) ? "" : n.ToLowerInvariant();
        }

        static void SafeDestroy(Object o)
        {
            if (o == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(o);
            else
                Object.DestroyImmediate(o);
        }
    }
}
