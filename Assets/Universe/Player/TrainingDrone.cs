using UnityEngine;
using TMPro;

namespace RealityEngine.Player
{
    /// <summary>
    /// Floating plaza training drone — soft HP, optional soft projectile, hit flash + chip.
    /// Labelled as Training System (not fantasy combat).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingDrone : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHp = 80f;
        [SerializeField] float hp = 80f;
        [SerializeField] float bobAmp = 0.25f;
        [SerializeField] float bobSpeed = 1.4f;
        [SerializeField] float softDamage = 8f;
        [SerializeField] float fireInterval = 2.4f;
        [SerializeField] bool canFire = true;

        Vector3 _home;
        float _phase;
        float _nextFire;
        float _flashUntil;
        Renderer _bodyRenderer;
        MaterialPropertyBlock _mpb;
        TextMeshPro _label;
        TextMeshPro _hpChip;
        Color _baseColor = new Color(0.85f, 0.35f, 0.25f);

        public bool IsAlive => hp > 0.01f && isActiveAndEnabled;

        void Awake()
        {
            _home = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _mpb = new MaterialPropertyBlock();
            _bodyRenderer = GetComponentInChildren<Renderer>();
            EnsureLabels();
        }

        void Update()
        {
            if (!IsAlive)
                return;

            float y = Mathf.Sin((Time.time + _phase) * bobSpeed) * bobAmp;
            transform.position = _home + Vector3.up * y;

            // Face player loosely
            var desktop = DesktopPlayerController.Instance;
            Transform target = desktop != null && desktop.Origin != null ? desktop.Origin
                : (Camera.main != null ? Camera.main.transform : null);
            if (target != null)
            {
                Vector3 to = target.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 3f);
            }

            if (_flashUntil > 0f && Time.time > _flashUntil)
            {
                _flashUntil = 0f;
                ApplyFlash(false);
            }

            if (canFire && Time.time >= _nextFire)
            {
                _nextFire = Time.time + fireInterval;
                TryFireSoft(target);
            }

