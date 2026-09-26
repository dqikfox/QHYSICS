using UnityEngine;
using UnityEngine.UI;
using RealityEngine.Player;

namespace RealityEngine.UI
{
    /// <summary>
    /// Global UI state: while the boot menu is up the gameplay HUD (dock, status strip, hints, inspect)
    /// is hidden. Gameplay HUD appears after Enter Sandbox (or immediately when the menu is skipped).
    /// </summary>
    public static class QhysicsUiState
    {
        public static bool BootMenuOpen { get; set; }

        /// <summary>True when the in-play HUD (dock, status strip, hints) may be shown.</summary>
        public static bool GameplayHudVisible => !BootMenuOpen;

        public static bool IsXr => DesktopPlayerController.IsXrDisplayRunning();
    }

    /// <summary>
    /// Helper for HUD canvases that are ScreenSpaceOverlay on desktop and WorldSpace (head-follow) in XR.
    /// </summary>
    public sealed class QhysicsHudCanvas
    {
        public Canvas Canvas;
        public RectTransform Root;      // the content rect positioned in screen mode
        public Vector2 ScreenAnchor;    // e.g. (0.5,1) top-centre
        public Vector2 ScreenOffset;    // px from the anchor
        public Vector3 WorldOffset;     // (right, up, forward) metres from the camera
        public float WorldScale = 1f;
        public Vector2 WorldSize = new Vector2(1200f, 200f);
        bool _world;
        bool _init;

        public static QhysicsHudCanvas Create(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            return new QhysicsHudCanvas { Canvas = c, Root = content.GetComponent<RectTransform>() };
        }

        public void SetActive(bool on)
        {
            if (Canvas != null && Canvas.gameObject.activeSelf != on)
                Canvas.gameObject.SetActive(on);
        }

        /// <summary>Apply render mode for this frame and follow the camera in XR. Call from LateUpdate.</summary>
        public void Tick(Camera cam, float followSharpness = 10f)
        {
            if (Canvas == null)
                return;
            bool world = QhysicsUiState.IsXr;
            if (!_init || world != _world)
            {
                _init = true;
                _world = world;
                if (world)
                {
                    Canvas.renderMode = RenderMode.WorldSpace;
                    var rt = Canvas.GetComponent<RectTransform>();
                    rt.sizeDelta = WorldSize;
                    Canvas.transform.localScale = Vector3.one * QhysicsUiStyle.CanvasScale * WorldScale;
                    Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
                    Root.pivot = new Vector2(0.5f, 0.5f);
                    Root.anchoredPosition = Vector2.zero;
                    var gr = Canvas.GetComponent<GraphicRaycaster>();
                    if (gr != null)
                        gr.enabled = false;
                    QhysicsUiBuilder.WireEventCamera(Canvas);
                    if (cam != null)
                        Canvas.transform.position = TargetPos(cam);
                }
                else
                {
                    Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    Canvas.transform.localScale = Vector3.one;
                    var gr = Canvas.GetComponent<GraphicRaycaster>();
                    if (gr != null)
                        gr.enabled = true;
                    Root.anchorMin = Root.anchorMax = ScreenAnchor;
                    Root.pivot = ScreenAnchor;
                    Root.anchoredPosition = ScreenOffset;
                }
            }
            if (world && cam != null)
            {
                Transform t = Canvas.transform;
                t.position = Vector3.Lerp(t.position, TargetPos(cam), 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
                QhysicsUiBuilder.FaceCamera(t, cam);
            }
        }

        Vector3 TargetPos(Camera cam)
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            return cam.transform.position + right * WorldOffset.x + Vector3.up * WorldOffset.y + fwd * WorldOffset.z;
        }
    }
}
