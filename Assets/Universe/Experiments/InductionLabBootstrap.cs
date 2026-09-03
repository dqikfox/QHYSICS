using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Visualization;
using RealityEngine.Core;
using RealityEngine.AI;
using RealityEngine.Chemistry;
using RealityEngine.Biology;
using RealityEngine.Physics.Thermo;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Experiments
{
    /// <summary>
    /// Reality Engine v1.0 — Persistent lab + gradient ledger (coil + cell + toy heat path).
    /// Spawns a grabable bar magnet, copper coil, resistive load, sampled B overlay,
    /// Field Lens peels, and a TMP readout beside Faraday's breadboard. Does not touch SpiceSharp.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ModelCard))]
    public sealed class InductionLabBootstrap : MonoBehaviour
    {
        public const string LabRootName = "Induction Lab";

        const float BoardWidthMeters = 0.70f;
        const float BoardHeightMeters = 1.25f;
        const float BoardGapMeters = 0.15f;
        const float BoardFontSize = 0.046f;
        const int BoardSlotCount = 5;
        const int SlotScientist = 0;
        const int SlotChemistry = 1;
        const int SlotBiology = 2;
        const int SlotConservation = 3;
        const int SlotExperiment = 4;

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
        FieldLens _fieldLens;
        ScaleEngine _scaleEngine;
        ExperimentRunner _experiment;
        Scientist _scientist;
        MuscleCell _muscleCell;
        BiologyBoard _biologyBoard;
        HeatCoupler _heatCoupler;
        ConservationBoard _conservationBoard;
        Renderer _loadRenderer;
        Material _loadMaterial;
        bool _built;
        bool _thinStand;

        public InductionCoil Coil => _coil;
        public InductionCircuit Circuit => _circuit;
        public MagneticDipole Dipole => _dipole;
        public FieldLens FieldLens => _fieldLens;
        public ScaleEngine ScaleEngine => _scaleEngine;
        public ExperimentRunner Experiment => _experiment;
        public Scientist Scientist => _scientist;
        public MuscleCell MuscleCell => _muscleCell;
        public BiologyBoard BiologyBoard => _biologyBoard;
        public HeatCoupler HeatCoupler => _heatCoupler;
        public ConservationBoard ConservationBoard => _conservationBoard;

        void Reset()
        {
            if (GetComponent<ModelCard>() == null)
                gameObject.AddComponent<ModelCard>();
        }

        public const string RealityEngineRootName = "RealityEngine";

        static bool _ensureBusy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookPlayModeSceneLoad()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoPlaceAfterSceneLoad()
        {
            TryEnsureLabForScene(SceneManager.GetActiveScene());
        }

        static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryEnsureLabForScene(scene);
        }

        public static void TryEnsureLabForScene(Scene scene)
        {
            if (!Application.isPlaying)
                return;
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            if (!IsFaradayOrXrScene(scene))
                return;
            EnsureLabInScene(scene);
        }

        public static bool IsFaradayOrXrScene(Scene scene)
        {
            if (!scene.IsValid())
                return false;
            string n = scene.name;
            if (!string.IsNullOrEmpty(n) && n.IndexOf("Faraday", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (ContainsXrOrigin(roots[i].transform))
                    return true;
            }
            return false;
        }

        static bool ContainsXrOrigin(Transform root)
        {
            Transform[] ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                if (ts[i] != null && ts[i].name == "XR Origin")
                    return true;
            }
            return false;
        }

        public static InductionLabBootstrap EnsureLabInScene(Scene scene)
        {
            if (_ensureBusy)
                return FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
            _ensureBusy = true;
            try
            {
                InductionLabBootstrap existing = FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    if (!existing.gameObject.activeSelf)
                        existing.gameObject.SetActive(true);
                    existing.BuildLab();
                    existing.EnsureScientist();
                    existing.EnsureChemistry();
                    existing.EnsureBiology();
                    existing.EnsureLabStyle();
                    existing.EnsureThermo();
                    existing.EnsureLedger();
                    return existing;
                }

                var go = new GameObject(RealityEngineRootName);
                if (scene.IsValid())
                    SceneManager.MoveGameObjectToScene(go, scene);
                if (go.GetComponent<ModelCard>() == null)
                    go.AddComponent<ModelCard>();
                var bootstrap = go.AddComponent<InductionLabBootstrap>();
                bootstrap.BuildLab();
                bootstrap.EnsureScientist();
                bootstrap.EnsureChemistry();
                bootstrap.EnsureBiology();
                bootstrap.EnsureLabStyle();
                bootstrap.EnsureThermo();
                bootstrap.EnsureLedger();
                return bootstrap;
            }
            finally
            {
                _ensureBusy = false;
            }
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
            {
                CacheChildren();
                EnsureFieldLens();
                EnsureScaleEngine();
                EnsureExperimentFramework();
                EnsureScientist();
                EnsureChemistry();
                EnsureBiology();
                EnsureLabStyle();
                EnsureThermo();
                EnsureLedger();
            }
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
                if (kb.fKey.wasPressedThisFrame) ToggleMagneticLayer();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetTimeScale(1f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetTimeScale(0.1f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetTimeScale(0f);
            if (Input.GetKeyDown(KeyCode.F)) ToggleMagneticLayer();
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
                EnsureFieldLens();
                EnsureScaleEngine();
                EnsureExperimentFramework();
                EnsureScientist();
                EnsureChemistry();
                EnsureBiology();
                EnsureLabStyle();
                EnsureThermo();
                EnsureLedger();
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

            EnsureFieldLens();
            EnsureScaleEngine();
            EnsureExperimentFramework();
            EnsureScientist();
            EnsureChemistry();
            EnsureBiology();
            EnsureLabStyle();
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
            _fieldLens = GetComponent<FieldLens>();
            _scaleEngine = GetComponent<ScaleEngine>();
            _experiment = GetComponent<ExperimentRunner>();
            _scientist = GetComponent<Scientist>();
            Transform muscle = transform.Find("MuscleCell");
            if (muscle != null)
                _muscleCell = muscle.GetComponent<MuscleCell>();
            Transform bioBoard = transform.Find("BiologyBoard");
            if (bioBoard != null)
                _biologyBoard = bioBoard.GetComponent<BiologyBoard>();
            Transform heatC = transform.Find("HeatCoupler");
            if (heatC != null)
                _heatCoupler = heatC.GetComponent<HeatCoupler>();
            Transform ledger = transform.Find("ConservationBoard");
            if (ledger != null)
                _conservationBoard = ledger.GetComponent<ConservationBoard>();
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
            Collider[] cs = root.GetComponentsInChildren<Collider>();
            bool any = false;
            Bounds b = new Bounds(root.position, Vector3.one * 0.1f);
            for (int i = 0; i < rs.Length; i++)
            {
                if (!any)
                {
                    b = rs[i].bounds;
                    any = true;
                }
                else
                    b.Encapsulate(rs[i].bounds);
            }
            for (int i = 0; i < cs.Length; i++)
            {
                if (!any)
                {
                    b = cs[i].bounds;
                    any = true;
                }
                else
                    b.Encapsulate(cs[i].bounds);
            }
            if (!any)
                return new Bounds(root.position, Vector3.one * 0.1f);
            return b;
        }

        bool TryGetLabSurface(out Bounds bounds)
        {
            Transform table = FindNamedContains("circuittable", "circuit table");
            if (table == null)
                table = FindNamedContains("workbench", "labbench");
            Transform breadboard = FindNamedContains("breadboard");
            if (table != null)
            {
                bounds = CollectBounds(table);
                if (breadboard != null)
                    bounds.Encapsulate(CollectBounds(breadboard));
                return true;
            }
            if (breadboard != null)
            {
                bounds = CollectBounds(breadboard);
                return true;
            }
            bounds = new Bounds(fallbackPosition, Vector3.one * 0.4f);
            return false;
        }

        void GetPlayerAxes(out Vector3 fwd, out Vector3 right)
        {
            Transform eye = FindPlayerEye();
            if (eye != null)
            {
                fwd = eye.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-6f)
                    fwd = Vector3.forward;
                fwd.Normalize();
                right = Vector3.Cross(Vector3.up, fwd);
                if (right.sqrMagnitude < 1e-6f)
                    right = Vector3.right;
                right.y = 0f;
                right.Normalize();
                return;
            }
            fwd = Vector3.forward;
            right = Vector3.right;
        }

        void ApplyBoardFace(GameObject go, TextMeshPro tmp)
        {
            if (go != null)
                go.transform.localScale = Vector3.one;

            const float w = BoardWidthMeters;
            const float h = BoardHeightMeters;
            if (tmp != null)
            {
                tmp.fontSize = BoardFontSize;
                tmp.rectTransform.sizeDelta = new Vector2(w - 0.06f, h - 0.08f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                tmp.transform.localScale = Vector3.one;
                tmp.transform.localPosition = new Vector3(-(w - 0.06f) * 0.5f, (h - 0.08f) * 0.5f, -0.008f);
            }

            if (go == null)
                return;
            Transform board = go.transform.Find("Board");
            if (board != null)
            {
                board.localScale = new Vector3(w, h, 0.010f);
                board.localPosition = new Vector3(0f, 0f, 0.012f);
            }
        }

        void PlaceBoardAlongTableEdge(Transform t, int slot)
        {
            if (t == null)
                return;
            slot = Mathf.Clamp(slot, 0, BoardSlotCount - 1);
            t.localScale = Vector3.one;

            Vector3 fwd;
            Vector3 right;
            GetPlayerAxes(out fwd, out right);
            Transform eye = FindPlayerEye();

            float pitch = BoardWidthMeters + BoardGapMeters;
            float total = BoardSlotCount * BoardWidthMeters + (BoardSlotCount - 1) * BoardGapMeters;
            float alongOffset = -0.5f * total + 0.5f * BoardWidthMeters + slot * pitch;

            Vector3 origin;
            Vector3 faceDir;
            Bounds surface;
            if (TryGetLabSurface(out surface))
            {
                Vector3 towardPlayer;
                if (eye != null)
                {
                    towardPlayer = eye.position - surface.center;
                    towardPlayer.y = 0f;
                    if (towardPlayer.sqrMagnitude < 1e-6f)
                        towardPlayer = -fwd;
                    towardPlayer.Normalize();
                }
                else
                    towardPlayer = -fwd;

                Vector3 away = -towardPlayer;
                Vector3 along = Vector3.Cross(towardPlayer, Vector3.up);
                if (along.sqrMagnitude < 1e-6f)
                    along = right;
                along.Normalize();

                float towardExtent = Mathf.Abs(away.x) * surface.extents.x + Mathf.Abs(away.z) * surface.extents.z;
                Vector3 edge = surface.center + away * (towardExtent + 0.04f);
                origin = edge + along * alongOffset;
                origin.y = surface.max.y + BoardHeightMeters * 0.5f;
                faceDir = towardPlayer;
            }
            else
            {
                origin = fallbackPosition + right * alongOffset;
                origin.y = Mathf.Max(fallbackPosition.y, 0.90f) + BoardHeightMeters * 0.5f;
                faceDir = -fwd;
                if (eye != null)
                {
                    Vector3 toEye = eye.position - origin;
                    toEye.y = 0f;
                    if (toEye.sqrMagnitude > 1e-6f)
                        faceDir = toEye.normalized;
                }
            }

            faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 1e-6f)
                faceDir = Vector3.forward;
            t.position = origin;
            t.rotation = Quaternion.LookRotation(-faceDir.normalized, Vector3.up);
        }

        void PlaceOnTableGrab(Transform t, float alongRight, float alongFwd)
        {
            if (t == null)
                return;
            Vector3 fwd;
            Vector3 right;
            GetPlayerAxes(out fwd, out right);
            Bounds surface;
            Vector3 p;
            if (TryGetLabSurface(out surface))
            {
                p = surface.center + right * alongRight + fwd * alongFwd;
                p.y = surface.max.y + 0.04f;
            }
            else
            {
                p = fallbackPosition + right * alongRight + fwd * alongFwd;
                if (p.y < 0.80f)
                    p.y = 0.80f;
            }
            t.position = p;
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

            _loadMaterial = LabWorldMeshes.MakeLit("InductionLoad", new Color(0.15f, 0.08f, 0.04f), 0.28f, 0.24f, true);
            if (_loadMaterial == null)
                return go;
            _loadMaterial.hideFlags = HideFlags.HideAndDontSave;
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
            tmp.rectTransform.sizeDelta = new Vector2(0.62f, 0.48f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
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
            board.transform.localScale = new Vector3(0.64f, 0.44f, 0.008f);
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
                ToggleMagneticLayer();
            }, new Color(0.2f, 0.55f, 0.75f));
        }

        GameObject BuildButton(Vector3 pos, string label, System.Action onPress, Color color)
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
            return go;
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
            return LabWorldMeshes.MakeLit("RELab_Induction", color, 0.28f, 0.24f, false);
        }

        static Shader LitShader()
        {
            return LabWorldMeshes.LitShader;
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null)
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

        void ToggleMagneticLayer()
        {
            if (_fieldLens == null)
                _fieldLens = GetComponent<FieldLens>();
            if (_fieldLens != null)
            {
                if (_fieldLens.CurrentLayer == (int)FieldLensLayer.Magnetic)
                    _fieldLens.SetLayer((int)FieldLensLayer.Normal);
                else
                    _fieldLens.SetLayer((int)FieldLensLayer.Magnetic);
                return;
            }
            if (_fieldViz != null)
                _fieldViz.ToggleVisible();
        }

        public void EnsureFieldLens()
        {
            CacheChildren();

            FieldLens lens = GetComponent<FieldLens>();
            if (lens == null)
                lens = gameObject.AddComponent<FieldLens>();
            _fieldLens = lens;

            if (_dipole != null)
            {
                FieldLensTarget magTarget = _dipole.GetComponent<FieldLensTarget>();
                if (magTarget == null)
                    magTarget = _dipole.gameObject.AddComponent<FieldLensTarget>();
                magTarget.Configure(lens, FieldLensTargetKind.Magnet, _dipole, _circuit, _fieldViz);
            }

            if (_coil != null)
            {
                EnsureAimCollider(_coil.gameObject, Mathf.Max(0.05f, coilRadius));
                FieldLensTarget coilTarget = _coil.GetComponent<FieldLensTarget>();
                if (coilTarget == null)
                    coilTarget = _coil.gameObject.AddComponent<FieldLensTarget>();
                coilTarget.Configure(lens, FieldLensTargetKind.Coil, _dipole, _circuit, null);
            }

            Transform load = transform.Find("Load");
            if (load != null)
            {
                EnsureAimCollider(load.gameObject, 0.04f);
                FieldLensTarget loadTarget = load.GetComponent<FieldLensTarget>();
                if (loadTarget == null)
                    loadTarget = load.gameObject.AddComponent<FieldLensTarget>();
                loadTarget.Configure(lens, FieldLensTargetKind.Load, _dipole, _circuit, null);
            }

            if (_readout != null)
                lens.BindReadout(_readout);

            ModelCard card = GetComponent<ModelCard>();
            if (card != null)
                lens.BindModelCard(card);

            MathModelViz math = null;
            Transform mathT = transform.Find("MathModel");
            if (mathT != null)
                math = mathT.GetComponent<MathModelViz>();
            if (math == null)
            {
                var mathGo = new GameObject("MathModel");
                mathGo.transform.SetParent(transform, true);
                if (_readout != null)
                    mathGo.transform.position = _readout.transform.position + new Vector3(0.0f, 0.42f, 0.0f);
                else
                    mathGo.transform.position = transform.position + new Vector3(0.4f, 1.1f, 0.6f);
                math = mathGo.AddComponent<MathModelViz>();
            }
            math.Bind(_circuit, _dipole);
            lens.BindMath(math);

            EnsureLensButtons(lens);
            lens.SetLayer(lens.CurrentLayer);
        }

        public void EnsureScaleEngine()
        {
            CacheChildren();
            if (_fieldLens == null)
                EnsureFieldLens();

            ScaleEngine engine = GetComponent<ScaleEngine>();
            if (engine == null)
                engine = gameObject.AddComponent<ScaleEngine>();
            _scaleEngine = engine;

            BindScaleTarget(_dipole != null ? _dipole.gameObject : null, engine);
            BindScaleTarget(_coil != null ? _coil.gameObject : null, engine);
            Transform load = transform.Find("Load");
            if (load != null)
                BindScaleTarget(load.gameObject, engine);

            if (_readout != null)
                engine.BindReadout(_readout);

            ModelCard card = GetComponent<ModelCard>();
            if (card != null)
                engine.BindModelCard(card);

            EnsureScaleButtons(engine);
            engine.SetScale(engine.CurrentScale);
        }

        static void BindScaleTarget(GameObject go, ScaleEngine engine)
        {
            if (go == null || engine == null)
                return;
            FieldLensTarget lensTarget = go.GetComponent<FieldLensTarget>();
            ScaleAwareTarget aware = go.GetComponent<ScaleAwareTarget>();
            if (aware == null)
                aware = go.AddComponent<ScaleAwareTarget>();
            aware.Configure(engine, lensTarget);
        }

        static void EnsureAimCollider(GameObject go, float worldRadius)
        {
            if (go.GetComponent<XRGrabInteractable>() != null)
                return;

            Transform hit = go.transform.Find("LensHit");
            GameObject hitGo;
            if (hit == null)
            {
                hitGo = new GameObject("LensHit");
                hitGo.transform.SetParent(go.transform, false);
                hitGo.transform.localPosition = Vector3.zero;
                Vector3 ls = go.transform.lossyScale;
                hitGo.transform.localScale = new Vector3(
                    1f / Mathf.Max(ls.x, 1e-4f),
                    1f / Mathf.Max(ls.y, 1e-4f),
                    1f / Mathf.Max(ls.z, 1e-4f));
                var sc = hitGo.AddComponent<SphereCollider>();
                sc.radius = worldRadius;
                sc.isTrigger = true;
            }
            else
            {
                hitGo = hit.gameObject;
            }

            if (go.GetComponent<XRSimpleInteractable>() == null && hitGo.GetComponent<XRSimpleInteractable>() == null)
                hitGo.AddComponent<XRSimpleInteractable>();
        }

        void EnsureLensButtons(FieldLens lens)
        {
            if (transform.Find("Button_Lens+") != null)
                return;
            Transform time1 = transform.Find("Button_Time1x");
            Vector3 origin = time1 != null
                ? time1.position
                : transform.position + new Vector3(-0.25f, 0.8f, 0.5f);
            BuildButton(origin + new Vector3(0f, 0f, 0.24f), "Lens +", () => lens.StepNext(), new Color(0.55f, 0.35f, 0.85f));
            BuildButton(origin + new Vector3(0f, 0f, 0.30f), "Lens -", () => lens.StepPrevious(), new Color(0.35f, 0.22f, 0.55f));
        }

        void EnsureScaleButtons(ScaleEngine engine)
        {
            if (transform.Find("Button_Scale+") != null)
                return;
            Transform lensPlus = transform.Find("Button_Lens+");
            Vector3 origin = lensPlus != null
                ? lensPlus.position
                : transform.position + new Vector3(-0.25f, 0.8f, 0.5f);
            BuildButton(origin + new Vector3(0f, 0f, 0.06f), "Scale +", () => engine.StepIn(), new Color(0.15f, 0.62f, 0.72f));
            BuildButton(origin + new Vector3(0f, 0f, 0.12f), "Scale -", () => engine.StepOut(), new Color(0.10f, 0.42f, 0.52f));
        }

        public void EnsureExperimentFramework()
        {
            CacheChildren();
            if (_scaleEngine == null)
                EnsureScaleEngine();

            ExperimentRunner runner = GetComponent<ExperimentRunner>();
            if (runner == null)
                runner = gameObject.AddComponent<ExperimentRunner>();
            _experiment = runner;
            runner.SetLab(_coil, _circuit, _dipole);

            if (_readout != null)
                runner.BindReadout(_readout);

            ModelCard card = GetComponent<ModelCard>();
            if (card != null)
                runner.BindModelCard(card);

            ExperimentBoard board = EnsureExperimentBoard(runner);
            runner.BindBoard(board);

            if (runner.Definition == null || string.IsNullOrEmpty(runner.Definition.id))
                runner.ApplyCannedDefinition();

            EnsureExperimentButtons(runner);

            if (runner.State == ExperimentState.Idle)
                runner.Arm();
        }

        ExperimentBoard EnsureExperimentBoard(ExperimentRunner runner)
        {
            Transform existing = transform.Find("ExperimentBoard");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("ExperimentBoard");
                go.transform.SetParent(transform, true);

                var tmpGo = new GameObject("Text");
                tmpGo.transform.SetParent(go.transform, false);
                var tmp = tmpGo.AddComponent<TextMeshPro>();
                tmp.text = "EXPERIMENT";
                tmp.fontSize = 0.18f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.color = new Color(0.90f, 0.93f, 0.80f);
                tmp.rectTransform.sizeDelta = new Vector2(0.62f, 0.52f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                    tmp.font = font;

                var boardMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardMesh.name = "Board";
                boardMesh.transform.SetParent(go.transform, false);
                boardMesh.transform.localPosition = new Vector3(0.22f, -0.14f, 0.01f);
                boardMesh.transform.localScale = new Vector3(0.64f, 0.50f, 0.008f);
                KillCollider(boardMesh);
                ApplyMat(boardMesh, MakeLit(new Color(0.06f, 0.07f, 0.05f)));

                var view = go.AddComponent<ExperimentBoard>();
                ApplyBoardFace(go, tmp);
                PlaceBoardAlongTableEdge(go.transform, SlotExperiment);
                view.Bind(runner, tmp);
                return view;
            }

            var viewExisting = go.GetComponent<ExperimentBoard>();
            if (viewExisting == null)
                viewExisting = go.AddComponent<ExperimentBoard>();
            TextMeshPro tmpExisting = go.GetComponentInChildren<TextMeshPro>();
            ApplyBoardFace(go, tmpExisting);
            PlaceBoardAlongTableEdge(go.transform, SlotExperiment);
            viewExisting.Bind(runner, tmpExisting);
            return viewExisting;
        }

        void EnsureExperimentButtons(ExperimentRunner runner)
        {
            if (transform.Find("Button_Record") != null)
                return;
            Transform scaleMinus = transform.Find("Button_Scale-");
            Vector3 origin = scaleMinus != null
                ? scaleMinus.position
                : transform.position + new Vector3(-0.25f, 0.8f, 0.5f);
            BuildButton(origin + new Vector3(0f, 0f, 0.06f), "Record", () => runner.Record(), new Color(0.20f, 0.70f, 0.35f));
            BuildButton(origin + new Vector3(0f, 0f, 0.12f), "Stop", () => runner.Stop(), new Color(0.72f, 0.22f, 0.18f));
            BuildButton(origin + new Vector3(0f, 0f, 0.18f), "Save", () => runner.Save(), new Color(0.25f, 0.45f, 0.75f));
            BuildButton(origin + new Vector3(0f, 0f, 0.24f), "Load", () => runner.LoadLatest(), new Color(0.20f, 0.35f, 0.60f));
            BuildButton(origin + new Vector3(0f, 0f, 0.30f), "Repeat", () => runner.Repeat(), new Color(0.55f, 0.45f, 0.15f));
        }


        public void EnsureScientist()
        {
            CacheChildren();
            if (_experiment == null)
                EnsureExperimentFramework();

            Scientist scientist = GetComponent<Scientist>();
            if (scientist == null)
                scientist = gameObject.AddComponent<Scientist>();
            _scientist = scientist;
            scientist.SetLab(_coil, _circuit, _dipole, _experiment);

            if (_readout != null)
                _readout.SetScientist(scientist);

            ScientistBoard board = EnsureScientistBoard(scientist);
            scientist.BindBoard(board);

            EnsureScientistButtons(scientist);
        }

        ScientistBoard EnsureScientistBoard(Scientist scientist)
        {
            Transform existing = transform.Find("ScientistBoard");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("ScientistBoard");
                go.transform.SetParent(transform, true);

                var tmpGo = new GameObject("Text");
                tmpGo.transform.SetParent(go.transform, false);
                var tmp = tmpGo.AddComponent<TextMeshPro>();
                tmp.text = "SCIENTIST";
                tmp.fontSize = 0.22f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.color = new Color(0.88f, 0.94f, 0.82f);
                tmp.rectTransform.sizeDelta = new Vector2(0.92f, 0.78f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                    tmp.font = font;

                var boardMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardMesh.name = "Board";
                boardMesh.transform.SetParent(go.transform, false);
                boardMesh.transform.localPosition = new Vector3(0.34f, -0.22f, 0.012f);
                boardMesh.transform.localScale = new Vector3(0.96f, 0.78f, 0.010f);
                KillCollider(boardMesh);
                ApplyMat(boardMesh, MakeLit(new Color(0.05f, 0.08f, 0.06f)));

                var view = go.AddComponent<ScientistBoard>();
                ApplyBoardFace(go, tmp);
                PlaceBoardAlongTableEdge(go.transform, SlotScientist);
                view.Bind(scientist, _experiment, tmp);
                go.SetActive(true);
                return view;
            }

            var viewExisting = go.GetComponent<ScientistBoard>();
            if (viewExisting == null)
                viewExisting = go.AddComponent<ScientistBoard>();
            TextMeshPro tmpExisting = go.GetComponentInChildren<TextMeshPro>();
            ApplyBoardFace(go, tmpExisting);
            PlaceBoardAlongTableEdge(go.transform, SlotScientist);
            viewExisting.Bind(scientist, _experiment, tmpExisting);
            go.SetActive(true);
            return viewExisting;
        }


        void EnsureScientistBoardButtons(Scientist scientist)
        {
            Transform board = transform.Find("ScientistBoard");
            if (board == null)
                return;
            if (board.Find("Button_Q6") != null)
                return;
            Vector3 origin = board.position + board.right * -0.42f + board.up * -0.28f;
            if (board.Find("Button_Q1") != null)
            {
                if (board.Find("Button_Q5") == null)
                    BuildButton(origin + board.right * 0.28f, "Q5", () => scientist.SelectWhereMuscleEnergy(), new Color(0.45f, 0.55f, 0.22f)).transform.SetParent(board, true);
                BuildButton(origin + board.right * 0.49f, "Q6", () => scientist.SelectIsEnergyCreated(), new Color(0.62f, 0.38f, 0.18f)).transform.SetParent(board, true);
                return;
            }
            BuildButton(origin, "Q1", () => scientist.SelectDoubleVelocity(), new Color(0.25f, 0.55f, 0.35f)).transform.SetParent(board, true);
            BuildButton(origin + board.right * 0.07f, "Q2", () => scientist.SelectDoubleN(), new Color(0.25f, 0.50f, 0.55f)).transform.SetParent(board, true);
            BuildButton(origin + board.right * 0.14f, "Q3", () => scientist.SelectDoubleR(), new Color(0.50f, 0.40f, 0.20f)).transform.SetParent(board, true);
            BuildButton(origin + board.right * 0.21f, "Q4", () => scientist.SelectWhyCopper(), new Color(0.55f, 0.32f, 0.18f)).transform.SetParent(board, true);
            BuildButton(origin + board.right * 0.28f, "Q5", () => scientist.SelectWhereMuscleEnergy(), new Color(0.45f, 0.55f, 0.22f)).transform.SetParent(board, true);
            BuildButton(origin + board.right * 0.35f, "Q6", () => scientist.SelectIsEnergyCreated(), new Color(0.62f, 0.38f, 0.18f)).transform.SetParent(board, true);
            BuildButton(origin + board.right * 0.42f, "Form hypothesis", () => scientist.FormHypothesis(), new Color(0.45f, 0.35f, 0.70f)).transform.SetParent(board, true);
            BuildButton(origin + board.right * 0.49f, "Arm experiment", () => scientist.ArmExperiment(), new Color(0.70f, 0.35f, 0.20f)).transform.SetParent(board, true);
        }

        public void EnsureChemistry()
        {
            CacheChildren();
            if (_scientist == null)
                EnsureScientist();

            Transform existing = transform.Find("ChemistryBoard");
            GameObject go;
            TextMeshPro tmp;
            if (existing != null)
            {
                go = existing.gameObject;
                go.SetActive(true);
                tmp = go.GetComponentInChildren<TextMeshPro>();
            }
            else
            {
                go = new GameObject("ChemistryBoard");
                go.transform.SetParent(transform, true);

                var tmpGo = new GameObject("Text");
                tmpGo.transform.SetParent(go.transform, false);
                tmp = tmpGo.AddComponent<TextMeshPro>();
                tmp.text = "Cu";
                tmp.fontSize = 0.20f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.color = new Color(0.95f, 0.82f, 0.55f);
                tmp.rectTransform.sizeDelta = new Vector2(0.86f, 0.70f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                    tmp.font = font;

                var boardMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardMesh.name = "Board";
                boardMesh.transform.SetParent(go.transform, false);
                boardMesh.transform.localPosition = new Vector3(0.32f, -0.20f, 0.012f);
                boardMesh.transform.localScale = new Vector3(0.90f, 0.72f, 0.010f);
                KillCollider(boardMesh);
                ApplyMat(boardMesh, MakeLit(new Color(0.12f, 0.07f, 0.04f)));
            }

            ChemistryBoard view = go.GetComponent<ChemistryBoard>();
            if (view == null)
                view = go.AddComponent<ChemistryBoard>();
            ApplyBoardFace(go, tmp);
            PlaceBoardAlongTableEdge(go.transform, SlotChemistry);
            view.Bind(_fieldLens, _scaleEngine, tmp);

            EnsureCoilElementCard();
        }

        void EnsureCoilElementCard()
        {
            if (_coil == null)
                return;
            Transform existing = _coil.transform.Find("CuCard");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("CuCard");
                go.transform.SetParent(_coil.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.fontSize = 0.14f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.95f, 0.78f, 0.42f);
                tmp.rectTransform.sizeDelta = new Vector2(0.55f, 0.28f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                    tmp.font = font;
                tmp.text = Element.Cu.Symbol + " Z=" + Element.Cu.Z + "\n" + Element.Cu.ElectronShells
                    + "\nconductor (conceptual metals)\n" + Element.ConceptualHonesty;
            }
            go.SetActive(true);
        }

        static Transform FindPlayerEye()
        {
            GameObject xr = GameObject.Find("XR Origin");
            if (xr != null)
            {
                Camera xrCam = xr.GetComponentInChildren<Camera>(true);
                if (xrCam != null)
                    return xrCam.transform;
                return xr.transform;
            }
            Camera cam = Camera.main;
            if (cam != null)
                return cam.transform;
            return null;
        }

        void EnsureScientistButtons(Scientist scientist)

        {
            if (transform.Find("Button_Q1") != null)
            {
                if (transform.Find("Button_Q5") == null)
                {
                    Transform q4 = transform.Find("Button_Q4");
                    Vector3 q5pos = q4 != null
                        ? q4.position + new Vector3(0f, 0f, 0.42f)
                        : transform.position + new Vector3(-0.25f, 0.8f, 0.92f);
                    BuildButton(q5pos, "Q5", () => scientist.SelectWhereMuscleEnergy(), new Color(0.45f, 0.55f, 0.22f));
                }
                if (transform.Find("Button_Q6") == null)
                {
                    Transform q5 = transform.Find("Button_Q5");
                    Vector3 q6pos = q5 != null
                        ? q5.position + new Vector3(0f, 0f, 0.06f)
                        : transform.position + new Vector3(-0.25f, 0.8f, 0.98f);
                    BuildButton(q6pos, "Q6", () => scientist.SelectIsEnergyCreated(), new Color(0.62f, 0.38f, 0.18f));
                }
                EnsureScientistBoardButtons(scientist);
                return;
            }
            Transform repeat = transform.Find("Button_Repeat");
            Vector3 origin = repeat != null
                ? repeat.position
                : transform.position + new Vector3(-0.25f, 0.8f, 0.5f);
            BuildButton(origin + new Vector3(0f, 0f, 0.06f), "Q1", () => scientist.SelectDoubleVelocity(), new Color(0.25f, 0.55f, 0.35f));
            BuildButton(origin + new Vector3(0f, 0f, 0.12f), "Q2", () => scientist.SelectDoubleN(), new Color(0.25f, 0.50f, 0.55f));
            BuildButton(origin + new Vector3(0f, 0f, 0.18f), "Q3", () => scientist.SelectDoubleR(), new Color(0.50f, 0.40f, 0.20f));
            BuildButton(origin + new Vector3(0f, 0f, 0.24f), "Q4", () => scientist.SelectWhyCopper(), new Color(0.55f, 0.32f, 0.18f));
            BuildButton(origin + new Vector3(0f, 0f, 0.30f), "Q5", () => scientist.SelectWhereMuscleEnergy(), new Color(0.45f, 0.55f, 0.22f));
            BuildButton(origin + new Vector3(0f, 0f, 0.36f), "Q6", () => scientist.SelectIsEnergyCreated(), new Color(0.62f, 0.38f, 0.18f));
            BuildButton(origin + new Vector3(0f, 0f, 0.42f), "Form hypothesis", () => scientist.FormHypothesis(), new Color(0.45f, 0.35f, 0.70f));
            BuildButton(origin + new Vector3(0f, 0f, 0.48f), "Arm experiment", () => scientist.ArmExperiment(), new Color(0.70f, 0.35f, 0.20f));
            EnsureScientistBoardButtons(scientist);
        }

        public void EnsureBiology()
        {
            CacheChildren();
            if (_scientist == null)
                EnsureScientist();
            if (_fieldLens == null)
                EnsureFieldLens();
            if (_scaleEngine == null)
                EnsureScaleEngine();

            MuscleCell cell = EnsureMuscleCell();
            BiologyBoard board = EnsureBiologyBoard(cell);
            _muscleCell = cell;
            _biologyBoard = board;
        }

        MuscleCell EnsureMuscleCell()
        {
            Transform existing = transform.Find("MuscleCell");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("MuscleCell");
                go.transform.SetParent(transform, true);
            }
            go.SetActive(true);
            PlaceOnTableGrab(go.transform, -0.22f, 0.18f);

            MuscleCell cell = go.GetComponent<MuscleCell>();
            if (cell == null)
                cell = go.AddComponent<MuscleCell>();
            cell.EnsureBuilt();

            if (_fieldLens != null)
            {
                FieldLensTarget lensTarget = go.GetComponent<FieldLensTarget>();
                if (lensTarget == null)
                    lensTarget = go.AddComponent<FieldLensTarget>();
                lensTarget.ConfigureCell(_fieldLens);
            }

            if (_scaleEngine != null)
                BindScaleTarget(go, _scaleEngine);

            _muscleCell = cell;
            return cell;
        }

        BiologyBoard EnsureBiologyBoard(MuscleCell cell)
        {
            Transform existing = transform.Find("BiologyBoard");
            GameObject go;
            TextMeshPro tmp;
            if (existing != null)
            {
                go = existing.gameObject;
                go.SetActive(true);
                tmp = go.GetComponentInChildren<TextMeshPro>();
            }
            else
            {
                go = new GameObject("BiologyBoard");
                go.transform.SetParent(transform, true);

                var tmpGo = new GameObject("Text");
                tmpGo.transform.SetParent(go.transform, false);
                tmp = tmpGo.AddComponent<TextMeshPro>();
                tmp.text = "BIOLOGY";
                tmp.fontSize = 0.18f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.color = new Color(0.82f, 0.95f, 0.78f);
                tmp.rectTransform.sizeDelta = new Vector2(0.92f, 0.82f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                    tmp.font = font;

                var boardMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardMesh.name = "Board";
                boardMesh.transform.SetParent(go.transform, false);
                boardMesh.transform.localPosition = new Vector3(0.34f, -0.24f, 0.012f);
                boardMesh.transform.localScale = new Vector3(0.96f, 0.82f, 0.010f);
                KillCollider(boardMesh);
                ApplyMat(boardMesh, MakeLit(new Color(0.05f, 0.10f, 0.06f)));
            }

            BiologyBoard view = go.GetComponent<BiologyBoard>();
            if (view == null)
                view = go.AddComponent<BiologyBoard>();
            ApplyBoardFace(go, tmp);
            PlaceBoardAlongTableEdge(go.transform, SlotBiology);
            view.Bind(_fieldLens, _scaleEngine, cell, tmp);
            go.SetActive(true);
            _biologyBoard = view;
            return view;
        }


        public void EnsureLabStyle()
        {
            CircuitLabStyleApplier.EnsureApplied();
        }


        public void EnsureThermo()
        {
            CacheChildren();
            if (_scientist == null)
                EnsureScientist();
            if (_fieldLens == null)
                EnsureFieldLens();
            if (_scaleEngine == null)
                EnsureScaleEngine();

            HeatReservoir hot = EnsureReservoir("HeatHot", true, ThermoEnergy.DefaultTHot);
            HeatReservoir cold = EnsureReservoir("HeatCold", false, ThermoEnergy.DefaultTCold);
            HeatCoupler coupler = EnsureCoupler(hot, cold);
            PlaceHeatCluster(hot, cold, coupler);
            LabState.EnsureOn(gameObject);
            _heatCoupler = coupler;
        }

        HeatReservoir EnsureReservoir(string name, bool isHot, float kelvin)
        {
            Transform existing = transform.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(transform, true);
            }
            go.SetActive(true);
            HeatReservoir res = go.GetComponent<HeatReservoir>();
            if (res == null)
                res = go.AddComponent<HeatReservoir>();
            res.Configure(isHot, kelvin);
            res.EnsureBuilt();
            if (_fieldLens != null)
            {
                FieldLensTarget lensTarget = go.GetComponent<FieldLensTarget>();
                if (lensTarget == null)
                    lensTarget = go.AddComponent<FieldLensTarget>();
                lensTarget.ConfigureThermo(_fieldLens);
            }
            if (_scaleEngine != null)
                BindScaleTarget(go, _scaleEngine);
            return res;
        }

        HeatCoupler EnsureCoupler(HeatReservoir hot, HeatReservoir cold)
        {
            Transform existing = transform.Find("HeatCoupler");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("HeatCoupler");
                go.transform.SetParent(transform, true);
            }
            go.SetActive(true);
            HeatCoupler coupler = go.GetComponent<HeatCoupler>();
            if (coupler == null)
                coupler = go.AddComponent<HeatCoupler>();
            coupler.Bind(hot, cold);
            coupler.EnsureBuilt();
            if (_fieldLens != null)
            {
                FieldLensTarget lensTarget = go.GetComponent<FieldLensTarget>();
                if (lensTarget == null)
                    lensTarget = go.AddComponent<FieldLensTarget>();
                lensTarget.ConfigureThermo(_fieldLens);
            }
            if (_scaleEngine != null)
                BindScaleTarget(go, _scaleEngine);
            return coupler;
        }

        void PlaceHeatCluster(HeatReservoir hot, HeatReservoir cold, HeatCoupler coupler)
        {
            if (hot == null || cold == null || coupler == null)
                return;

            Transform table = FindNamedContains("circuittable", "circuit table", "workbench", "labbench");
            Transform breadboard = FindNamedContains("breadboard");
            Transform surface = table != null ? table : breadboard;
            Vector3 hotPos;
            Vector3 coldPos;
            Vector3 couplerPos;
            Quaternion couplerRot = Quaternion.Euler(0f, 0f, 90f);

            if (surface != null)
            {
                Bounds b = CollectBounds(surface);
                float y = b.max.y + 0.045f;
                Vector3 right = Vector3.right;
                Vector3 fwd = Vector3.forward;
                Transform eye = FindPlayerEye();
                if (eye != null)
                {
                    fwd = eye.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 1e-6f)
                        fwd = Vector3.forward;
                    fwd.Normalize();
                    right = Vector3.Cross(Vector3.up, fwd);
                    if (right.sqrMagnitude < 1e-6f)
                        right = Vector3.right;
                    right.y = 0f;
                    right.Normalize();
                }
                Vector3 along = b.center + right * (b.extents.x * 0.72f);
                along.y = y;
                Vector3 span = fwd * 0.14f;
                hotPos = along - span;
                coldPos = along + span;
                couplerPos = along + right * 0.16f;
                couplerPos.y = y;
                Vector3 axis = right;
                couplerRot = Quaternion.FromToRotation(Vector3.up, axis);
            }
            else
            {
                Transform eye = FindPlayerEye();
                Vector3 origin = fallbackPosition;
                Vector3 fwd = Vector3.forward;
                Vector3 right = Vector3.right;
                if (eye != null)
                {
                    fwd = eye.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 1e-6f)
                        fwd = Vector3.forward;
                    fwd.Normalize();
                    right = Vector3.Cross(Vector3.up, fwd);
                    right.y = 0f;
                    if (right.sqrMagnitude < 1e-6f)
                        right = Vector3.right;
                    right.Normalize();
                    origin = eye.position + fwd * 1.15f + right * 0.55f;
                    origin.y = Mathf.Max(0.85f, fallbackPosition.y);
                }
                hotPos = origin - fwd * 0.14f;
                coldPos = origin + fwd * 0.14f;
                couplerPos = origin + right * 0.16f;
                couplerRot = Quaternion.FromToRotation(Vector3.up, right);
            }

            hotPos = NudgeAwayFrom(hotPos, 0.06f, "MuscleCell", "BiologyBoard", "ScientistBoard", "ChemistryBoard");
            coldPos = NudgeAwayFrom(coldPos, 0.06f, "MuscleCell", "BiologyBoard", "ScientistBoard", "ChemistryBoard");
            couplerPos = NudgeAwayFrom(couplerPos, 0.08f, "MuscleCell", "BiologyBoard", "ScientistBoard", "ChemistryBoard");

            hot.transform.position = hotPos;
            cold.transform.position = coldPos;
            coupler.transform.position = couplerPos;
            coupler.transform.rotation = couplerRot;
        }

        Vector3 NudgeAwayFrom(Vector3 pos, float radius, params string[] childNames)
        {
            for (int n = 0; n < childNames.Length; n++)
            {
                Transform t = transform.Find(childNames[n]);
                if (t == null)
                    continue;
                Bounds b = CollectBounds(t);
                b.Expand(0.04f);
                if (!b.Contains(pos) && (b.ClosestPoint(pos) - pos).sqrMagnitude > radius * radius)
                    continue;
                pos.x = b.max.x + radius + 0.08f;
            }
            return pos;
        }

        public void EnsureLedger()
        {
            CacheChildren();
            if (_heatCoupler == null)
                EnsureThermo();

            Transform existing = transform.Find("ConservationBoard");
            GameObject go;
            TextMeshPro tmp;
            if (existing != null)
            {
                go = existing.gameObject;
                go.SetActive(true);
                tmp = go.GetComponentInChildren<TextMeshPro>();
            }
            else
            {
                go = new GameObject("ConservationBoard");
                go.transform.SetParent(transform, true);

                var tmpGo = new GameObject("Text");
                tmpGo.transform.SetParent(go.transform, false);
                tmp = tmpGo.AddComponent<TextMeshPro>();
                tmp.text = "CONSERVATION";
                tmp.fontSize = 0.16f;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.color = new Color(0.92f, 0.95f, 0.82f);
                tmp.rectTransform.sizeDelta = new Vector2(1.08f, 0.98f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                    tmp.font = font;

                var boardMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardMesh.name = "Board";
                boardMesh.transform.SetParent(go.transform, false);
                boardMesh.transform.localPosition = new Vector3(0.40f, -0.28f, 0.012f);
                boardMesh.transform.localScale = new Vector3(1.12f, 0.98f, 0.010f);
                KillCollider(boardMesh);
                ApplyMat(boardMesh, MakeLit(new Color(0.05f, 0.07f, 0.06f)));
            }

            ConservationBoard view = go.GetComponent<ConservationBoard>();
            if (view == null)
                view = go.AddComponent<ConservationBoard>();
            ApplyBoardFace(go, tmp);
            PlaceBoardAlongTableEdge(go.transform, SlotConservation);
            view.Bind(_circuit, _heatCoupler, _fieldLens, _scaleEngine, tmp);
            go.SetActive(true);
            _conservationBoard = view;
        }


    }
}
