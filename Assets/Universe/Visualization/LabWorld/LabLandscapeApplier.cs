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
    /// (Khufu, queens G1a-c, temples, causeways, boat pits, Khafre, Menkaure, Sphinx) beyond the circuit table. Play auto-applies.
    /// Does not move XR Origin. Does not disable MountainScene.
    /// Test: Ctrl+R, Play Faraday.unity. From the lab: sand around Khufu's base you could eat,
    /// desert beyond the east cliff, Tura courses still on the pyramid, not magenta, not a plastic plane.
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
            Transform rootXf = transform.Find(RootName);
            if (rootXf == null)
            {
                GameObject named = GameObject.Find(RootName);
                if (named != null)
                    rootXf = named.transform;
            }
            if (!force && rootXf != null && rootXf.Find("GizaDesert") == null)
                force = true;

            if (force)
            {
                GizaField.ForceRebuildAll();
                if (rootXf != null)
                    SafeDestroy(rootXf.gameObject);
                _built = false;
                rootXf = null;
            }

            if (rootXf != null)
            {
                GizaComplex.Pose pose = ReadPose(rootXf);
                GizaComplex.Ensure(pose, GizaComplex.Spawn.All);
                FitPlateau(rootXf, pose);
                GizaBuild.ReapplyMaterials(rootXf);
                GizaBuild.SitExisting(pose);
                AddTeleports(rootXf.gameObject);
                _built = true;
                return;
            }

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
                    l.color = new Color(1.00f, 0.91f, 0.72f, 1f);
                    l.intensity = 1.15f;
                    break;
                }
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.78f, 0.72f, 0.58f, 1f);
            RenderSettings.fogDensity = 0.00032f;
            RenderSettings.ambientLight = new Color(0.62f, 0.56f, 0.44f, 1f);
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
            FitPlateau(root, pose);
            GizaBuild.ReapplyMaterials(root);
            GizaBuild.SitExisting(pose);
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
                FitPlateau(found.transform, poseExisting);
                GizaBuild.ReapplyMaterials(found.transform);
                GizaBuild.SitExisting(poseExisting);
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

            Material graph = LabWorldMeshes.MakeLit("RELab_LabPlaza", new Color(0.16f, 0.165f, 0.17f, 1f), 0.62f, 0.30f, false);
            Texture2D gtex = LabWorldMeshes.MakeGraphiteTexture();
            LabWorldMeshes.ApplyAlbedo(graph, gtex, new Vector2(8f, 8f));

            Mesh plazaMesh = LabWorldMeshes.BuildFlatPad(LabPlazaSize, LabPlazaSize, 8f);
            Vector3 plazaLocal = root.transform.InverseTransformPoint(plazaPos + Vector3.up * 0.03f);
            SpawnLocal(root.transform, "LabPlaza", plazaMesh, graph, plazaLocal, true);

            var pose = new GizaComplex.Pose
            {
                parent = root.transform,
                khufuCenter = khufuCenter,
                rot = khufuRot,
                surfaceY = surfaceY,
                comfortScale = comfortScale
            };
            StorePose(root.transform, pose);
            FitPlateau(root.transform, pose);
            GizaComplex.Ensure(pose, which);
            GizaBuild.ReapplyMaterials(root.transform);
            GizaBuild.SitExisting(pose);
            AddTeleports(root);

            Debug.Log(
                "LabLandscapeApplier: Giza limestone plateau ~" + plateauX.ToString("0") + "x" + plateauZ.ToString("0") +
                " m, cliff " + GizaComplex.CliffHeightM.ToString("0") + " m, desert " + GizaComplex.DesertSizeM.ToString("0") +
                " m. Khufu 230.38x146.61 m Tura courses. Khafre on +10 m terrace. Sphinx court " +
                GizaComplex.SphinxCourtDropM.ToString("0") + " m below the table. " +
                "Oasis sand skirts Khufu " + KhufuDuneRadiusM.ToString("0") +
                " m, Khafre " + KhafreDuneRadiusM.ToString("0") + " m, Menkaure " + MenkaureDuneRadiusM.ToString("0") +
                " m. East of the cliff: Nile floodplain silt, schematic harbor basin, and valley settlement (true Nile ~8 km further east, not modeled)." +
                " West/East/Central Field mastabas (west+east of Khufu, south of Khafre); Heit el-Ghurab workers village + Wall of the Crow south; Osiris Shaft near Sphinx (schematic); SpeculativeUnderworld water-shaft fringe diagram OFF by default." +
                " From the lab: sand around Khufu's base you could eat, Tura courses still on the pyramid. Ctrl+R then Play, or Reality Engine / Place Giza Complex.");
        }

        const int PlateauDiv = 80;
        const float TopNoiseM = 1.2f;
        const float EastSouthBevelM = 8f;
        const float WestNorthBevelM = 52f;

        void FitPlateau(Transform root, GizaComplex.Pose pose)
        {
            if (root == null)
                return;
            Transform plaza = root.Find("LabPlaza");
            Vector3 plazaPos = plaza != null ? plaza.position : pose.khufuCenter;
            ComputeOrientedBounds(pose, plazaPos, out float xMin, out float xMax, out float zMin, out float zMax);
            float cx = (xMin + xMax) * 0.5f;
            float cz = (zMin + zMax) * 0.5f;
            float plateauX = Mathf.Max(40f, xMax - xMin);
            float plateauZ = Mathf.Max(40f, zMax - zMin);
            float hx = plateauX * 0.5f;
            float hz = plateauZ * 0.5f;

            GizaPrecinct.Layout L = GizaPrecinct.Compute();
            var court = new LabWorldMeshes.PlateauCut();
            court.xMin = (GizaComplex.SphinxEastM - GizaSphinx.LengthM * 0.5f - 24f) - cx;
            court.xMax = hx;
            court.zMin = (Mathf.Min(-GizaComplex.SphinxSouthM - 20f, L.valleyNorth - L.valleyNS * 0.5f - 20f)) - cz;
            court.zMax = (Mathf.Max(-GizaComplex.SphinxSouthM + 20f, L.sphinxTempleNorth + L.sphinxTempleNS * 0.5f + 16f)) - cz;
            court.dropM = GizaComplex.SphinxCourtDropM;
            court.xMin = Mathf.Clamp(court.xMin, -hx + 12f, hx - 40f);
            court.zMin = Mathf.Clamp(court.zMin, -hz + 12f, hz - 12f);
            court.zMax = Mathf.Clamp(court.zMax, court.zMin + 20f, hz - 12f);

            Vector3 plateauPos = pose.khufuCenter + pose.rot * new Vector3(cx, 0f, cz);
            plateauPos.y = pose.surfaceY;

            Material sand = GizaBuild.DesertSand();
            Material cliff = GizaBuild.CliffRock();
            Material rock = GizaBuild.Bedrock();

            Mesh topMesh = LabWorldMeshes.BuildPlateauTop(plateauX, plateauZ, PlateauDiv, court, TopNoiseM);
            ApplyLandMesh(root, "GizaPlateau", topMesh, rock, plateauPos, pose.rot, true, false, true);

            Mesh cliffMesh = LabWorldMeshes.BuildPlateauCliffs(
                plateauX, plateauZ, PlateauDiv, court, GizaComplex.CliffHeightM,
                EastSouthBevelM, EastSouthBevelM, WestNorthBevelM, WestNorthBevelM, TopNoiseM);
            ApplyLandMesh(root, "GizaPlateauCliffs", cliffMesh, cliff, plateauPos, pose.rot, true, false, false);

            Mesh desertMesh = LabWorldMeshes.BuildDesertFloor(GizaComplex.DesertSizeM, 16, 1.2f);
            Vector3 desertPos = new Vector3(plateauPos.x, pose.surfaceY - GizaComplex.CliffHeightM - 0.12f, plateauPos.z);
            ApplyLandMesh(root, "GizaDesert", desertMesh, sand, desertPos, Quaternion.identity, true, true, true);

            PlaceDuneSkirts(root, pose, sand, plazaPos);
            PlaceSandWashes(root, pose, sand, plazaPos);
            GizaNile.Ensure(root, pose, plazaPos, xMin, xMax, zMin, zMax);
            PlaceDesertDust(root, pose);

            float terrX = -GizaComplex.KhafreWestM - cx;
            float terrZ = -GizaComplex.KhafreSouthM - cz;
            float terrSize = KhafrePyramid.BaseMeters + 28f;
            Mesh terrMesh = LabWorldMeshes.BuildRaisedPad(terrSize, terrSize, GizaComplex.KhafreBedrockM * 0.99f);
            Vector3 terrPos = plateauPos + pose.rot * new Vector3(terrX, 0f, terrZ);
            terrPos.y = pose.surfaceY;
            ApplyLandMesh(root, "GizaKhafreTerrace", terrMesh, rock, terrPos, pose.rot, true, false, true);

            if (plaza != null)
            {
                Vector3 p = plaza.position;
                p.y = pose.surfaceY + 0.03f;
                plaza.position = p;
            }
        }

        static void ComputeOrientedBounds(GizaComplex.Pose pose, Vector3 plazaWorld,
            out float xMin, out float xMax, out float zMin, out float zMax)
        {
            GizaComplex.LocalExtents(out xMin, out xMax, out zMin, out zMax);
            float hs = LabPlazaSize * 0.5f;
            EncLocal(pose, plazaWorld + new Vector3(hs, 0f, hs), ref xMin, ref xMax, ref zMin, ref zMax);
            EncLocal(pose, plazaWorld + new Vector3(-hs, 0f, hs), ref xMin, ref xMax, ref zMin, ref zMax);
            EncLocal(pose, plazaWorld + new Vector3(hs, 0f, -hs), ref xMin, ref xMax, ref zMin, ref zMax);
            EncLocal(pose, plazaWorld + new Vector3(-hs, 0f, -hs), ref xMin, ref xMax, ref zMin, ref zMax);
            xMin -= PlateauPadM;
            xMax += PlateauPadM;
            zMin -= PlateauPadM;
            zMax += PlateauPadM;
        }

        static void EncLocal(GizaComplex.Pose pose, Vector3 world, ref float xMin, ref float xMax, ref float zMin, ref float zMax)
        {
            Vector3 local = Quaternion.Inverse(pose.rot) * (world - pose.khufuCenter);
            xMin = Mathf.Min(xMin, local.x);
            xMax = Mathf.Max(xMax, local.x);
            zMin = Mathf.Min(zMin, local.z);
            zMax = Mathf.Max(zMax, local.z);
        }

        static Transform ApplyLandMesh(Transform parent, string name, Mesh mesh, Material mat,
            Vector3 worldPos, Quaternion worldRot, bool meshCol, bool boxCol, bool teleport)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, true);
                t = go.transform;
            }
            else
                go = t.gameObject;
            t.SetPositionAndRotation(worldPos, worldRot);

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null)
                mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            if (mat != null)
            {
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr == null)
                    mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            else
                Debug.LogError("LabLandscapeApplier: skipped renderer on '" + name + "' (no URP Lit from RELab_Graphite).");

            BoxCollider box = go.GetComponent<BoxCollider>();
            if (boxCol)
            {
                if (box == null)
                    box = go.AddComponent<BoxCollider>();
                if (mesh != null)
                {
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                }
            }
            else if (box != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(box);
                else
                    Object.DestroyImmediate(box);
            }

            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (meshCol)
            {
                if (mc == null)
                    mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
            }
            else if (mc != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(mc);
                else
                    Object.DestroyImmediate(mc);
            }

            if (teleport)
                AddTeleport(go);
            return t;
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
                if (n.Contains("honesty") || n.Contains("plate") || n.Contains("airshaft") || n.Contains("emit") || n.Contains("sarcophagus") || n.Contains("hull") || n.Contains("cliff")
                    || n.Contains("water") || n.Contains("house") || n.Contains("heatmap") || n.Contains("surveyframe")
                    || n.Contains("_shaft") || n.Contains("speculative"))
                    continue;
                AddTeleport(cols[i].gameObject);
            }
            Transform plaza = root.transform.Find("LabPlaza");
            if (plaza != null)
                AddTeleport(plaza.gameObject);
            Transform plateau = root.transform.Find("GizaPlateau");
            if (plateau != null)
                AddTeleport(plateau.gameObject);
            Transform desert = root.transform.Find("GizaDesert");
            if (desert != null)
                AddTeleport(desert.gameObject);
            Transform terrace = root.transform.Find("GizaKhafreTerrace");
            if (terrace != null)
                AddTeleport(terrace.gameObject);
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform ch = root.transform.GetChild(i);
                if (ch == null)
                    continue;
                string n = ch.name.ToLowerInvariant();
                if (n.StartsWith("gizadune") || n.StartsWith("gizasandwash"))
                    AddTeleport(ch.gameObject);
            }
            Transform floodplain = root.transform.Find("GizaNileFloodplain");
            if (floodplain != null)
            {
                AddTeleport(floodplain.gameObject);
                for (int i = 0; i < floodplain.childCount; i++)
                {
                    Transform fch = floodplain.GetChild(i);
                    if (fch == null)
                        continue;
                    string fn = fch.name.ToLowerInvariant();
                    if (fn.Contains("field") || fn.Contains("silt"))
                        AddTeleport(fch.gameObject);
                }
            }
            Transform harbor = root.transform.Find("GizaNileHarbor");
            if (harbor != null)
            {
                for (int i = 0; i < harbor.childCount; i++)
                {
                    Transform hch = harbor.GetChild(i);
                    if (hch == null)
                        continue;
                    string hn = hch.name.ToLowerInvariant();
                    if (hn.Contains("rim") && !hn.Contains("water"))
                        AddTeleport(hch.gameObject);
                }
            }
            Transform village = root.transform.Find("GizaValleySettlement");
            if (village != null)
            {
                for (int i = 0; i < village.childCount; i++)
                {
                    Transform vch = village.GetChild(i);
                    if (vch == null)
                        continue;
                    string vn = vch.name.ToLowerInvariant();
                    if (vn.Contains("yard") || vn.Contains("court"))
                        AddTeleport(vch.gameObject);
                }
            }
            Transform westField = root.transform.Find("KhufuWestField");
            if (westField == null)
            {
                GameObject wf = GizaComplex.FindNamed("KhufuWestField");
                westField = wf != null ? wf.transform : null;
            }
            if (westField != null)
            {
                for (int i = 0; i < westField.childCount; i++)
                {
                    Transform wch = westField.GetChild(i);
                    if (wch == null)
                        continue;
                    string wn = wch.name.ToLowerInvariant();
                    if (wn.Contains("sand") || wn.Contains("street") || wn.Contains("yard"))
                        AddTeleport(wch.gameObject);
                }
            }
            Transform workers = root.transform.Find("GizaWorkersVillage");
            if (workers == null)
            {
                GameObject wv = GizaComplex.FindNamed("GizaWorkersVillage");
                workers = wv != null ? wv.transform : null;
            }
            if (workers != null)
            {
                for (int i = 0; i < workers.childCount; i++)
                {
                    Transform wch = workers.GetChild(i);
                    if (wch == null)
                        continue;
                    string wn = wch.name.ToLowerInvariant();
                    if (wn.Contains("street") || wn.Contains("yard"))
                        AddTeleport(wch.gameObject);
                }
            }
            Transform shaft = root.transform.Find("OsirisShaft");
            if (shaft == null)
            {
                GameObject sh = GizaComplex.FindNamed("OsirisShaft");
                shaft = sh != null ? sh.transform : null;
            }
            if (shaft != null)
            {
                for (int i = 0; i < shaft.childCount; i++)
                {
                    Transform sch = shaft.GetChild(i);
                    if (sch == null)
                        continue;
                    string sn = sch.name.ToLowerInvariant();
                    if (sn.Contains("apron") || sn.Contains("pad") || sn.Contains("stairs")
                        || sn.Contains("midledge") || sn.Contains("bottom"))
                        AddTeleport(sch.gameObject);
                }
            }
        }

        public const float KhufuDuneRadiusM = 32f;
        public const float KhafreDuneRadiusM = 28f;
        public const float MenkaureDuneRadiusM = 20f;
        public const float QueenDuneRadiusM = 14f;

        const string DustRootName = "GizaDesertDust";

        /// <summary>
        /// Ambient sand/dust drift over West Field + desert floor. Few systems, low max particles for VR fill-rate.
        /// No Sprites/Default - URP Particles/Unlit only.
        /// </summary>
        void PlaceDesertDust(Transform root, GizaComplex.Pose pose)
        {
            if (root == null)
                return;
            Transform existing = root.Find(DustRootName);
            if (existing != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(existing.gameObject);
                else
                    Object.DestroyImmediate(existing.gameObject);
            }

            var dustRoot = new GameObject(DustRootName);
            dustRoot.transform.SetParent(root, false);
            dustRoot.transform.localPosition = Vector3.zero;
            dustRoot.transform.localRotation = Quaternion.identity;

            Material mat = MakeDustMaterial();
            // West Field loft - local west of Khufu.
            float kh = KhufuPyramid.BaseMeters * 0.5f;
            Vector3 westLocal = new Vector3(-(kh + 170f), 4.5f, 0f);
            Vector3 westWorld = pose.khufuCenter + pose.rot * westLocal;
            SpawnDustSystem(dustRoot.transform, "GizaDust_WestField", westWorld, pose.rot,
                new Vector3(140f, 8f, 220f), 90, 0.55f, mat);

            // Broad desert ambience around plateau centre (low rate).
            Vector3 desertWorld = new Vector3(root.position.x, pose.surfaceY - GizaComplex.CliffHeightM + 6f, root.position.z);
            SpawnDustSystem(dustRoot.transform, "GizaDust_Desert", desertWorld, Quaternion.identity,
                new Vector3(900f, 24f, 900f), 120, 0.35f, mat);
        }

        static Material MakeDustMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
                sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null)
            {
                Debug.LogError("LabLandscapeApplier: no URP particle/unlit shader for desert dust (refusing Sprites/Default).");
                return null;
            }
            var mat = new Material(sh)
            {
                name = "RELab_DesertDust",
                hideFlags = HideFlags.DontSave,
                color = new Color(0.82f, 0.72f, 0.52f, 0.28f)
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.82f, 0.72f, 0.52f, 0.28f));
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", new Color(0.82f, 0.72f, 0.52f, 0.28f));
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f); // transparent
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f); // alpha
            mat.renderQueue = 3000;
            return mat;
        }

        static void SpawnDustSystem(Transform parent, string name, Vector3 worldPos, Quaternion worldRot,
            Vector3 boxSize, int maxParticles, float rate, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = 12f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            main.startColor = new Color(0.86f, 0.76f, 0.56f, 0.22f);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.02f;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.9f, 0.8f, 0.6f), 0f),
                    new GradientColorKey(new Color(0.75f, 0.65f, 0.45f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.28f, 0.2f),
                    new GradientAlphaKey(0.18f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (mat != null)
                    renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        void PlaceDuneSkirts(Transform root, GizaComplex.Pose pose, Material sand, Vector3 plazaPos)
        {
            SpawnDune(root, "GizaDune_Khufu", pose.khufuCenter, pose.rot, pose.surfaceY, sand,
                KhufuPyramid.BaseMeters * 0.5f, KhufuDuneRadiusM, 3.5f, 40,
                KhufuPyramid.EntranceEastOffsetM, 16f, 0.38f, plazaPos);
            Vector3 khafre = GizaComplex.WorldFromKhufu(pose, -GizaComplex.KhafreWestM, -GizaComplex.KhafreSouthM, 0f);
            SpawnDune(root, "GizaDune_Khafre", khafre, pose.rot, GizaComplex.TerraceY(pose), sand,
                KhafrePyramid.BaseMeters * 0.5f, KhafreDuneRadiusM, 3.1f, 36,
                KhafrePyramid.EntranceEastOffsetM, 16f, 0.40f, plazaPos);
            Vector3 menkaure = GizaComplex.WorldFromKhufu(pose, -GizaComplex.MenkaureWestM, -GizaComplex.MenkaureSouthM, 0f);
            SpawnDune(root, "GizaDune_Menkaure", menkaure, pose.rot, pose.surfaceY, sand,
                MenkaurePyramid.BaseMeters * 0.5f, MenkaureDuneRadiusM, 2.4f, 28,
                0f, 12f, 0.42f, plazaPos);
            GizaPrecinct.Layout L = GizaPrecinct.Compute();
            SpawnQueenDune(root, pose, sand, plazaPos, "GizaDune_G1a", L.g1aEast, L.g1aNorth, L.g1aBase);
            SpawnQueenDune(root, pose, sand, plazaPos, "GizaDune_G1b", L.g1bEast, L.g1bNorth, L.g1bBase);
            SpawnQueenDune(root, pose, sand, plazaPos, "GizaDune_G1c", L.g1cEast, L.g1cNorth, L.g1cBase);
            // Menkaure queens G3a-c (south of Menkaure; same local layout as MenkaurePyramid.BuildQueens).
            float g3South = -MenkaurePyramid.BaseMeters * 0.5f - 8f - MenkaurePyramid.QueenBaseM * 0.5f;
            float[] g3Xs = { 32f, 0f, -32f };
            string[] g3Dunes = { "GizaDune_G3a", "GizaDune_G3b", "GizaDune_G3c" };
            for (int i = 0; i < 3; i++)
            {
                float east = -GizaComplex.MenkaureWestM + g3Xs[i];
                float north = -GizaComplex.MenkaureSouthM + g3South;
                SpawnQueenDune(root, pose, sand, plazaPos, g3Dunes[i], east, north, MenkaurePyramid.QueenBaseM);
            }

            // Thin Oasis skirt around the Sphinx court / enclosure approach.
            Vector3 sphinx = GizaComplex.WorldFromKhufu(pose, GizaComplex.SphinxEastM, -GizaComplex.SphinxSouthM, 0f);
            SpawnDune(root, "GizaDune_Sphinx", sphinx, pose.rot, GizaComplex.CourtY(pose), sand,
                GizaSphinx.LengthM * 0.32f, 16f, 1.6f, 24,
                GizaSphinx.LengthM * 0.42f, 12f, 0.55f, plazaPos);
        }

        void SpawnQueenDune(Transform root, GizaComplex.Pose pose, Material sand, Vector3 plazaPos,
            string name, float east, float north, float baseM)
        {
            Vector3 c = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            SpawnDune(root, name, c, pose.rot, pose.surfaceY, sand,
                baseM * 0.5f, QueenDuneRadiusM, 1.5f, 20, 0f, 0f, 1f, plazaPos);
        }

        void SpawnDune(Transform root, string name, Vector3 center, Quaternion rot, float y,
            Material sand, float innerHalf, float radius, float height, int div,
            float doorX, float doorGap, float northScale, Vector3 plazaPos)
        {
            if (TooClose(center, plazaPos, LabPlazaSize * 0.5f + innerHalf + 6f))
                return;
            Mesh mesh = LabWorldMeshes.BuildDuneSkirt(innerHalf, radius, height, div, doorX, doorGap, northScale);
            Vector3 pos = new Vector3(center.x, y, center.z);
            ApplyLandMesh(root, name, mesh, sand, pos, rot, true, false, true);
        }

        void PlaceSandWashes(Transform root, GizaComplex.Pose pose, Material sand, Vector3 plazaPos)
        {
            float kh = KhufuPyramid.BaseMeters * 0.5f;
            float hf = KhafrePyramid.BaseMeters * 0.5f;
            float mn = MenkaurePyramid.BaseMeters * 0.5f;
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_KhufuWest", -kh - 18f, 0f, 36f, 28f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_KhufuSouth", 8f, -kh - 16f, 34f, 26f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_KhafreWest", -GizaComplex.KhafreWestM - hf - 16f, -GizaComplex.KhafreSouthM, 32f, 24f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_KhafreSouth", -GizaComplex.KhafreWestM, -GizaComplex.KhafreSouthM - hf - 14f, 30f, 22f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_MenkaureWest", -GizaComplex.MenkaureWestM - mn - 12f, -GizaComplex.MenkaureSouthM, 24f, 20f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_MenkaureSouth", -GizaComplex.MenkaureWestM, -GizaComplex.MenkaureSouthM - mn - 12f, 22f, 18f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_SphinxEast",
                GizaComplex.SphinxEastM + GizaSphinx.LengthM * 0.5f + 10f, -GizaComplex.SphinxSouthM, 28f, 20f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_SphinxSouth",
                GizaComplex.SphinxEastM, -GizaComplex.SphinxSouthM - GizaSphinx.WidthM * 0.5f - 12f, 26f, 18f);
            PlaceWash(root, pose, sand, plazaPos, "GizaSandWash_G3South",
                -GizaComplex.MenkaureWestM, -GizaComplex.MenkaureSouthM - mn - 8f - MenkaurePyramid.QueenBaseM - 10f, 48f, 20f);
        }

        void PlaceWash(Transform root, GizaComplex.Pose pose, Material sand, Vector3 plazaPos,
            string name, float east, float north, float sizeX, float sizeZ)
        {
            Vector3 world = GizaComplex.WorldFromKhufu(pose, east, north, 0f);
            if (TooClose(world, plazaPos, LabPlazaSize * 0.5f + 18f))
                return;
            Mesh mesh = LabWorldMeshes.BuildSandWash(sizeX, sizeZ, 10, 0.11f);
            Vector3 pos = new Vector3(world.x, pose.surfaceY + 0.04f, world.z);
            ApplyLandMesh(root, name, mesh, sand, pos, pose.rot, true, false, true);
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
            GizaPrecinct.Layout L = GizaPrecinct.Compute();
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
                if (TooClose(p, GizaComplex.WorldFromKhufu(pose, L.g1aEast, L.g1aNorth, 0f), 50f))
                    continue;
                if (TooClose(p, GizaComplex.WorldFromKhufu(pose, L.g1bEast, L.g1bNorth, 0f), 50f))
                    continue;
                if (TooClose(p, GizaComplex.WorldFromKhufu(pose, L.g1cEast, L.g1cNorth, 0f), 50f))
                    continue;
                if (TooClose(p, GizaComplex.WorldFromKhufu(pose, L.valleyEast, L.valleyNorth, 0f), 55f))
                    continue;
                if (TooClose(p, GizaComplex.WorldFromKhufu(pose, L.sphinxTempleEast, L.sphinxTempleNorth, 0f), 50f))
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
