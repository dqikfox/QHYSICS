using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Visualization;
using RealityEngine.Chemistry;

namespace RealityEngine.Biology
{
    /// <summary>
    /// Grabbable conceptual muscle cell / mitochondrion. Primitive capsules and spheres.
    /// ScaleEngine + Field Lens peel the same object. Quest budget: dozens of spheres max.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class MuscleCell : MonoBehaviour
    {
        public const string Honesty = BioEnergy.Honesty;

        Transform _human;
        Transform _membrane;
        Transform _molecular;
        Transform _atomic;
        Transform _energy;
        Transform _math;
        TextMeshPro _caption;
        Camera _camera;
        bool _built;
        int _layer;
        int _scale;
        Material _matCell;
        Material _matMito;
        Material _matNucleus;
        Material _matMembrane;
        Material _matProtein;
        Material _matAtp;
        Material _matIn;
        Material _matCap;
        Material _matLoss;

        public int Layer => _layer;
        public int Scale => _scale;

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
            bool membrane = !emParked && (L == FieldLensLayer.Material || S == ScaleLevel.Material);
            bool molecular = !emParked && (S == ScaleLevel.Molecular || L == FieldLensLayer.Atomic);
            bool atomic = !emParked && S == ScaleLevel.Atomic;
            bool energy = !emParked && (L == FieldLensLayer.Charge || L == FieldLensLayer.EnergyFlow);
            bool math = !emParked && L == FieldLensLayer.Mathematical;
            bool blob = !molecular && !atomic;

            SetOn(_human, blob || membrane || energy || math || emParked);
            SetOn(_membrane, membrane);
            SetOn(_molecular, molecular);
            SetOn(_atomic, atomic);
            SetOn(_energy, energy);
            SetOn(_math, math);
            UpdateCaption(L, S, emParked, membrane, molecular, atomic, energy, math);
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
        }

        void OnDestroy()
        {
            DestroyMat(_matCell);
            DestroyMat(_matMito);
            DestroyMat(_matNucleus);
            DestroyMat(_matMembrane);
            DestroyMat(_matProtein);
            DestroyMat(_matAtp);
            DestroyMat(_matIn);
            DestroyMat(_matCap);
            DestroyMat(_matLoss);
        }

        void CacheIfNeeded()
        {
            if (_human == null)
            {
                Transform t = transform.Find("HumanView");
                if (t != null)
                    _human = t;
            }
            if (_membrane == null)
            {
                Transform t = transform.Find("BioMembrane");
                if (t != null)
                    _membrane = t;
            }
            if (_molecular == null)
            {
                Transform t = transform.Find("BioMolecular");
                if (t != null)
                    _molecular = t;
            }
            if (_atomic == null)
            {
                Transform t = transform.Find("BioAtomic");
                if (t != null)
                    _atomic = t;
            }
            if (_energy == null)
            {
                Transform t = transform.Find("BioEnergy");
                if (t != null)
                    _energy = t;
            }
            if (_math == null)
            {
                Transform t = transform.Find("BioMath");
                if (t != null)
                    _math = t;
            }
            if (_caption == null)
            {
                Transform t = transform.Find("BioCaption");
                if (t != null)
                    _caption = t.GetComponent<TextMeshPro>();
            }
        }

        void BuildAll()
        {
            _matCell = MakeLit(new Color(0.78f, 0.32f, 0.38f));
            _matMito = MakeLit(new Color(0.88f, 0.48f, 0.16f));
            _matNucleus = MakeLit(new Color(0.45f, 0.22f, 0.50f));
            _matMembrane = MakeLit(new Color(0.55f, 0.72f, 0.42f));
            _matProtein = MakeLit(new Color(0.82f, 0.70f, 0.28f));
            _matAtp = MakeLit(new Color(0.28f, 0.62f, 0.88f));
            _matIn = MakeLit(new Color(0.22f, 0.70f, 0.38f));
            _matCap = MakeLit(new Color(0.88f, 0.72f, 0.18f));
            _matLoss = MakeLit(new Color(0.78f, 0.22f, 0.18f));

            _human = NewRoot("HumanView");
            // Capsule primitive: height 2 at scale.y, diameter 1 at scale.xz.
            Primitive(_human, PrimitiveType.Capsule, "Sarcolemma", Vector3.zero, new Vector3(0.045f, 0.055f, 0.045f), _matCell);
            Primitive(_human, PrimitiveType.Sphere, "Mitochondrion", new Vector3(0.012f, -0.006f, 0f), Vector3.one * 0.028f, _matMito);
            Primitive(_human, PrimitiveType.Sphere, "Nucleus", new Vector3(-0.012f, 0.018f, 0f), Vector3.one * 0.014f, _matNucleus);

            _membrane = NewRoot("BioMembrane");
            const int membraneCount = 10;
            for (int i = 0; i < membraneCount; i++)
            {
                float a = (i / (float)membraneCount) * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.030f, 0f, Mathf.Sin(a) * 0.030f);
                Primitive(_membrane, PrimitiveType.Sphere, "Lipid_" + i, p, Vector3.one * 0.008f, _matMembrane);
            }

