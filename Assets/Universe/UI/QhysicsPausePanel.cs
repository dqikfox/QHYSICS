using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

// COMPILE_OK pause uses UiButton via ChipButton
namespace RealityEngine.UI
{
    /// <summary>
    /// Sparse pause panel: Resume / New Experiment / Settings(disabled) / Exit Play (editor only).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(204)]
    public sealed class QhysicsPausePanel : MonoBehaviour
    {
        public const string RootName = "QhysicsPausePanel";

        Canvas _canvas;
        Camera _cam;
        bool _open;

        public static QhysicsPausePanel Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsPausePanel>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsPausePanel>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsPausePanel>();
            comp.Build();
            return comp;
        }

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(720f, 520f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(680f, 480f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 18f);

            var title = QhysicsUiBuilder.Label(face.transform, "Title", "PAUSED", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentAttention, TextAlignmentOptions.Center);
            title.rectTransform.sizeDelta = new Vector2(600f, 56f);

            QhysicsUiBuilder.ChipButton(face.transform, "Resume", "Resume", new Vector2(520f, 96f), Resume);
            QhysicsUiBuilder.ChipButton(face.transform, "NewExperiment", "New Experiment", new Vector2(520f, 96f), NewExperiment);
            var settings = QhysicsUiBuilder.ChipButton(face.transform, "Settings", "Settings", new Vector2(520f, 96f), null);
            settings.interactable = false;
            var dim = settings.targetGraphic as Image;
            if (dim != null)
                dim.color = new Color(0.2f, 0.2f, 0.22f, 0.55f);

#if UNITY_EDITOR
            QhysicsUiBuilder.ChipButton(face.transform, "ExitPlay", "Exit Play", new Vector2(520f, 96f), ExitPlay);
#endif
            SetOpen(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                Toggle();
        }

        void LateUpdate()
        {
            if (!_open)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 fwd = _cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 pos = _cam.transform.position + fwd * 1.05f + Vector3.up * 0.05f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        public void Toggle() => SetOpen(!_open);

        public void SetOpen(bool on)
        {
            _open = on;
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
            if (on && Time.timeScale > 0.01f)
                Time.timeScale = 0f;
        }

        void Resume()
        {
            Time.timeScale = 1f;
            SetOpen(false);
            Debug.Log("QHYSICS PausePanel: Resume");
        }

        void NewExperiment()
        {
            Time.timeScale = 1f;
            SetOpen(false);
            Debug.Log("QHYSICS PausePanel: New Experiment (stub)");
        }

#if UNITY_EDITOR
        void ExitPlay()
        {
            EditorApplication.isPlaying = false;
        }
#endif
    }
}
