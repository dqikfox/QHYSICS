using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using RealityEngine.Visualization;

namespace RealityEngine.XR
{
    /// <summary>
    /// Parks Faraday's XR Origin on the lab plaza (north of the circuit table, looking
    /// south at Khufu). Unparents from MountainScene at runtime. Rewires XRI 3.6
    /// teleport + both-hand snap. Does not disable MountainScene. Does not rebuild OVR.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class LabPlayerSpawn : MonoBehaviour
    {
        public const string OriginName = "XR Origin";
        public const string CameraOffsetName = "Camera Offset";
        const string HostName = "RealityEngine";
        const float StandNorthOfTableM = 2.0f;
        const float CcHeight = 1.8f;
        const float CcSkin = 0.08f;
        const float CcRadius = 0.15f;
        const float SinkResetM = 4f;

        static int _appliedFrame = -1;
        bool _parked;
        float _plazaY = float.NaN;
        XROrigin _origin;
        CharacterController _cc;
        SmoothMovementController _move;
        TeleportationController _teleportCtrl;
        TeleportationProvider _teleport;
#pragma warning disable CS0618
        DeviceBasedSnapTurnProvider _snap;
#pragma warning restore CS0618
        Behaviour[] _locomotion;
        bool _locomotionPaused;
        float _nextTrackCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookSceneLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _appliedFrame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoApplyAfterSceneLoad()
        {
            EnsureApplied();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            EnsureApplied();
        }

        public static LabPlayerSpawn EnsureApplied()
        {
            LabPlayerSpawn existing = Object.FindFirstObjectByType<LabPlayerSpawn>(FindObjectsInactive.Include);
            if (existing == null)
            {
                GameObject host = GameObject.Find(HostName);
                if (host == null)
                {
                    GameObject originGo = GameObject.Find(OriginName);
                    host = originGo != null ? originGo : new GameObject("LabPlayerSpawnHost");
                }
                existing = host.GetComponent<LabPlayerSpawn>();
                if (existing == null)
                    existing = host.AddComponent<LabPlayerSpawn>();
            }

            if (!existing.isActiveAndEnabled && existing.gameObject.activeInHierarchy)
                existing.enabled = true;

            existing.ApplyNow(false);
            return existing;
        }

        void Awake()
        {
            ApplyNow(false);
        }

        void Start()
        {
            ApplyNow(false);
            if (Application.isPlaying)
                StartCoroutine(ApplyDelayed());
        }

        IEnumerator ApplyDelayed()
        {
            yield return null;
            ApplyNow(false);
            yield return null;
            ApplyNow(false);
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            if (Time.unscaledTime < _nextTrackCheck)
                return;
            _nextTrackCheck = Time.unscaledTime + 0.25f;
            ApplyTrackingPause();
            if (_origin != null && !float.IsNaN(_plazaY) && _origin.transform.position.y < _plazaY - SinkResetM)
                ApplyNow(true);
        }

        public void ApplyNow(bool force)
        {
            if (!force && _appliedFrame == Time.frameCount)
                return;
            _appliedFrame = Time.frameCount;

            Transform originXf = FindXrOrigin();
            if (originXf == null)
            {
                Debug.LogWarning("LabPlayerSpawn: XR Origin not found; skipped.");
                return;
            }

            _origin = originXf.GetComponent<XROrigin>();
            UnparentKeepingWorld(originXf);
            PreserveCameraRig(originXf);
            EnsureFloorTracking(_origin);
            EnsureCharacterController(originXf);
            bool parked = ParkOnLabPlaza(originXf, force || !_parked);
            if (parked)
                _parked = true;

            WireLocomotion(originXf);
            WireTeleport(originXf);
            WireHands(originXf);
            ApplyTrackingPause();
        }

        static Transform FindXrOrigin()
        {
            GameObject named = GameObject.Find(OriginName);
            if (named != null)
                return named.transform;

            XROrigin[] origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < origins.Length; i++)
            {
                if (origins[i] == null)
                    continue;
                string n = origins[i].gameObject.name;
                if (n != null && n.IndexOf("OVR", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (n != null && n.IndexOf("Oculus", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                return origins[i].transform;
            }
            return null;
        }

        static void UnparentKeepingWorld(Transform originXf)
        {
            if (originXf.parent == null)
                return;
            originXf.SetParent(null, true);
        }

        static void PreserveCameraRig(Transform originXf)
        {
            Transform offset = originXf.Find(CameraOffsetName);
            if (offset == null && originXf.GetComponent<XROrigin>() != null)
            {
                GameObject floor = originXf.GetComponent<XROrigin>().CameraFloorOffsetObject;
                if (floor != null)
                    offset = floor.transform;
            }
            if (offset == null)
                Debug.LogWarning("LabPlayerSpawn: Camera Offset missing under XR Origin; not rebuilding OVR. Using remaining camera.");
        }

        static void EnsureFloorTracking(XROrigin origin)
        {
            if (origin == null)
                return;
            if (origin.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Floor)
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        }

        void EnsureCharacterController(Transform originXf)
        {
            _cc = originXf.GetComponent<CharacterController>();
            if (_cc == null)
            {
                Debug.LogWarning("LabPlayerSpawn: CharacterController missing on XR Origin; not adding a Rigidbody.");
                return;
            }
            bool ccOn = _cc.enabled;
            _cc.enabled = false;
            _cc.height = CcHeight;
            _cc.skinWidth = CcSkin;
            if (_cc.radius > CcRadius)
                _cc.radius = CcRadius;
            _cc.center = new Vector3(0f, CcHeight * 0.5f, 0f);
            _cc.enabled = ccOn;
        }

        bool ParkOnLabPlaza(Transform originXf, bool snapPose)
        {
            if (!snapPose && _parked)
                return true;

            Bounds table;
            Transform tableXf;
            ResolveTable(out table, out tableXf);

            Bounds plaza;
            bool hasPlaza = TryPlazaBounds(out plaza);
            Vector3 khufu = ResolveKhufuCenter(table.center);

            Vector3 north = Vector3.forward;
            Vector3 toKhufu = khufu - table.center;
            toKhufu.y = 0f;
            if (toKhufu.sqrMagnitude > 1f)
                north = -toKhufu.normalized;
            else if (tableXf != null)
            {
                Vector3 tf = tableXf.forward;
                tf.y = 0f;
                if (tf.sqrMagnitude > 1e-4f)
                {
                    Vector3 tableFwd = tf.normalized;
                    if (Vector3.Dot(tableFwd, toKhufu.sqrMagnitude > 1e-4f ? toKhufu.normalized : -Vector3.forward) > 0.25f)
                        north = -tableFwd;
                    else
                        north = Vector3.forward;
                }
            }

            float tableNorthExtent = ExtentsAlong(table, north);
            Vector3 stand = table.center + north * (tableNorthExtent + StandNorthOfTableM);
            Vector3 tableProbe = new Vector3(stand.x, table.center.y, stand.z);
            if (table.Contains(tableProbe))
                stand += north * 1.5f;
            if (hasPlaza)
            {
                stand.x = Mathf.Clamp(stand.x, plaza.min.x + 0.6f, plaza.max.x - 0.6f);
                stand.z = Mathf.Clamp(stand.z, plaza.min.z + 0.6f, plaza.max.z - 0.6f);
                stand.y = plaza.max.y;
                _plazaY = plaza.max.y;
            }
            else
            {
                stand.y = table.min.y;
                if (table.size.y > 8f)
                    stand.y = originXf.position.y;
                _plazaY = stand.y;
            }

            Vector3 look = khufu - stand;
            look.y = 0f;
            if (look.sqrMagnitude < 0.04f)
                look = -north;
            Quaternion yaw = Quaternion.LookRotation(look.normalized, Vector3.up);

            CharacterController cc = _cc;
            if (cc != null)
                cc.enabled = false;
            originXf.SetPositionAndRotation(stand, yaw);
            if (cc != null)
                cc.enabled = true;

            Debug.Log(
                "LabPlayerSpawn: XR Origin at " + stand.ToString("F2") +
                " (lab plaza, north of table, facing Khufu). Floor tracking. Teleport rewired.");
            return true;
        }

        static void ResolveTable(out Bounds table, out Transform tableXf)
        {
            tableXf = FindNamedContains("circuittable");
            Transform board = FindNamedContains("breadboard");
            if (tableXf == null)
                tableXf = FindNamedContains("circuitlab");
            if (tableXf != null)
            {
                table = CollectBounds(tableXf);
                if (board != null)
                    table.Encapsulate(CollectBounds(board));
                return;
            }
            if (board != null)
            {
                tableXf = board;
                table = CollectBounds(board);
                return;
            }
            table = new Bounds(new Vector3(0.4f, 0.75f, 0.55f), new Vector3(2f, 1f, 2f));
        }

        static bool TryPlazaBounds(out Bounds plaza)
        {
            plaza = new Bounds();
            Transform plazaXf = FindNamedExact("LabPlaza");
            if (plazaXf == null)
                plazaXf = FindNamedContains("labplaza");
            if (plazaXf == null)
                return false;
            plaza = CollectColliderOrRenderer(plazaXf);
            return plaza.size.sqrMagnitude > 0.01f;
        }

        static Vector3 ResolveKhufuCenter(Vector3 fallback)
        {
            Transform marker = FindNamedExact("_GizaPose");
            if (marker != null)
                return marker.position;
            Transform khufu = FindNamedExact(KhufuPyramid.RootName);
            if (khufu != null)
                return khufu.position;
            return fallback + Vector3.back * 80f;
        }

        void WireLocomotion(Transform originXf)
        {
            _move = originXf.GetComponent<SmoothMovementController>();
            if (_move != null)
            {
                if (_move.tableCollider == null)
                {
                    Transform tableXf = FindNamedContains("circuittable");
                    if (tableXf != null)
                        _move.tableCollider = tableXf.GetComponent<Collider>();
                }
                if (_move.groundLayer.value == 0)
                    _move.groundLayer = ~0;
            }

#pragma warning disable CS0618
            _snap = originXf.GetComponent<DeviceBasedSnapTurnProvider>();
            if (_snap != null)
            {
                _snap.turnAmount = 45f;
                _snap.enableTurnLeftRight = true;
                _snap.enableTurnAround = true;
                List<XRBaseController> list = _snap.controllers;
                if (list == null)
                    list = new List<XRBaseController>();
                XRController[] controllers = originXf.GetComponentsInChildren<XRController>(true);
                for (int i = 0; i < controllers.Length; i++)
                {
                    XRController c = controllers[i];
                    if (c == null)
                        continue;
                    string n = c.gameObject.name;
                    if (string.IsNullOrEmpty(n))
                        continue;
                    if (n.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (n.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (!list.Contains(c))
                        list.Add(c);
                }
                _snap.controllers = list;
            }
#pragma warning restore CS0618

            _teleportCtrl = originXf.GetComponent<TeleportationController>();
            CollectLocomotionBehaviours(originXf);
        }

        void CollectLocomotionBehaviours(Transform originXf)
        {
            var list = new List<Behaviour>(4);
            if (_move != null)
                list.Add(_move);
            if (_snap != null)
                list.Add(_snap);
            if (_teleportCtrl != null)
                list.Add(_teleportCtrl);
            if (_teleport != null)
                list.Add(_teleport);
            _locomotion = list.ToArray();
        }

        void WireTeleport(Transform originXf)
        {
            _teleport = originXf.GetComponent<TeleportationProvider>();
            if (_teleport == null)
                _teleport = originXf.GetComponentInChildren<TeleportationProvider>(true);
            if (_teleport == null)
            {
                Debug.LogWarning("LabPlayerSpawn: TeleportationProvider missing on XR Origin; teleport areas stay unwired.");
                return;
            }

            EnsureTeleportAreaOn("LabPlaza");
            EnsureTeleportAreaOn("GizaPlateau");
            EnsureTeleportAreaOn("GizaDesert");
            EnsureTeleportAreaOn("GizaKhafreTerrace");
            AddTeleportOnNamedContains("causeway");
            AddTeleportOnNamedContains("temple");

            TeleportationArea[] areas = Object.FindObjectsByType<TeleportationArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] == null)
                    continue;
                if (areas[i].teleportationProvider != _teleport)
                    areas[i].teleportationProvider = _teleport;
                n++;
            }

            Transform offset = originXf.Find(CameraOffsetName);
            if (offset == null)
                return;

            Transform rightRay = FindChildNamed(offset, "Right Teleport Ray");
            Transform leftRay = FindChildNamed(offset, "Left Teleport Ray");
            if (leftRay == null && rightRay != null)
            {
                GameObject clone = Object.Instantiate(rightRay.gameObject, offset);
                clone.name = "Left Teleport Ray";
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
#pragma warning disable CS0618
                XRController xc = clone.GetComponent<XRController>();
                if (xc != null)
                    xc.controllerNode = XRNode.LeftHand;
#pragma warning restore CS0618
                leftRay = clone.transform;
            }
            else if (leftRay == null && rightRay == null)
                Debug.LogWarning("LabPlayerSpawn: no XRI teleport ray under Camera Offset after OVR strip; using remaining controllers.");

            if (_teleportCtrl != null)
            {
#pragma warning disable CS0618
                if (_teleportCtrl.rightTeleportRay == null && rightRay != null)
                    _teleportCtrl.rightTeleportRay = rightRay.GetComponent<XRController>();
                if (_teleportCtrl.leftTeleportRay == null && leftRay != null)
                    _teleportCtrl.leftTeleportRay = leftRay.GetComponent<XRController>();
#pragma warning restore CS0618
            }

            CollectLocomotionBehaviours(originXf);
            if (n > 0)
                Debug.Log("LabPlayerSpawn: wired " + n + " TeleportationArea(s) to TeleportationProvider.");
        }

        static void EnsureTeleportAreaOn(string name)
        {
            Transform t = FindNamedExact(name);
            if (t == null)
                return;
            Collider col = t.GetComponent<MeshCollider>();
            if (col == null)
                col = t.GetComponent<Collider>();
            if (col == null)
                return;
            if (t.GetComponent<TeleportationArea>() == null)
                t.gameObject.AddComponent<TeleportationArea>();
        }

        static void AddTeleportOnNamedContains(string token)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || string.IsNullOrEmpty(t.name))
                    continue;
                if (t.name.ToLowerInvariant().IndexOf(token) < 0)
                    continue;
                Collider col = t.GetComponent<Collider>();
                if (col == null)
                    continue;
                if (t.GetComponent<TeleportationArea>() == null)
                    t.gameObject.AddComponent<TeleportationArea>();
            }
        }

