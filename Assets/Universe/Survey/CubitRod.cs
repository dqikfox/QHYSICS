using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using RealityEngine.Visualization;

namespace RealityEngine.Survey
{
    /// <summary>
    /// Grabbable royal-cubit rod (0.5236 m). Kinematic XR grab. When held, a thin
    /// ray from the rod measures live lengths on Khufu colliders in meters and cubits.
    /// Tiny TMP readout is parented to the rod — not a giant board.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(41)]
    public sealed class CubitRod : MonoBehaviour
    {
        public const string RootName = "CubitRod";
        public const float LengthMeters = KhufuPyramid.Cubit;
        public const string Honesty =
            "Royal cubit 0.5236 m. Reconstructed original (Petrie/Lehner). Not a scan.";

        static CubitRod _live;
        static float _lastMeters;
        static string _lastHit = "";
        static bool _hasMeasurement;

        XRGrabInteractable _grab;
        LineRenderer _ray;
        TextMeshPro _readout;
        Camera _camera;
        Material _wood;
        Material _copper;
        Material _rayMat;
        bool _built;
        readonly RaycastHit[] _hits = new RaycastHit[24];

        public static float LastMeters => _lastMeters;
        public static float LastCubits => _lastMeters / LengthMeters;
        public static string LastHitName => _lastHit;
        public static bool HasMeasurement => _hasMeasurement;

        public static string DescribeLast()
        {
            if (!_hasMeasurement)
                return "no live rod hit yet";
            return _lastMeters.ToString("0.000") + " m = " + (_lastMeters / LengthMeters).ToString("0.00")
                + " cubits on " + _lastHit;
        }

        public void EnsureBuilt()
        {
            if (!_built)
            {
                BuildVisual();
                EnsureGrab();
                BuildRay();
                BuildReadout();
                _built = true;
            }
            _live = this;
            _camera = Camera.main;
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            _live = this;
        }

        void OnDisable()
        {
            if (_live == this)
                _live = null;
        }

        void Update()
        {
            bool held = _grab != null && _grab.isSelected;
            if (held)
                Measure();
            else
                HideRay();
            UpdateReadout();
        }

        void LateUpdate()
        {
            if (_readout == null)
                return;
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;
            Vector3 toCam = _readout.transform.position - _camera.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-6f)
                _readout.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        void OnDestroy()
        {
            if (_live == this)
                _live = null;
            DestroyMat(_wood);
            DestroyMat(_copper);
            DestroyMat(_rayMat);
        }

