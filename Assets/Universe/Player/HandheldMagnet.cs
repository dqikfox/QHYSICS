using UnityEngine;
using TMPro;
using RealityEngine.Physics.Electromagnetism;

namespace RealityEngine.Player
{
    /// <summary>
    /// Grabbable PHYSICS magnet/dipole gadget with a live classical MagneticDipole.
    /// Honesty: lumped two-pole / dipole model in SI - not a micromagnetic or quantum magnet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandheldMagnet : MonoBehaviour
    {
        public const string MagnetRootName = "Gadget_Magnet";
        public const string DipoleRootName = "Gadget_Dipole";
        public const string Honesty = "Classical two-pole / dipole B(r). Not micromagnetic.";

        [SerializeField] bool pureDipole;
        [SerializeField] float magneticMoment = 2.0f;
        [SerializeField] float magnetLength = 0.12f;
        [SerializeField] float magnetRadius = 0.02f;

        MagneticDipole _dipole;
        TextMeshPro _readout;
        float _refreshAt;
        bool _built;

        public MagneticDipole Dipole => _dipole;
        public bool IsPureDipole => pureDipole;

        public void Configure(bool asPureDipole)
        {
            pureDipole = asPureDipole;
            if (asPureDipole)
            {
                magneticMoment = 1.2f;
                magnetLength = 0.06f;
                magnetRadius = 0.015f;
            }
            else
            {
                magneticMoment = 2.0f;
                magnetLength = 0.12f;
                magnetRadius = 0.02f;
            }
        }

        public void EnsureBuilt()
        {
            if (!_built)
                BuildVisuals();
            EnsureDipole();
            if (_readout == null)
                BuildReadout();
            RefreshText();
            _built = true;
        }

        void Awake()
        {
            EnsureBuilt();
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.15f;
            RefreshText();
        }

        void EnsureDipole()
        {
            _dipole = GetComponent<MagneticDipole>();
            if (_dipole == null)
                _dipole = gameObject.AddComponent<MagneticDipole>();
            _dipole.localAxis = Vector3.up;
            _dipole.magnetLength = magnetLength;
            _dipole.magnetRadius = magnetRadius;
            _dipole.magneticMoment = magneticMoment;
            _dipole.isActive = true;
        }

        void BuildVisuals()
        {
            // Root is empty; build N/S halves as children (URP Lit only - never Sprites/Default).
            ClearChildren();

            float len = Mathf.Max(0.04f, magnetLength);
            float rad = Mathf.Max(0.008f, magnetRadius);
            gameObject.name = pureDipole ? DipoleRootName : MagnetRootName;

            var north = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            north.name = "North";
            north.transform.SetParent(transform, false);
            north.transform.localPosition = new Vector3(0f, len * 0.25f, 0f);
            north.transform.localScale = new Vector3(rad * 2f, len * 0.25f, rad * 2f);
            Object.Destroy(north.GetComponent<Collider>());
            ApplyLit(north, new Color(0.85f, 0.18f, 0.16f));

            var south = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            south.name = "South";
            south.transform.SetParent(transform, false);
            south.transform.localPosition = new Vector3(0f, -len * 0.25f, 0f);
            south.transform.localScale = new Vector3(rad * 2f, len * 0.25f, rad * 2f);
            Object.Destroy(south.GetComponent<Collider>());
            ApplyLit(south, new Color(0.2f, 0.35f, 0.9f));

            var col = gameObject.GetComponent<CapsuleCollider>();
            if (col == null)
                col = gameObject.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.height = len;
            col.radius = rad * 1.2f;
            col.center = Vector3.zero;
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == "Readout")
                    continue;
                if (Application.isPlaying) Object.Destroy(c.gameObject);
                else Object.DestroyImmediate(c.gameObject);
            }
        }

        static void ApplyLit(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null)
                return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null)
                return;
            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            mat.color = color;
            r.sharedMaterial = mat;
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
                go.transform.localPosition = new Vector3(0f, magnetLength * 0.7f + 0.04f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.012f;
            }

            _readout = go.GetComponent<TextMeshPro>();
            if (_readout == null)
                _readout = go.AddComponent<TextMeshPro>();
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(1f, 0.75f, 0.55f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(28f, 16f);
            _readout.text = pureDipole ? "DIPOLE\n..." : "MAGNET\n...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;
            if (_dipole == null)
                EnsureDipole();
            string title = pureDipole ? "DIPOLE" : "BAR MAGNET";
            _readout.text =
                title + "\n"
                + "m=" + _dipole.magneticMoment.ToString("0.##") + " A.m^2\n"
                + "L=" + (_dipole.magnetLength * 100f).ToString("0.#") + " cm\n"
                + "[" + Honesty + "]";
        }
    }
}
