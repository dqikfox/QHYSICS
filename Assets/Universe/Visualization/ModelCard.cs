using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Honest model label for Reality Engine v0.6. Not a Maxwell solver, not a picture of electrons.
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
        string _scaleLine;
        string _experimentLine;

        public string Disclaimer => string.IsNullOrEmpty(disclaimer) ? Text : disclaimer;

        public string LayerLine => _layerLine;

        public string ScaleLine => _scaleLine;

        public string ExperimentLine => _experimentLine;

        public void SetLayerLine(string line)
        {
            _layerLine = line;
        }

        public void SetScaleLine(string line)
        {
            _scaleLine = line;
        }

        public void SetExperimentLine(string line)
        {
            _experimentLine = line;
        }

        public string Compose()
        {
            string s = Disclaimer;
            if (!string.IsNullOrEmpty(_layerLine))
                s += "\n" + _layerLine;
            if (!string.IsNullOrEmpty(_scaleLine))
                s += "\n" + _scaleLine;
            if (!string.IsNullOrEmpty(_experimentLine))
                s += "\n" + _experimentLine;
            return s;
        }
    }
}