            if (_hpChip != null)
                _hpChip.text = Mathf.CeilToInt(hp) + "/" + Mathf.CeilToInt(maxHp);
        }

        void TryFireSoft(Transform target)
        {
            if (target == null)
                return;
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > 12f || dist < 1.5f)
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TrainingPulse";
            go.transform.position = transform.position + transform.forward * 0.4f;
            go.transform.localScale = Vector3.one * 0.12f;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            Tint(go, new Color(1f, 0.45f, 0.25f));
            var pulse = go.AddComponent<TrainingSoftProjectile>();
            pulse.Init(target, softDamage, 6.5f);
            UnityEngine.Object.Destroy(go, 4f);
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!IsAlive)
                return;
            hp = Mathf.Max(0f, hp - amount);
            _flashUntil = Time.time + 0.22f;
            ApplyFlash(true);
            SpawnHitChip(hitPoint, amount);
            if (hp <= 0.01f)
                OnDisabled();
        }

        void OnDisabled()
        {
            // Soft disable — respawn after delay (training lane).
            gameObject.SetActive(false);
            Invoke(nameof(Respawn), 6f);
        }

        void Respawn()
        {
            hp = maxHp;
            transform.position = _home;
            gameObject.SetActive(true);
            ApplyFlash(false);
        }

        void ApplyFlash(bool on)
        {
            if (_bodyRenderer == null)
                _bodyRenderer = GetComponentInChildren<Renderer>();
            if (_bodyRenderer == null || _mpb == null)
                return;
            if (!on)
            {
                _bodyRenderer.SetPropertyBlock(null);
                return;
            }
            _bodyRenderer.GetPropertyBlock(_mpb);
            Color glow = Color.white;
            if (_bodyRenderer.sharedMaterial != null && _bodyRenderer.sharedMaterial.HasProperty("_EmissionColor"))
                _mpb.SetColor("_EmissionColor", glow * 3.2f);
            if (_bodyRenderer.sharedMaterial != null && _bodyRenderer.sharedMaterial.HasProperty("_BaseColor"))
                _mpb.SetColor("_BaseColor", glow);
            _bodyRenderer.SetPropertyBlock(_mpb);
        }

        void SpawnHitChip(Vector3 pos, float amount)
        {
            var go = new GameObject("HitChip");
            go.transform.position = pos + Vector3.up * 0.2f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "-" + Mathf.RoundToInt(amount);
            tmp.fontSize = 4.2f;
            tmp.color = new Color(1f, 0.85f, 0.3f);
            tmp.alignment = TextAlignmentOptions.Center;
            go.AddComponent<TrainingHitChip>();
            UnityEngine.Object.Destroy(go, 1.05f);
        }

        void EnsureLabels()
        {
            if (_label == null)
            {
                var go = new GameObject("Label");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                _label = go.AddComponent<TextMeshPro>();
                _label.text = "TRAINING DRONE";
                _label.fontSize = 2.2f;
                _label.color = new Color(0f, 0.9f, 1f);
                _label.alignment = TextAlignmentOptions.Center;
            }
            if (_hpChip == null)
            {
                var go = new GameObject("HpChip");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.38f, 0f);
                _hpChip = go.AddComponent<TextMeshPro>();
                _hpChip.fontSize = 2f;
                _hpChip.color = Color.white;
                _hpChip.alignment = TextAlignmentOptions.Center;
            }
        }

        public static TrainingDrone Spawn(Vector3 pos, Transform parent = null)
        {
            var root = new GameObject("TrainingDrone");
            if (parent != null)
                root.transform.SetParent(parent, true);
            root.transform.position = pos;

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 0.55f;
            Tint(body, new Color(0.85f, 0.35f, 0.25f));

            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(body.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0.1f, 0.4f);
            eye.transform.localScale = Vector3.one * 0.35f;
            UnityEngine.Object.Destroy(eye.GetComponent<Collider>());
            Tint(eye, new Color(0f, 0.9f, 1f));

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var drone = root.AddComponent<TrainingDrone>();
            drone._home = pos;
            drone._bodyRenderer = body.GetComponent<Renderer>();
            return drone;
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
                mat.SetColor("_EmissionColor", c * 0.4f);
            }
            r.sharedMaterial = mat;
        }
    }

    sealed class TrainingSoftProjectile : MonoBehaviour
    {
        Transform _target;
        float _damage;
        float _speed;
        Vector3 _vel;

        public void Init(Transform target, float damage, float speed)
        {
            _target = target;
            _damage = damage;
            _speed = speed;
            if (target != null)
                _vel = (target.position + Vector3.up * 1.2f - transform.position).normalized * speed;
        }

        void Update()
        {
            if (_target != null)
            {
                Vector3 want = (_target.position + Vector3.up * 1.2f - transform.position).normalized * _speed;
                _vel = Vector3.Lerp(_vel, want, Time.deltaTime * 2f);
            }
            transform.position += _vel * Time.deltaTime;

            var vitals = PlayerVitality.Instance;
            if (vitals == null || _target == null)
                return;
            if (Vector3.Distance(transform.position, _target.position + Vector3.up * 1.2f) < 0.45f)
            {
                vitals.ApplyDamage(_damage, "training pulse");
                Destroy(gameObject);
            }
        }
    }

    sealed class TrainingHitChip : MonoBehaviour
    {
        float _age;

        void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (Time.deltaTime * 0.95f);
            Camera cam = Camera.main;
            if (cam == null)
            {
                var desktop = DesktopPlayerController.Instance;
                if (desktop != null && desktop.MainCamera != null)
                    cam = desktop.MainCamera.GetComponent<Camera>();
            }
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            // Fade via TMP alpha
            var tmp = GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                Color c = tmp.color;
                c.a = Mathf.Clamp01(1.05f - _age);
                tmp.color = c;
            }
        }
    }
}
