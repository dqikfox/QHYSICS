using System.Collections.Generic;
using UnityEngine;
using RealityEngine.Audio;
using RealityEngine.Player;
using TMPro;

namespace RealityEngine.Combat
{
    public enum ArenaTier { Recruit = 0, Veteran = 1, Champion = 2, Legend = 3 }

    /// <summary>
    /// Arena wave mode: pick a tier (tier plate / toolbelt "Tier" / Alt+L), ring the gong (any weapon hit &gt; 2.5 m/s,
    /// toolbelt "Waves", Shift+L) and fight escalating waves of Raiders / Duelists / Wardens / Casters with a Brute boss
    /// on the last wave. 6 s breaks heal 25 %. Player death or 30 s outside the arena ends the run.
    /// Also hosts the loot janitor that tidies old dropped enemy weapons.
    /// </summary>
    public sealed class ArenaWaves : MonoBehaviour
    {
        public const string BestKey = "qhysics.arena.best";

        struct TierDef
        {
            public string name;
            public float hp, dmg, tempo;
            public int cap, waves;
            public Color color;
        }

        static readonly TierDef[] Tiers =
        {
            new TierDef { name = "RECRUIT",  hp = 0.8f,  dmg = 0.7f,  tempo = 1.25f, cap = 2, waves = 5, color = new Color(0.45f, 1f, 0.6f) },
            new TierDef { name = "VETERAN",  hp = 1f,    dmg = 1f,    tempo = 1f,    cap = 3, waves = 6, color = new Color(0.3f, 0.9f, 1f) },
            new TierDef { name = "CHAMPION", hp = 1.35f, dmg = 1.25f, tempo = 0.85f, cap = 3, waves = 7, color = new Color(1f, 0.7f, 0.25f) },
            new TierDef { name = "LEGEND",   hp = 1.8f,  dmg = 1.5f,  tempo = 0.7f,  cap = 4, waves = 8, color = new Color(1f, 0.3f, 0.35f) },
        };

        enum Phase { Idle, Fighting, Break }

        public static ArenaWaves Instance { get; private set; }
        public static bool Active => Instance != null && Instance._phase != Phase.Idle;
        /// <summary>Raised at the start of every wave (props respawn on this).</summary>
        public static event System.Action<int> WaveStarted;

        public ArenaTier Tier { get; private set; } = ArenaTier.Veteran;
        public int Wave => _wave;
        public int TotalWaves => Tiers[(int)Tier].waves;

        CombatArena _arena;
        Phase _phase = Phase.Idle;
        int _wave;
        readonly List<EnemyType> _queue = new List<EnemyType>();
        readonly List<CombatEnemy> _spawned = new List<CombatEnemy>();
        float _nextSpawn, _breakUntil, _outsideFor, _lastCountdown;
        bool _warnedOutside;
        PlayerVitality _vit;
        TextMeshPro _board;
        float _nextBoard, _nextJanitor;
        Transform _gongDisc;
        Renderer _plateGlow;

        public static ArenaWaves Ensure(CombatArena arena)
        {
            if (Instance != null) return Instance;
            if (arena == null) arena = CombatArena.Instance;
            if (arena == null) return null;
            Instance = arena.gameObject.AddComponent<ArenaWaves>();
            Instance._arena = arena;
            Instance.Tier = (ArenaTier)Mathf.Clamp(PlayerPrefs.GetInt("qhysics.arena.tier", 1), 0, 3);
            Instance.BuildControls();
            return Instance;
        }

        static TierDef Def(ArenaTier t) => Tiers[(int)t];

        public static string TierName(ArenaTier t) => Def(t).name;

        static int BestCleared => PlayerPrefs.GetInt(BestKey, 0); // 0 = none, else tier index + 1

        // ------------------------------------------------------------------ public controls

        /// <summary>Gong / toolbelt "Waves" / Shift+L: start a run, or stop the current one.</summary>
        public void Toggle()
        {
            if (_phase == Phase.Idle) StartRun();
            else StopRun("Waves stopped", true);
        }

        /// <summary>Tier plate / toolbelt "Tier" / Alt+L. Locked during a run.</summary>
        public void CycleTier()
        {
            if (_phase != Phase.Idle)
            {
                QhysicsSfx.Play2D(SfxId.Denied, 0.5f);
                CombatHud.Toast("Tier is locked during a run");
                return;
            }
            Tier = (ArenaTier)(((int)Tier + 1) % Tiers.Length);
            PlayerPrefs.SetInt("qhysics.arena.tier", (int)Tier);
            var d = Def(Tier);
            QhysicsSfx.Play2D(SfxId.UiClick, 0.6f, 0.9f + 0.1f * (int)Tier);
            CombatHud.Toast("Arena tier: " + d.name + "  (" + d.waves + " waves, up to " + d.cap + " at once)");
            RefreshBoard();
        }