        void WireHands(Transform originXf)
        {
            Transform offset = originXf.Find(CameraOffsetName);
            if (offset == null)
                return;

            Transform left = FindChildNamed(offset, "Left Hand");
            Transform right = FindChildNamed(offset, "Right Hand");
            if (left == null)
                Debug.LogWarning("LabPlayerSpawn: Left Hand missing under Camera Offset after OVR strip; using remaining XRI controllers.");
            if (right == null)
                Debug.LogWarning("LabPlayerSpawn: Right Hand missing under Camera Offset after OVR strip; using remaining XRI controllers.");

            SetDirectHand(left, InteractorHandedness.Left);
            SetDirectHand(right, InteractorHandedness.Right);

            bool leftRay = HasRay(offset, left);
            bool rightRay = HasRay(offset, right);
            if (!leftRay)
                Debug.LogWarning("LabPlayerSpawn: Left Ray missing under Camera Offset; not rebuilding OVR.");
            if (!rightRay)
                Debug.LogWarning("LabPlayerSpawn: Right Ray missing under Camera Offset; not rebuilding OVR.");
        }

        static void SetDirectHand(Transform hand, InteractorHandedness side)
        {
            if (hand == null)
                return;
            XRDirectInteractor direct = hand.GetComponent<XRDirectInteractor>();
            if (direct == null)
                return;
            direct.handedness = side;
        }

