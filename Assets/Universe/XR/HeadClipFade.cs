using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.XR
{
    /// <summary>
    /// Comfort: no seeing through walls. In the headset you can physically lean or walk your head into a lab
    /// bench, a wall, a pillar or a pyramid passage ceiling (the rig's capsule only stops stick walking), and the
    /// inside of the mesh / the room behind it pops into view, which is disorienting. When the headset's eye point
    /// is inside solid scenery (a 9 cm sphere at the XR camera overlaps a non-trigger collider) the view fades to
    /// black in about 0.12 s, and fades back in over 0.2 s as soon as you pull your head out.
    /// Ignored, so they never black you out: the player's own rig (body capsule, hands), anything with an XRI
    /// interactable (grabbables, held gadgets, buttons), dynamic rigidbodies (props, weapons, ragdoll enemies),
    /// character controllers, world-space UI (Canvas or UI layer) and triggers.
    /// Safety: if the head stays inside something for 3 s (for example a low passage or a spawn point that
    /// overlaps a collider) the black eases to a dim 55% so you can still see your way out; the collider's name is
    /// logged once. Unscaled time, so it works in Focus slow-mo and while paused. XR only: desktop and the XR
    /// Device Simulator without a running XR display are untouched. Runtime only, nothing saved, no Settings chip.
    /// Rendering: one camera-parented quad (UI/Default, vertex alpha), renderer disabled when clear.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10001)] // after rig / travel movement and after TeleportBlink
    public sealed class HeadClipFade : MonoBehaviour
    {
        const float HeadRadius = 0.09f;   // eye point + a little skin, so the near plane never shows the inside
        const float FadeInS = 0.12f;      // to black
        const float FadeOutS = 0.2f;      // back to clear
        const float StuckS = 3f;          // inside this long: ease to the dim fallback
        const float StuckAlpha = 0.55f;
        const float CachePurgeS = 10f;

        static HeadClipFade _instance;

        readonly Collider[] _hits = new Collider[16];
        readonly Dictionary<Collider, bool> _static = new Dictionary<Collider, bool>();
        Camera _cam;
        Transform _rig;
        GameObject _go;
        MeshRenderer _mr;
        Mesh _mesh;
        Color32[] _cols;
        float _alpha;
        byte _builtAlpha = 255;
        float _nextResolve;
        float _nextPurge;
        float _insideSince = -1f;
        int _uiLayer = -2;
        int _fades;
        bool _stuckLogged;
        string _lastName = "";

        /// <summary>True while the head is inside scenery (view faded or fading).</summary>
        public static bool HeadInside => _instance != null && _instance._insideSince >= 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsHeadClipFade");
            _instance = host.AddComponent<HeadClipFade>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
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
            float now = Time.unscaledTime;
            bool xr = DesktopPlayerController.IsXrDisplayRunning();
            if (xr && (_cam == null || _rig == null) && now >= _nextResolve)
            {
                _nextResolve = now + 0.5f;
                Resolve();
            }
            if (now >= _nextPurge)
            {
                _nextPurge = now + CachePurgeS;
                _static.Clear(); // drops destroyed colliders and picks up re-parented / re-layered ones
            }

            bool inside = xr && _cam != null && HeadInsideSolid();
            float target = 0f;
            if (inside)
            {
                if (_insideSince < 0f)
                {
                    _insideSince = now;
                    _fades++;
                    if (_fades == 1)
                        Debug.Log("HeadClipFade: head inside '" + _lastName + "' - fading the view until you lean out.");
                }
                if (now - _insideSince >= StuckS)
                {
                    target = StuckAlpha;
                    if (!_stuckLogged)
                    {
                        _stuckLogged = true;
                        Debug.Log("HeadClipFade: head has been inside '" + _lastName + "' for " + StuckS +
                                  " s - easing to a dim view so you can see your way out.");
                    }
                }
                else
                {
                    target = 1f;
                }
            }
            else
            {
                _insideSince = -1f;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _alpha = Mathf.MoveTowards(_alpha, target, dt / (target > _alpha ? FadeInS : FadeOutS));
            Apply(xr);
        }

        bool HeadInsideSolid()
        {
            Vector3 p = _cam.transform.position;
            int n = UnityEngine.Physics.OverlapSphereNonAlloc(p, HeadRadius, _hits, UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i];
                _hits[i] = null;
                if (found || c == null || !c.enabled)
                    continue;
                if (IsSolidScenery(c))
                {
                    _lastName = c.name;
                    found = true;
                }
            }
            return found;
        }

        bool IsSolidScenery(Collider c)
        {
            // Live checks (these change during play: grabs re-parent, held bodies toggle kinematic).
            if (_rig != null && c.transform.IsChildOf(_rig))
                return false;
            Rigidbody rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
                return false;
            if (_static.TryGetValue(c, out bool solid))
                return solid;
            solid = Classify(c);
            _static[c] = solid;
            return solid;
        }

        bool Classify(Collider c)
        {
            if (c is CharacterController)
                return false;
            if (_uiLayer == -2)
                _uiLayer = LayerMask.NameToLayer("UI");
            if (_uiLayer >= 0 && c.gameObject.layer == _uiLayer)
                return false;
            if (c.GetComponentInParent<XRBaseInteractable>(true) != null)
                return false;
            if (c.GetComponentInParent<Canvas>(true) != null)
                return false;
            return true;
        }

        void Resolve()
        {
            Camera cam = QhysicsUiBuilder.ResolveXrCamera();
            if (cam == null)
                return;
            if (cam != _cam)
            {
                _cam = cam;
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
                _static.Clear();
            }
        }

        void EnsureRenderer()
        {
            if (_cam == null)
                return;
            if (_go == null)
            {
                _go = new GameObject("HeadClipFadeQuad");
                _mesh = new Mesh { name = "HeadClipFade" };
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
            }
            if (_go.transform.parent != _cam.transform)
                _go.transform.SetParent(_cam.transform, false);
            // Just past the near plane; half-size 6x the distance covers past the Quest 3S per-eye FOV.
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
            float a = xr ? _alpha : 0f;
            a = a * a * (3f - 2f * a); // smoothstep: soft edges on both fades
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
            // Same shader choice as TeleportBlink / ComfortVignette: vertex colour * alpha blend, Cull Off,
            // ZWrite Off, stereo-instancing safe. ZTest Always so a wall face nearer than the quad can't poke through.
            Shader sh = Shader.Find("UI/Default");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");
            Material m;
            if (sh != null)
            {
                m = new Material(sh);
                m.color = Color.white;
                m.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
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
            m.name = "HeadClipFade";
            m.renderQueue = 4000; // over the world, world-space UI and the comfort vignette; TeleportBlink (4001) on top
            return m;
        }
    }
}
