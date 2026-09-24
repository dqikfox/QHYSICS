using UnityEngine;
using UnityEngine.UI;
using TMPro;

using RealityEngine.UI;

namespace RealityEngine.Stations
{
    /// <summary>
    /// Minimal world-space uGUI label chip that floats above a lab station and billboards
    /// toward the camera (yaw-only, matching the board billboard pattern).
    /// Runtime-created by <see cref="LabStationHub"/> — no scene edits.
    /// Uses QhysicsUiBuilder / QhysicsUiStyle for consistent dark-glass styling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StationSignpost : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Vertical offset above the station world anchor, in metres.")]
        [SerializeField]
        float _heightAboveStation = 0.35f;

        LabStation _station;
        Transform _anchor;
        RectTransform _chipRoot;
        Camera _camera;

        /// <summary>The station this signpost labels.</summary>
        public LabStation Station => _station;

        /// <summary>
        /// Factory: create a signpost for the given station, parented to <paramref name="parent"/>.
        /// Builds a small world-space canvas chip with QhysicsUiBuilder styling.
        /// </summary>
        public static StationSignpost Create(LabStation station, Transform parent)
        {
            if (station == null)
                return null;

            var go = new GameObject($"Signpost_{station.Id}");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var signpost = go.AddComponent<StationSignpost>();
            signpost.Initialize(station);
            return signpost;
        }

        void Initialize(LabStation station)
        {
            _station = station;
            _anchor = station.WorldAnchor;

            // Build a small world-space canvas chip using QhysicsUiBuilder styling.
            // 300 x 64 UI px at CanvasScale 0.001 ≈ 0.30 m x 0.064 m.
            Canvas canvas = QhysicsUiBuilder.CreateWorldCanvas(
                "Chip", transform, new Vector2(300f, 64f));
            _chipRoot = canvas.GetComponent<RectTransform>();

            // Dark-glass panel background + centered TMP label.
            Image face = QhysicsUiBuilder.BorderPanel(
                canvas.transform, "Face", new Vector2(280f, 48f));
            face.raycastTarget = false;
            // Disable raycast on the border image too.
            Image border = face.transform.parent.GetComponent<Image>();
            if (border != null)
                border.raycastTarget = false;

            TextMeshProUGUI label = QhysicsUiBuilder.Label(
                face.transform, "Label", station.DisplayName,
                QhysicsUiStyle.FontChip, QhysicsUiStyle.TextPrimary,
                TextAlignmentOptions.Center);
            RectTransform labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 4f);
            labelRt.offsetMax = new Vector2(-8f, -4f);
        }

        void LateUpdate()
        {
            if (_anchor == null || _chipRoot == null)
                return;

            // Position above the station anchor.
            _chipRoot.position = _anchor.position + Vector3.up * _heightAboveStation;

            // Billboard toward camera — yaw only (flatten y), matching board billboard pattern.
            if (_camera == null)
                _camera = Camera.main;
            if (_camera != null)
            {
                Vector3 toCam = _chipRoot.position - _camera.transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 1e-6f)
                    _chipRoot.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
        }
    }
}
