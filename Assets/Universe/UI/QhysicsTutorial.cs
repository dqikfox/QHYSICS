using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using TMPro;
using RealityEngine.Audio;
using RealityEngine.Challenges;
using RealityEngine.Player;
using RealityEngine.XR;

namespace RealityEngine.UI
{
    /// <summary>
    /// First-time interactive tutorial during Chapter 1 Level 1 (Bench Orientation): step prompts that tick
    /// themselves off when you actually do the thing. 1 Move (XR teleport or walk / desktop walk), 2 Toolbelt (open it),
    /// 3 Grab (pick anything up), 4 First circuit (light a bulb). Steps latch in any order; the card shows the first
    /// open one. Done once per install (PlayerPrefs); toolbelt WORLD > Tutorial replays it. Non-blocking card:
    /// desktop right side, XR world-space right of view.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(209)]
    public sealed class QhysicsTutorial : MonoBehaviour
    {
        const string PrefDone = "QHYSICS.Tutorial.Done";
        const string LevelId = "bench_orientation";

        static QhysicsTutorial _instance;

        QhysicsHudCanvas _hud;
        CanvasGroup _group;
        TextMeshProUGUI _kicker, _title, _body, _checks;
        Camera _cam;
        bool _running;
        readonly bool[] _done = new bool[4];
        int _shownStep = -1;
        float _stepFlashUntil;
        Vector3 _lastOrigin;
        bool _haveOrigin;
        float _walked;
        float _nextPoll;
        XRBaseInteractor[] _interactors;
        float _nextInteractorRefresh;
        DesktopInteractor _desk;
        float _finishAt = -1f;

        static readonly string[] Titles = { "Move", "Open the toolbelt", "Grab something", "Build your first circuit" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var go = new GameObject("QhysicsTutorial");
            _instance = go.AddComponent<QhysicsTutorial>();
        }

        /// <summary>Replay the tutorial now (toolbelt WORLD > Tutorial).</summary>
        public static void Restart()
        {
            if (_instance == null)
                Boot();
            if (_instance != null)
                _instance.Begin();
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
            _hud = QhysicsHudCanvas.Create("TutorialCanvas", transform, 204);
            _hud.ScreenAnchor = new Vector2(1f, 0.62f);
            _hud.ScreenOffset = new Vector2(-24f, 0f);
            _hud.WorldOffset = new Vector3(0.34f, -0.1f, 0.95f);
            _hud.WorldSize = new Vector2(560f, 300f);
            _hud.WorldScale = 0.85f;
            _group = _hud.Canvas.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Vector2 size = new Vector2(520f, 270f);
            _hud.Root.sizeDelta = size;
            Image bg = QhysicsUiBuilder.Panel(_hud.Root, "Card", QhysicsUiStyle.PanelBg, size);
            bg.raycastTarget = false;
            _kicker = Line(bg.transform, "Kicker", 20f, QhysicsUiStyle.AccentInfo, 106f, 28f);
            _kicker.characterSpacing = 4f;
            _title = Line(bg.transform, "Title", 32f, QhysicsUiStyle.TextPrimary, 70f, 40f);
            _title.fontStyle = FontStyles.Bold;
            _body = Line(bg.transform, "Body", QhysicsUiStyle.FontSmall, QhysicsUiStyle.TextPrimary, 4f, 84f);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _checks = Line(bg.transform, "Checks", 20f, QhysicsUiStyle.TextMuted, -90f, 56f);
            _checks.textWrappingMode = TextWrappingModes.Normal;
            _hud.SetActive(false);
        }

        static TextMeshProUGUI Line(Transform parent, string name, float size, Color color, float y, float h)
        {
            TextMeshProUGUI t = QhysicsUiBuilder.Label(parent, name, "", size, color, TextAlignmentOptions.MidlineLeft);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(480f, h);
            t.rectTransform.anchoredPosition = new Vector2(0f, y);
            return t;
        }

        void Begin()
        {
            for (int i = 0; i < _done.Length; i++)
                _done[i] = false;
            _running = true;
            _shownStep = -1;
            _walked = 0f;
            _haveOrigin = false;
            _finishAt = -1f;
            _hud.SetActive(true);
        }

        void Update()
        {
            if (!_running)
            {
                // Auto-start the first time Level 1 is active.
                if (PlayerPrefs.GetInt(PrefDone, 0) == 0 && Time.unscaledTime >= _nextPoll)
                {
                    _nextPoll = Time.unscaledTime + 0.5f;
                    ChallengeManager mgr = ChallengeManager.Instance;
                    if (mgr != null && mgr.ActiveChallenge != null && mgr.ActiveChallenge.id == LevelId && !QhysicsUiState.BootMenuOpen)
                        Begin();
                }
                return;
            }
            if (_finishAt > 0f)
            {
                if (Time.unscaledTime >= _finishAt)
                {
                    _running = false;
                    _hud.SetActive(false);
                }
                return;
            }
            Detect();
            int step = FirstOpen();
            if (step < 0)
            {
                PlayerPrefs.SetInt(PrefDone, 1);
                PlayerPrefs.Save();
                QhysicsSfx.Play2D(SfxId.ChallengeComplete, 0.6f);
                _kicker.text = "TUTORIAL COMPLETE";
                _title.text = "You're ready";
                _body.text = "Finish the level goals in the challenge panel. " + (QhysicsUiState.IsXr ? "Toolbelt: Menu / B / Y." : "C opens the challenge list.");
                _checks.text = ChecksLine();
                _finishAt = Time.unscaledTime + 5f;
                return;
            }
            if (step != _shownStep)
            {
                if (_shownStep >= 0)
                {
                    QhysicsSfx.Play2D(SfxId.ObjectiveDone, 0.5f);
                    _stepFlashUntil = Time.unscaledTime + 0.4f;
                }
                _shownStep = step;
                _kicker.text = "TUTORIAL  |  STEP " + (step + 1) + " / 4";
                _title.text = Titles[step];
                _body.text = Instruction(step, QhysicsUiState.IsXr);
                _checks.text = ChecksLine();
            }
        }

