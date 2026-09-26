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
        // v0.2 tokens (2026-09-27 declutter): dark glass #0B0F14 @ 85%, cyan #00E5FF accents, white text @ 90%.
        public static readonly Color PanelBg = new Color(0.043f, 0.059f, 0.078f, 0.85f);
        public static readonly Color PanelBorder = new Color(0f, 0.898f, 1f, 0.22f);
        public static readonly Color AccentInfo = new Color(0f, 0.898f, 1f, 1f); // #00E5FF cyan
        public static readonly Color AccentActive = new Color(0.35f, 0.90f, 0.45f, 1f);
        public static readonly Color AccentAttention = new Color(0.95f, 0.82f, 0.25f, 1f);
        public static readonly Color AccentError = new Color(0.95f, 0.30f, 0.28f, 1f);
        public static readonly Color TextPrimary = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color TextMuted = new Color(0.72f, 0.77f, 0.82f, 0.85f);
        public static readonly Color ChipBg = new Color(0.10f, 0.13f, 0.16f, 0.92f);
        public static readonly Color ChipBgHover = new Color(0.12f, 0.24f, 0.29f, 0.96f);
        public static readonly Color ChipBgPressed = new Color(0.02f, 0.45f, 0.52f, 1f);
        public static readonly Color ChipBgActive = new Color(0.06f, 0.30f, 0.36f, 0.95f);
        public static readonly Color TabIdle = new Color(0.08f, 0.10f, 0.13f, 0.90f);
        public static readonly Color TabActive = new Color(0.05f, 0.33f, 0.40f, 0.96f);
        public static readonly Color Separator = new Color(1f, 1f, 1f, 0.14f);

        /// <summary>8 / 16 px spacing grid.</summary>
        public const float Space1 = 8f;
        public const float Space2 = 16f;
        /// <summary>Corner radius (UI px) of the runtime 9-slice panel sprite.</summary>
        public const int CornerRadiusPx = 14;

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
        static Sprite _roundedSprite;
        static Sprite _ringSprite;
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

        /// <summary>Runtime-generated rounded-rect 9-slice sprite (anti-aliased corners), used by every panel/chip.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_roundedSprite == null)
                    _roundedSprite = BuildRounded("QHYSICS_UI_Rounded", 0f);
                return _roundedSprite;
            }
        }

        /// <summary>Rounded outline ring (3 px stroke) 9-slice sprite, used for the selected-slot cyan outline.</summary>
        public static Sprite RingSprite
        {
            get
            {
                if (_ringSprite == null)
                    _ringSprite = BuildRounded("QHYSICS_UI_Ring", 3f);
                return _ringSprite;
            }
        }

        static Sprite BuildRounded(string name, float stroke)
        {
            int r = CornerRadiusPx;
            int size = r * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[size * size];
            float inner = size - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to a rounded rect covering the whole texture.
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (stroke > 0f)
                        a *= Mathf.Clamp01(d + stroke + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = r + 1f;
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sp.name = name;
            sp.hideFlags = HideFlags.DontSave;
            return sp;
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
