using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Visualization;

namespace RealityEngine.Physics.Cosmos
{
    /// <summary>
    /// Grabbable tabletop cosmos toy. ScaleEngine swaps Room / Planetary / Solar schematic views.
    /// Classical toy only — not N-body, not GR, not photogrammetry. Never moves XR Origin / camera.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class CosmosToy : MonoBehaviour
    {
        public const string RootName = "CosmosToy";

        ScaleEngine _engine;
        Transform _idle;
        Transform _room;
        Transform _planetary;
        Transform _solar;
        Transform _orbit;
        TextMeshPro _caption;
        Camera _camera;
        bool _built;
        int _scale;
        bool _solarActive;
        Material _matIdle;
        Material _matTable;
        Material _matProp;
        Material _matEarth;
        Material _matGiza;
        Material _matSun;

        public int Scale => _scale;

        public void Configure(ScaleEngine engine)
        {
            if (_engine != null)
                _engine.ScaleChanged -= OnScaleChanged;
            _engine = engine;
            if (_engine != null)
            {
                _engine.ScaleChanged += OnScaleChanged;
                ApplyScale(_engine.CurrentScale);
            }
        }

        public void EnsureBuilt()
        {
            CacheIfNeeded();
            if (!_built)
            {
                if (_idle == null)
                    BuildAll();
                EnsureGrab();
                _built = true;
            }
            ApplyScale(_scale);
        }

        public void ApplyScale(int scale)
        {
            _scale = ScaleEngine.ClampScale(scale);
            if (!_built)
                EnsureBuilt();

            bool cosmos = ScaleEngine.IsCosmos(_scale);
            ScaleLevel L = (ScaleLevel)_scale;
            bool room = cosmos && L == ScaleLevel.Room;
            bool planetary = cosmos && L == ScaleLevel.Planetary;
            bool solar = cosmos && L == ScaleLevel.Solar;
            bool idle = !cosmos;

            SetOn(_idle, idle);
            SetOn(_room, room);
            SetOn(_planetary, planetary);
            SetOn(_solar, solar);
            _solarActive = solar;
            UpdateCaption(L, cosmos);
        }

        void OnScaleChanged(int scale)
        {
            ApplyScale(scale);
        }

        void Awake()
        {
            _camera = Camera.main;
            EnsureBuilt();
        }

        void Update()
        {
            if (!_solarActive || _orbit == null)
                return;
            // Toy Kepler: slow circular orbit of Earth around Sun. Schematic only.
            _orbit.Rotate(0f, 28f * Time.deltaTime, 0f, Space.Self);
        }

        void LateUpdate()
        {
            if (_caption == null)
                return;
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;
            Vector3 toCam = _caption.transform.position - _camera.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-6f)
                _caption.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        void OnDisable()
        {
            if (_engine != null)
                _engine.ScaleChanged -= OnScaleChanged;
        }

        void OnEnable()
        {
            if (_engine != null)
            {
                _engine.ScaleChanged -= OnScaleChanged;
                _engine.ScaleChanged += OnScaleChanged;
                ApplyScale(_engine.CurrentScale);
            }
        }

        void OnDestroy()
        {
            if (_engine != null)
                _engine.ScaleChanged -= OnScaleChanged;
            DestroyMat(_matIdle);
            DestroyMat(_matTable);
            DestroyMat(_matProp);
            DestroyMat(_matEarth);
            DestroyMat(_matGiza);
            DestroyMat(_matSun);
        }

        void CacheIfNeeded()
        {
            if (_idle == null)
            {
                Transform t = transform.Find("IdleView");
                if (t != null)
                    _idle = t;
            }
            if (_room == null)
            {
                Transform t = transform.Find("RoomView");
                if (t != null)
                    _room = t;
            }
            if (_planetary == null)
            {
                Transform t = transform.Find("PlanetaryView");
                if (t != null)
                    _planetary = t;
            }
            if (_solar == null)
            {
                Transform t = transform.Find("SolarView");
                if (t != null)
                    _solar = t;
            }
            if (_orbit == null && _solar != null)
            {
                Transform t = _solar.Find("EarthOrbit");
                if (t != null)
                    _orbit = t;
            }
            if (_caption == null)
            {
                Transform t = transform.Find("CosmosCaption");
                if (t != null)
                    _caption = t.GetComponent<TextMeshPro>();
            }
        }

        void BuildAll()
        {
            _matIdle = MakeLit("CosmosIdle", new Color(0.18f, 0.20f, 0.24f), 0.12f, 0.35f, false);
            _matTable = MakeLit("CosmosTable", new Color(0.42f, 0.30f, 0.18f), 0.08f, 0.28f, false);
            _matProp = MakeLit("CosmosProp", new Color(0.55f, 0.38f, 0.22f), 0.55f, 0.40f, false);
            _matEarth = MakeLit("CosmosEarth", new Color(0.18f, 0.42f, 0.78f), 0.05f, 0.45f, false);
            _matGiza = MakeLit("CosmosGiza", new Color(0.78f, 0.68f, 0.42f), 0.04f, 0.30f, false);
            _matSun = MakeLit("CosmosSun", new Color(1f, 0.82f, 0.22f), 0.10f, 0.55f, true);
            if (_matSun != null && _matSun.HasProperty("_EmissionColor"))
                _matSun.SetColor("_EmissionColor", new Color(1.4f, 0.95f, 0.25f));

            _idle = NewRoot("IdleView");
            Primitive(_idle, PrimitiveType.Sphere, "IdleCore", Vector3.zero, Vector3.one * 0.05f, _matIdle);
            MakeLabel(_idle, "IdleHint", "Scale - : Room / Planet / Solar", new Vector3(0f, 0.07f, 0f), 0.014f, new Vector2(18f, 4f), new Color(0.80f, 0.90f, 1f));

            _room = NewRoot("RoomView");
            Primitive(_room, PrimitiveType.Cube, "TableSlab", new Vector3(0f, -0.01f, 0f), new Vector3(0.14f, 0.012f, 0.10f), _matTable);
            Primitive(_room, PrimitiveType.Cube, "MagnetStandIn", new Vector3(-0.035f, 0.015f, 0f), new Vector3(0.022f, 0.030f, 0.022f), _matProp);
            Primitive(_room, PrimitiveType.Cube, "CoilStandIn", new Vector3(0.035f, 0.012f, 0f), new Vector3(0.028f, 0.024f, 0.028f), _matProp);

            _planetary = NewRoot("PlanetaryView");
            Primitive(_planetary, PrimitiveType.Sphere, "Earth", Vector3.zero, Vector3.one * 0.08f, _matEarth);
            // Tiny pyramid-ish cube on the surface as a Giza marker (schematic, not photogrammetry).
            Primitive(_planetary, PrimitiveType.Cube, "GizaMarker", new Vector3(0f, 0.042f, 0f), new Vector3(0.012f, 0.010f, 0.012f), _matGiza);

            _solar = NewRoot("SolarView");
            Primitive(_solar, PrimitiveType.Sphere, "Sun", Vector3.zero, Vector3.one * 0.05f, _matSun);
            var orbitGo = new GameObject("EarthOrbit");
            orbitGo.transform.SetParent(_solar, false);
            orbitGo.transform.localPosition = Vector3.zero;
            orbitGo.transform.localRotation = Quaternion.identity;
            _orbit = orbitGo.transform;
            Primitive(_orbit, PrimitiveType.Sphere, "Earth", new Vector3(0.12f, 0f, 0f), Vector3.one * 0.02f, _matEarth);

            var capGo = new GameObject("CosmosCaption");
            capGo.transform.SetParent(transform, false);
            capGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            capGo.transform.localScale = Vector3.one * 0.018f;
            _caption = capGo.AddComponent<TextMeshPro>();
            _caption.fontSize = 5f;
            _caption.alignment = TextAlignmentOptions.Center;
            _caption.color = new Color(0.85f, 0.92f, 1f);
            _caption.rectTransform.sizeDelta = new Vector2(22f, 8f);
            _caption.raycastTarget = false;
            _caption.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _caption.font = font;
        }

        void EnsureGrab()
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.07f;
            sphere.center = Vector3.zero;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.08f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearDamping = 0.8f;
            rb.angularDamping = 1.0f;

            if (GetComponent<XRGrabInteractable>() == null)
            {
                var grab = gameObject.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = true;
                grab.useDynamicAttach = true;
                grab.selectMode = InteractableSelectMode.Single;
            }
        }

        void UpdateCaption(ScaleLevel L, bool cosmos)
        {
            if (_caption == null)
                return;
            string line = "CosmosToy — classical schematic\n" + ScaleEngine.HonestyOf(_scale);
            if (!cosmos)
                line += "\nScale - for Room / Planetary / Solar";
            else if (L == ScaleLevel.Room)
                line += "\nLab table + local objects. PARKED: architecture";
            else if (L == ScaleLevel.Planetary)
                line += "\nToy Earth + Giza marker. PARKED: geodesy / N-body";
            else if (L == ScaleLevel.Solar)
                line += "\nToy Kepler (Sun + Earth). PARKED: N-body / GR / ephemeris";
            _caption.text = line;
        }

        Transform NewRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static void Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null)
                r.sharedMaterial = mat;
        }

        static void MakeLabel(Transform parent, string name, string text, Vector3 localPos, float scale, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = size;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.text = text;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
        }

        static void SetOn(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on)
                t.gameObject.SetActive(on);
        }

        static Material MakeLit(string name, Color color, float metallic, float smoothness, bool emission)
        {
            return LabWorldMeshes.MakeLit(name, color, metallic, smoothness, emission);
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
