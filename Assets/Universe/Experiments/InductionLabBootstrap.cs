using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Visualization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Experiments
{
    /// <summary>
    /// Reality Engine v0.3 — Electromagnetic Induction Laboratory.
    /// Spawns a grabable bar magnet, copper coil, resistive load, sampled B overlay,
    /// and a TMP readout beside Faraday's breadboard. Does not touch SpiceSharp.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ModelCard))]
    public sealed class InductionLabBootstrap : MonoBehaviour
    {
        public const string LabRootName = "Induction Lab";

        [SerializeField]
        [Tooltip("World position of the coil center if no table/breadboard is found. 1 unit = 1 meter.")]
        Vector3 fallbackPosition = new Vector3(0.40f, 0.75f, 0.55f);

        [SerializeField]
        [Tooltip("Coil radius in meters.")]
        float coilRadius = 0.15f;

        [SerializeField]
        [Tooltip("Bar-magnet length in meters (~12 cm).")]
        float magnetLength = 0.12f;

        [SerializeField]
        [Tooltip("Bar-magnet radius in meters.")]
        float magnetRadius = 0.012f;

        [SerializeField]
        [Tooltip("Winding count N for the lumped Faraday coil.")]
        int coilTurns = 80;

        [SerializeField]
        [Tooltip("Winding resistance in ohms.")]
        float coilResistance = 2.0f;

        [SerializeField]
        [Tooltip("Load resistance in ohms.")]
        float loadResistance = 8.0f;

        [SerializeField]
        [Tooltip("If true, BuildLab runs on Start when the magnet child is missing.")]
        bool buildOnStart = true;

        [SerializeField]
        [Tooltip("If true, keys 1/2/3 set timeScale to 1x / 0.1x / pause. F toggles field lines.")]
        bool enableKeyboardTimeControl = true;

        InductionCoil _coil;
        InductionCircuit _circuit;
        MagneticFieldViz _fieldViz;
        InductionReadout _readout;
        MagneticDipole _dipole;
        Renderer _loadRenderer;
        Material _loadMaterial;
        bool _built;
        bool _thinStand;

        public InductionCoil Coil => _coil;
        public InductionCircuit Circuit => _circuit;
        public MagneticDipole Dipole => _dipole;

        void Reset()
        {
            if (GetComponent<ModelCard>() == null)
                gameObject.AddComponent<ModelCard>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoPlaceInFaradayScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Faraday")
                return;
            if (FindFirstObjectByType<InductionLabBootstrap>() != null)
                return;
            var go = new GameObject(LabRootName);
            if (go.GetComponent<ModelCard>() == null)
                go.AddComponent<ModelCard>();
            var bootstrap = go.AddComponent<InductionLabBootstrap>();
            bootstrap.BuildLab();
        }

        void Awake()
        {
            CacheChildren();
        }

        void Start()
        {
            if (buildOnStart && transform.Find("Magnet") == null)
                BuildLab();
            else
                CacheChildren();
        }

        void OnDestroy()
        {
            if (_loadMaterial != null)
                Destroy(_loadMaterial);
        }

        void Update()
        {
            HandleKeyboard();
            UpdateLoadGlow();
        }

        void HandleKeyboard()
        {
            if (!enableKeyboardTimeControl)
                return;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SetTimeScale(1f);
                if (kb.digit2Key.wasPressedThisFrame) SetTimeScale(0.1f);
                if (kb.digit3Key.wasPressedThisFrame) SetTimeScale(0f);
                if (kb.fKey.wasPressedThisFrame && _fieldViz != null) _fieldViz.ToggleVisible();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetTimeScale(1f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetTimeScale(0.1f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetTimeScale(0f);
            if (Input.GetKeyDown(KeyCode.F) && _fieldViz != null) _fieldViz.ToggleVisible();
#endif
        }

        void UpdateLoadGlow()
        {
            if (_loadMaterial == null || _circuit == null)
                return;
            float k = _circuit.NormalizedLoadCurrent;
            Color baseCol = Color.Lerp(new Color(0.12f, 0.07f, 0.04f), new Color(1.0f, 0.48f, 0.12f), k);
            Color emission = new Color(2.4f, 0.55f, 0.08f) * (0.04f + 3.5f * k);
            if (_loadMaterial.HasProperty("_BaseColor"))
                _loadMaterial.SetColor("_BaseColor", baseCol);
            if (_loadMaterial.HasProperty("_Color"))
                _loadMaterial.SetColor("_Color", baseCol);
            if (_loadMaterial.HasProperty("_EmissionColor"))
                _loadMaterial.SetColor("_EmissionColor", emission);
            _loadMaterial.EnableKeyword("_EMISSION");
        }

        public static void SetTimeScale(float scale)
        {
            Time.timeScale = Mathf.Max(0f, scale);
            Time.fixedDeltaTime = 0.02f * Mathf.Max(0.02f, Time.timeScale <= 0f ? 1f : Time.timeScale);
            if (Time.timeScale <= 0f)
                Time.fixedDeltaTime = 0.02f;
        }

        public void BuildLab()
        {
            if (transform.Find("Magnet") != null)
            {
                CacheChildren();
                _built = true;
                return;
            }

            Vector3 coilPos = ResolvePlacement();
            transform.position = Vector3.zero;

            Material copper = MakeLit(new Color(0.72f, 0.45f, 0.20f));
            Material wood = MakeLit(new Color(0.28f, 0.18f, 0.10f));
            Material red = MakeLit(new Color(0.82f, 0.12f, 0.10f));
            Material blue = MakeLit(new Color(0.12f, 0.28f, 0.78f));
            Material metal = MakeLit(new Color(0.35f, 0.36f, 0.38f));

            GameObject stand = BuildStand(coilPos, wood);
            GameObject coilGo = BuildCoil(coilPos, copper);
            GameObject magnetGo = BuildMagnet(coilPos + new Vector3(coilRadius + 0.08f, 0.10f, 0f), red, blue);
            GameObject loadGo = BuildLoad(coilPos + new Vector3(0f, 0.02f, -coilRadius - 0.07f), metal);
            GameObject vizGo = BuildFieldViz(magnetGo);
            GameObject readoutGo = BuildReadout(coilPos + new Vector3(0.0f, 0.22f, coilRadius + 0.12f));
            BuildTimeButtons(coilPos + new Vector3(-coilRadius - 0.10f, 0.04f, 0.05f), wood);
            BuildWireHints(coilGo.transform, loadGo.transform, copper);

            _coil = coilGo.GetComponent<InductionCoil>();
            _circuit = coilGo.GetComponent<InductionCircuit>();
            _dipole = magnetGo.GetComponent<MagneticDipole>();
            _fieldViz = vizGo.GetComponent<MagneticFieldViz>();
            _readout = readoutGo.GetComponent<InductionReadout>();
            _loadRenderer = loadGo.GetComponent<Renderer>();
            _loadMaterial = _loadRenderer.material;

            _coil.SetMagnets(_dipole, null);
            _circuit.SetCoil(_coil);
            _fieldViz.Magnet = _dipole;
            _readout.SetCircuit(_circuit);

            if (GetComponent<ModelCard>() == null)
                gameObject.AddComponent<ModelCard>();

            _built = true;
        }

        void CacheChildren()
        {
            Transform magnet = transform.Find("Magnet");
            Transform coil = transform.Find("Coil");
            Transform viz = transform.Find("FieldViz");
            Transform readout = transform.Find("Readout");
            Transform load = transform.Find("Load");

            if (magnet != null)
                _dipole = magnet.GetComponent<MagneticDipole>();
            if (coil != null)
            {
                _coil = coil.GetComponent<InductionCoil>();
                _circuit = coil.GetComponent<InductionCircuit>();
            }
            if (viz != null)
                _fieldViz = viz.GetComponent<MagneticFieldViz>();
            if (readout != null)
                _readout = readout.GetComponent<InductionReadout>();
            if (load != null)
            {
                _loadRenderer = load.GetComponent<Renderer>();
                if (_loadRenderer != null)
                    _loadMaterial = _loadRenderer.material;
            }
        }

        Vector3 ResolvePlacement()
        {
            Transform table = FindNamedContains("table", "desk", "workbench", "labbench");
            Transform breadboard = FindNamedContains("breadboard", "circuitlab");
            if (table != null)
            {
                _thinStand = true;
                Bounds b = CollectBounds(table);
                Vector3 p = new Vector3(b.max.x + 0.22f, b.max.y + 0.04f, b.center.z);
                if (breadboard != null)
                {
                    Bounds bb = CollectBounds(breadboard);
                    p = new Vector3(bb.max.x + 0.28f, Mathf.Max(b.max.y, bb.max.y) + 0.04f, bb.center.z);
                }
                return p;
            }

            if (breadboard != null)
            {
                _thinStand = true;
                Bounds bb = CollectBounds(breadboard);
                return new Vector3(bb.max.x + 0.28f, bb.max.y + 0.04f, bb.center.z);
            }

            _thinStand = false;
            return fallbackPosition;
        }

        Transform FindNamedContains(params string[] tokens)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (string.IsNullOrEmpty(n))
                    continue;
                string lower = n.ToLowerInvariant();
                for (int t = 0; t < tokens.Length; t++)
                {
                    if (lower.Contains(tokens[t]))
                        return all[i];
                }
            }
            return null;
        }

        static Bounds CollectBounds(Transform root)
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
                return new Bounds(root.position, Vector3.one * 0.1f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);
            return b;
        }

        GameObject BuildStand(Vector3 coilPos, Material wood)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Stand";
            go.transform.SetParent(transform, true);
            float top = coilPos.y - 0.022f;
            float height = _thinStand ? 0.04f : Mathf.Max(0.05f, top);
            go.transform.position = new Vector3(coilPos.x, top - height * 0.5f, coilPos.z);
            go.transform.localScale = new Vector3(coilRadius * 2.4f, height, coilRadius * 2.4f);
            ApplyMat(go, wood);
            return go;
        }

        GameObject BuildCoil(Vector3 pos, Material copper)
        {
            var go = new GameObject("Coil");
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;

            float R = Mathf.Max(0.05f, coilRadius);
            float tube = 0.007f;
            const int segments = 16;
            const int rings = 4;
            float stack = 0.036f;
            for (int r = 0; r < rings; r++)
            {
                float y = (r / (float)(rings - 1) - 0.5f) * stack;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = (i / (float)segments) * Mathf.PI * 2f;
                    float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * R, y, Mathf.Sin(a0) * R);
                    Vector3 p1 = new Vector3(Mathf.Cos(a1) * R, y, Mathf.Sin(a1) * R);
                    Vector3 mid = (p0 + p1) * 0.5f;
                    Vector3 dir = p1 - p0;
                    var tubeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    KillCollider(tubeGo);
                    tubeGo.name = "Winding";
                    tubeGo.transform.SetParent(go.transform, false);
                    tubeGo.transform.localPosition = mid;
                    tubeGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                    tubeGo.transform.localScale = new Vector3(tube * 2f, dir.magnitude * 0.5f, tube * 2f);
                    ApplyMat(tubeGo, copper);
                }
            }

            var coil = go.AddComponent<InductionCoil>();
            var circuit = go.AddComponent<InductionCircuit>();
            circuit.SetCoil(coil);

            coil.Configure(coilTurns, R, coilResistance, loadResistance);

            return go;
        }

        GameObject BuildMagnet(Vector3 pos, Material red, Material blue)
        {
            var go = new GameObject("Magnet");
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;

            float len = Mathf.Max(0.06f, magnetLength);
            float rad = Mathf.Max(0.006f, magnetRadius);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(rad * 2f, len * 0.5f, rad * 2f);
            KillCollider(body);
            ApplyMat(body, MakeLit(new Color(0.22f, 0.22f, 0.24f)));

            var north = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            north.name = "North";
            north.transform.SetParent(go.transform, false);
            north.transform.localPosition = new Vector3(0f, len * 0.35f, 0f);
            north.transform.localScale = new Vector3(rad * 2.05f, len * 0.15f, rad * 2.05f);
            KillCollider(north);
            ApplyMat(north, red);

            var south = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            south.name = "South";
            south.transform.SetParent(go.transform, false);
            south.transform.localPosition = new Vector3(0f, -len * 0.35f, 0f);
            south.transform.localScale = new Vector3(rad * 2.05f, len * 0.15f, rad * 2.05f);
            KillCollider(south);
            ApplyMat(south, blue);

            var nLabel = MakeWorldLabel(north.transform, "N", new Vector3(0f, 1.4f, 0f), new Color(1f, 0.85f, 0.85f));
            var sLabel = MakeWorldLabel(south.transform, "S", new Vector3(0f, -1.4f, 0f), new Color(0.85f, 0.9f, 1f));
            nLabel.transform.localScale = Vector3.one * 0.08f;
            sLabel.transform.localScale = Vector3.one * 0.08f;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = len;
            capsule.radius = rad * 1.15f;
            capsule.center = Vector3.zero;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.12f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearDamping = 0.4f;
            rb.angularDamping = 0.8f;

            var dipole = go.AddComponent<MagneticDipole>();
            dipole.localAxis = Vector3.up;
            dipole.magnetLength = len;
            dipole.magnetRadius = rad;
            dipole.magneticMoment = 2.0f;
            dipole.isActive = true;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = true;
            grab.selectMode = InteractableSelectMode.Single;
            // Default XRI interaction layers: either hand can grab. Do not bind to a right-hand-only mask.

            return go;
        }

        GameObject BuildLoad(Vector3 pos, Material metal)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Load";
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.03f, 0.035f, 0.03f);
            KillCollider(go);

            _loadMaterial = new Material(LitShader())
            {
                name = "InductionLoad",
                hideFlags = HideFlags.HideAndDontSave
            };
            _loadMaterial.EnableKeyword("_EMISSION");
            _loadMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (_loadMaterial.HasProperty("_BaseColor"))
                _loadMaterial.SetColor("_BaseColor", new Color(0.15f, 0.08f, 0.04f));
            if (_loadMaterial.HasProperty("_EmissionColor"))
                _loadMaterial.SetColor("_EmissionColor", Color.black);
            ApplyMat(go, _loadMaterial);

            var socket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            socket.name = "LoadBase";
            socket.transform.SetParent(go.transform, false);
            socket.transform.localPosition = new Vector3(0f, -1.15f, 0f);
            socket.transform.localScale = new Vector3(1.4f, 0.25f, 1.4f);
            KillCollider(socket);
            ApplyMat(socket, metal);
            return go;
        }

        GameObject BuildFieldViz(GameObject magnetGo)
        {
            var go = new GameObject("FieldViz");
            go.transform.SetParent(transform, true);
            var viz = go.AddComponent<MagneticFieldViz>();
            MagneticDipole d = magnetGo.GetComponent<MagneticDipole>();
            viz.Magnet = d;
            viz.EnsureBuilt();
            return go;
        }

        GameObject BuildReadout(Vector3 pos)
        {
            var go = new GameObject("Readout");
            go.transform.SetParent(transform, true);
            go.transform.position = pos;

            var tmpGo = new GameObject("Text");
            tmpGo.transform.SetParent(go.transform, false);
            var tmp = tmpGo.AddComponent<TextMeshPro>();
            tmp.text = "INDUCTION LAB";
            tmp.fontSize = 0.22f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.92f, 0.95f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(0.62f, 0.40f);
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;

            var readout = go.AddComponent<InductionReadout>();
            go.AddComponent<ModelCard>();
            readout.Bind(null, tmp);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(go.transform, false);
            board.transform.localPosition = new Vector3(0.22f, -0.12f, 0.01f);
            board.transform.localScale = new Vector3(0.64f, 0.36f, 0.008f);
            KillCollider(board);
            ApplyMat(board, MakeLit(new Color(0.05f, 0.07f, 0.06f)));

            return go;
        }

        void BuildTimeButtons(Vector3 origin, Material wood)
        {
            BuildButton(origin, "Time 1x", () => SetTimeScale(1f), new Color(0.2f, 0.7f, 0.3f));
            BuildButton(origin + new Vector3(0f, 0f, 0.06f), "Time 0.1x", () => SetTimeScale(0.1f), new Color(0.75f, 0.6f, 0.15f));
            BuildButton(origin + new Vector3(0f, 0f, 0.12f), "Pause", () => SetTimeScale(0f), new Color(0.7f, 0.2f, 0.2f));
            BuildButton(origin + new Vector3(0f, 0f, 0.18f), "Field", () =>
            {
                if (_fieldViz != null)
                    _fieldViz.ToggleVisible();
            }, new Color(0.2f, 0.55f, 0.75f));
        }

        void BuildButton(Vector3 pos, string label, System.Action onPress, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Button_" + label.Replace(" ", "");
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.045f, 0.03f, 0.045f);
            ApplyMat(go, MakeLit(color));

            var simple = go.AddComponent<XRSimpleInteractable>();
            simple.selectEntered.AddListener(_ => onPress());

            MakeWorldLabel(go.transform, label, new Vector3(0f, 1.6f, 0f), Color.white);
        }

        void BuildWireHints(Transform coil, Transform load, Material copper)
        {
            var go = new GameObject("Leads");
            go.transform.SetParent(transform, true);
            var lr = go.AddComponent<LineRenderer>();
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave, color = new Color(0.72f, 0.45f, 0.20f) };
            lr.sharedMaterial = mat;
            lr.widthMultiplier = 0.004f;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 3;
            Vector3 a = coil.position + coil.right * coilRadius;
            Vector3 b = load.position + Vector3.up * 0.03f;
            lr.SetPosition(0, a);
            lr.SetPosition(1, (a + b) * 0.5f + Vector3.up * 0.04f);
            lr.SetPosition(2, b);
        }

        static TextMeshPro MakeWorldLabel(Transform parent, string text, Vector3 localPos, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.025f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
            tmp.raycastTarget = false;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        Material MakeLit(Color color)
        {
            var mat = new Material(LitShader())
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            mat.color = color;
            return mat;
        }

        static Shader LitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
                s = Shader.Find("URP/Lit");
            if (s == null)
                s = Shader.Find("Standard");
            if (s == null)
                s = Shader.Find("Sprites/Default");
            return s;
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = mat;
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
    }
}
