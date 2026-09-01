using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Visualization;

namespace RealityEngine.Physics.Thermo
{
    /// <summary>
    /// Lumped hot or cold reservoir. Classical toy temperature only — not a thermal mesh.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(38)]
    public sealed class HeatReservoir : MonoBehaviour
    {
        public const string Honesty = ThermoEnergy.Honesty;

        [SerializeField]
        bool isHot = true;

        [SerializeField]
        float temperature = ThermoEnergy.DefaultTHot;

        Transform _human;
        TextMeshPro _caption;
        Camera _camera;
        bool _built;
        int _layer;
        int _scale;
        Material _mat;

        public bool IsHot => isHot;
        public float Temperature
        {
            get => temperature;
            set => temperature = Mathf.Max(1f, value);
        }

        public void Configure(bool hot, float kelvin)
        {
            isHot = hot;
            temperature = Mathf.Max(1f, kelvin);
            EnsureBuilt();
            ApplyView(_layer, _scale);
        }

        public Bounds WorldBounds
        {
            get
            {
                Renderer r = GetComponentInChildren<Renderer>();
                if (r != null)
                    return r.bounds;
                Collider c = GetComponent<Collider>();
                if (c != null)
                    return c.bounds;
                return new Bounds(transform.position, Vector3.one * 0.08f);
            }
        }

        public void EnsureBuilt()
        {
            CacheIfNeeded();
            if (!_built)
            {
                if (_human == null)
                    BuildAll();
                EnsureBody();
                _built = true;
            }
            ApplyView(_layer, _scale);
        }

        public void ApplyView(int layer, int scale)
        {
            _layer = Mathf.Clamp(layer, 0, FieldLens.LayerCount - 1);
            _scale = Mathf.Clamp(scale, 0, ScaleEngine.StepCount - 1);
            if (!_built)
                EnsureBuilt();

            FieldLensLayer L = (FieldLensLayer)_layer;
            ScaleLevel S = (ScaleLevel)_scale;
            bool emParked = L == FieldLensLayer.Electric || L == FieldLensLayer.Magnetic;
            bool molParked = S == ScaleLevel.Molecular || S == ScaleLevel.Atomic || L == FieldLensLayer.Atomic;
            UpdateCaption(L, S, emParked, molParked);
        }

        void Awake()
        {
            _camera = Camera.main;
            EnsureBuilt();
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
            if (Time.unscaledTime % 0.25f < Time.unscaledDeltaTime)
                ApplyView(_layer, _scale);
        }

        void OnDestroy()
        {
            DestroyMat(_mat);
        }

        void CacheIfNeeded()
        {
            if (_human == null)
            {
                Transform t = transform.Find("HumanView");
                if (t != null)
                    _human = t;
            }
            if (_caption == null)
            {
                Transform t = transform.Find("HeatCaption");
                if (t != null)
                    _caption = t.GetComponent<TextMeshPro>();
            }
        }

        void BuildAll()
        {
            Color col = isHot
                ? new Color(0.52f, 0.16f, 0.08f)
                : new Color(0.22f, 0.30f, 0.38f);
            float metallic = isHot ? 0.32f : 0.86f;
            float smooth = isHot ? 0.26f : 0.44f;
            _mat = MakeLit(col, metallic, smooth);

            _human = NewRoot("HumanView");
            Primitive(_human, PrimitiveType.Sphere, "Reservoir", Vector3.zero, Vector3.one * 0.08f, _mat);

            var capGo = new GameObject("HeatCaption");
            capGo.transform.SetParent(transform, false);
            capGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            capGo.transform.localScale = Vector3.one * 0.016f;
            _caption = capGo.AddComponent<TextMeshPro>();
            _caption.fontSize = 5f;
            _caption.alignment = TextAlignmentOptions.Center;
            _caption.color = isHot ? new Color(0.95f, 0.72f, 0.48f) : new Color(0.72f, 0.84f, 0.95f);
            _caption.rectTransform.sizeDelta = new Vector2(18f, 8f);
            _caption.raycastTarget = false;
            _caption.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _caption.font = font;
        }

        void EnsureBody()
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.042f;
            sphere.center = Vector3.zero;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.20f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearDamping = 1.0f;
            rb.angularDamping = 1.0f;
        }

        void UpdateCaption(FieldLensLayer L, ScaleLevel S, bool emParked, bool molParked)
        {
            if (_caption == null)
                return;
            string role = isHot ? "HOT" : "COLD";
            string line = role + "  " + temperature.ToString("0.0") + " K  reservoir\n" + Honesty;
            if (emParked)
                line += "\nnot an EM source";
            else if (molParked)
                line += "\nnot a molecular sim";
            else if (L == FieldLensLayer.Material || S == ScaleLevel.Material)
                line += "\nhot / cold bodies";
            else if (L == FieldLensLayer.Charge || L == FieldLensLayer.EnergyFlow)
                line += "\nCharge/Energy bars live on the coupler.";
            else if (L == FieldLensLayer.Mathematical)
                line += "\nMath lives on the coupler: Q_in, eta_toy, W, Q_c.";
            _caption.text = line;
        }

        Transform NewRoot(string name)
        {
            Transform existing = transform.Find(name);
            if (existing != null)
                return existing;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
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
            KillCollider(go);
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static void KillCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(c);
            else
                Object.DestroyImmediate(c);
        }

        static Material MakeLit(Color color, float metallic, float smoothness)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
            {
                s = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s != null)
                    Debug.LogWarning("HeatReservoir: URP Lit not found; using Simple Lit.");
            }
            if (s == null)
                s = Shader.Find("Standard");
            if (s == null)
                s = Shader.Find("Sprites/Default");
            var mat = new Material(s)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            return mat;
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
