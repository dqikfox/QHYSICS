using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RealityEngine.XR
{
    /// <summary>
    /// Lost item return: a grabbable that falls out of the world comes back. In the headset it is easy to drop or
    /// throw a gadget, magnet or sword so it slips through a floor seam, tunnels through a thin bench at speed or
    /// sails off the edge of the map, and then it is gone for the rest of the session. This watches every live
    /// XRGrabInteractable with a non-kinematic Rigidbody and remembers its last resting spot (still for half a
    /// second, not held). If the object keeps falling for 3 s with nothing under it (a 2 km ray down finds no
    /// collider other than itself), or drops below y = -500, it is put back at that resting spot with its velocity
    /// cleared; if it falls out again within 6 s of being put back, it goes to where it was first seen instead.
    /// Objects that land on real ground (thrown off a pyramid, dropped down a stairwell) are left where they land.
    /// Held, socketed and kinematic objects are never touched. Fall time counts in game time, so slow-mo and pause
    /// do not trigger it early. Runtime only: grabbables are found every 2 s, checked 4 times a second; nothing in
    /// the scene, prefabs or settings is saved.
    /// </summary>
    public sealed class LostItemReturn : MonoBehaviour
    {
        const float SweepEvery = 2f;
        const float CheckEvery = 0.25f;
        const float RestSpeed = 0.15f;
        const int RestChecksNeeded = 2;
        const float FallSpeed = 2f;
        const float FallSeconds = 3f;
        const float AbsoluteFloor = -500f;
        const float GroundProbe = 2000f;
        const float RepeatWindow = 6f;

        sealed class Track
        {
            public XRGrabInteractable grab;
            public Rigidbody rb;
            public Vector3 firstPos, safePos;
            public Quaternion firstRot, safeRot;
            public int restChecks;
            public float fallTime;
            public float lastReturn = -100f;
        }

        static LostItemReturn _instance;
        readonly Dictionary<XRGrabInteractable, Track> _tracks = new Dictionary<XRGrabInteractable, Track>();
        readonly List<XRGrabInteractable> _dead = new List<XRGrabInteractable>();
        readonly RaycastHit[] _hits = new RaycastHit[16];
        float _nextSweep, _nextCheck, _lastCheckGameTime;
        int _returned;
        bool _logged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _instance != null)
                return;
            var host = new GameObject("QhysicsLostItemReturn");
            _instance = host.AddComponent<LostItemReturn>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            _lastCheckGameTime = Time.time;
        }

        void OnDestroy()
        {
            _tracks.Clear();
            if (_instance == this)
                _instance = null;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _nextSweep)
            {
                _nextSweep = now + SweepEvery;
                Sweep();
            }
            if (now < _nextCheck)
                return;
            _nextCheck = now + CheckEvery;
            float dt = Mathf.Max(0f, Time.time - _lastCheckGameTime);
            _lastCheckGameTime = Time.time;
            Check(dt);
        }

        void Sweep()
        {
            XRGrabInteractable[] grabs = FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int k = 0; k < grabs.Length; k++)
            {
                XRGrabInteractable g = grabs[k];
                if (g == null || _tracks.ContainsKey(g))
                    continue;
                Rigidbody rb = g.GetComponent<Rigidbody>();
                if (rb == null)
                    continue;
                Transform tr = rb.transform;
                _tracks.Add(g, new Track
                {
                    grab = g,
                    rb = rb,
                    firstPos = tr.position,
                    firstRot = tr.rotation,
                    safePos = tr.position,
                    safeRot = tr.rotation,
                });
            }

            if (!_logged && _tracks.Count > 0)
            {
                _logged = true;
                Debug.Log("LostItemReturn: watching " + _tracks.Count +
                          " grabbables (one that falls out of the world returns to its last resting spot)");
            }
        }

        void Check(float dt)
        {
            _dead.Clear();
            foreach (KeyValuePair<XRGrabInteractable, Track> kv in _tracks)
            {
                Track t = kv.Value;
                if (t.grab == null || t.rb == null)
                {
                    _dead.Add(kv.Key);
                    continue;
                }
                if (!t.grab.isActiveAndEnabled || t.grab.isSelected || t.rb.isKinematic)
                {
                    t.fallTime = 0f;
                    t.restChecks = 0;
                    continue;
                }

                Vector3 p = t.rb.position;
                Vector3 v = t.rb.linearVelocity;
                bool lost = p.y < AbsoluteFloor;

                if (!lost && v.sqrMagnitude < RestSpeed * RestSpeed)
                {
                    t.fallTime = 0f;
                    if (++t.restChecks >= RestChecksNeeded)
                    {
                        t.safePos = p;
                        t.safeRot = t.rb.rotation;
                    }
                    continue;
                }
                t.restChecks = 0;

                if (!lost && v.y < -FallSpeed)
                {
                    t.fallTime += dt;
                    if (t.fallTime >= FallSeconds)
                    {
                        if (GroundBelow(t.rb, p))
                            t.fallTime = FallSeconds - 1f; // something to land on: look again in a second
                        else
                            lost = true;
                    }
                }
                else if (!lost)
                {
                    t.fallTime = 0f;
                }

                if (lost)
                    Return(t);
            }
            for (int k = 0; k < _dead.Count; k++)
                _tracks.Remove(_dead[k]);
        }

        bool GroundBelow(Rigidbody rb, Vector3 p)
        {
            int n = UnityEngine.Physics.RaycastNonAlloc(p, Vector3.down, _hits, GroundProbe, UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Transform self = rb.transform;
            for (int k = 0; k < n; k++)
            {
                Collider c = _hits[k].collider;
                if (c == null || c.attachedRigidbody == rb || c.transform.IsChildOf(self))
                    continue;
                return true;
            }
            return false;
        }

        void Return(Track t)
        {
            bool repeat = Time.unscaledTime - t.lastReturn < RepeatWindow;
            Vector3 pos = repeat ? t.firstPos : t.safePos;
            Quaternion rot = repeat ? t.firstRot : t.safeRot;
            t.lastReturn = Time.unscaledTime;
            t.fallTime = 0f;
            t.restChecks = 0;

            Rigidbody rb = t.rb;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.transform.SetPositionAndRotation(pos, rot);
            rb.position = pos;
            rb.rotation = rot;
            _returned++;
            Debug.Log("LostItemReturn: " + t.grab.name + " fell out of the world - put back at its " +
                      (repeat ? "first position" : "last resting spot") + " " + pos.ToString("F2") + " (" + _returned + " returned this session)");
        }
    }
}