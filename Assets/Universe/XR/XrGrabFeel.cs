using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RealityEngine.XR
{
    /// <summary>
    /// Grab feel: pick things up where your hand actually is. Most lab grabbables (spawned gadgets, cloned
    /// templates, props) are plain XRGrabInteractables with no grip point, so XRI snapped the object's pivot
    /// (usually its centre) into the palm and rotated it to the controller: a coil or rod jumped into the hand,
    /// a big gadget clipped through the wrist. This turns on XRI dynamic attach for them, so the object stays
    /// where you touched it and keeps the angle it had (position and rotation matched at grab, grip point kept on
    /// the collider surface). Ray grabs that pull to the hand still pull to the hand (XRI keeps that rule), and
    /// sockets keep their fixed pose. Objects with an authored grip (attachTransform set) and ones that already
    /// use dynamic attach are left exactly as they are. Runtime only: grabbables are checked every 2 s and the
    /// moment a hand hovers one (so a just-spawned gadget is ready before the grab). Nothing in the scene,
    /// prefabs or settings is saved; it only changes live instances during Play.
    /// </summary>
    public sealed class XrGrabFeel : MonoBehaviour
    {
        const float SweepEvery = 2f;

        static XrGrabFeel _instance;
        readonly HashSet<XRGrabInteractable> _seen = new HashSet<XRGrabInteractable>();
        readonly HashSet<XRBaseInteractor> _hooked = new HashSet<XRBaseInteractor>();
        float _nextSweep;
        int _converted, _authoredGrip, _alreadyDynamic;
        bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsXrGrabFeel");
            _instance = host.AddComponent<XrGrabFeel>();
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
                if (i != null)
                    i.hoverEntered.RemoveListener(OnHoverEntered);
            }
            _hooked.Clear();
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now < _nextSweep)
                return;
            _nextSweep = now + SweepEvery;
            Sweep();
        }

        void Sweep()
        {
            _seen.RemoveWhere(g => g == null);
            _hooked.RemoveWhere(i => i == null);

            XRGrabInteractable[] grabs = FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int k = 0; k < grabs.Length; k++)
                Apply(grabs[k]);

            XRBaseInteractor[] interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int k = 0; k < interactors.Length; k++)
            {
                XRBaseInteractor i = interactors[k];
                if (i == null || i is XRSocketInteractor || _hooked.Contains(i))
                    continue;
                i.hoverEntered.AddListener(OnHoverEntered);
                _hooked.Add(i);
            }

            if (!_logged && _seen.Count > 0)
            {
                _logged = true;
                Debug.Log("XrGrabFeel: grab where you hold - dynamic attach on " + _converted + " grabbables (" +
                          _alreadyDynamic + " already dynamic, " + _authoredGrip + " with an authored grip left as-is; new ones on hover)");
            }
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (args != null && args.interactableObject is XRGrabInteractable g)
                Apply(g);
        }

        void Apply(XRGrabInteractable g)
        {
            if (g == null || _seen.Contains(g))
                return;
            if (g.isSelected)
                return; // held right now: leave this grab alone, convert on a later pass
            _seen.Add(g);
            if (g.attachTransform != null)
            {
                _authoredGrip++; // designed grip point (handle, tool): keep the authored pose
                return;
            }
            if (g.useDynamicAttach)
            {
                _alreadyDynamic++; // set up in code with its own match options: untouched
                return;
            }
            g.useDynamicAttach = true;
            g.matchAttachPosition = true;
            g.matchAttachRotation = true;
            g.snapToColliderVolume = true;
            _converted++;
        }
    }
}
