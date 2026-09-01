using RealityEngine.Chemistry;

namespace RealityEngine.Biology
{
    /// <summary>
    /// Reality Engine v0.9 toy energy accounting for the muscle / mitochondrion slice.
    /// Educational approximation. Not MD, not kinetics, not systems biology.
    /// </summary>
    public static class BioEnergy
    {
        public const string Honesty = "Conceptual visualization / Classical energy accounting.";
        public const string NotSimulation =
            "Not molecular dynamics. Not a kinetic simulation. Not systems biology. Not QM.";
        public const string Educational =
            "Educational approximation ? toy efficiency model, not measured calorimetry.";

        public const float InputUnits = 100f;
        public const float Efficiency = 0.25f;

        public static float CapturedUnits => InputUnits * Efficiency;
        public static float LossUnits => InputUnits - CapturedUnits;

        public static string ScaleLadder()
        {
            return
                "Organism                 " + Honesty + "\n" +
                "Cell                     " + Honesty + "\n" +
                "Mitochondrion            " + Honesty + "\n" +
                "Protein (ATP synthase)   " + Honesty + "\n" +
                "Molecule (ATP)           " + Honesty + "\n" +
                "Atom (C, H, O, N, P)     " + Honesty;
        }

        public static string AccountLines()
        {
            return
                "INPUT     " + InputUnits.ToString("0") + "  chemical potential (toy units)\n" +
                "CAPTURED  " + CapturedUnits.ToString("0") + "  ATP -> mechanical work  (eta=" + Efficiency.ToString("0.00") + ")\n" +
                "LOSSES    " + LossUnits.ToString("0") + "  heat + unused\n" +
                Educational;
        }

        public static string GradientContrast()
        {
            return
                "Coil: electromagnetic induction extracts useful work from a changing magnetic flux (an EM gradient).\n" +
                "Cell: a conceptual chemical / proton-motive gradient is transduced (ATP synthase schematic) to ATP, then to mechanical work.\n" +
                "Both extract useful work from a gradient. Neither creates energy.";
        }

        public static string HydrolysisMath()
        {
            return
                "MATH  ATP hydrolysis (conceptual chemical potential, not kinetics)\n" +
                "ATP + H2O  ->  ADP + Pi\n" +
                "dG < 0  (schematic sign only; no dG table is solved here)\n" +
                "Work comes from a drop in chemical potential along that story.\n" +
                Honesty + "\n" +
                NotSimulation;
        }

        public static string MuscleEnergyAnswer()
        {
            return
                "Q5  Where does muscle energy come from?\n" +
                ScaleLadder() + "\n" +
                AccountLines() + "\n" +
                GradientContrast() + "\n" +
                "Landing chemistry: ATP is schematically " + Element.AtpFormula +
                " ? atoms C, H, O, N, P. The copper coil is the same lab's Cu chemistry slice. Continuity of scale, not a second universe.\n" +
                Honesty + " " + NotSimulation;
        }
    }
}
