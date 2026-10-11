using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using RealityEngine.Player;

namespace RealityEngine.XR
{
    /// <summary>
    /// Grab feel for every XRI grabbable in the headset (lab gadgets, magnets, coils, rods, toys, props).
    /// Grabbing something gives a short firm bump in that hand, letting go a soft tap, and while you hold
    /// it, knocking it into a table, wall or another object buzzes that hand in proportion to how hard it
    /// hit (bigger for faster knocks, scaled down for light objects). Two-handed holds buzz both hands.
    /// Bumps against your own rig (hands, body) and socket snaps are ignored so holding is not noisy.
    /// Combat PhysicsWeapon / PhysicsHands keep their own haptics (they do not use XRI grab). Runtime only:
    /// finds the rig's interactors every second, adds a tiny collision relay to held rigidbodies, changes
    /// no scene or prefab data. XR only: desktop and the Device Simulator without a running XR display are untouched.
    /// </summary>
    public sealed class XrGrabHaptics : MonoBehaviour
    {
        const float GrabAmp = 0.32f, GrabSec = 0.04f;
        const float ReleaseAmp = 0.12f, ReleaseSec = 0.02f;
        internal const float ImpactMinSpeed = 0.35f;   // m/s relative speed below which a touch is silent
        internal const float ImpactFullSpeed = 3.0f;   // m/s at which a knock reaches full strength
        internal const float ImpactMaxAmp = 0.7f;
        internal const float ImpactGap = 0.07f;        // s between impact buzzes per object (no rattling)

        static XrGrabHaptics _instance;
        readonly HashSet<XRBaseInteractor> _hooked = new HashSet<XRBaseInteractor>();
        float _nextResolve;
        bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsXrGrabHaptics");
            _instance = host.AddComponent<XrGrabHaptics>();
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
            foreach (XRBaseInteractor i in _hooked)
            {
                if (i == null)
                    continue;
                i.selectEntered.RemoveListener(OnSelectEntered);
                i.selectExited.RemoveListener(OnSelectExited);
            }
            _hooked.Clear();
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now < _nextResolve)
                return;
            _nextResolve = now + 1f;
            Resolve();
        }

        void Resolve()
        {
            _hooked.RemoveWhere(i => i == null);
            XRBaseInteractor[] found = FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int added = 0;
            for (int k = 0; k < found.Length; k++)
            {
                XRBaseInteractor i = found[k];
                if (i == null || i is XRSocketInteractor || _hooked.Contains(i))
                    continue;
                if (NodeFor(i) == null)
                    continue; // not a hand (e.g. scripted interactors)
                i.selectEntered.AddListener(OnSelectEntered);
                i.selectExited.AddListener(OnSelectExited);
                _hooked.Add(i);
                added++;
            }
            if (!_logged && _hooked.Count > 0)
            {
                _logged = true;
                Debug.Log("XrGrabHaptics: ready (" + _hooked.Count + " hand interactors; grab bump, release tap, held-object impact buzz)");
            }
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            XRNode? node = NodeFor(args.interactorObject as XRBaseInteractor);
            if (node == null || args.interactableObject == null)
                return;
            Pulse(node.Value, GrabAmp, GrabSec);
            Rigidbody rb = BodyOf(args.interactableObject);
            if (rb == null)
                return;
            XrHeldImpactRelay relay = rb.GetComponent<XrHeldImpactRelay>();
            if (relay == null)
                relay = rb.gameObject.AddComponent<XrHeldImpactRelay>();
            relay.AddHolder(node.Value);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            XRNode? node = NodeFor(args.interactorObject as XRBaseInteractor);
            if (node == null || args.interactableObject == null)
                return;
            if (!args.isCanceled)
                Pulse(node.Value, ReleaseAmp, ReleaseSec);
            Rigidbody rb = BodyOf(args.interactableObject);
            if (rb == null)
                return;
            XrHeldImpactRelay relay = rb.GetComponent<XrHeldImpactRelay>();
            if (relay != null)
                relay.RemoveHolder(node.Value);
        }

        static Rigidbody BodyOf(IXRSelectInteractable interactable)
        {
            Transform t = interactable.transform;
            if (t == null)
                return null;
            Rigidbody rb = t.GetComponent<Rigidbody>();
            return rb != null ? rb : t.GetComponentInParent<Rigidbody>();
        }

        internal static XRNode? NodeFor(XRBaseInteractor i)
        {
            if (i == null)
                return null;
            if (i.handedness == InteractorHandedness.Left)
                return XRNode.LeftHand;
            if (i.handedness == InteractorHandedness.Right)
                return XRNode.RightHand;
            for (Transform t = i.transform; t != null; t = t.parent)
            {
                if (t.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return XRNode.LeftHand;
                if (t.name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return XRNode.RightHand;
            }
            return null;
        }

        internal static void Pulse(XRNode node, float amplitude, float seconds)
        {
            if (!DesktopPlayerController.IsXrDisplayRunning())
                return;
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid)
                return;
            if (d.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Clamp(seconds, 0.01f, 1f));
        }
    }

    /// <summary>
    /// Added at runtime by XrGrabHaptics to a held rigidbody: turns collisions into a buzz in the holding
    /// hand(s) while held. Silent when nobody holds it. Never removed so repeated grabs reuse it.
    /// </summary>
    public sealed class XrHeldImpactRelay : MonoBehaviour
    {
        readonly List<XRNode> _holders = new List<XRNode>(2);
        Rigidbody _rb;
        float _lastBuzz = -1f;

        public bool Held => _holders.Count > 0;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void AddHolder(XRNode node)
        {
            _holders.Add(node);
        }

        public void RemoveHolder(XRNode node)
        {
            _holders.Remove(node);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_holders.Count == 0 || c == null || c.collider == null)
                return;
            if (c.collider.GetComponentInParent<XROrigin>() != null)
                return; // own hands / body
            float now = Time.unscaledTime;
            if (now - _lastBuzz < XrGrabHaptics.ImpactGap)
                return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < XrGrabHaptics.ImpactMinSpeed)
                return;
            float t = Mathf.InverseLerp(XrGrabHaptics.ImpactMinSpeed, XrGrabHaptics.ImpactFullSpeed, speed);
            float mass = _rb != null ? _rb.mass : 1f;
            float massScale = Mathf.Clamp(0.45f + 0.55f * Mathf.Sqrt(Mathf.Max(0.01f, mass)), 0.45f, 1f);
            float amp = Mathf.Lerp(0.08f, XrGrabHaptics.ImpactMaxAmp, t) * massScale;
            float sec = Mathf.Lerp(0.02f, 0.07f, t);
            _lastBuzz = now;
            bool left = false, right = false;
            for (int i = 0; i < _holders.Count; i++)
            {
                if (_holders[i] == XRNode.LeftHand) left = true;
                else if (_holders[i] == XRNode.RightHand) right = true;
            }
            if (left) XrGrabHaptics.Pulse(XRNode.LeftHand, amp, sec);
            if (right) XrGrabHaptics.Pulse(XRNode.RightHand, amp, sec);
        }
    }
}