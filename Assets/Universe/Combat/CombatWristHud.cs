using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.Combat
{
    /// <summary>
    /// XR wrist HUD on the back of the LEFT wrist (like a watch): HP, mana, current spell (+ focus), level / XP bar.
    /// Fades in when the back of the wrist is turned toward your face (look at your watch), hidden otherwise.
    /// XR only; desktop keeps the screen-space CombatHud. Display-only canvas (no raycaster, never eats ray clicks).
    /// Pose: OpenXR grip (Unity frame: +Z forward, +Y thumb/top, +X = left palm normal), converted to world space.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(211)]
    public sealed class CombatWristHud : MonoBehaviour
    {
        // Panel ~13 x 7.5 cm at the wrist.
        const float PxW = 520f, PxH = 300f;
        const float Scale = 0.00025f;
        static readonly Vector3 WristOffset = new Vector3(-0.045f, -0.012f, -0.105f); // back of hand side, toward the forearm

        static CombatWristHud _instance;

        Canvas _canvas;
        CanvasGroup _group;
        Image _hp, _mana, _xp;
        TextMeshProUGUI _hpTxt, _manaTxt, _spell, _xpTxt;
        Camera _cam;
        float _alpha;
        float _nextText;

        public static bool Visible => _instance != null && _instance._alpha > 0.05f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var go = new GameObject("QhysicsCombatWristHud");
            _instance = go.AddComponent<CombatWristHud>();
        }

        void Awake()
        {
            _instance = this;
            Build();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void Build()
        {
            var go = new GameObject("WristCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 60;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(PxW, PxH);
            go.transform.localScale = Vector3.one * Scale;
            _group = go.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Image face = QhysicsUiBuilder.Panel(go.transform, "Panel", QhysicsUiStyle.PanelBg, new Vector2(PxW, PxH));
            face.raycastTarget = false;
            Image rim = QhysicsUiBuilder.Panel(face.transform, "Rim", QhysicsUiStyle.AccentInfo, new Vector2(PxW - 40f, 3f));
            rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rim.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            rim.raycastTarget = false;

            _spell = Txt(face.transform, "Spell", new Vector2(0f, 104f), new Vector2(480f, 50f), 36f, TextAlignmentOptions.Center);
            _hpTxt = Txt(face.transform, "HpTxt", new Vector2(0f, 60f), new Vector2(480f, 30f), 24f, TextAlignmentOptions.MidlineLeft);
            _hp = Bar(face.transform, "Hp", new Vector2(0f, 34f), new Color(0.95f, 0.3f, 0.28f), 20f);
            _manaTxt = Txt(face.transform, "ManaTxt", new Vector2(0f, 2f), new Vector2(480f, 30f), 24f, TextAlignmentOptions.MidlineLeft);
            _mana = Bar(face.transform, "Mana", new Vector2(0f, -24f), new Color(0.3f, 0.6f, 1f), 20f);
            _xpTxt = Txt(face.transform, "XpTxt", new Vector2(0f, -62f), new Vector2(480f, 30f), 24f, TextAlignmentOptions.MidlineLeft);
            _xp = Bar(face.transform, "Xp", new Vector2(0f, -88f), new Color(0.95f, 0.82f, 0.25f), 14f);
            _hpTxt.color = _manaTxt.color = _xpTxt.color = QhysicsUiStyle.TextMuted;
            go.SetActive(false);
        }

        static TextMeshProUGUI Txt(Transform p, string name, Vector2 pos, Vector2 size, float font, TextAlignmentOptions al)
        {
            TextMeshProUGUI t = QhysicsUiBuilder.Label(p, name, "", font, QhysicsUiStyle.TextPrimary, al);
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = size;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static Image Bar(Transform p, string name, Vector2 pos, Color c, float h)
        {
            Image bg = QhysicsUiBuilder.Panel(p, name + "Bg", new Color(1f, 1f, 1f, 0.1f), new Vector2(480f, h));
            bg.rectTransform.anchoredPosition = pos;
            bg.raycastTarget = false;
            Image fill = QhysicsUiBuilder.Panel(bg.transform, name, c, new Vector2(480f, h));
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.raycastTarget = false;
            return fill;
        }

        static void SetFill(Image fill, float f)
        {
            fill.rectTransform.sizeDelta = new Vector2(480f * Mathf.Clamp01(f), fill.rectTransform.sizeDelta.y);
        }

        void LateUpdate()
        {
            bool allowed = CombatInputGate.IsXr && QhysicsUiState.GameplayHudVisible && !QhysicsPausePanel.AnyOpen;
            float target = 0f;
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            if (allowed && XrTrackingSpace.TryGetWorldPose(XRNode.LeftHand, out Vector3 gp, out Quaternion gr))
            {
                if (_cam == null || !_cam.isActiveAndEnabled)
                    _cam = QhysicsUiBuilder.ResolveXrCamera();
                pos = gp + gr * WristOffset;
                // Canvas forward points away from the viewer (+X = into the palm side), up = controller -Y
                // so the text reads elbow -> fingers when you raise the wrist across your chest.
                rot = Quaternion.LookRotation(gr * Vector3.right, gr * Vector3.down);
                if (_cam != null)
                {
                    Vector3 toHead = _cam.transform.position - pos;
                    float dist = toHead.magnitude;
                    Vector3 facing = gr * Vector3.left; // back-of-wrist normal
                    float d = dist > 1e-4f ? Vector3.Dot(facing, toHead / dist) : 0f;
                    target = dist < 0.8f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.75f, d)) : 0f;
                }
            }
            _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime * 6f);
            bool on = _alpha > 0.01f;
            if (_canvas.gameObject.activeSelf != on)
                _canvas.gameObject.SetActive(on);
            if (!on)
                return;
            _canvas.transform.SetPositionAndRotation(pos, rot);
            _group.alpha = _alpha;
            if (_cam != null && _canvas.worldCamera != _cam)
                _canvas.worldCamera = _cam;

            var v = PlayerVitality.Instance;
            float hp01 = v != null ? v.Hp01 : 1f;
            SetFill(_hp, hp01);
            SetFill(_mana, ManaPool.Mana01);
            int next = Mathf.Max(1, SkillSystem.XpToNext);
            SetFill(_xp, (float)SkillSystem.Xp / next);
            if (Time.unscaledTime >= _nextText)
            {
                _nextText = Time.unscaledTime + 0.1f;
                Color sc = SpellSystem.SpellColor(SpellSystem.Current);
                _spell.text = "<color=#" + ColorUtility.ToHtmlStringRGB(sc) + ">" + SpellSystem.SpellName(SpellSystem.Current).ToUpperInvariant()
                    + "</color>" + (FocusTime.Active ? "  <color=#F2D140>FOCUS</color>" : "");
                _hpTxt.text = v != null ? "HP  " + Mathf.CeilToInt(v.Hp) + " / " + Mathf.CeilToInt(v.MaxHp) : "HP";
                _manaTxt.text = "MANA  " + Mathf.RoundToInt(ManaPool.Current) + "   <size=80%>focus " + Mathf.RoundToInt(FocusTime.Meter01 * 100f) + "%</size>";
                _xpTxt.text = "LV " + SkillSystem.Level + "   XP " + SkillSystem.Xp + " / " + next
                    + (SkillSystem.Points > 0 ? "   <color=#59E673>+" + SkillSystem.Points + " pts</color>" : "");
            }
        }
    }
}
