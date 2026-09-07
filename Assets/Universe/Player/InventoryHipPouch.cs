using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>
    /// Worn hip pouch under HipAnchor: belt + bag mesh, plus a tiny cube for the
    /// currently selected QhysicsInventory slot (hidden on Empty/Delete).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(133)]
    public sealed class InventoryHipPouch : MonoBehaviour
    {
        public const string RootName = "HipPouch";
        public const string BeltName = "Belt";
        public const string BagName = "Bag";
        public const string IconName = "SlotIcon";

        Transform _icon;
        Renderer _iconRenderer;
        Material _iconMat;
        int _lastSlot = int.MinValue;

        /// <summary>Build or refresh pouch mesh under HipAnchor; returns the root component.</summary>
        public static InventoryHipPouch Ensure(Transform hipAnchor)
        {
            if (hipAnchor == null)
                return null;

            Transform root = hipAnchor.Find(RootName);
            InventoryHipPouch pouch;
            if (root == null)
            {
                var go = new GameObject(RootName);
                go.transform.SetParent(hipAnchor, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                root = go.transform;
                pouch = go.AddComponent<InventoryHipPouch>();
                pouch.BuildMeshes(root);
            }
            else
            {
                pouch = root.GetComponent<InventoryHipPouch>();
                if (pouch == null)
                    pouch = root.gameObject.AddComponent<InventoryHipPouch>();
                if (root.Find(BeltName) == null || root.Find(BagName) == null)
                    pouch.BuildMeshes(root);
                pouch.CacheIcon(root);
            }

            pouch.ForceRefresh();
            return pouch;
        }

        void Awake()
        {
            CacheIcon(transform);
        }

        void LateUpdate()
        {
            // Only meaningful on desktop body path; XR hides all body renderers via DesktopBodyVisibility.
            var desktop = DesktopPlayerController.Instance;
            bool desktopOn = desktop != null && desktop.IsDesktopActive
                && !DesktopPlayerController.IsXrDisplayRunning();
            if (!desktopOn)
            {
                if (_icon != null && _icon.gameObject.activeSelf)
                    _icon.gameObject.SetActive(false);
                _lastSlot = int.MinValue;
                return;
            }

            int slot = 0;
            var inv = QhysicsInventory.Instance;
            if (inv != null)
                slot = inv.SelectedIndex;
            else
                slot = (int)QhysicsInventory.SlotId.Empty;

            if (slot == _lastSlot)
                return;
            ApplySlot(slot);
        }

        public void ForceRefresh()
        {
            _lastSlot = int.MinValue;
            var inv = QhysicsInventory.Instance;
            int slot = inv != null ? inv.SelectedIndex : (int)QhysicsInventory.SlotId.Empty;
            ApplySlot(slot);
        }

        void BuildMeshes(Transform root)
        {
            // Thin belt strip around the lower torso (HipAnchor sits chest-forward).
            Transform belt = root.Find(BeltName);
            if (belt == null)
            {
                var beltGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beltGo.name = BeltName;
                beltGo.transform.SetParent(root, false);
                Object.Destroy(beltGo.GetComponent<Collider>());
                Tint(beltGo, new Color(0.12f, 0.13f, 0.15f));
                belt = beltGo.transform;
            }
            belt.localPosition = new Vector3(0f, -0.02f, -0.02f);
            belt.localRotation = Quaternion.identity;
            belt.localScale = new Vector3(0.42f, 0.03f, 0.08f);

            // Worn pouch bag hanging slightly right/front so it reads when looking down.
            Transform bag = root.Find(BagName);
            if (bag == null)
            {
                var bagGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bagGo.name = BagName;
                bagGo.transform.SetParent(root, false);
                Object.Destroy(bagGo.GetComponent<Collider>());
                Tint(bagGo, new Color(0.22f, 0.18f, 0.14f));
                bag = bagGo.transform;
            }
            bag.localPosition = new Vector3(0.14f, -0.08f, 0.02f);
            bag.localRotation = Quaternion.Euler(8f, -12f, 6f);
            bag.localScale = new Vector3(0.10f, 0.12f, 0.06f);

            // Flap accent on top of bag.
            Transform flap = bag.Find("Flap");
            if (flap == null)
            {
                var flapGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flapGo.name = "Flap";
                flapGo.transform.SetParent(bag, false);
                Object.Destroy(flapGo.GetComponent<Collider>());
                Tint(flapGo, new Color(0.16f, 0.14f, 0.11f));
                flap = flapGo.transform;
            }
            // Parent is already scaled; keep flap as a thin top lid in bag-local space.
            flap.localPosition = new Vector3(0f, 0.42f, 0.15f);
            flap.localRotation = Quaternion.Euler(18f, 0f, 0f);
            flap.localScale = new Vector3(1.05f, 0.18f, 0.7f);

            Transform icon = bag.Find(IconName);
            if (icon == null)
            {
                var iconGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                iconGo.name = IconName;
                iconGo.transform.SetParent(bag, false);
                Object.Destroy(iconGo.GetComponent<Collider>());
                icon = iconGo.transform;
            }
            // Sits proud of the pouch face (bag-local).
            icon.localPosition = new Vector3(0f, 0.05f, 0.55f);
            icon.localRotation = Quaternion.identity;
            icon.localScale = new Vector3(0.45f, 0.45f, 0.2f);

            CacheIcon(root);
            if (_iconMat == null && _iconRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _iconMat = new Material(sh);
                _iconRenderer.sharedMaterial = _iconMat;
            }
        }

        void CacheIcon(Transform root)
        {
            if (root == null)
                return;
            Transform bag = root.Find(BagName);
            if (bag == null)
                return;
            _icon = bag.Find(IconName);
            if (_icon != null)
                _iconRenderer = _icon.GetComponent<Renderer>();
        }

        void ApplySlot(int slotIndex)
        {
            _lastSlot = slotIndex;
            if (_icon == null)
                CacheIcon(transform);
            if (_icon == null)
                return;

            // Empty / Delete (last label) — clear the pouch face.
            bool empty = slotIndex < 0
                || slotIndex >= QhysicsInventory.SlotLabels.Length
                || slotIndex == (int)QhysicsInventory.SlotId.Empty
                || QhysicsInventory.SlotLabels[slotIndex] == "Delete";

            if (empty)
            {
                if (_icon.gameObject.activeSelf)
                    _icon.gameObject.SetActive(false);
                return;
            }

            if (!_icon.gameObject.activeSelf)
                _icon.gameObject.SetActive(true);

            string label = QhysicsInventory.SlotLabels[slotIndex];
            Color c = ColorForLabel(label);
            if (_iconMat == null && _iconRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _iconMat = new Material(sh);
                _iconRenderer.sharedMaterial = _iconMat;
            }
            if (_iconMat != null)
            {
                _iconMat.color = c;
                if (_iconMat.HasProperty("_BaseColor"))
                    _iconMat.SetColor("_BaseColor", c);
            }
            // Slight shape cue per family so the pouch reads at a glance (label-based = slot-order safe).
            if (_icon != null)
                _icon.localScale = ScaleForLabel(label);
        }

        static Vector3 ScaleForLabel(string label)
        {
            if (label == "Wire") return new Vector3(0.55f, 0.18f, 0.18f);
            if (label == "Battery") return new Vector3(0.28f, 0.55f, 0.28f);
            if (label == "Bulb") return new Vector3(0.4f, 0.4f, 0.4f);
            if (label == "Field Lens") return new Vector3(0.5f, 0.5f, 0.12f);
            if (label == "Cubit Rod") return new Vector3(0.22f, 0.55f, 0.22f);
            return new Vector3(0.45f, 0.45f, 0.2f);
        }

        static Color ColorForLabel(string label)
        {
            switch (label)
            {
                case "Wire": return new Color(0.85f, 0.55f, 0.18f);
                case "Battery": return new Color(0.25f, 0.85f, 0.40f);
                case "Switch": return new Color(0.95f, 0.82f, 0.25f);
                case "Bulb": return new Color(0.95f, 0.92f, 0.75f);
                case "Resistor": return new Color(0.75f, 0.35f, 0.18f);
                case "Magnet": return new Color(0.85f, 0.22f, 0.28f);
                case "Field Lens": return new Color(0.25f, 0.85f, 0.95f);
                case "Cubit Rod": return new Color(0.55f, 0.35f, 0.90f);
                default: return new Color(0.4f, 0.4f, 0.4f);
            }
        }

        static void Tint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null)
                return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
            r.sharedMaterial = mat;
        }
    }
}
