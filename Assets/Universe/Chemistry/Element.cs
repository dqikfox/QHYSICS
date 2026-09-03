using UnityEngine;

namespace RealityEngine.Chemistry
{
    /// <summary>
    /// Reality Engine v0.8 chemistry slice. Tabulated element facts for the copper coil
    /// (and optional magnet metals). Not a QM band-structure solver, not molecular dynamics.
    /// </summary>
    public readonly struct Element
    {
        public readonly int Z;
        public readonly string Symbol;
        public readonly string Name;
        public readonly string ElectronShells;
        public readonly string Honesty;
        public readonly string BondingOneLiner;

        public const string ConceptualHonesty =
            "Conceptual chemistry, not a QM band-structure solver. Not molecular dynamics.";

        public const string ClassicalCoilHonesty =
            "Classical lumped model: the coil is a resistor R_winding. Conductivity is an input, not computed from orbitals.";

        public Element(int z, string symbol, string name, string electronShells, string honesty, string bondingOneLiner)
        {
            Z = z;
            Symbol = symbol;
            Name = name;
            ElectronShells = electronShells;
            Honesty = honesty;
            BondingOneLiner = bondingOneLiner;
        }

        public static readonly Element Cu = new Element(
            29,
            "Cu",
            "Copper",
            "2, 8, 18, 1",
            ConceptualHonesty,
            "Metallic Cu: CONCEPTUAL delocalized electrons in a metal lattice (not a band-structure solve) explain why bulk copper is a conductor. The induction coil uses that as a classical lumped resistance R_winding.");

        public static readonly Element Fe = new Element(
            26,
            "Fe",
            "Iron",
            "2, 8, 14, 2",
            ConceptualHonesty,
            "Fe is a 3d metal used in many magnets. This lab's bar magnet is a classical two-pole dipole, not a micromagnetic Fe/NdFeB simulation.");

        public static readonly Element Nd = new Element(
            60,
            "Nd",
            "Neodymium",
            "2, 8, 18, 22, 8, 2",
            ConceptualHonesty,
            "Nd appears in NdFeB magnet marketing. Here the magnet is still a classical two-pole MagneticDipole. No rare-earth electronic structure is solved.");

        public static readonly Element C = new Element(
            6,
            "C",
            "Carbon",
            "2, 4",
            ConceptualHonesty,
            "Organic backbone of ATP (schematic). Not a QM electronic-structure solve.");

        public static readonly Element H = new Element(
            1,
            "H",
            "Hydrogen",
            "1",
            ConceptualHonesty,
            "Protons appear in the conceptual proton-motive story; here H is an element card, not a pH simulation.");

        public static readonly Element O = new Element(
            8,
            "O",
            "Oxygen",
            "2, 6",
            ConceptualHonesty,
            "ATP hydrolysis is conceptually ATP + H2O -> ADP + Pi. Not a reaction-rate solver.");

        public static readonly Element N = new Element(
            7,
            "N",
            "Nitrogen",
            "2, 5",
            ConceptualHonesty,
            "Nitrogenous base in ATP (adenine, schematic). Not a tautomer simulation.");

        public static readonly Element P = new Element(
            15,
            "P",
            "Phosphorus",
            "2, 8, 5",
            ConceptualHonesty,
            "Phosphate groups in ATP. 'High-energy phosphate' is classical accounting, not a special kind of energy.");

        public static readonly Element Ca = new Element(
            20,
            "Ca",
            "Calcium",
            "2, 8, 8, 2",
            ConceptualHonesty,
            "Ca in calcite (CaCO3). Ionic Ca2+ with carbonate — conceptual crystal, not XRD, not a band-structure solve.");

        public const string AtpFormula = "C10H16N5O13P3";
        public const string CalciteFormula = "CaCO3";

        public string TinyCard()
        {
            return Symbol + " Z=" + Z + "  shells " + ElectronShells;
        }

        public static string AtpAtomCards()
        {
            return "ATP atoms (schematic " + AtpFormula + ")\n"
                + C.TinyCard() + "\n"
                + H.TinyCard() + "\n"
                + O.TinyCard() + "\n"
                + N.TinyCard() + "\n"
                + P.TinyCard() + "\n"
                + ConceptualHonesty + "\n"
                + "Same lab chemistry as the Cu coil (Cu / C / H / O continuity; ATP also uses N, P).";
        }

        public static string CalciteAtomCards()
        {
            return "calcite (schematic " + CalciteFormula + ")\n"
                + Ca.TinyCard() + "\n"
                + C.TinyCard() + "\n"
                + O.TinyCard() + "\n"
                + "Conceptual / Classical crystal, not XRD.\n"
                + ConceptualHonesty;
        }

        public string PeriodicCard()
        {
            return Symbol + "  Z=" + Z + "  " + Name + "\n"
                + "shells  " + ElectronShells + "  (schematic, not a quantum state)\n"
                + BondingOneLiner + "\n"
                + Honesty;
        }

        public static string CopperConductorAnswer()
        {
            return "Q4  Why is copper a conductor?\n"
                + Cu.PeriodicCard() + "\n"
                + "Lab link: copper wire / coil conductivity. Metallic bonding (CONCEPTUAL) -> mobile charge in bulk Cu -> the lumped Faraday coil is treated as a conductor with R_winding="
                + " finite ohms, I = EMF / (R_winding + R_load).\n"
                + ClassicalCoilHonesty;
        }
    }
}