using System;
using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>
    /// Lab operator archetype (not fantasy class). Stats + starting carry kit + body tint.
    /// Runtime catalog — no ScriptableObject assets required for the vertical slice.
    /// </summary>
    [Serializable]
    public sealed class OperatorArchetype
    {
        public string Id;
        public string DisplayName;
        public string Blurb;
        public float MoveSpeed = 3.4f;
        public float SprintMult = 2f;
        public float MaxHp = 100f;
        public int InventorySlots = 6;
        public Color BodyTint = new Color(0.22f, 0.25f, 0.32f);
        public Color ArmTint = new Color(0.20f, 0.24f, 0.30f);
        public CarryItemId[] StartingKit;

        public static readonly OperatorArchetype[] Catalog =
        {
            new OperatorArchetype
            {
                Id = "field_scientist",
                DisplayName = "Field Scientist",
                Blurb = "Balanced operator. Extra inspect / measure kit.",
                MoveSpeed = 3.4f,
                SprintMult = 2.0f,
                MaxHp = 100f,
                InventorySlots = 6,
                BodyTint = new Color(0.18f, 0.28f, 0.36f),
                ArmTint = new Color(0.15f, 0.45f, 0.55f),
                StartingKit = new[]
                {
                    CarryItemId.ProbeTip,
                    CarryItemId.BatteryPack,
                    CarryItemId.HealthAmpoule,
                    CarryItemId.TrainingBaton
                }
            },
            new OperatorArchetype
            {
                Id = "lab_engineer",
                DisplayName = "Lab Engineer",
                Blurb = "Builder focus. Extra carry slots + BUILD-leaning kit.",
                MoveSpeed = 3.2f,
                SprintMult = 1.85f,
                MaxHp = 110f,
                InventorySlots = 8,
                BodyTint = new Color(0.28f, 0.24f, 0.18f),
                ArmTint = new Color(0.75f, 0.55f, 0.20f),
                StartingKit = new[]
                {
                    CarryItemId.BatteryPack,
                    CarryItemId.BatteryPack,
                    CarryItemId.ShieldCell,
                    CarryItemId.ProbeTip,
                    CarryItemId.HealthAmpoule
                }
            },
            new OperatorArchetype
            {
                Id = "survey_ranger",
                DisplayName = "Survey Ranger",
                Blurb = "Mobility + lighter training baton focus.",
                MoveSpeed = 3.9f,
                SprintMult = 2.35f,
                MaxHp = 90f,
                InventorySlots = 5,
                BodyTint = new Color(0.16f, 0.30f, 0.20f),
                ArmTint = new Color(0.30f, 0.70f, 0.40f),
                StartingKit = new[]
                {
                    CarryItemId.TrainingBaton,
                    CarryItemId.TrainingBaton,
                    CarryItemId.HealthAmpoule,
                    CarryItemId.ShieldCell
                }
            }
        };

        public static OperatorArchetype Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Catalog[0];
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].Id == id)
                    return Catalog[i];
            }
            return Catalog[0];
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].Id == id)
                    return i;
            }
            return 0;
        }
    }
}