        int FirstOpen()
        {
            for (int i = 0; i < _done.Length; i++)
                if (!_done[i]) return i;
            return -1;
        }

        string ChecksLine()
        {
            var sb = new System.Text.StringBuilder(128);
            for (int i = 0; i < _done.Length; i++)
            {
                sb.Append(_done[i] ? "<color=#59E673>[x]</color> " : "[ ] ").Append(Titles[i]);
                if (i < _done.Length - 1) sb.Append("   ");
            }
            return sb.ToString();
        }

        static string Instruction(int step, bool xr)
        {
            switch (step)
            {
                case 0: return xr ? "Hold A / X, aim the teleport arc at the floor and release. (Left stick walks.)"
                                  : "Walk with WASD, look with the mouse. Take a few steps.";
                case 1: return xr ? "Press Menu / B / Y to open the toolbelt at your hip."
                                  : "Press M or Tab to open the toolbelt.";
                case 2: return xr ? "Reach an object (or spawn one from a toolbelt tab) and squeeze grip to grab it."
                                  : "Spawn a gadget from the toolbelt, look at it and press E to grab it.";
                default: return xr ? "BUILD tab: place a Battery and a Bulb, connect them with Wire. Light the bulb."
                                   : "BUILD tab: place a Battery and a Bulb, connect them with Wire. Light the bulb.";
            }
        }

        void Detect()
        {
            // 0 Move: XR teleport = a jump of the rig > 0.8 m in one frame; desktop = walk 3 m.
            GameObject origin = GameObject.Find(LabPlayerSpawn.OriginName);
            if (origin != null)
            {
                Vector3 p = origin.transform.position;
                if (_haveOrigin)
                {
                    Vector3 d = p - _lastOrigin;
                    d.y = 0f;
                    float m = d.magnitude;
                    if (m > 0.8f && m < 60f) _done[0] = true;       // teleport / snap move
                    else if (m < 0.8f) _walked += m;
                    if (_walked > 3f) _done[0] = true;
                }
                _lastOrigin = p;
                _haveOrigin = true;
            }
            // 1 Toolbelt open.
            if (QhysicsXrRayPolicy.ToolbeltVisible()) _done[1] = true;
            // 2 Grab: XR interactor selection (not a teleport target), desktop interactor, or a held combat weapon.
            if (!_done[2] && Grabbing()) _done[2] = true;
            // 3 First circuit: any bulb carrying current.
            if (!_done[3] && Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + 0.5f;
                Bulb[] bulbs = UnityEngine.Object.FindObjectsByType<Bulb>(FindObjectsInactive.Exclude);
                for (int i = 0; i < bulbs.Length; i++)
                {
                    if (bulbs[i] != null && CircuitReader.IsCurrentSignificant(bulbs[i])) { _done[3] = true; break; }
                }
            }
        }

        bool Grabbing()
        {
            if (RealityEngine.Combat.PhysicsHands.Instance != null && RealityEngine.Combat.PhysicsHands.Instance.AnyHeld != null)
                return true;
            if (_desk == null && Time.frameCount % 60 == 0)
                _desk = UnityEngine.Object.FindAnyObjectByType<DesktopInteractor>();
            if (_desk != null && _desk.Held != null)
                return true;
            if (_interactors == null || Time.unscaledTime >= _nextInteractorRefresh)
            {
                _nextInteractorRefresh = Time.unscaledTime + 1f;
                _interactors = UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
            }
            for (int i = 0; i < _interactors.Length; i++)
            {
                var it = _interactors[i];
                if (it == null) continue;
                var sels = it.interactablesSelected;
                if (sels == null || sels.Count == 0) continue;
                var c = sels[0] as Component;
                if (c != null && c.GetType().Name.IndexOf("Teleport", System.StringComparison.Ordinal) < 0)
                    return true;
            }
            return false;
        }

        void LateUpdate()
        {
            if (!_running || _hud == null)
                return;
            bool show = QhysicsUiState.GameplayHudVisible && !QhysicsModal.AnyOpen;
            _hud.SetActive(show);
            if (!show)
                return;
            if (_cam == null || !_cam.isActiveAndEnabled)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            _hud.Tick(_cam, 5f);
            _group.alpha = Time.unscaledTime < _stepFlashUntil ? 0.6f + 0.4f * Mathf.PingPong(Time.unscaledTime * 8f, 1f) : 1f;
        }
    }
}
