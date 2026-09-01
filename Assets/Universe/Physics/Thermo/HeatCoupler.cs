using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Visualization;

namespace RealityEngine.Physics.Thermo
{
    /// <summary>
    /// Grabbable cylinder turbine/path. When both ends sit near hot and cold, the toy heat path closes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(42)]
    public sealed class HeatCoupler : MonoBehaviour
    {
        public const string Honesty = ThermoEnergy.Honesty;
        const float CloseThreshold = 0.11f;

        HeatReservoir _hot;
        HeatReservoir _cold;
        Transform _human;
        Transform _energy;
        Transform _math;
        Transform _body;
        Transform _barIn;
        Transform _barCap;
        Transform _barLoss;
        TextMeshPro _caption;
        TextMeshPro _account;
        TextMeshPro _mathLabel;
        Camera _camera;
        bool _built;
        bool _pathClosed;
        float _qin;
        float _eta;
        float _captured;
        float _losses;
        int _layer;
        int _scale;
        Material _matCoupler;
        Material _matIn;
        Material _matCap;
        Material _matLoss;

        public bool PathClosed => _pathClosed;
        public float QinPerSecond => _qin;
        public float Eta => _eta;
        public float CapturedPerSecond => _captured;
        public float LossesPerSecond => _losses;
        public float THot => _hot != null ? _hot.Temperature : ThermoEnergy.DefaultTHot;
        public float TCold => _cold != null ? _cold.Temperature : ThermoEnergy.DefaultTCold;
        public HeatReservoir Hot => _hot;
        public HeatReservoir Cold => _cold;

        public void Bind(HeatReservoir hot, HeatReservoir cold)
        {
            _hot = hot;
            _cold = cold;
        }

        public void ApplyTemperatures(float tHot, float tCold)
        {
            ResolveReservoirs();
            if (_hot != null)
                _hot.Temperature = tHot;
            if (_cold != null)
                _cold.Temperature = tCold;
        }

        public void SnapBetweenReservoirs()
        {
            ResolveReservoirs();
            if (_hot == null || _cold == null)
                return;
            Vector3 a = _hot.transform.position;
            Vector3 b = _cold.transform.position;
            transform.position = (a + b) * 0.5f;
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f)
                d = Vector3.forward;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        }

        public Bounds WorldBounds
        {
            get
            {
                Renderer r = null;
                if (_body != null)
                    r = _body.GetComponent<Renderer>();
                if (r == null)
                    r = GetComponentInChildren<Renderer>();
                if (r != null)
                    return r.bounds;
                Collider c = GetComponent<Collider>();
                if (c != null)
                    return c.bounds;
                return new Bounds(transform.position, new Vector3(0.04f, 0.20f, 0.04f));
            }
        }

        public void EnsureBuilt()
        {
            CacheIfNeeded();
            if (!_built)
            {
                if (_human == null)
                    BuildAll();
                EnsureGrab();
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
            bool energy = !emParked && (L == FieldLensLayer.Charge || L == FieldLensLayer.EnergyFlow);
            bool math = !emParked && L == FieldLensLayer.Mathematical;

            SetOn(_human, true);
            SetOn(_energy, energy);
            SetOn(_math, math);
            UpdateCaption(L, S, emParked, molParked, energy, math);
        }

        void Awake()
        {
            _camera = Camera.main;
            EnsureBuilt();
        }

        void Update()
        {
            Simulate();
            UpdateBars();
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

        void OnDestroy()
        {
            DestroyMat(_matCoupler);
            DestroyMat(_matIn);
            DestroyMat(_matCap);
            DestroyMat(_matLoss);
        }

        void Simulate()
        {
            ResolveReservoirs();
            _pathClosed = ComputeClosed();
            ThermoEnergy.Account(THot, TCold, _pathClosed, out _qin, out _eta, out _captured, out _losses);
            if (_pathClosed)
                Flatten(Time.deltaTime);
        }

        void Flatten(float dt)
        {
            if (_hot == null || _cold == null || dt <= 0f)
                return;
            float th = _hot.Temperature;
            float tc = _cold.Temperature;
            float dT = th - tc;
            if (dT <= ThermoEnergy.MinDeltaK)
                return;
            float step = ThermoEnergy.FlattenKelvinPerSecond * dt;
            float maxStep = (dT - ThermoEnergy.MinDeltaK) * 0.5f;
            if (step > maxStep)
                step = maxStep;
            if (step < 0f)
                step = 0f;
            _hot.Temperature = th - step;
            _cold.Temperature = tc + step;
        }

        bool ComputeClosed()
        {
            if (_hot == null || _cold == null)
                return false;
            Bounds hotB = _hot.WorldBounds;
            Bounds coldB = _cold.WorldBounds;
            GetEnds(out Vector3 a, out Vector3 b);
            float aHot = DistToBounds(a, hotB);
            float aCold = DistToBounds(a, coldB);
            float bHot = DistToBounds(b, hotB);
            float bCold = DistToBounds(b, coldB);
            bool pair = (aHot <= CloseThreshold && bCold <= CloseThreshold)
                        || (aCold <= CloseThreshold && bHot <= CloseThreshold);
            Bounds self = WorldBounds;
            self.Expand(0.05f);
            bool overlapBoth = self.Intersects(hotB) && self.Intersects(coldB);
            return pair || overlapBoth;
        }

        void GetEnds(out Vector3 a, out Vector3 b)
        {
            Transform axis = _body != null ? _body : transform;
            a = axis.TransformPoint(Vector3.up);
            b = axis.TransformPoint(Vector3.down);
        }

        static float DistToBounds(Vector3 p, Bounds b)
        {
            return Vector3.Distance(p, b.ClosestPoint(p));
        }

        void ResolveReservoirs()
        {
            if (_hot == null || _cold == null)
            {
                Transform root = transform.parent != null ? transform.parent : transform;
                if (_hot == null)
                {
                    Transform t = root.Find("HeatHot");
                    if (t != null)
                        _hot = t.GetComponent<HeatReservoir>();
                }
                if (_cold == null)
                {
                    Transform t = root.Find("HeatCold");
                    if (t != null)
                        _cold = t.GetComponent<HeatReservoir>();
                }
            }
            if (_hot == null || _cold == null)
            {
                HeatReservoir[] all = FindObjectsByType<HeatReservoir>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null)
                        continue;
                    if (all[i].IsHot)
                    {
                        if (_hot == null)
                            _hot = all[i];
                    }
                    else if (_cold == null)
                        _cold = all[i];
                }
            }
        }

