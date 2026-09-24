using System.Collections.Generic;
using UnityEngine;

using RealityEngine.Biology;
using RealityEngine.Chemistry;
using RealityEngine.Physics.Thermo;
using RealityEngine.Survey;
using RealityEngine.Experiments;

namespace RealityEngine.Stations
{
    /// <summary>
    /// Self-spawning singleton that unifies the isolated science boards into a
    /// linked-stations hub. After scene load, discovers all board MonoBehaviours,
    /// wraps each in a <see cref="LabStation"/>, spawns a <see cref="StationSignpost"/>
    /// per station, and exposes nearest-station lookup.
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

        /// <summary>All discovered lab stations (read-only).</summary>
        public IReadOnlyList<LabStation> Stations => _stations;

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
    }
}
