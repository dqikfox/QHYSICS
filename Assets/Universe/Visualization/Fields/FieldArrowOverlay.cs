using UnityEngine;
using UnityEngine.Rendering;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Tiny world-space arrow pool for Field Lens overlays. Quest 3S budget: dozens of markers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FieldArrowOverlay : MonoBehaviour
    {
        LineRenderer[] _lines;
        Material _mat;
        bool _visible = true;
        static Shader _cachedShader;

        public int Count => _lines != null ? _lines.Length : 0;

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                ApplyVisibility();
            }
        }

        public void EnsureBuilt(int count, Color color, float width)
        {
            int n = Mathf.Clamp(count, 1, 64);
            if (_lines != null && _lines.Length == n && _mat != null)
                return;

            ClearLines();
            _mat = CreateMaterial(color);
            _lines = new LineRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var child = new GameObject("Arrow_" + i);
                child.transform.SetParent(transform, false);
                var lr = child.AddComponent<LineRenderer>();
                lr.sharedMaterial = _mat;
                lr.widthMultiplier = width;
                lr.positionCount = 0;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 1;
                lr.textureMode = LineTextureMode.Stretch;
                lr.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                _lines[i] = lr;
            }

            ApplyVisibility();
        }

        public void SetArrow(int index, Vector3 origin, Vector3 vector, float visualLength)
        {
            if (_lines == null || index < 0 || index >= _lines.Length)
                return;
            LineRenderer lr = _lines[index];
            if (lr == null)
                return;
            if (!_visible || vector.sqrMagnitude < 1e-16f || visualLength < 1e-5f)
            {
                lr.positionCount = 0;
                return;
            }

            Vector3 dir = vector.normalized;
            float len = Mathf.Max(0.006f, visualLength);
            Vector3 tip = origin + dir * len;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-8f)
                side = Vector3.Cross(dir, Vector3.right);
            side.Normalize();
            float head = Mathf.Min(0.012f, len * 0.35f);
            Vector3 neck = tip - dir * head;

            lr.positionCount = 5;
            lr.SetPosition(0, origin);
            lr.SetPosition(1, neck);
            lr.SetPosition(2, neck + side * (head * 0.35f));
            lr.SetPosition(3, tip);
            lr.SetPosition(4, neck - side * (head * 0.35f));
        }

        public void Hide(int index)
        {
            if (_lines == null || index < 0 || index >= _lines.Length)
                return;
            if (_lines[index] != null)
                _lines[index].positionCount = 0;
        }

        public void HideAll()
        {
            if (_lines == null)
                return;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] != null)
                    _lines[i].positionCount = 0;
            }
        }

        void ApplyVisibility()
        {
            if (_lines == null)
                return;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] != null)
                    _lines[i].enabled = _visible;
            }
        }

        void OnDestroy()
        {
            if (_mat != null)
                Destroy(_mat);
        }

        void ClearLines()
        {
            if (_lines == null)
                return;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] != null)
                    Destroy(_lines[i].gameObject);
            }
            _lines = null;
        }

        static Material CreateMaterial(Color color)
        {
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Sprites/Default");
                if (_cachedShader == null)
                    _cachedShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (_cachedShader == null)
                    _cachedShader = Shader.Find("Unlit/Color");
            }

            var mat = new Material(_cachedShader)
            {
                name = "FieldLensArrow",
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}