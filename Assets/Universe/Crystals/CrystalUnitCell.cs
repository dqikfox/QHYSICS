using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealityEngine.Crystals
{
    /// <summary>
    /// "What's happening in the lattice": a small floating ball-and-stick model next to each bench, driven live by
    /// the station (Drive in -1..1, Aux in 0..1). Red = positive ion, blue = negative ion, grey = neutral,
    /// cyan arrow = net dipole / polarisation. Honesty: schematic 2D/3D cartoons of the real structures
    /// (displacements exaggerated about 10^5 times); not a DFT or molecular-dynamics simulation.
    /// ~10-20 shared low-poly beads per model, updated only while visible.
    /// </summary>
    public sealed class CrystalUnitCell : MonoBehaviour
    {
        public enum Mode { QuartzPiezo, PyroLiTaO3, Resonator, Calcite, Tribo, HotIce, Perovskite }

        public Mode CellMode;
        /// <summary>Main drive: stress / polarisation / phase / angle (station-defined, -1..1).</summary>
        public float Drive;
        /// <summary>Secondary drive 0..1 (screening charge, crack opening, order fraction, temperature...).</summary>
        public float Aux;
        public float Highlight;

        readonly List<Transform> _ions = new List<Transform>();
        readonly List<Vector3> _base = new List<Vector3>();
        readonly List<int> _charge = new List<int>();
        readonly List<Vector3> _jitter = new List<Vector3>();
        Transform _arrow, _arrowHead, _frame, _extraA, _extraB;
        TextMeshPro _caption;
        Renderer _frameR;
        Material _plus, _minus, _neutral, _arrowMat, _frameMat, _water;
        Camera _cam;
        float _nextVis;
        bool _visible = true;
        string _title;

        public static CrystalUnitCell Create(Transform parent, Vector3 lp, Mode mode, string title)
        {
            var go = new GameObject("UnitCell_" + mode);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            var c = go.AddComponent<CrystalUnitCell>();
            c.CellMode = mode;
            c._title = title;
            c.Build();
            return c;
        }

        void Build()
        {
            _plus = CrystalFx.Solid(new Color(0.95f, 0.32f, 0.28f), 0f, 0.6f, new Color(0.35f, 0.05f, 0.04f));
            _minus = CrystalFx.Solid(new Color(0.25f, 0.5f, 1f), 0f, 0.6f, new Color(0.03f, 0.08f, 0.3f));
            _neutral = CrystalFx.Solid(new Color(0.75f, 0.78f, 0.8f), 0f, 0.5f);
            _water = CrystalFx.Solid(new Color(0.55f, 0.85f, 0.95f), 0f, 0.6f);
            _arrowMat = CrystalFx.Emissive(new Color(0f, 0.9f, 1f), 1.4f);
            _frameMat = CrystalFx.Crystal(new Color(0f, 0.9f, 1f, 0.07f));

            var frame = CrystalFx.Prim(PrimitiveType.Cube, "CellFrame", transform, Vector3.zero, Vector3.one * 0.3f, _frameMat, false);
            _frame = frame.transform;
            _frameR = frame.GetComponent<Renderer>();

            var arrow = CrystalFx.Prim(PrimitiveType.Cylinder, "Dipole", transform, Vector3.zero, new Vector3(0.012f, 0.05f, 0.012f), _arrowMat, false);
            _arrow = arrow.transform;
            var head = CrystalFx.Prim(PrimitiveType.Cube, "DipoleHead", transform, Vector3.zero, Vector3.one * 0.026f, _arrowMat, false);
            _arrowHead = head.transform;

            _caption = CrystalFx.Text(transform, "Caption", "", 1.9f, new Color(0.7f, 0.92f, 1f, 0.95f), new Vector3(0f, -0.21f, 0f), new Vector2(4.2f, 0.9f));
            _caption.transform.localScale = Vector3.one * 0.1f;

            switch (CellMode)
            {
                case Mode.QuartzPiezo:
                    // Classic 2D quartz model: hexagon of alternating Si(+) and O(-) (Curie brothers' picture).
                    for (int k = 0; k < 6; k++)
                    {
                        float a = Mathf.PI / 2f + k * Mathf.PI / 3f;
                        AddIon(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.1f, k % 2 == 0 ? +1 : -1, k % 2 == 0 ? 0.04f : 0.032f);
                    }
                    break;
                case Mode.PyroLiTaO3:
                    // Polar column: O planes (blue), Ta(+) near centre, Li(+) off-centre along c.
                    for (int i = 0; i < 4; i++)
                        AddIon(new Vector3((i % 2 == 0 ? -1f : 1f) * 0.08f, 0.1f, (i < 2 ? -1f : 1f) * 0.08f), -1, 0.03f);
                    for (int i = 0; i < 4; i++)
                        AddIon(new Vector3((i % 2 == 0 ? -1f : 1f) * 0.08f, -0.1f, (i < 2 ? -1f : 1f) * 0.08f), -1, 0.03f);
                    AddIon(new Vector3(0f, 0.0f, 0f), +1, 0.045f);   // Ta
                    AddIon(new Vector3(0f, -0.05f, 0f), +1, 0.03f);  // Li
                    break;
                case Mode.Resonator:
                    // Two tines (beads along each) with surface charges that swap each half cycle.
                    for (int i = 0; i < 5; i++)
                    {
                        AddIon(new Vector3(-0.05f, -0.12f + i * 0.055f, 0f), 0, 0.03f);
                        AddIon(new Vector3(0.05f, -0.12f + i * 0.055f, 0f), 0, 0.03f);
                    }
                    AddIon(new Vector3(-0.085f, 0.03f, 0f), +1, 0.022f);
                    AddIon(new Vector3(0.085f, 0.03f, 0f), -1, 0.022f);
                    break;
                case Mode.Calcite:
                    // Three stacked CO3 triangles (C grey, O blue) with Ca(+) between: optic axis = stack axis.
                    for (int layer = 0; layer < 3; layer++)
                    {
                        float y = -0.09f + layer * 0.09f;
                        float rot = layer % 2 == 0 ? 0f : Mathf.PI / 3f;
                        AddIon(new Vector3(0f, y, 0f), 0, 0.025f);
                        for (int k = 0; k < 3; k++)
                        {
                            float a = rot + k * 2f * Mathf.PI / 3f;
                            AddIon(new Vector3(Mathf.Cos(a) * 0.06f, y, Mathf.Sin(a) * 0.06f), -1, 0.024f);
                        }
                        if (layer < 2)
                            AddIon(new Vector3(0.09f, y + 0.045f, 0f), +1, 0.03f);
                    }
                    break;
                case Mode.Tribo:
                    // Two halves of a sucrose / quartz grain: when the crack opens, + and - faces separate.
                    for (int i = 0; i < 4; i++)
                    {
                        AddIon(new Vector3(-0.03f, -0.09f + i * 0.06f, 0f), +1, 0.03f);
                        AddIon(new Vector3(0.03f, -0.09f + i * 0.06f, 0f), -1, 0.03f);
                        AddIon(new Vector3(-0.09f, -0.09f + i * 0.06f, 0f), 0, 0.03f);
                        AddIon(new Vector3(0.09f, -0.09f + i * 0.06f, 0f), 0, 0.03f);
                    }
                    break;
                case Mode.HotIce:
                    // 4 x 4 lattice of Na+ / acetate- with waters that jitter in liquid and lock in crystal.
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 4; j++)
                            AddIon(new Vector3(-0.09f + i * 0.06f, -0.09f + j * 0.06f, 0f), (i + j) % 2 == 0 ? +1 : -1, 0.032f);
                    for (int i = 0; i < 6; i++)
                        AddIon(new Vector3(-0.06f + (i % 3) * 0.06f, -0.06f + (i / 3) * 0.12f, 0.03f), 2, 0.018f);
                    break;
                case Mode.Perovskite:
                    // BaTiO3: Ba corners (neutral grey here), O face centres (blue), Ti centre (red) that shifts with P.
                    for (int i = 0; i < 8; i++)
                        AddIon(new Vector3((i & 1) == 0 ? -0.1f : 0.1f, (i & 2) == 0 ? -0.1f : 0.1f, (i & 4) == 0 ? -0.1f : 0.1f), 0, 0.035f);
                    Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                    for (int i = 0; i < 6; i++)
                        AddIon(faces[i] * 0.1f, -1, 0.028f);
                    AddIon(Vector3.zero, +1, 0.035f);
                    break;
            }
            var title = CrystalFx.Text(transform, "Title", _title, 2.2f, new Color(0f, 0.9f, 1f, 1f), new Vector3(0f, 0.21f, 0f), new Vector2(4.2f, 0.6f));
            title.transform.localScale = Vector3.one * 0.1f;
        }

        void AddIon(Vector3 p, int charge, float d)
        {
            Material m = charge > 1 ? _water : charge > 0 ? _plus : charge < 0 ? _minus : _neutral;
            var b = CrystalFx.Bead("Ion" + _ions.Count, transform, p, d, m);
            _ions.Add(b.transform);
            _base.Add(p);
            _charge.Add(charge);
            _jitter.Add(new Vector3(Random.value, Random.value, Random.value) * 6.28f);
        }

        void SetArrow(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            bool on = len > 0.004f;
            _arrow.gameObject.SetActive(on);
            _arrowHead.gameObject.SetActive(on);
            if (!on)
                return;
            _arrow.localPosition = (from + to) * 0.5f;
            _arrow.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
            _arrow.localScale = new Vector3(0.01f, len * 0.5f, 0.01f);
            _arrowHead.localPosition = to;
            _arrowHead.localRotation = _arrow.localRotation * Quaternion.Euler(45f, 0f, 45f);
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextVis)
            {
                _nextVis = Time.unscaledTime + 0.5f;
                if (_cam == null) _cam = Camera.main;
                _visible = _cam == null || (_cam.transform.position - transform.position).sqrMagnitude < 36f;
                for (int i = 0; i < transform.childCount; i++)
                    transform.GetChild(i).gameObject.SetActive(_visible);
            }
            if (!_visible)
                return;
            float t = Time.time;
            // Billboard yaw toward the viewer so the 2D models read correctly.
            if (_cam != null)
            {
                Vector3 to = transform.position - _cam.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
            float s = Mathf.Clamp(Drive, -1f, 1f);
            Vector3 plusC = Vector3.zero, minusC = Vector3.zero;
            int np = 0, nm = 0;
            for (int i = 0; i < _ions.Count; i++)
            {
                Vector3 p = _base[i];
                int q = _charge[i];
                switch (CellMode)
                {
                    case Mode.QuartzPiezo:
                        // Squeeze along x: hexagon flattens and the Si(+) sublattice shifts against O(-) -> dipole.
                        p = new Vector3(p.x * (1f - 0.25f * s), p.y * (1f + 0.15f * s), 0f);
                        p.y += (q > 0 ? 1f : -1f) * 0.035f * s;
                        break;
                    case Mode.PyroLiTaO3:
                        // Li / Ta displaced along c by Ps(T); Aux = T/Tc shrinks the displacement.
                        if (q > 0)
                        {
                            float ps = Mathf.Sqrt(Mathf.Clamp01(1f - Aux));
                            p.y = _base[i].y + (i == _ions.Count - 1 ? -0.035f : -0.02f) * ps * Mathf.Sign(s == 0f ? 1f : s);
                        }
                        p += new Vector3(Mathf.Sin(t * 13f + _jitter[i].x), Mathf.Sin(t * 11f + _jitter[i].y), 0f) * 0.004f * (0.3f + Aux);
                        break;
                    case Mode.Resonator:
                    {
                        float bend = Mathf.Sin(Aux * Mathf.PI * 2f) * s;
                        float h = (p.y + 0.12f) / 0.24f;
                        float side = p.x < 0f ? -1f : 1f;
                        p.x += side * bend * 0.035f * h * h;
                        if (q != 0)
                            q = bend >= 0f ? (side < 0f ? 1 : -1) : (side < 0f ? -1 : 1);
                        break;
                    }
                    case Mode.Calcite:
                        p = Quaternion.Euler(0f, s * 180f, 0f) * p;
                        break;
                    case Mode.Tribo:
                    {
                        float open = Mathf.Clamp01(Aux);
                        if (p.x < 0f) p.x -= open * 0.05f; else p.x += open * 0.05f;
                        break;
                    }
                    case Mode.HotIce:
                    {
                        float liquid = 1f - Mathf.Clamp01(Aux);
                        p += new Vector3(Mathf.Sin(t * 3.1f + _jitter[i].x), Mathf.Sin(t * 2.7f + _jitter[i].y), Mathf.Sin(t * 2.3f + _jitter[i].z)) * 0.03f * liquid;
                        break;
                    }
                    case Mode.Perovskite:
                        if (q > 0)
                            p = new Vector3(0f, 0.035f * s, 0f);
                        else if (q < 0)
                            p -= new Vector3(0f, 0.008f * s, 0f);
                        break;
                }
                _ions[i].localPosition = p;
                if (q > 0 && q < 2) { plusC += p; np++; }
                else if (q < 0) { minusC += p; nm++; }
            }
            if (np > 0) plusC /= np;
            if (nm > 0) minusC /= nm;

            switch (CellMode)
            {
                case Mode.QuartzPiezo:
                case Mode.PyroLiTaO3:
                case Mode.Perovskite:
                {
                    Vector3 d = (plusC - minusC) * 3f;
                    SetArrow(-d * 0.5f + new Vector3(0.15f, 0f, 0f), d * 0.5f + new Vector3(0.15f, 0f, 0f));
                    break;
                }
                case Mode.Calcite:
                {
                    Vector3 axis = Quaternion.Euler(0f, s * 180f, 0f) * new Vector3(0.05f, 0.13f, 0f);
                    SetArrow(-axis, axis);
                    break;
                }
                case Mode.Tribo:
                    SetArrow(new Vector3(-0.02f - Aux * 0.05f, 0.14f, 0f), new Vector3(0.02f + Aux * 0.05f, 0.14f, 0f));
                    break;
                default:
                    SetArrow(Vector3.zero, Vector3.zero);
                    break;
            }
            if (_frameR != null)
                _frameR.enabled = Highlight > 0.5f || CellMode == Mode.Perovskite || CellMode == Mode.PyroLiTaO3;
        }

        public void SetCaption(string text)
        {
            if (_caption != null && _caption.text != text)
                _caption.text = text;
        }
    }
}