        void Measure()
        {
            Vector3 axis = transform.up;
            Vector3 origin = transform.position + axis * (LengthMeters * 0.48f);
            int n = Physics.RaycastNonAlloc(origin, axis, _hits, 80f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            RaycastHit chosen = default;
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null)
                    continue;
                if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform))
                    continue;
                if (!IsKhufuHit(h.collider.transform))
                    continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    chosen = h;
                    any = true;
                }
            }

            if (!any)
            {
                HideRay();
                return;
            }

            _lastMeters = chosen.distance;
            _lastHit = chosen.collider.gameObject.name;
            _hasMeasurement = true;
            ShowRay(origin, chosen.point);
        }

        static bool IsKhufuHit(Transform t)
        {
            while (t != null)
            {
                string n = t.name;
                if (!string.IsNullOrEmpty(n) && (n == KhufuPyramid.RootName || n.StartsWith("Khufu", System.StringComparison.Ordinal)))
                    return true;
                t = t.parent;
            }
            return false;
        }

        void ShowRay(Vector3 a, Vector3 b)
        {
            if (_ray == null)
                return;
            _ray.enabled = true;
            _ray.positionCount = 2;
            _ray.SetPosition(0, a);
            _ray.SetPosition(1, b);
        }

        void HideRay()
        {
            if (_ray != null)
                _ray.enabled = false;
        }

        void UpdateReadout()
        {
            if (_readout == null)
                return;
            string body = "ROYAL CUBIT\n" + LengthMeters.ToString("0.0000") + " m  =  1 cubit\n";
            if (_hasMeasurement)
                body += "last  " + DescribeLast();
            else
                body += "aim the tip at Khufu";
            _readout.text = body;
        }

        void BuildVisual()
        {
            _wood = LabWorldMeshes.MakeLit("RELab_CubitWood", new Color(0.42f, 0.26f, 0.12f), 0.04f, 0.28f, false);
            _copper = LabWorldMeshes.MakeLit("RELab_CubitCopper", new Color(0.72f, 0.45f, 0.20f), 0.62f, 0.48f, false);

            float len = LengthMeters;
            float rad = 0.011f;
            Primitive(transform, PrimitiveType.Cylinder, "Shaft", Vector3.zero, new Vector3(rad * 2f, len * 0.5f, rad * 2f), _wood);
            Primitive(transform, PrimitiveType.Cylinder, "CapN", new Vector3(0f, len * 0.42f, 0f), new Vector3(rad * 2.15f, len * 0.06f, rad * 2.15f), _copper);
            Primitive(transform, PrimitiveType.Cylinder, "CapS", new Vector3(0f, -len * 0.42f, 0f), new Vector3(rad * 2.15f, len * 0.06f, rad * 2.15f), _copper);

            const int marks = 7;
            for (int i = 1; i < marks; i++)
            {
                float y = -len * 0.5f + (i / (float)marks) * len;
                Primitive(transform, PrimitiveType.Cylinder, "Palm_" + i, new Vector3(0f, y, 0f), new Vector3(rad * 2.25f, 0.0025f, rad * 2.25f), _copper);
            }
        }

        void EnsureGrab()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = LengthMeters;
            capsule.radius = 0.016f;
            capsule.center = Vector3.zero;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.18f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearDamping = 1.2f;
            rb.angularDamping = 1.4f;

            _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                _grab = gameObject.AddComponent<XRGrabInteractable>();
            _grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            _grab.throwOnDetach = false;
            _grab.useDynamicAttach = true;
            _grab.selectMode = InteractableSelectMode.Single;
        }

        void BuildRay()
        {
            Transform existing = transform.Find("MeasureRay");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("MeasureRay");
                go.transform.SetParent(transform, false);
            }

            _ray = go.GetComponent<LineRenderer>();
            if (_ray == null)
                _ray = go.AddComponent<LineRenderer>();
            _rayMat = LabWorldMeshes.MakeLit("RELab_CubitRay", new Color(0.95f, 0.78f, 0.22f), 0.08f, 0.35f, true);
            if (_rayMat != null && _rayMat.HasProperty("_EmissionColor"))
                _rayMat.SetColor("_EmissionColor", new Color(0.55f, 0.38f, 0.06f));
            if (_rayMat != null)
                _ray.sharedMaterial = _rayMat;
            _ray.widthMultiplier = 0.004f;
            _ray.useWorldSpace = true;
            _ray.shadowCastingMode = ShadowCastingMode.Off;
            _ray.receiveShadows = false;
            _ray.positionCount = 2;
            _ray.enabled = false;
        }

        void BuildReadout()
        {
            Transform existing = transform.Find("RodReadout");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("RodReadout");
                go.transform.SetParent(transform, false);
            }
            go.transform.localPosition = new Vector3(0.038f, 0.08f, 0f);
            go.transform.localScale = Vector3.one * 0.012f;
            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 5.5f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.95f, 0.88f, 0.62f);
            _readout.rectTransform.sizeDelta = new Vector2(18f, 6f);
            _readout.raycastTarget = false;
            _readout.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _readout.font = font;
            _readout.text = "ROYAL CUBIT";
        }

        static void Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(col);
                else
                    Object.DestroyImmediate(col);
            }
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static void DestroyMat(Material mat)
        {
            if (mat == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(mat);
            else
                Object.DestroyImmediate(mat);
        }
    }
}
