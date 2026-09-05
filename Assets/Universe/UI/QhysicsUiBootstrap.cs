using UnityEngine;

namespace RealityEngine.UI
{
    /// <summary>
    /// Ensures QHYSICS UI on Play / Place UI. Wires XR camera + XR UI Input Module.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(190)]
    public sealed class QhysicsUiBootstrap : MonoBehaviour
    {
        public const string RootName = "QhysicsUI";
        public const string HostName = "RealityEngine";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoEnsure()
        {
            if (!Application.isPlaying)
                return;
            EnsurePlaced();
        }

        public static QhysicsUiBootstrap EnsurePlaced()
        {
            QhysicsUiBuilder.EnsureXrUiInputModule();

            QhysicsUiBootstrap existing = Object.FindFirstObjectByType<QhysicsUiBootstrap>(FindObjectsInactive.Include);
            if (existing == null)
            {
                GameObject host = GameObject.Find(HostName);
                if (host == null)
                    host = GameObject.Find(RootName);
                if (host == null)
                    host = new GameObject(RootName);
                existing = host.GetComponent<QhysicsUiBootstrap>();
                if (existing == null)
                    existing = host.AddComponent<QhysicsUiBootstrap>();
            }

            existing.BuildChildren();
            return existing;
        }

        public void BuildChildren()
        {
            Transform root = transform.Find(RootName);
            if (root == null && gameObject.name == RootName)
                root = transform;
            if (root == null)
            {
                var go = new GameObject(RootName);
                go.transform.SetParent(transform, false);
                root = go.transform;
            }

            if (QhysicsUiStyle.Font == null)
                Debug.LogError("QHYSICS UI: LiberationSans SDF missing - TMP text may pink. Import TMP Essentials.");

            QhysicsHud.Ensure(root);
            QhysicsToolbelt.Ensure(root);
            QhysicsContextStrip.Ensure(root);
            QhysicsSimChip.Ensure(root);
            QhysicsPausePanel.Ensure(root);
            QhysicsSettingsPanel.Ensure(root);
            QhysicsOnboarding.Ensure(root);
            QhysicsMainMenu.Ensure(root);

            var canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
                QhysicsUiBuilder.WireEventCamera(canvases[i]);
        }

        void Awake()
        {
            if (Application.isPlaying)
                BuildChildren();
        }
    }
}
