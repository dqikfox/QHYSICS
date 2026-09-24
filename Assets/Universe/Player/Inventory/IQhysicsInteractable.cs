using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>World interactable for desktop ray / XR grab (pickup, use, examine).</summary>
    public interface IQhysicsInteractable
    {
        string InteractLabel { get; }
        bool CanInteract(GameObject actor);
        void Interact(GameObject actor);
        void SetHover(bool hovered);
    }
}
