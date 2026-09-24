using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using RealityEngine.Physics.Electromagnetism;
using RealityEngine.Player;
using RealityEngine.Survey;
using RealityEngine.Visualization;

namespace RealityEngine.UI
{
    /// <summary>
    /// Inspect panel (context strip): hover = summary, hold = detail. Live classical lab stats + hints.
    /// Desktop uses DesktopInteractor hover/held; VR uses XR interactor hover/select.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(202)]
    public sealed class QhysicsContextStrip : MonoBehaviour
    {
        public const string RootName = "QhysicsContextStrip";

        Canvas _canvas;
        TextMeshProUGUI _name;
        TextMeshProUGUI _stats;
        TextMeshProUGUI _detail;
        TextMeshProUGUI _hints;
        Camera _cam;
        float _nextPoll;
        float _nextInteractorRefresh;
        XRBaseInteractor[] _interactors;
        Transform _followTarget;
        DesktopInteractor _desktop;

        public static QhysicsContextStrip Ensure(Transform parent)
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
                var c = existing.GetComponent<QhysicsContextStrip>();
                if (c == null)
                    c = existing.gameObject.AddComponent<QhysicsContextStrip>();
                if (c._canvas == null)
                    c.Build();
                return c;
            }
            var root = new GameObject(RootName);
            if (parent != null)
                root.transform.SetParent(parent, false);
            var comp = root.AddComponent<QhysicsContextStrip>();
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

            _canvas = QhysicsUiBuilder.CreateWorldCanvas("Canvas", transform, new Vector2(540f, 200f));
            QhysicsUiBuilder.WireEventCamera(_canvas);
            var face = QhysicsUiBuilder.BorderPanel(_canvas.transform, "Panel", new Vector2(520f, 180f));
            face.raycastTarget = false;
            face.transform.parent.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

            var tag = QhysicsUiBuilder.Label(face.transform, "Tag", "INSPECT", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            tag.rectTransform.anchoredPosition = new Vector2(-180f, 64f);
            tag.rectTransform.sizeDelta = new Vector2(120f, 22f);

            _name = QhysicsUiBuilder.Label(face.transform, "Name", "-", QhysicsUiStyle.FontBody,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.MidlineLeft);
            _name.rectTransform.anchoredPosition = new Vector2(20f, 58f);
            _name.rectTransform.sizeDelta = new Vector2(460f, 32f);

            _stats = QhysicsUiBuilder.Label(face.transform, "Stats", "hover for summary", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextMuted, TextAlignmentOptions.MidlineLeft);
            _stats.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            _stats.rectTransform.sizeDelta = new Vector2(480f, 26f);

            _detail = QhysicsUiBuilder.Label(face.transform, "Detail", "", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.TextPrimary, TextAlignmentOptions.MidlineLeft);
            _detail.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            _detail.rectTransform.sizeDelta = new Vector2(480f, 26f);

            _hints = QhysicsUiBuilder.Label(face.transform, "Hints", "Grip/E grab | Ray to select", QhysicsUiStyle.FontSmall,
                QhysicsUiStyle.AccentInfo, TextAlignmentOptions.MidlineLeft);
            _hints.rectTransform.anchoredPosition = new Vector2(0f, -48f);
            _hints.rectTransform.sizeDelta = new Vector2(480f, 26f);

            _canvas.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + 0.08f;
                PollInteractables();
            }

            if (_canvas == null || !_canvas.gameObject.activeSelf)
                return;
            if (_cam == null)
                _cam = QhysicsUiBuilder.ResolveXrCamera();
            if (_cam == null)
                return;

