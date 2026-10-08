using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;
using RealityEngine.Player;
using TMPro;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Runtime arena dressing (no scene edits), dark sci-fi stone with cyan accents, Quest-friendly: primitives, shared
    /// materials, 2 extra point lights (braziers, no shadows). Entrance arch, two weapon racks (grabbable, auto-restock),
    /// breakable crates / barrels / volatile barrels (respawn after 40 s and at each wave), braziers that ignite blades,
    /// crystal torches on the wall tops, swaying pillar banners, outer columns + obelisk, floor rune ring, heavy bag.
    /// </summary>
    public sealed class ArenaDecor : MonoBehaviour
    {
        public static ArenaDecor Instance { get; private set; }

        CombatArena _arena;
        Transform _root, _props;

        Material _stone, _stoneLight, _rim, _rimHot, _metal, _cloth;

        // animated bits
        readonly List<(Transform t, Quaternion baseRot, float phase)> _banners = new List<(Transform, Quaternion, float)>();
        readonly List<(Transform t, Vector3 basePos, float phase)> _crystals = new List<(Transform, Vector3, float)>();
        readonly List<(Light l, Vector3 flame)> _braziers = new List<(Light, Vector3)>();

        sealed class RackSlot
        {
            public WeaponKind kind;
            public Vector3 pos;
            public Quaternion rot;
            public PhysicsWeapon w;
            public float leftAt = -1f;
        }
        readonly List<RackSlot> _rack = new List<RackSlot>();

        sealed class BreakSlot
        {
            public ArenaBreakable.Kind kind;
            public Vector3 pos;      // world
            public Quaternion rot;   // world
            public ArenaBreakable obj;
            public float goneAt = -1f;
        }
        readonly List<BreakSlot> _breakables = new List<BreakSlot>();

        float _nextRack, _nextBreak, _nextFire;

        public static ArenaDecor Build(CombatArena arena)
        {
            if (Instance != null || arena == null) return Instance;
            Instance = arena.gameObject.AddComponent<ArenaDecor>();
            Instance._arena = arena;
            try { Instance.Create(); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            return Instance;
        }

        static Vector3 Polar(float deg, float r)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
        }

        /// <summary>Local rotation whose +Z faces the arena centre from the given angle.</summary>
        static Quaternion FaceCentre(float deg) => Quaternion.LookRotation(-Polar(deg, 1f));

        static GameObject P(PrimitiveType t, string name, Transform parent, Vector3 lp, Vector3 ls, Material m, bool collider = true, bool shadows = true)
        {
            var g = CombatUtil.Prim(t, name, parent, lp, ls, m, collider);
            if (!shadows)
            {
                var r = g.GetComponent<Renderer>();
                if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return g;
        }

        Transform Group(string name, Vector3 localPos, Quaternion localRot)
        {
            var g = new GameObject(name).transform;
            g.SetParent(_root, false);
            g.localPosition = localPos;
            g.localRotation = localRot;
            return g;
        }

        /// <summary>Group outside the arena floor, snapped to the real ground (skipped if the ground is far off).</summary>
        Transform OuterGroup(string name, Vector3 localPos, Quaternion localRot)
        {
            Vector3 w = _arena.transform.TransformPoint(localPos);
            if (CombatArena.GroundAt(w, out Vector3 g))
            {
                if (Mathf.Abs(g.y - _arena.Center.y) > 2f) return null;
                localPos.y = _arena.transform.InverseTransformPoint(g).y;
                w = g;
            }
            // Don't build into existing level geometry around the arena.
            foreach (var c in UnityEngine.Physics.OverlapBox(w + Vector3.up * 1.6f, new Vector3(0.7f, 1.4f, 0.7f), _arena.transform.rotation * localRot,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || c.transform.IsChildOf(_arena.transform) || CombatInputGate.IsPlayerCollider(c) || c.attachedRigidbody != null) continue;
                Debug.Log("ArenaDecor: skipped " + name + " (blocked by " + c.name + ")");
                return null;
            }
            return Group(name, localPos, localRot);
        }

        /// <summary>True if a tracked XR hand is within r of p (desktop: always false).</summary>
        public static bool HandNear(Vector3 p, float r)
        {
            if (!CombatInputGate.IsXr) return false;
            float r2 = r * r;
            if (XrTrackingSpace.TryGetWorldPose(UnityEngine.XR.XRNode.LeftHand, out Vector3 l, out _) && (l - p).sqrMagnitude < r2) return true;
            if (XrTrackingSpace.TryGetWorldPose(UnityEngine.XR.XRNode.RightHand, out Vector3 rr, out _) && (rr - p).sqrMagnitude < r2) return true;
            return false;
        }

        void Create()
        {
            _root = new GameObject("ArenaDecor").transform;
            _root.SetParent(_arena.transform, false);
            _props = new GameObject("ArenaProps").transform;
            _props.SetParent(_arena.transform, false);

            _stone = CombatMaterials.Get(new Color(0.16f, 0.17f, 0.2f), 0.1f, 0.45f);
            _stoneLight = CombatMaterials.Get(new Color(0.26f, 0.27f, 0.3f), 0.05f, 0.35f);
            _rim = CombatMaterials.Get(new Color(0.02f, 0.2f, 0.25f), 0f, 0.6f, new Color(0f, 0.9f, 1f) * 0.9f);
            _rimHot = CombatMaterials.Get(new Color(0.1f, 0.5f, 0.6f), 0f, 0.8f, new Color(0.2f, 0.95f, 1f) * 2.2f);
            _metal = CombatMaterials.Get(new Color(0.09f, 0.1f, 0.12f), 0.75f, 0.55f);
            _cloth = CombatMaterials.Get(new Color(0.06f, 0.08f, 0.11f), 0f, 0.15f);

            BuildArch();
            BuildRack("WeaponRackA", -95f, new[] { WeaponKind.Axe, WeaponKind.Saber, WeaponKind.Sword, WeaponKind.Staff }, WeaponKind.Shield);
            BuildRack("WeaponRackB", -118f, new[] { WeaponKind.Dagger, WeaponKind.Mace, WeaponKind.Spear, WeaponKind.Axe }, WeaponKind.TowerShield);
            BuildBrazier(new Vector3(-1.9f, 0f, -6.1f));
            BuildBrazier(new Vector3(1.9f, 0f, -6.1f));
            float[] torchAngles = { 27f, -27f, 81f, -81f, 153f, -153f };
            foreach (float a in torchAngles) BuildTorch(a);
            for (int i = 0; i < 4; i++) BuildBanner(i * 90f + 45f, i);
            BuildColumn(22f, 9.0f, false);
            BuildColumn(-22f, 9.0f, false);
            BuildColumn(105f, 8.8f, false);
            BuildColumn(-105f, 8.8f, true);
            BuildObelisk();
            BuildRuneRing();
            BuildHeavyBag(84f, 5.6f);

            // Breakable clusters near the walls (outside the spawn arc and the walkways).
            AddCluster(64f, 6.25f, false);
            AddCluster(122f, 6.25f, true);
            foreach (var s in _breakables) SpawnBreakable(s);

            ArenaWaves.WaveStarted += OnWaveStarted;
        }

        void OnDestroy()
        {
            ArenaWaves.WaveStarted -= OnWaveStarted;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ structures

        void BuildArch()
        {
            var g = OuterGroup("EntranceArch", new Vector3(0f, 0f, -7.6f), Quaternion.identity);
            if (g == null) return;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = 1.65f * s;
                P(PrimitiveType.Cube, "ArchPillar", g, new Vector3(x, 1.75f, 0f), new Vector3(0.5f, 3.5f, 0.55f), _stone);
                P(PrimitiveType.Cube, "ArchPlinth", g, new Vector3(x, 0.12f, 0f), new Vector3(0.7f, 0.24f, 0.75f), _stoneLight);
                P(PrimitiveType.Cube, "ArchGlow", g, new Vector3(x - 0.26f * s, 1.7f, 0f), new Vector3(0.03f, 2.6f, 0.08f), _rim, false, false);
            }
            P(PrimitiveType.Cube, "Lintel", g, new Vector3(0f, 3.65f, 0f), new Vector3(4.1f, 0.42f, 0.65f), _stoneLight);
            P(PrimitiveType.Cube, "LintelGlow", g, new Vector3(0f, 3.43f, 0f), new Vector3(3.2f, 0.03f, 0.12f), _rim, false, false);
            var key = P(PrimitiveType.Cube, "Keystone", g, new Vector3(0f, 3.95f, 0f), new Vector3(0.36f, 0.36f, 0.2f), _rimHot, false, false);
            key.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        void BuildRack(string name, float deg, WeaponKind[] standing, WeaponKind hung)
        {
            var g = Group(name, Polar(deg, 6.35f) + Vector3.up * 0.06f, FaceCentre(deg));
            P(PrimitiveType.Cube, "Base", g, new Vector3(0f, 0.05f, 0f), new Vector3(1.9f, 0.1f, 0.42f), _stone);
            P(PrimitiveType.Cube, "PostL", g, new Vector3(-0.92f, 0.8f, -0.12f), new Vector3(0.09f, 1.5f, 0.09f), _metal);
            P(PrimitiveType.Cube, "PostR", g, new Vector3(0.92f, 0.8f, -0.12f), new Vector3(0.09f, 1.5f, 0.09f), _metal);
            P(PrimitiveType.Cube, "BarTop", g, new Vector3(0f, 1.5f, -0.12f), new Vector3(1.92f, 0.07f, 0.07f), _metal, false);
            P(PrimitiveType.Cube, "BarLow", g, new Vector3(0f, 0.55f, -0.12f), new Vector3(1.92f, 0.06f, 0.06f), _metal, false);
            P(PrimitiveType.Cube, "TopGlow", g, new Vector3(0f, 1.55f, -0.12f), new Vector3(1.8f, 0.02f, 0.04f), _rim, false, false);
            P(PrimitiveType.Cube, "Back", g, new Vector3(0f, 0.8f, -0.2f), new Vector3(1.8f, 1.4f, 0.04f), _stoneLight);

            // Standing weapons along the left 3/4, shield hung on the right.
            for (int i = 0; i < standing.Length; i++)
            {
                var k = standing[i];
                float x = -0.75f + i * 0.3f;
                bool longPole = k == WeaponKind.Spear || k == WeaponKind.Staff;
                float baseY = longPole ? 0.11f : 0.14f; // butt / pommel resting on the base plank
                Vector3 lp = new Vector3(x, baseY, 0.02f);
                Quaternion lr = Quaternion.Euler(longPole ? -3f : -6f, 0f, 0f);
                _rack.Add(new RackSlot { kind = k, pos = g.TransformPoint(lp), rot = g.rotation * lr });
            }
            Vector3 sp = new Vector3(0.55f, 0.85f, 0.06f);
            Quaternion sr = Quaternion.Euler(90f, 0f, 0f); // shield face (local +Y) toward the centre, long side vertical
            _rack.Add(new RackSlot { kind = hung, pos = g.TransformPoint(sp), rot = g.rotation * sr });
        }

        void BuildBrazier(Vector3 lp)
        {
            var g = Group("Brazier", lp + Vector3.up * 0.06f, Quaternion.identity);
            var b = P(PrimitiveType.Cylinder, "Foot", g, new Vector3(0f, 0.05f, 0f), new Vector3(0.55f, 0.05f, 0.55f), _stone);
            CombatUtil.FlatCylinderCollider(b);
            P(PrimitiveType.Cylinder, "Stem", g, new Vector3(0f, 0.5f, 0f), new Vector3(0.16f, 0.42f, 0.16f), _metal);
            var bowl = P(PrimitiveType.Cylinder, "Bowl", g, new Vector3(0f, 0.98f, 0f), new Vector3(0.62f, 0.07f, 0.62f), _metal);
            CombatUtil.FlatCylinderCollider(bowl);
            P(PrimitiveType.Cylinder, "BowlRim", g, new Vector3(0f, 1.055f, 0f), new Vector3(0.66f, 0.012f, 0.66f), _rim, false, false);
            Material coals = CombatMaterials.Get(new Color(0.4f, 0.12f, 0.03f), 0f, 0.3f, new Color(1f, 0.38f, 0.08f) * 1.8f);
            P(PrimitiveType.Cylinder, "Coals", g, new Vector3(0f, 1.06f, 0f), new Vector3(0.5f, 0.01f, 0.5f), coals, false, false);

            var ps = ImbueVfx.MakeFlames(g, new Vector3(0f, 1.12f, 0f), 0.16f, new Color(1f, 0.45f, 0.1f));
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            var em = ps.emission;
            em.rateOverTime = 28f;

            var l = new GameObject("BrazierLight").AddComponent<Light>();
            l.transform.SetParent(g, false);
            l.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.58f, 0.28f);
            l.range = 4.2f;
            l.intensity = 1.4f;
            l.shadows = LightShadows.None;
            _braziers.Add((l, g.TransformPoint(new Vector3(0f, 1.22f, 0f))));
        }

        void BuildTorch(float deg)
        {
            var g = Group("CrystalTorch", Polar(deg, 7f), FaceCentre(deg));
            P(PrimitiveType.Cube, "Socket", g, new Vector3(0f, 0.96f, 0f), new Vector3(0.22f, 0.12f, 0.22f), _metal, false);
            P(PrimitiveType.Cube, "SocketGlow", g, new Vector3(0f, 1.025f, 0f), new Vector3(0.16f, 0.012f, 0.16f), _rim, false, false);
            var c = P(PrimitiveType.Cube, "Crystal", g, new Vector3(0f, 1.24f, 0f), new Vector3(0.09f, 0.28f, 0.09f), _rimHot, false, false);
            _crystals.Add((c.transform, c.transform.localPosition, deg * 0.13f));
        }

        void BuildBanner(float deg, int index)
        {
            // Hangs on the inner face of the existing corner pillar (radius 7.4, 0.45 wide, 2.6 tall).
            var pivot = Group("Banner" + index, Polar(deg, 7.4f - 0.26f) + Vector3.up * 2.45f, FaceCentre(deg));
            P(PrimitiveType.Cube, "Rod", pivot, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.03f, 0.03f), _metal, false, false);
            var sway = new GameObject("Sway").transform;
            sway.SetParent(pivot, false);
            P(PrimitiveType.Cube, "Cloth", sway, new Vector3(0f, -0.66f, 0f), new Vector3(0.42f, 1.3f, 0.015f), _cloth, false);
            P(PrimitiveType.Cube, "Stripe", sway, new Vector3(0f, -0.62f, 0.01f), new Vector3(0.06f, 1.18f, 0.01f), _rim, false, false);
            var emblem = P(PrimitiveType.Cube, "Emblem", sway, new Vector3(0f, -0.42f, 0.012f), new Vector3(0.17f, 0.17f, 0.01f), _rim, false, false);
            emblem.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            P(PrimitiveType.Cube, "Hem", sway, new Vector3(0f, -1.3f, 0.005f), new Vector3(0.42f, 0.05f, 0.02f), _rim, false, false);
            _banners.Add((sway, Quaternion.identity, index * 1.7f));
        }

        void BuildColumn(float deg, float r, bool broken)
        {
            var g = OuterGroup(broken ? "BrokenColumn" : "Column", Polar(deg, r), FaceCentre(deg));
            if (g == null) return;
            P(PrimitiveType.Cube, "Base", g, new Vector3(0f, 0.12f, 0f), new Vector3(0.8f, 0.24f, 0.8f), _stoneLight);
            float h = broken ? 1.3f : 3.2f;
            P(PrimitiveType.Cylinder, "Shaft", g, new Vector3(0f, 0.24f + h * 0.5f, 0f), new Vector3(0.52f, h * 0.5f, 0.52f), _stone);
            P(PrimitiveType.Cylinder, "Band", g, new Vector3(0f, 0.24f + h * 0.72f, 0f), new Vector3(0.545f, 0.02f, 0.545f), _rim, false, false);
            if (!broken)
            {
                P(PrimitiveType.Cube, "Capital", g, new Vector3(0f, 0.24f + h + 0.1f, 0f), new Vector3(0.8f, 0.2f, 0.8f), _stoneLight);
            }
            else
            {
                var top = P(PrimitiveType.Cube, "Shard", g, new Vector3(0.08f, 0.24f + h + 0.08f, 0f), new Vector3(0.3f, 0.22f, 0.4f), _stone);
                top.transform.localRotation = Quaternion.Euler(12f, 20f, 25f);
                var drum = P(PrimitiveType.Cylinder, "FallenDrum", g, new Vector3(0.9f, 0.26f, 0.5f), new Vector3(0.5f, 0.45f, 0.5f), _stone);
                drum.transform.localRotation = Quaternion.Euler(0f, 35f, 90f);
            }
        }

        void BuildObelisk()
        {
            var g = OuterGroup("Obelisk", Polar(0f, 9.4f), FaceCentre(0f));
            if (g == null) return;
            P(PrimitiveType.Cube, "Plinth", g, new Vector3(0f, 0.2f, 0f), new Vector3(1.3f, 0.4f, 1.3f), _stoneLight);
            P(PrimitiveType.Cube, "Shaft", g, new Vector3(0f, 2.5f, 0f), new Vector3(0.72f, 4.2f, 0.72f), _stone);
            P(PrimitiveType.Cube, "Rune", g, new Vector3(0f, 2.55f, 0.37f), new Vector3(0.08f, 3.3f, 0.02f), _rim, false, false);
            for (int i = 0; i < 4; i++)
                P(PrimitiveType.Cube, "RuneTick", g, new Vector3(0f, 1.3f + i * 0.85f, 0.37f), new Vector3(0.3f, 0.04f, 0.02f), _rim, false, false);
            var cap = P(PrimitiveType.Cube, "Cap", g, new Vector3(0f, 4.85f, 0f), new Vector3(0.5f, 0.5f, 0.5f), _rimHot, false, false);
            cap.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            _crystals.Add((cap.transform, cap.transform.localPosition, 0.5f));
        }

        void BuildRuneRing()
        {
            var g = Group("RuneRing", Vector3.zero, Quaternion.identity);
            const float y = 0.064f;
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                if (i % 2 == 1) continue;
                var d = P(PrimitiveType.Cube, "Dash", g, Polar(a, 3.0f) + Vector3.up * y, new Vector3(0.62f, 0.004f, 0.05f), _rim, false, false);
                d.transform.localRotation = Quaternion.Euler(0f, a, 0f); // long side tangent to the ring
                var tick = P(PrimitiveType.Cube, "Tick", g, Polar(a + 15f, 3.0f) + Vector3.up * y, new Vector3(0.12f, 0.004f, 0.12f), _rim, false, false);
                tick.transform.localRotation = Quaternion.Euler(0f, a + 15f + 45f, 0f);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f;
                var d = P(PrimitiveType.Cube, "Spoke", g, Polar(a, 1.9f) + Vector3.up * y, new Vector3(0.04f, 0.004f, 1.2f), _rim, false, false);
                d.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
            P(PrimitiveType.Cylinder, "Core", g, Vector3.up * y, new Vector3(0.7f, 0.002f, 0.7f), _rim, false, false);
        }

        void BuildHeavyBag(float deg, float r)
        {
            var g = Group("HeavyBag", Polar(deg, r) + Vector3.up * 0.06f, FaceCentre(deg));
            P(PrimitiveType.Cube, "Foot", g, new Vector3(0f, 0.04f, -0.55f), new Vector3(0.6f, 0.08f, 0.6f), _stone);
            P(PrimitiveType.Cube, "Post", g, new Vector3(0f, 1.15f, -0.55f), new Vector3(0.14f, 2.2f, 0.14f), _metal);
            P(PrimitiveType.Cube, "Arm", g, new Vector3(0f, 2.2f, -0.15f), new Vector3(0.1f, 0.1f, 0.9f), _metal);
            P(PrimitiveType.Cube, "ArmGlow", g, new Vector3(0f, 2.26f, -0.15f), new Vector3(0.04f, 0.02f, 0.8f), _rim, false, false);
            HeavyBag.Spawn(g, g.TransformPoint(new Vector3(0f, 2.15f, 0.22f)), _cloth, _rim, _metal);
        }

        // ------------------------------------------------------------------ breakables

        void AddCluster(float deg, float r, bool second)
        {
            Vector3 c = Polar(deg, r);
            Quaternion face = FaceCentre(deg);
            Vector3 right = face * Vector3.right, fwd = face * Vector3.forward;
            const float floor = 0.06f;
            void Add(ArenaBreakable.Kind k, Vector3 lp, float yaw)
            {
                _breakables.Add(new BreakSlot
                {
                    kind = k,
                    pos = _arena.transform.TransformPoint(lp),
                    rot = _arena.transform.rotation * face * Quaternion.Euler(0f, yaw, 0f)
                });
            }
            Add(ArenaBreakable.Kind.Crate, c - right * 0.45f + Vector3.up * (floor + 0.31f), 8f);
            Add(ArenaBreakable.Kind.Crate, c + right * 0.25f + fwd * 0.05f + Vector3.up * (floor + 0.31f), -12f);
            Add(ArenaBreakable.Kind.Crate, c - right * 0.15f + Vector3.up * (floor + 0.93f), 30f);
            Add(ArenaBreakable.Kind.Barrel, c + right * 1.05f + fwd * 0.15f + Vector3.up * (floor + 0.43f), 0f);
            Add(ArenaBreakable.Kind.Volatile, c - right * 1.2f + fwd * 0.35f + Vector3.up * (floor + 0.43f), 0f);
            if (second)
                Add(ArenaBreakable.Kind.Barrel, c + right * 0.6f + fwd * 0.75f + Vector3.up * (floor + 0.43f), 0f);
        }

        void SpawnBreakable(BreakSlot s)
        {
            s.obj = ArenaBreakable.Spawn(s.kind, s.pos, s.rot, _props);
            s.goneAt = -1f;
        }

        bool SlotClear(Vector3 p)
        {
            var cam = CombatInputGate.ViewCamera();
            if (cam != null)
            {
                Vector3 d = cam.transform.position - p;
                if (new Vector2(d.x, d.z).magnitude < 0.9f) return false;
            }
            foreach (var e in CombatEnemy.All)
                if (e != null && !e.IsDead && Vector3.Distance(e.transform.position, p) < 1f) return false;
            return true;
        }

        void OnWaveStarted(int wave)
        {
            foreach (var s in _breakables)
                if (s.obj == null && SlotClear(s.pos))
                {
                    SpawnBreakable(s);
                    SpawnFx.Spawn(s.pos - Vector3.up * 0.4f, 0.5f);
                }
        }

        void TickBreakables()
        {
            foreach (var s in _breakables)
            {
                if (s.obj != null)
                {
                    Vector3 p = s.obj.transform.position;
                    if ((p - s.pos).sqrMagnitude > 12f * 12f || p.y < s.pos.y - 6f)
                    {
                        Destroy(s.obj.gameObject);
                        s.obj = null;
                    }
                    else continue;
                }
                if (s.goneAt < 0f) { s.goneAt = Time.time; continue; }
                if (Time.time - s.goneAt > 40f && SlotClear(s.pos))
                {
                    SpawnBreakable(s);
                    SpawnFx.Spawn(s.pos - Vector3.up * 0.4f, 0.5f);
                }
            }
        }

        // ------------------------------------------------------------------ racks

        void TickRacks()
        {
            foreach (var s in _rack)
            {
                var w = s.w;
                if (w == null)
                {
                    if (s.leftAt < 0f) s.leftAt = Time.time - 30f; // first fill / destroyed: restock now
                    if (Time.time - s.leftAt > 4f) Restock(s);
                    continue;
                }
                if (w.GetComponent<RackHold>() != null) { s.leftAt = -1f; continue; }
                float off = Vector3.Distance(w.transform.position, s.pos);
                bool resting = !w.IsHeld && !w.IsStuck && w.Body != null && w.Body.linearVelocity.sqrMagnitude < 0.04f;
                // Knocked loose but still at its spot: hang it back up.
                if (off < 0.35f && resting && w.enemyOwner == null)
                {
                    w.transform.SetPositionAndRotation(s.pos, s.rot);
                    w.Body.position = s.pos;
                    w.Body.rotation = s.rot;
                    w.Body.linearVelocity = Vector3.zero;
                    w.Body.angularVelocity = Vector3.zero;
                    RackHold.Hang(w);
                    s.leftAt = -1f;
                    continue;
                }
                if (s.leftAt < 0f) s.leftAt = Time.time;
                // Taken 25 s ago: put a fresh one up; the old one becomes loot the janitor can tidy once idle.
                if (Time.time - s.leftAt > 25f)
                {
                    if (w.GetComponent<LootTag>() == null) w.gameObject.AddComponent<LootTag>();
                    s.w = null;
                    Restock(s);
                }
            }
        }

        void Restock(RackSlot s)
        {
            var w = WeaponFactory.Spawn(s.kind, s.pos, s.rot);
            if (w == null) return;
            w.gameObject.name = "Weapon_" + s.kind + "_Rack";
            RackHold.Hang(w);
            s.w = w;
            s.leftAt = -1f;
        }

        // ------------------------------------------------------------------ braziers

        void TickBraziers()
        {
            foreach (var (l, flame) in _braziers)
            {
                foreach (var w in PhysicsWeapon.All)
                {
                    if (w == null || w.IsShield || w.enemyOwner != null || w.Imbue == Element.Fire) continue;
                    Vector3 tip = w.TipWorld;
                    Vector3 mid = w.transform.TransformPoint(0f, w.tipY * 0.55f, 0f);
                    if (DistToSegment(flame, mid, tip) > 0.28f) continue;
                    w.ApplyImbue(Element.Fire, 20f * SkillSystem.ImbueDurationMult);
                    QhysicsSfx.PlayAt(SfxId.Ignite, tip, 0.8f);
                    SparkBurst.Spawn(tip, new Color(1f, 0.55f, 0.15f), 14);
                    if (w.HeldByPlayer)
                    {
                        CombatHaptics.Pulse(w.PrimaryNode, 0.5f, 0.1f);
                        CombatHud.Toast("Blade ignited (fire)");
                    }
                }
            }
        }

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            float t = Time.time;
            foreach (var (tr, baseRot, phase) in _banners)
            {
                if (tr == null) continue;
                // Biased so the cloth only swings out from the pillar face, never into it.
                float sx = -4.5f + Mathf.Sin(t * 0.9f + phase) * 3f + (Mathf.PerlinNoise(t * 0.35f, phase) - 0.5f) * 3f;
                float sz = Mathf.Sin(t * 0.6f + phase * 1.3f) * 2f;
                tr.localRotation = baseRot * Quaternion.Euler(sx, 0f, sz);
            }
            foreach (var (tr, basePos, phase) in _crystals)
            {
                if (tr == null) continue;
                tr.localPosition = basePos + Vector3.up * (Mathf.Sin(t * 1.3f + phase) * 0.025f);
                tr.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);
            }
            foreach (var (l, flame) in _braziers)
                if (l != null) l.intensity = 1.25f + (Mathf.PerlinNoise(t * 6f, flame.x * 0.37f) - 0.5f) * 0.6f;

            if (t >= _nextFire) { _nextFire = t + 0.1f; TickBraziers(); }
            if (t >= _nextRack) { _nextRack = t + 1.5f; TickRacks(); }
            if (t >= _nextBreak) { _nextBreak = t + 2f; TickBreakables(); }
        }
    }

    /// <summary>
    /// Physical breakable prop (crate, barrel, volatile barrel): Neutral CombatHealth so player weapons, spells and hard
    /// impacts (&gt; 7 m/s) break it into fading debris. Crates can drop heal / mana orbs; volatile barrels explode
    /// (fire damage + knockback to enemies and props within 3.2 m, and to you within 2.5 m) and chain.
    /// </summary>
    public sealed class ArenaBreakable : MonoBehaviour
    {
        public enum Kind { Crate, Barrel, Volatile }

        public Kind kind;
        CombatHealth _h;
        Rigidbody _rb;
        float _born;
        float _fuseAt = -1f;
        bool _broken;
        Material _main, _trim;

        public static ArenaBreakable Spawn(Kind k, Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = k == Kind.Crate
                ? CombatUtil.Prim(PrimitiveType.Cube, "Crate", parent, Vector3.zero, Vector3.one * 0.6f, Crate())
                : CombatUtil.Prim(PrimitiveType.Cylinder, k == Kind.Volatile ? "VolatileBarrel" : "Barrel", parent, Vector3.zero, new Vector3(0.5f, 0.42f, 0.5f), k == Kind.Volatile ? VolatileMat() : BarrelMat());
            go.transform.SetPositionAndRotation(pos, rot);
            if (k != Kind.Crate)
            {
                CombatUtil.FlatCylinderCollider(go);
                var mc = go.GetComponent<MeshCollider>();
                if (mc != null) mc.convex = true;
            }
            var b = go.AddComponent<ArenaBreakable>();
            b.kind = k;
            b._born = Time.time;
            Material trim = k == Kind.Volatile
                ? CombatMaterials.Get(new Color(0.5f, 0.15f, 0.02f), 0f, 0.5f, new Color(1f, 0.4f, 0.05f) * 1.6f)
                : CombatMaterials.Get(new Color(0.02f, 0.2f, 0.25f), 0f, 0.6f, new Color(0f, 0.9f, 1f) * 0.9f);
            Material band = CombatMaterials.Get(new Color(0.09f, 0.1f, 0.12f), 0.75f, 0.55f);
            b._main = go.GetComponent<Renderer>().sharedMaterial;
            b._trim = trim;
            var off = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (k == Kind.Crate)
            {
                Deco(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.36f, 0f), new Vector3(1.04f, 0.1f, 1.04f), band, off);
                Deco(go.transform, PrimitiveType.Cube, new Vector3(0f, -0.36f, 0f), new Vector3(1.04f, 0.1f, 1.04f), band, off);
                Deco(go.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.505f), new Vector3(0.5f, 0.06f, 0.01f), trim, off);
                Deco(go.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.505f), new Vector3(0.5f, 0.06f, 0.01f), trim, off);
            }
            else
            {
                Deco(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), new Vector3(1.04f, 0.05f, 1.04f), k == Kind.Volatile ? trim : band, off);
                Deco(go.transform, PrimitiveType.Cylinder, new Vector3(0f, -0.55f, 0f), new Vector3(1.04f, 0.05f, 1.04f), k == Kind.Volatile ? trim : band, off);
                Deco(go.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.0f, 0f), new Vector3(0.5f, 0.01f, 0.5f), trim, off);
            }
            b._rb = go.AddComponent<Rigidbody>();
            b._rb.mass = k == Kind.Crate ? 10f : k == Kind.Barrel ? 16f : 14f;
            b._rb.angularDamping = 0.3f;
            b._rb.interpolation = RigidbodyInterpolation.Interpolate;
            b._h = go.AddComponent<CombatHealth>(); // after the Rigidbody so hits push it
            b._h.team = CombatTeam.Neutral;
            b._h.showNumbers = false;
            b._h.ResetHp(k == Kind.Crate ? 25f : k == Kind.Barrel ? 35f : 20f);
            b._h.Died += b.OnDied;
            b._h.Damaged += b.OnDamaged;
            return b;
        }

        static void Deco(Transform parent, PrimitiveType t, Vector3 lp, Vector3 ls, Material m, UnityEngine.Rendering.ShadowCastingMode shadows)
        {
            var g = CombatUtil.Prim(t, "Deco", parent, lp, ls, m, false);
            g.GetComponent<Renderer>().shadowCastingMode = shadows;
        }

        static Material Crate() => CombatMaterials.Get(new Color(0.24f, 0.19f, 0.14f), 0.05f, 0.3f);
        static Material BarrelMat() => CombatMaterials.Get(new Color(0.2f, 0.22f, 0.25f), 0.55f, 0.45f);
        static Material VolatileMat() => CombatMaterials.Get(new Color(0.32f, 0.12f, 0.06f), 0.4f, 0.4f);

        void OnDamaged(DamageInfo hit)
        {
            if (_broken) return;
            if (kind != Kind.Volatile)
                QhysicsSfx.PlayAt(SfxId.HitBlunt, hit.point, 0.35f, kind == Kind.Crate ? 0.8f : 1.15f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_broken || _h == null || !_h.IsAlive || Time.time - _born < 1.2f) return;
            var w = c.collider != null ? c.collider.GetComponentInParent<PhysicsWeapon>() : null;
            if (w != null && w.ActiveTeam == CombatTeam.Player) return; // the weapon applies its own damage
            float speed = c.relativeVelocity.magnitude;
            if (speed < 7f) return;
            Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            _h.TakeHit(new DamageInfo
            {
                amount = (speed - 7f) * 6f + 5f,
                type = DamageType.Blunt,
                point = p,
                normal = c.contactCount > 0 ? c.GetContact(0).normal : Vector3.up,
                sourceTeam = CombatTeam.Neutral,
                relativeSpeed = speed
            });
        }

        void OnDied(DamageInfo hit)
        {
            if (_broken) return;
            if (kind == Kind.Volatile)
            {
                // Short fuse so chain reactions ripple instead of all popping in one frame.
                if (_fuseAt < 0f) _fuseAt = Time.time + Random.Range(0.08f, 0.2f);
                return;
            }
            Break(hit);
        }

        void Update()
        {
            if (_fuseAt > 0f && !_broken && Time.time >= _fuseAt)
                Break(new DamageInfo { point = transform.position });
        }

        void Break(DamageInfo hit)
        {
            if (_broken) return;
            _broken = true;
            Vector3 c = transform.position;
            Vector3 v = _rb != null ? _rb.linearVelocity : Vector3.zero;
            QhysicsSfx.PlayAt(SfxId.WoodBreak, c, 0.85f, kind == Kind.Crate ? 1f : 0.8f);
            int n = kind == Kind.Crate ? 6 : 5;
            Vector3 push = hit.impulse.sqrMagnitude > 0f ? hit.impulse.normalized * 1.5f : Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                Vector3 size = kind == Kind.Crate ? new Vector3(0.55f, 0.06f, 0.14f) : new Vector3(0.12f, 0.75f, 0.04f);
                Debris.Spawn(c + dir * 0.18f, Random.rotation, size, i % 3 == 0 ? _trim : _main, v + push + dir * Random.Range(1.5f, 3.5f));
            }
            if (kind == Kind.Volatile)
                Explode(c);
            else
                MaybeDrop(c);
            Destroy(gameObject);
        }

        void MaybeDrop(Vector3 c)
        {
            float r = Random.value;
            float heal = kind == Kind.Crate ? 0.35f : 0.2f;
            float mana = kind == Kind.Crate ? 0.6f : 0.2f;
            if (r < heal) ArenaPickup.Spawn(c, ArenaPickup.Kind.Heal);
            else if (r < mana) ArenaPickup.Spawn(c, ArenaPickup.Kind.Mana);
        }

        static readonly HashSet<ICombatTarget> _done = new HashSet<ICombatTarget>();
        static readonly HashSet<Rigidbody> _pushed = new HashSet<Rigidbody>();

        /// <summary>Fire explosion: 3.2 m radius damage + knockback, 2.5 m player damage.</summary>
        public static void Explode(Vector3 p)
        {
            const float radius = 3.2f;
            QhysicsSfx.PlayAt(SfxId.Explosion, p, 1f);
            SparkBurst.Spawn(p, new Color(1f, 0.5f, 0.12f), 40);
            ExplosionFx.Spawn(p, radius);
            CombatHaptics.Both(0.6f, 0.2f);
            _done.Clear();
            _pushed.Clear();
            foreach (var col in UnityEngine.Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col == null) continue;
                float d = Vector3.Distance(p, col.ClosestPoint(p));
                float fall = Mathf.Clamp01(1f - d / radius);
                var rb = col.attachedRigidbody;
                ICombatTarget t = col.GetComponentInParent<ICombatTarget>();
                if (t is BodyPart bp) t = bp.health;
                if (t != null && t.IsAlive && t.Team != CombatTeam.Player && _done.Add(t))
                {
                    Vector3 dir = (col.bounds.center - p); dir.y = Mathf.Max(dir.y, 0.3f); dir.Normalize();
                    t.TakeHit(new DamageInfo
                    {
                        amount = Mathf.Max(6f, 45f * fall),
                        type = DamageType.Fire,
                        point = col.bounds.center,
                        normal = -dir,
                        impulse = dir * 10f * fall,
                        sourceTeam = CombatTeam.Player
                    });
                    if (t is CombatHealth h)
                    {
                        var e = h.GetComponent<CombatEnemy>();
                        if (e != null && !e.IsDead)
                        {
                            SpellSystem.ApplyBurn(h, 3f);
                            if (d < 2.5f) e.OnHeavyImpact(1.2f);
                        }
                    }
                }
                if (rb != null && !rb.isKinematic && _pushed.Add(rb))
                    rb.AddExplosionForce(650f * Mathf.Clamp(rb.mass / 10f, 0.15f, 1.5f), p, radius, 0.6f);
            }
            var hb = PlayerHurtbox.Instance;
            if (hb != null)
            {
                float pd = Vector3.Distance(p, hb.Collider != null ? hb.Collider.ClosestPoint(p) : hb.transform.position);
                if (pd < 2.5f)
                {
                    hb.Hurt(4f + 24f * (1f - pd / 2.5f), p, "Explosion");
                    CombatHaptics.Both(1f, 0.3f);
                }
            }
        }
    }

    /// <summary>Loose plank / stave from a broken prop: falls, then shrinks away after 4-5 s.</summary>
    public sealed class Debris : MonoBehaviour
    {
        float _born;
        Vector3 _scale;

        public static void Spawn(Vector3 pos, Quaternion rot, Vector3 size, Material m, Vector3 vel)
        {
            var g = CombatUtil.Prim(PrimitiveType.Cube, "Debris", null, pos, size, m);
            g.transform.rotation = rot;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearVelocity = vel;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            var d = g.AddComponent<Debris>();
            d._born = Time.time + Random.Range(0f, 1f);
            d._scale = size;
        }

        void Update()
        {
            float a = Time.time - _born - 4f;
            if (a <= 0f) return;
            if (a >= 1f) { Destroy(gameObject); return; }
            transform.localScale = _scale * (1f - a);
        }
    }

    /// <summary>Expanding fire ball + short flash light for explosions.</summary>
    public sealed class ExplosionFx : MonoBehaviour
    {
        float _born, _radius;
        Light _light;

        public static void Spawn(Vector3 p, float radius)
        {
            var g = CombatUtil.Prim(PrimitiveType.Sphere, "ExplosionFx", null, p, Vector3.one * 0.3f,
                CombatMaterials.Transparent(new Color(1f, 0.5f, 0.15f, 0.45f), new Color(1f, 0.45f, 0.1f) * 2f), false);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fx = g.AddComponent<ExplosionFx>();
            fx._born = Time.time;
            fx._radius = radius;
            fx._light = g.AddComponent<Light>();
            fx._light.type = LightType.Point;
            fx._light.color = new Color(1f, 0.55f, 0.2f);
            fx._light.range = radius * 2.2f;
            fx._light.intensity = 4f;
            fx._light.shadows = LightShadows.None;
        }

        void Update()
        {
            float a = (Time.time - _born) / 0.35f;
            if (a >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, _radius * 1.5f, Mathf.Sqrt(a));
            if (_light != null) _light.intensity = 4f * (1f - a);
        }
    }

    /// <summary>Floating heal / mana orb dropped by crates: walk over it, touch it, or poke it with a weapon.</summary>
    public sealed class ArenaPickup : MonoBehaviour
    {
        public enum Kind { Heal, Mana }

        Kind _kind;
        float _born;
        Vector3 _base;
        Renderer _r;

        public static void Spawn(Vector3 at, Kind k)
        {
            Color c = k == Kind.Heal ? new Color(0.3f, 1f, 0.45f) : new Color(0.35f, 0.55f, 1f);
            var g = CombatUtil.Prim(PrimitiveType.Sphere, k == Kind.Heal ? "HealOrb" : "ManaOrb", null, Vector3.zero, Vector3.one * 0.16f,
                CombatMaterials.Get(c * 0.3f, 0f, 0.8f, c * 2f), false);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 p = at;
            if (CombatArena.GroundAt(at, out Vector3 ground) && at.y - ground.y < 2f) p = ground + Vector3.up * 0.45f;
            g.transform.position = p;
            var pk = g.AddComponent<ArenaPickup>();
            pk._kind = k;
            pk._born = Time.time;
            pk._base = p;
            pk._r = g.GetComponent<Renderer>();
            QhysicsSfx.PlayAt(SfxId.UiTick, p, 0.4f, 1.4f);
        }

        void Update()
        {
            float age = Time.time - _born;
            if (age > 40f) { Destroy(gameObject); return; }
            transform.position = _base + Vector3.up * (Mathf.Sin(age * 2.2f) * 0.06f);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
            if (_r != null) _r.enabled = age < 35f || Mathf.Repeat(age * 6f, 1f) > 0.35f;
            if (age < 0.6f) return;
            Vector3 p = transform.position;
            bool got = false;
            var cam = CombatInputGate.ViewCamera();
            if (cam != null)
            {
                Vector3 d = cam.transform.position - p;
                got = new Vector2(d.x, d.z).magnitude < 0.55f && d.y > -0.3f && d.y < 2.1f;
            }
            if (!got) got = ArenaDecor.HandNear(p, 0.3f);
            if (!got)
                foreach (var w in PhysicsWeapon.All)
                    if (w != null && w.HeldByPlayer && (w.TipWorld - p).sqrMagnitude < 0.3f * 0.3f) { got = true; break; }
            if (!got) return;
            if (_kind == Kind.Heal)
            {
                var v = PlayerVitality.Instance;
                if (v != null) v.Heal(20f);
                CombatHud.Toast("+20 HP");
            }
            else
            {
                ManaPool.Add(30f);
                CombatHud.Toast("+30 mana");
            }
            QhysicsSfx.PlayAt(SfxId.Pickup, p, 0.75f);
            SparkBurst.Spawn(p, _kind == Kind.Heal ? new Color(0.3f, 1f, 0.45f) : new Color(0.35f, 0.55f, 1f), 12);
            Destroy(gameObject);
        }
    }

    /// <summary>Hanging heavy bag: swings on a joint, huge HP that resets when idle, shows last hit and combo count.</summary>
    public sealed class HeavyBag : MonoBehaviour
    {
        CombatHealth _h;
        TextMeshPro _label;
        float _lastHit = -9f, _last;
        int _combo;

        public static HeavyBag Spawn(Transform frame, Vector3 hangPoint, Material cloth, Material rim, Material metal)
        {
            const float halfH = 0.55f, rad = 0.21f;
            Vector3 centre = hangPoint + Vector3.down * (0.45f + halfH);
            var bag = CombatUtil.Prim(PrimitiveType.Capsule, "Bag", frame, Vector3.zero, new Vector3(rad * 2f, halfH, rad * 2f), cloth);
            bag.transform.position = centre;
            Vector3 s = bag.transform.lossyScale;
            var band = CombatUtil.Prim(PrimitiveType.Cylinder, "Band", bag.transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.04f, 0.04f, 1.04f), rim, false);
            band.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var chain = CombatUtil.Prim(PrimitiveType.Cylinder, "Chain", bag.transform, new Vector3(0f, (halfH + 0.225f) / s.y, 0f),
                new Vector3(0.025f / s.x, 0.225f / s.y, 0.025f / s.z), metal, false);
            chain.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rb = bag.AddComponent<Rigidbody>();
            rb.mass = 30f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var j = bag.AddComponent<ConfigurableJoint>();
            j.autoConfigureConnectedAnchor = true;
            j.anchor = bag.transform.InverseTransformPoint(hangPoint);
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = ConfigurableJointMotion.Limited;
            j.angularZMotion = ConfigurableJointMotion.Limited;
            j.angularYMotion = ConfigurableJointMotion.Free;
            j.lowAngularXLimit = new SoftJointLimit { limit = -55f };
            j.highAngularXLimit = new SoftJointLimit { limit = 55f };
            j.angularZLimit = new SoftJointLimit { limit = 55f };

            var hb = bag.AddComponent<HeavyBag>();
            hb._h = bag.AddComponent<CombatHealth>();
            hb._h.team = CombatTeam.Neutral;
            hb._h.ResetHp(5000f);
            hb._h.resetsWhenIdle = true;
            hb._h.resetDelay = 4f;
            hb._h.showNumbers = true;
            hb._h.Damaged += hb.OnDamaged;

            var lg = new GameObject("BagLabel");
            lg.transform.SetParent(frame, false);
            lg.transform.position = hangPoint + Vector3.up * 0.45f;
            hb._label = lg.AddComponent<TextMeshPro>();
            hb._label.alignment = TextAlignmentOptions.Center;
            hb._label.fontSize = 1.4f;
            hb._label.color = new Color(0.75f, 0.95f, 1f, 0.9f);
            hb._label.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            hb._label.text = "HEAVY BAG";
            return hb;
        }

        void OnDamaged(DamageInfo hit)
        {
            _combo = Time.time - _lastHit < 1.5f ? _combo + 1 : 1;
            _lastHit = Time.time;
            _last = hit.amount;
        }

        void Update()
        {
            if (_label == null) return;
            _label.text = Time.time - _lastHit < 3f
                ? "HIT " + _last.ToString("0") + (_combo > 1 ? "  |  COMBO " + _combo : "")
                : "HEAVY BAG";
            var cam = CombatInputGate.ViewCamera();
            if (cam != null)
                _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - cam.transform.position);
        }
    }
}