        /// <summary>Gong struck: start, or skip the break between waves.</summary>
        public void GongStruck()
        {
            if (_phase == Phase.Idle) StartRun();
            else if (_phase == Phase.Break) _breakUntil = Mathf.Min(_breakUntil, Time.time + 0.5f);
        }

        void StartRun()
        {
            if (_arena == null) return;
            // Clean slate: remove stray (non-wave) enemies inside the arena.
            for (int i = CombatEnemy.All.Count - 1; i >= 0; i--)
            {
                var e = CombatEnemy.All[i];
                if (e == null || e.IsDead) continue;
                if (Flat(e.transform.position - _arena.Center) < CombatArena.Radius + 3f)
                {
                    SpawnFx.Spawn(e.transform.position, 0.8f);
                    Destroy(e.gameObject);
                }
            }
            _spawned.Clear();
            _wave = 0;
            _outsideFor = 0f;
            _warnedOutside = false;
            HookVitality();
            CombatHud.Toast("Arena: " + Def(Tier).name + " - " + TotalWaves + " waves. Survive!");
            BeginBreak(2.5f);
        }

        void StopRun(string msg, bool despawn)
        {
            if (_phase == Phase.Idle) return;
            _phase = Phase.Idle;
            _queue.Clear();
            if (despawn)
            {
                foreach (var e in _spawned)
                    if (e != null && !e.IsDead)
                    {
                        SpawnFx.Spawn(e.transform.position, 0.8f);
                        Destroy(e.gameObject);
                    }
            }
            _spawned.Clear();
            if (!string.IsNullOrEmpty(msg)) CombatHud.Toast(msg);
            QhysicsSfx.Play2D(SfxId.Denied, 0.45f, 0.8f);
            RefreshBoard();
        }

        void BeginBreak(float seconds)
        {
            _phase = Phase.Break;
            _breakUntil = Time.time + seconds;
            _lastCountdown = -1f;
            RefreshBoard();
        }

        void BeginWave()
        {
            _wave++;
            _phase = Phase.Fighting;
            BuildWave(_wave);
            _nextSpawn = Time.time + 0.4f;
            bool final = _wave >= TotalWaves;
            QhysicsSfx.Play2D(SfxId.WaveStart, 0.75f, final ? 0.8f : 1f);
            CombatHud.Toast((final ? "FINAL WAVE " : "Wave ") + _wave + "/" + TotalWaves + "  -  " + _queue.Count + " foes" + (final ? " + BRUTE" : ""));
            try { WaveStarted?.Invoke(_wave); } catch (System.Exception ex) { Debug.LogException(ex); }
            RefreshBoard();
        }

        void BuildWave(int w)
        {
            _queue.Clear();
            int tier = (int)Tier;
            bool final = w >= TotalWaves;
            int count = Mathf.Min(9, 1 + w + (tier >= 2 ? 1 : 0) + (tier >= 3 && w >= 4 ? 1 : 0));
            if (final)
            {
                _queue.Add(EnemyType.Brute);
                count -= 1;
            }
            int casters = 0, brutes = 0;
            for (int i = 0; i < count; i++)
            {
                var pool = new List<EnemyType> { EnemyType.Raider, EnemyType.Raider };
                if (w >= 2) pool.Add(EnemyType.Duelist);
                if (w >= 3) { pool.Add(EnemyType.Warden); if (casters < 2) pool.Add(EnemyType.Caster); }
                if (w >= 4) pool.Add(EnemyType.Duelist);
                if (!final && tier >= 2 && w >= 4 && brutes == 0) pool.Add(EnemyType.Brute);
                var t = pool[Random.Range(0, pool.Count)];
                // Guarantee a taste of each new type the wave it unlocks.
                if (i == 0 && w == 2) t = EnemyType.Duelist;
                if (i == 0 && w == 3) t = EnemyType.Warden;
                if (i == 1 && w == 3 && casters < 2) t = EnemyType.Caster;
                if (t == EnemyType.Caster) casters++;
                if (t == EnemyType.Brute) brutes++;
                _queue.Add(t);
            }
            // Final wave: the boss walks in after the first escort.
            if (final && _queue.Count > 1)
            {
                _queue.RemoveAt(0);
                _queue.Insert(1, EnemyType.Brute);
            }
        }

