using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealityEngine.Player;
using RealityEngine.Experiments;
using RealityEngine.Challenges;
using RealityEngine.Stations;

using UiButton = UnityEngine.UI.Button;

namespace RealityEngine.UI
{
    /// <summary>
    /// ONE slim top status strip replacing the old separate HUD pieces:
    /// training vitals bar (operator + HP), experiment card ("QHYSICS / What happens if..."), sim-speed chip,
    /// and challenge progress. Desktop: screen-space top-centre. XR: world-space above the view.
    /// Hidden while the boot menu is open.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(207)]
    public sealed class QhysicsStatusStrip : MonoBehaviour
    {
        public const string RootName = "QhysicsStatusStrip";
        const float Height = 48f;

        QhysicsHudCanvas _hud;
        Image _bg;
        Image _dot;
        TextMeshProUGUI _op;
        Image _hpBack;
        Image _hpFill;
        TextMeshProUGUI _hp;
        TextMeshProUGUI _context;
        UiButton _speedBtn;
        TextMeshProUGUI _speed;
        Camera _cam;
        float _nextRefresh;
        PlayerVitality _vitals;
        ExperimentRunner _runner;

        public static QhysicsStatusStrip Ensure(Transform parent)
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<QhysicsStatusStrip>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<QhysicsStatusStrip>();
        }

        void Awake()
        {
            Build();
        }

        void Build()
        {
            _hud = QhysicsHudCanvas.Create("StatusCanvas", transform, 196);
            _hud.ScreenAnchor = new Vector2(0.5f, 1f);
            _hud.ScreenOffset = new Vector2(0f, -QhysicsUiStyle.Space2);
            _hud.WorldOffset = new Vector3(0f, 0.30f, 1.15f);
            _hud.WorldSize = new Vector2(1400f, 80f);

            RectTransform root = _hud.Root;
            _bg = root.gameObject.AddComponent<Image>();
            _bg.sprite = QhysicsUiStyle.RoundedSprite;
            _bg.type = Image.Type.Sliced;
            _bg.color = QhysicsUiStyle.PanelBg;
            _bg.material = QhysicsUiStyle.UiMaterial;
            _bg.raycastTarget = false;
            var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = QhysicsUiStyle.Space2;
            h.padding = new RectOffset(16, 8, 6, 6);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            root.sizeDelta = new Vector2(900f, Height);

            _dot = Fixed(QhysicsUiBuilder.Panel(root, "Dot", QhysicsUiStyle.AccentActive, new Vector2(12f, 12f)), 12f, 12f);
            var brand = QhysicsUiBuilder.Label(root, "Brand", "QHYSICS", QhysicsUiStyle.FontSmall, QhysicsUiStyle.AccentInfo,
                TextAlignmentOptions.MidlineLeft);
            brand.fontStyle = FontStyles.Bold;
            Sep(root);
            _op = QhysicsUiBuilder.Label(root, "Operator", "Operator", QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextPrimary,
                TextAlignmentOptions.MidlineLeft);

            _hpBack = Fixed(QhysicsUiBuilder.Panel(root, "HpBack", QhysicsUiStyle.ChipBg, new Vector2(120f, 10f)), 120f, 10f);
            _hpFill = QhysicsUiBuilder.Panel(_hpBack.transform, "HpFill", QhysicsUiStyle.AccentActive, new Vector2(120f, 10f));
            _hpFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            _hpFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            _hpFill.rectTransform.offsetMin = Vector2.zero;
            _hpFill.rectTransform.offsetMax = Vector2.zero;
            _hpFill.raycastTarget = false;
            _hp = QhysicsUiBuilder.Label(root, "Hp", "HP 100", QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted,
                TextAlignmentOptions.MidlineLeft);
            Sep(root);
            _context = QhysicsUiBuilder.Label(root, "Context", "Induction Lab", QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextPrimary,
                TextAlignmentOptions.MidlineLeft);
            Sep(root);
            _speedBtn = QhysicsUiBuilder.ChipButton(root, "Speed", "1x", new Vector2(72f, 36f), ToggleSimPanel);
            var le = _speedBtn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 72f;
            le.preferredHeight = 36f;
            _speed = _speedBtn.GetComponentInChildren<TextMeshProUGUI>();
            _speed.fontSize = QhysicsUiStyle.FontSmall;

            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                t.overflowMode = TextOverflowModes.Overflow;
            _hud.SetActive(false);
        }

        static Image Fixed(Image img, float w, float h)
        {
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minWidth = w;
            img.raycastTarget = false;
            return img;
        }

