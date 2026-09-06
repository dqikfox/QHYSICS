using UnityEngine;
using Unity.XR.CoreUtils;
using RealityEngine.XR;
using RealityEngine.UI;

namespace RealityEngine.Player
{
    /// <summary>
    /// Ensures desktop player stack on Play (alongside LabPlayerSpawn / QhysicsUiBootstrap).
    /// Builds a simple standing body under XR Origin (not under Main Camera / never under monuments).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(125)]
    public sealed class QhysicsDesktopBootstrap : MonoBehaviour
    {
        public const string HostName = "QhysicsDesktopPlayer";
        public const string BodyName = "DesktopBody";
        public const string LeftHandName = "LeftHandProxy";
        public const string RightHandName = "RightHandProxy";
        public const string HandAttachName = "HandAttach";
        /// <summary>Layer used to hide head mesh from the player's own camera (FP).</summary>
        public const int PlayerSelfLayer = 31;

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
                    host = new GameObject(HostName);
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

            if (LabPlayerSpawnCompat.IsMonumentTransform(origin))
            {
                Debug.LogError("QhysicsDesktopBootstrap: FindOrigin returned monument '" + origin.name + "'; abort bind.");
                return;
            }

            LabPlayerSpawnCompat.EnsurePlayerNotUnderMonument(origin);
            LabPlayerSpawnCompat.StripMonumentChildren(origin);

            // Prefer host under Origin for local body visuals — never under a pyramid.
            if (transform.parent != origin)
                transform.SetParent(origin, false);
            LabPlayerSpawnCompat.EnsurePlayerNotUnderMonument(transform);

            var controller = GetComponent<DesktopPlayerController>();
            if (controller == null)
                controller = gameObject.AddComponent<DesktopPlayerController>();
            controller.Bind(origin);

            if (GetComponent<DesktopInteractor>() == null)
                gameObject.AddComponent<DesktopInteractor>();

            QhysicsInventory.Ensure(transform);
            EnsureDesktopBody(origin, controller != null ? controller.MainCamera : null);
            LabPlayerSpawnCompat.EnsureDesktopViewAuthority(origin, controller != null ? controller.MainCamera : null);
        }

