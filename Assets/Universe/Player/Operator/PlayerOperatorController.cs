using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>
    /// Applies selected <see cref="OperatorArchetype"/> to locomotion, vitals, carry slots, body tint.
    /// Persists last choice in PlayerPrefs.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(126)]
    public sealed class PlayerOperatorController : MonoBehaviour
    {
        public const string PrefsKey = "qhysics.operator.id";
        public const string RootName = "PlayerOperatorController";

        public static PlayerOperatorController Instance { get; private set; }

        OperatorArchetype _current;

        public OperatorArchetype Current => _current ?? OperatorArchetype.Catalog[0];
        public string CurrentId => Current.Id;

        public static PlayerOperatorController Ensure(Transform parent = null)
        {
            if (Instance != null)
                return Instance;
            var found = Object.FindFirstObjectByType<PlayerOperatorController>(FindObjectsInactive.Include);
            if (found != null)
            {
                Instance = found;
                return found;
            }
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<PlayerOperatorController>();
        }

        void OnEnable() => Instance = this;
        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            string id = PlayerPrefs.GetString(PrefsKey, OperatorArchetype.Catalog[0].Id);
            Apply(OperatorArchetype.Find(id), refillKit: true);
        }

        public void SelectByIndex(int index, bool refillKit = true)
        {
            if (index < 0 || index >= OperatorArchetype.Catalog.Length)
                index = 0;
            Apply(OperatorArchetype.Catalog[index], refillKit);
        }

        public void SelectById(string id, bool refillKit = true)
        {
            Apply(OperatorArchetype.Find(id), refillKit);
        }

        public void Apply(OperatorArchetype arch, bool refillKit)
        {
            if (arch == null)
                arch = OperatorArchetype.Catalog[0];
            _current = arch;
            PlayerPrefs.SetString(PrefsKey, arch.Id);
            PlayerPrefs.Save();

            var desktop = DesktopPlayerController.Instance;
            if (desktop != null)
                desktop.ApplyLocomotionStats(arch.MoveSpeed, arch.SprintMult);

            var vitals = PlayerVitality.Ensure();
            vitals.ConfigureMaxHp(arch.MaxHp, refill: true);

            var carry = PlayerCarryInventory.Ensure();
            carry.Resize(arch.InventorySlots);
            if (refillKit)
                carry.ApplyStartingKit(arch.StartingKit);

            ApplyBodyTint(arch);
            Debug.Log("QHYSICS Operator: " + arch.DisplayName + " (" + arch.Id + ")");
        }

        static void ApplyBodyTint(OperatorArchetype arch)
        {
            Transform origin = null;
            var desktop = DesktopPlayerController.Instance;
            if (desktop != null)
                origin = desktop.Origin;
            if (origin == null)
            {
                var go = GameObject.Find(LabPlayerSpawnCompat.OriginName);
                origin = go != null ? go.transform : null;
            }
            if (origin == null)
                return;
            Transform body = origin.Find(QhysicsDesktopBootstrap.BodyName);
            if (body == null)
                return;

            TintNamed(body, "Torso", arch.BodyTint);
            TintNamed(body, QhysicsDesktopBootstrap.LeftHandName, arch.ArmTint);
            TintNamed(body, QhysicsDesktopBootstrap.RightHandName, arch.ArmTint);
            TintChildNamed(body, QhysicsDesktopBootstrap.LeftHandName, "Arm", arch.ArmTint);
            TintChildNamed(body, QhysicsDesktopBootstrap.LeftHandName, "Forearm", arch.ArmTint);
            TintChildNamed(body, QhysicsDesktopBootstrap.RightHandName, "Arm", arch.ArmTint);
            TintChildNamed(body, QhysicsDesktopBootstrap.RightHandName, "Forearm", arch.ArmTint);
        }

        static void TintNamed(Transform root, string child, Color c)
        {
            Transform t = root.Find(child);
            if (t == null) return;
            TintRenderer(t.GetComponent<Renderer>(), c);
        }

        static void TintChildNamed(Transform root, string hand, string child, Color c)
        {
            Transform h = root.Find(hand);
            if (h == null) return;
            Transform t = h.Find(child);
            if (t == null) return;
            TintRenderer(t.GetComponent<Renderer>(), c);
        }

        static void TintRenderer(Renderer r, Color c)
        {
            if (r == null) return;
            // Shared material instance per renderer is OK at apply-time (not per-frame).
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            r.sharedMaterial = mat;
        }
    }
}
