using System;
using UnityEngine;
using UnityEngine.XR;

namespace RealityEngine.Player
{
    /// <summary>
    /// Carry inventory (slots) alongside the BUILD hotbar (<see cref="QhysicsInventory"/>).
    /// Pickup → slot; equip combat tools to hand; drop / use consumables.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(134)]
    public sealed class PlayerCarryInventory : MonoBehaviour
    {
        public const string RootName = "PlayerCarryInventory";

        public static PlayerCarryInventory Instance { get; private set; }

        CarryItemId[] _slots = Array.Empty<CarryItemId>();
        int _selected;
        int _equippedSlot = -1;
        GameObject _equippedVisual;
        bool _xrTrackedEquip;

        public int SlotCount => _slots.Length;
        public int SelectedIndex => _selected;
        public int EquippedSlot => _equippedSlot;

        public CarryItemId EquippedItem =>
            _equippedSlot >= 0 && _equippedSlot < _slots.Length ? _slots[_equippedSlot] : CarryItemId.None;

        public event Action Changed;

        public static PlayerCarryInventory Ensure(Transform parent = null)
        {
            if (Instance != null)
                return Instance;
            var found = UnityEngine.Object.FindFirstObjectByType<PlayerCarryInventory>(FindObjectsInactive.Include);
            if (found != null)
            {
                Instance = found;
                return found;
            }
            var go = new GameObject(RootName);
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<PlayerCarryInventory>();
        }

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Awake()
        {
            if (_slots == null || _slots.Length == 0)
                Resize(6);
        }

        void LateUpdate()
        {
            if (_equippedVisual == null || !_xrTrackedEquip)
                return;
            if (!DesktopPlayerController.IsXrDisplayRunning())
                return;
            if (!TryControllerPose(XRNode.RightHand, out Vector3 pos, out Quaternion rot)
                && !TryControllerPose(XRNode.LeftHand, out pos, out rot))
                return;
            _equippedVisual.transform.SetPositionAndRotation(
                pos + rot * new Vector3(0f, 0f, 0.12f),
                rot * Quaternion.Euler(90f, 0f, 0f));
        }

        public void Resize(int slots)
        {
            slots = Mathf.Clamp(slots, 3, 12);
            var next = new CarryItemId[slots];
            if (_slots != null)
            {
                int n = Mathf.Min(_slots.Length, slots);
                for (int i = 0; i < n; i++)
                    next[i] = _slots[i];
            }
            _slots = next;
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _slots.Length - 1));
            if (_equippedSlot >= _slots.Length)
                ClearEquip();
            Changed?.Invoke();
        }

        public CarryItemId GetSlot(int i)
        {
            if (i < 0 || i >= _slots.Length)
                return CarryItemId.None;
            return _slots[i];
        }

        public void Select(int index)
        {
            if (_slots.Length == 0)
                return;
            _selected = Mathf.Clamp(index, 0, _slots.Length - 1);
            Changed?.Invoke();
        }

        public void SelectDelta(int delta)
        {
            if (_slots.Length == 0)
                return;
            Select((_selected + delta + _slots.Length) % _slots.Length);
        }

        public bool TryAdd(CarryItemId id)
        {
            if (id == CarryItemId.None)
                return false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == CarryItemId.None)
                {
                    _slots[i] = id;
                    Changed?.Invoke();
                    return true;
                }
            }
            return false;
        }

        public void ApplyStartingKit(CarryItemId[] kit)
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = CarryItemId.None;
            ClearEquip();
            if (kit != null)
            {
                for (int i = 0; i < kit.Length && i < _slots.Length; i++)
                    _slots[i] = kit[i];
            }
            Changed?.Invoke();
        }

        public bool TryDropSelected(out CarryItemId dropped, out Vector3 worldPos)
        {
            dropped = CarryItemId.None;
            worldPos = Vector3.zero;
            if (_slots.Length == 0)
                return false;
            CarryItemId id = _slots[_selected];
            if (id == CarryItemId.None)
                return false;
            if (_equippedSlot == _selected)
                ClearEquip();
            _slots[_selected] = CarryItemId.None;
            dropped = id;
            worldPos = ResolveDropPose();
            Changed?.Invoke();
            return true;
        }

        public bool TryUseOrEquipSelected()
        {
            if (_slots.Length == 0)
                return false;
            CarryItemId id = _slots[_selected];
            if (!CarryItemCatalog.TryGet(id, out var def))
                return false;

            var vitals = global::RealityEngine.Player.PlayerVitality.Instance;
            if (def.IsConsumable)
            {
                if (def.HealAmount > 0f && vitals != null)
                    vitals.Heal(def.HealAmount);
                if (def.ShieldSeconds > 0f && vitals != null)
                    vitals.ApplyShield(def.ShieldSeconds);
                _slots[_selected] = CarryItemId.None;
                if (_equippedSlot == _selected)
                    ClearEquip();
                Changed?.Invoke();
                return true;
            }

            if (def.IsCombatTool)
            {
                EquipSelected();
                return true;
            }

            return false;
        }

        public void EquipSelected()
        {
            if (_slots.Length == 0)
                return;
            CarryItemId id = _slots[_selected];
            if (id == CarryItemId.None)
            {
                ClearEquip();
                return;
            }
            if (!CarryItemCatalog.TryGet(id, out var def) || !def.IsCombatTool)
                return;
            _equippedSlot = _selected;
            RebuildEquipVisual(def);
            Changed?.Invoke();
        }

        public void ClearEquip()
        {
            _equippedSlot = -1;
            _xrTrackedEquip = false;
            if (_equippedVisual != null)
            {
                if (Application.isPlaying) Destroy(_equippedVisual);
                else DestroyImmediate(_equippedVisual);
                _equippedVisual = null;
            }
        }

        void RebuildEquipVisual(CarryItemCatalog.Def def)
        {
            if (_equippedVisual != null)
            {
                if (Application.isPlaying) Destroy(_equippedVisual);
                else DestroyImmediate(_equippedVisual);
                _equippedVisual = null;
            }

            bool xr = DesktopPlayerController.IsXrDisplayRunning();
            Transform attach = null;
            if (!xr)
                attach = QhysicsDesktopBootstrap.FindHandAttach();

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Equipped_Baton";
            if (attach != null)
            {
                go.transform.SetParent(attach, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.12f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _xrTrackedEquip = false;
            }
            else
            {
                // XR: unparented; LateUpdate tracks controller pose.
                go.transform.SetParent(null, true);
                _xrTrackedEquip = true;
            }
            go.transform.localScale = new Vector3(0.03f, 0.18f, 0.03f);
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            Tint(go, def.Color);
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            tip.transform.localScale = new Vector3(1.4f, 0.35f, 1.4f);
            Destroy(tip.GetComponent<Collider>());
            Tint(tip, new Color(1f, 0.85f, 0.35f));
            _equippedVisual = go;
        }

        static bool TryControllerPose(XRNode node, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                return false;
            return true;
        }

        static Vector3 ResolveDropPose()
        {
            var desktop = DesktopPlayerController.Instance;
            Transform cam = desktop != null ? desktop.MainCamera : null;
            if (cam == null && Camera.main != null)
                cam = Camera.main.transform;
            if (cam != null)
                return cam.position + cam.forward * 1.1f + Vector3.up * 0.1f;
            if (desktop != null && desktop.Origin != null)
                return desktop.Origin.position + desktop.Origin.forward * 1.0f + Vector3.up * 0.9f;
            return Vector3.up;
        }

        static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            r.sharedMaterial = mat;
        }
    }
}
