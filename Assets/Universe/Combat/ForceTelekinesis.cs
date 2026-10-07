using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using RealityEngine.Audio;
using RealityEngine.Player;

namespace RealityEngine.Combat
{
    /// <summary>
    /// Blade &amp; Sorcery-style Force telekinesis: while Force is charged, lock onto a loose weapon,
    /// free rigidbody, or enemy body part and drag it with a laggy PD drive. Release flings it with
    /// your hand velocity; a short tap still uses SpellSystem ForcePull.
    /// </summary>
    [DefaultExecutionOrder(153)]
    public sealed class ForceTelekinesis : MonoBehaviour
    {
        public static ForceTelekinesis Instance { get; private set; }

        public static bool IsHolding => Instance != null && Instance._rb != null;

        const float AcquireRange = 9f;
        const float AcquireAngle = 28f;
        const float HoldDistance = 0.85f;
        const float ManaPerSecond = 7f;
        const float AcquireMana = 10f;

        Rigidbody _rb;
        PhysicsWeapon _weapon;
        CombatEnemy _enemy;
        XRNode _node;
        Vector3 _handPos, _handPrev, _handVel, _handDir;
        Quaternion _handRot = Quaternion.identity;
        float _holdDist = HoldDistance;
        LineRenderer _tether;
        GameObject _tetherGo;
        readonly List<(Collider a, Collider b)> _ignored = new List<(Collider, Collider)>(32);
        readonly List<Collider> _playerCols = new List<Collider>(16);
        float _nextManaTick;
        bool _handInit;

        public static ForceTelekinesis Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("QhysicsForceTelekinesis");
            Instance = go.AddComponent<ForceTelekinesis>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearIgnore();
            if (_tetherGo != null) Destroy(_tetherGo);
        }

        /// <summary>Call each frame while Force is charging. Acquires at charge &gt;= 0.35, then holds.</summary>
        public static void TickWhileCharging(Vector3 handPos, Quaternion handRot, Vector3 aimDir, float charge01, XRNode node)
        {
            var tk = Ensure();
            if (!tk._handInit)
            {
                tk._handPos = handPos;
                tk._handPrev = handPos;
                tk._handInit = true;
            }
            else
            {
                tk._handPrev = tk._handPos;
                tk._handPos = handPos;
            }
            tk._handRot = handRot;
            tk._handDir = aimDir.sqrMagnitude > 1e-4f ? aimDir.normalized : handRot * Vector3.forward;
            float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
            tk._handVel = (tk._handPos - tk._handPrev) / dt;
            tk._node = node;

            if (tk._rb == null)
            {
                if (charge01 >= 0.35f)
                    tk.TryAcquire();
                return;
            }

            if (Time.unscaledTime >= tk._nextManaTick)
            {
                tk._nextManaTick = Time.unscaledTime + 0.25f;
                if (!ManaPool.TrySpend(ManaPerSecond * 0.25f))
                {
                    tk.Drop(false);
                    QhysicsSfx.Play2D(SfxId.Denied, 0.4f);
                }
            }
            tk.UpdateTether();
        }

        public static void Release(Vector3 handVel, bool fling)
        {
            if (Instance == null || Instance._rb == null) return;
            Instance.Drop(fling, handVel);
        }

        public static void Cancel()
        {
            if (Instance != null && Instance._rb != null)
                Instance.Drop(false);
            if (Instance != null) Instance._handInit = false;
        }

