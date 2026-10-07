using UnityEngine;
using Unity.XR.CoreUtils;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.XR
{
    /// <summary>
    /// Comfort tunnelling vignette for headset locomotion (Quest comfort guideline: reduce peripheral
    /// optic flow during artificial motion). Darkens the edges of your view only while the RIG moves
    /// (smooth stick walk, smooth turn, falling, being pushed by the CharacterController) and fades out
    /// when you stop. Physical head/body movement never triggers it (it watches the XR Origin, not the head).
    /// Teleport / arena / Giza travel (big one-frame jumps) and 45 deg snap turns are ignored.
    /// XR only (desktop and the Device Simulator without a running XR display are untouched).
    /// Settings > "Comfort vignette" cycles Mild (default) / Strong / Off, saved in PlayerPrefs.
    /// Rendering: one small procedural ring mesh (UI/Default, vertex alpha) on a 0.3 m sphere around the
    /// camera; the renderer is disabled while idle so there is zero fill-rate cost when standing still.
    /// Objects closer than 0.3 m to your eyes (a hand right at your face) can show through the dark edge.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(230)]
    public sealed class ComfortVignette : MonoBehaviour
    {
        public enum Mode { Off = 0, Mild = 1, Strong = 2 }

        public const string PrefKey = "qhysics.comfortVignette";
        const int Segments = 48;
        const float RadiusM = 0.3f;
        const float OuterDeg = 120f;      // well past the Quest 3S field of view
        const float OpenDeg = 75f;        // idle aperture (outside the view, so nothing visible)
        const float MildMinDeg = 46f;     // aperture at full strength
        const float StrongMinDeg = 33f;
        const float FeatherDeg = 14f;
        const float SpeedStart = 0.15f;   // m/s before anything shows
        const float SpeedFull = 2.0f;     // matches the 2 m/s stick walk
        const float YawStart = 15f;       // deg/s
        const float YawFull = 90f;
        const float JumpM = 1.0f;         // per-frame rig jump => teleport/travel, ignore
        const float SnapDeg = 20f;        // per-frame yaw jump => snap turn, ignore

        static ComfortVignette _instance;
        static int _mode = -1;

        Camera _cam;
        Transform _rig;
        GameObject _go;
        MeshRenderer _mr;
        Mesh _mesh;
        Vector3[] _verts;
        Color32[] _cols;
        Vector3 _lastPos;
        float _lastYaw;
        bool _hasLast;
        float _speed, _yawRate, _strength;
        float _builtAperture = -1f;
        byte _builtAlpha;
        float _nextResolve;

        /// <summary>Current vignette strength 0..1 (0 = fully open / hidden).</summary>
        public static float Strength => _instance != null ? _instance._strength : 0f;

        public static Mode CurrentMode
        {
            get
            {
                if (_mode < 0)
                    _mode = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)Mode.Mild), 0, 2);
                return (Mode)_mode;
            }
        }

        public static void SetMode(Mode m)
        {
            _mode = Mathf.Clamp((int)m, 0, 2);
            PlayerPrefs.SetInt(PrefKey, _mode);
            PlayerPrefs.Save();
        }

        /// <summary>Mild -> Strong -> Off -> Mild.</summary>
        public static Mode CycleMode()
        {
            Mode next = CurrentMode == Mode.Mild ? Mode.Strong : CurrentMode == Mode.Strong ? Mode.Off : Mode.Mild;
            SetMode(next);
            return next;
        }

        public static string ModeLabel(Mode m) => m == Mode.Off ? "Off" : m == Mode.Strong ? "Strong" : "Mild";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsComfortVignette");
            _instance = host.AddComponent<ComfortVignette>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            _mode = -1; // re-read PlayerPrefs each Play (domain reload may be off)
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_go != null)
                Destroy(_go);
            if (_mesh != null)
                Destroy(_mesh);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            bool xr = DesktopPlayerController.IsXrDisplayRunning();
            if (xr && (_cam == null || _rig == null) && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.5f;
                Resolve();
            }

            float target = 0f;
            if (xr && _rig != null && _cam != null)
            {
                SampleRigMotion(dt);
                Mode mode = CurrentMode;
                if (mode != Mode.Off)
                {
                    float move = Mathf.InverseLerp(SpeedStart, SpeedFull, _speed);
                    float turn = Mathf.InverseLerp(YawStart, YawFull, _yawRate);
                    target = Mathf.Clamp01(Mathf.Max(move, turn));
                }
            }
            else
            {
                _hasLast = false;
                _speed = _yawRate = 0f;
            }

            // Close quickly when motion starts, open a bit slower when it stops.
            float rate = target > _strength ? 8f : 4f;
            _strength = Mathf.Lerp(_strength, target, 1f - Mathf.Exp(-rate * dt));
            if (target <= 0f && _strength < 0.01f)
                _strength = 0f;

            Apply();
        }

        void Resolve()
        {
            Camera cam = QhysicsUiBuilder.ResolveXrCamera();
            if (cam == null)
                return;
            if (cam != _cam)
            {
                _cam = cam;
                _hasLast = false;
                EnsureRenderer();
            }
            XROrigin origin = cam.GetComponentInParent<XROrigin>();
            if (origin == null)
            {
                GameObject named = GameObject.Find(LabPlayerSpawn.OriginName);
                if (named != null)
                    origin = named.GetComponent<XROrigin>();
            }
            Transform rig = origin != null ? origin.transform
                : (cam.transform.parent != null && cam.transform.parent.parent != null ? cam.transform.parent.parent : cam.transform.parent);
            if (rig != _rig)
            {
                _rig = rig;
                _hasLast = false;
            }
        }

        void SampleRigMotion(float dt)
        {
            Vector3 pos = _rig.position;
            float yaw = _rig.eulerAngles.y;
            if (!_hasLast)
            {
                _lastPos = pos;
                _lastYaw = yaw;
                _hasLast = true;
                return;
            }
            Vector3 d = pos - _lastPos;
            float dYaw = Mathf.DeltaAngle(_lastYaw, yaw);
            _lastPos = pos;
            _lastYaw = yaw;

            float rawSpeed, rawYaw;
            if (d.magnitude > JumpM)
                rawSpeed = 0f; // teleport / arena / Giza travel
            else
            {
                Vector3 flat = new Vector3(d.x, 0f, d.z);
                rawSpeed = (flat.magnitude + Mathf.Abs(d.y) * 0.5f) / dt;
            }
            rawYaw = Mathf.Abs(dYaw) > SnapDeg ? 0f : Mathf.Abs(dYaw) / dt;

            // The CharacterController moves in FixedUpdate, so per-frame deltas pulse; low-pass them.
            float k = 1f - Mathf.Exp(-10f * dt);
            _speed = Mathf.Lerp(_speed, rawSpeed, k);
            _yawRate = Mathf.Lerp(_yawRate, rawYaw, k);
        }

        void EnsureRenderer()
        {
            if (_cam == null)
                return;
            if (_go == null)
            {
                _go = new GameObject("ComfortVignetteMesh");
                _mesh = new Mesh { name = "ComfortVignette" };
                _mesh.MarkDynamic();
                BuildTopology();
                _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _mr = _go.AddComponent<MeshRenderer>();
                _mr.sharedMaterial = MakeMaterial();
                _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mr.receiveShadows = false;
                _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                _mr.allowOcclusionWhenDynamic = false;
                _mr.enabled = false;
                Debug.Log("ComfortVignette: ready (mode " + ModeLabel(CurrentMode) + ", shader "
                    + (_mr.sharedMaterial != null && _mr.sharedMaterial.shader != null ? _mr.sharedMaterial.shader.name : "none") + ").");
            }
            _go.transform.SetParent(_cam.transform, false);
            _go.transform.localPosition = Vector3.zero;
            _go.transform.localRotation = Quaternion.identity;
            _go.transform.localScale = Vector3.one;
            _go.layer = (_cam.cullingMask & 1) != 0 ? 0 : _cam.gameObject.layer;
            _builtAperture = -1f;
        }

        void BuildTopology()
        {
            int n = Segments;
            _verts = new Vector3[n * 3];
            _cols = new Color32[n * 3];
            var tris = new int[n * 12];
            int t = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                for (int r = 0; r < 2; r++)
                {
                    int a = r * n + i, b = r * n + j, c = (r + 1) * n + i, e = (r + 1) * n + j;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = e;
                }
            }
            _mesh.vertices = _verts;
            _mesh.colors32 = _cols;
            _mesh.triangles = tris;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (RadiusM * 2.5f));
        }

        void Apply()
        {
            if (_mr == null)
                return;
            bool show = _strength > 0.01f;
            if (_mr.enabled != show)
                _mr.enabled = show;
            if (!show)
                return;

            float minDeg = CurrentMode == Mode.Strong ? StrongMinDeg : MildMinDeg;
            float aperture = Mathf.Lerp(OpenDeg, minDeg, _strength);
            byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(_strength * 3f));
            if (Mathf.Abs(aperture - _builtAperture) < 0.25f && alpha == _builtAlpha)
                return;
            _builtAperture = aperture;
            _builtAlpha = alpha;

            int n = Segments;
            float[] ringDeg = { aperture, Mathf.Min(aperture + FeatherDeg, OuterDeg), OuterDeg };
            byte[] ringA = { 0, alpha, alpha };
            for (int r = 0; r < 3; r++)
            {
                float th = ringDeg[r] * Mathf.Deg2Rad;
                float s = Mathf.Sin(th) * RadiusM, z = Mathf.Cos(th) * RadiusM;
                var col = new Color32(0, 0, 0, ringA[r]);
                for (int i = 0; i < n; i++)
                {
                    float ph = (i / (float)n) * Mathf.PI * 2f;
                    _verts[r * n + i] = new Vector3(Mathf.Cos(ph) * s, Mathf.Sin(ph) * s, z);
                    _cols[r * n + i] = col;
                }
            }
            _mesh.vertices = _verts;
            _mesh.colors32 = _cols;
        }

        static Material MakeMaterial()
        {
            // UI/Default and Sprites/Default: vertex colour * alpha blend, Cull Off, ZWrite Off, stereo-instancing safe.
            Shader sh = Shader.Find("UI/Default");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            Material m;
            if (sh != null)
            {
                m = new Material(sh);
                m.color = Color.white;
            }
            else
            {
                sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null)
                    return null;
                m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            m.name = "ComfortVignette";
            m.renderQueue = 4000; // after world + world-space UI, so the edges stay dark over panels
            return m;
        }
    }
}