using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class HandPresence : MonoBehaviour
{
    public bool showController = false;
    public InputDeviceCharacteristics controllerCharacteristics;
    public List<GameObject> controllerPrefabs;
    public GameObject handModelPrefab;
    public GameObject pinchCollider;

    private InputDevice targetDevice;
    private GameObject spawnedController;
    private GameObject spawnedHandModel;
    private Animator handAnimator;

    void Start()
    {
        TryInitialize();
    }

    void TryInitialize()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristics, devices);

        if (devices.Count > 0)
        {
            targetDevice = devices[0];
            GameObject prefab = (controllerPrefabs != null) ? controllerPrefabs.Find(controller => controller != null && controller.name == targetDevice.name) : null;
            if (prefab)
            {
                spawnedController = Instantiate(prefab, transform);
            }
            else
            {
                Debug.LogError("Did not find corresponding controller model");
                if (controllerPrefabs != null && controllerPrefabs.Count > 0 && controllerPrefabs[0] != null)
                    spawnedController = Instantiate(controllerPrefabs[0], transform);
            }

            if (handModelPrefab != null)
            {
                spawnedHandModel = Instantiate(handModelPrefab, transform);
                handAnimator = spawnedHandModel != null ? spawnedHandModel.GetComponent<Animator>() : null;
            }
        }
    }

    void UpdateHandAnimation()
    {
        if (targetDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            handAnimator.SetFloat("Trigger", triggerValue);
        }
        else
        {
            handAnimator.SetFloat("Trigger", 0);
        }

        if (targetDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
        {
            handAnimator.SetFloat("Grip", gripValue);
        }
        else
        {
            handAnimator.SetFloat("Grip", 0);
        }
    }

    void Update()
    {
        if (!targetDevice.isValid)
        {
            TryInitialize();
        }
        else
        {
            // Find out if the hand is pinching (trigger pressed but grip released)
            bool triggerPressed;
            bool gripPressed;
            targetDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);
            targetDevice.TryGetFeatureValue(CommonUsages.gripButton, out gripPressed);

            // Activate Pinch Collider so we can detect pinch collisions with other objects
            if (pinchCollider != null)
                pinchCollider.SetActive(triggerPressed && !gripPressed);

            if (showController)
            {
                if (spawnedHandModel != null) spawnedHandModel.SetActive(false);
                if (spawnedController != null) spawnedController.SetActive(true);
            }
            else
            {
                if (spawnedHandModel != null) spawnedHandModel.SetActive(true);
                if (spawnedController != null) spawnedController.SetActive(false);
                if (handAnimator != null) UpdateHandAnimation();
            }
        }
    }
}
