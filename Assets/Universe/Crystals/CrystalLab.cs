using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Audio;
using RealityEngine.Challenges;
using RealityEngine.Combat;
using RealityEngine.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Crystals
{
    /// <summary>
    /// The Crystal Lab: a runtime-built area near the plaza (no scene edits) with seven benches - piezo, pyro,
    /// quartz resonator, calcite birefringence, triboluminescence, hot-ice growth and ferroelectric hysteresis.
    /// Reach it from toolbelt WORLD > Crystal Lab or the CRYSTAL tab (station chips travel straight to a bench);
    /// desktop PageDown cycles the benches. Built 4 s after load, one bench per frame (no hitch), at the first
    /// clear, level site around the plaza that is not the combat arena.
    /// </summary>
    public sealed class CrystalLab : MonoBehaviour
    {
        public const string RootName = "QhysicsCrystalLab";
        public const float HalfX = 6.8f, HalfZ = 5.3f;
        public static CrystalLab Instance { get; private set; }
        public static bool Built => Instance != null && Instance._built;

        static readonly string[][] StationKeys =
        {
            new[] { "piezo", "piezoelectricity", "piezo station" },
            new[] { "pyro", "pyroelectricity", "pyro station" },
            new[] { "resonator", "quartz resonator", "watch crystal" },
            new[] { "calcite", "birefringence" },
            new[] { "tribo", "triboluminescence" },
            new[] { "hot ice", "growth", "crystal growth" },
            new[] { "ferro", "ferroelectric", "curie" }
        };

        readonly List<CrystalStation> _stations = new List<CrystalStation>();
        bool _built;
        int _cycle = -1;
        int _pendingIndex = -2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || Instance != null)
                return;
            var go = new GameObject(RootName);
            Instance = go.AddComponent<CrystalLab>();
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            StartCoroutine(BuildLater());
        }

        IEnumerator BuildLater()
        {
            yield return new WaitForSecondsRealtime(4f);
            float waited = 0f;
            GameObject origin = null;
            while (origin == null && waited < 20f)
            {
                origin = GameObject.Find(RealityEngine.XR.LabPlayerSpawn.OriginName);
                if (origin == null)
                {
                    waited += 0.5f;
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
            Vector3 plaza = origin != null ? origin.transform.position : Vector3.zero;
            ChooseSite(plaza, out Vector3 center, out Quaternion rot, out bool clear);
            transform.SetPositionAndRotation(center, rot);
            BuildShell();
            yield return null;

            var types = new Func<GameObject, CrystalStation>[]
            {
                g => g.AddComponent<PiezoStation>(),
                g => g.AddComponent<PyroStation>(),
                g => g.AddComponent<ResonatorStation>(),
                g => g.AddComponent<BirefringenceStation>(),
                g => g.AddComponent<TriboStation>(),
                g => g.AddComponent<GrowthStation>(),
                g => g.AddComponent<FerroStation>()
            };
            // Back row (3), right side (2), left side (2). Local -Z faces the plaza / entrance.
            var poses = new (Vector3 p, float yaw)[]
            {
                (new Vector3(-4.2f, 0f, 3.2f), 0f),
                (new Vector3(0f, 0f, 3.2f), 0f),
                (new Vector3(4.2f, 0f, 3.2f), 0f),
                (new Vector3(5.4f, 0f, 0.4f), 90f),
                (new Vector3(5.4f, 0f, -2.6f), 90f),
                (new Vector3(-5.4f, 0f, -2.6f), -90f),
                (new Vector3(-5.4f, 0f, 0.4f), -90f)
            };
            for (int i = 0; i < types.Length; i++)
            {
                var g = new GameObject("CrystalStation_" + i);
                g.transform.SetParent(transform, false);
                g.transform.localPosition = poses[i].p;
                g.transform.localRotation = Quaternion.Euler(0f, poses[i].yaw, 0f);
                CrystalStation st = types[i](g);
                st.Index = i;
                try
                {
                    st.Build();
                    g.name = "CrystalStation_" + st.Id;
                    _stations.Add(st);
                }
                catch (Exception e)
                {
                    Debug.LogError("CrystalLab: station " + i + " failed to build: " + e);
                }
                yield return null;
            }
            _built = true;
            Debug.Log("CrystalLab: built " + _stations.Count + " stations at " + center.ToString("F1") + (clear ? "" : " (no fully clear site; nearest ground)")
                + ". Toolbelt WORLD > Crystal Lab, CRYSTAL tab, or PageDown.");
            if (_pendingIndex > -2)
            {
                int idx = _pendingIndex;
                _pendingIndex = -2;
                TravelInternal(idx);
            }
        }

        static void ChooseSite(Vector3 plaza, out Vector3 center, out Quaternion rot, out bool clear)
        {
            Vector3[] offsets =
            {
                new Vector3(-22f, 0f, 0f), new Vector3(0f, 0f, -22f), new Vector3(22f, 0f, 0f), new Vector3(0f, 0f, 22f),
                new Vector3(-16f, 0f, -16f), new Vector3(16f, 0f, -16f), new Vector3(-16f, 0f, 16f), new Vector3(16f, 0f, 16f),
                new Vector3(-34f, 0f, 0f), new Vector3(0f, 0f, -34f), new Vector3(-28f, 0f, 14f), new Vector3(14f, 0f, -28f)
            };
            center = plaza + offsets[0];
            rot = Quaternion.identity;
            clear = false;
            bool haveFallback = false;
            int bestBlockers = int.MaxValue;
            Vector3 fb = center;
            Quaternion fbRot = rot;
            Vector3 arena = CombatArena.Instance != null ? CombatArena.Instance.Center : new Vector3(float.MaxValue, 0f, float.MaxValue);
            foreach (Vector3 o in offsets)
            {
                if (!CombatArena.GroundAt(plaza + o, out Vector3 g))
                    continue;
                if (Mathf.Abs(g.y - plaza.y) > 3f)
                    continue;
                Vector3 flatA = new Vector3(arena.x - g.x, 0f, arena.z - g.z);
                if (flatA.magnitude < CombatArena.Radius + HalfX + 2f)
                    continue;
                Vector3 toPlaza = plaza - g;
                toPlaza.y = 0f;
                if (toPlaza.sqrMagnitude < 0.01f)
                    toPlaza = Vector3.back;
                Quaternion r = Quaternion.LookRotation(-toPlaza.normalized);
                int blockers = SiteBlockers(g, r);
                if (blockers == 0)
                {
                    center = g;
                    rot = r;
                    clear = true;
                    return;
                }
                // No clear site yet: remember the least obstructed grounded one (not just the first one found).
                if (blockers < bestBlockers)
                {
                    bestBlockers = blockers;
                    haveFallback = true;
                    fb = g;
                    fbRot = r;
                }
            }
            if (haveFallback)
            {
                center = fb;
                rot = fbRot;
                Debug.LogWarning("CrystalLab: no fully clear site near the plaza; using the least obstructed one (" + bestBlockers + " blocker score).");
            }
            else
            {
                Debug.LogWarning("CrystalLab: no ground found around the plaza; building at plaza height (the lab floor has its own collider).");
            }
        }

        /// <summary>0 = clear and level. Otherwise the number of non-player colliders in the lab volume, +100 per corner that is
        /// unlevel (> 0.6 m) or has no ground, so a fallback prefers a crowded-but-level site over a cliff edge.</summary>
        static int SiteBlockers(Vector3 g, Quaternion r)
        {
            int n = 0;
            var cols = UnityEngine.Physics.OverlapBox(g + Vector3.up * 1.45f, new Vector3(HalfX, 1.3f, HalfZ), r, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || CombatInputGate.IsPlayerCollider(cols[i]))
                    continue;
                n++;
            }
            // Also require roughly level ground at the four corners.
            Vector3[] corners = { new Vector3(HalfX, 0f, HalfZ), new Vector3(-HalfX, 0f, HalfZ), new Vector3(HalfX, 0f, -HalfZ), new Vector3(-HalfX, 0f, -HalfZ) };
            for (int i = 0; i < corners.Length; i++)
            {
                if (!CombatArena.GroundAt(g + r * corners[i], out Vector3 c) || Mathf.Abs(c.y - g.y) > 0.6f)
                    n += 100;
            }
            return n;
        }

        void BuildShell()
        {
            Material floor = CrystalFx.Solid(new Color(0.06f, 0.075f, 0.09f), 0.2f, 0.6f);
            Material rim = CrystalFx.Emissive(new Color(0f, 0.85f, 1f), 0.9f);
            Material post = CrystalFx.Solid(new Color(0.1f, 0.11f, 0.13f), 0.5f, 0.5f);
            var shell = new GameObject("Shell").transform;
            shell.SetParent(transform, false);
            CrystalFx.Prim(PrimitiveType.Cube, "Floor", shell, new Vector3(0f, 0.015f, 0f), new Vector3(HalfX * 2f, 0.03f, HalfZ * 2f), floor);
            CrystalFx.Prim(PrimitiveType.Cube, "RimBack", shell, new Vector3(0f, 0.035f, HalfZ), new Vector3(HalfX * 2f, 0.012f, 0.06f), rim, false);
            CrystalFx.Prim(PrimitiveType.Cube, "RimL", shell, new Vector3(-HalfX, 0.035f, 0f), new Vector3(0.06f, 0.012f, HalfZ * 2f), rim, false);
            CrystalFx.Prim(PrimitiveType.Cube, "RimR", shell, new Vector3(HalfX, 0.035f, 0f), new Vector3(0.06f, 0.012f, HalfZ * 2f), rim, false);
            CrystalFx.Prim(PrimitiveType.Cube, "RimFrontL", shell, new Vector3(-HalfX * 0.6f, 0.035f, -HalfZ), new Vector3(HalfX * 0.8f, 0.012f, 0.06f), rim, false);
            CrystalFx.Prim(PrimitiveType.Cube, "RimFrontR", shell, new Vector3(HalfX * 0.6f, 0.035f, -HalfZ), new Vector3(HalfX * 0.8f, 0.012f, 0.06f), rim, false);

            // Entrance sign on two posts (readable from the plaza side).
            CrystalFx.Prim(PrimitiveType.Cube, "SignPostL", shell, new Vector3(-1.9f, 1.3f, -HalfZ), new Vector3(0.12f, 2.6f, 0.12f), post);
            CrystalFx.Prim(PrimitiveType.Cube, "SignPostR", shell, new Vector3(1.9f, 1.3f, -HalfZ), new Vector3(0.12f, 2.6f, 0.12f), post);
            CrystalFx.Prim(PrimitiveType.Cube, "SignBoard", shell, new Vector3(0f, 2.75f, -HalfZ), new Vector3(4.0f, 0.5f, 0.06f), post, false);
            var title = CrystalFx.Text(shell, "SignText", "CRYSTAL LAB", 3.2f, new Color(0f, 0.9f, 1f), new Vector3(0f, 2.78f, -HalfZ - 0.04f), new Vector2(3.8f, 0.45f));
            title.fontStyle = TMPro.FontStyles.Bold;
            var back = CrystalFx.Text(shell, "SignTextIn", "CRYSTAL LAB", 3.2f, new Color(0f, 0.9f, 1f), new Vector3(0f, 2.78f, -HalfZ + 0.04f), new Vector2(3.8f, 0.45f));
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            CrystalFx.Text(shell, "SignSub", "piezo  pyro  quartz  calcite  tribo  hot ice  ferro", 1.3f, new Color(0.7f, 0.9f, 1f), new Vector3(0f, 2.42f, -HalfZ - 0.04f), new Vector2(3.8f, 0.2f));

            // Welcome board just inside the entrance.
            var welcome = CrystalFx.Text(shell, "Welcome",
                "<b>WELCOME TO THE CRYSTAL LAB</b>\nSeven benches. Each has a guide card: WHAT / MATH / TRY THIS / LATTICE / CHALLENGE.\n"
                + "Toolbelt CRYSTAL tab: jump to a bench or spawn crystals.  Desktop: PageDown = next bench.\n"
                + "Point at anything and read the INSPECT strip.", 1.1f, new Color(0.8f, 0.95f, 1f),
                new Vector3(0f, 1.55f, -HalfZ + 1.4f), new Vector2(3.4f, 0.6f));
            welcome.transform.localRotation = Quaternion.identity;
            CrystalFx.Prim(PrimitiveType.Cube, "WelcomeBack", shell, new Vector3(0f, 1.55f, -HalfZ + 1.43f), new Vector3(3.5f, 0.68f, 0.03f), CrystalFx.Solid(new Color(0.03f, 0.05f, 0.07f), 0.2f, 0.5f), false);

            // Decorative quartz cluster with the lab's one fill light.
            var cluster = new GameObject("QuartzCluster").transform;
            cluster.SetParent(shell, false);
            cluster.localPosition = new Vector3(0f, 0.03f, -0.2f);
            Material q = CrystalFx.Crystal(new Color(0.75f, 0.9f, 1f, 0.55f), new Color(0.05f, 0.25f, 0.35f));
            CrystalFx.Prim(PrimitiveType.Cylinder, "Plinth", cluster, new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.15f, 0.9f), post);
            var rnd = new System.Random(3);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f + (float)rnd.NextDouble() * 15f;
                float tilt = i == 0 ? 0f : 12f + (float)rnd.NextDouble() * 22f;
                float h = i == 0 ? 1.1f : 0.45f + (float)rnd.NextDouble() * 0.45f;
                CrystalFx.MeshObject("Point" + i, cluster, CrystalGadgets.HexPoint, q, new Vector3(0f, 0.3f, 0f) + Quaternion.Euler(0f, a, 0f) * new Vector3(i == 0 ? 0f : 0.14f, 0f, 0f),
                    Quaternion.Euler(0f, a, tilt), new Vector3(0.16f, h, 0.16f) * (i == 0 ? 1.2f : 1f));
            }
            var lightGo = new GameObject("LabLight");
            lightGo.transform.SetParent(shell, false);
            lightGo.transform.localPosition = new Vector3(0f, 3.2f, 0.4f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 11f;
            l.intensity = 1.1f;
            l.color = new Color(0.75f, 0.92f, 1f);
            l.shadows = LightShadows.None;
            CrystalFlashPool.Ensure();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null || CombatInputGate.Blocked)
                return;
            if (kb.pageDownKey.wasPressedThisFrame)
                CycleNext();