            Vector3 anchor = _followTarget != null ? _followTarget.position : _cam.transform.position + _cam.transform.forward;
            Vector3 pos = anchor + Vector3.up * 0.18f;
            Vector3 toCam = _cam.transform.position - pos;
            if (toCam.sqrMagnitude > 1e-4f)
                pos += toCam.normalized * 0.05f;
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            QhysicsUiBuilder.FaceCamera(transform, _cam);
            QhysicsUiBuilder.WireEventCamera(_canvas);
        }

        void PollInteractables()
        {
            Transform target = null;
            bool selected = false;

            if (_desktop == null)
                _desktop = Object.FindFirstObjectByType<DesktopInteractor>(FindObjectsInactive.Exclude);
            if (_desktop != null)
            {
                if (_desktop.Held != null)
                {
                    target = _desktop.Held;
                    selected = true;
                }
                else if (_desktop.HoverTarget != null)
                {
                    target = _desktop.HoverTarget;
                }
            }

            if (target == null)
            {
                if (_interactors == null || Time.unscaledTime >= _nextInteractorRefresh)
                {
                    _nextInteractorRefresh = Time.unscaledTime + 1f;
                    _interactors = Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
                }

                var interactors = _interactors;
                if (interactors != null)
                {
                    for (int i = 0; i < interactors.Length; i++)
                    {
                        var interactor = interactors[i];
                        if (interactor == null)
                            continue;
                        var sels = interactor.interactablesSelected;
                        if (sels != null && sels.Count > 0)
                        {
                            Component c = sels[0] as Component;
                            if (c != null)
                            {
                                target = c.transform;
                                selected = true;
                                break;
                            }
                        }
                    }

                    if (target == null)
                    {
                        for (int i = 0; i < interactors.Length; i++)
                        {
                            var interactor = interactors[i];
                            if (interactor == null)
                                continue;
                            var hovered = interactor.interactablesHovered;
                            if (hovered != null && hovered.Count > 0)
                            {
                                Component c = hovered[0] as Component;
                                if (c != null)
                                {
                                    target = c.transform;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (target == null)
            {
                if (_canvas != null)
                    _canvas.gameObject.SetActive(false);
                _followTarget = null;
                return;
            }

            _followTarget = target;
            if (_name != null)
                _name.text = PrettyName(target.gameObject.name);
            if (_stats != null)
                _stats.text = FormatStats(target, selected);
            if (_detail != null)
            {
                _detail.text = selected ? FormatDetail(target) : "hold to expand detail";
                _detail.color = selected ? QhysicsUiStyle.TextPrimary : QhysicsUiStyle.TextMuted;
            }
            if (_hints != null)
                _hints.text = FormatHints(target, selected);
            if (_canvas != null)
                _canvas.gameObject.SetActive(true);
        }

        static string PrettyName(string n)
        {
            if (string.IsNullOrEmpty(n))
                return "-";
            if (n.StartsWith("Gadget_"))
                return n.Substring(7);
            return n;
        }

        static string FormatStats(Transform t, bool selected)
        {
            if (t == null)
                return "-";

            var circuit = t.GetComponentInParent<InductionCircuit>();
            if (circuit == null)
                circuit = Object.FindFirstObjectByType<InductionCircuit>(FindObjectsInactive.Exclude);

            var magnet = t.GetComponentInParent<MagneticDipole>();
            var coil = t.GetComponentInParent<InductionCoil>();
            var probe = t.GetComponentInParent<FieldProbe>();
            var meter = t.GetComponentInParent<MultimeterProbe>();
            var sw = t.GetComponentInParent<ExperimentStopwatch>();
            var rod = t.GetComponentInParent<CubitRod>();
            var lensHand = t.GetComponentInParent<FieldLensHandheld>();
            var lens = t.GetComponentInParent<FieldLens>();
            if (lens == null && lensHand != null)
                lens = Object.FindFirstObjectByType<FieldLens>(FindObjectsInactive.Include);

            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            string mass = rb != null ? rb.mass.ToString("0.###") + " kg" : null;
            string state = selected ? "held" : "hover";

            if (meter != null || (coil != null && magnet == null && probe == null))
            {
                if (circuit != null)
                {
                    return "EMF " + FormatEmf(circuit.EmfVolts)
                        + " | I " + circuit.CurrentAmperes.ToString("0.####") + " A"
                        + " | Phi " + FormatFlux(circuit.FluxWebers)
                        + " | " + state;
                }
            }

            if (probe != null)
            {
                Vector3 tip = probe.TipWorld;
                Vector3 B = SampleB(tip);
                return FormatB(B) + " @ tip | " + state + " | classical dipole";
            }

            if (magnet != null)
            {
                Vector3 B = magnet.CalculateFieldAt(magnet.transform.position + Vector3.up * 0.05f);
                return "m " + magnet.magneticMoment.ToString("0.##") + " A*m^2"
                    + " | " + FormatB(B) + " nearby"
                    + (mass != null ? " | " + mass : "")
                    + " | " + state;
            }

            if (coil != null && circuit != null)
            {
                return "EMF " + FormatEmf(circuit.EmfVolts)
                    + " | dPhi/dt " + circuit.FluxRateWebersPerSecond.ToString("0.####")
                    + " | " + state;
            }

            if (sw != null)
                return "t " + FormatTime(sw.DisplaySeconds) + (sw.IsRunning ? " run" : " stop") + " | wall-clock | " + state;

            if (rod != null)
                return "cubit rod | survey measure | " + (mass != null ? mass + " | " : "") + state;

            if (lens != null || lensHand != null)
            {
                if (lens == null)
                    lens = Object.FindFirstObjectByType<FieldLens>(FindObjectsInactive.Include);
                if (lens != null)
                    return "layer " + lens.CurrentLayerName + " | " + lens.CurrentHonestyTag + " | " + state;
                return "Field Lens | " + state;
            }

            if (circuit != null && (t.name.IndexOf("Induction", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.name.IndexOf("Readout", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "EMF " + FormatEmf(circuit.EmfVolts)
                    + " | I " + circuit.CurrentAmperes.ToString("0.####") + " A | " + state;
            }

            if (mass != null)
                return "mass " + mass + " | " + state;
            return state + " | inspect";
        }

        static string FormatDetail(Transform t)
        {
            if (t == null)
                return "";
            var circuit = t.GetComponentInParent<InductionCircuit>();
            if (circuit == null)
                circuit = Object.FindFirstObjectByType<InductionCircuit>(FindObjectsInactive.Exclude);
            Rigidbody rb = t.GetComponentInParent<Rigidbody>();
            if (circuit != null)
            {
                return "R " + circuit.TotalResistanceOhms.ToString("0.##") + " ohm"
                    + " | Phi " + FormatFlux(circuit.FluxWebers)
                    + " | dPhi/dt " + circuit.FluxRateWebersPerSecond.ToString("0.####");
            }
            if (rb != null)
            {
                return "v " + rb.linearVelocity.magnitude.ToString("0.###") + " m/s"
                    + " | mass " + rb.mass.ToString("0.###") + " kg";
            }
            return "inspect focus";
        }

        static string FormatHints(Transform t, bool selected)
        {
            if (t.GetComponentInParent<FieldProbe>() != null)
                return selected ? "Move tip near magnet poles" : "Grab probe | sample B(r)";
            if (t.GetComponentInParent<MultimeterProbe>() != null)
                return selected ? "Sweep magnet through coil" : "Grab meter | live EMF/I/Phi";
            if (t.GetComponentInParent<ExperimentStopwatch>() != null)
                return selected ? "T toggle · Y reset · trigger" : "Grab stopwatch | time a run";
            if (t.GetComponentInParent<MagneticDipole>() != null)
                return selected ? "Pull through coil for EMF" : "Grab magnet | induction source";
            if (t.GetComponentInParent<InductionCoil>() != null)
                return "Move magnet through coil | Faraday loop";
            if (t.GetComponentInParent<FieldLensHandheld>() != null || t.GetComponentInParent<FieldLens>() != null)
                return selected ? "[ ] / N P cycle layers" : "Grab lens | peel representation";
            if (t.GetComponentInParent<CubitRod>() != null)
                return selected ? "Align to survey edges" : "Grab cubit | Giza measure";
            return selected ? "Rotate | Move | Inspect" : "Grip/E grab | Ray to select";
        }

        static Vector3 SampleB(Vector3 worldPoint)
        {
            var dipoles = Object.FindObjectsByType<MagneticDipole>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < dipoles.Length; i++)
            {
                if (dipoles[i] == null || !dipoles[i].isActive)
                    continue;
                sum += dipoles[i].CalculateFieldAt(worldPoint);
            }
            return sum;
        }

        static string FormatB(Vector3 B)
        {
            float mag = B.magnitude;
            if (mag >= 1f)
                return "|B| " + mag.ToString("0.###") + " T";
            if (mag >= 1e-3f)
                return "|B| " + (mag * 1e3f).ToString("0.###") + " mT";
            return "|B| " + (mag * 1e6f).ToString("0.###") + " uT";
        }

        static string FormatEmf(float v)
        {
            float a = Mathf.Abs(v);
            if (a >= 1f)
                return v.ToString("0.###") + " V";
            if (a >= 1e-3f)
                return (v * 1e3f).ToString("0.###") + " mV";
            return (v * 1e6f).ToString("0.###") + " uV";
        }

        static string FormatFlux(float phi)
        {
            float a = Mathf.Abs(phi);
            if (a >= 1f)
                return phi.ToString("0.####") + " Wb";
            if (a >= 1e-3f)
                return (phi * 1e3f).ToString("0.####") + " mWb";
            return (phi * 1e6f).ToString("0.####") + " uWb";
        }

        static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds - m * 60f;
            return m.ToString("00") + ":" + s.ToString("00.00");
        }
    }
}

