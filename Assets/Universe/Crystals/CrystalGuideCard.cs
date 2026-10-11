using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RealityEngine.Audio;
using RealityEngine.Challenges;
using RealityEngine.UI;

using UiButton = UnityEngine.UI.Button;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// World-space guide card behind each Crystal Lab bench (dark glass + cyan, QhysicsUiBuilder styling).
    /// Progressive disclosure: WHAT (plain language) is shown first; MATH (equations, constants, sources),
    /// TRY THIS (steps) and LATTICE (what the ions do; highlights the unit-cell model) are one tap away.
    /// CHALLENGE starts the station's starred challenge. A live line shows the simulation numbers and up to
    /// two rows of control chips drive the station (ray-clickable in the headset, mouse on desktop).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrystalGuideCard : MonoBehaviour
    {
        public sealed class Content
        {
            public string Title;
            public string What;
            public string Math;
            public string TryThis;
            public string Lattice;
            public string ChallengeId;
        }

        static readonly string[] Pages = { "WHAT", "MATH", "TRY THIS", "LATTICE", "CHALLENGE" };
        const float W = 940f, H = 700f;
        /// <summary>Card is ~1.3 m wide (1.35 x the shared 1 mm/unit canvas scale) so 15-22 pt body text stays readable from the stand point ~1.6 m away in the headset.</summary>
        const float VrScale = 1.35f;

        Canvas _canvas;
        TextMeshProUGUI _title, _status, _body, _live;
        UiButton[] _tabs;
        RectTransform _row1, _row2;
        int _row1Count, _row2Count;
        Content _content;
        Func<string> _liveProvider;
        float _nextLive;
        int _page;
        public Action<int> PageChanged;
        public int Page => _page;

        public static CrystalGuideCard Create(Transform parent, Vector3 lp, Quaternion lr, Content c)
        {
            var go = new GameObject("GuideCard");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localRotation = lr;
            var card = go.AddComponent<CrystalGuideCard>();
            card._content = c;
            card.Build();
            return card;
        }

        void Build()
        {
            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(W + 20f, H + 20f));
            _canvas.sortingOrder = 40;
            _canvas.transform.localScale = Vector3.one * (QhysicsUiStyle.CanvasScale * VrScale);
            QhysicsUiBuilder.WireEventCamera(_canvas);
            Image face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(W, H));

            _title = QhysicsUiBuilder.Label(face.transform, "Title", _content.Title, 36f, QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            Place(_title.rectTransform, 0f, 305f, W - 60f, 50f);

            _status = QhysicsUiBuilder.Label(face.transform, "Status", "", QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineLeft);
            Place(_status.rectTransform, 0f, 265f, W - 60f, 28f);

            var tabRow = NewRow(face.transform, "Tabs", 213f, 60f);
            _tabs = new UiButton[Pages.Length];
            for (int i = 0; i < Pages.Length; i++)
            {
                int idx = i;
                _tabs[i] = QhysicsUiBuilder.ChipButton(tabRow, "Tab_" + Pages[i], Pages[i], new Vector2(164f, 52f), () => OnTab(idx));
            }

            _body = QhysicsUiBuilder.Label(face.transform, "Body", "", 22f, QhysicsUiStyle.TextPrimary, TextAlignmentOptions.TopLeft);
            Place(_body.rectTransform, 0f, 18f, W - 60f, 320f);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Truncate;
            _body.enableAutoSizing = true;
            _body.fontSizeMin = 15f;
            _body.fontSizeMax = 22f;
            _body.richText = true;

            _live = QhysicsUiBuilder.Label(face.transform, "Live", "", 21f, QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            Place(_live.rectTransform, 0f, -178f, W - 60f, 58f);
            _live.textWrappingMode = TextWrappingModes.Normal;
            _live.enableAutoSizing = true;
            _live.fontSizeMin = 14f;
            _live.fontSizeMax = 21f;
            _live.richText = true;

            _row1 = NewRow(face.transform, "Controls1", -243f, 60f);
            _row2 = NewRow(face.transform, "Controls2", -307f, 60f);

            ShowPage(0);
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        RectTransform NewRow(Transform parent, string name, float y, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(W - 20f, h);
            QhysicsUiBuilder.LayoutHorizontal(rt, QhysicsUiStyle.Space1);
            return rt;
        }

        /// <summary>Add a control chip (6 per row, two rows).</summary>
        public UiButton AddControl(string label, Action onClick)
        {
            RectTransform row = _row1Count < 6 ? _row1 : _row2;
            if (row == _row1) _row1Count++; else _row2Count++;
            if (_row2Count > 6)
                return null;
            return QhysicsUiBuilder.ChipButton(row, "Ctl_" + label, label, new Vector2(140f, 52f), () =>
            {
                QhysicsSfx.Play2D(SfxId.UiClick, 0.45f);
                if (onClick != null)
                    onClick();
            });
        }

        /// <summary>Change a control chip's caption (e.g. Gas: ON / OFF).</summary>
        public static void SetChipLabel(UiButton chip, string label)
        {
            if (chip == null)
                return;
            var t = chip.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null)
                t.text = label;
        }

        public void SetLive(Func<string> provider)
        {
            _liveProvider = provider;
        }

        void OnTab(int idx)
        {
            QhysicsSfx.Play2D(SfxId.UiClick, 0.45f);
            if (idx == 4)
                StartChallenge();
            ShowPage(idx);
        }

        public void ShowPage(int idx)
        {
            _page = Mathf.Clamp(idx, 0, Pages.Length - 1);
            for (int i = 0; i < _tabs.Length; i++)
                QhysicsUiBuilder.SetChipSelected(_tabs[i], i == _page);
            switch (_page)
            {
                case 0: _body.text = _content.What; break;
                case 1: _body.text = _content.Math; break;
                case 2: _body.text = _content.TryThis; break;
                case 3: _body.text = _content.Lattice; break;
                default: _body.text = ChallengeText(); break;
            }
            if (PageChanged != null)
                PageChanged(_page);
        }

        void StartChallenge()
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            if (mgr == null || string.IsNullOrEmpty(_content.ChallengeId))
                return;
            if (mgr.IsChallengeActive && mgr.ActiveChallenge != null && mgr.ActiveChallenge.id == _content.ChallengeId)
                return;
            if (mgr.IsChallengeActive)
                mgr.AbandonChallenge();
            bool ok = mgr.StartChallenge(_content.ChallengeId);
            QhysicsSfx.Play2D(ok ? SfxId.Unlock : SfxId.Denied, 0.5f);
        }

        string ChallengeText()
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            ChallengeDefinition def = mgr != null ? mgr.FindChallenge(_content.ChallengeId) : null;
            if (def == null)
                return "Challenge unavailable (ChallengeManager not running).";
            var sb = new System.Text.StringBuilder(256);
            sb.Append("<b>").Append(def.title).Append("</b>   ").Append(Stars(mgr.GetStars(def.id))).Append('\n');
            sb.Append(def.description).Append("\n\n");
            if (def.objectives != null)
            {
                for (int i = 0; i < def.objectives.Length; i++)
                    sb.Append("- ").Append(def.objectives[i].displayText).Append('\n');
            }
            if (def.starThresholds != null)
                sb.Append("\nStars: finish = *, under ").Append(def.starThresholds.twoStarTimeSeconds.ToString("0"))
                    .Append(" s = **, under ").Append(def.starThresholds.threeStarTimeSeconds.ToString("0")).Append(" s = ***");
            if (!string.IsNullOrEmpty(def.mentorHint))
                sb.Append("\n<color=#9FB4C4>Hint: ").Append(def.mentorHint).Append("</color>");
            bool active = mgr.IsChallengeActive && mgr.ActiveChallenge == def;
            sb.Append(active ? "\n\n<color=#59E673>RUNNING - the objective panel tracks it.</color>" : "\n\n(Tap CHALLENGE again to restart it.)");
            return sb.ToString();
        }

        static string Stars(int n)
        {
            n = Mathf.Clamp(n, 0, 3);
            return "<color=#F2D140>" + new string('*', n) + "</color><color=#5A636D>" + new string('*', 3 - n) + "</color>";
        }

        void Update()
        {
            if (Time.unscaledTime < _nextLive)
                return;
            _nextLive = Time.unscaledTime + 0.12f;
            if (_liveProvider != null && _live != null)
            {
                string s;
                try { s = _liveProvider(); }
                catch (Exception e) { s = "live readout error: " + e.Message; }
                _live.text = s;
            }
            if (_status != null)
            {
                ChallengeManager mgr = ChallengeManager.Instance;
                int stars = mgr != null && !string.IsNullOrEmpty(_content.ChallengeId) ? mgr.GetStars(_content.ChallengeId) : 0;
                bool running = mgr != null && mgr.IsChallengeActive && mgr.ActiveChallenge != null && mgr.ActiveChallenge.id == _content.ChallengeId;
                _status.text = "CRYSTAL LAB   |   challenge " + Stars(stars) + (running ? "   <color=#59E673>running</color>" : "")
                    + "   |   ray + trigger to tap chips";
            }
            if (_page == 4 && _body != null && Time.frameCount % 30 == 0)
                _body.text = ChallengeText();
        }
    }
}