        void UpdateBars()
        {
            float refQ = Mathf.Max(1f, ThermoEnergy.ConductanceK * 100f);
            float hIn = 0.02f + 0.10f * Mathf.Clamp01(_qin / refQ);
            float hCap = 0.02f + 0.10f * Mathf.Clamp01(_captured / refQ);
            float hLoss = 0.02f + 0.10f * Mathf.Clamp01(_losses / refQ);
            SetBar(_barIn, hIn);
            SetBar(_barCap, hCap);
            SetBar(_barLoss, hLoss);
            if (_account != null)
                _account.text = ThermoEnergy.AccountLines(_qin, _eta, _captured, _losses, _pathClosed);
            if (_mathLabel != null)
                _mathLabel.text = ThermoEnergy.MathLines(THot, TCold, _qin, _eta, _captured, _losses);
        }

        static void SetBar(Transform bar, float height)
        {
            if (bar == null)
                return;
            Vector3 s = bar.localScale;
            s.y = height;
            bar.localScale = s;
            Vector3 p = bar.localPosition;
            p.y = height * 0.5f;
            bar.localPosition = p;
        }

        void CacheIfNeeded()
        {
            if (_human == null)
            {
                Transform t = transform.Find("HumanView");
                if (t != null)
                    _human = t;
            }
            if (_energy == null)
            {
                Transform t = transform.Find("HeatEnergy");
                if (t != null)
                    _energy = t;
            }
            if (_math == null)
            {
                Transform t = transform.Find("HeatMath");
                if (t != null)
                    _math = t;
            }
            if (_body == null && _human != null)
            {
                Transform t = _human.Find("Body");
                if (t != null)
                    _body = t;
            }
            if (_caption == null)
            {
                Transform t = transform.Find("HeatCaption");
                if (t != null)
                    _caption = t.GetComponent<TextMeshPro>();
            }
            if (_account == null && _energy != null)
            {
                Transform t = _energy.Find("Account");
                if (t != null)
                    _account = t.GetComponent<TextMeshPro>();
            }
            if (_mathLabel == null && _math != null)
            {
                Transform t = _math.Find("Equations");
                if (t != null)
                    _mathLabel = t.GetComponent<TextMeshPro>();
            }
            if (_barIn == null && _energy != null)
            {
                Transform t = _energy.Find("BarInput");
                if (t != null)
                    _barIn = t;
            }
            if (_barCap == null && _energy != null)
            {
                Transform t = _energy.Find("BarCaptured");
                if (t != null)
                    _barCap = t;
            }
            if (_barLoss == null && _energy != null)
            {
                Transform t = _energy.Find("BarLosses");
                if (t != null)
                    _barLoss = t;
            }
        }

