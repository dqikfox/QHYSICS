using UnityEngine;

namespace RealityEngine.Visualization
{
    /// <summary>
    /// Honest model label for Reality Engine v0.3. Not a Maxwell solver, not a picture of electrons.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModelCard : MonoBehaviour
    {
        public const string Text =
            "Classical model: two-pole magnet, lumped Faraday law. Visualization of B is a sampled field, not a photograph of reality.";

        [SerializeField]
        [Tooltip("Disclaimer shown to the player. Keep this honest: classical two-pole + lumped Faraday law only.")]
        string disclaimer = Text;

        public string Disclaimer => string.IsNullOrEmpty(disclaimer) ? Text : disclaimer;
    }
}
