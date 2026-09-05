using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// Context strip when pointing at / holding an XR interactable: name + stub stats + hints.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(202)]
    public sealed class QhysicsContextStrip : MonoBehaviour
    {
        public const string RootName = "QhysicsContextStrip";

        Canvas _canvas;
        TextMeshProUGUI _name;
        TextMeshProUGUI _stats;
        TextMeshProUGUI _hints;
        Camera _cam;
        float _nextPoll;
        float _nextInteractorRefresh;
        XRBaseInteractor[] _interactors;
        Transform _followTarget;

        public static QhysicsContextStrip Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsContextStrip>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsContextStrip>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsContextStrip>();
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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(520f, 160f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(500f, 140f));
            face.raycastTarget = false;
            face.transform.parent.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

            _name = QhysicsUiBuilder.Label(face.transform, "Name", "-", QhysicsUiStyle.FontBody,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.MidlineLeft);
            _name.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            _name.rectTransform.sizeDelta = new Vector2(460f, 36f);

            _stats = QhysicsUiBuilder.Label(face.transform, "Stats", "mass - | charge - | T -", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineLeft);
            _stats.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            _stats.rectTransform.sizeDelta = new Vector2(460f, 28f);

            _hints = QhysicsUiBuilder.Label(face.transform, "Hints", "Rotate | Move | Inspect", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            _hints.rectTransform.anchoredPosition = new Vector2(0f, -36f);
            _hints.rectTransform.sizeDelta = new Vector2(460f, 28f);

            _canvas.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + 0.08f;
                PollInteractables();
            }

            if (_canvas == null || !_canvas.gameObject.activeSelf)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;

            Vector3 anchor = _followTarget != null ? _followTarget.position : _cam.transform.position + _cam.transform.forward;
            Vector3 pos = anchor + Vector3.up * 0.18f;
            Vector3 toCam = _cam.transform.position - pos;
            if (toCam.sqrMagnitude > 1e-4f)
                pos += toCam.normalized * 0.05f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        void PollInteractables()
        {
            IXRInteractable best = null;
            bool selected = false;

            if (_interactors == null || Time.unscaledTime >= _nextInteractorRefresh)
            {
                _nextInteractorRefresh = Time.unscaledTime + 1f;
                _interactors = Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
            }
            var interactors = _interactors;
            for (int i = 0; i < interactors.Length; i++)
            {
                var interactor = interactors[i];
                if (interactor == null)
                    continue;
                var sels = interactor.interactablesSelected;
                if (sels != null && sels.Count > 0)
                {
                    best = sels[0];
                    selected = true;
                    break;
                }
            }

            if (best == null)
            {
                for (int i = 0; i < interactors.Length; i++)
                {
                    var interactor = interactors[i];
                    if (interactor == null)
                        continue;
                    var hovered = interactor.interactablesHovered;
                    if (hovered != null && hovered.Count > 0)
                    {
                        best = hovered[0];
                        break;
                    }
                }
            }

            if (best == null)
            {
                if (_canvas != null)
                    _canvas.gameObject.SetActive(false);
                _followTarget = null;
                return;
            }

            Component comp = best as Component;
            _followTarget = comp != null ? comp.transform : null;
            string n = comp != null ? comp.gameObject.name : best.ToString();
            if (_name != null)
                _name.text = n;
            if (_stats != null)
            {
                Rigidbody rb = comp != null ? comp.GetComponentInParent<Rigidbody>() : null;
                string mass = rb != null ? rb.mass.ToString("0.###") + " kg" : "-";
                _stats.text = "mass " + mass + " | state " + (selected ? "held" : "hover") + " | id stub";
            }
            if (_hints != null)
                _hints.text = selected ? "Rotate | Move | Inspect" : "Grip to grab | Ray to select";
            if (_canvas != null)
                _canvas.gameObject.SetActive(true);
        }
    }
}

