using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Honest model label for Reality Engine v0.4. Not a Maxwell solver, not a picture of electrons.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModelCard : MonoBehaviour
    {
        public const string Text =
            "Classical model: two-pole magnet, lumped Faraday law. Visualization of B is a sampled field, not a photograph of reality. Field Lens layers are classical / numerical samples / conceptual visualizations — never a literal quantum state.";

        [SerializeField]
        [Tooltip("Disclaimer shown to the player. Keep this honest: classical two-pole + lumped Faraday law only.")]
        string disclaimer = Text;

        string _layerLine;

        public string Disclaimer => string.IsNullOrEmpty(disclaimer) ? Text : disclaimer;

        public string LayerLine => _layerLine;

        public void SetLayerLine(string line)
        {
            _layerLine = line;
        }

        public string Compose()
        {
            if (string.IsNullOrEmpty(_layerLine))
                return Disclaimer;
            return Disclaimer + "\n" + _layerLine;
        }
    }
}