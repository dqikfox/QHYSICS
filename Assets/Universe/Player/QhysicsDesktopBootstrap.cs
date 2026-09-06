using UnityEngine;
using Unity.XR.CoreUtils;
using RealityEngine.XR;
using RealityEngine.UI;

namespace RealityEngine.Player
{
    /// <summary>
    /// Ensures desktop player stack on Play (alongside LabPlayerSpawn / QhysicsUiBootstrap).
    /// Adds a simple capsule/hands under camera for desktop visibility — not a full humanoid.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(125)]
    public sealed class QhysicsDesktopBootstrap : MonoBehaviour
    {
        public const string HostName = "QhysicsDesktopPlayer";
        public const string BodyName = "DesktopBody";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoEnsure()
        {
            if (!Application.isPlaying)
                return;
            Ensure();
        }

        public static QhysicsDesktopBootstrap Ensure()
        {
            // Park XR Origin first so desktop binds to the plaza pose.
            LabPlayerSpawn.EnsureApplied();

            QhysicsDesktopBootstrap existing = Object.FindFirstObjectByType<QhysicsDesktopBootstrap>(FindObjectsInactive.Include);
            if (existing == null)
            {
                GameObject host = GameObject.Find(HostName);
                if (host == null)
                {
                    GameObject origin = GameObject.Find(LabPlayerSpawn.OriginName);
                    host = origin != null ? new GameObject(HostName) : new GameObject(HostName);
                    if (origin != null)
                        host.transform.SetParent(origin.transform, false);
                }
                existing = host.GetComponent<QhysicsDesktopBootstrap>();
                if (existing == null)
                    existing = host.AddComponent<QhysicsDesktopBootstrap>();
            }

            existing.Build();
            return existing;
        }

        public void Build()
        {
            Transform origin = FindOrigin();
            if (origin == null)
            {
                Debug.LogWarning("QhysicsDesktopBootstrap: XR Origin missing; desktop player deferred.");
                return;
            }

            // Prefer host under Origin for local body visuals.
            if (transform.parent != origin)
                transform.SetParent(origin, false);

            var controller = GetComponent<DesktopPlayerController>();
            if (controller == null)
                controller = gameObject.AddComponent<DesktopPlayerController>();
            controller.Bind(origin);

            if (GetComponent<DesktopInteractor>() == null)
                gameObject.AddComponent<DesktopInteractor>();

            QhysicsInventory.Ensure(transform);
            EnsureDesktopBody(origin);
        }

        static Transform FindOrigin()
        {
            GameObject go = GameObject.Find(LabPlayerSpawn.OriginName);
            if (go != null)
                return go.transform;
            var origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return origins.Length > 0 ? origins[0].transform : null;
        }

        static void EnsureDesktopBody(Transform origin)
        {
            Transform offset = origin.Find(LabPlayerSpawn.CameraOffsetName);
            if (offset == null)
            {
                var xr = origin.GetComponent<XROrigin>();
                if (xr != null && xr.CameraFloorOffsetObject != null)
                    offset = xr.CameraFloorOffsetObject.transform;
            }
            Transform cam = offset != null ? offset.Find(LabPlayerSpawn.MainCameraName) : null;
            if (cam == null && offset != null)
            {
                var c = offset.GetComponentInChildren<Camera>(true);
                if (c != null)
                    cam = c.transform;
            }
            if (cam == null)
                return;

            Transform body = cam.Find(BodyName);
            if (body != null)
                return;

            // Only show the desktop body when XR display is off (headset still first-person).
            var root = new GameObject(BodyName);
            root.transform.SetParent(cam, false);
            root.transform.localPosition = new Vector3(0f, -0.35f, 0.25f);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Torso";
            capsule.transform.SetParent(root.transform, false);
            capsule.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            capsule.transform.localScale = new Vector3(0.28f, 0.45f, 0.22f);
            Object.Destroy(capsule.GetComponent<Collider>());
            Tint(capsule, new Color(0.18f, 0.22f, 0.28f));

            MakeHand(root.transform, "LeftHandProxy", new Vector3(-0.22f, -0.25f, 0.35f));
            MakeHand(root.transform, "RightHandProxy", new Vector3(0.22f, -0.25f, 0.35f));

            // Soft shadow blob on floor (local under body, projected downward visually).
            var blob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blob.name = "ShadowBlob";
            blob.transform.SetParent(root.transform, false);
            blob.transform.localPosition = new Vector3(0f, -1.35f, 0f);
            blob.transform.localScale = new Vector3(0.45f, 0.01f, 0.45f);
            Object.Destroy(blob.GetComponent<Collider>());
            Tint(blob, new Color(0f, 0f, 0f, 0.35f));

            var gate = root.AddComponent<DesktopBodyVisibility>();
            gate.Refresh();
        }

        static void MakeHand(Transform parent, string name, Vector3 localPos)
        {
            var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hand.name = name;
            hand.transform.SetParent(parent, false);
            hand.transform.localPosition = localPos;
            hand.transform.localScale = Vector3.one * 0.07f;
            Object.Destroy(hand.GetComponent<Collider>());
            Tint(hand, new Color(0.85f, 0.72f, 0.58f));
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
    }

    /// <summary>Hides desktop body mesh when an XR headset display is running (stays enabled so it can re-show).</summary>
    [DisallowMultipleComponent]
    public sealed class DesktopBodyVisibility : MonoBehaviour
    {
        Renderer[] _renderers;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        void Update() => Refresh();

        public void Refresh()
        {
            bool show = !DesktopPlayerController.IsXrDisplayRunning();
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = show;
            }
        }
    }
}
