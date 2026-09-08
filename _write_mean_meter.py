# -*- coding: utf-8 -*-
"""Weekday slice: MEASURE Mean Meter (classical windowed mean Emf/I)."""
from pathlib import Path
import uuid

ROOT = Path(r"C:\Users\KING\projects\QHYSICS")
PLAYER = ROOT / "Assets" / "Universe" / "Player"
UI = ROOT / "Assets" / "Universe" / "UI"

CS = r"""using UnityEngine;
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
    /// Grabbable MEASURE mean meter: while near an InductionCircuit, measures
    /// classical time-weighted windowed mean Emf (DCV), mean I (DCI), and mean |Emf|
    /// (MAV). Honesty: ideal windowed mean Emf/I from classical InductionCircuit
    /// samples - NOT real DMM DC mode, not integrating ADC, not true-RMS, not
    /// calibrated offset meter. CLR zeros accumulators then snaps to DCV.
    /// XR activate / N cycles; P steps back. Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(12)]
    public sealed class LoadMeanMeterGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_MeanMeter";
        public const string Honesty =
            "Ideal windowed mean Emf/I from classical InductionCircuit samples. NOT real DMM DC mode, not integrating ADC, not true-RMS, not calibrated offset meter.";

        enum Mode { Dcv, Dci, Mav, Clr }

        static readonly string[] PresetLabels = { "DCV", "DCI", "MAV", "CLR" };
        static readonly Mode[] PresetModes = { Mode.Dcv, Mode.Dci, Mode.Mav, Mode.Clr };

        const float Floor = 1e-12f;

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and sample.")]
        float connectWithinMeters = 0.85f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex; // default DCV
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _bodyRenderer;
        Material _mat;
        Material _faceMat;

        float _sumEmf;
        float _sumI;
        float _sumAbsEmf;
        float _windowSeconds;
        float _liveDcv;
        float _liveDci;
        float _liveMav;
        float _glow;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];
        Mode ActiveMode => PresetModes[Mathf.Clamp(_presetIndex, 0, PresetModes.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCircuits(force: true);
            BindNearest();
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
            DestroyMat(ref _mat);
            DestroyMat(ref _faceMat);
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
            if (Time.unscaledTime >= _cacheAt || _circuits == null)
                EnsureCircuits(force: false);

            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();

            SampleMean();

            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 0.08f;
                PollDesktopCycle();
                ApplyVisual();
                RefreshText();
            }
        }

        void ResetAccumulators()
        {
            _sumEmf = 0f;
            _sumI = 0f;
            _sumAbsEmf = 0f;
            _windowSeconds = 0f;
            _liveDcv = 0f;
            _liveDci = 0f;
            _liveMav = 0f;
            _glow = 0f;
        }

        void SampleMean()
        {
            _linked = false;
            if (_circuit == null)
                return;

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            if (dist > connectWithinMeters)
                return;

            _linked = true;
            float dt = Time.timeScale <= 0f ? 0f : Time.unscaledDeltaTime;
            if (dt <= Floor)
                return;

            float emf = _circuit.EmfVolts;
            float cur = _circuit.CurrentAmperes;
            _sumEmf += emf * dt;
            _sumI += cur * dt;
            _sumAbsEmf += Mathf.Abs(emf) * dt;
            _windowSeconds += dt;

            if (_windowSeconds > Floor)
            {
                _liveDcv = _sumEmf / _windowSeconds;
                _liveDci = _sumI / _windowSeconds;
                _liveMav = _sumAbsEmf / _windowSeconds;
            }
            else
            {
                _liveDcv = 0f;
                _liveDci = 0f;
                _liveMav = 0f;
            }

            _glow = Mathf.Clamp01(Mathf.Max(Mathf.Abs(_liveDcv), Mathf.Abs(_liveDci) * 10f, _liveMav) / 2f);
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
            Cycle(+1);
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
            if ((cam.transform.position - TipWorld).sqrMagnitude > 1.44f)
                return;
            if (kb.nKey.wasPressedThisFrame)
            {
                Cycle(+1);
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
            if (PresetLabels.Length == 0)
                return;
            _presetIndex = (_presetIndex + delta) % PresetLabels.Length;
            if (_presetIndex < 0)
                _presetIndex += PresetLabels.Length;

            if (ActiveMode == Mode.Clr)
            {
                ResetAccumulators();
                for (int i = 0; i < PresetModes.Length; i++)
                {
                    if (PresetModes[i] == Mode.Dcv)
                    {
                        _presetIndex = i;
                        break;
                    }
                }
            }

            ApplyVisual();
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsureCircuits(bool force)
        {
            _cacheAt = Time.unscaledTime + 1.25f;
            if (!force && _circuits != null && _circuits.Length > 0)
            {
                for (int i = 0; i < _circuits.Length; i++)
                {
                    if (_circuits[i] != null)
                        return;
                }
            }
            _circuits = Object.FindObjectsByType<InductionCircuit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        void BindNearest()
        {
            if (_circuits == null || _circuits.Length == 0)
            {
                _circuit = null;
                return;
            }

            Vector3 tip = TipWorld;
            InductionCircuit best = null;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < _circuits.Length; i++)
            {
                InductionCircuit c = _circuits[i];
                if (c == null || !c.isActiveAndEnabled)
                    continue;
                if (c.transform == transform || c.transform.IsChildOf(transform))
                    continue;
                float sq = (c.transform.position - tip).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = c;
                }
            }
            _circuit = best;
        }

        void EnsureLabCircuit()
        {
            if (_circuit != null)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            EnsureCircuits(force: true);
            BindNearest();
        }

        void EnsureVisual()
        {
            if (_bodyRenderer == null)
                _bodyRenderer = GetComponent<Renderer>();

            if (_mat == null && _bodyRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                Color baseC = new Color(0.22f, 0.48f, 0.52f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_Metallic"))
                    _mat.SetFloat("_Metallic", 0.48f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.42f);
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", Color.black);
                }
                _bodyRenderer.sharedMaterial = _mat;
            }

            Transform face = transform.Find("Face");
            if (face == null)
            {
                var faceGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                faceGo.name = "Face";
                faceGo.transform.SetParent(transform, false);
                faceGo.transform.localPosition = new Vector3(0f, 0.03f, 0.01f);
                faceGo.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                faceGo.transform.localScale = new Vector3(0.11f, 0.05f, 0.01f);
                Object.Destroy(faceGo.GetComponent<Collider>());
                var fr = faceGo.GetComponent<Renderer>();
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _faceMat = new Material(sh);
                Color faceC = new Color(0.04f, 0.12f, 0.14f, 1f);
                if (_faceMat.HasProperty("_BaseColor"))
                    _faceMat.SetColor("_BaseColor", faceC);
                _faceMat.color = faceC;
                fr.sharedMaterial = _faceMat;
            }
            else if (_faceMat == null)
            {
                var fr = face.GetComponent<Renderer>();
                if (fr != null)
                    _faceMat = fr.sharedMaterial;
            }
        }

        void ApplyVisual()
        {
            if (_mat == null)
                EnsureVisual();
            if (_mat == null || !_mat.HasProperty("_EmissionColor"))
                return;
            _mat.EnableKeyword("_EMISSION");
            Color pulse = new Color(0.35f, 0.85f, 0.90f, 1f) * (0.08f + 1.2f * _glow);
            if (!_linked)
                pulse = Color.black;
            _mat.SetColor("_EmissionColor", pulse);
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
            _readout.color = new Color(0.72f, 0.95f, 0.98f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(34f, 22f);
            _readout.text = "DCV\nseeking circuit...";
        }

        // Signed — means can be negative (DCV / DCI).
        static string FormatSignedVolts(float v)
        {
            if (float.IsNaN(v))
                return "---";
            float a = Mathf.Abs(v);
            if (a < 1e-6f)
                return "0 V";
            if (a < 1e-3f)
                return (v * 1e6f).ToString("+0.00;-0.00;0.00") + " uV";
            if (a < 1f)
                return (v * 1e3f).ToString("+0.00;-0.00;0.00") + " mV";
            return v.ToString("+0.000;-0.000;0.000") + " V";
        }

        static string FormatSignedAmps(float a)
        {
            if (float.IsNaN(a))
                return "---";
            float x = Mathf.Abs(a);
            if (x < 1e-6f)
                return "0 A";
            if (x < 1e-3f)
                return (a * 1e6f).ToString("+0.00;-0.00;0.00") + " uA";
            if (x < 1f)
                return (a * 1e3f).ToString("+0.00;-0.00;0.00") + " mA";
            return a.ToString("+0.000;-0.000;0.000") + " A";
        }

        // Unsigned — MAV is always >= 0.
        static string FormatAbsVolts(float v)
        {
            if (float.IsNaN(v) || v < 0f)
                return "---";
            float a = Mathf.Abs(v);
            if (a < 1e-6f)
                return "0 V";
            if (a < 1e-3f)
                return (a * 1e6f).ToString("0.00") + " uV";
            if (a < 1f)
                return (a * 1e3f).ToString("0.00") + " mV";
            return a.ToString("0.000") + " V";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            string label = ActiveLabel;
            if (_circuit == null)
            {
                _readout.text = "MEAN " + label + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON circuit" : (dist > connectWithinMeters ? "far" : "near");
            string win = "t=" + _windowSeconds.ToString("0.0") + "s";
            string line;
            switch (ActiveMode)
            {
                case Mode.Dci:
                    line = "DCI " + FormatSignedAmps(_liveDci) + "\nmean I  " + win;
                    break;
                case Mode.Mav:
                    line = "MAV " + FormatAbsVolts(_liveMav) + "\nmean |Emf|  " + win;
                    break;
                case Mode.Clr:
                    line = "CLR -> DCV";
                    break;
                default:
                    line = "DCV " + FormatSignedVolts(_liveDcv) + "\nmean Emf  " + win;
                    break;
            }

            _readout.text = "MEAN " + label + "\n" + line + "\n" + target + " [" + link + "]\n" + Honesty.Substring(0, Mathf.Min(48, Honesty.Length)) + "..";
        }
    }
}
"""