        void TryAcquire()
        {
            Rigidbody bestRb = null;
            PhysicsWeapon bestW = null;
            float bestScore = float.MaxValue;

            foreach (var w in PhysicsWeapon.All)
            {
                if (w == null || w.Body == null || w.IsHeld || w.ownerTeam == CombatTeam.Enemy) continue;
                if (!Score(w.Body.worldCenterOfMass, out float score)) continue;
                if (score < bestScore) { bestScore = score; bestRb = w.Body; bestW = w; }
            }

            if (bestRb == null)
            {
                Vector3 origin = _handPos;
                Vector3 dir = _handDir;
                if (UnityEngine.Physics.SphereCast(origin, 0.12f, dir, out RaycastHit hit, AcquireRange, ~0, QueryTriggerInteraction.Ignore)
                    && !IsPlayerOwned(hit.collider))
                {
                    var rb = hit.collider.attachedRigidbody;
                    if (rb != null && !rb.isKinematic && Score(rb.worldCenterOfMass, out float sc))
                    {
                        bestScore = sc;
                        bestRb = rb;
                        bestW = rb.GetComponentInParent<PhysicsWeapon>();
                    }
                }
            }

            if (bestRb == null)
            {
                var cols = UnityEngine.Physics.OverlapSphere(_handPos + _handDir * 2.5f, 2.2f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in cols)
                {
                    if (IsPlayerOwned(c)) continue;
                    var rb = c.attachedRigidbody;
                    if (rb == null || rb.isKinematic) continue;
                    var w = rb.GetComponentInParent<PhysicsWeapon>();
                    if (w != null && (w.IsHeld || w.ownerTeam == CombatTeam.Enemy)) continue;
                    if (!Score(rb.worldCenterOfMass, out float sc)) continue;
                    if (sc < bestScore) { bestScore = sc; bestRb = rb; bestW = w; }
                }
            }

            if (bestRb == null)
                return;

            if (!ManaPool.TrySpend(AcquireMana))
                return;

            _rb = bestRb;
            _weapon = bestW;
            if (_weapon != null && _weapon.IsStuck) _weapon.Unstick();
            if (_weapon != null) _weapon.MarkPlayerHold();

            _enemy = null;
            var part = bestRb.GetComponent<BodyPart>();
            if (part != null && part.health != null)
                _enemy = part.health.GetComponent<CombatEnemy>();
            if (_enemy != null) _enemy.OnForceLifted();

            float dist = Vector3.Distance(_handPos, _rb.worldCenterOfMass);
            _holdDist = Mathf.Clamp(dist, 0.45f, HoldDistance);

            IgnorePlayer(true);
            EnsureTether();
            QhysicsSfx.PlayAt(SfxId.ForcePush, _rb.worldCenterOfMass, 0.55f, 1.5f);
            LightningVfx.Bolt(_handPos, _rb.worldCenterOfMass, new Color(0.75f, 0.55f, 1f, 0.85f), 0.18f);
            CombatHaptics.Pulse(_node, 0.45f, 0.07f);
            _nextManaTick = Time.unscaledTime + 0.25f;
        }

        bool Score(Vector3 point, out float score)
        {
            Vector3 to = point - _handPos;
            float dist = to.magnitude;
            score = float.MaxValue;
            if (dist < 0.25f || dist > AcquireRange) return false;
            float ang = Vector3.Angle(_handDir, to);
            if (ang > AcquireAngle) return false;
            score = ang * 0.25f + dist;
            return true;
        }

        void FixedUpdate()
        {
            if (_rb == null || !_rb.gameObject.activeInHierarchy) { if (_rb != null) Drop(false); return; }

            Vector3 target = _handPos + _handDir * _holdDist;
            float mass = Mathf.Clamp(_rb.mass, 0.3f, 8f);
            float force = Mathf.Lerp(380f, 160f, Mathf.InverseLerp(0.3f, 8f, mass));
            float torque = Mathf.Lerp(40f, 12f, Mathf.InverseLerp(0.3f, 8f, mass));
            float tauPos = Mathf.Lerp(0.05f, 0.14f, Mathf.InverseLerp(0.3f, 8f, mass));

            Quaternion face = _weapon != null ? AimWeaponRot() : Quaternion.LookRotation(
                Vector3.Slerp(_rb.rotation * Vector3.forward, _handDir, 0.35f), Vector3.up);

            PhysicsHands.Drive(_rb, target, face, _handVel * 0.65f, force, torque, tauPos, 0.08f);
        }

