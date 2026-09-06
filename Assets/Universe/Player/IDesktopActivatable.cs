using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>
    /// Desktop "trigger" while a gadget is held: LMB / scroll cycles presets or toggles.
    /// XR uses XRGrabInteractable.activated instead.
    /// </summary>
    public interface IDesktopActivatable
    {
        void DesktopActivate(int delta);
    }
}
