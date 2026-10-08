using UnityEngine;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Builds primitive-based physics weapons along local +Y (handle at 0, tip at the top).
    /// enemyStyle: darker enemy colours + red trail (enemy-carried Axe / Saber / Staff / Tower Shield); the weapon
    /// stays a normal loot weapon once its owner drops it.
    /// </summary>
    public static class WeaponFactory
    {
        public static PhysicsWeapon Spawn(WeaponKind kind, Vector3 pos, Quaternion rot)
        {
            return Spawn(kind, pos, rot, false);
        }

        public static PhysicsWeapon Spawn(WeaponKind kind, Vector3 pos, Quaternion rot, bool enemyStyle)
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
                case WeaponKind.Axe:
                {
                    w.displayName = enemyStyle ? "Raider Axe" : "Axe";
                    Material head = enemyStyle ? enemySteel : steel;
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Handle, new Vector3(0f, 0.35f, 0f), new Vector3(0.036f, 0.35f, 0.036f), dark);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Guard, new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 0.1f, 0.04f), enemyStyle ? red : brass);
                    // Bearded blade on +X, spike on -X.
                    Part(go, PrimitiveType.Cube, WeaponPartType.Edge, new Vector3(0.1f, 0.6f, 0f), new Vector3(0.15f, 0.17f, 0.014f), head);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Edge, new Vector3(0.18f, 0.6f, 0f), new Vector3(0.03f, 0.23f, 0.01f), head);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Head, new Vector3(-0.07f, 0.6f, 0f), new Vector3(0.09f, 0.04f, 0.03f), head);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Head, new Vector3(0f, 0.71f, 0f), new Vector3(0.035f, 0.04f, 0.035f), head);
                    Finish(w, rb, 1.9f, 0.03f, 0.5f, 0.72f, false, false, 1.15f, 1.3f);
                    break;
                }
                case WeaponKind.Saber:
                {
                    w.displayName = enemyStyle ? "Duelist Saber" : "Saber";
                    Material blade = enemyStyle ? enemySteel : steel;
                    Material trim = enemyStyle ? CombatMaterials.Get(new Color(0.7f, 0.5f, 0.15f), 0.85f, 0.65f, new Color(1f, 0.6f, 0.1f) * 0.35f) : cyan;
                    Part(go, PrimitiveType.Sphere, WeaponPartType.Handle, new Vector3(0f, -0.01f, 0f), Vector3.one * 0.035f, trim);
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Handle, new Vector3(0f, 0.085f, 0f), new Vector3(0.03f, 0.085f, 0.03f), dark);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Guard, new Vector3(0f, 0.18f, 0f), new Vector3(0.12f, 0.016f, 0.04f), trim);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Edge, new Vector3(0f, 0.57f, 0f), new Vector3(0.034f, 0.76f, 0.006f), blade);
                    Part(go, PrimitiveType.Cube, WeaponPartType.Tip, new Vector3(0f, 0.985f, 0f), new Vector3(0.02f, 0.07f, 0.006f), blade);
                    Finish(w, rb, 1.0f, 0.01f, 0.16f, 1.02f, false, true, 0.95f, 0.8f);
                    break;
                }
                case WeaponKind.Staff:
                {
                    w.displayName = enemyStyle ? "Caster Staff" : "Quarterstaff";
                    Material crystal = enemyStyle ? CombatMaterials.Get(new Color(0.35f, 0.1f, 0.55f), 0f, 0.7f, new Color(0.8f, 0.25f, 1.4f) * 1.2f) : cyan;
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Handle, new Vector3(0f, 0.85f, 0f), new Vector3(0.04f, 0.85f, 0.04f), dark);
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Head, new Vector3(0f, 0.03f, 0f), new Vector3(0.055f, 0.03f, 0.055f), enemyStyle ? enemySteel : brass);
                    Part(go, PrimitiveType.Cylinder, WeaponPartType.Guard, new Vector3(0f, 1.62f, 0f), new Vector3(0.06f, 0.04f, 0.06f), enemyStyle ? enemySteel : brass);
                    Part(go, PrimitiveType.Sphere, WeaponPartType.Head, new Vector3(0f, 1.74f, 0f), Vector3.one * 0.1f, crystal);
                    Finish(w, rb, 1.8f, 0.25f, 1.45f, 1.78f, true, false, 0.9f, 1.5f);
                    break;
                }
                case WeaponKind.TowerShield:
                {
                    w.displayName = enemyStyle ? "Warden Shield" : "Tower Shield";
                    Material plate = enemyStyle ? CombatMaterials.Get(new Color(0.3f, 0.32f, 0.37f), 0.8f, 0.45f) : steel;
                    Part(go, PrimitiveType.Cube, WeaponPartType.Handle, new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.03f, 0.03f), dark);
                    Part(go, PrimitiveType.Cube, WeaponPartType.ShieldFace, new Vector3(0f, 0.075f, 0f), new Vector3(0.5f, 0.024f, 0.78f), plate);
                    Part(go, PrimitiveType.Cube, WeaponPartType.ShieldFace, new Vector3(0f, 0.09f, 0f), new Vector3(0.08f, 0.012f, 0.6f), enemyStyle ? red : cyan, false);
                    Part(go, PrimitiveType.Cube, WeaponPartType.ShieldFace, new Vector3(0f, 0.09f, 0.1f), new Vector3(0.36f, 0.012f, 0.06f), enemyStyle ? red : cyan, false);
                    Finish(w, rb, 4.0f, 0f, 0.05f, 0.1f, false, false, 0.5f, 1.6f);
                    break;
                }
            }
            w.RefreshColliders();
            bool redTrail = kind == WeaponKind.EnemySword || enemyStyle;
            AddTrail(w, redTrail ? new Color(1f, 0.25f, 0.2f, 0.5f) : new Color(0f, 0.9f, 1f, 0.45f));
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

    /// <summary>
    /// Holds a weapon on a rack with a breakable FixedJoint (anchored to the world) so it hangs still but stays a
    /// normal dynamic body: grabbing it, Blade Recall, telekinesis or a hard knock frees it. Enemies ignore racked weapons.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RackHold : MonoBehaviour
    {
        PhysicsWeapon _w;
        FixedJoint _joint;
        Vector3 _pos;

        public static RackHold Hang(PhysicsWeapon w)
        {
            if (w == null) return null;
            var h = w.gameObject.AddComponent<RackHold>();
            h._w = w;
            h._pos = w.transform.position;
            h._joint = w.gameObject.AddComponent<FixedJoint>();
            h._joint.breakForce = 220f;
            h._joint.breakTorque = 120f;
            h._joint.enablePreprocessing = false;
            return h;
        }

        void FixedUpdate()
        {
            if (_w == null) { Destroy(this); return; }
            if (_joint == null || _w.IsHeld || _w.IsStuck || _w.GetComponent<RecallFlight>() != null
                || (_w.transform.position - _pos).sqrMagnitude > 0.04f)
                Release();
        }

        public void Release()
        {
            if (_joint != null) Destroy(_joint);
            _joint = null;
            if (_w != null && _w.Body != null) _w.Body.WakeUp();
            Destroy(this);
        }
    }
}