        Quaternion AimWeaponRot()
        {
            Vector3 tipDir = _handDir;
            Vector3 right = _handRot * Vector3.right;
            Vector3 up = Vector3.Cross(tipDir, right);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.up;
            return Quaternion.LookRotation(Vector3.Cross(up.normalized, tipDir), tipDir);
        }

        void Drop(bool fling, Vector3 handVel = default)
        {
            if (_rb == null) return;
            var rb = _rb;
            if (fling)
            {
                Vector3 v = handVel.sqrMagnitude > 0.01f ? handVel : _handVel;
                float boost = Mathf.Lerp(1.4f, 0.7f, Mathf.InverseLerp(0.3f, 8f, rb.mass)) * SkillSystem.ThrowMult;
                rb.linearVelocity = Vector3.ClampMagnitude(v * boost, 28f);
                rb.angularVelocity += Random.insideUnitSphere * 2f;
                QhysicsSfx.PlayAt(SfxId.ForcePush, rb.worldCenterOfMass, 0.7f, 0.85f);
                CombatHaptics.Pulse(_node, 0.55f, 0.06f);
            }
            if (_weapon != null) _weapon.MarkPlayerHold();
            ClearIgnore();
            _rb = null;
            _weapon = null;
            _enemy = null;
            _handInit = false;
            if (_tetherGo != null) _tetherGo.SetActive(false);
        }

        void UpdateTether()
        {
            if (_tether == null || _rb == null) return;
            _tetherGo.SetActive(true);
            _tether.SetPosition(0, _handPos);
            _tether.SetPosition(1, _rb.worldCenterOfMass);
        }

        void EnsureTether()
        {
            if (_tetherGo != null) { _tetherGo.SetActive(true); return; }
            _tetherGo = new GameObject("ForceTether");
            _tether = _tetherGo.AddComponent<LineRenderer>();
            _tether.positionCount = 2;
            _tether.startWidth = 0.012f;
            _tether.endWidth = 0.004f;
            _tether.sharedMaterial = CombatMaterials.UnlitLine(new Color(0.75f, 0.5f, 1f, 0.85f));
            _tether.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tether.receiveShadows = false;
        }

        void IgnorePlayer(bool ignore)
        {
            if (_rb == null) return;
            if (ignore)
            {
                _ignored.Clear();
                _playerCols.Clear();
                var origin = GameObject.Find("XR Origin");
                if (origin != null)
                    origin.GetComponentsInChildren(true, _playerCols);
                var desk = DesktopPlayerController.Instance;
                if (desk != null && desk.Origin != null && (origin == null || desk.Origin.gameObject != origin))
                    _playerCols.AddRange(desk.Origin.GetComponentsInChildren<Collider>(true));
                var hurt = PlayerHurtbox.Instance != null ? PlayerHurtbox.Instance.Collider : null;
                if (hurt != null && !_playerCols.Contains(hurt))
                    _playerCols.Add(hurt);
            }

            var cols = _rb.GetComponentsInChildren<Collider>();
            foreach (var c in cols)
            {
                if (c == null) continue;
                foreach (var p in _playerCols)
                {
                    if (p == null) continue;
                    UnityEngine.Physics.IgnoreCollision(c, p, ignore);
                    if (ignore) _ignored.Add((c, p));
                }
            }
            if (!ignore) _ignored.Clear();
        }

        void ClearIgnore()
        {
            foreach (var pair in _ignored)
                if (pair.a != null && pair.b != null)
                    UnityEngine.Physics.IgnoreCollision(pair.a, pair.b, false);
            _ignored.Clear();
        }

        static bool IsPlayerOwned(Collider c)
        {
            if (CombatInputGate.IsPlayerCollider(c)) return true;
            var w = c.GetComponentInParent<PhysicsWeapon>();
            return w != null && w.HeldByPlayer;
        }
    }
}
