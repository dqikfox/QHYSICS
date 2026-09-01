using RealityEngine.Core;

namespace RealityEngine.Biology
{
    /// <summary>
    /// Maps ScaleEngine Human/Material/Molecular/Atomic onto the v0.9 biology slice.
    /// Does not replace ScaleEngine and does not move the camera.
    /// </summary>
    public static class BioScale
    {
        public const string Honesty = BioEnergy.Honesty;

        public static string RepresentationOf(ScaleLevel scale)
        {
            switch (scale)
            {
                case ScaleLevel.Material:
                    return "Membrane look (lipid bilayer conceptual)";
                case ScaleLevel.Molecular:
                    return "ATP / ATP synthase schematic (few spheres)";
                case ScaleLevel.Atomic:
                    return "Element cards C, H, O, N, P ? not QM";
                default:
                    return "Cell blob (muscle fiber / mitochondrion schematic)";
            }
        }

        public static string HonestyOf(ScaleLevel scale)
        {
            return Honesty;
        }

        public static string InForceOf(ScaleLevel scale)
        {
            switch (scale)
            {
                case ScaleLevel.Material:
                    return "IN FORCE: classical energy accounting. VISUAL: membrane look. PARKED: real bilayer physics.";
                case ScaleLevel.Molecular:
                    return "IN FORCE: classical energy accounting. VISUAL: ATP / protein schematic. PARKED: MD / kinetics.";
                case ScaleLevel.Atomic:
                    return "IN FORCE: tabulated element cards. PARKED: electronic structure / QM.";
                default:
                    return "IN FORCE: conceptual cell / mitochondrion blob. PARKED: physiology, MD, ecosystems.";
            }
        }
    }
}
