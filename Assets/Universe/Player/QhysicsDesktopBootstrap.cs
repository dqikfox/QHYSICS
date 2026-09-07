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
        public const string HipAnchorName = "HipAnchor";
        public const float BodyEyeHeightM = 1.65f;
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
                capsule.transform.localPosition = new Vector3(0f, 0.86f, 0f);
                // Half-width ~0.20 matches CharacterController radius 0.22 (no snag / no fall-through).
                capsule.transform.localScale = new Vector3(0.40f, 0.42f, 0.28f);
                Object.Destroy(capsule.GetComponent<Collider>());
                Tint(capsule, new Color(0.18f, 0.22f, 0.28f));

                // Head — hidden from own camera via PlayerSelf layer.
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(body, false);
                head.transform.localPosition = new Vector3(0f, BodyEyeHeightM, 0f);
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

            // Ensure FP arm proxies + hand attach on rebuild / prior DesktopBody.
            EnsureFpArmVisuals(body);
            Transform rh = body.Find(RightHandName);
            if (rh != null && rh.Find(HandAttachName) == null)
            {
                var attach = new GameObject(HandAttachName);
                attach.transform.SetParent(rh, false);
                attach.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            }

            // Hip / belt anchor for worn toolbelt (chest-ish, not glued to HMD).
            Transform hip = body.Find(HipAnchorName);
            if (hip == null)
            {
                var hipGo = new GameObject(HipAnchorName);
                hipGo.transform.SetParent(body, false);
                hip = hipGo.transform;
            }
            hip.localPosition = new Vector3(0f, 1.05f, 0.12f);
            hip.localRotation = Quaternion.identity;

            // Worn inventory pouch (belt + bag + selected-slot icon). XR-safe via body visibility gate.
            InventoryHipPouch.Ensure(hip);

            // Keep head mesh aligned with desktop eye height.
            Transform headTf = body.Find("Head");
            if (headTf != null)
            {
                Vector3 hp = headTf.localPosition;
                hp.y = BodyEyeHeightM;
                headTf.localPosition = hp;
            }

            var gate = body.GetComponent<DesktopBodyVisibility>();
            if (gate == null)
                gate = body.gameObject.AddComponent<DesktopBodyVisibility>();
            // Re-bind after pouch meshes so XR hide / FP culling includes new renderers.
            gate.Bind(mainCamera);
            gate.Refresh();
        }

        /// <summary>Upgrade or create clearer desktop arm/hand proxies (cylinder + sphere).</summary>
        static void EnsureFpArmVisuals(Transform body)
        {
            if (body == null)
                return;
            EnsureOneArm(body, LeftHandName, new Vector3(-0.28f, 1.05f, 0.28f), left: true);
            EnsureOneArm(body, RightHandName, new Vector3(0.28f, 1.05f, 0.28f), left: false);
        }

        static Transform EnsureOneArm(Transform body, string handName, Vector3 restPos, bool left)
        {
            Transform hand = body.Find(handName);
            if (hand == null)
                hand = MakeHand(body, handName, restPos);
            else
                hand.localPosition = restPos;

            // Arm bone: upper-arm cylinder from shoulder toward hand rest.
            Transform arm = hand.Find("Arm");
            if (arm == null)
            {
                var armGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                armGo.name = "Arm";
                armGo.transform.SetParent(hand, false);
                Object.Destroy(armGo.GetComponent<Collider>());
                Tint(armGo, new Color(0.20f, 0.24f, 0.30f));
                arm = armGo.transform;
            }
            // Local: arm sits behind the hand sphere (toward shoulder).
            float side = left ? 1f : -1f;
            arm.localPosition = new Vector3(side * 0.12f, 0.02f, -0.18f);
            arm.localRotation = Quaternion.Euler(0f, 0f, side * 55f);
            arm.localScale = new Vector3(0.045f, 0.16f, 0.045f);

            Transform forearm = hand.Find("Forearm");
            if (forearm == null)
            {
                var fa = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fa.name = "Forearm";
                fa.transform.SetParent(hand, false);
                Object.Destroy(fa.GetComponent<Collider>());
                Tint(fa, new Color(0.22f, 0.26f, 0.32f));
                forearm = fa.transform;
            }
            forearm.localPosition = new Vector3(side * 0.04f, 0f, -0.06f);
            forearm.localRotation = Quaternion.Euler(80f, 0f, side * 10f);
            forearm.localScale = new Vector3(0.035f, 0.10f, 0.035f);

            // Palm cue (flat cube) — clearer than bare sphere alone.
            Transform palm = hand.Find("Palm");
            if (palm == null)
            {
                var palmGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                palmGo.name = "Palm";
                palmGo.transform.SetParent(hand, false);
                Object.Destroy(palmGo.GetComponent<Collider>());
                Tint(palmGo, new Color(0.85f, 0.72f, 0.58f));
                palm = palmGo.transform;
            }
            palm.localPosition = new Vector3(0f, 0f, 0.02f);
            palm.localRotation = Quaternion.identity;
            palm.localScale = new Vector3(0.7f, 0.25f, 0.9f);

            if (hand.GetComponent<Collider>() != null)
                Object.Destroy(hand.GetComponent<Collider>());
            // Hand root stays a small sphere.
            if (hand.localScale.sqrMagnitude < 0.001f || Mathf.Abs(hand.localScale.x - 0.08f) > 0.05f)
                hand.localScale = Vector3.one * 0.08f;
            return hand;
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

        /// <summary>Hip/chest anchor for worn toolbelt (DesktopBody or Origin fallback).</summary>
        public static Transform FindHipAnchor(Transform origin = null)
        {
            if (origin == null)
            {
                GameObject go = GameObject.Find(LabPlayerSpawn.OriginName);
                origin = go != null ? go.transform : null;
            }
            if (origin == null)
                return null;
            Transform body = origin.Find(BodyName);
            if (body != null)
            {
                Transform hip = body.Find(HipAnchorName);
                if (hip != null)
                    return hip;
            }
            // XR without desktop body: soft chest anchor under Origin.
            Transform existing = origin.Find(HipAnchorName);
            if (existing != null)
                return existing;
            var hipGo = new GameObject(HipAnchorName);
            hipGo.transform.SetParent(origin, false);
            hipGo.transform.localPosition = new Vector3(0f, 1.05f, 0.12f);
            return hipGo.transform;
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
            // Force-hide when Link/OpenXR display is running (desktop WASD body must not remain visible in HMD).
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

            // Pitch sway: look down lowers hands slightly, look up raises — follows look a bit.
            float pitch = _mainCamera.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            float swayY = Mathf.Clamp(pitch, -80f, 80f) * -0.0018f;
            float swayZ = Mathf.Clamp(pitch, -80f, 80f) * 0.0006f;

            PlaceHandWorld(_leftHand, new Vector3(-0.22f, -0.18f + swayY, 0.42f + swayZ));
            if (!holding)
                PlaceHandWorld(_rightHand, new Vector3(0.22f, -0.18f + swayY, 0.42f + swayZ));
        }

        void PlaceHandWorld(Transform hand, Vector3 camLocal)
        {
            if (hand == null || _mainCamera == null)
                return;
            Vector3 world = _mainCamera.TransformPoint(camLocal);
            Quaternion rot = _mainCamera.rotation;
            // Soft follow so arms do not lock 1:1 to every mouse twitch.
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            hand.position = Vector3.Lerp(hand.position, world, k);
            hand.rotation = Quaternion.Slerp(hand.rotation, rot, k);
        }
    }
}
