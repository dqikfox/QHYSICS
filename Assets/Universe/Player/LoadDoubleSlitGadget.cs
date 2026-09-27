using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS Young double-slit: far-field Fraunhofer I = I0 cos^2(pi*d*sin(theta)/lambda).
    /// Honesty: monochromatic equal-slit interference — NOT Fresnel near-field, not single-slit envelope,
    /// not polarization, not finite source width, not quantum path amplitudes.
    /// XR activate / N cycles slit spacing d; Shift+N/P cycles wavelength. Desktop via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadDoubleSlitGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_DoubleSlit";
        public const string Honesty =
            "Far-field Fraunhofer two-slit I=I0 cos^2(pi d sin(theta)/lambda). NOT Fresnel, not single-slit envelope, not polarization, not finite source, not QM amplitudes.";

        // Slit separation presets (m) — lab-scale Young slits
        static readonly float[] PresetsD = { 0.0002f, 0.0005f, 0.0010f, 0.0020f };
        static readonly string[] PresetDLabels = { "d0.2mm", "d0.5mm", "d1.0mm", "d2.0mm" };

        // Wavelength presets (m)
        static readonly float[] PresetsLambda = { 450e-9f, 532e-9f, 650e-9f };
        static readonly string[] PresetLambdaLabels = { "BLUE", "GREEN", "RED" };
        static readonly Color[] LambdaColors =
        {
            new Color(0.35f, 0.55f, 1.00f, 1f),
            new Color(0.25f, 0.95f, 0.45f, 1f),
            new Color(1.00f, 0.28f, 0.28f, 1f),
        };

        const float ScreenDistanceM = 1.00f; // physical L used in fringe formula
        const float ScreenHalfWidthM = 0.08f; // visual screen half-width (m, local X)
        const int FringeBarCount = 25;
        const float BarrierLocalZ = 0.00f;
        const float ScreenLocalZ = 0.32f;
        const float SourceLocalZ = -0.10f;

        TextMeshPro _readout;
        Transform _base;
        Transform _barrier;
        Transform _slitA;
        Transform _slitB;
        Transform _source;
        Transform _screen;
        Transform[] _bars;
        XRGrabInteractable _grab;
        Material _baseMat;
        Material _barrierMat;
        Material _slitMat;
        Material _sourceMat;
        Material _screenMat;
        Material _barMat;
        float _refreshAt;
        float _inputCooldown;
        int _dIndex = 1;
        int _lambdaIndex = 1;

        public float ActiveD => PresetsD[Mathf.Clamp(_dIndex, 0, PresetsD.Length - 1)];
        public float ActiveLambda => PresetsLambda[Mathf.Clamp(_lambdaIndex, 0, PresetsLambda.Length - 1)];
        public string ActiveDLabel => PresetDLabels[Mathf.Clamp(_dIndex, 0, PresetDLabels.Length - 1)];
        public string ActiveLambdaLabel => PresetLambdaLabels[Mathf.Clamp(_lambdaIndex, 0, PresetLambdaLabels.Length - 1)];
        public float FringeSpacingM => ActiveD > 1e-12f ? ActiveLambda * ScreenDistanceM / ActiveD : 0f;
        public float ScreenDistance => ScreenDistanceM;

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            ApplyWavelengthLook();
            RefreshFringes();
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
            DestroyMat(ref _baseMat);
            DestroyMat(ref _barrierMat);
            DestroyMat(ref _slitMat);
            DestroyMat(ref _sourceMat);
            DestroyMat(ref _screenMat);
            DestroyMat(ref _barMat);
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
            RefreshFringes();
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
            CycleD(+1);
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
                if (shift) CycleLambda(+1);
                else CycleD(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleLambda(-1);
                else CycleD(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleD(int delta)
        {
            if (PresetsD.Length == 0)
                return;
            _dIndex = (_dIndex + delta) % PresetsD.Length;
            if (_dIndex < 0)
                _dIndex += PresetsD.Length;
            PlaceSlits();
            RefreshFringes();
            RefreshText();
        }

        public void CycleLambda(int delta)
        {
            if (PresetsLambda.Length == 0)
                return;
            _lambdaIndex = (_lambdaIndex + delta) % PresetsLambda.Length;
            if (_lambdaIndex < 0)
                _lambdaIndex += PresetsLambda.Length;
            ApplyWavelengthLook();
            RefreshFringes();
            RefreshText();
        }

        public void CyclePreset(int delta) => CycleD(delta);

        public void DesktopActivate(int delta)
        {
            CycleD(delta == 0 ? 1 : delta);
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _base = EnsurePrimitive("Base", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0.12f),
                new Vector3(0.22f, 0.03f, 0.48f), Quaternion.identity);
            _baseMat = EnsureMat(ref _baseMat, sh, new Color(0.10f, 0.12f, 0.14f, 1f), 0.4f, 0.45f);
            ApplyMat(_base, _baseMat);

            _barrier = EnsurePrimitive("Barrier", PrimitiveType.Cube, new Vector3(0f, 0.11f, BarrierLocalZ),
                new Vector3(0.18f, 0.18f, 0.012f), Quaternion.identity);
            _barrierMat = EnsureMat(ref _barrierMat, sh, new Color(0.08f, 0.10f, 0.12f, 1f), 0.2f, 0.35f);
            ApplyMat(_barrier, _barrierMat);

            _slitA = EnsurePrimitive("SlitA", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.006f, 0.12f, 0.014f), Quaternion.identity);
            _slitB = EnsurePrimitive("SlitB", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.006f, 0.12f, 0.014f), Quaternion.identity);
            _slitMat = EnsureMat(ref _slitMat, sh, new Color(0.2f, 0.85f, 0.95f, 1f), 0.1f, 0.7f, emit: 0.6f);
            ApplyMat(_slitA, _slitMat);
            ApplyMat(_slitB, _slitMat);
            PlaceSlits();

            _source = EnsurePrimitive("Source", PrimitiveType.Sphere, new Vector3(0f, 0.11f, SourceLocalZ),
                new Vector3(0.04f, 0.04f, 0.04f), Quaternion.identity);
            _sourceMat = EnsureMat(ref _sourceMat, sh, LambdaColors[_lambdaIndex], 0.05f, 0.8f, emit: 1.2f);
            ApplyMat(_source, _sourceMat);

            _screen = EnsurePrimitive("Screen", PrimitiveType.Cube, new Vector3(0f, 0.11f, ScreenLocalZ),
                new Vector3(0.20f, 0.18f, 0.01f), Quaternion.identity);
            _screenMat = EnsureMat(ref _screenMat, sh, new Color(0.06f, 0.07f, 0.09f, 1f), 0.05f, 0.25f);
            ApplyMat(_screen, _screenMat);

            if (_bars == null || _bars.Length != FringeBarCount)
                _bars = new Transform[FringeBarCount];
            _barMat = EnsureMat(ref _barMat, sh, LambdaColors[_lambdaIndex], 0.05f, 0.75f, emit: 0.9f);
            for (int i = 0; i < FringeBarCount; i++)
            {
                string name = "Bar" + i.ToString("00");
                Transform bar = EnsurePrimitive(name, PrimitiveType.Cube, Vector3.zero,
                    new Vector3(0.006f, 0.01f, 0.008f), Quaternion.identity);
                ApplyMat(bar, _barMat);
                _bars[i] = bar;
            }
        }

        void PlaceSlits()
        {
            // Visual slit separation compressed from physical d into barrier width.
            float visSep = Mathf.Lerp(0.018f, 0.070f, Mathf.InverseLerp(PresetsD[0], PresetsD[PresetsD.Length - 1], ActiveD));
            if (_slitA != null)
                _slitA.localPosition = new Vector3(-visSep * 0.5f, 0.11f, BarrierLocalZ);
            if (_slitB != null)
                _slitB.localPosition = new Vector3(+visSep * 0.5f, 0.11f, BarrierLocalZ);
        }

        void ApplyWavelengthLook()
        {
            Color c = LambdaColors[Mathf.Clamp(_lambdaIndex, 0, LambdaColors.Length - 1)];
            SetMatColor(_sourceMat, c, emit: 1.2f);
            SetMatColor(_barMat, c, emit: 0.9f);
            SetMatColor(_slitMat, Color.Lerp(c, new Color(0.2f, 0.85f, 0.95f), 0.35f), emit: 0.55f);
        }

        void RefreshFringes()
        {
            if (_bars == null)
                return;
            float d = ActiveD;
            float lambda = ActiveLambda;
            float L = ScreenDistanceM;
            float dy = FringeSpacingM;
            for (int i = 0; i < _bars.Length; i++)
            {
                Transform bar = _bars[i];
                if (bar == null)
                    continue;
                float t = (_bars.Length <= 1) ? 0f : (i / (float)(_bars.Length - 1)) * 2f - 1f; // -1..+1
                float xLocal = t * ScreenHalfWidthM;
                // Map visual x to physical screen height (~±4.5 fringe spacings at default)
                float yPhys = t * Mathf.Max(dy * 4.5f, 0.001f);
                float sinTheta = yPhys / Mathf.Sqrt(yPhys * yPhys + L * L);
                float phase = (Mathf.PI * d * sinTheta) / Mathf.Max(lambda, 1e-15f);
                float cosP = Mathf.Cos(phase);
                float intensity = cosP * cosP;
                bar.localPosition = new Vector3(xLocal, 0.11f, ScreenLocalZ - 0.008f);
                bar.localScale = new Vector3(0.0065f, 0.02f + 0.15f * intensity, 0.008f);
            }
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
                go.transform.localPosition = new Vector3(0f, 0.28f, 0.10f);
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
            _readout.rectTransform.sizeDelta = new Vector2(48f, 28f);
            _readout.text = "DOUBLE SLIT\nidle";
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
            float dyMm = FringeSpacingM * 1000f;
            float dMm = ActiveD * 1000f;
            float lamNm = ActiveLambda * 1e9f;
            _readout.text =
                "SLIT " + ActiveDLabel + " / " + ActiveLambdaLabel + "\n"
                + "d=" + dMm.ToString("0.00") + "mm  L=" + ScreenDistanceM.ToString("0.00") + "m\n"
                + "lambda=" + lamNm.ToString("0") + "nm\n"
                + "dy=lambda*L/d=" + dyMm.ToString("0.00") + "mm\n"
                + "N/P d  Shift lambda\n"
                + "[Fraunhofer 2-slit]";
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
