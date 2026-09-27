using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityEngine.Audio;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Compact combat readout (HP / mana / focus bars, spell, held weapon, kills) that only appears in
    /// combat contexts, plus hurt flash and slow-mo tint. Same dark-glass style as the rest of the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class CombatHud : MonoBehaviour
    {
        static CombatHud _instance;

        QhysicsHudCanvas _hud;
        Image _hp, _mana, _focus, _charge;
        TextMeshProUGUI _spell, _info, _toast;
        float _toastUntil;
        Canvas _fx;
        Image _hurt, _slowmo;
        float _hurtUntil;
        Camera _cam;
        float _lastCombat = -99f;

        public static CombatHud Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("QhysicsCombatHud");
            _instance = go.AddComponent<CombatHud>();
            return _instance;
        }

        public static void Toast(string msg)
        {
            var h = Ensure();
            if (h._toast == null) return;
            h._toast.text = msg;
            h._toastUntil = Time.unscaledTime + 2.2f;
            h._lastCombat = Time.unscaledTime;
        }

        void Awake()
        {
            _instance = this;
            Build();
        }

        void OnEnable()
        {
            var v = PlayerVitality.Instance;
            if (v != null) { v.Damaged -= OnPlayerDamaged; v.Damaged += OnPlayerDamaged; }
        }

        void Build()
        {
            _hud = QhysicsHudCanvas.Create("CombatHudCanvas", transform, 150);
            _hud.ScreenAnchor = new Vector2(1f, 0f);
            _hud.ScreenOffset = new Vector2(-24f, 24f);
            _hud.WorldOffset = new Vector3(-0.42f, -0.34f, 1.0f);
            _hud.WorldSize = new Vector2(440f, 200f);
            _hud.WorldScale = 0.8f;
            _hud.Root.sizeDelta = new Vector2(400f, 176f);
            var face = QhysicsUiBuilder.Panel(_hud.Root, "Panel", QhysicsUiStyle.PanelBg, new Vector2(400f, 176f));
            face.raycastTarget = false;

            _spell = Lbl(face.transform, "Spell", new Vector2(0f, 60f), new Vector2(368f, 36f), QhysicsUiStyle.FontSmall, TextAlignmentOptions.MidlineLeft);
            _hp = Bar(face.transform, "Hp", new Vector2(0f, 24f), new Color(0.95f, 0.3f, 0.28f));
            _mana = Bar(face.transform, "Mana", new Vector2(0f, 0f), new Color(0.3f, 0.6f, 1f));
            _focus = Bar(face.transform, "Focus", new Vector2(0f, -24f), new Color(0.95f, 0.82f, 0.25f));
            _charge = Bar(face.transform, "Charge", new Vector2(0f, -42f), QhysicsUiStyle.AccentInfo, 6f);
            _info = Lbl(face.transform, "Info", new Vector2(0f, -66f), new Vector2(368f, 30f), 18f, TextAlignmentOptions.MidlineLeft);
            _info.color = QhysicsUiStyle.TextMuted;
            _toast = Lbl(_hud.Root, "Toast", new Vector2(0f, 112f), new Vector2(400f, 34f), QhysicsUiStyle.FontSmall, TextAlignmentOptions.MidlineRight);
            _toast.color = QhysicsUiStyle.AccentAttention;
            _hud.SetActive(false);

            // Full-screen effects (desktop only).
            var fxGo = new GameObject("CombatFxCanvas", typeof(RectTransform));
            fxGo.transform.SetParent(transform, false);
            _fx = fxGo.AddComponent<Canvas>();
            _fx.renderMode = RenderMode.ScreenSpaceOverlay;
            _fx.sortingOrder = 140;
            _hurt = FullImage(fxGo.transform, "Hurt", new Color(0.8f, 0.05f, 0.05f, 0f));
            _slowmo = FullImage(fxGo.transform, "SlowMo", new Color(0f, 0.55f, 0.65f, 0f));
        }

        static Image FullImage(Transform p, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(p, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Lbl(Transform p, string name, Vector2 pos, Vector2 size, float font, TextAlignmentOptions al)
        {
            var t = QhysicsUiBuilder.Label(p, name, "", font, QhysicsUiStyle.TextPrimary, al);
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = size;
            t.raycastTarget = false;
            return t;
        }

        static Image Bar(Transform p, string name, Vector2 pos, Color c, float h = 14f)
        {
            var bg = QhysicsUiBuilder.Panel(p, name + "Bg", new Color(1f, 1f, 1f, 0.08f), new Vector2(368f, h));
            bg.rectTransform.anchoredPosition = pos;
            bg.raycastTarget = false;
            var fill = QhysicsUiBuilder.Panel(bg.transform, name, c, new Vector2(368f, h));
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.raycastTarget = false;
            return fill;
        }

        static void SetFill(Image fill, float f)
        {
            fill.rectTransform.sizeDelta = new Vector2(368f * Mathf.Clamp01(f), fill.rectTransform.sizeDelta.y);
        }

        void OnPlayerDamaged(float amount)
        {
            _hurtUntil = Time.unscaledTime + 0.35f;
            _lastCombat = Time.unscaledTime;
            QhysicsSfx.Play2D(SfxId.PlayerHurt, 0.8f);
            CombatHaptics.Both(Mathf.Clamp01(0.4f + amount / 30f), 0.15f);
        }

        void Update()
        {
            var v = PlayerVitality.Instance;
            if (v != null) { v.Damaged -= OnPlayerDamaged; v.Damaged += OnPlayerDamaged; }
        }

        void LateUpdate()
        {
            bool combat = SpellSystem.CombatModeActive() || FocusTime.Active || SpellSystem.Charging || ManaPool.Mana01 < 0.999f;
            if (combat) _lastCombat = Time.unscaledTime;
            bool show = QhysicsUiState.GameplayHudVisible && !CombatInputGate.Blocked && Time.unscaledTime - _lastCombat < 4f;
            _hud.SetActive(show);

            // FX
            bool desktop = !CombatInputGate.IsXr;
            float hurtA = Time.unscaledTime < _hurtUntil ? (_hurtUntil - Time.unscaledTime) / 0.35f * 0.35f : 0f;
            _hurt.color = new Color(0.8f, 0.05f, 0.05f, desktop ? hurtA : 0f);
            _slowmo.color = new Color(0f, 0.55f, 0.65f, desktop && FocusTime.Active ? 0.12f : 0f);

            if (!show) return;
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = QhysicsUiBuilder.ResolveXrCamera();
            _hud.Tick(_cam, 8f);

            SetFill(_hp, v01());
            SetFill(_mana, ManaPool.Mana01);
            SetFill(_focus, FocusTime.Meter01);
            SetFill(_charge, SpellSystem.Charge01);
            var sc = SpellSystem.SpellColor(SpellSystem.Current);
            string key = CombatInputGate.IsXr ? "L-stick click" : "Z";
            _spell.text = "<color=#" + ColorUtility.ToHtmlStringRGB(sc) + ">" + SpellSystem.SpellName(SpellSystem.Current).ToUpperInvariant()
                + "</color>  <size=80%><color=#9AA3AD>[" + key + "]</color></size>"
                + (FocusTime.Active ? "   <color=#F2D140>FOCUS</color>" : "");
            var held = PhysicsHands.Instance != null ? PhysicsHands.Instance.AnyHeld : null;
            string heldTxt = held != null ? held.displayName + (held.Imbue != Element.None ? " (" + held.Imbue + ")" : "") : "empty hands";
            int kills = EnemyDirector.Instance != null ? EnemyDirector.Instance.Kills : 0;
            _info.text = "HP  MANA  FOCUS   |   " + heldTxt + "   |   KO " + kills + "   |   LV " + SkillSystem.Level;
            if (_toast != null)
                _toast.gameObject.SetActive(Time.unscaledTime < _toastUntil);
        }

        static float v01()
        {
            var v = PlayerVitality.Instance;
            return v != null ? v.Hp01 : 1f;
        }
    }
}
