using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#pragma warning disable CS0618 // Type or member is obsolete

public class TeleportationController : MonoBehaviour
{
    public XRController rightTeleportRay;
    public InputHelpers.Button teleportActivationButton;
    public GameObject reticle;

    void Update()
    {
        if (rightTeleportRay != null)
        {
            bool isActive = CheckIfActivated(rightTeleportRay);
            rightTeleportRay.gameObject.SetActive(isActive);
            if (reticle != null)
            {
                reticle.SetActive(isActive);
            }
        }
    }

    public bool CheckIfActivated(XRController controller)
    {
        if (controller == null)
            return false;

        bool isActivated = false;
        InputHelpers.IsPressed(controller.inputDevice, teleportActivationButton, out isActivated);
        return isActivated;
    }
}

#pragma warning restore CS0618