cs_path = PLAYER / "LoadMeanMeterGadget.cs"
cs_path.write_text(CS, encoding="utf-8")
guid = uuid.uuid4().hex
meta_path = PLAYER / "LoadMeanMeterGadget.cs.meta"
meta_path.write_text(
    "fileFormatVersion: 2\n"
    f"guid: {guid}\n"
    "MonoImporter:\n"
    "  externalObjects: {}\n"
    "  serializedVersion: 2\n"
    "  defaultReferences: []\n"
    "  executionOrder: 12\n"
    "  icon: {instanceID: 0}\n"
    "  userData: \n"
    "  assetBundleName: \n"
    "  assetBundleVariant: \n",
    encoding="utf-8",
)
print("wrote", cs_path)
print("wrote", meta_path, "guid", guid)

# --- patch QhysicsGadgets.cs ---
gadgets = PLAYER / "QhysicsGadgets.cs"
g = gadgets.read_text(encoding="utf-8")
needle = (
    '            if (key == "peak to peak meter" || key == "peaktopeakmeter" || key == "peak-to-peak meter" || key == "peak-to-peak" || key == "peak to peak" || key == "vpp meter" || key == "vppmeter" || key == "pp meter" || key == "ppmeter" || key == "ptp meter" || key == "ptpmeter")\n'
    "                return SpawnLoadPeakToPeakMeter(worldPos);\n"
)
insert = needle + (
    "\n"
    '            if (key == "mean meter" || key == "meanmeter" || key == "mean" || key == "dc meter" || key == "dcmeter" || key == "dc offset meter" || key == "dcoffsetmeter" || key == "average meter" || key == "averagemeter" || key == "avg meter" || key == "avgmeter")\n'
    "                return SpawnLoadMeanMeter(worldPos);\n"
)
if "SpawnLoadMeanMeter" not in g:
    if needle not in g:
        raise SystemExit("gadgets Peak-to-Peak key missing")
    g = g.replace(needle, insert, 1)

