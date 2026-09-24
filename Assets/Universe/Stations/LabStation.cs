using UnityEngine;

using RealityEngine.Biology;
using RealityEngine.Chemistry;
using RealityEngine.Physics.Thermo;
using RealityEngine.Survey;
using RealityEngine.Experiments;

namespace RealityEngine.Stations
{
    /// <summary>
    /// Runtime descriptor that wraps a discovered science board MonoBehaviour into a
    /// unified lab-station entry. Attached to the board's own GameObject at runtime by
    /// <see cref="LabStationHub"/> — no scene edits required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabStation : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Short stable id, e.g. \"biology\".")]
        [SerializeField]
        string _id;

        [Tooltip("Human-readable name shown on signposts and in context, e.g. \"Biology Bench\".")]
        [SerializeField]
        string _displayName;

        [Header("References")]
        [Tooltip("The board MonoBehaviour this station wraps.")]
        [SerializeField]
        Component _board;

        [Tooltip("World-space anchor for distance checks and signpost placement. Defaults to the board's transform.")]
        [SerializeField]
        Transform _worldAnchor;

        /// <summary>Short stable id, e.g. "biology".</summary>
        public string Id => _id;

        /// <summary>Human-readable name, e.g. "Biology Bench".</summary>
        public string DisplayName => _displayName;

        /// <summary>The board MonoBehaviour this station wraps.</summary>
        public Component Board => _board;

        /// <summary>The GameObject of the wrapped board.</summary>
        public GameObject BoardObject => _board != null ? _board.gameObject : gameObject;

        /// <summary>
        /// World-space anchor for distance checks and signpost placement.
        /// Falls back to the board's transform, then this component's transform.
        /// </summary>
        public Transform WorldAnchor
        {
            get
            {
                if (_worldAnchor != null)
                    return _worldAnchor;
                if (_board != null)
                    return _board.transform;
                return transform;
            }
        }

        /// <summary>
        /// Short human-readable summary: station display name + board type name.
        /// e.g. "Chemistry Bench (ChemistryBoard)".
        /// </summary>
        public string GetContextSummary()
        {
            string boardType = _board != null ? _board.GetType().Name : "Unknown";
            return $"{_displayName} ({boardType})";
        }

        // ── Factory ───────────────────────────────────────────────

        /// <summary>
        /// Wrap a discovered board Component into a LabStation by attaching (or reusing)
        /// a LabStation component on the board's GameObject at runtime. Infers a sensible
        /// id and displayName from the board type.
        /// </summary>
        public static LabStation Wrap(Component board)
        {
            if (board == null)
                return null;

            GameObject go = board.gameObject;
            LabStation station = go.GetComponent<LabStation>();
            if (station == null)
                station = go.AddComponent<LabStation>();

            station._board = board;
            station._worldAnchor = board.transform;
            (station._id, station._displayName) = InferMetadata(board);
            return station;
        }

        /// <summary>Overload allowing explicit id / displayName override.</summary>
        public static LabStation Wrap(Component board, string id, string displayName)
        {
            if (board == null)
                return null;

            GameObject go = board.gameObject;
            LabStation station = go.GetComponent<LabStation>();
            if (station == null)
                station = go.AddComponent<LabStation>();

            station._board = board;
            station._worldAnchor = board.transform;
            station._id = id;
            station._displayName = displayName;
            return station;
        }

        static (string id, string displayName) InferMetadata(Component board)
        {
            switch (board)
            {
                case BiologyBoard _:
                    return ("biology", "Biology Bench");
                case ChemistryBoard _:
                    return ("chemistry", "Chemistry Bench");
                case ConservationBoard _:
                    return ("conservation", "Conservation Bench");
                case SurveyBoard _:
                    return ("survey", "Survey Bench");
                case ExperimentBoard _:
                    return ("experiment", "Experiment Bench");
                default:
                    string typeName = board.GetType().Name;
                    return (typeName.ToLowerInvariant(), typeName);
            }
        }
    }
}