        void BuildAll()
        {
            _matCoupler = MakeLit(new Color(0.46f, 0.30f, 0.12f), 0.92f, 0.42f);
            _matIn = MakeLit(new Color(0.22f, 0.70f, 0.38f), 0.08f, 0.35f);
            _matCap = MakeLit(new Color(0.88f, 0.72f, 0.18f), 0.08f, 0.35f);
            _matLoss = MakeLit(new Color(0.78f, 0.22f, 0.18f), 0.08f, 0.35f);

            _human = NewRoot("HumanView");
            _body = Primitive(_human, PrimitiveType.Cylinder, "Body", Vector3.zero, new Vector3(0.028f, 0.10f, 0.028f), _matCoupler).transform;
            Primitive(_human, PrimitiveType.Cylinder, "Turbine", Vector3.zero, new Vector3(0.055f, 0.012f, 0.055f), _matCoupler);

            _energy = NewRoot("HeatEnergy");
            _barIn = Primitive(_energy, PrimitiveType.Cube, "BarInput", new Vector3(-0.04f, 0.04f, 0.05f), new Vector3(0.016f, 0.08f, 0.016f), _matIn).transform;
            _barCap = Primitive(_energy, PrimitiveType.Cube, "BarCaptured", new Vector3(0f, 0.02f, 0.05f), new Vector3(0.016f, 0.04f, 0.016f), _matCap).transform;
            _barLoss = Primitive(_energy, PrimitiveType.Cube, "BarLosses", new Vector3(0.04f, 0.03f, 0.05f), new Vector3(0.016f, 0.06f, 0.016f), _matLoss).transform;
            _account = MakeLabel(_energy, "Account", ThermoEnergy.AccountLines(0f, 0f, 0f, 0f, false), new Vector3(0f, 0.14f, 0.05f), 0.016f, new Vector2(24f, 10f), new Color(0.95f, 0.92f, 0.70f));

            _math = NewRoot("HeatMath");
            _mathLabel = MakeLabel(_math, "Equations", ThermoEnergy.MathLines(ThermoEnergy.DefaultTHot, ThermoEnergy.DefaultTCold, 0f, 0f, 0f, 0f), new Vector3(0f, 0.14f, 0f), 0.016f, new Vector2(24f, 12f), new Color(0.85f, 0.92f, 1f));

            var capGo = new GameObject("HeatCaption");
            capGo.transform.SetParent(transform, false);
            capGo.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            capGo.transform.localScale = Vector3.one * 0.016f;
            _caption = capGo.AddComponent<TextMeshPro>();
            _caption.fontSize = 5f;
            _caption.alignment = TextAlignmentOptions.Center;
            _caption.color = new Color(0.92f, 0.86f, 0.70f);
            _caption.rectTransform.sizeDelta = new Vector2(24f, 8f);
            _caption.raycastTarget = false;
            _caption.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _caption.font = font;
        }

        void EnsureGrab()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = 0.22f;
            capsule.radius = 0.030f;
            capsule.center = Vector3.zero;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.08f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearDamping = 0.8f;
            rb.angularDamping = 1.0f;

            if (GetComponent<XRGrabInteractable>() == null)
            {
                var grab = gameObject.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = false;
                grab.useDynamicAttach = true;
                grab.selectMode = InteractableSelectMode.Single;
            }
        }

        void UpdateCaption(FieldLensLayer L, ScaleLevel S, bool emParked, bool molParked, bool energy, bool math)
        {
            if (_caption == null)
                return;
            string path = _pathClosed ? "PATH CLOSED" : "PATH OPEN";
            string line = "heat coupler / turbine  " + path + "\n" + Honesty;
            if (emParked)
                line += "\nnot an EM source";
            else if (molParked)
                line += "\nnot a molecular sim";
            else if (math)
                line += "\nQ_in, eta_toy = 1 - Tc/Th (capped), W = eta Q_in, Q_c = Q_in - W";
            else if (energy)
                line += "\nINPUT / CAPTURED / LOSSES  " + ThermoEnergy.Educational;
            else if (L == FieldLensLayer.Material || S == ScaleLevel.Material)
                line += "\nhot / cold bodies  (coupler = copper/brass path)";
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

        static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat)
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
            return go;
        }

        static TextMeshPro MakeLabel(Transform parent, string name, string body, Vector3 localPos, float localScale, Vector2 size, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * localScale;
            TextMeshPro tmp = go.GetComponent<TextMeshPro>();
            if (tmp == null)
                tmp = go.AddComponent<TextMeshPro>();
            tmp.text = body;
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = size;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        static void SetOn(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on)
                t.gameObject.SetActive(on);
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
                    Debug.LogWarning("HeatCoupler: URP Lit not found; using Simple Lit.");
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