spawn_needle = (
    "        static GameObject SpawnLoadPeakToPeakMeter(Vector3 worldPos)\n"
    "        {\n"
    "            // Real MEASURE tool: ideal Emf Vpp / I Ipp from classical InductionCircuit soft-window min/max.\n"
    '            var go = SpawnPrimitiveProxy("PeakToPeakMeter", worldPos, new Color(0.42f, 0.32f, 0.58f));\n'
    "            go.name = LoadPeakToPeakMeterGadget.RootName;\n"
    "            go.transform.localScale = new Vector3(0.12f, 0.05f, 0.09f);\n"
    "            var pp = go.GetComponent<LoadPeakToPeakMeterGadget>();\n"
    "            if (pp == null)\n"
    "                pp = go.AddComponent<LoadPeakToPeakMeterGadget>();\n"
    "            pp.EnsureBuilt();\n"
    "            return go;\n"
    "        }\n"
)
spawn_new = spawn_needle + (
    "\n"
    "        static GameObject SpawnLoadMeanMeter(Vector3 worldPos)\n"
    "        {\n"
    "            // Real MEASURE tool: ideal windowed mean Emf/I from classical InductionCircuit samples.\n"
    '            var go = SpawnPrimitiveProxy("MeanMeter", worldPos, new Color(0.22f, 0.48f, 0.52f));\n'
    "            go.name = LoadMeanMeterGadget.RootName;\n"
    "            go.transform.localScale = new Vector3(0.12f, 0.05f, 0.09f);\n"
    "            var mm = go.GetComponent<LoadMeanMeterGadget>();\n"
    "            if (mm == null)\n"
    "                mm = go.AddComponent<LoadMeanMeterGadget>();\n"
    "            mm.EnsureBuilt();\n"
    "            return go;\n"
    "        }\n"
)
if "static GameObject SpawnLoadMeanMeter" not in g:
    if spawn_needle not in g:
        raise SystemExit("gadgets SpawnLoadPeakToPeakMeter missing")
    g = g.replace(spawn_needle, spawn_new, 1)
