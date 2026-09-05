using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// Small > 1x chip expanding to speed / pause / step / reset stubs (Time.timeScale).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(203)]
    public sealed class QhysicsSimChip : MonoBehaviour
    {
        public const string RootName = "QhysicsSimChip";

        static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };

        Canvas _canvas;
        TextMeshProUGUI _label;
        RectTransform _expandRow;
        bool _expanded;
        bool _paused;
        float _savedScale = 1f;
        Camera _cam;

        public static QhysicsSimChip Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsSimChip>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsSimChip>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsSimChip>();
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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(720f, 120f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(200f, 88f));

            var main = QhysicsUiBuilder.ChipButton(face.transform, "Main", "> 1x", new Vector2(160f, 72f), ToggleExpand);
            _label = main.GetComponentInChildren<TextMeshProUGUI>();

            var expandGo = new GameObject("Expand", typeof(RectTransform));
            expandGo.transform.SetParent(_canvas.transform, false);
            _expandRow = expandGo.GetComponent<RectTransform>();
            _expandRow.anchoredPosition = new Vector2(0f, -90f);
            _expandRow.sizeDelta = new Vector2(680f, 80f);
            QhysicsUiBuilder.LayoutHorizontal(_expandRow, 8f);

            for (int i = 0; i < Speeds.Length; i++)
            {
                float s = Speeds[i];
                string t = s + "x";
                QhysicsUiBuilder.ChipButton(_expandRow, "Speed_" + t, t, new Vector2(88f, 64f), () => SetSpeed(s));
            }
            QhysicsUiBuilder.ChipButton(_expandRow, "Pause", "Pause", new Vector2(100f, 64f), TogglePause);
            QhysicsUiBuilder.ChipButton(_expandRow, "Step", "Step", new Vector2(88f, 64f), StepOnce);
            QhysicsUiBuilder.ChipButton(_expandRow, "Reset", "Reset", new Vector2(100f, 64f), ResetSim);

            _expandRow.gameObject.SetActive(false);
            RefreshLabel();
        }

        void LateUpdate()
        {
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 right = Flatten(_cam.transform.right);
            Vector3 fwd = Flatten(_cam.transform.forward);
            Vector3 pos = _cam.transform.position + fwd * 0.65f + right * 0.28f + Vector3.up * -0.18f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
            RefreshLabel();
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.right;
        }

        void ToggleExpand()
        {
            _expanded = !_expanded;
            if (_expandRow != null)
                _expandRow.gameObject.SetActive(_expanded);
            var face = _canvas != null ? _canvas.transform.Find("Panel_Border") : null;
            // keep main chip only
            RefreshLabel();
        }

        void SetSpeed(float scale)
        {
            _paused = false;
            _savedScale = scale;
            Time.timeScale = scale;
            // speed applied
            RefreshLabel();
        }

        void TogglePause()
        {
            if (!_paused)
            {
                _savedScale = Mathf.Max(0.01f, Time.timeScale);
                Time.timeScale = 0f;
                _paused = true;
                
            }
            else
            {
                Time.timeScale = _savedScale > 0.01f ? _savedScale : 1f;
                _paused = false;
                
            }
            RefreshLabel();
        }

        void StepOnce()
        {
            if (Time.timeScale > 0.01f)
            {
                _savedScale = Time.timeScale;
                Time.timeScale = 0f;
                _paused = true;
            }
            // Unscaled step stub: briefly nudge scale then pause again next frame via coroutine-less flag.
            StartCoroutine(StepCoroutine());
            
            RefreshLabel();
        }

        System.Collections.IEnumerator StepCoroutine()
        {
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(0.05f);
            Time.timeScale = 0f;
            _paused = true;
            RefreshLabel();
        }

        void ResetSim()
        {
            Time.timeScale = 1f;
            _savedScale = 1f;
            _paused = false;
            QhysicsLabActions.ResetCircuitLab();
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (_label == null)
                return;
            if (_paused || Time.timeScale < 0.01f)
                _label.text = "II";
            else
                _label.text = "> " + Time.timeScale.ToString("0.##") + "x";
        }
    }
}