        int AliveSpawned()
        {
            int n = 0;
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                var e = _spawned[i];
                if (e == null || e.IsDead) { _spawned.RemoveAt(i); continue; }
                n++;
            }
            return n;
        }

        void SpawnNext()
        {
            var type = _queue[0];
            _queue.RemoveAt(0);
            Vector3 sp = _arena.EnemySpawnPoint();
            for (int tries = 0; tries < 6; tries++)
            {
                bool clear = true;
                foreach (var e in CombatEnemy.All)
                    if (e != null && !e.IsDead && Flat(e.transform.position - sp) < 1.3f) { clear = false; break; }
                if (clear) break;
                sp = _arena.EnemySpawnPoint();
            }
            if (CombatArena.GroundAt(sp, out Vector3 g) && Mathf.Abs(g.y - sp.y) < 1.5f) sp = g + Vector3.up * 0.05f;
            Vector3 look = _arena.Center - sp; look.y = 0f;
            var d = Def(Tier);
            bool boss = type == EnemyType.Brute && _wave >= TotalWaves;
            float hp = d.hp * (1f + 0.04f * (_wave - 1)) * (boss ? 1.5f : 1f);
            var enemy = CombatEnemy.Spawn(sp, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward), hp, type, d.dmg, d.tempo);
            if (enemy != null)
            {
                if (boss) enemy.gameObject.name = "CombatEnemy_Brute_Boss";
                _spawned.Add(enemy);
            }
            SpawnFx.Spawn(sp, boss ? 1.6f : 1f);
            QhysicsSfx.PlayAt(SfxId.SpawnIn, sp + Vector3.up, boss ? 1f : 0.75f, boss ? 0.7f : 1f);
        }

        void WaveCleared()
        {
            int tier = (int)Tier;
            SkillSystem.AddXp(10 * (tier + 1), null);
            if (_wave >= TotalWaves)
            {
                _phase = Phase.Idle;
                QhysicsSfx.Play2D(SfxId.WaveClear, 0.9f);
                QhysicsSfx.Play2D(SfxId.GongHit, 0.5f, 1.2f);
                int xp = 80 * (tier + 1) + 15 * TotalWaves;
                SkillSystem.AddXp(xp, "Arena cleared (" + Def(Tier).name + ")");
                bool newBest = tier + 1 > BestCleared;
                if (newBest) { PlayerPrefs.SetInt(BestKey, tier + 1); PlayerPrefs.Save(); }
                CombatHud.Toast("ARENA CLEARED - " + Def(Tier).name + (newBest ? "  NEW BEST!" : "") + "  +" + xp + " XP");
                if (_vit != null) _vit.Heal(_vit.MaxHp);
                RefreshBoard();
                return;
            }
            QhysicsSfx.Play2D(SfxId.WaveClear, 0.6f, 0.9f);
            if (_vit != null) _vit.Heal(_vit.MaxHp * 0.25f);
            CombatHud.Toast("Wave " + _wave + " cleared! +25% HP - next wave in 6 s (hit the gong to skip)");
            BeginBreak(6f);
        }

        // ------------------------------------------------------------------ loop

        void HookVitality()
        {
            var v = PlayerVitality.Instance;
            if (v == _vit) return;
            if (_vit != null) _vit.Died -= OnPlayerDied;
            _vit = v;
            if (_vit != null) _vit.Died += OnPlayerDied;
        }

        void OnPlayerDied()
        {
            if (_phase == Phase.Idle) return;
            StopRun("You fell on wave " + _wave + "/" + TotalWaves + " - ring the gong to try again", true);
        }

        void OnDestroy()
        {
            if (_vit != null) _vit.Died -= OnPlayerDied;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Time.time >= _nextJanitor)
            {
                _nextJanitor = Time.time + 2f;
                LootJanitor();
            }
            if (Time.time >= _nextBoard)
            {
                _nextBoard = Time.time + 0.25f;
                RefreshBoard();
            }
            if (_phase == Phase.Idle || _arena == null) return;
            HookVitality();

            bool inside = _arena.PlayerInside();
            if (!inside)
            {
                _outsideFor += Time.deltaTime;
                if (_outsideFor > 10f && !_warnedOutside)
                {
                    _warnedOutside = true;
                    CombatHud.Toast("Waves paused - return to the arena within 20 s");
                }
                if (_outsideFor > 30f) { StopRun("Left the arena - waves ended", true); return; }
            }
            else
            {
                _outsideFor = 0f;
                _warnedOutside = false;
            }

            if (_phase == Phase.Break)
            {
                float left = _breakUntil - Time.time;
                if (inside && left > 0f && left <= 3f && Mathf.Ceil(left) != _lastCountdown)
                {
                    _lastCountdown = Mathf.Ceil(left);
                    QhysicsSfx.Play2D(SfxId.UiClick, 0.4f, 1.2f);
                    CombatHud.Toast("Wave " + (_wave + 1) + " in " + _lastCountdown.ToString("0") + "...");
                }
                if (left <= 0f)
                {
                    if (inside) BeginWave();
                    else _breakUntil = Time.time + 0.5f; // hold the break until the player is back
                }
                return;
            }

            // Fighting
            int alive = AliveSpawned();
            if (_queue.Count > 0)
            {
                if (_outsideFor < 10f && alive < Def(Tier).cap && Time.time >= _nextSpawn)
                {
                    _nextSpawn = Time.time + 1.5f;
                    SpawnNext();
                }
            }
            else if (alive == 0)
            {
                WaveCleared();
            }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // ------------------------------------------------------------------ loot janitor

        static readonly List<LootTag> _loot = new List<LootTag>();

        /// <summary>Keeps dropped enemy weapons from piling up: max 8 (oldest idle &gt; 20 s go first), none idle &gt; 150 s.</summary>
        static void LootJanitor()
        {
            _loot.Clear();
            foreach (var tag in FindObjectsByType<LootTag>(FindObjectsSortMode.None))
            {
                if (tag == null) continue;
                var w = tag.GetComponent<PhysicsWeapon>();
                if (w == null) { _loot.Add(tag); continue; }
                if (w.IsHeld || w.IsStuck || w.enemyOwner != null) { tag.born = Time.time; continue; } // in use: stays fresh
                _loot.Add(tag);
            }
            _loot.Sort((a, b) => a.born.CompareTo(b.born));
            int total = _loot.Count;
            foreach (var tag in _loot)
            {
                float idle = Time.time - tag.born;
                bool over = total > 8 && idle > 20f;
                if (over || idle > 150f)
                {
                    total--;
                    Destroy(tag.gameObject);
                }
            }
        }

        // ------------------------------------------------------------------ physical controls (gong, tier plate, board)

        void BuildControls()
        {
            Transform at = _arena.transform;
            Material stone = CombatMaterials.Get(new Color(0.16f, 0.17f, 0.2f), 0.1f, 0.45f);
            Material rim = CombatMaterials.Get(new Color(0.02f, 0.2f, 0.25f), 0f, 0.6f, new Color(0f, 0.9f, 1f) * 0.9f);
            Material bronze = CombatMaterials.Get(new Color(0.55f, 0.4f, 0.18f), 0.85f, 0.6f);
            Material dark = CombatMaterials.Get(new Color(0.05f, 0.06f, 0.07f), 0.3f, 0.5f);

            // Gong frame (static), just outside the spawn arc on the left-rear of the arena, facing the centre.
            float ang = -62f * Mathf.Deg2Rad;
            Vector3 lp = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 6.2f;
            var frame = new GameObject("ArenaGong");
            frame.transform.SetParent(at, false);
            frame.transform.localPosition = lp;
            frame.transform.localRotation = Quaternion.LookRotation(-lp.normalized); // +Z faces the centre
            Transform f = frame.transform;
            CombatUtil.Prim(PrimitiveType.Cube, "PostL", f, new Vector3(-0.75f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.14f), stone);
            CombatUtil.Prim(PrimitiveType.Cube, "PostR", f, new Vector3(0.75f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.14f), stone);
            CombatUtil.Prim(PrimitiveType.Cube, "Beam", f, new Vector3(0f, 2.3f, 0f), new Vector3(1.75f, 0.14f, 0.16f), stone);
            CombatUtil.Prim(PrimitiveType.Cube, "BeamGlow", f, new Vector3(0f, 2.39f, 0f), new Vector3(1.6f, 0.03f, 0.1f), rim, false);
            CombatUtil.Prim(PrimitiveType.Cube, "FootL", f, new Vector3(-0.75f, 0.05f, 0f), new Vector3(0.36f, 0.1f, 0.5f), dark);
            CombatUtil.Prim(PrimitiveType.Cube, "FootR", f, new Vector3(0.75f, 0.05f, 0f), new Vector3(0.36f, 0.1f, 0.5f), dark);
            CombatUtil.Prim(PrimitiveType.Cylinder, "Chain", f, new Vector3(0f, 2.13f, 0f), new Vector3(0.03f, 0.1f, 0.03f), dark, false);

            // Swinging disc on a hinge (world-anchored).
            var disc = CombatUtil.Prim(PrimitiveType.Cylinder, "GongDisc", null, Vector3.zero, new Vector3(1.0f, 0.03f, 1.0f), bronze);
            disc.transform.SetPositionAndRotation(f.TransformPoint(new Vector3(0f, 1.5f, 0f)), f.rotation * Quaternion.Euler(90f, 0f, 0f));
            CombatUtil.FlatCylinderCollider(disc);
            var mc = disc.GetComponent<MeshCollider>();
            if (mc != null) mc.convex = true;
            CombatUtil.Prim(PrimitiveType.Cylinder, "Boss", disc.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.28f, 0.6f, 0.28f), rim, false);
            CombatUtil.Prim(PrimitiveType.Cylinder, "BossBack", disc.transform, new Vector3(0f, -0.6f, 0f), new Vector3(0.28f, 0.6f, 0.28f), rim, false);
            disc.transform.SetParent(f, true);
            var rb = disc.AddComponent<Rigidbody>();
            rb.mass = 6f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var hj = disc.AddComponent<HingeJoint>();
            hj.autoConfigureConnectedAnchor = true;
            hj.anchor = disc.transform.InverseTransformPoint(f.TransformPoint(new Vector3(0f, 2.2f, 0f)));
            hj.axis = disc.transform.InverseTransformDirection(f.right);
            hj.useLimits = true;
            hj.limits = new JointLimits { min = -50f, max = 50f };
            disc.AddComponent<ArenaGong>().waves = this;
            _gongDisc = disc.transform;

            // Tier plate: a pedestal with a glowing pad; touch it (hand or weapon) to cycle the tier.
            float ang2 = -76f * Mathf.Deg2Rad;
            Vector3 lp2 = new Vector3(Mathf.Sin(ang2), 0f, Mathf.Cos(ang2)) * 6.0f;
            var ped = new GameObject("ArenaTierPlate");
            ped.transform.SetParent(at, false);
            ped.transform.localPosition = lp2;
            ped.transform.localRotation = Quaternion.LookRotation(-lp2.normalized);
            CombatUtil.Prim(PrimitiveType.Cube, "Pedestal", ped.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.45f, 1f, 0.45f), stone);
            var pad = CombatUtil.Prim(PrimitiveType.Cube, "Pad", ped.transform, new Vector3(0f, 1.03f, 0f), new Vector3(0.36f, 0.05f, 0.36f), TierMat(Tier), false);
            _plateGlow = pad.GetComponent<Renderer>();
            var trig = new GameObject("PadTrigger");
            trig.transform.SetParent(ped.transform, false);
            trig.transform.localPosition = new Vector3(0f, 1.12f, 0f);
            var bc = trig.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.38f, 0.18f, 0.38f);
            trig.AddComponent<ArenaTierPad>().waves = this;

            // Board above the gong.
            var bg = new GameObject("ArenaWaveBoard");
            bg.transform.SetParent(f, false);
            bg.transform.localPosition = new Vector3(0f, 2.95f, 0f);
            bg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TMP reads along -Z; flip to face the centre
            _board = bg.AddComponent<TextMeshPro>();
            _board.alignment = TextAlignmentOptions.Center;
            _board.fontSize = 1.5f;
            _board.rectTransform.sizeDelta = new Vector2(3.2f, 1.2f);
            _board.color = new Color(0.75f, 0.95f, 1f, 0.95f);
            RefreshBoard();
        }

        static Material TierMat(ArenaTier t)
        {
            Color c = Def(t).color;
            return CombatMaterials.Get(c * 0.25f, 0f, 0.6f, c * 1.2f);
        }

        void RefreshBoard()
        {
            if (_plateGlow != null) _plateGlow.sharedMaterial = TierMat(Tier);
            if (_board == null) return;
            var d = Def(Tier);
            string best = BestCleared > 0 ? Tiers[BestCleared - 1].name : "-";
            string hex = ColorUtility.ToHtmlStringRGB(d.color);
            string status;
            switch (_phase)
            {
                case Phase.Fighting:
                    status = "WAVE " + _wave + "/" + TotalWaves + "  -  " + (AliveSpawnedCount() + _queue.Count) + " LEFT";
                    break;
                case Phase.Break:
                    status = (_wave == 0 ? "GET READY" : "WAVE " + _wave + " CLEAR") + "  -  NEXT IN " + Mathf.Max(0f, _breakUntil - Time.time).ToString("0");
                    break;
                default:
                    status = "RING THE GONG TO BEGIN";
                    break;
            }
            _board.text = "<b>ARENA WAVES</b>\n<color=#" + hex + ">" + d.name + "</color>  " + d.waves + " waves  |  BEST " + best + "\n<size=80%>" + status + "</size>";
        }

        int AliveSpawnedCount()
        {
            int n = 0;
            foreach (var e in _spawned) if (e != null && !e.IsDead) n++;
            return n;
        }
    }

    /// <summary>Hinged gong disc: a weapon (or thrown object) hit faster than 2.5 m/s rings it and starts / advances the waves.</summary>
    public sealed class ArenaGong : MonoBehaviour
    {
        public ArenaWaves waves;
        float _last = -9f;

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.2f || Time.time - _last < 0.35f) return;
            var other = c.collider;
            if (other == null) return;
            var w = other.GetComponentInParent<PhysicsWeapon>();
            if (w != null && w.enemyOwner != null) return;
            if (other.GetComponentInParent<CombatEnemy>() != null) return;
            _last = Time.time;
            Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            QhysicsSfx.PlayAt(SfxId.GongHit, p, Mathf.Clamp01(0.25f + speed / 8f), Random.Range(0.97f, 1.03f));
            if (speed >= 2.5f)
            {
                SparkBurst.Spawn(p, new Color(1f, 0.8f, 0.4f), 12);
                if (w != null && w.HeldByPlayer) CombatHaptics.Pulse(w.PrimaryNode, 0.8f, 0.12f);
                if (waves != null) waves.GongStruck();
            }
        }
    }

    /// <summary>Trigger pad: a player hand / body or player weapon touching it cycles the arena tier.</summary>
    public sealed class ArenaTierPad : MonoBehaviour
    {
        public ArenaWaves waves;
        float _last = -9f;

        void OnTriggerEnter(Collider other)
        {
            if (waves == null || Time.time - _last < 0.7f) return;
            var w = other.GetComponentInParent<PhysicsWeapon>();
            bool player = (w != null && (w.HeldByPlayer || w.ActiveTeam == CombatTeam.Player)) || CombatInputGate.IsPlayerCollider(other);
            if (!player) return;
            _last = Time.time;
            waves.CycleTier();
        }
    }

    /// <summary>Cheap spawn-in column: a cyan emissive cylinder that shoots up and shrinks (no light).</summary>
    public sealed class SpawnFx : MonoBehaviour
    {
        float _born, _size;
        Transform _col, _ring;

        public static void Spawn(Vector3 feet, float size)
        {
            var go = new GameObject("SpawnFx");
            go.transform.position = feet;
            Material m = CombatMaterials.Get(new Color(0.02f, 0.25f, 0.3f), 0f, 0.6f, new Color(0f, 0.9f, 1f) * 2.2f);
            var fx = go.AddComponent<SpawnFx>();
            fx._col = CombatUtil.Prim(PrimitiveType.Cylinder, "Column", go.transform, Vector3.up, new Vector3(0.6f, 1f, 0.6f) * size, m, false).transform;
            fx._ring = CombatUtil.Prim(PrimitiveType.Cylinder, "Ring", go.transform, Vector3.up * 0.02f, new Vector3(0.4f, 0.01f, 0.4f) * size, m, false).transform;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx._born = Time.time;
            fx._size = size;
            SparkBurst.Spawn(feet + Vector3.up * 0.2f, new Color(0.3f, 0.9f, 1f), 16);
        }

        void Update()
        {
            float a = (Time.time - _born) / 0.6f;
            if (a >= 1f) { Destroy(gameObject); return; }
            float w = Mathf.Lerp(0.6f, 0.02f, a) * _size;
            _col.localScale = new Vector3(w, Mathf.Lerp(0.2f, 1.6f, a) * _size, w);
            _col.localPosition = Vector3.up * _col.localScale.y;
            float r = Mathf.Lerp(0.4f, 2.4f, a) * _size;
            _ring.localScale = new Vector3(r, 0.01f, r);
        }
    }
}
