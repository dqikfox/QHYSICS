using UnityEngine;
using TMPro;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Conceptual lattice of spheres. Not a measured crystal, not a quantum state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LatticeViz : MonoBehaviour
    {
        public enum Pattern
        {
            MagnetBar,
            CoilRing,
            LoadBlock
        }

        GameObject[] _sites;
        Material _mat;
        bool _visible;
        TextMeshPro _caption;
        bool _built;

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                ApplyVisibility();
            }
        }

        public void BuildMagnet(float length, float radius, Color color)
        {
            int along = 5;
            int around = 6;
            int n = along * around;
            EnsureSites(n, color);
            float L = Mathf.Max(0.04f, length);
            float R = Mathf.Max(0.004f, radius) * 0.65f;
            int w = 0;
            for (int i = 0; i < along; i++)
            {
                float y = (i / (float)(along - 1) - 0.5f) * L * 0.9f;
                for (int k = 0; k < around; k++)
                {
                    float a = (k / (float)around) * Mathf.PI * 2f;
                    _sites[w].transform.localPosition = new Vector3(Mathf.Cos(a) * R, y, Mathf.Sin(a) * R);
                    w++;
                }
            }

            SetCaption("Atomic lattice (conceptual visualization)\nNot a crystal measurement. Not a quantum state.");
        }

        public void BuildCoil(float radius, Color color)
        {
            int rings = 2;
            int around = 12;
            EnsureSites(rings * around, color);
            float R = Mathf.Max(0.04f, radius) * 0.92f;
            int w = 0;
            for (int r = 0; r < rings; r++)
            {
                float y = (r - 0.5f) * 0.02f;
                for (int k = 0; k < around; k++)
                {
                    float a = (k / (float)around) * Mathf.PI * 2f;
                    _sites[w].transform.localPosition = new Vector3(Mathf.Cos(a) * R, y, Mathf.Sin(a) * R);
                    w++;
                }
            }

            SetCaption("Atomic lattice (conceptual visualization)\nCopper lattice is schematic. Not a quantum state.");
        }

        public void BuildLoad(Color color)
        {
            int nx = 3, ny = 3, nz = 3;
            EnsureSites(nx * ny * nz, color);
            int w = 0;
            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            for (int iz = 0; iz < nz; iz++)
            {
                _sites[w].transform.localPosition = new Vector3(
                    (ix - 1) * 0.012f,
                    (iy - 1) * 0.014f,
                    (iz - 1) * 0.012f);
                w++;
            }

            SetCaption("Atomic lattice (conceptual visualization)\nLoad body is schematic. Not a quantum state.");
        }

        void EnsureSites(int count, Color color)
        {
            if (_built && _sites != null && _sites.Length == count)
            {
                Tint(color);
                return;
            }

            ClearSites();
            _mat = MakeMat(color);
            _sites = new GameObject[count];
            Mesh mesh = null;
            var proto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh = proto.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying)
                Destroy(proto);
            else
                DestroyImmediate(proto);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Site_" + i);
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.008f;
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _sites[i] = go;
            }

            _built = true;
            ApplyVisibility();
        }

        void Tint(Color color)
        {
            if (_mat == null)
                return;
            _mat.color = color;
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", color);
            if (_mat.HasProperty("_Color"))
                _mat.SetColor("_Color", color);
        }

        void SetCaption(string text)
        {
            if (_caption == null)
            {
                var go = new GameObject("Caption");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                go.transform.localScale = Vector3.one * 0.02f;
                _caption = go.AddComponent<TextMeshPro>();
                _caption.fontSize = 5f;
                _caption.alignment = TextAlignmentOptions.Center;
                _caption.color = new Color(0.95f, 0.92f, 0.7f);
                _caption.rectTransform.sizeDelta = new Vector2(18f, 4f);
                _caption.raycastTarget = false;
                _caption.textWrappingMode = TextWrappingModes.Normal;
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null)
                    _caption.font = font;
            }

            _caption.text = text;
        }

        void ApplyVisibility()
        {
            if (_sites != null)
            {
                for (int i = 0; i < _sites.Length; i++)
                {
                    if (_sites[i] != null)
                        _sites[i].SetActive(_visible);
                }
            }

            if (_caption != null)
                _caption.gameObject.SetActive(_visible);
        }

        void ClearSites()
        {
            if (_sites == null)
                return;
            for (int i = 0; i < _sites.Length; i++)
            {
                if (_sites[i] != null)
                    Destroy(_sites[i]);
            }
            _sites = null;
            _built = false;
        }

        void OnDestroy()
        {
            if (_mat != null)
                Destroy(_mat);
        }

        static Material MakeMat(Color color)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
                s = Shader.Find("Sprites/Default");
            var mat = new Material(s)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.2f);
            return mat;
        }
    }
}