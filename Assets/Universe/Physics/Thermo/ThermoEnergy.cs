using UnityEngine;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Biology;

namespace RealityEngine.Physics.Thermo
{
    /// <summary>
    /// Reality Engine v1.0 toy heat-path accounting. Classical lumped model.
    /// Not CFD. Not a real Carnot machine. Not QM. Educational approximation.
    /// </summary>
    public static class ThermoEnergy
    {
        public const string Honesty =
            "Classical toy heat engine. Not CFD. Not a real Carnot machine. Not QM. Educational approximation.";

        public const string Educational =
            "Educational approximation — toy efficiency model, not measured calorimetry.";

        public const string LabName = "Faraday Bench";

        public const float DefaultTHot = 400f;
        public const float DefaultTCold = 300f;
        public const float ConductanceK = 0.05f;
        public const float EtaCap = 0.4f;
        public const float MinDeltaK = 5f;
        public const float FlattenKelvinPerSecond = 1.5f;

        public static float ToyEta(float tHot, float tCold)
        {
            if (tHot <= 1e-3f)
                return 0f;
            return Mathf.Clamp(1f - tCold / tHot, 0f, EtaCap);
        }

        public static void Account(float tHot, float tCold, bool pathClosed, out float qIn, out float eta, out float captured, out float losses)
        {
            eta = ToyEta(tHot, tCold);
            if (!pathClosed)
            {
                qIn = 0f;
                captured = 0f;
                losses = 0f;
                return;
            }

            qIn = ConductanceK * Mathf.Max(0f, tHot - tCold);
            captured = eta * qIn;
            losses = qIn - captured;
        }

        public static string MathLines(float tHot, float tCold, float qIn, float eta, float captured, float losses)
        {
            return
                "MATH  toy heat path (lumped, not CFD)\n" +
                "Q_in = k (T_hot - T_cold)   k=" + ConductanceK.ToString("0.###") + "\n" +
                "eta_toy = clamp(1 - Tc/Th, 0, " + EtaCap.ToString("0.00") + ") = " + eta.ToString("0.000") + "\n" +
                "W = eta Q_in = " + captured.ToString("0.000") + "\n" +
                "Q_c = Q_in - W = " + losses.ToString("0.000") + "\n" +
                "INPUT = CAPTURED + LOSSES  (by construction)\n" +
                "T_hot=" + tHot.ToString("0.0") + " K  T_cold=" + tCold.ToString("0.0") + " K\n" +
                Honesty;
        }

        public static string AccountLines(float qIn, float eta, float captured, float losses, bool pathClosed)
        {
            string path = pathClosed ? "PATH CLOSED" : "PATH OPEN (flow = 0)";
            return
                "HEAT  " + path + "\n" +
                "INPUT     " + qIn.ToString("0.000") + "  Q_in / s  (toy units)\n" +
                "CAPTURED  " + captured.ToString("0.000") + "  work W = eta Q_in  (eta=" + eta.ToString("0.00") + ")\n" +
                "LOSSES    " + losses.ToString("0.000") + "  rejected heat Q_c = Q_in - W\n" +
                "identity  INPUT = CAPTURED + LOSSES  (" + (Mathf.Abs(qIn - (captured + losses)) < 1e-4f ? "holds" : "CHECK") + ")\n" +
                Educational;
        }

        public static string CoilInstantPowerLines(InductionCircuit circuit)
        {
            if (circuit == null)
            {
                return
                    "COIL  (no circuit)\n" +
                    "instantaneous toy electrical power, not calorimetry.";
            }

            float emf = circuit.EmfVolts;
            float i = circuit.CurrentAmperes;
            float pLoad = circuit.LoadPowerWatts;
            float rTot = circuit.TotalResistanceOhms;
            float rLoad = 0f;
            float rWind = 0f;
            if (circuit.Coil != null)
            {
                rLoad = circuit.Coil.LoadResistance;
                rWind = circuit.Coil.Resistance;
            }

            float input = Mathf.Abs(emf * i);
            float losses = i * i * rWind;
            return
                "COIL  (instantaneous toy electrical power, not calorimetry)\n" +
                "  EMF " + Sci(emf) + " V   I " + Sci(i) + " A   R_tot " + rTot.ToString("0.###") + " ohm\n" +
                "INPUT     |EMF I|          " + Sci(input) + " W\n" +
                "CAPTURED  I^2 R_load       " + Sci(pLoad) + " W   (R_load=" + rLoad.ToString("0.###") + ")\n" +
                "LOSSES    I^2 R_winding    " + Sci(losses) + " W   (R_wind=" + rWind.ToString("0.###") + ")\n" +
                "identity  |EMF I| = I^2 R_tot  (resistive loop; no L di/dt)";
        }

        public static string CellAccountLines()
        {
            return
                "CELL  biology toy account  eta=" + BioEnergy.Efficiency.ToString("0.00") + "\n" +
                BioEnergy.AccountLines();
        }

        public static string EnergyCreatedAnswer(InductionCircuit circuit, HeatCoupler heat)
        {
            float qIn = 0f, eta = 0f, captured = 0f, losses = 0f;
            bool closed = false;
            float th = DefaultTHot;
            float tc = DefaultTCold;
            if (heat != null)
            {
                qIn = heat.QinPerSecond;
                eta = heat.Eta;
                captured = heat.CapturedPerSecond;
                losses = heat.LossesPerSecond;
                closed = heat.PathClosed;
                th = heat.THot;
                tc = heat.TCold;
            }

            return
                "Q6  Is energy created in this lab?\n" +
                "No. Coil / cell / heat each convert a gradient. None create energy.\n" +
                CoilInstantPowerLines(circuit) + "\n" +
                "  Coil gradient: changing magnetic flux (lumped Faraday).\n" +
                CellAccountLines() + "\n" +
                "  Cell gradient: conceptual chemical / proton-motive (ATP schematic).\n" +
                AccountLines(qIn, eta, captured, losses, closed) + "\n" +
                "  Heat gradient: T_hot=" + th.ToString("0.0") + " K minus T_cold=" + tc.ToString("0.0") + " K.\n" +
                "Footer: None of these create energy. Each extracts work from a gradient. INPUT = CAPTURED + LOSSES by construction of the toy ledger.\n" +
                Honesty + " " + BioEnergy.Honesty;
        }

        public static string Footer()
        {
            return "None of these create energy. Each extracts work from a gradient. INPUT = CAPTURED + LOSSES by construction of the toy ledger.";
        }

        public static string IntegrityLines()
        {
            return
                "MODEL       lumped Faraday coil + conceptual ATP cell + toy heat path\n" +
                "PARAMETERS  coil N,R live; cell eta=0.25 fixed toy; heat k=" + ConductanceK.ToString("0.###") +
                "  eta_cap=" + EtaCap.ToString("0.00") + "  T_hot0=" + DefaultTHot.ToString("0") +
                " K  T_cold0=" + DefaultTCold.ToString("0") + " K\n" +
                "ASSUMPTIONS resistive loop (no L); ATP not kinetics; heat not CFD / not a real Carnot engine\n" +
                "ACCURACY    educational toy ledger — numbers are constructed identities, not calorimetry";
        }

        static string Sci(float value)
        {
            float abs = Mathf.Abs(value);
            if (abs > 0f && (abs < 1e-3f || abs >= 1e3f))
                return value.ToString("0.000e+0");
            return value.ToString("0.0000");
        }
    }
}
