using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using RealityEngine.Player;
using RealityEngine.UI;

namespace RealityEngine.Combat
{
    public enum DamageType { Blunt = 0, Slash = 1, Pierce = 2, Fire = 3, Lightning = 4, Force = 5 }

    public enum CombatTeam { Player = 0, Enemy = 1, Neutral = 2 }

    public enum Element { None = 0, Fire = 1, Lightning = 2 }

    /// <summary>One hit. Amount is final damage (already scaled by velocity/mass/skills).</summary>
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public Vector3 point;
        public Vector3 normal;
        public Vector3 impulse;     // world impulse to apply to the victim (knockback)
        public CombatTeam sourceTeam;
        public GameObject source;
        public float relativeSpeed; // m/s at impact (0 for spells)
    }

    /// <summary>Anything that can take a <see cref="DamageInfo"/> hit.</summary>
    public interface ICombatTarget
    {
        CombatTeam Team { get; }
        bool IsAlive { get; }
        void TakeHit(DamageInfo hit);
    }

    /// <summary>Global combat event bus (XP, SFX, UI).</summary>
    public static class CombatEvents
    {
        public static event Action<ICombatTarget, DamageInfo> Hit;
        public static event Action<ICombatTarget, DamageInfo> Killed;
        public static event Action<Vector3> Parried;

        public static void RaiseHit(ICombatTarget t, DamageInfo d) { Hit?.Invoke(t, d); }
        public static void RaiseKilled(ICombatTarget t, DamageInfo d) { Killed?.Invoke(t, d); }
        public static void RaiseParried(Vector3 p) { Parried?.Invoke(p); }
    }

    /// <summary>Shared tuning constants.</summary>
    public static class CombatTuning
    {
        public const float MinSwingSpeed = 2.0f;       // m/s relative speed below which weapon contact does no damage
        public const float DamagePerMs = 6.0f;          // base damage per (m/s above threshold) per sqrt(kg)
        public const float PerTargetCooldown = 0.15f;
        public const float StabMinSpeed = 3.0f;         // along-blade speed to embed
        public const float StabMaxAngle = 30f;          // blade axis vs velocity
        public const float ParryMinSpeed = 1.5f;
        public const int PlayerLayerHint = 31;          // QhysicsDesktopBootstrap.PlayerSelfLayer
    }

    /// <summary>URP Lit runtime materials (cached by colour/metal/emission).</summary>
    public static class CombatMaterials
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static Shader _lit;
        static Shader _unlit;

        static Shader Lit
        {
            get
            {
                if (_lit == null)
                    _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null)
                    _lit = Shader.Find("Standard");
                return _lit;
            }
        }

        public static Material Get(Color color, float metallic = 0f, float smoothness = 0.4f, Color? emission = null)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + "_" + metallic.ToString("0.00") + "_" + smoothness.ToString("0.00")
                + "_" + (emission.HasValue ? ColorUtility.ToHtmlStringRGB(emission.Value) : "-");
            if (Cache.TryGetValue(key, out Material m) && m != null)
                return m;
            m = new Material(Lit) { name = "Combat_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Transparent URP Lit (alpha blend) for spell VFX / flashes.</summary>
        public static Material Transparent(Color color, Color? emission = null)
        {
            string key = "T_" + ColorUtility.ToHtmlStringRGBA(color) + (emission.HasValue ? ColorUtility.ToHtmlStringRGB(emission.Value) : "");
            if (Cache.TryGetValue(key, out Material m) && m != null)
                return m;
            m = new Material(Lit) { name = "CombatT_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Unlit colour for LineRenderers (lightning).</summary>
        public static Material UnlitLine(Color color)
        {
            string key = "L_" + ColorUtility.ToHtmlStringRGBA(color);
            if (Cache.TryGetValue(key, out Material m) && m != null)
                return m;
            if (_unlit == null)
                _unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (_unlit == null)
                _unlit = Shader.Find("Unlit/Color");
            m = new Material(_unlit) { name = "CombatLine_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
            Cache[key] = m;
            return m;
        }
    }

    /// <summary>Controller haptics (XR InputDevices; no-op on desktop).</summary>
    public static class CombatHaptics
    {
        public static void Pulse(XRNode node, float amplitude, float seconds)
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid)
                return;
            if (d.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Clamp(seconds, 0.01f, 1f));
        }

        public static void Both(float amplitude, float seconds)
        {
            Pulse(XRNode.LeftHand, amplitude, seconds);
            Pulse(XRNode.RightHand, amplitude, seconds);
        }
    }

    /// <summary>Input gating: no combat input while menus are open.</summary>
    public static class CombatInputGate
    {
        public static bool Blocked
        {
            get
            {
                if (!Application.isPlaying)
                    return true;
                if (QhysicsUiState.BootMenuOpen || QhysicsPausePanel.AnyOpen || QhysicsControlsOverlay.IsOpen)
                    return true;
                if (RealityEngine.Challenges.ChallengeUi.IsListOpen)
                    return true;
                if (SkillsPanel.IsOpen)
                    return true;
                return false;
            }
        }

        public static bool IsXr => DesktopPlayerController.IsXrDisplayRunning();

        public static Camera ViewCamera()
        {
            var desktop = DesktopPlayerController.Instance;
            if (desktop != null && desktop.IsDesktopActive && desktop.MainCamera != null)
            {
                var c = desktop.MainCamera.GetComponent<Camera>();
                if (c != null && c.isActiveAndEnabled)
                    return c;
            }
            return QhysicsUiBuilder.ResolveXrCamera();
        }

        /// <summary>True if the collider belongs to the player rig (desktop body or XR origin).</summary>
        public static bool IsPlayerCollider(Collider c)
        {
            if (c == null)
                return false;
            if (c.gameObject.layer == CombatTuning.PlayerLayerHint)
                return true;
            if (c.GetComponentInParent<CharacterController>() != null)
                return true;
            Transform t = c.transform;
            while (t != null)
            {
                string n = t.name;
                if (n == "XR Origin" || n == "DesktopBody" || n == "Camera Offset")
                    return true;
                t = t.parent;
            }
            return false;
        }
    }

    /// <summary>Small helpers.</summary>
    public static class CombatUtil
    {
        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat,
            bool keepCollider = true)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = localScale;
            var r = g.GetComponent<Renderer>();
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (!keepCollider)
                UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>Primitive cylinders get a CapsuleCollider (a sphere when flat); swap for a mesh collider.</summary>
        public static void FlatCylinderCollider(GameObject g)
        {
            var cap = g.GetComponent<CapsuleCollider>();
            if (cap != null) UnityEngine.Object.DestroyImmediate(cap);
            var mf = g.GetComponent<MeshFilter>();
            var mc = g.AddComponent<MeshCollider>();
            if (mf != null) mc.sharedMesh = mf.sharedMesh;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform)
                SetLayerRecursive(t.gameObject, layer);
        }
    }
}