        static Transform FindOrigin()
        {
            GameObject go = GameObject.Find(LabPlayerSpawn.OriginName);
            if (go != null)
                return go.transform;
            var origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < origins.Length; i++)
            {
                if (origins[i] == null)
                    continue;
                string n = origins[i].gameObject.name;
                if (n != null && (n.IndexOf("OVR", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Oculus", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                return origins[i].transform;
            }
            return null;
        }

        /// <summary>Standing capsule body under XR Origin. Head on PlayerSelf layer (hidden from own cam).</summary>
        static void EnsureDesktopBody(Transform origin, Transform mainCamera)
        {
            if (origin == null)
                return;

            // Migrate old camera-parented body (broke VR camera rules / pitched with look).
            if (mainCamera != null)
            {
                Transform legacy = mainCamera.Find(BodyName);
                if (legacy != null)
                {
                    if (Application.isPlaying) Object.Destroy(legacy.gameObject);
                    else Object.DestroyImmediate(legacy.gameObject);
                }
            }

            Transform body = origin.Find(BodyName);
            if (body == null)
            {
                // Also search under host in case of prior place.
                GameObject host = GameObject.Find(HostName);
                if (host != null)
                {
                    Transform underHost = host.transform.Find(BodyName);
                    if (underHost != null)
                        body = underHost;
                }
            }

            if (body == null)
            {
                var root = new GameObject(BodyName);
                root.transform.SetParent(origin, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                body = root.transform;

                // Torso — visible when looking down in FP; does not block view at eye height.
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Torso";
                capsule.transform.SetParent(body, false);
                capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                capsule.transform.localScale = new Vector3(0.32f, 0.45f, 0.24f);
                Object.Destroy(capsule.GetComponent<Collider>());
                Tint(capsule, new Color(0.18f, 0.22f, 0.28f));

                // Head — hidden from own camera via PlayerSelf layer.
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(body, false);
                head.transform.localPosition = new Vector3(0f, 1.55f, 0f);
                head.transform.localScale = Vector3.one * 0.22f;
                Object.Destroy(head.GetComponent<Collider>());
                Tint(head, new Color(0.85f, 0.72f, 0.58f));
                SetLayerRecursive(head, PlayerSelfLayer);

                MakeHand(body, LeftHandName, new Vector3(-0.28f, 1.05f, 0.28f));
                Transform right = MakeHand(body, RightHandName, new Vector3(0.28f, 1.05f, 0.28f));

                // Attach point for desktop grab (child of right hand).
                var attach = new GameObject(HandAttachName);
                attach.transform.SetParent(right, false);
                attach.transform.localPosition = new Vector3(0f, 0f, 0.06f);

                // Soft shadow blob on floor.
                var blob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                blob.name = "ShadowBlob";
                blob.transform.SetParent(body, false);
                blob.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                blob.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
                Object.Destroy(blob.GetComponent<Collider>());
                Tint(blob, new Color(0f, 0f, 0f, 0.35f));
            }
            else if (body.parent != origin)
            {
                body.SetParent(origin, false);
                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
            }

            // Ensure hand attach exists on rebuild.
            Transform rh = body.Find(RightHandName);
            if (rh != null && rh.Find(HandAttachName) == null)
            {
                var attach = new GameObject(HandAttachName);
                attach.transform.SetParent(rh, false);
                attach.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            }

            var gate = body.GetComponent<DesktopBodyVisibility>();
            if (gate == null)
                gate = body.gameObject.AddComponent<DesktopBodyVisibility>();
            gate.Bind(mainCamera);
            gate.Refresh();
        }

        static Transform MakeHand(Transform parent, string name, Vector3 localPos)
        {
            var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hand.name = name;
            hand.transform.SetParent(parent, false);
            hand.transform.localPosition = localPos;
            hand.transform.localScale = Vector3.one * 0.08f;
            Object.Destroy(hand.GetComponent<Collider>());
            Tint(hand, new Color(0.85f, 0.72f, 0.58f));
            return hand.transform;
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

        static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null)
                return;
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        /// <summary>Right-hand attach used by DesktopInteractor for held props.</summary>
        public static Transform FindHandAttach(Transform origin = null)
        {
            if (origin == null)
            {
                GameObject go = GameObject.Find(LabPlayerSpawn.OriginName);
                origin = go != null ? go.transform : null;
            }
            if (origin == null)
                return null;
            Transform body = origin.Find(BodyName);
            if (body == null)
                return null;
            Transform rh = body.Find(RightHandName);
            if (rh == null)
                return null;
            Transform attach = rh.Find(HandAttachName);
            return attach != null ? attach : rh;
        }
    }

    /// <summary>
    /// Hides desktop body when XR display is running; excludes PlayerSelf (head) from own Main Camera;
    /// parks FP hand proxies in front of the camera so grab attach feels attached to the character.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopBodyVisibility : MonoBehaviour
    {
        Renderer[] _renderers;
        Transform _mainCamera;
        Transform _leftHand;
        Transform _rightHand;
        Vector3 _leftRest;
        Vector3 _rightRest;
        bool _restCaptured;
        int _savedMask = -1;
        Camera _cam;

        public void Bind(Transform mainCamera)
        {
            _mainCamera = mainCamera;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _leftHand = transform.Find(QhysicsDesktopBootstrap.LeftHandName);
            _rightHand = transform.Find(QhysicsDesktopBootstrap.RightHandName);
            if (!_restCaptured)
            {
                if (_leftHand != null) _leftRest = _leftHand.localPosition;
                if (_rightHand != null) _rightRest = _rightHand.localPosition;
                _restCaptured = true;
            }
            ExcludeHeadFromOwnCamera();
        }

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _leftHand = transform.Find(QhysicsDesktopBootstrap.LeftHandName);
            _rightHand = transform.Find(QhysicsDesktopBootstrap.RightHandName);
        }

        void LateUpdate()
        {
            Refresh();
            UpdateFpHands();
        }

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
            if (show)
                ExcludeHeadFromOwnCamera();
        }

        void ExcludeHeadFromOwnCamera()
        {
            if (_mainCamera == null)
            {
                var desktop = DesktopPlayerController.Instance;
                if (desktop != null)
                    _mainCamera = desktop.MainCamera;
            }
            if (_mainCamera == null)
                return;
            if (_cam == null)
                _cam = _mainCamera.GetComponent<Camera>();
            if (_cam == null)
                return;
            int bit = 1 << QhysicsDesktopBootstrap.PlayerSelfLayer;
            if (_savedMask < 0)
                _savedMask = _cam.cullingMask;
            if ((_cam.cullingMask & bit) != 0)
                _cam.cullingMask &= ~bit;
        }

        void UpdateFpHands()
        {
            if (DesktopPlayerController.IsXrDisplayRunning())
            {
                // Restore rest pose under body when in VR (body itself is hidden).
                if (_leftHand != null) _leftHand.localPosition = _leftRest;
                if (_rightHand != null) _rightHand.localPosition = _rightRest;
                return;
            }

            // DesktopInteractor drives the right hand while a prop is held - do not fight it.
            var interactor = Object.FindFirstObjectByType<DesktopInteractor>(FindObjectsInactive.Exclude);
            bool holding = interactor != null && interactor.Held != null;

            if (_mainCamera == null)
            {
                var desktop = DesktopPlayerController.Instance;
                if (desktop != null)
                    _mainCamera = desktop.MainCamera;
            }
            if (_mainCamera == null)
                return;

            // World-space FP hands in front of camera (character "holds" them).
            PlaceHandWorld(_leftHand, new Vector3(-0.22f, -0.18f, 0.42f));
            if (!holding)
                PlaceHandWorld(_rightHand, new Vector3(0.22f, -0.18f, 0.42f));
        }

        void PlaceHandWorld(Transform hand, Vector3 camLocal)
        {
            if (hand == null || _mainCamera == null)
                return;
            Vector3 world = _mainCamera.TransformPoint(camLocal);
            hand.position = world;
            hand.rotation = _mainCamera.rotation;
        }
    }
}
