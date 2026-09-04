using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using RealityEngine.Core;
using RealityEngine.Visualization;
using RealityEngine.Chemistry;

namespace RealityEngine.Survey
{
    /// <summary>
    /// Field Lens / Scale Engine peel on Menkaure casing (Aswan granite lower + Tura upper)
    /// and granite burial chamber. Human: reconstructed casing already on the monument.
    /// Material: ~a dozen block outlines near the look-hit. Molecular: conceptual calcite
    /// lattice (Tura) + granite note. Atomic: Ca / C / O cards. E/B parked.
    /// Math: published slope 51 deg 20' 25" and base/height in royal cubits.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class MenkaureSurvey : MonoBehaviour
    {
        public const string Honesty = GizaComplex.HonestyPrefix;

        Transform _material;
        Transform _molecular;
        Transform _atomic;
        Transform _parked;
        Transform _math;
        TextMeshPro _caption;
        Camera _camera;
        bool _built;
        int _layer;
        int _scale;
        Material _matBlock;
        Material _matGranite;
        Material _matCa;
        Material _matC;
        Material _matO;
        Vector3 _hitPoint;
        Vector3 _hitNormal = Vector3.up;
        bool _hasHit;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        public int Layer => _layer;
        public int Scale => _scale;

        public void EnsureBuilt()
        {
            CacheIfNeeded();
            if (!_built)
            {
                if (_material == null)
                    BuildAll();
                _built = true;
            }
            ApplyView(_layer, _scale);
        }

        public void ApplyView(int layer, int scale)
        {
            _layer = Mathf.Clamp(layer, 0, FieldLens.LayerCount - 1);
            _scale = Mathf.Clamp(scale, 0, ScaleEngine.StepCount - 1);
            if (!_built)
                EnsureBuilt();

            FieldLensLayer L = (FieldLensLayer)_layer;
            ScaleLevel S = (ScaleLevel)_scale;

            bool emParked = L == FieldLensLayer.Electric || L == FieldLensLayer.Magnetic;
            bool energyParked = L == FieldLensLayer.Charge || L == FieldLensLayer.EnergyFlow;
            bool material = !emParked && !energyParked && (L == FieldLensLayer.Material || S == ScaleLevel.Material);
            bool molecular = !emParked && !energyParked && (S == ScaleLevel.Molecular || L == FieldLensLayer.Atomic);
            bool atomic = !emParked && !energyParked && S == ScaleLevel.Atomic;
            bool math = !emParked && !energyParked && L == FieldLensLayer.Mathematical;

            SetOn(_material, material);
            SetOn(_molecular, molecular);
            SetOn(_atomic, atomic);
            SetOn(_parked, emParked || energyParked);
            SetOn(_math, math);
            UpdateCaption(L, S, emParked || energyParked, material, molecular, atomic, math);
        }

        public static string SlopeAnswer()
        {
            float cubit = KhufuPyramid.Cubit;
            float baseCubits = MenkaurePyramid.BaseMeters / cubit;
            float heightCubits = MenkaurePyramid.HeightMeters / cubit;
            float tanSlope = Mathf.Tan(MenkaurePyramid.SlopeDeg * Mathf.Deg2Rad);
            float seked = 7f / tanSlope;
            string body =
                "Why is Menkaure's slope 51 deg 20' 25\"?\n"
                + "Published face slope " + MenkaurePyramid.SlopeDeg.ToString("0.######")
                + " deg. Seked ≈ " + seked.ToString("0.00")
                + " palms (horizontal run for a 1-cubit / 7-palm rise).\n"
                + "Base ≈ " + baseCubits.ToString("0.0") + " cubits (" + MenkaurePyramid.BaseMeters.ToString("0.00")
                + " m), height ≈ " + heightCubits.ToString("0.0") + " cubits ("
                + MenkaurePyramid.HeightMeters.ToString("0.0") + " m). Royal cubit "
                + cubit.ToString("0.0000") + " m.\n"
                + "Lower ~" + MenkaurePyramid.GraniteCourses.ToString("0")
                + " courses Aswan granite (~" + MenkaurePyramid.GraniteHeightM.ToString("0.0")
                + " m), upper Tura limestone + intact pyramidion (reconstructed).\n"
                + Honesty + "\n"
                + "Granite burial chamber (barrel vault) under the mass.";
            if (CubitRod.HasMeasurement)
                body += "\nLive rod: " + CubitRod.DescribeLast();
            return body;
        }

        void Awake()
        {
            _camera = Camera.main;
            EnsureBuilt();
        }

        void LateUpdate()
        {
            TrackLookHit();
            PlaceOverlays();
            if (_caption == null)
                return;
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;
            Vector3 toCam = _caption.transform.position - _camera.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-6f)
                _caption.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        void OnDestroy()
        {
            DestroyMat(_matBlock);
            DestroyMat(_matGranite);
            DestroyMat(_matCa);
            DestroyMat(_matC);
            DestroyMat(_matO);
        }

        void TrackLookHit()
        {
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;
            int n = UnityEngine.Physics.RaycastNonAlloc(_camera.transform.position, _camera.transform.forward, _hits, 400f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            bool any = false;
            RaycastHit chosen = default;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null)
                    continue;
                Transform t = h.collider.transform;
                if (t.IsChildOf(transform) && IsOverlay(t))
                    continue;
                if (!IsMenkaurePart(t))
                    continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    chosen = h;
                    any = true;
                }
            }
            if (!any)
                return;
            _hitPoint = chosen.point;
            _hitNormal = chosen.normal.sqrMagnitude > 1e-6f ? chosen.normal.normalized : Vector3.up;
            _hasHit = true;
        }

        static bool IsOverlay(Transform t)
        {
            while (t != null)
            {
                string n = t.name;
                if (!string.IsNullOrEmpty(n) && n.StartsWith("MenkaureLens", System.StringComparison.Ordinal))
                    return true;
                t = t.parent;
            }
            return false;
        }

        static bool IsMenkaurePart(Transform t)
        {
            while (t != null)
            {
                string n = t.name;
                if (!string.IsNullOrEmpty(n) && (n == MenkaurePyramid.RootName || n.StartsWith("Menkaure", System.StringComparison.Ordinal)))
                    return true;
                t = t.parent;
            }
            return false;
        }

        void PlaceOverlays()
        {
            if (!_hasHit)
                return;
            Vector3 p = _hitPoint + _hitNormal * 0.18f;
            Quaternion face = Quaternion.LookRotation(-_hitNormal, Vector3.up);
            SetWorld(_material, p, face);
            SetWorld(_molecular, p, face);
            SetWorld(_atomic, p, face);
            SetWorld(_parked, p, face);
            SetWorld(_math, p, face);
            if (_caption != null)
                _caption.transform.position = p + Vector3.up * 0.55f;
        }

        static void SetWorld(Transform t, Vector3 p, Quaternion r)
        {
            if (t == null)
                return;
            t.position = p;
            t.rotation = r;
        }

        void CacheIfNeeded()
        {
            if (_material == null)
            {
                Transform t = transform.Find("MenkaureLensMaterial");
                if (t != null)
                    _material = t;
            }
            if (_molecular == null)
            {
                Transform t = transform.Find("MenkaureLensMolecular");
                if (t != null)
                    _molecular = t;
            }
            if (_atomic == null)
            {
                Transform t = transform.Find("MenkaureLensAtomic");
                if (t != null)
                    _atomic = t;
            }
            if (_parked == null)
            {
                Transform t = transform.Find("MenkaureLensParked");
                if (t != null)
                    _parked = t;
            }
            if (_math == null)
            {
                Transform t = transform.Find("MenkaureLensMath");
                if (t != null)
                    _math = t;
            }
            if (_caption == null)
            {
                Transform t = transform.Find("MenkaureLensCaption");
                if (t != null)
                    _caption = t.GetComponent<TextMeshPro>();
            }
        }

        void BuildAll()
        {
            _matBlock = LabWorldMeshes.MakeLit("RELab_MenkaureBlock", new Color(0.78f, 0.72f, 0.60f), 0.06f, 0.22f, false);
            LabWorldMeshes.ApplyAlbedo(_matBlock, LabWorldMeshes.MakeTuraBlockTexture(), Vector2.one);
            _matGranite = LabWorldMeshes.MakeLit("RELab_MenkaureGranite", new Color(0.42f, 0.28f, 0.24f), 0.10f, 0.35f, false);
            _matCa = LabWorldMeshes.MakeLit("RELab_CalciteCa", new Color(0.82f, 0.82f, 0.86f), 0.12f, 0.40f, false);
            _matC = LabWorldMeshes.MakeLit("RELab_CalciteC", new Color(0.22f, 0.22f, 0.24f), 0.08f, 0.28f, false);
            _matO = LabWorldMeshes.MakeLit("RELab_CalciteO", new Color(0.72f, 0.18f, 0.16f), 0.08f, 0.32f, false);

            _material = NewRoot("MenkaureLensMaterial");
            const int count = 12;
            for (int i = 0; i < count; i++)
            {
                int col = i % 4;
                int row = i / 4;
                Vector3 p = new Vector3((col - 1.5f) * 1.15f, (row - 1f) * 0.72f, 0.02f);
                Material mat = row == 0 ? _matGranite : _matBlock;
                Primitive(_material, PrimitiveType.Cube, "CourseBlock_" + i, p, new Vector3(1.05f, 0.62f, 0.18f), mat);
            }

            _molecular = NewRoot("MenkaureLensMolecular");
            BuildCalcite(_molecular);
            MakeLabel(_molecular, "CrystalNote",
                "Lower Aswan granite + upper CaCO3 Tura\nConceptual / Classical\nnot XRD  not MD",
                new Vector3(0f, 0.42f, 0.05f), 0.018f, new Vector2(20f, 6f), new Color(0.92f, 0.90f, 0.78f));

            _atomic = NewRoot("MenkaureLensAtomic");
            MakeLabel(_atomic, "AtomCard", Element.CalciteAtomCards(), new Vector3(0f, 0.28f, 0.05f), 0.016f, new Vector2(22f, 12f), new Color(0.92f, 0.95f, 0.82f));

            _parked = NewRoot("MenkaureLensParked");
            MakeLabel(_parked, "Parked", "not an EM source\nMenkaure is stone geometry.\nUse the copper coil for B / E.",
                new Vector3(0f, 0.22f, 0.05f), 0.018f, new Vector2(18f, 7f), new Color(0.75f, 0.85f, 0.95f));

            float baseCubits = MenkaurePyramid.BaseMeters / KhufuPyramid.Cubit;
            float heightCubits = MenkaurePyramid.HeightMeters / KhufuPyramid.Cubit;
            float tanSlope = Mathf.Tan(MenkaurePyramid.SlopeDeg * Mathf.Deg2Rad);
            float seked = 7f / tanSlope;
            _math = NewRoot("MenkaureLensMath");
            MakeLabel(_math, "Slope",
                "51 deg 20' 25\"\nseked ≈ " + seked.ToString("0.00") + " palms\n"
                + baseCubits.ToString("0.0") + " x " + heightCubits.ToString("0.0") + " cubits\ngranite + Tura",
                new Vector3(0f, 0.28f, 0.05f), 0.018f, new Vector2(18f, 9f), new Color(0.85f, 0.92f, 1f));

            var capGo = new GameObject("MenkaureLensCaption");
            capGo.transform.SetParent(transform, false);
            capGo.transform.localPosition = new Vector3(0f, 2.2f, MenkaurePyramid.BaseMeters * 0.5f + 2f);
            capGo.transform.localScale = Vector3.one * 0.018f;
            _caption = capGo.AddComponent<TextMeshPro>();
            _caption.fontSize = 5f;
            _caption.alignment = TextAlignmentOptions.Center;
            _caption.color = new Color(0.92f, 0.90f, 0.78f);
            _caption.rectTransform.sizeDelta = new Vector2(24f, 8f);
            _caption.raycastTarget = false;
            _caption.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                _caption.font = font;
        }

        void BuildCalcite(Transform parent)
        {
            float s = 0.11f;
            Vector3[] corners =
            {
                new Vector3(-s, -s * 0.7f, -s),
                new Vector3(s, -s * 0.7f, -s),
                new Vector3(s, -s * 0.7f, s),
                new Vector3(-s, -s * 0.7f, s),
                new Vector3(-s * 0.7f, s * 0.9f, -s * 0.7f),
                new Vector3(s * 1.1f, s * 0.9f, -s * 0.7f),
                new Vector3(s * 1.1f, s * 0.9f, s * 1.1f),
                new Vector3(-s * 0.7f, s * 0.9f, s * 1.1f)
            };
            for (int i = 0; i < corners.Length; i++)
                Primitive(parent, PrimitiveType.Sphere, "Ca_" + i, corners[i], Vector3.one * 0.055f, _matCa);
            Primitive(parent, PrimitiveType.Sphere, "C_0", Vector3.zero, Vector3.one * 0.040f, _matC);
            Primitive(parent, PrimitiveType.Sphere, "O_0", new Vector3(0.08f, 0.02f, 0f), Vector3.one * 0.032f, _matO);
            Primitive(parent, PrimitiveType.Sphere, "O_1", new Vector3(-0.05f, 0.02f, 0.07f), Vector3.one * 0.032f, _matO);
            Primitive(parent, PrimitiveType.Sphere, "O_2", new Vector3(-0.05f, 0.02f, -0.07f), Vector3.one * 0.032f, _matO);
        }

        void UpdateCaption(FieldLensLayer L, ScaleLevel S, bool parked, bool material, bool molecular, bool atomic, bool math)
        {
            if (_caption == null)
                return;
            string line = "Menkaure casing / burial chamber\n" + Honesty;
            if (parked)
                line += "\nnot an EM source";
            else if (math)
                line += "\n51 deg 20' 25\". Seked from published slope. Base/height in royal cubits. Granite + Tura.";
            else if (atomic)
                line += "\nCa, C, O cards. CaCO3 schematic (Tura). Not QM. Not XRD.";
            else if (molecular)
                line += "\nLower Aswan granite + upper calcite schematic. Conceptual / Classical, not XRD.";
            else if (material)
                line += "\nCourse banding near look-hit. Lower row Aswan granite, upper Tura limestone.";
            else
                line += "\nHuman: reconstructed original casing (granite courses + Tura).";
            _caption.text = line;
        }

        Transform NewRoot(string name)
        {
            Transform existing = transform.Find(name);
            if (existing != null)
                return existing;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static void Primitive(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(col);
                else
                    Object.DestroyImmediate(col);
            }
            var r = go.GetComponent<Renderer>();
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static TextMeshPro MakeLabel(Transform parent, string name, string body, Vector3 localPos, float localScale, Vector2 size, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * localScale;
            TextMeshPro tmp = go.GetComponent<TextMeshPro>();
            if (tmp == null)
                tmp = go.AddComponent<TextMeshPro>();
            tmp.text = body;
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = size;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        static void SetOn(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on)
                t.gameObject.SetActive(on);
        }

        static void DestroyMat(Material mat)
        {
            if (mat == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(mat);
            else
                Object.DestroyImmediate(mat);
        }
    }
}
