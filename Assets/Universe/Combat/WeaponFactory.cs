using UnityEngine;

namespace RealityEngine.Combat
{
    /// <summary>Builds primitive-based physics weapons along local +Y (handle at 0, tip at the top).</summary>
    public static class WeaponFactory
    {
        public static PhysicsWeapon Spawn(WeaponKind kind, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Weapon_" + kind);
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            var w = go.AddComponent<PhysicsWeapon>();
            w.kind = kind;

            Material steel = CombatMaterials.Get(new Color(0.78f, 0.8f, 0.84f), 0.9f, 0.75f);
            Material dark = CombatMaterials.Get(new Color(0.16f, 0.12f, 0.1f), 0f, 0.3f);
            Material brass = CombatMaterials.Get(new Color(0.75f, 0.58f, 0.25f), 0.8f, 0.6f);
            Material cyan = CombatMaterials.Get(new Color(0.05f, 0.25f, 0.3f), 0.2f, 0.6f, new Color(0f, 0.9f, 1f) * 0.8f);
            Material enemySteel = CombatMaterials.Get(new Color(0.35f, 0.3f, 0.32f), 0.8f, 0.5f);
            Material red = CombatMaterials.Get(new Color(0.45f, 0.05f, 0.05f), 0.1f, 0.4f, new Color(1f, 0.1f, 0.05f) * 0.6f);

            switch (kind)
            {
                case WeaponKind.Dagger:
                    w.displayName = "Dagger";
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Handle, new Vector3(0f, 0.055f, 0f), new Vector3(0.03f, 0.055f, 0.03f), dark);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Guard, new Vector3(0f, 0.118f, 0f), new Vector3(0.08f, 0.014f, 0.022f), brass);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Edge, new Vector3(0f, 0.225f, 0f), new Vector3(0.032f, 0.2f, 0.006f), steel);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Tip, new Vector3(0f, 0.34f, 0f), new Vector3(0.018f, 0.04f, 0.006f), steel);
                    Finish(w, rb, 0.45f, 0.01f, 0.1f, 0.36f, false, true, 0.9f, 0.7f);
                    break;
                case WeaponKind.Sword:
                case WeaponKind.EnemySword:
                {
                    bool enemy = kind == WeaponKind.EnemySword;
                    w.displayName = enemy ? "Raider Sword" : "Sword";
                    Part(go, PrimitiveType.Sphere, WeaponPartType.Handle, new Vector3(0f, -0.015f, 0f), Vector3.one * 0.045f, enemy ? red : brass);
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Handle, new Vector3(0f, 0.11f, 0f), new Vector3(0.034f, 0.11f, 0.034f), dark);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Guard, new Vector3(0f, 0.235f, 0f), new Vector3(0.2f, 0.022f, 0.03f), enemy ? red : brass);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Edge, new Vector3(0f, 0.6f, 0f), new Vector3(0.048f, 0.7f, 0.008f), enemy ? enemySteel : steel);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Tip, new Vector3(0f, 0.985f, 0f), new Vector3(0.026f, 0.07f, 0.008f), enemy ? enemySteel : steel);
                    Finish(w, rb, 1.3f, 0.02f, 0.21f, 1.02f, !enemy, true, 1f, 1f);
                    if (enemy) w.ownerTeam = CombatTeam.Enemy;
                    break;
                }
                case WeaponKind.Spear:
                    w.displayName = "Spear";
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Handle, new Vector3(0f, 0.85f, 0f), new Vector3(0.036f, 0.85f, 0.036f), dark);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Guard, new Vector3(0f, 1.71f, 0f), new Vector3(0.05f, 0.03f, 0.05f), brass);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Tip, new Vector3(0f, 1.83f, 0f), new Vector3(0.05f, 0.22f, 0.01f), steel);
                    Finish(w, rb, 2.2f, 0.1f, 1.5f, 1.95f, true, true, 1.05f, 0.8f);
                    break;
                case WeaponKind.Mace:
                    w.displayName = "Mace";
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Handle, new Vector3(0f, 0.28f, 0f), new Vector3(0.036f, 0.28f, 0.036f), dark);
                    Part(go, PrimitiveType.Sphere, WeaponPartType.Head, new Vector3(0f, 0.62f, 0f), Vector3.one * 0.17f, steel);
                    for (int i = 0; i < 4; i++)
                    {
                        var fl = Part(go, PrimitiveType.Cube, WeaponPartType.Head, new Vector3(0f, 0.62f, 0f), new Vector3(0.23f, 0.12f, 0.02f), steel);
                        fl.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                    }
                    Finish(w, rb, 2.8f, 0.03f, 0.45f, 0.72f, true, false, 1.1f, 1.7f);
                    break;
                case WeaponKind.Shield:
                    w.displayName = "Shield";
                    Part(go, PrimitiveType.Cube, WeaponPartType.Handle, new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.03f, 0.03f), dark);
                    var face = Part(go, PrimitiveType.Cylinder, WeaponPartType.ShieldFace, new Vector3(0f, 0.075f, 0f), new Vector3(0.62f, 0.012f, 0.62f), steel);
                    Object.DestroyImmediate(face.GetComponent<Collider>());
                    var bc = face.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.9f, 2f, 0.9f);
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.ShieldFace, new Vector3(0f, 0.09f, 0f), new Vector3(0.14f, 0.012f, 0.14f), cyan, false);
                    Finish(w, rb, 3.0f, 0f, 0.04f, 0.1f, false, false, 0.5f, 1.4f);
                    break;
            }
            w.RefreshColliders();
            AddTrail(w, kind == WeaponKind.EnemySword ? new Color(1f, 0.25f, 0.2f, 0.5f) : new Color(0f, 0.9f, 1f, 0.45f));
            return w;
        }

        static GameObject Part(GameObject root, PrimitiveType t, WeaponPartType part, Vector3 lp, Vector3 ls, Material m, bool collider = true)
        {
            var g = CombatUtil.Prim(t, part.ToString(), root.transform, lp, ls, m, collider);
            if (collider)
            {
                var wp = g.AddComponent<WeaponPart>();
                wp.weapon = root.GetComponent<PhysicsWeapon>();
                wp.part = part;
            }
            return g;
        }

        static void Finish(PhysicsWeapon w, Rigidbody rb, float mass, float gMin, float gMax, float tip, bool twoHanded, bool stab,
            float dmg, float knock)
        {
            rb.mass = mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            w.gripMinY = gMin;
            w.gripMaxY = gMax;
            w.tipY = tip;
            w.twoHanded = twoHanded;
            w.canStab = stab;
            w.damageScale = dmg;
            w.knockbackScale = knock;
        }

        static void AddTrail(PhysicsWeapon w, Color c)
        {
            var t = new GameObject("Trail");
            t.transform.SetParent(w.transform, false);
            t.transform.localPosition = new Vector3(0f, w.tipY * 0.85f, 0f);
            var tr = t.AddComponent<TrailRenderer>();
            tr.time = 0.12f;
            tr.minVertexDistance = 0.02f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, Mathf.Clamp(w.tipY * 0.12f, 0.03f, 0.12f)), new Keyframe(1f, 0f));
            tr.sharedMaterial = CombatMaterials.Transparent(c, c);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.emitting = false;
            w.SetTrail(tr);
        }
    }
}
