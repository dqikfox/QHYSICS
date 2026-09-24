using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>
    /// World pickup implementing <see cref="IQhysicsInteractable"/>.
    /// PropertyBlock hover highlight — never assigns .material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QhysicsWorldPickup : MonoBehaviour, IQhysicsInteractable
    {
        [SerializeField] CarryItemId itemId = CarryItemId.HealthAmpoule;
        [SerializeField] bool destroyOnPickup = true;

        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        bool _hovered;
        Color _baseColor = Color.white;

        public CarryItemId ItemId
        {
            get => itemId;
            set => itemId = value;
        }

        public string InteractLabel => CarryItemCatalog.LabelOf(itemId) + "  [E]";

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColor = CarryItemCatalog.ColorOf(itemId);
        }

        public bool CanInteract(GameObject actor) => itemId != CarryItemId.None && isActiveAndEnabled;

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor))
                return;
            var inv = PlayerCarryInventory.Ensure();
            if (!inv.TryAdd(itemId))
            {
                Debug.Log("Carry inventory full — cannot pick up " + CarryItemCatalog.LabelOf(itemId));
                return;
            }
            if (destroyOnPickup)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
            else
                gameObject.SetActive(false);
        }

        public void SetHover(bool hovered)
        {
            if (_hovered == hovered)
                return;
            _hovered = hovered;
            if (_renderers == null)
                _renderers = GetComponentsInChildren<Renderer>(true);
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                if (!hovered)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                r.GetPropertyBlock(_mpb);
                Color glow = Color.Lerp(_baseColor, new Color(0f, 0.9f, 1f), 0.45f);
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_EmissionColor"))
                    _mpb.SetColor("_EmissionColor", glow * 1.6f);
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                    _mpb.SetColor("_BaseColor", glow);
                else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                    _mpb.SetColor("_Color", glow);
                r.SetPropertyBlock(_mpb);
            }
        }

        public static QhysicsWorldPickup Spawn(CarryItemId id, Vector3 pos, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pickup_" + id;
            if (parent != null)
                go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = ScaleFor(id);
            Tint(go, CarryItemCatalog.ColorOf(id));
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var pickup = go.AddComponent<QhysicsWorldPickup>();
            pickup.itemId = id;
            pickup._baseColor = CarryItemCatalog.ColorOf(id);
            return pickup;
        }

        static Vector3 ScaleFor(CarryItemId id)
        {
            switch (id)
            {
                case CarryItemId.TrainingBaton: return new Vector3(0.04f, 0.28f, 0.04f);
                case CarryItemId.HealthAmpoule: return new Vector3(0.06f, 0.12f, 0.06f);
                case CarryItemId.BatteryPack: return new Vector3(0.10f, 0.06f, 0.14f);
                case CarryItemId.ProbeTip: return new Vector3(0.05f, 0.05f, 0.16f);
                case CarryItemId.ShieldCell: return new Vector3(0.10f, 0.10f, 0.04f);
                default: return Vector3.one * 0.1f;
            }
        }

        static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.35f);
            }
            r.sharedMaterial = mat;
        }
    }
}
