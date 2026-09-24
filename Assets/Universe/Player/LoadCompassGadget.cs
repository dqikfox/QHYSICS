using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Experiments;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable MEASURE magnetic compass: needle tracks horizontal classical MagneticDipole B
    /// plus a weak ambient lab-north Earth field. Honesty: dipole + constant ambient — not a
    /// fluxgate magnetometer or gyroscope.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadCompassGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Compass";
        // Ambient ~50 uT along world +Z = lab "north" so the needle rests when far from magnets.
        public const string Honesty =
            "Classical MagneticDipole B + ambient Earth ~5e-5 T along world +Z (lab north). Not a fluxgate / gyroscope.";

        public const float EarthFieldTesla = 5e-5f;
        static readonly Vector3 EarthFieldWorld = new Vector3(0f, 0f, EarthFieldTesla);
        static readonly string[] ModeNames = { "ALL", "NEAREST", "LAB" };

        const float SampleHz = 16f;
        const float CacheSeconds = 1.5f;
        const float Damping = 10f;
        const float MinHorizontalTesla = 2e-7f;
        const float AlignAngleDeg = 15f;
        const float AlignMinTesla = 5e-6f;

        [SerializeField, Tooltip("Needle yaw damping (1/s toward Bh).")]
        float needleDamping = Damping;

        TextMeshPro _readout;
        Transform _housing;
        Transform _needle;
        XRGrabInteractable _grab;
        MagneticDipole[] _dipoles;
        MagneticDipole _nearest;
        Material _housingMat;
        Material _rimMat;
        Material _nMat;
        Material _sMat;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        float _needleYaw;
        float _targetYaw;
        float _bhMag;
        int _modeIndex;
        bool _labEnsured;
        string _sourceLabel = "ambient";

        public float HorizontalFieldTesla => _bhMag;
        public float HeadingDegrees => _needleYaw;
        public bool NeedleAligned =>
            _bhMag >= AlignMinTesla
            && Mathf.Abs(Mathf.DeltaAngle(_needleYaw, _targetYaw)) < AlignAngleDeg;
        public string ActiveModeName => ModeNames[Mathf.Clamp(_modeIndex, 0, ModeNames.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureHousing();
            EnsureNeedle();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureDipoles(force: true);
            BindNearest();
            if (CountActiveDipoles() == 0)
                EnsureLabHasMagnet();
            SampleAndAim(forceSnap: true);
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
            DestroyMat(ref _housingMat);
            DestroyMat(ref _rimMat);
            DestroyMat(ref _nMat);
            DestroyMat(ref _sMat);
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
            float interval = 1f / SampleHz;
            if (Time.unscaledTime < _refreshAt)
                return;
            float dt = interval;
            _refreshAt = Time.unscaledTime + interval;

            if (Time.unscaledTime >= _cacheAt || _dipoles == null)
                EnsureDipoles(force: false);

            BindNearest();
            if (CountActiveDipoles() == 0)
                EnsureLabHasMagnet();

            SampleAndAim(forceSnap: false, dt: dt);
            PollDesktopCycle();
            RefreshText();
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
            Cycle(1);
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
            if ((cam.transform.position - transform.position).sqrMagnitude > 1.44f)
                return;
            if (kb.nKey.wasPressedThisFrame)
            {
                Cycle(1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
            else if (kb.pKey.wasPressedThisFrame)
            {
                Cycle(-1);
                _inputCooldown = Time.unscaledTime + 0.2f;
            }
#endif
        }

        public void Cycle(int delta)
        {
            _modeIndex = (_modeIndex + delta + ModeNames.Length) % ModeNames.Length;
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsureDipoles(bool force)
        {
            _cacheAt = Time.unscaledTime + CacheSeconds;
            if (!force && _dipoles != null && _dipoles.Length > 0)
            {
                for (int i = 0; i < _dipoles.Length; i++)
                {
                    if (_dipoles[i] != null)
                        return;
                }
            }
            _dipoles = Object.FindObjectsByType<MagneticDipole>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        void BindNearest()
        {
            if (_dipoles == null || _dipoles.Length == 0)
            {
                _nearest = null;
                return;
            }

            Vector3 center = transform.position;
            MagneticDipole best = null;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < _dipoles.Length; i++)
            {
                MagneticDipole d = _dipoles[i];
                if (d == null || !d.isActive)
                    continue;
                if (d.transform == transform || d.transform.IsChildOf(transform))
                    continue;
                float sq = (d.transform.position - center).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = d;
                }
            }
            _nearest = best;
        }

        void EnsureLabHasMagnet()
        {
            if (_labEnsured && CountActiveDipoles() > 0)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            _labEnsured = true;
            EnsureDipoles(force: true);
            BindNearest();
        }

        Vector3 SampleB(out string sourceLabel)
        {
            Vector3 center = transform.position;
            Vector3 b = Vector3.zero;
            sourceLabel = "ambient";
            int mode = Mathf.Clamp(_modeIndex, 0, ModeNames.Length - 1);

            if (mode == 0) // ALL: sum dipoles + Earth
            {
                int n = 0;
                if (_dipoles != null)
                {
                    for (int i = 0; i < _dipoles.Length; i++)
                    {
                        MagneticDipole d = _dipoles[i];
                        if (d == null || !d.isActive)
                            continue;
                        if (d.transform == transform || d.transform.IsChildOf(transform))
                            continue;
                        b += d.CalculateFieldAt(center);
                        n++;
                    }
                }
                b += EarthFieldWorld;
                sourceLabel = n > 0 ? (n + " dipole(s)+Earth") : "ambient";
                return b;
            }

            if (mode == 1) // NEAREST: nearest + Earth
            {
                if (_nearest != null)
                {
                    b = _nearest.CalculateFieldAt(center);
                    float dist = Vector3.Distance(center, _nearest.transform.position);
                    string name = _nearest.name;
                    if (name.Length > 16)
                        name = name.Substring(0, 14) + "..";
                    sourceLabel = name + " " + dist.ToString("0.00") + "m";
                }
                else
                    sourceLabel = "ambient";
                b += EarthFieldWorld;
                return b;
            }

            // LAB: dipoles only (no Earth) for strong-magnet mapping
            int count = 0;
            if (_dipoles != null)
            {
                for (int i = 0; i < _dipoles.Length; i++)
                {
                    MagneticDipole d = _dipoles[i];
                    if (d == null || !d.isActive)
                        continue;
                    if (d.transform == transform || d.transform.IsChildOf(transform))
                        continue;
                    b += d.CalculateFieldAt(center);
                    count++;
                }
            }
            if (_nearest != null)
            {
                float dist = Vector3.Distance(center, _nearest.transform.position);
                string name = _nearest.name;
                if (name.Length > 16)
                    name = name.Substring(0, 14) + "..";
                sourceLabel = name + " " + dist.ToString("0.00") + "m";
            }
            else
                sourceLabel = count > 0 ? (count + " dipole(s)") : "no B";
            return b;
        }

        void SampleAndAim(bool forceSnap, float dt = 0.06f)
        {
            Vector3 b = SampleB(out _sourceLabel);
            // Project onto housing local horizontal (perpendicular to transform.up).
            Vector3 bh = Vector3.ProjectOnPlane(b, transform.up);
            _bhMag = bh.magnitude;

            if (_bhMag >= MinHorizontalTesla)
            {
                Vector3 localBh = transform.InverseTransformDirection(bh);
                localBh.y = 0f;
                if (localBh.sqrMagnitude > 1e-20f)
                    _targetYaw = Mathf.Atan2(localBh.x, localBh.z) * Mathf.Rad2Deg;
            }
            // else keep last _targetYaw (needle rests / holds heading)

            float damp = Mathf.Max(0.1f, needleDamping);
            if (forceSnap)
                _needleYaw = _targetYaw;
            else
            {
                float t = 1f - Mathf.Exp(-damp * Mathf.Max(1e-4f, dt));
                _needleYaw = Mathf.LerpAngle(_needleYaw, _targetYaw, t);
            }

            if (_needle != null)
                _needle.localRotation = Quaternion.Euler(0f, _needleYaw, 0f);
        }

        int CountActiveDipoles()
        {
            if (_dipoles == null)
                return 0;
            int n = 0;
            for (int i = 0; i < _dipoles.Length; i++)
            {
                MagneticDipole d = _dipoles[i];
                if (d != null && d.isActive
                    && d.transform != transform
                    && !d.transform.IsChildOf(transform))
                    n++;
            }
            return n;
        }

        void EnsureHousing()
        {
            _housing = transform.Find("Housing");
            if (_housing == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Housing";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                // Cylinder default is Y-up; flatten into a disc in root local XZ.
                go.transform.localScale = new Vector3(0.95f, 0.18f, 0.95f);
                Object.Destroy(go.GetComponent<Collider>());
                _housing = go.transform;
            }

            var hr = _housing.GetComponent<Renderer>();
            if (hr != null && _housingMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _housingMat = new Material(sh);
                var dark = new Color(0.06f, 0.1f, 0.12f, 1f);
                if (_housingMat.HasProperty("_BaseColor"))
                    _housingMat.SetColor("_BaseColor", dark);
                _housingMat.color = dark;
                if (_housingMat.HasProperty("_Smoothness"))
                    _housingMat.SetFloat("_Smoothness", 0.75f);
                hr.sharedMaterial = _housingMat;
            }

            Transform rim = transform.Find("Rim");
            if (rim == null)
            {
                var rimGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rimGo.name = "Rim";
                rimGo.transform.SetParent(transform, false);
                rimGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                rimGo.transform.localRotation = Quaternion.identity;
                rimGo.transform.localScale = new Vector3(1.02f, 0.04f, 1.02f);
                Object.Destroy(rimGo.GetComponent<Collider>());
                rim = rimGo.transform;
            }
            var rr = rim.GetComponent<Renderer>();
            if (rr != null && _rimMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _rimMat = new Material(sh);
                var cyan = new Color(0.15f, 0.7f, 0.75f, 1f);
                if (_rimMat.HasProperty("_BaseColor"))
                    _rimMat.SetColor("_BaseColor", cyan);
                _rimMat.color = cyan;
                rr.sharedMaterial = _rimMat;
            }

            // Soften root cube so the disc reads as the body.
            var rootR = GetComponent<Renderer>();
            if (rootR != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (_housingMat == null)
                {
                    _housingMat = new Material(sh);
                    var dark = new Color(0.08f, 0.12f, 0.14f, 1f);
                    if (_housingMat.HasProperty("_BaseColor"))
                        _housingMat.SetColor("_BaseColor", dark);
                    _housingMat.color = dark;
                }
                rootR.sharedMaterial = _housingMat;
            }
        }

        void EnsureNeedle()
        {
            _needle = transform.Find("Needle");
            if (_needle == null)
            {
                var pivot = new GameObject("Needle");
                pivot.transform.SetParent(transform, false);
                pivot.transform.localPosition = new Vector3(0f, 0.045f, 0f);
                pivot.transform.localRotation = Quaternion.identity;
                pivot.transform.localScale = Vector3.one;
                _needle = pivot.transform;

                // Thin shaft along local +Z (N) / -Z (S).
                var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shaft.name = "Shaft";
                shaft.transform.SetParent(_needle, false);
                shaft.transform.localPosition = Vector3.zero;
                shaft.transform.localRotation = Quaternion.identity;
                shaft.transform.localScale = new Vector3(0.035f, 0.02f, 0.72f);
                Object.Destroy(shaft.GetComponent<Collider>());
                var sr = shaft.GetComponent<Renderer>();
                if (sr != null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    var mat = new Material(sh);
                    var gray = new Color(0.45f, 0.48f, 0.5f, 1f);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", gray);
                    mat.color = gray;
                    sr.sharedMaterial = mat;
                }

                var nTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nTip.name = "TipN";
                nTip.transform.SetParent(_needle, false);
                nTip.transform.localPosition = new Vector3(0f, 0f, 0.38f);
                nTip.transform.localScale = new Vector3(0.05f, 0.03f, 0.22f);
                Object.Destroy(nTip.GetComponent<Collider>());
                var nr = nTip.GetComponent<Renderer>();
                if (nr != null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _nMat = new Material(sh);
                    var red = new Color(0.85f, 0.15f, 0.12f, 1f);
                    if (_nMat.HasProperty("_BaseColor"))
                        _nMat.SetColor("_BaseColor", red);
                    _nMat.color = red;
                    nr.sharedMaterial = _nMat;
                }

                var sTip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sTip.name = "TipS";
                sTip.transform.SetParent(_needle, false);
                sTip.transform.localPosition = new Vector3(0f, 0f, -0.38f);
                sTip.transform.localScale = new Vector3(0.05f, 0.03f, 0.22f);
                Object.Destroy(sTip.GetComponent<Collider>());
                var srr = sTip.GetComponent<Renderer>();
                if (srr != null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _sMat = new Material(sh);
                    var gray = new Color(0.55f, 0.58f, 0.6f, 1f);
                    if (_sMat.HasProperty("_BaseColor"))
                        _sMat.SetColor("_BaseColor", gray);
                    _sMat.color = gray;
                    srr.sharedMaterial = _sMat;
                }
            }
            else
            {
                if (_nMat == null)
                {
                    var tipN = _needle.Find("TipN");
                    if (tipN != null)
                    {
                        var nr = tipN.GetComponent<Renderer>();
                        if (nr != null)
                            _nMat = nr.sharedMaterial;
                    }
                }
                if (_sMat == null)
                {
                    var tipS = _needle.Find("TipS");
                    if (tipS != null)
                    {
                        var sr = tipS.GetComponent<Renderer>();
                        if (sr != null)
                            _sMat = sr.sharedMaterial;
                    }
                }
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
                go.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 24f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.55f, 0.85f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(32f, 20f);
            _readout.text = "COMPASS\nseeking B...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string mode = ActiveModeName;
            string align = NeedleAligned ? "aligned" : "seek";
            _readout.text =
                "COMPASS " + mode + "\n"
                + "|Bh| " + FormatTesla(_bhMag) + "\n"
                + "hdg " + _needleYaw.ToString("0") + " deg " + align + "\n"
                + _sourceLabel + "\n"
                + "[classical + Earth]";
        }

        static string FormatTesla(float t)
        {
            float a = Mathf.Abs(t);
            if (a >= 1f)
                return t.ToString("0.###") + " T";
            if (a >= 1e-3f)
                return (t * 1e3f).ToString("0.##") + " mT";
            return (t * 1e6f).ToString("0.#") + " uT";
        }
    }
}
