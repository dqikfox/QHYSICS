using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Reality Engine lab style: restyles Faraday Circuit Lab meshes to a dark
    /// graphite / copper scientific bench. Materials only — no gameplay, grab,
    /// collider, XR Origin, or breadboard-layout changes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class CircuitLabStyleApplier : MonoBehaviour
    {
        public const string MaterialPrefix = "RELab_";
        const string RimRootName = "LabStyleRim";
        const string LitShaderName = "Universal Render Pipeline/Lit";

        static Shader _lit;
        static Material _graphite;
        static Material _copper;
        static Material _housing;
        static Material _glass;
        static Material _filament;
        static Material _label;
        static Material _bench;
        static Material _anodized;
        static Texture2D _grid;
        static bool _loggedSkipTmp;
        static bool _loggedSkipElectron;
        static bool _loggedSkipGlow;

        readonly HashSet<MeshRenderer> _styled = new HashSet<MeshRenderer>();
        float _nextPass;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookSceneLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoApplyAfterSceneLoad()
        {
            EnsureApplied();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            EnsureApplied();
        }

        public static CircuitLabStyleApplier EnsureApplied()
        {
            CircuitLabStyleApplier existing = Object.FindFirstObjectByType<CircuitLabStyleApplier>(FindObjectsInactive.Include);
            if (existing == null)
            {
                CircuitLab lab = Object.FindFirstObjectByType<CircuitLab>(FindObjectsInactive.Include);
                GameObject host = lab != null ? lab.gameObject : GameObject.Find("RealityEngine");
                if (host == null)
                    host = new GameObject("CircuitLabStyle");
                existing = host.GetComponent<CircuitLabStyleApplier>();
                if (existing == null)
                    existing = host.AddComponent<CircuitLabStyleApplier>();
            }

            if (!existing.isActiveAndEnabled && existing.gameObject.activeInHierarchy)
                existing.enabled = true;

            existing.ApplyNow(false);
            return existing;
        }

        void Awake()
        {
            ApplyNow(false);
        }

        void Start()
        {
            ApplyNow(false);
            if (Application.isPlaying)
                StartCoroutine(ApplyAfterPegs());
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            if (Time.unscaledTime < _nextPass)
                return;
            _nextPass = Time.unscaledTime + 0.8f;
            ApplyNow(false);
        }

        IEnumerator ApplyAfterPegs()
        {
            yield return null;
            ApplyNow(false);
        }

        public void ApplyNow(bool force)
        {
            EnsureMaterials();
            StripCircuitTableRootRenderer();
            if (_lit == null)
            {
                Debug.LogError("CircuitLabStyleApplier: URP Lit shader not found; skipped (no pink Sprites/Default fallback).");
                return;
            }

            if (force)
                _styled.Clear();

            int styled = 0;
            int skipped = 0;

            List<Transform> roots = CollectRoots();
            for (int r = 0; r < roots.Count; r++)
            {
                Transform root = roots[r];
                if (root == null)
                    continue;
                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer mr = renderers[i];
                    if (mr == null)
                        continue;
                    if (StyleOne(mr, force))
                        styled++;
                    else
                        skipped++;
                }
            }

            EnsureCopperRim();
            if (force || styled > 0)
                Debug.Log("CircuitLabStyleApplier: styled " + styled + " MeshRenderers, skipped " + skipped + " (TMP/electrons/glow/unknown).");
        }

        static List<Transform> CollectRoots()
        {
            var roots = new List<Transform>();
            var seen = new HashSet<Transform>();

            CircuitLab[] labs = Object.FindObjectsByType<CircuitLab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < labs.Length; i++)
                AddRoot(roots, seen, labs[i] != null ? labs[i].transform : null);

            CircuitComponent[] comps = Object.FindObjectsByType<CircuitComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < comps.Length; i++)
                AddRoot(roots, seen, comps[i] != null ? comps[i].transform : null);

            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                    continue;
                string n = t.name;
                if (string.IsNullOrEmpty(n))
                    continue;
                string lower = n.ToLowerInvariant();
                if (lower.Contains("xr origin") || lower == "xr origin")
                    continue;
                if (lower.Contains("circuittable") ||
                    lower.Contains("breadboard") ||
                    lower.Contains("circuitlabhandle") ||
                    lower.StartsWith("dispenser") ||
                    lower == "peg" ||
                    lower.StartsWith("peg_"))
                {
                    AddRoot(roots, seen, t);
                }
            }

            return roots;
        }

        static void AddRoot(List<Transform> roots, HashSet<Transform> seen, Transform t)
        {
            if (t == null)
                return;
            if (IsUnderXrOrigin(t))
                return;

            if (!seen.Add(t))
                return;
            roots.Add(t);
        }

        static bool IsUnderXrOrigin(Transform t)
        {
            Transform p = t;
            while (p != null)
            {
                if (p.name == "XR Origin")
                    return true;
                p = p.parent;
            }
            return false;
        }

        bool StyleOne(MeshRenderer mr, bool force)
        {

            if (!force && _styled.Contains(mr))
                return false;

            if (ShouldSkip(mr, out string reason))
            {
                if (force)
                    LogSkipOnce(mr, reason);
                _styled.Add(mr);
                return false;
            }

            Kind kind = Classify(mr);
            if (kind == Kind.Skip)
            {
                _styled.Add(mr);
                return false;
            }

            Material mat = MaterialFor(kind);
            if (mat == null)
            {
                _styled.Add(mr);
                return false;
            }

            AssignKeepingSlotCount(mr, mat);
            _styled.Add(mr);
            return true;
        }

        static bool ShouldSkip(MeshRenderer mr, out string reason)
        {
            reason = null;
            GameObject go = mr.gameObject;
            string n = go.name;

            if (go.GetComponent<TMP_Text>() != null || go.GetComponent<TextMeshPro>() != null)
            {
                reason = "TMP label";
                return true;
            }

            if (!string.IsNullOrEmpty(n))
            {
                string lower = n.ToLowerInvariant();
                if (lower.StartsWith("label") || lower.Contains("labelcurrent") || lower.Contains("labelvoltage") || lower.Contains("labelresistance"))
                {
                    reason = "label";
                    return true;
                }
                if (lower.StartsWith("electron"))
                {
                    reason = "electron (gameplay color)";
                    return true;
                }
                if (lower == "glow" || lower.StartsWith("glow"))
                {
                    reason = "glow/light";
                    return true;
                }
            }

            if (go.GetComponent<Light>() != null)
            {
                reason = "light";
                return true;
            }

            Material[] slots = mr.sharedMaterials;
            if (slots == null)
                return false;
            for (int i = 0; i < slots.Length; i++)
            {
                Material s = slots[i];
                if (s == null)
                    continue;
                string sn = s.name;
                string shaderName = s.shader != null ? s.shader.name : "";
                if ((!string.IsNullOrEmpty(sn) && (sn.Contains("LiberationSans") || sn.Contains("TMP") || sn.Contains("Glyph"))) ||
                    shaderName.Contains("TextMeshPro") || shaderName.Contains("Text Mesh Pro"))
                {
                    reason = "TMP font material";
                    return true;
                }
            }

            return false;
        }

        enum Kind
        {
            Skip,
            Graphite,
            Copper,
            Housing,
            Glass,
            Filament,
            Label,
            Bench,
            Anodized
        }

        static Kind Classify(MeshRenderer mr)
        {
            string self = SafeLower(mr.gameObject.name);
            string path = BuildNamePath(mr.transform);
            string matHint = FirstMaterialHint(mr);

            if (self.StartsWith("labstylerim") || path.Contains("/labstylerim"))
                return Kind.Copper;

            if (self.Contains("filament"))
                return Kind.Filament;
            if (self == "bulb" || (self.Contains("bulb") && !self.Contains("component") && !self.Contains("wire")))
                return Kind.Glass;

            if (self.Contains("wireend") || self.Contains("wirebody") || self == "wire" ||
                self.Contains("componentwire") || self.Contains("componentlongwire") ||
                self.Contains("switchend"))
                return Kind.Copper;

            if (self.StartsWith("peg") || self == "peg")
                return Kind.Copper;

            if (self.Contains("breadboard"))
                return Kind.Bench;

            if (self.StartsWith("leg") || self.Contains("circuittable") || self == "table" ||
                self.Contains("workbench") || self.Contains("labbench"))
                return Kind.Graphite;

            if (self.Contains("handle"))
                return Kind.Anodized;

            if (self.Contains("dispenser"))
                return Kind.Housing;

            if (self.Contains("onsign") || self.Contains("offsign") || self.Contains("sign"))
                return Kind.Label;

            if (self.Contains("positive") || self.Contains("negative") || self.Contains("terminal"))
                return Kind.Copper;

            if (self.Contains("battery") || self.Contains("motor") || self.Contains("switch") ||
                self.Contains("rocker") || self.Contains("pivot") || self.Contains("buttonpress") ||
                self.Contains("blade") || self == "base" || self.StartsWith("cube") ||
                self.StartsWith("cylinder"))
                return Kind.Housing;

            if (path.Contains("breadboard"))
                return Kind.Bench;
            if (path.Contains("circuittable") || path.Contains("/leg"))
                return Kind.Graphite;
            if (path.Contains("componentwire") || path.Contains("longwire") || path.Contains("wirebody") || path.Contains("wireend"))
                return Kind.Copper;
            if (path.Contains("componentbulb") && (self.Contains("glass") || self == "bulb"))
                return Kind.Glass;
            if (path.Contains("componentbattery") || path.Contains("componentmotor") || path.Contains("componentswitch") ||
                path.Contains("componentbulb") || path.Contains("balloon") || path.Contains("timer") ||
                path.Contains("flute") || path.Contains("solar") || path.Contains("button"))
                return Kind.Housing;
            if (path.Contains("dispenser"))
                return Kind.Housing;
            if (path.Contains("peg"))
                return Kind.Copper;
            if (path.Contains("handle"))
                return Kind.Anodized;

            if (matHint.Contains("leather") || matHint.Contains("wood") || matHint.Contains("parquet") || matHint.Contains("oldleather"))
                return Kind.Bench;
            if (matHint.Contains("brass") || matHint.Contains("copper") || matHint.Contains("componentend") || matHint == "wire")
                return Kind.Copper;
            if (matHint.Contains("filament"))
                return Kind.Filament;
            if (matHint.Contains("aluminum") || matHint.Contains("steel") || matHint.Contains("baremetal") || matHint.Contains("black"))
                return Kind.Housing;

            if (path.Contains("circuitlab") || path.Contains("circuittable") || path.Contains("component"))
                return Kind.Housing;

            return Kind.Skip;
        }

        static string FirstMaterialHint(MeshRenderer mr)
        {
            Material[] slots = mr.sharedMaterials;
            if (slots == null || slots.Length == 0 || slots[0] == null)
                return "";
            return slots[0].name.ToLowerInvariant().Replace(" (instance)", "");
        }

        static string SafeLower(string n)
        {
            return string.IsNullOrEmpty(n) ? "" : n.ToLowerInvariant();
        }

        static string BuildNamePath(Transform t)
        {
            var parts = new List<string>(8);
            Transform p = t;
            int guard = 0;
            while (p != null && guard++ < 12)
            {
                parts.Add(SafeLower(p.name));
                p = p.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Material MaterialFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Graphite: return _graphite;
                case Kind.Copper: return _copper;
                case Kind.Housing: return _housing;
                case Kind.Glass: return _glass;
                case Kind.Filament: return _filament;
                case Kind.Label: return _label;
                case Kind.Bench: return _bench;
                case Kind.Anodized: return _anodized;
                default: return null;
            }
        }

        static void AssignKeepingSlotCount(MeshRenderer mr, Material mat)
        {
            Material[] current = mr.sharedMaterials;
            if (current == null || current.Length <= 1)
            {
                mr.sharedMaterial = mat;
                return;
            }

            var next = new Material[current.Length];
            for (int i = 0; i < next.Length; i++)
                next[i] = mat;
            mr.sharedMaterials = next;
        }

        static void LogSkipOnce(MeshRenderer mr, string reason)
        {
            if (reason == "TMP label" || reason == "label" || reason == "TMP font material")
            {
                if (_loggedSkipTmp)
                    return;
                _loggedSkipTmp = true;
            }
            else if (reason != null && reason.StartsWith("electron"))
            {
                if (_loggedSkipElectron)
                    return;
                _loggedSkipElectron = true;
            }
            else if (reason != null && reason.StartsWith("glow"))
            {
                if (_loggedSkipGlow)
                    return;
                _loggedSkipGlow = true;
            }
            Debug.Log("CircuitLabStyleApplier skipped '" + mr.gameObject.name + "': " + reason);
        }

        void EnsureCopperRim()
        {
            CircuitLab[] labs = Object.FindObjectsByType<CircuitLab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < labs.Length; i++)
            {
                if (labs[i] == null)
                    continue;
                Transform board = FindChildNamed(labs[i].transform, "Breadboard");
                if (board == null)
                    continue;
                if (FindChildNamed(labs[i].transform, RimRootName) != null)
                    continue;
                BuildRim(board);
            }
        }

        static Transform FindChildNamed(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        static void BuildRim(Transform board)
        {
            var root = new GameObject(RimRootName);
            root.transform.SetParent(board, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            AddRimBar(root.transform, "LabStyleRim_ZPos", new Vector3(0f, 0.51f, 0.5f), new Vector3(1.04f, 0.14f, 0.028f));
            AddRimBar(root.transform, "LabStyleRim_ZNeg", new Vector3(0f, 0.51f, -0.5f), new Vector3(1.04f, 0.14f, 0.028f));
            AddRimBar(root.transform, "LabStyleRim_XPos", new Vector3(0.5f, 0.51f, 0f), new Vector3(0.028f, 0.14f, 1.04f));
            AddRimBar(root.transform, "LabStyleRim_XNeg", new Vector3(-0.5f, 0.51f, 0f), new Vector3(0.028f, 0.14f, 1.04f));
        }

        static void AddRimBar(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null)
                Object.DestroyImmediate(c);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && _copper != null)
                mr.sharedMaterial = _copper;
        }

        static void EnsureMaterials()
        {
            if (_lit == null)
            {
                Material graphite = LabWorldMeshes.GraphiteTemplate;
                if (graphite != null && graphite.shader != null)
                    _lit = graphite.shader;
                if (_lit == null)
                    _lit = Shader.Find(LitShaderName);
                if (_lit == null)
                    _lit = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (_lit == null)
                    Debug.LogError("CircuitLabStyleApplier: URP Lit missing. Pin RELab_Graphite. Not falling back to Sprites/Default.");
            }
            if (_lit == null)
                return;

            if (_graphite == null)
                _graphite = MakeLit("RELab_Graphite", new Color(0.13f, 0.135f, 0.14f, 1f), 0.78f, 0.36f, false);
            if (_copper == null)
                _copper = MakeLit("RELab_Copper", new Color(0.72f, 0.45f, 0.22f, 1f), 1.0f, 0.55f, false);
            if (_housing == null)
                _housing = MakeLit("RELab_Housing", new Color(0.10f, 0.105f, 0.11f, 1f), 0.28f, 0.24f, false);
            if (_glass == null)
                _glass = MakeLit("RELab_Glass", new Color(0.78f, 0.82f, 0.85f, 1f), 0.06f, 0.86f, false);
            if (_filament == null)
            {
                _filament = MakeLit("RELab_Filament", new Color(0.38f, 0.28f, 0.16f, 1f), 0.82f, 0.42f, true);
                if (_filament != null && _filament.HasProperty("_EmissionColor"))
                    _filament.SetColor("_EmissionColor", Color.black);
            }
            if (_label == null)
                _label = MakeLit("RELab_Label", new Color(0.86f, 0.88f, 0.84f, 1f), 0.05f, 0.18f, false);
            if (_anodized == null)
                _anodized = MakeLit("RELab_Anodized", new Color(0.18f, 0.185f, 0.19f, 1f), 0.88f, 0.48f, false);
            if (_bench == null)
            {
                _bench = MakeLit("RELab_Bench", new Color(0.16f, 0.165f, 0.17f, 1f), 0.62f, 0.30f, false);
                Texture2D grid = EnsureGrid();
                if (_bench != null && grid != null)
                {
                    if (_bench.HasProperty("_BaseMap"))
                        _bench.SetTexture("_BaseMap", grid);
                    if (_bench.HasProperty("_MainTex"))
                        _bench.SetTexture("_MainTex", grid);
                    _bench.SetTextureScale("_BaseMap", new Vector2(9f, 9f));
                    _bench.SetTextureScale("_MainTex", new Vector2(9f, 9f));
                }
            }
        }

        static Material MakeLit(string name, Color color, float metallic, float smoothness, bool emission)
        {
            return LabWorldMeshes.MakeLit(name, color, metallic, smoothness, emission);
        }

        static void StripCircuitTableRootRenderer()
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "CircuitTable")
                    continue;
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(mr);
                else
                    Object.DestroyImmediate(mr);
            }
        }

        static Texture2D EnsureGrid()
        {
            if (_grid != null)
                return _grid;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "RELab_Grid",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            var dark = new Color(0.16f, 0.165f, 0.17f, 1f);
            var line = new Color(0.28f, 0.22f, 0.16f, 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool gridLine = (x % 16 == 0) || (y % 16 == 0) || (x % 16 == 15) || (y % 16 == 15);
                    pixels[y * size + x] = gridLine ? line : dark;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _grid = tex;
            return _grid;
        }
    }
}
