using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using RealityEngine.Player;

namespace RealityEngine.XR
{
    /// <summary>
    /// Lightweight XR controller/hand proxies + DirectInteractor attach points.
    /// Complements existing Hand Presence (OpenXR may not match old device names).
    /// Visible only while an XR display is running; desktop DesktopBody stays separate.
    /// </summary>
    public static class XrHandVisuals
    {
        public const string ProxyName = "XrControllerProxy";
        public const string AttachName = "Attach";

        public static void Ensure(Transform originXf)
        {
            if (originXf == null)
                return;
            Transform offset = originXf.Find(LabPlayerSpawn.CameraOffsetName);
            if (offset == null)
                return;

            Transform left = FindChild(offset, "Left Hand");
            Transform right = FindChild(offset, "Right Hand");
            EnsureHand(left, InteractorHandedness.Left);
            EnsureHand(right, InteractorHandedness.Right);

            // Keep Hand Presence components alive; they spawn richer meshes when devices match.
            EnsureHandPresenceActive(left);
            EnsureHandPresenceActive(right);
        }

        static void EnsureHand(Transform hand, InteractorHandedness side)
        {
            if (hand == null)
                return;

            if (!hand.gameObject.activeSelf)
                hand.gameObject.SetActive(true);

            XRDirectInteractor direct = hand.GetComponent<XRDirectInteractor>();
            if (direct != null)
            {
                direct.handedness = side;
                Transform attach = hand.Find(AttachName);
                if (attach == null)
                {
                    var go = new GameObject(AttachName);
                    go.transform.SetParent(hand, false);
                    go.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                    go.transform.localRotation = Quaternion.identity;
                    attach = go.transform;
                }
                if (direct.attachTransform == null)
                    direct.attachTransform = attach;
            }

            EnsureProxy(hand);
        }

        static void EnsureProxy(Transform hand)
        {
            Transform proxy = hand.Find(ProxyName);
            if (proxy == null)
            {
                var root = new GameObject(ProxyName);
                root.transform.SetParent(hand, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                proxy = root.transform;

                // Slim grip + tip — comfort/perf light (no materials beyond URP Lit tint).
                var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                grip.name = "Grip";
                grip.transform.SetParent(proxy, false);
                grip.transform.localPosition = new Vector3(0f, -0.02f, 0.02f);
                grip.transform.localScale = new Vector3(0.035f, 0.08f, 0.055f);
                Object.Destroy(grip.GetComponent<Collider>());
                Tint(grip, new Color(0.22f, 0.24f, 0.28f));

                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = "Tip";
                tip.transform.SetParent(proxy, false);
                tip.transform.localPosition = new Vector3(0f, -0.01f, 0.07f);
                tip.transform.localScale = Vector3.one * 0.028f;
                Object.Destroy(tip.GetComponent<Collider>());
                Tint(tip, new Color(0.35f, 0.75f, 0.85f));
            }

            var gate = proxy.GetComponent<XrProxyVisibility>();
            if (gate == null)
                gate = proxy.gameObject.AddComponent<XrProxyVisibility>();
            gate.Refresh();
        }

        static void EnsureHandPresenceActive(Transform hand)
        {
            if (hand == null)
                return;
            for (int i = 0; i < hand.childCount; i++)
            {
                Transform c = hand.GetChild(i);
                if (c == null || c.name == null)
                    continue;
                if (c.name.IndexOf("Hand Presence", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!c.gameObject.activeSelf)
                    c.gameObject.SetActive(true);
                var presence = c.GetComponent("HandPresence") as Behaviour;
                if (presence != null && !presence.enabled)
                    presence.enabled = true;
            }
        }

        static void Tint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null)
                return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
            r.sharedMaterial = mat;
        }

        static Transform FindChild(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != null && c.name == name)
                    return c;
            }
            return parent.Find(name);
        }
    }

    /// <summary>Shows XR controller proxies only while an XR display is running.</summary>
    [DisallowMultipleComponent]
    public sealed class XrProxyVisibility : MonoBehaviour
    {
        Renderer[] _renderers;
        bool _lastShow = true;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            bool show = DesktopPlayerController.IsXrDisplayRunning();
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
            if (show == _lastShow && _renderers != null)
            {
                // Still force once in case renderers were rebuilt.
            }
            _lastShow = show;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = show;
            }
        }
    }
}
