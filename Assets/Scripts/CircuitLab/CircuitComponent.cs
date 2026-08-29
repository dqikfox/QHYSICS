using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LabelAlignment { Top, Bottom, Center };

public class CircuitComponent : MonoBehaviour
{
    // Public members set in Unity Object Inspector
    public CircuitLab Lab;

    public bool IsPlaced { get; protected set; }
    public bool IsHeld { get; protected set; }
    public bool IsClone { get; set; }
    public bool IsClosed { get; protected set; }

    protected Point StartingPeg { get; set; }
    protected Direction Direction { get; set; }
    protected bool IsActive { get; set; }
    protected bool IsForward { get; set; }
    protected bool IsShortCircuit { get; set; }
    protected double Voltage { get; set; }
    protected double Current { get; set; }

    const double SignificantCurrent = 0.0000001;
    const float LabelOffset = 0.022f;

    protected CircuitComponent()
    {
        IsPlaced = false;
        IsHeld = false;
        IsClone = false;
        IsClosed = true;
        IsActive = false;
        IsForward = true;
        IsShortCircuit = false;
        Voltage = 0f;
        Current = 0f;
    }

    protected virtual void Start()
    {
        EnsureLabReference();
    }

    protected void EnsureLabReference()
    {
        if (Lab == null)
        {
            Lab = Object.FindAnyObjectByType<CircuitLab>();
        }
    }

    protected virtual void Update() { }

    public void Place(Point start, Direction dir)
    {
        IsPlaced = true;
        StartingPeg = start;
        Direction = dir;
    }

    public virtual void SetActive(bool isActive, bool isForward)
    {
        IsActive = isActive;
        IsForward = isForward;
    }

    public virtual void SetShortCircuit(bool isShortCircuit, bool isForward)
    {
        IsShortCircuit = isShortCircuit;
        IsForward = isForward;
    }

    public double GetVoltage()
    {
        return Voltage;
    }

    public virtual void SetVoltage(double voltage)
    {
        Voltage = voltage;
    }

    public virtual void SetCurrent(double current)
    {
        Current = current;
    }

    protected bool IsCurrentSignificant()
    {
        return (Current > SignificantCurrent);
    }

    // Toggle the state of a binary component (for example, a switch)
    public virtual void Toggle()
    {
    }

    // Adjust the behavior of a component with various modes
    public virtual void Adjust()
    {
    }

    // Reset any component-specific state that might need resetting
    protected virtual void Reset()
    {
    }

    public virtual void SelectEntered()
    {
        IsHeld = true;
        EnsureLabReference();

        // Enable box and sphere colliders so this piece can be placed somewhere else on the board.
        var box = GetComponent<BoxCollider>();
        if (box != null) box.enabled = true;
        var sphere = GetComponent<SphereCollider>();
        if (sphere != null) sphere.enabled = true;

        if (IsPlaced)
        {
            if (Lab != null)
            {
                Lab.RemoveComponent(this.gameObject, StartingPeg);
            }

            IsPlaced = false;
        }

        // Reinitialize component state
        Reset();
    }

    public virtual void SelectExited()
    {
        IsHeld = false;

        // Make sure gravity is enabled any time we release the object
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    protected IEnumerator PlaySound(AudioSource source, float delay)
    {
        if (source == null) yield break;
        yield return new WaitForSeconds(delay);
        if (source == null) yield break;

        source.Stop();
        source.Play();
    }

    protected void RotateLabel(GameObject label, LabelAlignment alignment)
    {
        if (label == null) return;
        var rotation = label.transform.localEulerAngles;
        var position = label.transform.localPosition;

        switch (Direction)
        {
            case Direction.North:
            case Direction.East:
                rotation.z = -90f;
                position.x = alignment switch
                {
                    LabelAlignment.Top => -LabelOffset,
                    LabelAlignment.Bottom => LabelOffset,
                    _ => 0
                };
                break;
            case Direction.South:
            case Direction.West:
                rotation.z = 90f;
                position.x = alignment switch
                {
                    LabelAlignment.Top => LabelOffset,
                    LabelAlignment.Bottom => -LabelOffset,
                    _ => 0
                }; break;
            default:
                Debug.Log("Unrecognized direction!");
                break;
        }

        // Apply label positioning
        label.transform.localEulerAngles = rotation;
        label.transform.localPosition = position;
    }
}

public enum Direction { North, South, East, West };