        static void Sep(RectTransform root)
        {
            Image s = QhysicsUiBuilder.Separator(root, 24f);
            var le = s.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 2f;
            le.preferredHeight = 24f;
        }

        void ToggleSimPanel()
        {
            var chip = UnityEngine.Object.FindAnyObjectByType<QhysicsSimChip>(FindObjectsInactive.Include);
            if (chip != null)
                chip.ToggleExpand();
        }

        void LateUpdate()
        {
            bool show = QhysicsUiState.GameplayHudVisible && Application.isPlaying;
            _hud.SetActive(show);
            if (!show)
                return;
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            _hud.Tick(_cam);

            ApplyHurtFlash();
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        void ApplyHurtFlash()
        {
            if (_vitals == null || _hpFill == null)
                return;
            if (_vitals.IsFlashing)
            {
                float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 26f));
                _hpFill.color = Color.Lerp(QhysicsUiStyle.AccentError, Color.white, pulse * 0.4f);
                _bg.color = Color.Lerp(QhysicsUiStyle.PanelBg, new Color(0.45f, 0.06f, 0.06f, 0.9f), 0.5f);
            }
            else
            {
                _bg.color = QhysicsUiStyle.PanelBg;
            }
        }

        void Refresh()
        {
            if (_vitals == null)
                _vitals = PlayerVitality.Instance;
            var op = PlayerOperatorController.Instance;
            _op.text = op != null ? op.Current.DisplayName : "Operator";

            if (_vitals != null)
            {
                float hp01 = Mathf.Clamp01(_vitals.Hp01);
                _hpFill.rectTransform.anchorMax = new Vector2(hp01, 1f);
                if (!_vitals.IsFlashing)
                    _hpFill.color = _vitals.HasShield ? new Color(0.4f, 0.7f, 1f)
                        : (hp01 < 0.3f ? QhysicsUiStyle.AccentError : QhysicsUiStyle.AccentActive);
                _hp.text = "HP " + Mathf.CeilToInt(_vitals.Hp) + (_vitals.HasShield ? "  Shield" : "");
            }

            // Context: active challenge > experiment short name.
            string ctx;
            ChallengeManager cm = ChallengeManager.Instance;
            if (cm != null && cm.IsChallengeActive && cm.ActiveChallenge != null)
            {
                int done = 0, total = cm.ActiveObjectives != null ? cm.ActiveObjectives.Length : 0;
                for (int i = 0; i < total; i++)
                    if (cm.ActiveObjectives[i] != null && cm.ActiveObjectives[i].completed)
                        done++;
                ctx = "Challenge: " + cm.ActiveChallenge.title + "  " + done + "/" + total + "  " + cm.ElapsedTime.ToString("0") + "s";
            }
            else
            {
                ctx = ExperimentShortName();
                // Append nearest lab station within ~4 m when idle (no active challenge).
                LabStationHub hub = LabStationHub.Instance;
                if (hub != null)
                {
                    if (_cam == null || !_cam.isActiveAndEnabled)
                        _cam = QhysicsUiBuilder.ResolveXrCamera();
                    Camera cam = _cam != null ? _cam : Camera.main;
                    if (cam != null)
                    {
                        LabStation nearest = hub.NearestStation(cam.transform.position);
                        if (nearest != null && nearest.WorldAnchor != null)
                        {
                            float dist = Vector3.Distance(cam.transform.position, nearest.WorldAnchor.position);
                            if (dist <= 4f && !string.IsNullOrEmpty(nearest.DisplayName))
                                ctx = ctx + " \u00B7 " + nearest.DisplayName;
                        }
                    }
                }
            }
            _context.text = ctx;

            bool running = Time.timeScale > 0.01f;
            _dot.color = running ? QhysicsUiStyle.AccentActive : QhysicsUiStyle.AccentAttention;
            _speed.text = running ? (Time.timeScale.ToString("0.##") + "x") : "II";
        }

        string ExperimentShortName()
        {
            if (_runner == null)
                _runner = UnityEngine.Object.FindFirstObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
            if (_runner == null || _runner.Definition == null)
                return "Sandbox";
            string id = _runner.Definition.id;
            if (string.IsNullOrEmpty(id))
                return "Induction Lab";
            // "faraday_induction" -> "Faraday Induction" (short, never the long question text).
            var parts = id.Replace('-', '_').Split('_');
            var sb = new System.Text.StringBuilder(24);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    continue;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(char.ToUpperInvariant(parts[i][0]));
                if (parts[i].Length > 1)
                    sb.Append(parts[i].Substring(1));
            }
            string s = sb.ToString();
            return s.Length > 24 ? "Induction Lab" : s;
        }
    }
}
