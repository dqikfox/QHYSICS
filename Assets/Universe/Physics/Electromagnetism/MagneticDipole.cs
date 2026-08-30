using UnityEngine;

namespace RealityEngine.Physics.Electromagnetism
{
    /// <summary>
    /// Computes the 3D magnetic vector field B(r) produced by a magnetic dipole or cylindrical bar magnet.
    /// Uses standard SI units: B in Tesla (T), dipole moment m in A*m^2.
    /// </summary>
    public class MagneticDipole : MonoBehaviour
    {
        [Header("Magnetic Properties")]
        [Tooltip("Magnetic dipole moment magnitude in A*m^2 (typical strong neodymium magnet ~ 0.5 - 5.0)")]
        public float magneticMoment = 2.0f;

        [Tooltip("Effective length of the physical magnet in meters")]
        public float magnetLength = 0.12f;

        [Tooltip("Effective radius of the physical magnet in meters")]
        public float magnetRadius = 0.02f;

        [Tooltip("Dipole orientation in local space (default along local Z axis)")]
        public Vector3 localAxis = Vector3.forward;

        [Header("Runtime State")]
        public bool isActive = true;

        // Vacuum permeability constant mu_0 / (4 * pi) = 1e-7 T*m/A
        public const float Mu0Over4Pi = 1e-7f;

        /// <summary>
        /// Gets the world-space magnetic dipole moment vector m.
        /// </summary>
        public Vector3 GetWorldMomentVector()
        {
            return transform.TransformDirection(localAxis.normalized) * magneticMoment;
        }

        /// <summary>
        /// World-space position of North pole.
        /// </summary>
        public Vector3 NorthPoleWorldPosition => transform.position + transform.TransformDirection(localAxis.normalized) * (magnetLength * 0.5f);

        /// <summary>
        /// World-space position of South pole.
        /// </summary>
        public Vector3 SouthPoleWorldPosition => transform.position - transform.TransformDirection(localAxis.normalized) * (magnetLength * 0.5f);

        /// <summary>
        /// Calculates the magnetic field vector B (in Tesla) at a specified world position.
        /// Uses a two-pole (fictitious magnetic charge) model for accurate near-field behavior,
        /// which smoothly transitions to the classic dipole formula at larger distances.
        /// </summary>
        public Vector3 CalculateFieldAt(Vector3 worldPoint)
        {
            if (!isActive || magneticMoment <= 0f) return Vector3.zero;

            Vector3 northPos = NorthPoleWorldPosition;
            Vector3 southPos = SouthPoleWorldPosition;

            Vector3 rNorth = worldPoint - northPos;
            Vector3 rSouth = worldPoint - southPos;

            float dNorth = rNorth.magnitude;
            float dSouth = rSouth.magnitude;

            // Softening radius to prevent singularity inside magnet poles
            float softening = Mathf.Max(magnetRadius * 0.5f, 0.005f);
            float dNorthSoft = Mathf.Max(dNorth, softening);
            float dSouthSoft = Mathf.Max(dSouth, softening);

            // Equivalent pole strength q_m = m / L
            float poleStrength = magneticMoment / Mathf.Max(magnetLength, 0.01f);

            // B_north points away from North pole (+q_m)
            Vector3 bNorth = (Mu0Over4Pi * poleStrength / (dNorthSoft * dNorthSoft * dNorthSoft)) * rNorth;

            // B_south points towards South pole (-q_m)
            Vector3 bSouth = -(Mu0Over4Pi * poleStrength / (dSouthSoft * dSouthSoft * dSouthSoft)) * rSouth;

            return bNorth + bSouth;
        }
    }
}
