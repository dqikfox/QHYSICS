using UnityEngine;
using TMPro;

namespace RealityEngine.UI
{
    /// <summary>
    /// Short dismissible world-space tips: Welcome ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Grab ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Try table. Not a trap.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(205)]
    public sealed class QhysicsOnboarding : MonoBehaviour
    {
        public const string RootName = "QhysicsOnboarding";
        const string PrefKey = "QHYSICS.Onboarding.Dismissed";

        static readonly string[] Tips =
        {
            "Welcome to QHYSICS\nCircuit lab on the table. Giza outside.",
            "Desktop: WASD move, mouse look, E/LMB grab, F drop, R throw.\nVR: grip / trigger. Hotbar 1-8 / scroll.",
            "You are the XR Origin (and desktop body when no headset). Controllers / DesktopBody are your hands - not a separate avatar.",
            "Try a loop: Battery -> Wire -> Bulb -> Switch.\nEsc/P pause. Toolbelt New Run resets the table and clears spawned gadgets.",
            "PHYSICS Field Lens: grab it, N/P or VR trigger peels layers (honesty tags on readout).",
            "PHYSICS Magnet/Dipole: grab a live classical MagneticDipole (Probe reads B).",
            "PHYSICS Coil: grab a live classical InductionCoil; move a Magnet through it (Multimeter reads Emf/I).",
            "MEASURE Multimeter: binds nearest InductionCircuit (lab or PHYSICS Coil); tip shows target + distance.",
            "Lab + PHYSICS coils auto-bind every MagneticDipole — gadget Magnet through lab Coil still drives Emf/I.",
            "CIRCUIT Resistor: hold near a Coil to apply classical R_load (N/P or VR trigger cycles 2/8/50/OPEN); Multimeter I changes.",
            "CIRCUIT Lamp: hold near a Coil — glows from classical |I| (pair with Magnet sweep + Resistor); Multimeter still reads Emf/I.",
            "CIRCUIT Switch: hold near a Coil — N/P or VR trigger toggles CLOSED/OPEN (OPEN = classical open circuit; kills Lamp |I|).",
            "CIRCUIT Battery: hold near a Coil — N/P or VR trigger cycles 1.5/3/6/9/OFF V (classical series EMF; Lamp glows without magnet sweep).",
            "CIRCUIT Wire: hold near a Coil — classical series jumper R (N/P or VR trigger cycles 0.05/0.2/1 ohm; Resistor/Switch OPEN override).",
        };

        Canvas _canvas;
        TextMeshProUGUI _body;
        int _step;
        Camera _cam;
        bool _ready;

        public static QhysicsOnboarding Ensure(Transform parent)
        {
            Transform existing = parent != null ? parent.Find(RootName) : null;
            if (existing == null)
            {
                GameObject found = GameObject.Find(RootName);
                if (found != null)
                    existing = found.transform;
            }
            if (existing != null)
            {
                var c = existing.GetComponent<QhysicsOnboarding>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsOnboarding>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsOnboarding>();
            comp.Build();
            return comp;
        }

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(760f, 360f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(720f, 320f));
            QhysicsUiBuilder.LayoutVertical(face.rectTransform, 12f);

            QhysicsUiBuilder.Label(face.transform, "Title", "QHYSICS", QhysicsUiStyle.FontTitle,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.Center).rectTransform.sizeDelta = new Vector2(680f, 48f);

            _body = QhysicsUiBuilder.Label(face.transform, "Body", Tips[0], QhysicsUiStyle.FontBody,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.Center);
            _body.rectTransform.sizeDelta = new Vector2(660f, 140f);
            _body.textWrappingMode = TextWrappingModes.Normal;

            var row = new GameObject("Buttons", typeof(RectTransform));
            row.transform.SetParent(face.transform, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(680f, 88f);
            QhysicsUiBuilder.LayoutHorizontal(rowRt, 16f);

            QhysicsUiBuilder.ChipButton(row.transform, "Next", "Next", new Vector2(200f, 80f), Next);
            QhysicsUiBuilder.ChipButton(row.transform, "Skip", "Skip tips", new Vector2(200f, 80f), Dismiss);

            _step = 0;
            Refresh();
            _ready = true;

            bool dismissed = PlayerPrefs.GetInt(PrefKey, 0) == 1;
            if (dismissed || !Application.isPlaying)
                SetVisible(false);
            else
                SetVisible(true);
        }

        void LateUpdate()
        {
            if (!_ready || _canvas == null || !_canvas.gameObject.activeSelf)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;
            Vector3 fwd = _cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 pos = _cam.transform.position + fwd * 1.15f + Vector3.up * 0.05f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        void Next()
        {
            _step++;
            if (_step >= Tips.Length)
            {
                Dismiss();
                return;
            }
            Refresh();
        }

        void Refresh()
        {
            if (_body != null && _step >= 0 && _step < Tips.Length)
                _body.text = Tips[_step];
        }

        public void Dismiss()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
            SetVisible(false);
        }

        public void SetVisible(bool on)
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(on);
        }

        /// <summary>Editor / pause menu helper to show tips again.</summary>
        public void ResetAndShow()
        {
            PlayerPrefs.SetInt(PrefKey, 0);
            _step = 0;
            Refresh();
            SetVisible(true);
        }
    }
}

