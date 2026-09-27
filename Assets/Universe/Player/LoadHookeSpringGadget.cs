using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Hooke spring: ideal static F = -k x, PE = 0.5 k x^2 at hanging equilibrium x = m g / k.
    /// Honesty: ideal Hooke + static equilibrium — NOT oscillator dynamics, not continuum elasticity, not nonlinear spring.
    /// Distinct from Spring Mass (dynamic m x''=-k x - c x'). XR activate / N cycles k; Shift+N/P cycles mass.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadHookeSpringGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_HookeSpring";
        public const string Honesty =
            "Ideal Hooke F=-kx at static hanging eq x=mg/k. NOT oscillator dynamics, not continuum elasticity, not nonlinear spring.";

        // Stiffness presets (N/m) — labels SMALL/MED/LARGE/XL (Spring Mass uses SOFT/STIFF)
        static readonly float[] PresetsK = { 10f, 40f, 160f, 640f };
        static readonly string[] PresetKLabels = { "SMALL", "MED", "LARGE", "XL" };

        // Hanging mass presets (kg)
        static readonly float[] PresetsM = { 0.10f, 0.25f, 0.50f, 1.00f };
        static readonly string[] PresetMLabels = { "m0.10", "m0.25", "m0.50", "m1.00" };

        const float G = 9.81f;
        const float RestLen = 0.14f;
        const float AnchorLocalY = 0.34f;
        const float MaxVisualX = 0.22f;
        const int CoilSegments = 8;

        TextMeshPro _readout;
        Transform _stand;
        Transform _post;
        Transform _anchor;
        Transform _massBob;
        Transform[] _coils;
        XRGrabInteractable _grab;
        Material _standMat;
        Material _postMat;
        Material _anchorMat;
        Material _coilMat;
        Material _massMat;
        float _refreshAt;
        float _inputCooldown;
        int _kIndex = 1; // default MED
        int _mIndex = 1; // default 0.25 kg

        public float ActiveK => PresetsK[Mathf.Clamp(_kIndex, 0, PresetsK.Length - 1)];
        public float ActiveMass => PresetsM[Mathf.Clamp(_mIndex, 0, PresetsM.Length - 1)];
        public string ActiveKLabel => PresetKLabels[Mathf.Clamp(_kIndex, 0, PresetKLabels.Length - 1)];
        public string ActiveMLabel => PresetMLabels[Mathf.Clamp(_mIndex, 0, PresetMLabels.Length - 1)];
        public float StretchMeters => ActiveK > 1e-6f ? (ActiveMass * G) / ActiveK : 0f;
        public float ForceNewtons => -ActiveK * StretchMeters; // = -mg at eq
        public float ForceMagnitude => Mathf.Abs(ForceNewtons);
        public float SpringEnergyJoules => 0.5f * ActiveK * StretchMeters * StretchMeters;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            ApplySpringPose();
            ApplyCoilLook();
            RefreshText();
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void OnEnable()
        {
            WireGrab();
        }

        void OnDisable()
        {
            UnwireGrab();
        }

        void OnDestroy()
        {
            UnwireGrab();
            DestroyMat(ref _standMat);
            DestroyMat(ref _postMat);
            DestroyMat(ref _anchorMat);
            DestroyMat(ref _coilMat);
            DestroyMat(ref _massMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(m);
            else UnityEngine.Object.DestroyImmediate(m);
            m = null;
        }

        void Update()
        {
            PollDesktopCycle();
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.08f;
            ApplySpringPose();
            ApplyCoilLook();
            RefreshText();
            FaceReadout();
        }

        void WireGrab()
        {
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }

        void UnwireGrab()
        {
            if (_grab == null)
                return;
            _grab.activated.RemoveListener(OnActivated);
        }

        void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
        {
            CycleK(+1);
        }

        void PollDesktopCycle()
        {
            if (Time.unscaledTime < _inputCooldown)
                return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            if ((cam.transform.position - transform.position).sqrMagnitude > 2.25f)
                return;

            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.nKey.wasPressedThisFrame)
            {
                if (shift) CycleMass(+1);
                else CycleK(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleMass(-1);
                else CycleK(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleK(int delta)
        {
            if (PresetsK.Length == 0)
                return;
            _kIndex = (_kIndex + delta) % PresetsK.Length;
            if (_kIndex < 0)
                _kIndex += PresetsK.Length;
            ApplySpringPose();
            ApplyCoilLook();
            RefreshText();
        }

        public void CycleMass(int delta)
        {
            if (PresetsM.Length == 0)
                return;
            _mIndex = (_mIndex + delta) % PresetsM.Length;
            if (_mIndex < 0)
                _mIndex += PresetsM.Length;
            ApplySpringPose();
            ApplyCoilLook();
            RefreshText();
        }

        public void CyclePreset(int delta) => CycleK(delta);

        public void DesktopActivate(int delta)
        {
            CycleK(delta == 0 ? 1 : delta);
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _stand = EnsurePrimitive("Stand", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f),
                new Vector3(0.16f, 0.04f, 0.16f), Quaternion.identity);
            _standMat = EnsureMat(ref _standMat, sh, new Color(0.12f, 0.14f, 0.16f, 1f), 0.35f, 0.45f);
            ApplyMat(_stand, _standMat);

            _post = EnsurePrimitive("Post", PrimitiveType.Cube, new Vector3(0f, 0.20f, -0.06f),
                new Vector3(0.03f, 0.36f, 0.03f), Quaternion.identity);
            _postMat = EnsureMat(ref _postMat, sh, new Color(0.28f, 0.32f, 0.36f, 1f), 0.5f, 0.5f);
            ApplyMat(_post, _postMat);

            _anchor = EnsurePrimitive("Anchor", PrimitiveType.Cube, new Vector3(0f, AnchorLocalY, 0f),
                new Vector3(0.06f, 0.02f, 0.06f), Quaternion.identity);
            _anchorMat = EnsureMat(ref _anchorMat, sh, new Color(0.55f, 0.58f, 0.62f, 1f), 0.6f, 0.55f);
            ApplyMat(_anchor, _anchorMat);

            if (_coils == null || _coils.Length != CoilSegments)
                _coils = new Transform[CoilSegments];
            _coilMat = EnsureMat(ref _coilMat, sh, new Color(0.25f, 0.85f, 0.75f, 1f), 0.15f, 0.7f, emit: 0.45f);
            for (int i = 0; i < CoilSegments; i++)
            {
                string name = "Coil" + i.ToString("00");
                Transform coil = EnsurePrimitive(name, PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(0.05f, 0.01f, 0.05f), Quaternion.identity);
                ApplyMat(coil, _coilMat);
                _coils[i] = coil;
            }

            _massBob = EnsurePrimitive("Mass", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.07f, 0.07f, 0.07f), Quaternion.identity);
            _massMat = EnsureMat(ref _massMat, sh, new Color(0.85f, 0.40f, 0.28f, 1f), 0.25f, 0.5f, emit: 0.2f);
            ApplyMat(_massBob, _massMat);
        }

        void ApplySpringPose()
        {
            // Visual map stretch into coil length (clamped for lab prop scale)
            float xNorm = Mathf.InverseLerp(0f, 0.25f, StretchMeters);
            float visX = Mathf.Lerp(0.04f, MaxVisualX, Mathf.Clamp01(xNorm));
            float springLen = RestLen + visX;
            float massY = AnchorLocalY - springLen - 0.035f;

            if (_massBob != null)
            {
                _massBob.localPosition = new Vector3(0f, massY, 0f);
                float mScale = Mathf.Lerp(0.055f, 0.095f, Mathf.InverseLerp(PresetsM[0], PresetsM[PresetsM.Length - 1], ActiveMass));
                _massBob.localScale = new Vector3(mScale, mScale, mScale);
            }

            if (_coils == null)
                return;
            for (int i = 0; i < _coils.Length; i++)
            {
                Transform coil = _coils[i];
                if (coil == null)
                    continue;
                float t = (_coils.Length <= 1) ? 0.5f : (i + 0.5f) / _coils.Length;
                float y = AnchorLocalY - t * springLen;
                float r = (i % 2 == 0) ? 0.055f : 0.042f;
                float thick = Mathf.Max(0.006f, springLen / (_coils.Length * 2.2f));
                coil.localPosition = new Vector3(0f, y, 0f);
                coil.localScale = new Vector3(r * 2f, thick, r * 2f);
            }
        }

        void ApplyCoilLook()
        {
            if (_coilMat == null)
                return;
            float fNorm = Mathf.InverseLerp(0f, PresetsM[PresetsM.Length - 1] * G, ForceMagnitude);
            Color c = Color.Lerp(new Color(0.2f, 0.65f, 0.55f, 1f), new Color(0.45f, 1f, 0.85f, 1f), fNorm);
            SetMatColor(_coilMat, c, emit: 0.3f + 0.9f * fNorm);
        }

        void BuildReadout()
        {
            Transform existing = transform.Find("Readout");
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject("Readout");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.48f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 20f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.65f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(48f, 30f);
            _readout.text = "HOOKE\nidle";
        }

        void FaceReadout()
        {
            if (_readout == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector3 toCam = _readout.transform.position - cam.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-6f)
                _readout.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        void RefreshText()
        {
            if (_readout == null)
                return;
            float xCm = StretchMeters * 100f;
            float f = ForceMagnitude;
            float pe = SpringEnergyJoules;
            _readout.text =
                "HOOKE " + ActiveKLabel + " / " + ActiveMLabel + "\n"
                + "k=" + ActiveK.ToString("0") + " N/m\n"
                + "x=mg/k=" + xCm.ToString("0.0") + " cm\n"
                + "|F|=kx=" + f.ToString("0.00") + " N\n"
                + "PE=0.5kx^2=" + pe.ToString("0.000") + " J\n"
                + "N/P k  Shift mass\n"
                + "[ideal Hooke eq]";
        }

        Transform EnsurePrimitive(string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Quaternion localRot)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(transform, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.Destroy(col);
                t = go.transform;
            }
            t.localPosition = localPos;
            t.localRotation = localRot;
            t.localScale = localScale;
            return t;
        }

        static Material EnsureMat(ref Material mat, Shader sh, Color color, float metallic, float smooth, float emit = 0f)
        {
            if (mat == null)
                mat = new Material(sh);
            SetMatColor(mat, color, emit);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smooth);
            return mat;
        }

        static void SetMatColor(Material mat, Color color, float emit = 0f)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_EmissionColor"))
            {
                if (emit > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * emit);
                }
                else
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        static void ApplyMat(Transform t, Material mat)
        {
            if (t == null || mat == null)
                return;
            var r = t.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = mat;
        }
    }
}
