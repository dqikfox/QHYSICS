using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using RealityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Center-screen ray grab for desktop: LMB/E grab XR grabables or CircuitLab parts; while held LMB/scroll activate; F soft-drop / R throw.
    /// Held props parent to HandAttach; colliders disabled while held to avoid yanking through geometry.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(131)]
    public sealed class DesktopInteractor : MonoBehaviour
    {
        [SerializeField] float maxDistance = 4.5f;
        [SerializeField] float holdDistance = 1.15f;
        [SerializeField] float throwForce = 6.5f;
        [SerializeField] float softDropDownSpeed = 0.35f;
        [SerializeField] LayerMask rayMask = ~0;

        Camera _cam;
        Transform _holdPoint;
        Rigidbody _heldRb;
        Transform _held;
        XRGrabInteractable _heldGrab;
        Collider[] _heldCols;
        bool[] _heldColWasEnabled;
        bool _heldWasKinematic;
        bool _heldUsedGravity;
        Renderer _hoverRenderer;
        Color _hoverBaseEmission;
        bool _hoverHadEmission;
        MaterialPropertyBlock _mpb;
        Vector3 _holdLocalPos;
        Quaternion _holdLocalRot = Quaternion.identity;
        Vector3 _prevHoldWorld;
        Vector3 _holdVelocity;
        bool _haveHoldSample;

        public Transform Held => _held;
        public Transform HoverTarget { get; private set; }

        /// <summary>True when held prop exposes desktop LMB/scroll activate (hotbar scroll yields).</summary>
        public bool HoldsActivatable =>
            _held != null && _held.GetComponentInParent<IDesktopActivatable>() != null;

        /// <summary>Drop without throw — used by New Run before clearing spawned gadgets.</summary>
        public void ReleaseHeld()
        {
            Drop(false);
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            // Never raycast own head/self layer (PlayerSelf = 31).
            rayMask &= ~(1 << QhysicsDesktopBootstrap.PlayerSelfLayer);
            EnsureHoldPoint();
        }

        void Update()
        {
            var desktop = DesktopPlayerController.Instance;
            if (desktop == null || !desktop.IsDesktopActive)
            {
                if (_held != null)
                    Drop(false);
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
                int activateDelta = ReadActivateDelta();
                if (activateDelta != 0)
                    TryActivateHeld(activateDelta);
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
            // Prefer character right-hand attach so held props follow the player body.
            Transform attach = QhysicsDesktopBootstrap.FindHandAttach();
            if (attach != null)
            {
                _holdPoint = attach;
                return;
            }
            if (_holdPoint != null)
                return;
            var go = new GameObject("DesktopHoldPoint");
            go.transform.SetParent(transform, false);
            _holdPoint = go.transform;
        }

        void KeepHeldInFront()
        {
            if (_held == null || _cam == null)
                return;
            EnsureHoldPoint();
            if (_holdPoint == null)
                return;

            // Drive right-hand proxy to hold pose; prop sticks via parenting to HandAttach.
            Vector3 target = _cam.transform.position + _cam.transform.forward * holdDistance
                             + _cam.transform.right * 0.08f
                             + _cam.transform.up * -0.06f;
            Quaternion rot = _cam.transform.rotation;

            Transform hand = _holdPoint;
            if (_holdPoint.parent != null && _holdPoint.parent.name == QhysicsDesktopBootstrap.RightHandName)
                hand = _holdPoint.parent;
            hand.position = target;
            hand.rotation = rot;

            // Track hold velocity for throw (camera/hand sweep), not yank through walls.
            if (_haveHoldSample)
            {
                float dt = Time.deltaTime;
                if (dt > 1e-5f)
                {
                    Vector3 raw = (target - _prevHoldWorld) / dt;
                    _holdVelocity = Vector3.Lerp(_holdVelocity, raw, 1f - Mathf.Exp(-18f * dt));
                }
            }
            _prevHoldWorld = target;
            _haveHoldSample = true;

            // Stick cleanly: keep local grip pose; kinematic RB does not need MovePosition.
            if (_held.parent == _holdPoint)
            {
                _held.localPosition = _holdLocalPos;
                _held.localRotation = _holdLocalRot;
                if (_heldRb != null)
                {
                    _heldRb.linearVelocity = Vector3.zero;
                    _heldRb.angularVelocity = Vector3.zero;
                }
            }
            else if (_heldRb != null && !_heldRb.isKinematic)
            {
                _heldRb.linearVelocity = Vector3.zero;
                _heldRb.angularVelocity = Vector3.zero;
                _heldRb.MovePosition(target);
                _heldRb.MoveRotation(rot);
            }
            else
            {
                _held.position = target;
                _held.rotation = rot;
            }
        }

        static bool IsGrabTarget(Collider col)
        {
            if (col == null)
                return false;
            Transform t = col.transform;
            if (LabPlayerSpawnCompat.IsMonumentTransform(t))
                return false;
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
            EnsureHoldPoint();
            if (_holdPoint == null)
                return;

            // Do not steal dispenser shelf template still parented under Dispenser
            if (root.parent != null && root.parent.GetComponent<Dispenser>() != null)
                return;

            _held = root;
            _heldGrab = grab;
            _heldRb = root.GetComponent<Rigidbody>();
            CacheAndDisableColliders(root);
            if (_heldRb != null)
            {
                _heldWasKinematic = _heldRb.isKinematic;
                _heldUsedGravity = _heldRb.useGravity;
                _heldRb.isKinematic = true;
                _heldRb.useGravity = false;
                _heldRb.linearVelocity = Vector3.zero;
                _heldRb.angularVelocity = Vector3.zero;
            }

            // Parent to HandAttach and zero grip so the prop sticks to the hand (no world yank offset).
            root.SetParent(_holdPoint, false);
            _holdLocalPos = Vector3.zero;
            _holdLocalRot = Quaternion.identity;
            root.localPosition = _holdLocalPos;
            root.localRotation = _holdLocalRot;
            _haveHoldSample = false;
            _holdVelocity = Vector3.zero;
        }

        void CacheAndDisableColliders(Transform root)
        {
            _heldCols = root.GetComponentsInChildren<Collider>(true);
            _heldColWasEnabled = new bool[_heldCols.Length];
            for (int i = 0; i < _heldCols.Length; i++)
            {
                Collider c = _heldCols[i];
                if (c == null)
                {
                    _heldColWasEnabled[i] = false;
                    continue;
                }
                _heldColWasEnabled[i] = c.enabled;
                // Disable solid colliders so hold cannot yank/push through plaza / table / player CC.
                if (!c.isTrigger)
                    c.enabled = false;
            }
        }

        void RestoreColliders()
        {
            if (_heldCols == null)
                return;
            for (int i = 0; i < _heldCols.Length; i++)
            {
                Collider c = _heldCols[i];
                if (c == null)
                    continue;
                c.enabled = _heldColWasEnabled != null && i < _heldColWasEnabled.Length && _heldColWasEnabled[i];
            }
            _heldCols = null;
            _heldColWasEnabled = null;
        }

        void Drop(bool throwIt)
        {
            if (_held == null)
                return;
            Transform t = _held;
            Vector3 releasePos = t.position;
            Quaternion releaseRot = t.rotation;
            Vector3 throwVel = Vector3.zero;

            if (throwIt && _cam != null)
            {
                throwVel = _cam.transform.forward * throwForce;
                // Blend recent hand sweep so a flick throws harder.
                throwVel += Vector3.ClampMagnitude(_holdVelocity, throwForce * 1.25f) * 0.45f;
                var desktop = DesktopPlayerController.Instance;
                if (desktop != null && desktop.Origin != null)
                {
                    var cc = desktop.Origin.GetComponent<CharacterController>();
                    if (cc != null)
                        throwVel += cc.velocity;
                }
            }

            t.SetParent(null, true);
            t.position = releasePos;
            t.rotation = releaseRot;
            RestoreColliders();

            if (_heldRb != null)
            {
                if (throwIt)
                {
                    _heldRb.isKinematic = false;
                    _heldRb.useGravity = true;
                    _heldRb.linearVelocity = throwVel;
                    _heldRb.angularVelocity = _cam != null
                        ? _cam.transform.right * (throwForce * 0.15f)
                        : Vector3.zero;
                }
                else
                {
                    // Soft drop: restore gravity, gentle settle — no launch.
                    _heldRb.isKinematic = false;
                    _heldRb.useGravity = true;
                    _heldRb.linearVelocity = Vector3.down * softDropDownSpeed;
                    _heldRb.angularVelocity = Vector3.zero;
                }
            }

            _held = null;
            _heldGrab = null;
            _heldRb = null;
            _haveHoldSample = false;
            _holdVelocity = Vector3.zero;
        }

        void TryDelete(Collider col)
        {
            if (col == null)
                return;
            if (LabPlayerSpawnCompat.IsMonumentTransform(col.transform))
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
            {
                HoverTarget = (col != null && IsGrabTarget(col)) ? col.transform : null;
                return;
            }
            ClearHover();
            _hoverRenderer = next;
            HoverTarget = (col != null && IsGrabTarget(col)) ? col.transform : null;
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
            HoverTarget = null;
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

        void TryActivateHeld(int delta)
        {
            if (_held == null || delta == 0)
                return;
            var act = _held.GetComponentInParent<IDesktopActivatable>();
            if (act != null)
                act.DesktopActivate(delta);
        }

        static int ReadActivateDelta()
        {
            // LMB = +1 while holding (desktop "trigger"); scroll = +/-1.
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return 1;
            if (Mouse.current != null)
            {
                float y = Mouse.current.scroll.ReadValue().y;
                if (y > 0.1f) return 1;
                if (y < -0.1f) return -1;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
                return 1;
            float s = Input.mouseScrollDelta.y;
            if (s > 0.1f) return 1;
            if (s < -0.1f) return -1;
#endif
            return 0;
        }

        void OnGUI()
        {
            var desktop = DesktopPlayerController.Instance;
            if (desktop == null || !desktop.IsDesktopActive)
                return;
            // Simple center reticle ? grab cyan / hold amber / idle white.
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color c = Color.white;
            if (_held != null)
                c = new Color(1f, 0.75f, 0.25f, 0.9f);
            else if (HoverTarget != null)
                c = new Color(0.35f, 0.9f, 1f, 0.95f);
            var prev = GUI.color;
            GUI.color = c;
            const float arm = 7f;
            const float gap = 3f;
            const float thick = 2f;
            GUI.DrawTexture(new Rect(cx - arm, cy - thick * 0.5f, arm - gap, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - thick * 0.5f, arm - gap, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy - arm, thick, arm - gap), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy + gap, thick, arm - gap), Texture2D.whiteTexture);
            GUI.color = prev;
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
