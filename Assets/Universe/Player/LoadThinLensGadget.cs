using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS thin-lens optics bench: Gaussian 1/f = 1/u + 1/v, m = -v/u.
    /// Honesty: ideal thin lens, paraxial — NOT thick lens, not chromatic/spherical aberration, not wave optics.
    /// XR activate / N cycles f presets; P steps backward. Shift+N/P cycles object distance u. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class LoadThinLensGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_ThinLens";
        public const string Honesty =
            "Ideal thin lens 1/f=1/u+1/v (Gaussian), m=-v/u. Paraxial only. NOT thick lens, not chromatic/spherical aberration, not wave optics.";

        // Focal length presets (m). Positive = convex (converging), negative = concave (diverging).
        static readonly float[] PresetsF = { 0.05f, 0.10f, 0.20f, -0.10f };
        static readonly string[] PresetFLabels = { "CONVEX5", "CONVEX10", "CONVEX20", "CONCAVE" };

        // Object distance presets along optic axis (m, Gaussian u > 0).
        static readonly float[] PresetsU = { 0.08f, 0.12f, 0.15f, 0.25f, 0.40f };
        static readonly string[] PresetULabels = { "U8", "U12", "U15", "U25", "U40" };

        const float VisualScale = 0.85f; // compress physical metres into lab prop length
        const float LensLocalY = 0.18f;
        const float MaxVisImage = 0.55f;
        const float ObjectMarkerSize = 0.045f;
        const float ImageBaseSize = 0.045f;

        TextMeshPro _readout;
        Transform _bench;
        Transform _lensDisc;
        Transform _lensRing;
        Transform _axis;
        Transform _objectMarker;
        Transform _imageMarker;
        XRGrabInteractable _grab;
        Material _benchMat;
        Material _lensMat;
        Material _objectMat;
        Material _imageMat;
        float _refreshAt;
        float _inputCooldown;
        int _fIndex = 1; // default CONVEX10
        int _uIndex = 2; // default U15
        float _cachedV;
        float _cachedM;
        bool _realImage;
        bool _finiteImage;

        public float FocalMeters => ActiveF;
        public float ObjectDistanceMeters => ActiveU;
        public float ImageDistanceMeters => _cachedV;
        public float Magnification => _cachedM;
        public bool IsRealImage => _realImage;
        public bool IsFiniteImage => _finiteImage;
        public float ActiveF => PresetsF[Mathf.Clamp(_fIndex, 0, PresetsF.Length - 1)];
        public float ActiveU => PresetsU[Mathf.Clamp(_uIndex, 0, PresetsU.Length - 1)];
        public string ActiveFLabel => PresetFLabels[Mathf.Clamp(_fIndex, 0, PresetFLabels.Length - 1)];
        public string ActiveULabel => PresetULabels[Mathf.Clamp(_uIndex, 0, PresetULabels.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            RecomputeOptics();
            ApplyVisualPose();
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
            DestroyMat(ref _benchMat);
            DestroyMat(ref _lensMat);
            DestroyMat(ref _objectMat);
            DestroyMat(ref _imageMat);
        }

        static void DestroyMat(ref Material m)
        {
            if (m == null)
                return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
            m = null;
        }

        void Update()
        {
            PollDesktopCycle();
            RecomputeOptics();
            ApplyVisualPose();
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.06f;
            RefreshText();
            ApplyGlow();
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
            CycleF(+1);
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
                if (shift) CycleU(+1);
                else CycleF(+1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                if (shift) CycleU(-1);
                else CycleF(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void CycleF(int delta)
        {
            if (PresetsF.Length == 0)
                return;
            _fIndex = (_fIndex + delta) % PresetsF.Length;
            if (_fIndex < 0)
                _fIndex += PresetsF.Length;
            RefreshText();
        }

        public void CycleU(int delta)
        {
            if (PresetsU.Length == 0)
                return;
            _uIndex = (_uIndex + delta) % PresetsU.Length;
            if (_uIndex < 0)
                _uIndex += PresetsU.Length;
            RefreshText();
        }

        public void CyclePreset(int delta) => CycleF(delta);

        public void DesktopActivate(int delta)
        {
            // LMB / scroll / VR activate share f presets; Shift+N/P cycles object distance u.
            CycleF(delta == 0 ? 1 : delta);
        }

        void RecomputeOptics()
        {
            float f = ActiveF;
            float u = ActiveU;
            // Gaussian: 1/v = 1/f - 1/u
            if (Mathf.Abs(f) < 1e-6f || Mathf.Abs(u) < 1e-6f)
            {
                _finiteImage = false;
                _cachedV = float.PositiveInfinity;
                _cachedM = 0f;
                _realImage = false;
                return;
            }

            float invV = (1f / f) - (1f / u);
            if (Mathf.Abs(invV) < 1e-5f)
            {
                _finiteImage = false;
                _cachedV = float.PositiveInfinity;
                _cachedM = 0f;
                _realImage = false;
                return;
            }

            _cachedV = 1f / invV;
            _cachedM = -_cachedV / u;
            _finiteImage = true;
            _realImage = _cachedV > 0f;
        }

        void EnsureVisual()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            _bench = transform.Find("Bench");
            if (_bench == null)
            {
                var benchGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                benchGo.name = "Bench";
                benchGo.transform.SetParent(transform, false);
                benchGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                benchGo.transform.localRotation = Quaternion.identity;
                benchGo.transform.localScale = new Vector3(0.72f, 0.04f, 0.16f);
                Object.Destroy(benchGo.GetComponent<Collider>());
                _bench = benchGo.transform;
            }
            var br = _bench.GetComponent<Renderer>();
            if (br != null)
            {
                if (_benchMat == null)
                {
                    _benchMat = new Material(sh);
                    var dark = new Color(0.12f, 0.14f, 0.16f, 1f);
                    if (_benchMat.HasProperty("_BaseColor"))
                        _benchMat.SetColor("_BaseColor", dark);
                    _benchMat.color = dark;
                    if (_benchMat.HasProperty("_Metallic"))
                        _benchMat.SetFloat("_Metallic", 0.35f);
                    if (_benchMat.HasProperty("_Smoothness"))
                        _benchMat.SetFloat("_Smoothness", 0.45f);
                }
                br.sharedMaterial = _benchMat;
            }

            // Vertical post under lens
            Transform post = transform.Find("Post");
            if (post == null)
            {
                var postGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postGo.name = "Post";
                postGo.transform.SetParent(transform, false);
                postGo.transform.localPosition = new Vector3(0f, LensLocalY * 0.5f, 0f);
                postGo.transform.localRotation = Quaternion.identity;
                postGo.transform.localScale = new Vector3(0.025f, LensLocalY, 0.025f);
                Object.Destroy(postGo.GetComponent<Collider>());
                post = postGo.transform;
            }
            var pr = post.GetComponent<Renderer>();
            if (pr != null && _benchMat != null)
                pr.sharedMaterial = _benchMat;

            _lensDisc = transform.Find("LensDisc");
            if (_lensDisc == null)
            {
                var discGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                discGo.name = "LensDisc";
                discGo.transform.SetParent(transform, false);
                // Cylinder default axis = Y; rotate to face along local X (optic axis)
                discGo.transform.localPosition = new Vector3(0f, LensLocalY, 0f);
                discGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                discGo.transform.localScale = new Vector3(0.14f, 0.008f, 0.14f);
                Object.Destroy(discGo.GetComponent<Collider>());
                _lensDisc = discGo.transform;
            }
            var lr = _lensDisc.GetComponent<Renderer>();
            if (lr != null)
            {
                if (_lensMat == null)
                {
                    _lensMat = new Material(sh);
                    var cyan = new Color(0.35f, 0.85f, 0.95f, 1f);
                    if (_lensMat.HasProperty("_BaseColor"))
                        _lensMat.SetColor("_BaseColor", cyan);
                    _lensMat.color = cyan;
                    if (_lensMat.HasProperty("_Smoothness"))
                        _lensMat.SetFloat("_Smoothness", 0.75f);
                    if (_lensMat.HasProperty("_Metallic"))
                        _lensMat.SetFloat("_Metallic", 0.15f);
                    if (_lensMat.HasProperty("_EmissionColor"))
                    {
                        _lensMat.EnableKeyword("_EMISSION");
                        _lensMat.SetColor("_EmissionColor", cyan * 0.35f);
                    }
                }
                lr.sharedMaterial = _lensMat;
            }

            _lensRing = transform.Find("LensRing");
            if (_lensRing == null)
            {
                var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = "LensRing";
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localPosition = new Vector3(0f, LensLocalY, 0f);
                ringGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                ringGo.transform.localScale = new Vector3(0.16f, 0.004f, 0.16f);
                Object.Destroy(ringGo.GetComponent<Collider>());
                _lensRing = ringGo.transform;
            }
            var rr = _lensRing.GetComponent<Renderer>();
            if (rr != null && _benchMat != null)
                rr.sharedMaterial = _benchMat;

            _axis = transform.Find("Axis");
            if (_axis == null)
            {
                var axisGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                axisGo.name = "Axis";
                axisGo.transform.SetParent(transform, false);
                axisGo.transform.localPosition = new Vector3(0f, LensLocalY, 0f);
                axisGo.transform.localRotation = Quaternion.identity;
                axisGo.transform.localScale = new Vector3(0.70f, 0.004f, 0.004f);
                Object.Destroy(axisGo.GetComponent<Collider>());
                _axis = axisGo.transform;
            }
            var ar = _axis.GetComponent<Renderer>();
            if (ar != null && _lensMat != null)
                ar.sharedMaterial = _lensMat;

            _objectMarker = transform.Find("ObjectMarker");
            if (_objectMarker == null)
            {
                var objGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                objGo.name = "ObjectMarker";
                objGo.transform.SetParent(transform, false);
                objGo.transform.localPosition = new Vector3(-0.15f, LensLocalY, 0f);
                objGo.transform.localRotation = Quaternion.identity;
                objGo.transform.localScale = Vector3.one * ObjectMarkerSize;
                Object.Destroy(objGo.GetComponent<Collider>());
                _objectMarker = objGo.transform;

                // Arrow tip (small pyramid-ish via stretched cube)
                var tipGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tipGo.name = "ArrowTip";
                tipGo.transform.SetParent(_objectMarker, false);
                tipGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                tipGo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                tipGo.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                Object.Destroy(tipGo.GetComponent<Collider>());
            }
            var orr = _objectMarker.GetComponent<Renderer>();
            if (orr != null)
            {
                if (_objectMat == null)
                {
                    _objectMat = new Material(sh);
                    var amber = new Color(0.95f, 0.65f, 0.2f, 1f);
                    if (_objectMat.HasProperty("_BaseColor"))
                        _objectMat.SetColor("_BaseColor", amber);
                    _objectMat.color = amber;
                    if (_objectMat.HasProperty("_EmissionColor"))
                    {
                        _objectMat.EnableKeyword("_EMISSION");
                        _objectMat.SetColor("_EmissionColor", amber * 0.4f);
                    }
                }
                orr.sharedMaterial = _objectMat;
            }
            Transform tip = _objectMarker.Find("ArrowTip");
            if (tip != null)
            {
                var tr = tip.GetComponent<Renderer>();
                if (tr != null && _objectMat != null)
                    tr.sharedMaterial = _objectMat;
            }

            _imageMarker = transform.Find("ImageMarker");
            if (_imageMarker == null)
            {
                var imgGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                imgGo.name = "ImageMarker";
                imgGo.transform.SetParent(transform, false);
                imgGo.transform.localPosition = new Vector3(0.2f, LensLocalY, 0f);
                imgGo.transform.localRotation = Quaternion.identity;
                imgGo.transform.localScale = Vector3.one * ImageBaseSize;
                Object.Destroy(imgGo.GetComponent<Collider>());
                _imageMarker = imgGo.transform;
            }
            var ir = _imageMarker.GetComponent<Renderer>();
            if (ir != null)
            {
                if (_imageMat == null)
                {
                    _imageMat = new Material(sh);
                    // Ghost cyan — slightly emissive, not fully transparent (URP Lit alpha needs surface mode)
                    var ghost = new Color(0.45f, 0.9f, 1f, 1f);
                    if (_imageMat.HasProperty("_BaseColor"))
                        _imageMat.SetColor("_BaseColor", ghost);
                    _imageMat.color = ghost;
                    if (_imageMat.HasProperty("_Smoothness"))
                        _imageMat.SetFloat("_Smoothness", 0.5f);
                    if (_imageMat.HasProperty("_EmissionColor"))
                    {
                        _imageMat.EnableKeyword("_EMISSION");
                        _imageMat.SetColor("_EmissionColor", ghost * 0.55f);
                    }
                }
                ir.sharedMaterial = _imageMat;
            }
        }

        void ApplyVisualPose()
        {
            if (_objectMarker == null || _imageMarker == null)
                return;

            float uVis = Mathf.Max(0.04f, ActiveU * VisualScale);
            // Object sits on -X (left of lens)
            _objectMarker.localPosition = new Vector3(-uVis, LensLocalY, 0f);
            _objectMarker.localScale = Vector3.one * ObjectMarkerSize;
            _objectMarker.localRotation = Quaternion.identity;

            if (!_finiteImage || float.IsInfinity(_cachedV) || float.IsNaN(_cachedV))
            {
                _imageMarker.gameObject.SetActive(false);
                return;
            }

            _imageMarker.gameObject.SetActive(true);
            float vVis = _cachedV * VisualScale;
            // Clamp extreme real/virtual distances into bench length
            vVis = Mathf.Clamp(vVis, -MaxVisImage, MaxVisImage);
            if (Mathf.Abs(vVis) < 0.03f)
                vVis = vVis >= 0f ? 0.03f : -0.03f;

            _imageMarker.localPosition = new Vector3(vVis, LensLocalY, 0f);

            float absM = Mathf.Clamp(Mathf.Abs(_cachedM), 0.15f, 3.5f);
            float sy = _cachedM < 0f ? -absM : absM; // invert when m < 0 (real inverted image)
            _imageMarker.localScale = new Vector3(ImageBaseSize * absM, ImageBaseSize * Mathf.Abs(sy), ImageBaseSize * absM);
            _imageMarker.localRotation = _cachedM < 0f
                ? Quaternion.Euler(0f, 0f, 180f)
                : Quaternion.identity;
        }

        void ApplyGlow()
        {
            if (_lensMat == null || !_lensMat.HasProperty("_EmissionColor"))
                return;
            var cyan = new Color(0.35f, 0.85f, 0.95f, 1f);
            float pulse = _realImage ? 0.55f : 0.25f;
            if (!_finiteImage)
                pulse = 0.15f;
            _lensMat.EnableKeyword("_EMISSION");
            _lensMat.SetColor("_EmissionColor", cyan * pulse);
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
                go.transform.localPosition = new Vector3(0f, LensLocalY + 0.16f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 22f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.65f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(48f, 26f);
            _readout.text = "THIN LENS\nidle";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string fLabel = ActiveFLabel;
            string uLabel = ActiveULabel;
            string fStr = "f=" + (ActiveF * 100f).ToString("+0.0;-0.0;0.0") + "cm";
            string uStr = "u=" + (ActiveU * 100f).ToString("0.0") + "cm";
            string vStr;
            string mStr;
            string tag;
            if (!_finiteImage)
            {
                vStr = "v=inf";
                mStr = "m=--";
                tag = "NO IMAGE";
            }
            else
            {
                vStr = "v=" + (_cachedV * 100f).ToString("+0.0;-0.0;0.0") + "cm";
                mStr = "m=" + _cachedM.ToString("+0.00;-0.00;0.00");
                tag = _realImage ? "REAL" : "VIRTUAL";
            }

            _readout.text =
                "LENS " + fLabel + "/" + uLabel + "\n"
                + fStr + "  " + uStr + "\n"
                + vStr + "  " + mStr + "\n"
                + tag + "\n"
                + "N/P f  Shift u\n"
                + "[thin paraxial]";
        }
    }
}
