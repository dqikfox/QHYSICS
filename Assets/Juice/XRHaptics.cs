using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Subscribes to XR interactable select/activate events and sends haptic impulses.
/// Grab/select → 0.3 amplitude, 0.05 s.  Activate (trigger) → 0.6 amplitude, 0.1 s.
/// No-ops cleanly on desktop or when no XR device is present.
/// Self-initializing via RuntimeInitializeOnLoadMethod — no scene edits required.
/// Compatible with XR Interaction Toolkit 3.6.0.
/// </summary>
public class XRHaptics : MonoBehaviour
{
    [Header("Haptic Intensities")]
    [SerializeField] private float _grabAmplitude = 0.3f;
    [SerializeField] private float _grabDuration = 0.05f;
    [SerializeField] private float _activateAmplitude = 0.6f;
    [SerializeField] private float _activateDuration = 0.1f;

    [Header("Scan Settings")]
    [Tooltip("Interval (seconds) between re-scans for newly spawned XR interactables.")]
    [SerializeField] private float _rescanInterval = 3f;

    private readonly HashSet<XRBaseInteractable> _subscribed = new HashSet<XRBaseInteractable>();
    private float _scanTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        // Check whether any XR interactables exist; if not, this is likely a desktop scene
        // and we skip creating the monitor to avoid unnecessary overhead.
        var interactables = Object.FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Exclude);
        if (interactables == null || interactables.Length == 0) return;

        var go = new GameObject("XRHaptics");
        go.AddComponent<XRHaptics>();
        DontDestroyOnLoad(go);
    }

    private void Start()
    {
        ScanAndSubscribe();
    }

    private void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer >= _rescanInterval)
        {
            _scanTimer = 0f;
            ScanAndSubscribe();
        }
    }

    private void ScanAndSubscribe()
    {
        var interactables = Object.FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Exclude);
        if (interactables == null) return;

        foreach (var interactable in interactables)
        {
            if (interactable == null) continue;
            if (_subscribed.Contains(interactable)) continue;

            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.activated.AddListener(OnActivated);

            _subscribed.Add(interactable);
        }

        // Clean up destroyed interactables
        _subscribed.RemoveWhere(i => i == null);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args?.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controllerInteractor)
        {
            controllerInteractor.SendHapticImpulse(_grabAmplitude, _grabDuration);
        }
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (args?.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controllerInteractor)
        {
            controllerInteractor.SendHapticImpulse(_activateAmplitude, _activateDuration);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from all events to prevent dangling references
        foreach (var interactable in _subscribed)
        {
            if (interactable == null) continue;
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.activated.RemoveListener(OnActivated);
        }
        _subscribed.Clear();
    }
}
