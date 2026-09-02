using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using RealityEngine.Experiments;
using RealityEngine.Visualization;

namespace RealityEngine.EditorTools
{
    public static class CircuitLabStyleMenu
    {
        const string MenuPath = "Reality Engine/Apply Circuit Lab Style";
        const string FixPinkPath = "Reality Engine/Fix Pink Lab Materials";

        [MenuItem(MenuPath)]
        public static void ApplyCircuitLabStyle()
        {
            CircuitLabStyleApplier applier = CircuitLabStyleApplier.EnsureApplied();
            applier.ApplyNow(true);

            InductionLabBootstrap bootstrap = Object.FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
            if (bootstrap != null)
                bootstrap.EnsureLabStyle();

            if (applier != null && applier.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);

            Selection.activeGameObject = applier != null ? applier.gameObject : null;
            Debug.Log("Applied Reality Engine Circuit Lab style (graphite bench, copper conductors, matte housings). Play Faraday to restyle spawned pegs/components.");
        }

        [MenuItem(MenuPath, true)]
        public static bool ApplyCircuitLabStyleValidate()
        {
            return true;
        }

        [MenuItem(FixPinkPath)]
        public static void FixPinkLabMaterials()
        {
            int stripped = StripCircuitTableRootRenderers();
            int replaced = ReplacePinkRenderers();

            CircuitLabStyleApplier applier = CircuitLabStyleApplier.EnsureApplied();
            applier.ApplyNow(true);

            InductionLabBootstrap bootstrap = Object.FindFirstObjectByType<InductionLabBootstrap>(FindObjectsInactive.Include);
            if (bootstrap != null)
                bootstrap.EnsureLabStyle();

            LabLandscapeApplier landscape = LabLandscapeApplier.EnsureApplied();
            landscape.ApplyNow(true);

            replaced += ReplacePinkRenderers();

            Scene scene = applier != null && applier.gameObject.scene.IsValid()
                ? applier.gameObject.scene
                : (landscape != null ? landscape.gameObject.scene : default);
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("Fix Pink Lab Materials: stripped " + stripped + " CircuitTable root MeshRenderer(s), replaced " + replaced + " pink/error materials with URP Lit from RELab_Graphite. Scene view should be graphite-copper, not magenta.");
        }

        [MenuItem(FixPinkPath, true)]
        public static bool FixPinkLabMaterialsValidate()
        {
            return true;
        }

        static int StripCircuitTableRootRenderers()
        {
            int n = 0;
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "CircuitTable")
                    continue;
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr == null)
                    continue;
                Undo.DestroyObjectImmediate(mr);
                n++;
            }
            return n;
        }

        static readonly string[] RootNames =
        {
            "CircuitLab", "CircuitTable", "RealityEngine", "LabLandscape", "Khufu", "Induction Lab"
        };

        static int ReplacePinkRenderers()
        {
            int replaced = 0;
            var seen = new System.Collections.Generic.HashSet<MeshRenderer>();
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null)
                    continue;
                if (!IsWatchedRoot(t))
                    continue;
                MeshRenderer[] mrs = t.GetComponentsInChildren<MeshRenderer>(true);
                for (int j = 0; j < mrs.Length; j++)
                {
                    MeshRenderer mr = mrs[j];
                    if (mr == null || !seen.Add(mr))
                        continue;
                    if (mr.gameObject.name == "CircuitTable")
                        continue;
                    if (ShouldSkipTmp(mr))
                        continue;
                    if (!RendererLooksPink(mr))
                        continue;
                    if (ReplaceOne(mr))
                        replaced++;
                }
            }
            return replaced;
        }

        static bool IsWatchedRoot(Transform t)
        {
            string n = t.name;
            for (int i = 0; i < RootNames.Length; i++)
            {
                if (n == RootNames[i])
                    return true;
            }
            return n.StartsWith("Khufu");
        }

        static bool ShouldSkipTmp(MeshRenderer mr)
        {
            if (mr.GetComponent<TMP_Text>() != null || mr.GetComponent<TextMeshPro>() != null)
                return true;
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
                    return true;
            }
            return false;
        }

        static bool RendererLooksPink(MeshRenderer mr)
        {
            Material[] slots = mr.sharedMaterials;
            if (slots == null || slots.Length == 0)
                return true;
            for (int i = 0; i < slots.Length; i++)
            {
                if (LabWorldMeshes.MaterialLooksPink(slots[i]))
                    return true;
            }
            return false;
        }

        static bool ReplaceOne(MeshRenderer mr)
        {
            string n = mr.gameObject.name.ToLowerInvariant();
            bool label = n == "board" || n == "plate" || (n.Contains("sign") && !n.Contains("design"));
            Material mat;
            if (label)
                mat = LabWorldMeshes.MakeLit("RELab_Label", new Color(0.86f, 0.88f, 0.84f, 1f), 0.05f, 0.18f, false);
            else
                mat = LabWorldMeshes.MakeLit("RELab_Graphite", new Color(0.13f, 0.135f, 0.14f, 1f), 0.78f, 0.36f, false);
            if (mat == null)
            {
                Debug.LogError("Fix Pink Lab Materials: could not instantiate URP Lit from RELab_Graphite for " + mr.gameObject.name);
                return false;
            }
            Undo.RecordObject(mr, "Fix Pink Lab Materials");
            Material[] current = mr.sharedMaterials;
            if (current == null || current.Length <= 1)
                mr.sharedMaterial = mat;
            else
            {
                var next = new Material[current.Length];
                for (int i = 0; i < next.Length; i++)
                    next[i] = mat;
                mr.sharedMaterials = next;
            }
            EditorUtility.SetDirty(mr);
            return true;
        }
    }
}
