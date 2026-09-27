using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#pragma warning disable CS0618 // Type or member is obsolete

public class TeleportationController : MonoBehaviour
{
    public XRController rightTeleportRay;
    public XRController leftTeleportRay;
    public InputHelpers.Button teleportActivationButton;
    public GameObject reticle;

    /// <summary>
    /// Set at runtime (QhysicsXrRayPolicy): while it returns true (boot menu, pause, toolbelt, lists open)
    /// both rays stay visible so world UI can be pointed at and clicked without holding A/X.
    /// </summary>
    public static System.Func<bool> ForceVisible;

    void Update()
    {
        bool any = false;
        if (rightTeleportRay != null)
        {
            bool isActive = CheckIfActivated(rightTeleportRay);
            if (rightTeleportRay.gameObject.activeSelf != isActive)
                rightTeleportRay.gameObject.SetActive(isActive);
            any |= isActive;
        }
        if (leftTeleportRay != null)
        {
            bool isActive = CheckIfActivated(leftTeleportRay);
            if (leftTeleportRay.gameObject.activeSelf != isActive)
                leftTeleportRay.gameObject.SetActive(isActive);
            any |= isActive;
        }
        if (reticle != null)
            reticle.SetActive(any);
    }

    public bool CheckIfActivated(XRController controller)
    {
        if (controller == null)
            return false;
        if (ForceVisible != null && ForceVisible())
            return true;
        if (!controller.inputDevice.isValid)
            return false;

        bool isActivated = false;
        InputHelpers.IsPressed(controller.inputDevice, teleportActivationButton, out isActivated);
        return isActivated;
    }
}

#pragma warning restore CS0618
