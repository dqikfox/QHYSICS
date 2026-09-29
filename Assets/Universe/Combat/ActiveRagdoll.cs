using System.Collections.Generic;
using UnityEngine;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Blade &amp; Sorcery-style active ragdoll for primitive humanoids (plain class owned by <see cref="CombatEnemy"/>).
    /// Every body part is its own Rigidbody. The pelvis is pulled to the animated root pose by a PD force/torque
    /// (PhysicsHands.Drive); every other part hangs off its parent with a ConfigurableJoint whose Slerp drive
    /// chases the animated local rotation. Strength (0..1) scales the drives and a per-part gravity compensation:
    /// 1 = follows the pose but still gets knocked around by hits, ~0.25 = wobbly stagger, 0 = limp ragdoll.
    /// Perf (Quest): 6 bodies + 5 joints per enemy, no allocations per step.
    /// </summary>
    public sealed class ActiveRagdollRig
    {
        public sealed class Bone
        {
            public string name;
            public Transform t;
            public Rigidbody rb;
            public Collider col;
            public int parent = -1;
            public Vector3 center;   // rest centre, root-local (scaled)
            public Vector3 pivot;    // rest joint point to parent, root-local (scaled); pelvis: its centre
            public ConfigurableJoint joint;
            public float spring;
            public Quaternion local = Quaternion.identity; // animated rotation relative to the parent frame
            // runtime
            public Vector3 wPivot;
            public Quaternion wRot;
            public Vector3 targetPos;
            public Quaternion targetRot;
        }

        public readonly List<Bone> Bones = new List<Bone>(8);
        public readonly List<Collider> Colliders = new List<Collider>(8);
        public Transform Container { get; private set; }
        public float Strength { get; private set; } = 1f;
        public Bone Pelvis => Bones.Count > 0 ? Bones[0] : null;

        float _appliedStrength = -1f;
        Vector3 _lastPelvisTarget;
        bool _hasLast;

        public ActiveRagdollRig(string name, Vector3 at)
        {
            Container = new GameObject(name + "_Body").transform;
            Container.position = at;
        }

        /// <summary>Add a primitive part at its rest pose (root-local) relative to <paramref name="root"/>.</summary>
        public Bone AddBone(Transform root, PrimitiveType type, string name, int parent, Vector3 center, Vector3 size,
            Vector3 pivot, float mass, float spring, Material mat)
        {
            GameObject g = CombatUtil.Prim(type, name, Container, Vector3.zero, size, mat, true);
            g.transform.SetPositionAndRotation(root.TransformPoint(center), root.rotation);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 4;
            rb.maxAngularVelocity = 25f;
            var b = new Bone
            {
                name = name, t = g.transform, rb = rb, col = g.GetComponent<Collider>(), parent = parent,
                center = center, pivot = pivot, spring = spring
            };
            if (b.col != null)
            {
                foreach (var c in Colliders)
                    UnityEngine.Physics.IgnoreCollision(c, b.col, true);
                Colliders.Add(b.col);
            }
            Bones.Add(b);
            return b;
        }

        /// <summary>Create the joints once every bone exists (rest pose = joint reference).</summary>
        public void BuildJoints(Transform root)
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                Bone b = Bones[i];
                if (b.parent < 0)
                    continue;
                Bone p = Bones[b.parent];
                var j = b.t.gameObject.AddComponent<ConfigurableJoint>();
                j.connectedBody = p.rb;
                j.autoConfigureConnectedAnchor = true;
                j.anchor = b.t.InverseTransformPoint(root.TransformPoint(b.pivot));
                // Joint space == bone local space (axis X, secondary Y), so targetRotation = Inverse(local).
                j.axis = Vector3.right;
                j.secondaryAxis = Vector3.up;
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
                j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
                GetLimits(b.name, out float lowX, out float highX, out float twist, out float side);
                j.lowAngularXLimit = new SoftJointLimit { limit = lowX };
                j.highAngularXLimit = new SoftJointLimit { limit = highX };
                j.angularYLimit = new SoftJointLimit { limit = twist };
                j.angularZLimit = new SoftJointLimit { limit = side };
                j.rotationDriveMode = RotationDriveMode.Slerp;
                j.enablePreprocessing = false;
                j.enableCollision = false;
                b.joint = j;
            }
            ApplyStrength(1f, true);
        }

        static void GetLimits(string name, out float lowX, out float highX, out float twist, out float side)
        {
            // Symmetric pitch limits (joint X sign is the inverse of the pose Euler X), all poses stay well inside.
            if (name.StartsWith("Leg")) { lowX = -85f; highX = 85f; twist = 15f; side = 30f; return; }
            if (name.StartsWith("Arm")) { lowX = -125f; highX = 125f; twist = 40f; side = 60f; return; }
            if (name == "Head") { lowX = -40f; highX = 40f; twist = 50f; side = 25f; return; }
            lowX = -45f; highX = 45f; twist = 40f; side = 25f; // torso on pelvis
        }

        public void ApplyStrength(float s, bool force = false)
        {
            s = Mathf.Clamp01(s);
            Strength = s;
            if (!force && Mathf.Abs(s - _appliedStrength) < 0.02f)
                return;
            _appliedStrength = s;
            for (int i = 0; i < Bones.Count; i++)
            {
                Bone b = Bones[i];
                if (b.joint == null)
                    continue;
                float k = b.spring * s;
                b.joint.slerpDrive = new JointDrive { positionSpring = k, positionDamper = k * 0.08f + 0.5f, maximumForce = 1e5f };
            }
        }

        /// <summary>
        /// Compute the animated targets for this step. Pelvis frame = root pose + pelvisOffset (root-local), then
        /// each child: pivot_w = parentPivot_w + R_parent * (pivot - parentPivot), R = R_parent * local.
        /// </summary>
        public void SolveTargets(Transform root, Vector3 pelvisOffset)
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                Bone b = Bones[i];
                if (b.parent < 0)
                {
                    b.wRot = root.rotation * b.local;
                    b.wPivot = root.TransformPoint(b.pivot + pelvisOffset);
                }
                else
                {
                    Bone p = Bones[b.parent];
                    b.wRot = p.wRot * b.local;
                    b.wPivot = p.wPivot + p.wRot * (b.pivot - p.pivot);
                }
                b.targetRot = b.wRot;
                b.targetPos = b.wPivot + b.wRot * (b.center - b.pivot);
            }
        }

        /// <summary>One physics step: pelvis PD toward the target, joint slerp targets, gravity compensation.</summary>
        public void Step(float pelvisForcePerKg, float pelvisTorquePerKg)
        {
            float dt = Time.fixedDeltaTime;
            float s = Strength;
            Vector3 g = UnityEngine.Physics.gravity;
            Bone pel = Pelvis;
            if (pel == null || pel.rb == null)
                return;
            Vector3 tVel = _hasLast ? (pel.targetPos - _lastPelvisTarget) / Mathf.Max(1e-4f, dt) : Vector3.zero;
            _lastPelvisTarget = pel.targetPos;
            _hasLast = true;
            if (s > 0.001f)
            {
                PhysicsHands.Drive(pel.rb, pel.targetPos, pel.targetRot, Vector3.ClampMagnitude(tVel, 6f),
                    pel.rb.mass * pelvisForcePerKg * s, pel.rb.mass * pelvisTorquePerKg * s, 0.05f, 0.06f);
            }
            for (int i = 0; i < Bones.Count; i++)
            {
                Bone b = Bones[i];
                if (b.rb == null)
                    continue;
                if (b.joint != null)
                    b.joint.targetRotation = Quaternion.Inverse(b.local);
                // Weightless in proportion to strength (pelvis already gravity-compensates inside Drive).
                if (s > 0.001f && i != 0)
                    b.rb.AddForce(-g * b.rb.mass * s, ForceMode.Force);
            }
        }

        /// <summary>Largest pelvis position error (m) - balance-loss detector.</summary>
        public float PelvisError()
        {
            Bone p = Pelvis;
            return p != null && p.rb != null ? Vector3.Distance(p.rb.position, p.targetPos) : 0f;
        }

        public Bone Find(string name)
        {
            for (int i = 0; i < Bones.Count; i++)
                if (Bones[i].name == name) return Bones[i];
            return null;
        }

        public Bone Nearest(Vector3 p)
        {
            Bone best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].rb == null) continue;
                float d = (Bones[i].rb.worldCenterOfMass - p).sqrMagnitude;
                if (d < bd) { bd = d; best = Bones[i]; }
            }
            return best;
        }

        public bool Owns(Collider c)
        {
            return c != null && Container != null && c.transform.IsChildOf(Container);
        }

        public Renderer[] Renderers()
        {
            return Container != null ? Container.GetComponentsInChildren<Renderer>() : new Renderer[0];
        }

        /// <summary>Death: drives off for good, a little extra damping so the corpse settles.</summary>
        public void GoLimpForever(float lifetime)
        {
            ApplyStrength(0f, true);
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].rb == null) continue;
                Bones[i].rb.angularDamping = 2.5f;
                Bones[i].rb.linearDamping = 0.2f;
            }
            if (Container != null)
            {
                var c = Container.gameObject.AddComponent<RagdollCorpse>();
                c.lifetime = lifetime;
            }
        }
    }

    /// <summary>Corpse cleanup: frees weapons stuck in the parts, then removes the body after a delay.</summary>
    public sealed class RagdollCorpse : MonoBehaviour
    {
        public float lifetime = 8f;
        float _born;

        void Start() { _born = Time.time; }

        void Update()
        {
            if (Time.time - _born < lifetime) return;
            foreach (var w in PhysicsWeapon.All.ToArray())
            {
                if (w == null || !w.IsStuck || w.StuckBody == null) continue;
                if (w.StuckBody.transform.IsChildOf(transform)) w.Unstick();
            }
            Destroy(gameObject);
        }
    }
}