gadgets.write_text(g, encoding="utf-8")
print("patched QhysicsGadgets.cs")

# --- toolbelt ---
toolbelt = UI / "QhysicsToolbelt.cs"
t = toolbelt.read_text(encoding="utf-8")
old_tb = '"Peak-to-Peak Meter", "Cubit Rod"'
new_tb = '"Peak-to-Peak Meter", "Mean Meter", "Cubit Rod"'
if "Mean Meter" not in t:
    if old_tb not in t:
        raise SystemExit("toolbelt Peak-to-Peak Meter slot missing")
    t = t.replace(old_tb, new_tb, 1)
    toolbelt.write_text(t, encoding="utf-8")
    print("patched QhysicsToolbelt.cs")
else:
    print("toolbelt already has Mean Meter")

# --- onboarding ---
onb = UI / "QhysicsOnboarding.cs"
o = onb.read_text(encoding="utf-8")
pp_marker = "MEASURE Peak-to-Peak Meter:"
if "MEASURE Mean Meter:" not in o:
    lines = o.splitlines(keepends=True)
    out = []
    inserted = False
    for line in lines:
        out.append(line)
        if not inserted and pp_marker in line:
            out.append(
                '            "MEASURE Mean Meter: hold near a Coil - classical windowed mean Emf (DCV) / mean I (DCI) / mean |Emf| (MAV) (N/P or VR trigger cycles DCV/DCI/MAV/CLR; CLR zeros accumulators; pair with Function Generator / Oscilloscope / Peak-to-Peak Meter / Crest Factor Meter; NOT real DMM DC mode / integrating ADC / true-RMS / calibrated offset meter).",\n'
            )
            inserted = True
    if not inserted:
        raise SystemExit("onboarding Peak-to-Peak Meter tip missing")
    o = "".join(out)