            _molecular = NewRoot("BioMolecular");
            const int stator = 6;
            for (int i = 0; i < stator; i++)
            {
                float a = (i / (float)stator) * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.018f, 0f, Mathf.Sin(a) * 0.018f);
                Primitive(_molecular, PrimitiveType.Sphere, "SynthaseStator_" + i, p, Vector3.one * 0.008f, _matProtein);
            }
            Primitive(_molecular, PrimitiveType.Sphere, "SynthaseRotor", Vector3.zero, Vector3.one * 0.010f, _matProtein);
            Primitive(_molecular, PrimitiveType.Sphere, "SynthaseHead", new Vector3(0f, 0.020f, 0f), Vector3.one * 0.012f, _matProtein);
            Primitive(_molecular, PrimitiveType.Sphere, "ATP_A", new Vector3(0.042f, 0.008f, 0f), Vector3.one * 0.009f, _matAtp);
            Primitive(_molecular, PrimitiveType.Sphere, "ATP_P1", new Vector3(0.052f, 0.008f, 0f), Vector3.one * 0.006f, _matAtp);
            Primitive(_molecular, PrimitiveType.Sphere, "ATP_P2", new Vector3(0.059f, 0.008f, 0f), Vector3.one * 0.006f, _matAtp);
            Primitive(_molecular, PrimitiveType.Sphere, "ATP_P3", new Vector3(0.066f, 0.008f, 0f), Vector3.one * 0.006f, _matAtp);

            _atomic = NewRoot("BioAtomic");
            MakeLabel(_atomic, "AtomCard", Element.AtpAtomCards(), new Vector3(0f, 0.10f, 0f), 0.018f, new Vector2(22f, 14f), new Color(0.92f, 0.95f, 0.82f));

            _energy = NewRoot("BioEnergy");
            Primitive(_energy, PrimitiveType.Cube, "BarInput", new Vector3(-0.028f, 0.040f, 0.04f), new Vector3(0.016f, 0.080f, 0.016f), _matIn);
            Primitive(_energy, PrimitiveType.Cube, "BarCaptured", new Vector3(0f, 0.020f, 0.04f), new Vector3(0.016f, 0.040f, 0.016f), _matCap);
            Primitive(_energy, PrimitiveType.Cube, "BarLosses", new Vector3(0.028f, 0.030f, 0.04f), new Vector3(0.016f, 0.060f, 0.016f), _matLoss);
            MakeLabel(_energy, "Account", BioEnergy.AccountLines(), new Vector3(0f, 0.12f, 0.04f), 0.016f, new Vector2(22f, 8f), new Color(0.95f, 0.92f, 0.70f));

            _math = NewRoot("BioMath");
            MakeLabel(_math, "Hydrolysis", BioEnergy.HydrolysisMath(), new Vector3(0f, 0.11f, 0f), 0.016f, new Vector2(22f, 10f), new Color(0.85f, 0.92f, 1f));

            var capGo = new GameObject("BioCaption");
            capGo.transform.SetParent(transform, false);
            capGo.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            capGo.transform.localScale = Vector3.one * 0.018f;
            _caption = capGo.AddComponent<TextMeshPro>();
            _caption.fontSize = 5f;
            _caption.alignment = TextAlignmentOptions.Center;
            _caption.color = new Color(0.90f, 0.95f, 0.82f);
            _caption.rectTransform.sizeDelta = new Vector2(22f, 7f);
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
            capsule.height = 0.12f;
            capsule.radius = 0.030f;
            capsule.center = Vector3.zero;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.06f;
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

        void UpdateCaption(FieldLensLayer L, ScaleLevel S, bool emParked, bool membrane, bool molecular, bool atomic, bool energy, bool math)
        {
            if (_caption == null)
                return;
            string line = "muscle cell / mitochondrion\n" + Honesty;
            if (emParked)
                line += "\nThis object is not an EM source. Use the copper coil for B / E.";
            else if (math)
                line += "\nATP hydrolysis as conceptual chemical potential. Not a kinetic simulation.";
            else if (energy)
                line += "\n" + BioEnergy.Educational;
            else if (atomic)
                line += "\nElement cards C,H,O,N,P. Not QM.";
            else if (molecular)
                line += "\nATP / ATP synthase schematic. Not MD.";
            else if (membrane)
                line += "\nMembrane look. Not a real bilayer simulation.";
            else
                line += "\nHuman-scale blob. " + BioScale.RepresentationOf(S);
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
                Destroy(c);
            else
                DestroyImmediate(c);
        }

        static Material MakeLit(Color color)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
                s = Shader.Find("URP/Lit");
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
                mat.SetFloat("_Metallic", 0.08f);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.35f);
            return mat;
        }

        static void DestroyMat(Material mat)
        {
            if (mat == null)
                return;
            if (Application.isPlaying)
                Destroy(mat);
            else
                DestroyImmediate(mat);
        }
    }
}
