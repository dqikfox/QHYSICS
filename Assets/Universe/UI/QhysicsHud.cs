using UnityEngine;
using TMPro;
using RealityEngine.Experiments;

namespace RealityEngine.UI
{
    /// <summary>
    /// Tiny world-space HUD: QHYSICS + experiment name + running dot. Non-dominant side follow.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class QhysicsHud : MonoBehaviour
    {
        public const string RootName = "QhysicsHud";

        [SerializeField] bool wristFollow;
        [SerializeField] float sideSign = -1f;

        TextMeshProUGUI _title;
        TextMeshProUGUI _experiment;
        UnityEngine.UI.Image _dot;
        Camera _cam;
        ExperimentRunner _runner;
        float _nextRefresh;

        public static QhysicsHud Ensure(Transform parent)
        {
            Transform existing = parent != null ? parent.Find(RootName) : null;
            if (existing == null)
            {
                GameObject found = GameObject.Find(RootName);
                if (found != null)
                    existing = found.transform;
            }
            if (existing != null)
            {
                var hud = existing.GetComponent<QhysicsHud>();
                if (hud == null)
                    hud = existing.gameObject.AddComponent<QhysicsHud>();
                return hud;
            }

            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsHud>();
            comp.Build();
            return comp;
        }

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(c.gameObject);
                else
                    DestroyImmediate(c.gameObject);
            }

            var canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(420f, 110f));
            QhysicsUiBuilder.WireEventCamera(canvas);
            var face = QhysicsUiBuilder.BorderPanel(canvas.transform, "Panel", new Vector2(400f, 96f));
            face.raycastTarget = false;
            face.transform.parent.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

            _title = QhysicsUiBuilder.Label(face.transform, "Title", "QHYSICS", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            _title.rectTransform.anchoredPosition = new Vector2(-40f, 22f);
            _title.rectTransform.sizeDelta = new Vector2(280f, 44f);

            _dot = QhysicsUiBuilder.Panel(face.transform, "RunDot", QhysicsUiStyle.AccentActive, new Vector2(18f, 18f));
            _dot.rectTransform.anchoredPosition = new Vector2(170f, 24f);
            _dot.raycastTarget = false;

            _experiment = QhysicsUiBuilder.Label(face.transform, "Experiment", "Induction Lab", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineLeft);
            _experiment.rectTransform.anchoredPosition = new Vector2(-40f, -22f);
            _experiment.rectTransform.sizeDelta = new Vector2(340f, 32f);
        }

        void LateUpdate()
        {
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;

            Follow(_cam);
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(GetComponentInChildren<Canvas>());

            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            RefreshLabels();
        }

        void Follow(Camera cam)
        {
            Transform anchor = cam.transform;
            if (wristFollow)
            {
                Transform hand = FindHand(sideSign < 0f);
                if (hand != null)
                    anchor = hand;
            }

            Vector3 right = Vector3.Cross(Vector3.up, Flatten(anchor.forward));
            if (right.sqrMagnitude < 1e-6f)
                right = Flatten(anchor.right);
            right.Normalize();
            Vector3 pos = anchor.position
                + Flatten(anchor.forward) * QhysicsUiStyle.HudDistanceM
                + right * (0.22f * sideSign)
                + Vector3.up * (-0.12f);
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        static Transform FindHand(bool left)
        {
            string token = left ? "left" : "right";
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                    continue;
                string n = all[i].name.ToLowerInvariant();
                if ((n.Contains("controller") || n.Contains("hand") || n.Contains("interactor"))
                    && n.Contains(token)
                    && !n.Contains("model"))
                    return all[i];
            }
            return null;
        }

        void RefreshLabels()
        {
            if (_runner == null)
                _runner = Object.FindFirstObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
            string name = "Faraday Induction";
            bool running = Application.isPlaying && Time.timeScale > 0.01f;
            if (_runner != null && _runner.Definition != null)
            {
                if (!string.IsNullOrEmpty(_runner.Definition.title))
                    name = _runner.Definition.title;
                else if (!string.IsNullOrEmpty(_runner.Definition.id))
                    name = _runner.Definition.id;
                running = _runner.State == ExperimentState.Recording || _runner.State == ExperimentState.Armed
                    || (Application.isPlaying && Time.timeScale > 0.01f);
            }
            if (_experiment != null)
                _experiment.text = Truncate(name, 42);
            if (_dot != null)
                _dot.color = running ? QhysicsUiStyle.AccentActive : QhysicsUiStyle.AccentAttention;
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s;
            return s.Substring(0, max - 1) + "...";
        }
    }
}
