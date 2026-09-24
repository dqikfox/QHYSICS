using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>Lab-flavored carry items for the training / field kit (not BUILD hotbar gadgets).</summary>
    public enum CarryItemId
    {
        None = 0,
        HealthAmpoule = 1,
        BatteryPack = 2,
        ProbeTip = 3,
        TrainingBaton = 4,
        ShieldCell = 5
    }

    public static class CarryItemCatalog
    {
        public struct Def
        {
            public CarryItemId Id;
            public string Label;
            public string Hint;
            public Color Color;
            public bool IsCombatTool;
            public bool IsConsumable;
            public float HealAmount;
            public float ShieldSeconds;
        }

        static readonly Def[] All =
        {
            new Def { Id = CarryItemId.HealthAmpoule, Label = "Health Ampoule", Hint = "Restore operator vitals", Color = new Color(0.35f, 0.95f, 0.55f), IsConsumable = true, HealAmount = 35f },
            new Def { Id = CarryItemId.BatteryPack, Label = "Battery Pack", Hint = "Field power cell (kit)", Color = new Color(0.25f, 0.85f, 0.40f) },
            new Def { Id = CarryItemId.ProbeTip, Label = "Probe Tip", Hint = "Spare measure tip", Color = new Color(0.25f, 0.85f, 0.95f) },
            new Def { Id = CarryItemId.TrainingBaton, Label = "Training Baton", Hint = "Energy baton — Training System", Color = new Color(0.95f, 0.55f, 0.20f), IsCombatTool = true },
            new Def { Id = CarryItemId.ShieldCell, Label = "Shield Cell", Hint = "Brief soft shield overlay", Color = new Color(0.40f, 0.55f, 1f), IsConsumable = true, ShieldSeconds = 8f }
        };

        public static bool TryGet(CarryItemId id, out Def def)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    def = All[i];
                    return true;
                }
            }
            def = default;
            return false;
        }

        public static string LabelOf(CarryItemId id) =>
            TryGet(id, out Def d) ? d.Label : "Empty";

        public static Color ColorOf(CarryItemId id) =>
            TryGet(id, out Def d) ? d.Color : new Color(0.35f, 0.35f, 0.38f);
    }
}
