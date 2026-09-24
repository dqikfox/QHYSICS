using UnityEngine;
using UnityEngine.Audio;

namespace RealityEngine.Audio
{
    /// <summary>
    /// ScriptableObject reference to the QhysicsMixer asset, placed in a Resources folder
    /// so <see cref="AudioRouter"/> can load it at runtime via Resources.Load.
    /// </summary>
    [CreateAssetMenu(fileName = "QhysicsAudioMixerRef", menuName = "QHYSICS/Audio Mixer Ref")]
    public sealed class QhysicsAudioMixerRef : ScriptableObject
    {
        [SerializeField] private AudioMixer mixer;

        /// <summary>The AudioMixer asset referenced by this wrapper.</summary>
        public AudioMixer Mixer => mixer;
    }
}