using UnityEngine;
using Unity.XR.CoreUtils;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.XR
{
    /// <summary>
    /// Comfort "blink" for headset teleports (Quest comfort guideline: hide the instant viewpoint jump).
    /// When the XR rig jumps more than 1 m in a single frame (teleport arc, arena / Giza / station travel,
    /// chapter start, respawn) the view goes black for that frame and fades back in over about a quarter
    /// of a second, so you never see the world pop. Stick walking, falling and snap turns never blink
    /// (they are continuous or rotational; the comfort vignette covers walking).
    /// Watches the XR Origin only, so physical head/body movement never triggers it. Unscaled time, so it
    /// also works during Focus slow-mo or a paused sim. XR only (desktop and the Device Simulator without a
    /// running XR display are untouched). Settings > "Teleport blink" toggles On (default) / Off (PlayerPrefs).
    /// Rendering: one camera-parented quad (UI/Default, vertex alpha); renderer disabled when not blinking.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)] // after travel / teleport code that moves the rig in Update or LateUpdate
    public sealed class TeleportBlink : MonoBehaviour
    {
        public const string PrefKey = "qhysics.teleportBlink";
        const float JumpM = 1.0f;     // per-frame rig jump that counts as a teleport (matches ComfortVignette)
        const float HoldS = 0.06f;    // fully black
        const float FadeS = 0.22f;    // fade back in

        static TeleportBlink _instance;
        static int _enabled = -1;

        Camera _cam;
        Transform _rig;
        GameObject _go;
        MeshRenderer _mr;
        Mesh _mesh;
        Color32[] _cols;
        Vector3 _lastPos;
        bool _hasLast;
        float _blinkStart = -10f;
        byte _builtAlpha = 255;
        float _nextResolve;
        int _blinks;

        public static bool Enabled
        {
            get
            {
                if (_enabled < 0)
                    _enabled = PlayerPrefs.GetInt(PrefKey, 1) != 0 ? 1 : 0;
                return _enabled == 1;
            }
            set
            {
                _enabled = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefKey, _enabled);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Flips On/Off and returns the new state.</summary>
        public static bool Toggle()
        {
            Enabled = !Enabled;
            return Enabled;
        }

        public static string StateLabel => Enabled ? "On" : "Off";

        /// <summary>Lets travel code request a blink explicitly (e.g. a short hop under 1 m).</summary>
        public static void Blink()
        {
            if (_instance != null)
                _instance.StartBlink();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsTeleportBlink");
            _instance = host.AddComponent<TeleportBlink>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            _enabled = -1; // re-read PlayerPrefs each Play (domain reload may be off)
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
            bool xr = DesktopPlayerController.IsXrDisplayRunning();
            if (xr && (_cam == null || _rig == null) && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.5f;
                Resolve();
            }

            if (xr && _rig != null && _cam != null)
            {
                Vector3 pos = _rig.position;
                if (_hasLast && (pos - _lastPos).sqrMagnitude > JumpM * JumpM)
                    StartBlink();
                _lastPos = pos;
                _hasLast = true;
            }
            else
            {
                _hasLast = false;
            }

            Apply(xr);
        }

        void StartBlink()
        {
            if (!Enabled || _cam == null)
                return;
            EnsureRenderer();
            _blinkStart = Time.unscaledTime;
            _blinks++;
            if (_blinks == 1)
                Debug.Log("TeleportBlink: first blink (rig jump; Settings > Teleport blink " + StateLabel + ").");
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

        void EnsureRenderer()
        {
            if (_cam == null)
                return;
            if (_go == null)
            {
                _go = new GameObject("TeleportBlinkQuad");
                _mesh = new Mesh { name = "TeleportBlink" };
                _mesh.MarkDynamic();
                _mesh.vertices = new[]
                {
                    new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                    new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f)
                };
                _cols = new Color32[4];
                for (int i = 0; i < 4; i++)
                    _cols[i] = new Color32(0, 0, 0, 255);
                _mesh.colors32 = _cols;
                _mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                _mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0.1f));
                _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _mr = _go.AddComponent<MeshRenderer>();
                _mr.sharedMaterial = MakeMaterial();
                _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mr.receiveShadows = false;
                _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                _mr.allowOcclusionWhenDynamic = false;
                _mr.enabled = false;
                _builtAlpha = 255;
                Debug.Log("TeleportBlink: ready (" + StateLabel + ", shader "
                    + (_mr.sharedMaterial != null && _mr.sharedMaterial.shader != null ? _mr.sharedMaterial.shader.name : "none") + ").");
            }
            if (_go.transform.parent != _cam.transform)
                _go.transform.SetParent(_cam.transform, false);
            // Just past the near plane; half-size 6x the distance covers ~80 deg each side, past the Quest 3S FOV per eye.
            float d = Mathf.Max(_cam.nearClipPlane * 1.5f, 0.05f);
            _go.transform.localPosition = new Vector3(0f, 0f, d);
            _go.transform.localRotation = Quaternion.identity;
            _go.transform.localScale = Vector3.one * (d * 6f);
            _go.layer = (_cam.cullingMask & 1) != 0 ? 0 : _cam.gameObject.layer;
        }

        void Apply(bool xr)
        {
            if (_mr == null)
                return;
            float t = Time.unscaledTime - _blinkStart;
            float a = 0f;
            if (xr && Enabled && t >= 0f && t < HoldS + FadeS)
            {
                a = t < HoldS ? 1f : 1f - (t - HoldS) / FadeS;
                a = a * a * (3f - 2f * a); // smoothstep: soft tail
            }
            bool show = a > 0.004f;
            if (_mr.enabled != show)
                _mr.enabled = show;
            if (!show)
                return;
            byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a));
            if (alpha == _builtAlpha)
                return;
            _builtAlpha = alpha;
            for (int i = 0; i < 4; i++)
                _cols[i] = new Color32(0, 0, 0, alpha);
            _mesh.colors32 = _cols;
        }

        static Material MakeMaterial()
        {
            // Same shader choice as ComfortVignette: vertex colour * alpha blend, Cull Off, ZWrite Off, stereo-instancing safe.
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
            m.name = "TeleportBlink";
            m.renderQueue = 4001; // over the world, world-space UI and the comfort vignette
            return m;
        }
    }
}