using System;
using System.Collections.Generic;
using UnityEngine;

using RealityEngine.Biology;
using RealityEngine.Chemistry;
using RealityEngine.Physics.Thermo;
using RealityEngine.Survey;
using RealityEngine.Experiments;
using RealityEngine.XR;
using RealityEngine.UI;

namespace RealityEngine.Stations
{
    /// <summary>
    /// Self-spawning singleton that unifies the isolated science boards into a
    /// linked-stations hub. After scene load, discovers all board MonoBehaviours,
    /// wraps each in a <see cref="LabStation"/>, spawns a <see cref="StationSignpost"/>
    /// per station, and exposes nearest-station lookup + comfort teleport.
    /// Pattern mirrored from <c>ChallengeManager.AutoSpawn</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(70)]
    public sealed class LabStationHub : MonoBehaviour
    {
        static LabStationHub _instance;

        public static LabStationHub Instance => _instance;

        readonly List<LabStation> _stations = new List<LabStation>();
        readonly List<StationSignpost> _signposts = new List<StationSignpost>();
        LabStation _activeStation;
        int _cycleIndex = -1;

        /// <summary>All discovered lab stations (read-only).</summary>
        public IReadOnlyList<LabStation> Stations => _stations;

        /// <summary>Last station teleported/cycled to, if any.</summary>
        public LabStation ActiveStation => _activeStation;

        // ── Self-spawning (mirrors ChallengeManager) ──────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (!Application.isPlaying)
                return;
            if (_instance != null)
                return;
            // Avoid duplicate if one already exists in-scene.
            LabStationHub existing = UnityEngine.Object.FindAnyObjectByType<LabStationHub>(FindObjectsInactive.Include);
            if (existing != null)
                return;
            var go = new GameObject("LabStationHub");
            go.AddComponent<LabStationHub>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // Start runs after all scene Awake calls — boards are guaranteed to exist.
        void Start()
        {
            DiscoverStations();
            SpawnSignposts();
        }

        /// <summary>
        /// Re-discover boards and spawn any missing signposts (idempotent).
        /// Call after late EnsureBiology / BuildLab if Start ran with zero stations.
        /// </summary>
        public void RefreshStations()
        {
            DiscoverStations();
            for (int i = _signposts.Count - 1; i >= 0; i--)
            {
                if (_signposts[i] == null)
                    _signposts.RemoveAt(i);
            }
            for (int i = 0; i < _stations.Count; i++)
            {
                LabStation station = _stations[i];
                if (station == null)
                    continue;
                bool has = false;
                for (int j = 0; j < _signposts.Count; j++)
                {
                    StationSignpost sp = _signposts[j];
                    if (sp != null && sp.Station == station)
                    {
                        has = true;
                        break;
                    }
                }
                if (has)
                    continue;
                StationSignpost created = StationSignpost.Create(station, transform);
                if (created != null)
                    _signposts.Add(created);
            }
        }

        // ── Discovery ─────────────────────────────────────────────

        void DiscoverStations()
        {
            _stations.Clear();
            WrapAll<BiologyBoard>();
            WrapAll<ChemistryBoard>();
            WrapAll<ConservationBoard>();
            WrapAll<SurveyBoard>();
            WrapAll<ExperimentBoard>();
        }

        // Generic helper — FindObjectsByType<T> with FindObjectsInactive.Exclude
        // (qualified UnityEngine.Object to avoid ambiguity with System.Object).
        // Uses the non-deprecated single-parameter overload.
        static T[] FindActive<T>() where T : Component
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
        }

        void WrapAll<T>() where T : Component
        {
            T[] boards = FindActive<T>();
            if (boards == null)
                return;
            for (int i = 0; i < boards.Length; i++)
            {
                if (boards[i] == null)
                    continue;
                LabStation station = LabStation.Wrap(boards[i]);
                if (station != null)
                    _stations.Add(station);
            }
        }

        // ── Signposts ─────────────────────────────────────────────