#endif
        }

        // ── Travel ──

        public static bool CycleNext()
        {
            if (Instance == null)
                return false;
            Instance._cycle = (Instance._cycle + 1) % Mathf.Max(1, StationKeys.Length);
            return Instance.Travel(Instance._cycle);
        }

        /// <summary>index -1 = lab entrance overview.</summary>
        public bool Travel(int index)
        {
            if (!_built)
            {
                _pendingIndex = index;
                QhysicsSfx.Play2D(SfxId.UiTick, 0.4f);
                Debug.Log("CrystalLab: still building - will travel when ready.");
                return true;
            }
            return TravelInternal(index);
        }

        bool TravelInternal(int index)
        {
            Vector3 land, look;
            string label;
            if (index >= 0 && index < _stations.Count && _stations[index] != null)
            {
                land = _stations[index].StandPoint;
                look = _stations[index].LookPoint;
                label = _stations[index].Title;
                _cycle = index;
            }
            else
            {
                land = transform.TransformPoint(new Vector3(0f, 0f, -HalfZ + 0.6f));
                look = transform.TransformPoint(new Vector3(0f, 1.4f, 2f));
                label = "Crystal Lab";
            }
            if (!MoveOrigin(land, look))
            {
                QhysicsSfx.Play2D(SfxId.Denied, 0.5f);
                return false;
            }
            QhysicsSfx.Play2D(SfxId.Unlock, 0.5f);
            Debug.Log("CrystalLab: travel -> " + label + ".");
            return true;
        }

        static bool MoveOrigin(Vector3 land, Vector3 lookAt)
        {
            GameObject originGo = GameObject.Find(RealityEngine.XR.LabPlayerSpawn.OriginName);
            if (originGo == null)
                return false;
            var cc = originGo.GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (cc != null)
                cc.enabled = false;
            Transform o = originGo.transform;
            Camera cam = CombatInputGate.ViewCamera();
            Vector3 look = lookAt - land;
            look.y = 0f;
            if (look.sqrMagnitude < 1e-6f)
                look = Vector3.forward;
            float camYaw = cam != null ? cam.transform.eulerAngles.y - o.eulerAngles.y : 0f;
            o.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(look).eulerAngles.y - camYaw, 0f);
            Vector3 headOffset = cam != null ? cam.transform.position - o.position : Vector3.zero;
            headOffset.y = 0f;
            if (CombatArena.GroundAt(land + Vector3.up * 0.5f, out Vector3 g) && Mathf.Abs(g.y - land.y) < 0.6f)
                land.y = g.y;
            o.position = land - headOffset + Vector3.up * 0.05f;
            if (cc != null)
                cc.enabled = had;
            var desktop = DesktopPlayerController.Instance;
            if (desktop != null)
                desktop.SyncAfterTeleport();
            return true;
        }

        /// <summary>Toolbelt hook: "crystal lab" / station names travel. Returns true when handled.</summary>
        public static bool TryToolbeltAction(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            key = key.Trim().ToLowerInvariant();
            int idx = -2;
            if (key == "crystal lab" || key == "crystals" || key == "crystal")
                idx = -1;
            else
            {
                for (int i = 0; i < StationKeys.Length && idx == -2; i++)
                    if (Array.IndexOf(StationKeys[i], key) >= 0)
                        idx = i;
            }
            if (idx == -2)
                return false;
            if (Instance == null)
                Boot();
            return Instance.Travel(idx);
        }

        /// <summary>QhysicsGadgets.SpawnByLabel hook (also used by sandbox restore). Null when the label is not a crystal gadget.</summary>
        public static GameObject TrySpawnGadget(string key, Vector3 pos)
        {
            return CrystalGadgets.Spawn(key, pos);
        }
    }

    /// <summary>Spawnable Crystal Lab gadgets (roots named Gadget_* so sandbox save/restore and lost-item return pick them up).</summary>
    public static class CrystalGadgets
    {
        static Mesh _hexPoint;

        /// <summary>Unit hexagonal prism with a 6-sided pyramid tip (quartz habit), 1 m tall, 1 m across. Flat shaded.</summary>
        public static Mesh HexPoint
        {
            get
            {
                if (_hexPoint != null)
                    return _hexPoint;
                var v = new List<Vector3>();
                var t = new List<int>();
                const float body = 0.72f;
                Vector3 Ring(int k, float y) => new Vector3(Mathf.Cos(k * Mathf.PI / 3f) * 0.5f, y, Mathf.Sin(k * Mathf.PI / 3f) * 0.5f);
                for (int k = 0; k < 6; k++)
                {
                    int s = v.Count;
                    v.Add(Ring(k, 0f)); v.Add(Ring(k + 1, 0f)); v.Add(Ring(k + 1, body)); v.Add(Ring(k, body));
                    t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
                    s = v.Count;
                    v.Add(Ring(k, body)); v.Add(Ring(k + 1, body)); v.Add(new Vector3(0f, 1f, 0f));
                    t.Add(s); t.Add(s + 2); t.Add(s + 1);
                    s = v.Count;
                    v.Add(Ring(k, 0f)); v.Add(Ring(k + 1, 0f)); v.Add(Vector3.zero);
                    t.Add(s); t.Add(s + 1); t.Add(s + 2);
                }
                _hexPoint = new Mesh { name = "Crystal_HexPoint" };
                _hexPoint.SetVertices(v);
                _hexPoint.SetTriangles(t, 0);
                _hexPoint.RecalculateNormals();
                _hexPoint.RecalculateBounds();
                // Ensure outward winding (flip if the first side face points inward).
                Vector3 n = Vector3.Cross(v[2] - v[0], v[1] - v[0]);
                Vector3 c = (v[0] + v[1] + v[2] + v[3]) * 0.25f;
                c.y = 0f;
                if (Vector3.Dot(n, c) < 0f)
                {
                    var tri = _hexPoint.triangles;
                    for (int i = 0; i < tri.Length; i += 3) { int x = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = x; }
                    _hexPoint.triangles = tri;
                    _hexPoint.RecalculateNormals();
                }
                return _hexPoint;
            }
        }

        static string Norm(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return key.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        }

        public static GameObject Spawn(string key, Vector3 pos)
        {
            switch (Norm(key))
            {
                case "quartz":
                case "quartzcrystal": return SpawnQuartz(pos);
                case "tourmaline": return SpawnPyro(pos, true);
                case "litao3":
                case "lithiumtantalate": return SpawnPyro(pos, false);
                case "calciterhomb":
                case "icelandspar": return SpawnCalcite(pos);
                case "polarizer":
                case "polariser": return PolarizerSheet.Spawn(pos);
                case "igniter":
                case "piezoigniter": return CrystalIgniter.Spawn(pos);
                case "hammer":
                case "crystalhammer": return CreateHammer("Gadget_CrystalHammer", pos, Quaternion.identity);
                case "wintergreen":
                case "wintergreenmint": return TriboCandy.Create(TriboCandy.Kind.Wintergreen, pos, "Gadget_Wintergreen");
                case "sugarcube": return TriboCandy.Create(TriboCandy.Kind.Sugar, pos, "Gadget_SugarCube");
                case "quartzpebble": return TriboCandy.Create(TriboCandy.Kind.Quartz, pos, "Gadget_QuartzPebble");
                case "seedcrystal": return SeedCrystal.Create(pos, SeedCrystal.RootName);
            }
            return null;
        }

        public static GameObject CreateHammer(string name, Vector3 pos, Quaternion rot)
        {
            var hammer = new GameObject(name);
            hammer.transform.SetPositionAndRotation(pos, rot);
            Material steel = CrystalFx.Solid(new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.55f);
            CrystalFx.Prim(PrimitiveType.Cylinder, "Handle", hammer.transform, Vector3.zero, new Vector3(0.025f, 0.13f, 0.025f), CrystalFx.Solid(new Color(0.4f, 0.27f, 0.15f)));
            CrystalFx.Prim(PrimitiveType.Cube, "Head", hammer.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.11f, 0.04f, 0.04f), steel);
            CrystalFx.MakeGrabbable(hammer, 0.45f);
            hammer.AddComponent<CrystalHammer>();
            return hammer;
        }

        static GameObject SpawnQuartz(Vector3 pos)
        {
            var root = new GameObject("Gadget_QuartzCrystal");
            root.transform.position = pos;
            CrystalFx.MeshObject("Quartz", root.transform, HexPoint, CrystalFx.Crystal(new Color(0.85f, 0.93f, 1f, 0.5f), new Color(0.05f, 0.12f, 0.16f)),
                new Vector3(0f, -0.06f, 0f), Quaternion.identity, new Vector3(0.045f, 0.13f, 0.045f));
            Material gold = CrystalFx.Solid(new Color(0.95f, 0.75f, 0.3f), 1f, 0.75f);
            CrystalFx.Prim(PrimitiveType.Cube, "ElectrodeA", root.transform, new Vector3(0.0225f, -0.02f, 0f), new Vector3(0.002f, 0.04f, 0.025f), gold, false);
            CrystalFx.Prim(PrimitiveType.Cube, "ElectrodeB", root.transform, new Vector3(-0.0225f, -0.02f, 0f), new Vector3(0.002f, 0.04f, 0.025f), gold, false);
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.022f; col.height = 0.13f; col.center = new Vector3(0f, 0.005f, 0f);
            var pz = root.AddComponent<PiezoCrystal>();
            pz.LoadCap = 2e-12;
            var ga = new GameObject("GapA").transform; ga.SetParent(root.transform, false); ga.localPosition = new Vector3(0.024f, -0.02f, 0f);
            var gb = new GameObject("GapB").transform; gb.SetParent(root.transform, false); gb.localPosition = new Vector3(-0.024f, -0.02f, 0f);
            pz.GapA = ga; pz.GapB = gb;
            var grab = CrystalFx.MakeGrabbable(root, 0.06f);
            var sq = root.AddComponent<QuartzSqueeze>();
            grab.activated.AddListener(_ => sq.Set(true));
            grab.deactivated.AddListener(_ => sq.Set(false));
            pz.Struck = v => QhysicsSfx.PlayAt(SfxId.CrystalRing, root.transform.position, 0.35f, 1.2f);
            return root;
        }

        static GameObject SpawnPyro(Vector3 pos, bool tourmaline)
        {
            var root = new GameObject(tourmaline ? "Gadget_Tourmaline" : "Gadget_LiTaO3");
            root.transform.position = pos;
            GameObject body;
            if (tourmaline)
            {
                body = CrystalFx.MeshObject("Prism", root.transform, HexPoint, null, new Vector3(0f, -0.02f, 0f), Quaternion.identity, new Vector3(0.015f, 0.045f, 0.015f));
                root.AddComponent<CapsuleCollider>().radius = 0.008f;
                root.GetComponent<CapsuleCollider>().height = 0.045f;
            }
            else
            {
                body = CrystalFx.Prim(PrimitiveType.Cube, "Plate", root.transform, Vector3.zero, new Vector3(0.03f, 0.005f, 0.03f), null, false);
                root.AddComponent<BoxCollider>().size = new Vector3(0.03f, 0.006f, 0.03f);
            }
            var pc = root.AddComponent<PyroCrystal>();
            pc.Tourmaline = tourmaline;
            pc.Init(body.GetComponent<Renderer>());
            pc.ThermalTau = tourmaline ? 40f : 20f; // small free crystal: faster than the bench plate
            root.AddComponent<PyroCorona>();
            CrystalFx.MakeGrabbable(root, tourmaline ? 0.03f : 0.03f);
            return root;
        }

        static GameObject SpawnCalcite(Vector3 pos)
        {
            var root = new GameObject("Gadget_CalciteRhomb");
            root.transform.position = pos;
            float w = 0.05f, h = 0.035f;
            var mesh = BirefringenceStation.Parallelepiped(new Vector3(w, 0f, 0f), new Vector3(w * 0.35f, 0f, w * 0.94f), new Vector3(h * 0.33f, h, h * 0.33f));
            CrystalFx.MeshObject("Rhomb", root.transform, mesh, CrystalFx.Crystal(new Color(0.95f, 0.97f, 1f, 0.3f), new Color(0.03f, 0.04f, 0.05f)), Vector3.zero, Quaternion.identity, Vector3.one);
            root.AddComponent<BoxCollider>().size = new Vector3(w * 1.25f, h, w * 1.1f);
            root.AddComponent<CalciteGadget>();
            CrystalFx.MakeGrabbable(root, 0.12f);
            return root;
        }
    }

    /// <summary>Hand squeeze on the spawned quartz: trigger / LMB applies ~100 N (firm pinch) and releases it.</summary>
    public sealed class QuartzSqueeze : MonoBehaviour, IDesktopActivatable
    {
        PiezoCrystal _p;
        bool _on;
        void Awake() { _p = GetComponent<PiezoCrystal>(); }
        public void Set(bool on)
        {
            _on = on;
            if (_p != null) _p.StaticForce = on ? 100.0 : 0.0;
            QhysicsSfx.PlayAt(SfxId.CrystalClick, transform.position, 0.2f, on ? 1.2f : 0.9f);
        }
        public void DesktopActivate(int delta) => Set(!_on);
    }

    /// <summary>Free pyro crystal: a faint crackle and blue glints while its surface charge is at the corona limit.</summary>
    public sealed class PyroCorona : MonoBehaviour
    {
        PyroCrystal _p;
        float _next;
        void Awake() { _p = GetComponent<PyroCrystal>(); }
        void Update()
        {
            if (_p == null || !_p.Clamped || Time.time < _next)
                return;
            _next = Time.time + 0.35f + UnityEngine.Random.value * 0.5f;
            QhysicsSfx.PlayAt(SfxId.PyroTick, transform.position, 0.15f, 0.8f + UnityEngine.Random.value * 0.4f);
            if (Math.Abs(_p.LastRateKps) > 0.5)
                CrystalFx.Flash(transform.position + UnityEngine.Random.insideUnitSphere * 0.01f, new Color(0.55f, 0.65f, 1f), 0.25f, 0.05f, 0.006f);
        }
    }

    public sealed class CalciteGadget : MonoBehaviour, ICrystalInspectable
    {
        public string InspectStats(bool held) => "Calcite rhomb (Iceland spar) | n_o 1.658  n_e 1.486";
        public string InspectDetail() => "walk-off 6.2 deg -> image shift = 0.109 x thickness | cleaves at 75 / 105 deg";
        public string InspectHint(bool held) => "See the Birefringence bench for the double image";
    }

    /// <summary>Crystal Lab challenge table, appended to the starter campaign (chapter "Crystal Lab").</summary>
    public static class CrystalChallenges
    {
        public static readonly string[] Ids =
        {
            "crystal_piezo_1kv", "crystal_piezo_burner", "crystal_pyro", "crystal_watch",
            "crystal_calcite", "crystal_tribo", "crystal_hotice", "crystal_ferro"
        };

        static ChallengeObjective Goal(string id, string text, float target = 0f)
        {
            return new ChallengeObjective { type = ObjectiveType.CrystalGoal, targetTag = id, targetValue = target, displayText = text };
        }

        static ChallengeDefinition Def(string id, string title, string desc, string hint, float two, float three, params ChallengeObjective[] obj)
        {
            return new ChallengeDefinition
            {
                id = id,
                title = title,
                description = desc,
                mentorHint = hint,
                prerequisiteId = "",
                objectives = obj,
                starThresholds = new StarThresholds { twoStarTimeSeconds = two, threeStarTimeSeconds = three, threeStarMaxComponents = 0 }
            };
        }

        public static ChallengeDefinition[] Build()
        {
            return new[]
            {
                Def("crystal_piezo_1kv", "Squeeze 1 kV", "Use the screw press to squeeze 1000 V out of the quartz element (no hammer).",
                    "Quartz gives ~0.82 V per newton here: you need ~1220 N. Turn the wheel steadily - the charge leaks away (tau ~ 28 s).",
                    60f, 25f, Goal("piezo_press_v", "Press the quartz to 1 kV", 1000f)),
                Def("crystal_piezo_burner", "Piezo Igniter", "Turn on the gas and light the burner with a piezoelectric spark.",
                    "Gas valve ON, then strike the crystal hard (or switch to PZT), or click the toolbelt Igniter at the nozzle.",
                    60f, 25f, Goal("burner_lit_piezo", "Light the burner with a piezo spark")),
                Def("crystal_pyro", "Ash Puller", "Make the pyroelectric crystal lift paper - once by heating, once by cooling.",
                    "Heat lamp ON lifts the bits (top face -). Then lamp OFF, cold plate ON: cooling flips the sign (+) and they dance again.",
                    150f, 80f, Goal("pyro_lift", "Lift paper by HEATING"), Goal("pyro_reverse", "Lift paper by COOLING (opposite charge)")),
                Def("crystal_watch", "Watchmaker", "Laser-trim the gold on the quartz tuning fork until the watch is within +/-10 ppm (under 1 s/day).",
                    "Use -100 ng until the beats slow, then -10, -1, -0.1 ng. Keep the oven at 25 C.",
                    120f, 60f, Goal("watch_tuned", "Tune to 32768 Hz +/-10 ppm")),
                Def("crystal_calcite", "Extinction", "Turn the polarizer over the calcite until one of the two images disappears.",
                    "The two images are polarised at 90 deg to each other. Twist the violet ring slowly; one dot fades to nothing.",
                    60f, 25f, Goal("calcite_extinct", "Extinguish one image (< 2%)")),
                Def("crystal_tribo", "Sparks in the Dark", "Make a wintergreen mint and a quartz pebble flash.",
                    "Smash the mint on the anvil hard (> 0.8 m/s) or use DROP PESTLE. Bang the quartz pebbles together.",
                    90f, 40f, Goal("tribo_wintergreen", "Blue flash from wintergreen"), Goal("tribo_quartz", "Orange flash from quartz")),
                Def("crystal_hotice", "Hot Ice", "Seed the supercooled sodium acetate and let it crystallise completely while it is still above 50 C.",
                    "Dip the seed rod's white tip into the clear solution (or Seed centre). Seeding at 22 C gives the fastest front and ~54 C.",
                    90f, 45f, Goal("hot_ice", "Crystallise it, above 50 C")),
                Def("crystal_ferro", "Curie Point", "See a full ferroelectric hysteresis loop, then heat BaTiO3 past its Curie point until the loop collapses.",
                    "Keep the drive amplitude above Ec (~1 kV/mm at 25 C) for one loop, then use T 140 C.",
                    120f, 60f, Goal("ferro_loop", "Draw a hysteresis loop below Tc"), Goal("ferro_curie", "Collapse the loop above Tc (125 C)"))
            };
        }

        /// <summary>Return the campaign with the crystal challenges appended (no duplicates).</summary>
        public static ChallengeDefinition[] Append(ChallengeDefinition[] campaign)
        {
            var list = new List<ChallengeDefinition>(campaign ?? new ChallengeDefinition[0]);
            foreach (ChallengeDefinition d in Build())
            {
                bool exists = false;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].id == d.id) { exists = true; break; }
                if (!exists)
                    list.Add(d);
            }
            return list.ToArray();
        }
    }
}
