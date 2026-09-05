using UnityEngine;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// QHYSICS UI v0.1 design tokens. Dark glass charcoal, cyan info, Quest-comfortable world scale.
    /// Never uses Sprites/Default (magenta in URP).
    /// </summary>
    public static class QhysicsUiStyle
    {
        public static readonly Color PanelBg = new Color(0.08f, 0.09f, 0.10f, 0.82f);
        public static readonly Color PanelBorder = new Color(0.35f, 0.38f, 0.42f, 0.55f);
        public static readonly Color AccentInfo = new Color(0.25f, 0.85f, 0.95f, 1f);
        public static readonly Color AccentActive = new Color(0.35f, 0.90f, 0.45f, 1f);
        public static readonly Color AccentAttention = new Color(0.95f, 0.82f, 0.25f, 1f);
        public static readonly Color AccentError = new Color(0.95f, 0.30f, 0.28f, 1f);
        public static readonly Color TextPrimary = new Color(0.92f, 0.93f, 0.94f, 1f);
        public static readonly Color TextMuted = new Color(0.65f, 0.68f, 0.72f, 1f);
        public static readonly Color ChipBg = new Color(0.14f, 0.15f, 0.17f, 0.92f);
        public static readonly Color ChipBgActive = new Color(0.18f, 0.28f, 0.32f, 0.95f);
        public static readonly Color TabIdle = new Color(0.12f, 0.13f, 0.15f, 0.90f);
        public static readonly Color TabActive = new Color(0.16f, 0.32f, 0.36f, 0.95f);

        /// <summary>World canvas scale so 1000 UI units ≈ 1 m (Quest-comfortable at 1-2 m).</summary>
        public const float CanvasScale = 0.001f;
        public const float HudDistanceM = 0.55f;
        public const float ToolbeltDistanceM = 0.85f;
        public const float ComfortHeightM = 1.35f;

        public const float FontTitle = 42f;
        public const float FontBody = 28f;
        public const float FontSmall = 22f;
        public const float FontChip = 24f;

        public const float TargetMinPx = 72f;

        static TMP_FontAsset _font;
        static Sprite _whiteSprite;
        static Material _uiMat;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null)
                    return _font;
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (_font == null)
                    _font = TMP_Settings.defaultFontAsset;
                return _font;
            }
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null)
                    return _whiteSprite;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "QHYSICS_UI_White",
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear
                };
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++)
                    px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                _whiteSprite.name = "QHYSICS_UI_WhiteSprite";
                _whiteSprite.hideFlags = HideFlags.DontSave;
                return _whiteSprite;
            }
        }

        /// <summary>URP-safe UI material (UI/Default). Never Sprites/Default.</summary>
        public static Material UiMaterial
        {
            get
            {
                if (_uiMat != null)
                    return _uiMat;
                Shader sh = Shader.Find("UI/Default");
                if (sh == null)
                    sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null)
                {
                    Debug.LogError("QhysicsUiStyle: UI/Default missing. Not falling back to Sprites/Default.");
                    return null;
                }
                _uiMat = new Material(sh)
                {
                    name = "QHYSICS_UI_Default",
                    hideFlags = HideFlags.DontSave
                };
                return _uiMat;
            }
        }

        public static void ApplyTmp(TMP_Text tmp, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            if (tmp == null)
                return;
            if (Font != null)
                tmp.font = Font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
