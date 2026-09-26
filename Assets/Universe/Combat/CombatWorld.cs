using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;
using RealityEngine.XR;
using TMPro;

namespace RealityEngine.Combat
{
    /// <summary>Wobbling training dummy on a spring joint; big HP pool that resets when idle; shows DPS.</summary>
    public sealed class TrainingDummy : MonoBehaviour
    {
        CombatHealth _health;
        TextMeshPro _label;
        readonly Queue<(float t, float dmg)> _recent = new Queue<(float, float)>();
        float _last;

        public static TrainingDummy Spawn(Vector3 ground, Quaternion facing)
        {
            var root = new GameObject("TrainingDummy");
            root.transform.SetPositionAndRotation(ground, facing);
            Material wood = CombatMaterials.Get(new Color(0.45f, 0.32f, 0.2f), 0f, 0.25f);
            Material sack = CombatMaterials.Get(new Color(0.62f, 0.55f, 0.4f), 0f, 0.2f);
            Material mark = CombatMaterials.Get(new Color(0.05f, 0.3f, 0.35f), 0f, 0.5f, new Color(0f, 0.9f, 1f) * 0.6f);
            CombatUtil.FlatCylinderCollider(CombatUtil.Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.03f, 0.6f), wood));
            CombatUtil.Prim(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.1f, 0.45f, 0.1f), wood);

            var body = new GameObject("DummyBody");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var rb = body.AddComponent<Rigidbody>();
            rb.mass = 25f;
            rb.angularDamping = 2f;
            var d = root.AddComponent<TrainingDummy>();
            var h = body.AddComponent<CombatHealth>();
            h.ResetHp(500f);
            h.team = CombatTeam.Enemy;
            h.resetsWhenIdle = true;
            h.resetDelay = 3f;
            d._health = h;
            h.Damaged += d.OnDamaged;

            Part(body.transform, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.4f, 0.32f), sack, h, 1f);
            Part(body.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.02f, 0f), Vector3.one * 0.26f, sack, h, 2f);
            Part(body.transform, PrimitiveType.Cylinder, "Arms", new Vector3(0f, 0.62f, 0f), new Vector3(0.08f, 0.38f, 0.08f), wood, h, 0.6f)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CombatUtil.Prim(PrimitiveType.Cylinder, "Target", body.transform, new Vector3(0f, 0.5f, 0.165f), new Vector3(0.18f, 0.005f, 0.18f), mark, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            h.RefreshRenderers();

            var j = body.AddComponent<ConfigurableJoint>();
            j.anchor = Vector3.zero;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = ConfigurableJointMotion.Limited;
            j.angularYMotion = ConfigurableJointMotion.Free;
            j.angularZMotion = ConfigurableJointMotion.Limited;
            j.lowAngularXLimit = new SoftJointLimit { limit = -35f };
            j.highAngularXLimit = new SoftJointLimit { limit = 35f };
            j.angularZLimit = new SoftJointLimit { limit = 35f };
            j.rotationDriveMode = RotationDriveMode.Slerp;
            j.slerpDrive = new JointDrive { positionSpring = 900f, positionDamper = 40f, maximumForce = 1e5f };

            var lg = new GameObject("DpsLabel");
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            d._label = lg.AddComponent<TextMeshPro>();
            d._label.alignment = TextAlignmentOptions.Center;
            d._label.fontSize = 1.6f;
            d._label.color = new Color(1f, 1f, 1f, 0.9f);
            d._label.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            d._label.text = "TRAINING DUMMY";
            return d;
        }

        static GameObject Part(Transform parent, PrimitiveType t, string name, Vector3 lp, Vector3 ls, Material m, CombatHealth h, float mult)
        {
            var g = CombatUtil.Prim(t, name, parent, lp, ls, m, true);
            var bp = g.AddComponent<BodyPart>();
            bp.health = h;
            bp.multiplier = mult;
            bp.zone = name;
            return g;
        }

        void OnDamaged(DamageInfo hit)
        {
            _recent.Enqueue((Time.time, hit.amount));
            _last = hit.amount;
        }

        void Update()
        {
            while (_recent.Count > 0 && Time.time - _recent.Peek().t > 3f)
                _recent.Dequeue();
            float sum = 0f;
            foreach (var r in _recent) sum += r.dmg;
            if (_label != null)
            {
                _label.text = _recent.Count > 0 ? "DPS " + (sum / 3f).ToString("0") + "  |  LAST " + _last.ToString("0") : "TRAINING DUMMY";
                var cam = CombatInputGate.ViewCamera();
                if (cam != null)
                    _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - cam.transform.position);
            }
        }
    }

    /// <summary>Keeps at most 3 enemies alive; auto-spawns one while the player is in the arena.</summary>
    public sealed class EnemyDirector : MonoBehaviour
    {
        public const int MaxEnemies = 3;
        public static EnemyDirector Instance { get; private set; }
        public int Kills { get; private set; }
        public bool AutoArena = true;
        float _emptySince;

        public static EnemyDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("QhysicsEnemyDirector");
            Instance = go.AddComponent<EnemyDirector>();
            CombatEvents.Killed += (t, d) => { if (t is CombatHealth h && h.GetComponent<CombatEnemy>() != null && Instance != null) Instance.Kills++; };
            return Instance;
        }

        public static int AliveCount()
        {
            int n = 0;
            foreach (var e in CombatEnemy.All)
                if (e != null && !e.IsDead) n++;
            return n;
        }

        public bool SpawnInFrontOfPlayer()
        {
            if (AliveCount() >= MaxEnemies)
            {
                QhysicsSfx.Play2D(SfxId.Denied, 0.5f);
                return false;
            }
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return false;
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-3f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 p = cam.transform.position + fwd * 4f;
            if (!CombatArena.GroundAt(p, out Vector3 g)) g = new Vector3(p.x, cam.transform.position.y - 1.6f, p.z);
            CombatEnemy.Spawn(g + Vector3.up * 0.05f, Quaternion.LookRotation(-fwd), 1f + Kills * 0.05f);
            return true;
        }

        void Update()
        {
            var arena = CombatArena.Instance;
            if (!AutoArena || arena == null || !arena.PlayerInside())
            {
                _emptySince = Time.time;
                return;
            }
            if (AliveCount() > 0)
            {
                _emptySince = Time.time;
                return;
            }
            if (Time.time - _emptySince > 4f)
            {
                _emptySince = Time.time;
                Vector3 sp = arena.EnemySpawnPoint();
                Vector3 look = arena.Center - sp; look.y = 0f;
                CombatEnemy.Spawn(sp, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward), 1f + Kills * 0.05f);
            }
        }
    }

    /// <summary>Procedural arena near the plaza: floor, low wall ring, weapon rack (auto-restocks), dummies.</summary>
    public sealed class CombatArena : MonoBehaviour
    {
        public const float Radius = 7f;
        public static CombatArena Instance { get; private set; }
        public Vector3 Center { get; private set; }
        Vector3 _entry;
        Vector3 _toPlaza;
        readonly List<(WeaponKind kind, Vector3 pos, Quaternion rot)> _slots = new List<(WeaponKind, Vector3, Quaternion)>();
        readonly List<PhysicsWeapon> _rack = new List<PhysicsWeapon>();
        float _nextRestock;

        public static bool GroundAt(Vector3 p, out Vector3 ground)
        {
            var hits = UnityEngine.Physics.RaycastAll(p + Vector3.up * 40f, Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            ground = p;
            bool found = false;
            foreach (var h in hits)
            {
                if (CombatInputGate.IsPlayerCollider(h.collider) || h.collider.attachedRigidbody != null) continue;
                if (h.distance < best) { best = h.distance; ground = h.point; found = true; }
            }
            return found;
        }

        public static CombatArena Build(Vector3 plaza)
        {
            if (Instance != null) return Instance;
            Vector3[] offsets =
            {
                new Vector3(0f, 0f, 22f), new Vector3(22f, 0f, 0f), new Vector3(-22f, 0f, 0f), new Vector3(0f, 0f, -22f),
                new Vector3(16f, 0f, 16f), new Vector3(-16f, 0f, 16f), new Vector3(16f, 0f, -16f), new Vector3(-16f, 0f, -16f),
                new Vector3(0f, 0f, 34f), new Vector3(34f, 0f, 0f)
            };
            Vector3 chosen = Vector3.zero;
            bool ok = false;
            Vector3 fallback = plaza + offsets[0];
            bool haveFallback = false;
            foreach (var o in offsets)
            {
                if (!GroundAt(plaza + o, out Vector3 g)) continue;
                if (Mathf.Abs(g.y - plaza.y) > 3f) continue;
                if (!haveFallback) { fallback = g; haveFallback = true; }
                var cols = UnityEngine.Physics.OverlapBox(g + Vector3.up * 1.7f, new Vector3(Radius, 1.3f, Radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool clear = true;
                foreach (var c in cols)
                    if (!CombatInputGate.IsPlayerCollider(c)) { clear = false; break; }
                if (clear) { chosen = g; ok = true; break; }
            }
            if (!ok) chosen = haveFallback ? fallback : plaza + offsets[0];

            var go = new GameObject("QhysicsCombatArena");
            Instance = go.AddComponent<CombatArena>();
            Instance.Create(chosen, plaza);
            Debug.Log("Combat: arena built at " + chosen + (ok ? "" : " (no fully clear site; using nearest ground)"));
            return Instance;
        }

        void Create(Vector3 center, Vector3 plaza)
        {
            Center = center;
            transform.position = center;
            _toPlaza = plaza - center; _toPlaza.y = 0f;
            if (_toPlaza.sqrMagnitude < 0.01f) _toPlaza = Vector3.back;
            _toPlaza.Normalize();
            transform.rotation = Quaternion.LookRotation(-_toPlaza); // local -Z faces the plaza (entrance)
            _entry = center + _toPlaza * (Radius - 1.2f);

            Material floor = CombatMaterials.Get(new Color(0.07f, 0.09f, 0.11f), 0.2f, 0.55f);
            Material rim = CombatMaterials.Get(new Color(0.02f, 0.2f, 0.25f), 0f, 0.6f, new Color(0f, 0.9f, 1f) * 0.9f);
            Material stone = CombatMaterials.Get(new Color(0.32f, 0.33f, 0.36f), 0f, 0.35f);
            Material wood = CombatMaterials.Get(new Color(0.35f, 0.24f, 0.15f), 0f, 0.3f);

            CombatUtil.FlatCylinderCollider(CombatUtil.Prim(PrimitiveType.Cylinder, "Floor", transform, new Vector3(0f, 0.03f, 0f), new Vector3(Radius * 2f, 0.03f, Radius * 2f), floor));
            CombatUtil.Prim(PrimitiveType.Cylinder, "Rim", transform, new Vector3(0f, 0.015f, 0f), new Vector3(Radius * 2f + 0.3f, 0.02f, Radius * 2f + 0.3f), rim, false);

            int segs = 20;
            for (int i = 0; i < segs; i++)
            {
                float a = (i + 0.5f) / segs * Mathf.PI * 2f;
                Vector3 lp = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Radius;
                // Entrance gap on local -Z (toward the plaza).
                if (Vector3.Angle(lp, Vector3.back) < 20f) continue;
                var w = CombatUtil.Prim(PrimitiveType.Cube, "Wall" + i, transform, lp + Vector3.up * 0.45f, new Vector3(2.15f, 0.9f, 0.3f), stone);
                w.transform.localRotation = Quaternion.LookRotation(lp.normalized);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 lp = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (Radius + 0.4f);
                CombatUtil.Prim(PrimitiveType.Cube, "Pillar" + i, transform, lp + Vector3.up * 1.3f, new Vector3(0.45f, 2.6f, 0.45f), stone);
                CombatUtil.Prim(PrimitiveType.Cube, "PillarGlow" + i, transform, lp + Vector3.up * 2.65f, new Vector3(0.5f, 0.1f, 0.5f), rim, false);
                var l = new GameObject("PillarLight" + i).AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.localPosition = lp + Vector3.up * 2.9f;
                l.type = LightType.Point;
                l.color = new Color(0.4f, 0.9f, 1f);
                l.range = 7f;
                l.intensity = 1.2f;
            }

            // Sign over the entrance.
            var sg = new GameObject("ArenaSign");
            sg.transform.SetParent(transform, false);
            sg.transform.localPosition = new Vector3(0f, 2.6f, -Radius);
            sg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var t = sg.AddComponent<TextMeshPro>();
            t.text = "COMBAT ARENA";
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 4f;
            t.color = new Color(0f, 0.9f, 1f, 0.95f);
            t.rectTransform.sizeDelta = new Vector2(6f, 1f);

            // Weapon table on the left inside the entrance.
            Vector3 tableLp = new Vector3(-3.4f, 0f, -4.2f);
            var table = CombatUtil.Prim(PrimitiveType.Cube, "WeaponTable", transform, tableLp + Vector3.up * 0.42f, new Vector3(2.6f, 0.84f, 0.8f), wood);
            table.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            WeaponKind[] kinds = { WeaponKind.Dagger, WeaponKind.Sword, WeaponKind.Spear, WeaponKind.Mace, WeaponKind.Shield };
            for (int i = 0; i < kinds.Length; i++)
            {
                Vector3 onTable = table.transform.TransformPoint(new Vector3(-0.4f + i * 0.2f, 0.5f, 0f)) + Vector3.up * 0.06f;
                Quaternion rot = kinds[i] == WeaponKind.Shield
                    ? table.transform.rotation * Quaternion.Euler(0f, 0f, 0f)
                    : table.transform.rotation * Quaternion.Euler(90f, 0f, 0f); // lying flat, tip away from the entrance
                Vector3 back = table.transform.rotation * Vector3.back * (kinds[i] == WeaponKind.Spear ? 0.9f : 0.3f);
                _slots.Add((kinds[i], onTable + (kinds[i] == WeaponKind.Shield ? Vector3.zero : back), rot));
                _rack.Add(null);
            }
            Restock(true);

            TrainingDummy.Spawn(transform.TransformPoint(new Vector3(3.2f, 0.06f, -3.2f)), transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            TrainingDummy.Spawn(transform.TransformPoint(new Vector3(4.4f, 0.06f, -1.2f)), transform.rotation * Quaternion.Euler(0f, 210f, 0f));
        }

        void Restock(bool force)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var w = _rack[i];
                bool gone = w == null
                    || (!w.IsHeld && !w.IsStuck && (Vector3.Distance(w.transform.position, Center) > 30f || w.transform.position.y < Center.y - 10f));
                if (!gone && !force) continue;
                if (w != null && gone) Destroy(w.gameObject);
                if (w != null && !gone) continue;
                var s = _slots[i];
                _rack[i] = WeaponFactory.Spawn(s.kind, s.pos, s.rot);
            }
        }

        void Update()
        {
            if (Time.time >= _nextRestock)
            {
                _nextRestock = Time.time + 5f;
                Restock(false);
            }
        }

        public bool PlayerInside()
        {
            var cam = CombatInputGate.ViewCamera();
            if (cam == null) return false;
            Vector3 d = cam.transform.position - Center;
            return Mathf.Abs(d.y) < 4f && new Vector2(d.x, d.z).magnitude < Radius;
        }

        public Vector3 EnemySpawnPoint()
        {
            float a = Random.Range(-50f, 50f);
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * -_toPlaza;
            return Center + dir * (Radius - 1.6f) + Vector3.up * 0.1f;
        }

        /// <summary>L key / toolbelt: go to the arena, or back to the plaza if already inside.</summary>
        public void TogglePlayerTeleport()
        {
            if (PlayerInside())
            {
                LabPlayerSpawn.EnsureApplied();
                return;
            }
            var originGo = GameObject.Find(LabPlayerSpawn.OriginName);
            if (originGo == null) return;
            var cc = originGo.GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            Transform o = originGo.transform;
            var cam = CombatInputGate.ViewCamera();
            // Face the arena centre, then keep the head (not the rig origin) over the entry point (room-scale XR).
            Vector3 look = Center - _entry; look.y = 0f;
            float camYaw = cam != null ? cam.transform.eulerAngles.y - o.eulerAngles.y : 0f;
            o.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(look).eulerAngles.y - camYaw, 0f);
            Vector3 headOffset = cam != null ? cam.transform.position - o.position : Vector3.zero;
            headOffset.y = 0f;
            o.position = _entry - headOffset + Vector3.up * 0.05f;
            if (cc != null) cc.enabled = had;
            QhysicsSfx.Play2D(SfxId.Unlock, 0.5f);
        }
    }
}