        void SpawnSignposts()
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                LabStation station = _stations[i];
                if (station == null)
                    continue;
                StationSignpost signpost = StationSignpost.Create(station, transform);
                if (signpost != null)
                    _signposts.Add(signpost);
            }
        }

        // ── Nearest-station lookup ────────────────────────────────

        /// <summary>
        /// Returns the station whose world anchor is closest to <paramref name="worldPos"/>,
        /// or null if no stations are registered. Uses sqrMagnitude for efficiency.
        /// </summary>
        public LabStation NearestStation(Vector3 worldPos)
        {
            LabStation nearest = null;
            float minSqrDist = float.MaxValue;
            for (int i = 0; i < _stations.Count; i++)
            {
                LabStation s = _stations[i];
                if (s == null)
                    continue;
                Transform anchor = s.WorldAnchor;
                if (anchor == null)
                    continue;
                float sqrDist = (anchor.position - worldPos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = s;
                }
            }
            return nearest;
        }

        /// <summary>Nearest station to the active XR / desktop camera, or null.</summary>
        public LabStation NearestFromCamera()
        {
            Camera cam = QhysicsUiBuilder.ResolveXrCamera();
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return null;
            return NearestStation(cam.transform.position);
        }

        /// <summary>Find a station by stable id (e.g. "biology"), case-insensitive.</summary>
        public LabStation FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < _stations.Count; i++)
            {
                LabStation s = _stations[i];
                if (s == null || string.IsNullOrEmpty(s.Id))
                    continue;
                if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            return null;
        }

        // ── Comfort teleport ──────────────────────────────────────

        /// <summary>
        /// Comfort-snap the XR Origin so the player's head lands ~1.2 m in front of
        /// the station WorldAnchor, facing it. Mirrors CombatWorld.TogglePlayerTeleport:
        /// disable CharacterController while moving, correct yaw using camera yaw offset,
        /// subtract head-offset XY, re-enable CC. Never moves the camera alone / never parents.
        /// </summary>
        public bool TeleportToStation(LabStation station)
        {
            if (station == null)
                return false;
            Transform anchor = station.WorldAnchor;
            if (anchor == null)
                return false;

            GameObject originGo = GameObject.Find(LabPlayerSpawn.OriginName);
            if (originGo == null)
                return false;

            CharacterController cc = originGo.GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (cc != null)
                cc.enabled = false;

            Transform o = originGo.transform;
            Camera cam = QhysicsUiBuilder.ResolveXrCamera();
            if (cam == null)
                cam = Camera.main;

            Vector3 fwd = anchor.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = Vector3.forward;
            else
                fwd.Normalize();

            // Stand in front of the board (along its forward), facing the anchor.
            Vector3 entry = anchor.position + fwd * 1.2f;
            Vector3 look = anchor.position - entry;
            look.y = 0f;
            if (look.sqrMagnitude < 1e-6f)
                look = -fwd;

            float camYaw = cam != null ? cam.transform.eulerAngles.y - o.eulerAngles.y : 0f;
            o.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(look).eulerAngles.y - camYaw, 0f);
            Vector3 headOffset = cam != null ? cam.transform.position - o.position : Vector3.zero;
            headOffset.y = 0f;
            o.position = entry - headOffset + Vector3.up * 0.05f;

            if (cc != null)
                cc.enabled = had;

            _activeStation = station;
            for (int i = 0; i < _stations.Count; i++)
            {
                if (_stations[i] == station)
                {
                    _cycleIndex = i;
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// Cycle stations by list order (wrap), comfort-teleport, return the station (or null).
        /// </summary>
        public LabStation CycleNextStation()
        {
            if (_stations.Count == 0)
                return null;
            _cycleIndex = (_cycleIndex + 1) % _stations.Count;
            LabStation station = _stations[_cycleIndex];
            if (station == null)
                return null;
            TeleportToStation(station);
            return station;
        }
    }
}