using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using RealityEngine.Player;

namespace RealityEngine.XR
{
    /// <summary>
    /// Controller haptics for world-space UI in the headset. When an XR ray slides onto a new button,
    /// toggle, slider or chip you feel a faint tick in that hand; pulling the trigger on it gives a short,
    /// firmer click (a softer bump if the control is greyed out). Makes menu pointing readable by feel on
    /// Quest, where small panels are hard to aim at. Reads XRI 3.6 ray UI raycasts only (no UI changes,
    /// no input consumed). XR only: desktop and the Device Simulator without a running XR display are untouched.
    /// </summary>
    public sealed class XrUiHaptics : MonoBehaviour
    {
        const float HoverAmp = 0.12f, HoverSec = 0.018f;
        const float ClickAmp = 0.38f, ClickSec = 0.035f;
        const float DeniedAmp = 0.07f, DeniedSec = 0.02f;
        const float MinHoverGap = 0.06f; // dense lists: do not buzz on every row when sweeping fast

        sealed class RayState
        {
            public XRRayInteractor ray;
            public XRNode node;
            public Selectable hovered;
            public bool triggerWas;
            public float lastHoverTick;
        }

        static XrUiHaptics _instance;
        readonly List<RayState> _rays = new List<RayState>();
        float _nextResolve;
        bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsXrUiHaptics");
            _instance = host.AddComponent<XrUiHaptics>();
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
        }

        void Update()
        {
            if (!DesktopPlayerController.IsXrDisplayRunning())
                return;

            float now = Time.unscaledTime;
            if (now >= _nextResolve)
            {
                _nextResolve = now + 1f;
                Resolve();
            }

            for (int i = 0; i < _rays.Count; i++)
            {
                RayState s = _rays[i];
                if (s.ray == null || !s.ray.isActiveAndEnabled)
                {
                    s.hovered = null;
                    continue;
                }

                Selectable sel = null;
                if (s.ray.TryGetCurrentUIRaycastResult(out RaycastResult hit) && hit.gameObject != null)
                    sel = hit.gameObject.GetComponentInParent<Selectable>();

                if (sel != s.hovered)
                {
                    s.hovered = sel;
                    if (sel != null && sel.IsInteractable() && now - s.lastHoverTick >= MinHoverGap)
                    {
                        s.lastHoverTick = now;
                        Pulse(s.node, HoverAmp, HoverSec);
                    }
                }

                bool trig = TriggerDown(s.node);
                if (trig && !s.triggerWas && sel != null)
                {
                    if (sel.IsInteractable())
                        Pulse(s.node, ClickAmp, ClickSec);
                    else
                        Pulse(s.node, DeniedAmp, DeniedSec);
                }
                s.triggerWas = trig;
            }
        }

        void Resolve()
        {
            XRRayInteractor[] found = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = _rays.Count - 1; i >= 0; i--)
                if (_rays[i].ray == null)
                    _rays.RemoveAt(i);
            for (int i = 0; i < found.Length; i++)
            {
                XRRayInteractor r = found[i];
                bool known = false;
                for (int k = 0; k < _rays.Count; k++)
                    if (_rays[k].ray == r) { known = true; break; }
                if (known)
                    continue;
                _rays.Add(new RayState { ray = r, node = NodeFor(r), lastHoverTick = -1f });
            }
            if (!_logged && _rays.Count > 0)
            {
                _logged = true;
                Debug.Log("XrUiHaptics: ready (" + _rays.Count + " XR rays; hover tick + trigger click on UI)");
            }
        }

        static XRNode NodeFor(XRRayInteractor r)
        {
            if (r.handedness == InteractorHandedness.Left)
                return XRNode.LeftHand;
            if (r.handedness == InteractorHandedness.Right)
                return XRNode.RightHand;
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                if (t.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return XRNode.LeftHand;
                if (t.name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return XRNode.RightHand;
            }
            return XRNode.RightHand;
        }

        static bool TriggerDown(XRNode node)
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            return d.isValid && d.TryGetFeatureValue(CommonUsages.triggerButton, out bool b) && b;
        }

        static void Pulse(XRNode node, float amplitude, float seconds)
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid)
                return;
            if (d.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                d.SendHapticImpulse(0u, amplitude, seconds);
        }
    }
}