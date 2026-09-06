using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Center-screen ray grab for desktop: LMB/E grab XR grabables or CircuitLab parts; F/R drop/throw.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(131)]
    public sealed class DesktopInteractor : MonoBehaviour
    {
        [SerializeField] float maxDistance = 4.5f;
        [SerializeField] float holdDistance = 1.15f;
        [SerializeField] float throwForce = 4.5f;
        [SerializeField] LayerMask rayMask = ~0;

        Camera _cam;
        Transform _holdPoint;
        Rigidbody _heldRb;
        Transform _held;
        XRGrabInteractable _heldGrab;
        Collider _heldCol;
        bool _heldWasKinematic;
        bool _heldUsedGravity;
        Renderer _hoverRenderer;
        Color _hoverBaseEmission;
        bool _hoverHadEmission;
        MaterialPropertyBlock _mpb;

        public Transform Held => _held;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            EnsureHoldPoint();
        }

        void Update()
        {
            var desktop = DesktopPlayerController.Instance;
            if (desktop == null || !desktop.IsDesktopActive)
            {
                ClearHover();
                return;
            }

            var pause = Object.FindFirstObjectByType<QhysicsPausePanel>(FindObjectsInactive.Include);
            if (pause != null && pause.IsOpen)
            {
                ClearHover();
                return;
            }

            ResolveCamera();
            if (_cam == null)
                return;

            EnsureHoldPoint();
            if (_held != null)
                KeepHeldInFront();

            Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            bool hitSomething = UnityEngine.Physics.Raycast(ray, out RaycastHit hit, maxDistance, rayMask, QueryTriggerInteraction.Ignore);

            bool deleteMode = QhysicsInventory.Instance != null
                && QhysicsInventory.Instance.SelectedSlot == QhysicsInventory.SlotId.Empty;

            if (_held == null)
            {
                UpdateHover(hitSomething ? hit.collider : null);
                if (!deleteMode && WasGrabPressed() && hitSomething)
                    TryGrab(hit.collider);
            }
            else
            {
                ClearHover();
                if (WasDropPressed())
                    Drop(false);
                else if (WasThrowPressed())
                    Drop(true);
            }

            // LMB while tool selected (not Empty/Delete): spawn via gadgets when not hitting a grabable
            if (WasPrimaryClick() && _held == null && QhysicsInventory.Instance != null)
            {
                var slot = QhysicsInventory.Instance.SelectedSlot;
                if (slot != QhysicsInventory.SlotId.Empty)
                {
                    if (!hitSomething || !IsGrabTarget(hit.collider))
                        QhysicsGadgets.SpawnSelected(ray.GetPoint(holdDistance));
                }
                else if (slot == QhysicsInventory.SlotId.Empty && hitSomething)
                    TryDelete(hit.collider);
            }
        }

        void ResolveCamera()
        {
            if (_cam != null)
                return;
            var desktop = DesktopPlayerController.Instance;
            if (desktop != null && desktop.MainCamera != null)
                _cam = desktop.MainCamera.GetComponent<Camera>();
            if (_cam == null)
                _cam = Camera.main;
        }

        void EnsureHoldPoint()
        {
            if (_holdPoint != null)
                return;
            var go = new GameObject("DesktopHoldPoint");
            go.transform.SetParent(transform, false);
            _holdPoint = go.transform;
        }

        void KeepHeldInFront()
        {
            if (_cam == null || _held == null)
                return;
            Vector3 target = _cam.transform.position + _cam.transform.forward * holdDistance;
            _holdPoint.position = target;
            _holdPoint.rotation = _cam.transform.rotation;
            if (_heldRb != null && !_heldRb.isKinematic)
            {
                _heldRb.linearVelocity = Vector3.zero;
                _heldRb.angularVelocity = Vector3.zero;
                _heldRb.MovePosition(target);
                _heldRb.MoveRotation(_holdPoint.rotation);
            }
            else
            {
                _held.position = target;
                _held.rotation = _holdPoint.rotation;
            }
        }

        static bool IsGrabTarget(Collider col)
        {
            if (col == null)
                return false;
            Transform t = col.transform;
            if (t.GetComponentInParent<XRGrabInteractable>() != null)
                return true;
            if (t.GetComponentInParent<CircuitComponent>() != null)
                return true;
            if (t.GetComponentInParent<RealityEngine.Survey.CubitRod>() != null)
                return true;
            if (t.GetComponentInParent<FieldLensHandheld>() != null)
                return true;
            if (t.GetComponentInParent<Rigidbody>() != null && t.CompareTag("Untagged") == false)
                return true;
            string n = t.name;
            if (n == null)
                return false;
            return n.IndexOf("Component", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Gadget_", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("CubitRod", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void TryGrab(Collider col)
        {
            if (col == null)
                return;
            Transform root = col.transform;
            var grab = col.GetComponentInParent<XRGrabInteractable>();
            if (grab != null)
                root = grab.transform;
            else
            {
                var cc = col.GetComponentInParent<CircuitComponent>();
                if (cc != null)
                    root = cc.transform;
                else
                {
                    var rb = col.GetComponentInParent<Rigidbody>();
                    if (rb != null)
                        root = rb.transform;
                }
            }

            // Never grab Giza / pyramids / mastabas (would look like "a pyramid moves").
            if (LabPlayerSpawnCompat.IsMonumentTransform(root))
                return;

            // Do not steal dispenser shelf template still parented under Dispenser
            if (root.parent != null && root.parent.GetComponent<Dispenser>() != null)
                return;

            _held = root;
            _heldGrab = grab;
            _heldRb = root.GetComponent<Rigidbody>();
            _heldCol = col;
            if (_heldRb != null)
            {
                _heldWasKinematic = _heldRb.isKinematic;
                _heldUsedGravity = _heldRb.useGravity;
                _heldRb.isKinematic = true;
                _heldRb.useGravity = false;
            }
            root.SetParent(_holdPoint, true);
        }

        void Drop(bool throwIt)
        {
            if (_held == null)
                return;
            Transform t = _held;
            t.SetParent(null, true);
            if (_heldRb != null)
            {
                _heldRb.isKinematic = throwIt ? false : _heldWasKinematic;
                _heldRb.useGravity = throwIt ? true : _heldUsedGravity;
                if (throwIt && _cam != null)
                {
                    _heldRb.isKinematic = false;
                    _heldRb.useGravity = true;
                    _heldRb.linearVelocity = _cam.transform.forward * throwForce;
                }
            }
            _held = null;
            _heldGrab = null;
            _heldRb = null;
            _heldCol = null;
        }

        void TryDelete(Collider col)
        {
            if (col == null)
                return;
            var cc = col.GetComponentInParent<CircuitComponent>();
            if (cc == null)
                return;
            if (cc.transform.parent != null && cc.transform.parent.GetComponent<Dispenser>() != null)
                return;
            Object.Destroy(cc.gameObject);
        }

        void UpdateHover(Collider col)
        {
            Renderer next = null;
            if (col != null && IsGrabTarget(col))
            {
                var r = col.GetComponentInParent<Renderer>();
                if (r == null)
                    r = col.GetComponentInChildren<Renderer>();
                next = r;
            }
            if (next == _hoverRenderer)
                return;
            ClearHover();
            _hoverRenderer = next;
            if (_hoverRenderer == null)
                return;
            var mat = _hoverRenderer.sharedMaterial;
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                _hoverHadEmission = mat.IsKeywordEnabled("_EMISSION");
                _hoverBaseEmission = mat.GetColor("_EmissionColor");
                _hoverRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", new Color(0.15f, 0.55f, 0.7f) * 1.4f);
                _hoverRenderer.SetPropertyBlock(_mpb);
                _hoverRenderer.material.EnableKeyword("_EMISSION");
            }
            else if (_hoverRenderer != null)
            {
                _hoverRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", new Color(0.55f, 0.9f, 1f, 1f));
                _hoverRenderer.SetPropertyBlock(_mpb);
            }
        }

        void ClearHover()
        {
            if (_hoverRenderer == null)
                return;
            _hoverRenderer.SetPropertyBlock(null);
            if (_hoverRenderer.sharedMaterial != null && _hoverRenderer.sharedMaterial.HasProperty("_EmissionColor"))
            {
                if (!_hoverHadEmission)
                    _hoverRenderer.material.DisableKeyword("_EMISSION");
                else
                {
                    _hoverRenderer.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_EmissionColor", _hoverBaseEmission);
                    _hoverRenderer.SetPropertyBlock(_mpb);
                }
            }
            _hoverRenderer = null;
        }

        static bool WasGrabPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
                return true;
#endif
            return false;
        }

        static bool WasPrimaryClick()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
                return true;
#endif
            return false;
        }

        static bool WasDropPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1))
                return true;
#endif
            return false;
        }

        static bool WasThrowPressed()
        {
            // Same as drop with momentum Ã¢â‚¬â€ F/R already throws when held; treat R as throw preference.
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.R))
                return true;
#endif
            return false;
        }
    }
}