# Extend desktop-hold / chain lists that mention Peak-to-Peak Meter
o = o.replace(
    "Overshoot Meter/Peak-to-Peak Meter/Multimeter",
    "Overshoot Meter/Peak-to-Peak Meter/Mean Meter/Multimeter",
)
o = o.replace(
    "Rise/Fall Meter/Overshoot Meter/Peak-to-Peak Meter/Multimeter",
    "Rise/Fall Meter/Overshoot Meter/Peak-to-Peak Meter/Mean Meter/Multimeter",
)
o = o.replace(
    "Slew Rate Meter/Rise/Fall Meter/Overshoot Meter/Peak-to-Peak Meter/Multimeter",
    "Slew Rate Meter/Rise/Fall Meter/Overshoot Meter/Peak-to-Peak Meter/Mean Meter/Multimeter",
)
o = o.replace(
    "Peak-to-Peak Meter/Multimeter/Probe",
    "Peak-to-Peak Meter/Mean Meter/Multimeter/Probe",
)
o = o.replace(
    "Peak-to-Peak Meter/Multimeter/Probe/Stopwatch",
    "Peak-to-Peak Meter/Mean Meter/Multimeter/Probe/Stopwatch",
)
onb.write_text(o, encoding="utf-8")
print("patched QhysicsOnboarding.cs")

# --- PLAY.md ---
play = ROOT / "QHYSICS_PLAY.md"
p = play.read_text(encoding="utf-8")
p = p.replace(
    "Overshoot Meter, Peak-to-Peak Meter, Probe",
    "Overshoot Meter, Peak-to-Peak Meter, Mean Meter, Probe",
)
p = p.replace(
    "Rise/Fall Meter, Overshoot Meter, Peak-to-Peak Meter, Probe",
    "Rise/Fall Meter, Overshoot Meter, Peak-to-Peak Meter, Mean Meter, Probe",
)
p = p.replace(
    "Slew Rate Meter, Rise/Fall Meter, Overshoot Meter, Peak-to-Peak Meter, Probe",
    "Slew Rate Meter, Rise/Fall Meter, Overshoot Meter, Peak-to-Peak Meter, Mean Meter, Probe",
)
p = p.replace(
    "Overshoot Meter / Peak-to-Peak Meter / Multimeter",
    "Overshoot Meter / Peak-to-Peak Meter / Mean Meter / Multimeter",
)
p = p.replace(
    "Rise/Fall Meter / Overshoot Meter / Peak-to-Peak Meter / Multimeter",
    "Rise/Fall Meter / Overshoot Meter / Peak-to-Peak Meter / Mean Meter / Multimeter",
)
p = p.replace(
    "Slew Rate Meter / Rise/Fall Meter / Overshoot Meter / Peak-to-Peak Meter / Multimeter",
    "Slew Rate Meter / Rise/Fall Meter / Overshoot Meter / Peak-to-Peak Meter / Mean Meter / Multimeter",
)
p = p.replace(
    "Peak-to-Peak Meter / Multimeter / Galvanometer",
    "Peak-to-Peak Meter / Mean Meter / Multimeter / Galvanometer",
)
section_marker = "### MEASURE Peak-to-Peak Meter (toolbelt MEASURE)"
if "### MEASURE Mean Meter" not in p:
    idx = p.find(section_marker)
    if idx < 0:
        raise SystemExit("PLAY Peak-to-Peak Meter section missing")
    next_h = p.find("\n### ", idx + len(section_marker))
    if next_h < 0:
        raise SystemExit("PLAY next section after Peak-to-Peak missing")
    section_new = (
        "### MEASURE Mean Meter (toolbelt MEASURE)\n"
        "1. Enter Sandbox / Induction.\n"
        "2. Toolbelt **MEASURE -> Mean Meter**; grab it.\n"
        "3. Hold near Coil; classical windowed mean Emf (DCV), mean I (DCI), and mean |Emf| (MAV) "
        "(pair with BUILD Function Generator; Oscilloscope / Peak-to-Peak Meter / Crest Factor Meter for cross-check). "
        "A biased swing should shift DCV away from zero.\n"
        "4. **N/P**, VR trigger, or desktop **LMB/scroll** cycles DCV / DCI / MAV / CLR "
        "(default DCV; CLR zeros accumulators, then snaps to DCV). "
        "Honesty: ideal windowed mean Emf/I from classical InductionCircuit samples - NOT real DMM DC mode, "
        "not integrating ADC, not true-RMS, not calibrated offset meter.\n"
        "\n"
    )
    p = p[: next_h + 1] + section_new + p[next_h + 1 :]
play.write_text(p, encoding="utf-8")
print("patched QHYSICS_PLAY.md")
print("DONE")
