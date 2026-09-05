using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// World-space VR UI helpers: TrackedDeviceGraphicRaycaster, XR camera, Quest sizing.
    /// </summary>
    public static class QhysicsUiBuilder
    {
        public static Canvas CreateWorldCanvas(string name, Transform parent, Vector2 refSizePx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 80;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = refSizePx;
            go.transform.localScale = Vector3.one * QhysicsUiStyle.CanvasScale;

            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            go.AddComponent<GraphicRaycaster>().enabled = false;
            if (go.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                go.AddComponent<TrackedDeviceGraphicRaycaster>();

            Camera cam = ResolveXrCamera();
            if (cam != null)
                canvas.worldCamera = cam;
            return canvas;
        }

        public static Camera ResolveXrCamera()
        {
            Camera main = Camera.main;
            if (main != null)
                return main;
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].CompareTag("MainCamera"))
                    return cams[i];
            }
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] == null)
                    continue;
                string n = cams[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("center") || n.Contains("eye") || n.Contains("xr") || n.Contains("main"))
                    return cams[i];
            }
            return cams.Length > 0 ? cams[0] : null;
        }

        public static void WireEventCamera(Canvas canvas)
        {
            if (canvas == null)
                return;
            Camera cam = ResolveXrCamera();
            if (cam != null)
                canvas.worldCamera = cam;
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                canvas.AddComponent<TrackedDeviceGraphicRaycaster>();
            GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr != null && !(gr is TrackedDeviceGraphicRaycaster))
                gr.enabled = false;
        }

        public static void EnsureXrUiInputModule()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }
            if (es.GetComponent<XRUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    if (Application.isPlaying) Object.Destroy(legacy);
                    else Object.DestroyImmediate(legacy);
                }
                es.gameObject.AddComponent<XRUIInputModule>();
            }
        }

        public static Image Panel(Transform parent, string name, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.sprite = QhysicsUiStyle.WhiteSprite;
            img.color = color;
            img.material = QhysicsUiStyle.UiMaterial;
            img.raycastTarget = true;
            return img;
        }

        public static Image BorderPanel(Transform parent, string name, Vector2 size)
        {
            Image border = Panel(parent, name + "_Border", QhysicsUiStyle.PanelBorder, size + new Vector2(4f, 4f));
            Image face = Panel(border.transform, name, QhysicsUiStyle.PanelBg, size);
            return face;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            QhysicsUiStyle.ApplyTmp(tmp, size, color);
            tmp.alignment = align;
            tmp.text = text;
            tmp.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, size + 16f);
            return tmp;
        }

        public static Button ChipButton(Transform parent, string name, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            Image bg = Panel(parent, name, QhysicsUiStyle.ChipBg, size);
            var btn = bg.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = QhysicsUiStyle.ChipBg;
            colors.highlightedColor = QhysicsUiStyle.ChipBgActive;
            colors.pressedColor = QhysicsUiStyle.TabActive;
            colors.selectedColor = QhysicsUiStyle.ChipBgActive;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.22f, 0.5f);
            btn.colors = colors;
            btn.targetGraphic = bg;
            if (onClick != null)
                btn.onClick.AddListener(onClick);

            TextMeshProUGUI tmp = Label(bg.transform, "Label", label, QhysicsUiStyle.FontChip, QhysicsUiStyle.TextPrimary,
                TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 4f);
            rt.offsetMax = new Vector2(-8f, -4f);
            tmp.raycastTarget = false;
            return btn;
        }

        public static void LayoutHorizontal(RectTransform row, float spacing)
        {
            var h = row.gameObject.GetComponent<HorizontalLayoutGroup>();
            if (h == null)
                h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childControlWidth = false;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            h.padding = new RectOffset(12, 12, 8, 8);
        }

        public static void LayoutVertical(RectTransform col, float spacing)
        {
            var v = col.gameObject.GetComponent<VerticalLayoutGroup>();
            if (v == null)
                v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = false;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.padding = new RectOffset(16, 16, 16, 16);
        }

        public static void FaceCamera(Transform t, Camera cam)
        {
            if (t == null || cam == null)
                return;
            Vector3 to = t.position - cam.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
                t.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }
    }
}

