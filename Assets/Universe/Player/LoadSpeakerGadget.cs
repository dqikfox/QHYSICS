using UnityEngine;
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
    /// Grabbable CIRCUIT speaker: while near an InductionCircuit, plays a tone from classical |I|.
    /// Honesty: procedural sine |I| proxy — NOT a real voice coil, not Lorentz force audio, not AC spectrum.
    /// Does not set R_load (use CIRCUIT Resistor). XR activate / N cycles gain; P steps backward.
    /// Desktop LMB/scroll via IDesktopActivatable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class LoadSpeakerGadget : MonoBehaviour, IDesktopActivatable
    {
        public const string RootName = "Gadget_Speaker";
        public const string Honesty = "Procedural sine from classical |I|. NOT a real voice coil / Lorentz transducer.";

        // MUTE / LO / MID / HI gain multipliers on NormalizedLoadCurrent
        static readonly float[] Gains = { 0f, 0.35f, 0.7f, 1f };
        static readonly string[] PresetLabels = { "MUTE", "LO", "MID", "HI" };

        [SerializeField, Tooltip("Tip offset in local space (meters) used for nearest-circuit binding.")]
        Vector3 tipLocal = new Vector3(0f, 0.04f, 0f);

        [SerializeField, Tooltip("Max distance (m) to stay linked and drive audio.")]
        float connectWithinMeters = 0.85f;

        [SerializeField, Tooltip("Base tone Hz at idle / tiny |I|.")]
        float baseHz = 220f;

        [SerializeField, Tooltip("Extra Hz added at NormalizedLoadCurrent = 1.")]
        float hzPerNorm = 660f;

        [SerializeField, Tooltip("Master volume scale at gain=HI and NormalizedLoadCurrent=1.")]
        float maxVolume = 0.55f;

        TextMeshPro _readout;
        InductionCircuit _circuit;
        InductionCircuit[] _circuits;
        AudioSource _audio;
        AudioClip _tone;
        float _refreshAt;
        float _cacheAt;
        float _inputCooldown;
        int _presetIndex = 2; // default MID
        bool _linked;
        XRGrabInteractable _grab;
        Renderer _coneRenderer;
        Material _mat;

        public Vector3 TipWorld => transform.TransformPoint(tipLocal);
        public float ActiveGain => Gains[Mathf.Clamp(_presetIndex, 0, Gains.Length - 1)];
        public string ActiveLabel => PresetLabels[Mathf.Clamp(_presetIndex, 0, PresetLabels.Length - 1)];

        public void EnsureBuilt()
        {
            EnsureAudio();
            EnsureVisual();
            if (_readout == null)
                BuildReadout();
            if (_grab == null)
                _grab = GetComponent<XRGrabInteractable>();
            WireGrab();
            EnsureCircuits(force: true);
            BindNearest();
            ApplyAudio(0f);
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
            StopTone();
            UnwireGrab();
        }

        void OnDestroy()
        {
            StopTone();
            UnwireGrab();
            if (_tone != null)
            {
                if (Application.isPlaying) Destroy(_tone);
                else DestroyImmediate(_tone);
                _tone = null;
            }
            if (_mat != null)
            {
                if (Application.isPlaying) Destroy(_mat);
                else DestroyImmediate(_mat);
                _mat = null;
            }
        }

        void Update()
        {
            if (Time.unscaledTime < _refreshAt)
                return;
            _refreshAt = Time.unscaledTime + 0.05f;

            if (Time.unscaledTime >= _cacheAt || _circuits == null)
                EnsureCircuits(force: false);

            BindNearest();
            if (_circuit == null)
                EnsureLabCircuit();

            float glow = 0f;
            _linked = false;
            if (_circuit != null)
            {
                float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
                if (dist <= connectWithinMeters)
                {
                    _linked = true;
                    glow = _circuit.NormalizedLoadCurrent;
                }
            }

            PollDesktopCycle();
            ApplyAudio(glow);
            ApplyVisual(glow);
            RefreshText();
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
            Cycle(1);
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
                Cycle(1);
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
            _presetIndex = (_presetIndex + delta + Gains.Length) % Gains.Length;
            float glow = _linked && _circuit != null ? _circuit.NormalizedLoadCurrent : 0f;
            ApplyAudio(glow);
            ApplyVisual(glow);
            RefreshText();
        }

        public void DesktopActivate(int delta) => Cycle(delta);

        void EnsureAudio()
        {
            if (_audio == null)
                _audio = GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = true;
            _audio.spatialBlend = 1f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 0.3f;
            _audio.maxDistance = 6f;
            _audio.dopplerLevel = 0f;
            _audio.volume = 0f;
            if (_tone == null)
                _tone = BuildSineClip(440f, 0.25f);
            if (_audio.clip != _tone)
                _audio.clip = _tone;
        }

        static AudioClip BuildSineClip(float hz, float seconds)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(256, Mathf.RoundToInt(sampleRate * seconds));
            float[] data = new float[samples];
            float step = (hz * 2f * Mathf.PI) / sampleRate;
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                data[i] = Mathf.Sin(phase) * 0.35f;
                phase += step;
                if (phase > Mathf.PI * 2f)
                    phase -= Mathf.PI * 2f;
            }
            var clip = AudioClip.Create("SpeakerTone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void ApplyAudio(float norm01)
        {
            EnsureAudio();
            if (_audio == null)
                return;

            float g = ActiveGain;
            float n = Mathf.Clamp01(norm01);
            float drive = g * n;
            if (!_linked || drive < 0.01f)
            {
                StopTone();
                return;
            }

            _audio.pitch = Mathf.Clamp((baseHz + hzPerNorm * n) / 440f, 0.35f, 2.5f);
            _audio.volume = Mathf.Clamp01(drive * maxVolume);
            if (!_audio.isPlaying)
                _audio.Play();
        }

        void StopTone()
        {
            if (_audio == null)
                return;
            if (_audio.isPlaying)
                _audio.Stop();
            _audio.volume = 0f;
        }

        void EnsureVisual()
        {
            if (_coneRenderer == null)
            {
                Transform cone = transform.Find("Cone");
                if (cone != null)
                    _coneRenderer = cone.GetComponent<Renderer>();
                if (_coneRenderer == null)
                    _coneRenderer = GetComponent<Renderer>();
            }

            if (_coneRenderer == null)
            {
                var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.name = "Cone";
                cone.transform.SetParent(transform, false);
                cone.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                cone.transform.localScale = new Vector3(0.07f, 0.015f, 0.07f);
                Object.Destroy(cone.GetComponent<Collider>());
                _coneRenderer = cone.GetComponent<Renderer>();
            }

            if (_mat == null && _coneRenderer != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(sh);
                Color baseC = new Color(0.25f, 0.28f, 0.32f, 1f);
                if (_mat.HasProperty("_BaseColor"))
                    _mat.SetColor("_BaseColor", baseC);
                _mat.color = baseC;
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", Color.black);
                }
                _coneRenderer.sharedMaterial = _mat;
            }
        }

        void ApplyVisual(float norm01)
        {
            if (_mat == null)
                EnsureVisual();
            if (_mat == null)
                return;
            float g = ActiveGain * Mathf.Clamp01(norm01);
            Color e = new Color(0.35f, 0.85f, 1f) * (g * 2.2f);
            if (_mat.HasProperty("_EmissionColor"))
            {
                _mat.EnableKeyword("_EMISSION");
                _mat.SetColor("_EmissionColor", e);
            }
        }

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
            _readout.fontSize = 26f;
            _readout.alignment = TextAlignmentOptions.Center;
            _readout.color = new Color(0.45f, 0.9f, 1f, 1f);
            _readout.textWrappingMode = TextWrappingModes.Normal;
            _readout.rectTransform.sizeDelta = new Vector2(30f, 18f);
            _readout.text = "SPEAKER\nseeking I...";
        }

        void RefreshText()
        {
            if (_readout == null)
                return;

            if (_circuit == null)
            {
                _readout.text = "SPEAKER " + ActiveLabel + "\nno InductionCircuit\nEnter Sandbox / Induction";
                return;
            }

            float dist = Vector3.Distance(TipWorld, _circuit.transform.position);
            string target = _circuit.name;
            if (target.Length > 18)
                target = target.Substring(0, 16) + "..";

            string link = _linked ? "ON" : (dist > connectWithinMeters ? "far" : "near");
            float i = _linked ? _circuit.CurrentAmperes : 0f;
            _readout.text =
                "SPEAKER " + ActiveLabel + "\n"
                + link + " I " + FormatAmps(i) + "\n"
                + target + " " + dist.ToString("0.00") + "m\n"
                + "N/P LMB/scroll gain\n"
                + "[sine |I| proxy]";
        }

        static string FormatAmps(float a)
        {
            float abs = Mathf.Abs(a);
            if (abs < 1e-4f)
                return "0 A";
            if (abs < 1e-2f)
                return (a * 1000f).ToString("0.##") + " mA";
            return a.ToString("0.####") + " A";
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(TipWorld, 0.015f);
            Gizmos.DrawLine(transform.position, TipWorld);
        }
#endif
    }
}
