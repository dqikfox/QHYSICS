using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS buoyancy / Archimedes: lumped float fraction = rho_o/rho_f.
    /// Honesty: F_b=rho_f V_sub g - NOT viscous drag Cd, not free-surface waves, not 3D rigidbody fluid sim.
    /// XR activate toggles RUN/PAUSE or resets drop; N/P cycles object density; Shift+N/P cycles fluid. Desktop via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadBuoyancyGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Buoyancy";
        public const string Honesty =
            "Lumped Archimedes buoyancy: F_b=rho_f V_sub g, float fraction=rho_o/rho_f. NOT viscous drag Cd, not free-surface waves, not 3D rigidbody fluid sim.";

        // Fluid density presets (kg/m^3)
        static readonly float[] PresetsFluid = { 1000f, 900f, 1025f, 13500f };
        static readonly string[] PresetFluidLabels = { "WATER", "OIL", "SEA", "HG" };

        // Object density presets (kg/m^3)
        static readonly float[] PresetsObject = { 240f, 600f, 917f, 2700f };
        static readonly string[] PresetObjectLabels = { "CORK", "WOOD", "ICE", "ALUM" };

        const float VolumeM3 = 0.001f; // 1 liter
        const float G = 9.81f;
        const float NeutralEps = 5f; // kg/m^3 — treat as SUSPEND when |rho_o-rho_f| < eps
        const float CubeVisual = 0.06f; // lab visual edge (represents 0.1 m physical cube)
        const float TankW = 0.14f;
        const float TankD = 0.14f;
        const float TankH = 0.22f;
        const float FluidFillH = 0.16f;
        const float TankFloorY = 0.04f;
        const float SettleTau = 0.35f; // seconds toward target fraction

        enum RunState { Paused, Running }

        TextMeshPro _readout;
        Transform _base;
        Transform _tank;
        Transform _fluid;
        Transform _cube;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _tankMat;
        Material _fluidMat;
        Material _cubeMat;
        float _refreshAt;
        float _inputCooldown;
        int _objIndex = 1; // WOOD default
        int _fluidIndex = 0; // WATER default
        RunState _state = RunState.Paused;
        float _frac; // current submerged fraction 0..1 (visual)
        float _vel; // damped settle velocity

        public float ActiveRhoO => PresetsObject[Mathf.Clamp(_objIndex, 0, PresetsObject.Length - 1)];
        public float ActiveRhoF => PresetsFluid[Mathf.Clamp(_fluidIndex, 0, PresetsFluid.Length - 1)];
        public string ActiveObjLabel => PresetObjectLabels[Mathf.Clamp(_objIndex, 0, PresetObjectLabels.Length - 1)];
        public string ActiveFluidLabel => PresetFluidLabels[Mathf.Clamp(_fluidIndex, 0, PresetFluidLabels.Length - 1)];
        public float WeightN => ActiveRhoO * VolumeM3 * G;
        public float TargetFraction
        {
            get
            {
                float rf = ActiveRhoF;
                if (rf < 1e-6f) return 1f;
                float ratio = ActiveRhoO / rf;
                return Mathf.Clamp01(ratio);
            }
        }
        public float VSub => VolumeM3 * Mathf.Clamp01(_frac);
        public float BuoyancyN => ActiveRhoF * VSub * G;
        public bool IsRunning => _state == RunState.Running;

        public string BuoyancyState
        {
            get
            {
                float d = Mathf.Abs(ActiveRhoO - ActiveRhoF);
                if (d < NeutralEps) return "SUSPEND";
                if (ActiveRhoO < ActiveRhoF) return "FLOAT";
                return "SINK";
            }
        }

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            ApplyCubePose();
            ApplyFluidTint();
            ApplyCubeTint();
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            WireGrab();
        }

        void OnDisable()
        {
            UnwireGrab();
        }

        void OnDestroy()
        {
            UnwireGrab();
            DestroyMat(ref _baseMat);
            DestroyMat(ref _tankMat);
            DestroyMat(ref _fluidMat);
            DestroyMat(ref _cubeMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }

        void FixedUpdate()
        {
            if (_state != RunState.Running)
                return;
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f)
                return;

            float target = TargetFraction;
            // Critically-damped-ish settle toward equilibrium submerged fraction
            float err = target - _frac;
            _vel += err * (1f / Mathf.Max(0.05f, SettleTau)) * dt;
            _vel *= Mathf.Exp(-3.5f * dt); // damping
            _frac += _vel * dt;
            if (_frac < 0f) { _frac = 0f; _vel = 0f; }
            if (_frac > 1f) { _frac = 1f; _vel = 0f; }
            // Snap when close
            if (Mathf.Abs(err) < 0.002f && Mathf.Abs(_vel) < 0.01f)
            {
                _frac = target;
                _vel = 0f;
            }
            ApplyCubePose();
        }

        void Update()
        {
            PollDesktopCycle();
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.06f;
            RefreshText();
            ApplyGlow();
        }

        void WireGrab()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }

        void UnwireGrab()
        {
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
        }

        void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
        {
            ToggleRunOrReset();
        }

        void PollDesktopCycle()
        {
            if (Time.unscaledTime < _inputCooldown)
                return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            if ((cam.transform.position - transform.position).sqrMagnitude > 2.25f)
                return;

            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.nKey.wasPressedThisFrame)
            {
                if (shift) CycleFluid(+1);
                else CycleObject(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleFluid(-1);
                else CycleObject(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleObject(int delta)
        {
            if (PresetsObject.Length == 0)
                return;
            _objIndex = (_objIndex + delta) % PresetsObject.Length;
            if (_objIndex < 0)
                _objIndex += PresetsObject.Length;
            ApplyCubeTint();
            if (_state == RunState.Running)
                BeginDrop();
            RefreshText();
        }

        public void CycleFluid(int delta)
        {
            if (PresetsFluid.Length == 0)
                return;
            _fluidIndex = (_fluidIndex + delta) % PresetsFluid.Length;
            if (_fluidIndex < 0)
                _fluidIndex += PresetsFluid.Length;
            ApplyFluidTint();
            if (_state == RunState.Running)
                BeginDrop();
            RefreshText();
        }

        public void DesktopActivate(int delta)
        {
            if (delta != 0)
                CycleObject(delta);
            ToggleRunOrReset();
        }

        // Activate: if paused, start drop from top; if running, pause (hold pose).
        void ToggleRunOrReset()
        {
            if (_state == RunState.Running)
            {
                _state = RunState.Paused;
                _vel = 0f;
            }
            else
            {
                BeginDrop();
            }
            RefreshText();
        }

        void BeginDrop()
        {
            // Reset drop from top (barely submerged), then settle toward equilibrium.
            _frac = 0f;
            _vel = 0.8f;
            _state = RunState.Running;
            ApplyCubePose();
            RefreshText();
        }

        public void Pause()
        {
            _state = RunState.Paused;
            _vel = 0f;
            RefreshText();
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _base = transform.Find("Base");
            if (_base == null)
            {
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseGo.name = "Base";
                baseGo.transform.SetParent(transform, false);
                baseGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                baseGo.transform.localRotation = Quaternion.identity;
                baseGo.transform.localScale = new Vector3(0.16f, 0.02f, 0.16f);
                Object.Destroy(baseGo.GetComponent<Collider>());
                _base = baseGo.transform;
            }
            var br = _base.GetComponent<Renderer>();
            if (br != null)
            {
                if (_baseMat == null)
                {
                    _baseMat = new Material(sh);
                    var dark = new Color(0.12f, 0.14f, 0.16f, 1f);
                    if (_baseMat.HasProperty("_BaseColor"))
                        _baseMat.SetColor("_BaseColor", dark);
                    _baseMat.color = dark;
                    if (_baseMat.HasProperty("_Metallic"))
                        _baseMat.SetFloat("_Metallic", 0.35f);
                    if (_baseMat.HasProperty("_Smoothness"))
                        _baseMat.SetFloat("_Smoothness", 0.45f);
                }
                br.sharedMaterial = _baseMat;
            }

            _tank = transform.Find("Tank");
            if (_tank == null)
            {
                var tankGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tankGo.name = "Tank";
                tankGo.transform.SetParent(transform, false);
                tankGo.transform.localPosition = new Vector3(0f, TankFloorY + TankH * 0.5f, 0f);
                tankGo.transform.localRotation = Quaternion.identity;
                tankGo.transform.localScale = new Vector3(TankW, TankH, TankD);
                Object.Destroy(tankGo.GetComponent<Collider>());
                _tank = tankGo.transform;
            }
            var tr = _tank.GetComponent<Renderer>();
            if (tr != null)
            {
                if (_tankMat == null)
                {
                    _tankMat = new Material(sh);
                    MakeTransparent(_tankMat, new Color(0.55f, 0.70f, 0.78f, 0.18f));
                    if (_tankMat.HasProperty("_Metallic"))
                        _tankMat.SetFloat("_Metallic", 0.1f);
                    if (_tankMat.HasProperty("_Smoothness"))
                        _tankMat.SetFloat("_Smoothness", 0.7f);
                }
                tr.sharedMaterial = _tankMat;
            }

            _fluid = transform.Find("Fluid");
            if (_fluid == null)
            {
                var fluidGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fluidGo.name = "Fluid";
                fluidGo.transform.SetParent(transform, false);
                fluidGo.transform.localRotation = Quaternion.identity;
                Object.Destroy(fluidGo.GetComponent<Collider>());
                _fluid = fluidGo.transform;
            }
            _fluid.localScale = new Vector3(TankW * 0.92f, FluidFillH, TankD * 0.92f);
            _fluid.localPosition = new Vector3(0f, TankFloorY + FluidFillH * 0.5f, 0f);
            var fr = _fluid.GetComponent<Renderer>();
            if (fr != null)
            {
                if (_fluidMat == null)
                {
                    _fluidMat = new Material(sh);
                    MakeTransparent(_fluidMat, new Color(0.15f, 0.70f, 0.85f, 0.45f));
                    if (_fluidMat.HasProperty("_Metallic"))
                        _fluidMat.SetFloat("_Metallic", 0.05f);
                    if (_fluidMat.HasProperty("_Smoothness"))
                        _fluidMat.SetFloat("_Smoothness", 0.85f);
                    if (_fluidMat.HasProperty("_EmissionColor"))
                    {
                        _fluidMat.EnableKeyword("_EMISSION");
                        _fluidMat.SetColor("_EmissionColor", new Color(0.15f, 0.70f, 0.85f) * 0.25f);
                    }
                }
                fr.sharedMaterial = _fluidMat;
            }

            _cube = transform.Find("Cube");
            if (_cube == null)
            {
                var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeGo.name = "Cube";
                cubeGo.transform.SetParent(transform, false);
                cubeGo.transform.localRotation = Quaternion.identity;
                cubeGo.transform.localScale = Vector3.one * CubeVisual;
                Object.Destroy(cubeGo.GetComponent<Collider>());
                _cube = cubeGo.transform;
            }
            var cr = _cube.GetComponent<Renderer>();
            if (cr != null)
            {
                if (_cubeMat == null)
                {
                    _cubeMat = new Material(sh);
                    var amber = new Color(0.85f, 0.62f, 0.22f, 1f);
                    if (_cubeMat.HasProperty("_BaseColor"))
                        _cubeMat.SetColor("_BaseColor", amber);
                    _cubeMat.color = amber;
                    if (_cubeMat.HasProperty("_Metallic"))
                        _cubeMat.SetFloat("_Metallic", 0.25f);
                    if (_cubeMat.HasProperty("_Smoothness"))
                        _cubeMat.SetFloat("_Smoothness", 0.55f);
                    if (_cubeMat.HasProperty("_EmissionColor"))
                    {
                        _cubeMat.EnableKeyword("_EMISSION");
                        _cubeMat.SetColor("_EmissionColor", amber * 0.35f);
                    }
                }
                cr.sharedMaterial = _cubeMat;
            }
        }

        static void MakeTransparent(Material mat, Color color)
        {
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend"))
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
        }

        void ApplyCubePose()
        {
            if (_cube == null)
                return;
            // Fluid surface Y (local)
            float surfaceY = TankFloorY + FluidFillH;
            float bottomY = TankFloorY + CubeVisual * 0.5f;
            // Fully submerged: cube center at surface - CubeVisual*0.5 (top flush with surface)
            // Fraction 0: cube sitting just above surface (drop start)
            float submergedDepth = Mathf.Clamp01(_frac) * CubeVisual;
            float centerY = surfaceY - submergedDepth + CubeVisual * 0.5f;
            // Clamp so cube doesn't go through tank floor
            float minCenter = bottomY;
            if (centerY < minCenter)
                centerY = minCenter;
            // Cap above tank when fraction ~0
            float maxCenter = surfaceY + CubeVisual * 0.55f;
            if (centerY > maxCenter)
                centerY = maxCenter;
            _cube.localPosition = new Vector3(0f, centerY, 0f);
        }

        void ApplyFluidTint()
        {
            if (_fluidMat == null)
                return;
            // Tint fluid by density: water cyan, oil greenish, sea deeper, mercury silver
            Color c;
            switch (_fluidIndex)
            {
                case 1: c = new Color(0.55f, 0.70f, 0.25f, 0.50f); break; // OIL
                case 2: c = new Color(0.10f, 0.45f, 0.70f, 0.50f); break; // SEA
                case 3: c = new Color(0.70f, 0.72f, 0.78f, 0.65f); break; // HG
                default: c = new Color(0.15f, 0.70f, 0.85f, 0.45f); break; // WATER
            }
            if (_fluidMat.HasProperty("_BaseColor"))
                _fluidMat.SetColor("_BaseColor", c);
            _fluidMat.color = c;
            if (_fluidMat.HasProperty("_EmissionColor"))
            {
                _fluidMat.EnableKeyword("_EMISSION");
                _fluidMat.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * 0.3f);
            }
        }

        void ApplyCubeTint()
        {
            if (_cubeMat == null)
                return;
            Color c;
            switch (_objIndex)
            {
                case 0: c = new Color(0.78f, 0.55f, 0.28f, 1f); break; // CORK
                case 2: c = new Color(0.75f, 0.88f, 0.95f, 1f); break; // ICE
                case 3: c = new Color(0.72f, 0.74f, 0.78f, 1f); break; // ALUM
                default: c = new Color(0.55f, 0.38f, 0.18f, 1f); break; // WOOD
            }
            if (_cubeMat.HasProperty("_BaseColor"))
                _cubeMat.SetColor("_BaseColor", c);
            _cubeMat.color = c;
            if (_cubeMat.HasProperty("_EmissionColor"))
            {
                _cubeMat.EnableKeyword("_EMISSION");
                _cubeMat.SetColor("_EmissionColor", c * 0.35f);
            }
        }

        void ApplyGlow()
        {
            if (_cubeMat == null || !_cubeMat.HasProperty("_EmissionColor"))
                return;
            Color c = _cubeMat.HasProperty("_BaseColor") ? _cubeMat.GetColor("_BaseColor") : _cubeMat.color;
            float glow = _state == RunState.Running ? 0.85f : 0.30f;
            _cubeMat.EnableKeyword("_EMISSION");
            _cubeMat.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * glow);
        }

        void BuildReadout()
        {
            Transform existing = transform.Find("Readout");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("Readout");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 20f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.65f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(60f, 42f);
            _readout.text = "BUOYANCY\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string run = _state == RunState.Running ? "RUN" : "PAUSE";
            string state = BuoyancyState;
            string ro = "ro " + ActiveRhoO.ToString("0") + " " + ActiveObjLabel;
            string rf = "rf " + ActiveRhoF.ToString("0") + " " + ActiveFluidLabel;
            string frac = "sub " + (_frac * 100f).ToString("0.0") + "%";
            string fb = "Fb " + BuoyancyN.ToString("0.000") + "N";
            string w = "W " + WeightN.ToString("0.000") + "N";

            _readout.text =
                "BUOY " + ActiveObjLabel + "/" + ActiveFluidLabel + "\n"
                + run + " " + state + "\n"
                + ro + "\n"
                + rf + "\n"
                + frac + "\n"
                + fb + "  " + w + "\n"
                + "N/P obj  Shift fluid\n"
                + "activate run/pause\n"
                + "[Archimedes]";
        }
    }
}
