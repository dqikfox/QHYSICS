using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using UiButton = UnityEngine.UI.Button;
using RealityEngine.Audio;
using RealityEngine.UI;
using RealityEngine.Challenges;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Combat
{
    public sealed class SkillDef
    {
        public string id, name, branch, desc, requires;
        public int maxRank, requiresRank;
    }

    /// <summary>
    /// XP (kill 40, challenge 50 + 10/star, parry 5), levels (+1 point each) and a small skill tree.
    /// Saved to persistentDataPath/QHYSICS/skills.json. Exposes multipliers used by combat code.
    /// </summary>
    public static class SkillSystem
    {
        public static readonly SkillDef[] Defs =
        {
            new SkillDef { id = "keen_edge", name = "Keen Edge", branch = "MELEE", maxRank = 3, desc = "+10% slash/pierce damage" },
            new SkillDef { id = "heavy_hands", name = "Heavy Hands", branch = "MELEE", maxRank = 3, desc = "+8% blunt, +10% grip strength" },
            new SkillDef { id = "riposte", name = "Riposte", branch = "MELEE", maxRank = 2, desc = "Parry at 15% lower speed", requires = "keen_edge", requiresRank = 1 },
            new SkillDef { id = "deep_stab", name = "Deep Stab", branch = "MELEE", maxRank = 2, desc = "Easier stabs, stronger hold", requires = "keen_edge", requiresRank = 1 },
            new SkillDef { id = "iron_skin", name = "Iron Skin", branch = "MELEE", maxRank = 3, desc = "-8% damage taken" },
            new SkillDef { id = "mana_well", name = "Mana Well", branch = "MAGIC", maxRank = 3, desc = "+20% max mana" },
            new SkillDef { id = "flow", name = "Flow", branch = "MAGIC", maxRank = 3, desc = "+20% mana regen" },
            new SkillDef { id = "overcharge", name = "Overcharge", branch = "MAGIC", maxRank = 3, desc = "+12% spell damage", requires = "mana_well", requiresRank = 1 },
            new SkillDef { id = "lasting_imbue", name = "Lasting Imbue", branch = "MAGIC", maxRank = 2, desc = "+50% imbue time", requires = "flow", requiresRank = 1 },
            new SkillDef { id = "deep_focus", name = "Deep Focus", branch = "MIND", maxRank = 3, desc = "+25% focus duration" },
            new SkillDef { id = "throwing_arm", name = "Throwing Arm", branch = "MIND", maxRank = 2, desc = "+12% throw speed" },
            new SkillDef { id = "light_grip", name = "Light Grip", branch = "MIND", maxRank = 2, desc = "Damage from slower swings" },
        };

        [Serializable]
        sealed class SaveData
        {
            public int level = 1;
            public int xp;
            public int points;
            public List<string> ids = new List<string>();
            public List<int> ranks = new List<int>();
        }

        static SaveData _data;
        static readonly Dictionary<string, int> _ranks = new Dictionary<string, int>();
        public static event Action Changed;

        public static int Level { get { Load(); return _data.level; } }
        public static int Xp { get { Load(); return _data.xp; } }
        public static int Points { get { Load(); return _data.points; } }
        public static int XpToNext => 100 + 50 * (Level - 1);

        static string SavePath => Path.Combine(Application.persistentDataPath, "QHYSICS", "skills.json");

        public static int Rank(string id)
        {
            Load();
            return _ranks.TryGetValue(id, out int r) ? r : 0;
        }

        // ---- Multipliers used by combat code
        public static float DamageMult(DamageType t)
        {
            if (t == DamageType.Slash || t == DamageType.Pierce) return 1f + 0.10f * Rank("keen_edge");
            if (t == DamageType.Blunt) return 1f + 0.08f * Rank("heavy_hands");
            return 1f;
        }
        public static float StrengthMult => 1f + 0.10f * Rank("heavy_hands");
        public static float ParryThresholdMult => 1f - 0.15f * Rank("riposte");
        public static float StabThresholdMult => 1f - 0.15f * Rank("deep_stab");
        public static float PullOutForceMult => 1f + 0.25f * Rank("deep_stab");
        public static float IncomingDamageMult => 1f - 0.08f * Rank("iron_skin");
        public static float ManaMaxMult => 1f + 0.20f * Rank("mana_well");
        public static float ManaRegenMult => 1f + 0.20f * Rank("flow");
        public static float SpellPowerMult => 1f + 0.12f * Rank("overcharge");
        public static float ImbueDurationMult => 1f + 0.5f * Rank("lasting_imbue");
        public static float FocusDurationMult => 1f + 0.25f * Rank("deep_focus");
        public static float ThrowMult => 1f + 0.12f * Rank("throwing_arm");
        public static float SwingThresholdMult => 1f - 0.12f * Rank("light_grip");
        public static float EnemyBlockChanceMult => 1f;

        static bool _hooked;

        /// <summary>Wire XP sources (idempotent).</summary>
        public static void Hook()
        {
            Load();
            if (_hooked) return;
            _hooked = true;
            CombatEvents.Killed += (t, d) =>
            {
                if (t is CombatHealth h && h.GetComponent<CombatEnemy>() != null) AddXp(40, "Enemy down");
            };
            CombatEvents.Parried += p => AddXp(5, null);
        }

        static ChallengeManager _hookedMgr;

        /// <summary>Called periodically: subscribes to the (possibly late-created) challenge manager.</summary>
        public static void HookChallenges()
        {
            var m = ChallengeManager.Instance;
            if (m == null || m == _hookedMgr) return;
            _hookedMgr = m;
            m.OnChallengeCompleted += (def, stars, count) => AddXp(50 + 10 * Mathf.Max(0, stars), "Challenge complete");
        }

        public static void AddXp(int amount, string reason)
        {
            Load();
            _data.xp += amount;
            bool leveled = false;
            while (_data.xp >= XpToNext)
            {
                _data.xp -= XpToNext;
                _data.level++;
                _data.points++;
                leveled = true;
            }
            Save();
            Changed?.Invoke();
            if (leveled)
            {
                QhysicsSfx.Play2D(SfxId.LevelUp, 0.8f);
                QhysicsHintCard.Show("Level " + _data.level + "!  +1 skill point  |  K / toolbelt COMBAT > Skills", 5f);
            }
            else if (!string.IsNullOrEmpty(reason))
                CombatHud.Toast("+" + amount + " XP  " + reason);
        }

        public static bool CanBuy(SkillDef d, out string why)
        {
            why = null;
            if (Rank(d.id) >= d.maxRank) { why = "Maxed"; return false; }
            if (!string.IsNullOrEmpty(d.requires) && Rank(d.requires) < d.requiresRank)
            {
                why = "Needs " + Array.Find(Defs, x => x.id == d.requires)?.name;
                return false;
            }
            if (Points <= 0) { why = "No points"; return false; }
            return true;
        }

        public static bool Buy(SkillDef d)
        {
            if (!CanBuy(d, out _)) { QhysicsSfx.Play2D(SfxId.Denied, 0.5f); return false; }
            _ranks[d.id] = Rank(d.id) + 1;
            _data.points--;
            Save();
            QhysicsSfx.Play2D(SfxId.Unlock, 0.7f);
            Changed?.Invoke();
            return true;
        }

        static void Load()
        {
            if (_data != null) return;
            _data = new SaveData();
            try
            {
                if (File.Exists(SavePath))
                    _data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();
            }
            catch (Exception e) { Debug.LogWarning("Skills: could not read save (" + e.Message + "), starting fresh."); _data = new SaveData(); }
            _ranks.Clear();
            for (int i = 0; i < _data.ids.Count && i < _data.ranks.Count; i++)
                _ranks[_data.ids[i]] = _data.ranks[i];
            if (_data.level < 1) _data.level = 1;
        }

        static void Save()
        {
            try
            {
                _data.ids.Clear(); _data.ranks.Clear();
                foreach (var kv in _ranks) { _data.ids.Add(kv.Key); _data.ranks.Add(kv.Value); }
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath, JsonUtility.ToJson(_data, true));
            }
            catch (Exception e) { Debug.LogWarning("Skills: save failed: " + e.Message); }
        }
    }

    /// <summary>Skill tree panel (K, pause menu Skills, toolbelt COMBAT > Skills). Screen overlay on desktop, world panel in XR.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(209)]
    public sealed class SkillsPanel : MonoBehaviour
    {
        static SkillsPanel _instance;
        public static bool IsOpen => _instance != null && _instance._open;

        QhysicsHudCanvas _hud;
        TextMeshProUGUI _header;
        readonly List<(SkillDef def, UiButton btn, TextMeshProUGUI label)> _chips = new List<(SkillDef, UiButton, TextMeshProUGUI)>();
        bool _open;
        Camera _cam;
        int _sel;

        public static SkillsPanel Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("QhysicsSkillsPanel");
            _instance = go.AddComponent<SkillsPanel>();
            return _instance;
        }

        public static void Toggle() => Ensure().SetOpen(!IsOpen);
        public static void Open() => Ensure().SetOpen(true);

        void Awake()
        {
            _instance = this;
            Build();
            SkillSystem.Changed += Refresh;
        }

        void OnDestroy() { SkillSystem.Changed -= Refresh; }

        void Build()
        {
            _hud = QhysicsHudCanvas.Create("SkillsCanvas", transform, 232);
            _hud.ScreenAnchor = new Vector2(0.5f, 0.5f);
            _hud.ScreenOffset = Vector2.zero;
            _hud.WorldOffset = new Vector3(0f, 0f, 1.3f);
            _hud.WorldSize = new Vector2(1180f, 660f);
            _hud.Root.sizeDelta = new Vector2(1140f, 620f);
            var face = QhysicsUiBuilder.BorderPanel(_hud.Root, "Panel", new Vector2(1140f, 620f));

            _header = QhysicsUiBuilder.Label(face.transform, "Header", "SKILLS", QhysicsUiStyle.FontBody, QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            _header.fontStyle = FontStyles.Bold;
            _header.rectTransform.anchoredPosition = new Vector2(-150f, 270f);
            _header.rectTransform.sizeDelta = new Vector2(800f, 44f);

            var close = QhysicsUiBuilder.ChipButton(face.transform, "Close", "Close (K)", new Vector2(176f, 48f), () => SetOpen(false));
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(466f, 270f);

            string[] branches = { "MELEE", "MAGIC", "MIND" };
            for (int b = 0; b < branches.Length; b++)
            {
                float x = -368f + b * 368f;
                var bl = QhysicsUiBuilder.Label(face.transform, "Branch" + b, branches[b], QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
                bl.rectTransform.anchoredPosition = new Vector2(x, 214f);
                bl.rectTransform.sizeDelta = new Vector2(340f, 32f);
                int row = 0;
                foreach (var d in SkillSystem.Defs)
                {
                    if (d.branch != branches[b]) continue;
                    var def = d;
                    var btn = QhysicsUiBuilder.ChipButton(face.transform, "Skill_" + d.id, d.name, new Vector2(344f, 80f), () => { SkillSystem.Buy(def); });
                    btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, 158f - row * 96f);
                    var lab = btn.GetComponentInChildren<TextMeshProUGUI>();
                    lab.fontSize = QhysicsUiStyle.FontSmall;
                    lab.textWrappingMode = TextWrappingModes.Normal;
                    _chips.Add((def, btn, lab));
                    row++;
                }
            }
            var foot = QhysicsUiBuilder.Label(face.transform, "Foot", "XP: enemy 40  |  challenge 50 + 10 per star  |  parry 5.   Up/Down + Enter, or click.",
                QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.Center);
            foot.rectTransform.anchoredPosition = new Vector2(0f, -280f);
            foot.rectTransform.sizeDelta = new Vector2(1080f, 32f);
            _hud.SetActive(false);
        }

        public void SetOpen(bool on)
        {
            _open = on;
            _hud.SetActive(on);
            if (on) { Refresh(); QhysicsSfx.Play2D(SfxId.UiTick, 0.5f); }
        }

        void Refresh()
        {
            if (_header == null) return;
            _header.text = "SKILLS  |  LEVEL " + SkillSystem.Level + "  |  XP " + SkillSystem.Xp + " / " + SkillSystem.XpToNext
                + "  |  <color=#FFD24A>" + SkillSystem.Points + " POINT" + (SkillSystem.Points == 1 ? "" : "S") + "</color>";
            for (int i = 0; i < _chips.Count; i++)
            {
                var c = _chips[i];
                int r = SkillSystem.Rank(c.def.id);
                bool can = SkillSystem.CanBuy(c.def, out string why);
                string state = r >= c.def.maxRank ? "<color=#5BE673>MAX</color>" : can ? "<color=#00E5FF>+ buy</color>" : "<color=#9AA3AD>" + why + "</color>";
                c.label.text = "<b>" + c.def.name + "</b>  " + r + "/" + c.def.maxRank + "  " + state + "\n<size=80%><color=#B8C4CF>" + c.def.desc + "</color></size>";
                QhysicsUiBuilder.SetChipSelected(c.btn, i == _sel);
            }
        }

        void Update()
        {
            SkillSystem.HookChallenges();
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.kKey.wasPressedThisFrame && !QhysicsUiState.BootMenuOpen)
                SetOpen(!_open);
            if (!_open) return;
            int n = _chips.Count;
            if (n == 0) return;
            if (kb.downArrowKey.wasPressedThisFrame) { _sel = (_sel + 1) % n; Refresh(); }
            if (kb.upArrowKey.wasPressedThisFrame) { _sel = (_sel + n - 1) % n; Refresh(); }
            if (kb.rightArrowKey.wasPressedThisFrame) { _sel = Mathf.Min(n - 1, _sel + 5); Refresh(); }
            if (kb.leftArrowKey.wasPressedThisFrame) { _sel = Mathf.Max(0, _sel - 5); Refresh(); }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) SkillSystem.Buy(_chips[_sel].def);
#endif
        }

        void LateUpdate()
        {
            if (!_open) return;
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            _hud.Tick(_cam, 6f);
        }
    }
}