        static bool HasRay(Transform offset, Transform hand)
        {
            if (offset != null)
            {
                XRRayInteractor[] rays = offset.GetComponentsInChildren<XRRayInteractor>(true);
                for (int i = 0; i < rays.Length; i++)
                {
                    if (rays[i] == null)
                        continue;
                    if (hand != null && rays[i].transform.IsChildOf(hand))
                        return true;
                    string n = rays[i].gameObject.name;
                    bool wantLeft = hand != null && hand.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (wantLeft && n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    if (!wantLeft && n.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            if (hand != null && hand.GetComponent<XRRayInteractor>() != null)
                return true;
            return false;
        }

        void ApplyTrackingPause()
        {
            bool valid = HeadTrackingValid();
            if (valid)
            {
                if (_locomotionPaused)
                    SetLocomotionEnabled(true);
                _locomotionPaused = false;
                return;
            }

            if (!_locomotionPaused)
                SetLocomotionEnabled(false);
            _locomotionPaused = true;
        }

        void SetLocomotionEnabled(bool on)
        {
            if (_locomotion == null)
                return;
            for (int i = 0; i < _locomotion.Length; i++)
            {
                if (_locomotion[i] != null)
                    _locomotion[i].enabled = on;
            }
        }

        public static bool HeadTrackingValid()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid)
                return true;
            InputTrackingState state;
            if (head.TryGetFeatureValue(CommonUsages.trackingState, out state))
            {
                if ((state & InputTrackingState.Position) == 0 && (state & InputTrackingState.Rotation) == 0)
                    return false;
            }
            bool tracked;
            if (head.TryGetFeatureValue(CommonUsages.isTracked, out tracked) && !tracked)
                return false;
            return true;
        }

        static Transform FindChildNamed(Transform parent, string name)
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

        static Transform FindNamedExact(string name)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        static Transform FindNamedContains(string token)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || string.IsNullOrEmpty(all[i].name))
                    continue;
                if (all[i].name.ToLowerInvariant().Contains(token))
                    return all[i];
            }
            return null;
        }

        static Bounds CollectBounds(Transform root)
        {
            return CollectColliderOrRenderer(root);
        }

        static Bounds CollectColliderOrRenderer(Transform root)
        {
            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            bool any = false;
            Bounds b = new Bounds(root.position, Vector3.one * 0.1f);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || !cols[i].enabled)
                    continue;
                if (!any)
                {
                    b = cols[i].bounds;
                    any = true;
                }
                else
                    b.Encapsulate(cols[i].bounds);
            }
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null || !rs[i].enabled)
                    continue;
                if (!any)
                {
                    b = rs[i].bounds;
                    any = true;
                }
                else
                    b.Encapsulate(rs[i].bounds);
            }
            if (!any)
                return new Bounds(root.position, Vector3.one);
            return b;
        }

        static float ExtentsAlong(Bounds b, Vector3 dir)
        {
            Vector3 e = b.extents;
            Vector3 d = dir.normalized;
            return Mathf.Abs(d.x) * e.x + Mathf.Abs(d.y) * e.y + Mathf.Abs(d.z) * e.z;
        }
    }
}
