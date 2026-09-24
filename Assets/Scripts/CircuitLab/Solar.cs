using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using RealityEngine.Player;

public class Solar : CircuitComponent, ISolar, IDynamic, IDesktopActivatable
{
    // Public members set in Unity Object Inspector
    public GameObject labelWattage;
    public TMP_Text labelWattageText;
    public GameObject labelVoltage;
    public TMP_Text labelVoltageText;
    public GameObject panel;
    public GameObject cell;
    public int rotationIncrement = 90;
    bool cooldownActive = false;
    public AudioSource rotatePanelAudio;
    public GameObject sun;
    public AudioSource sunActivationAudio;

    public float SolarVoltage { get; private set; }
    public float SolarResistance { get; private set; }
    public float SolarWattage { get; private set; }

    private float previousWattage = 0;
    private bool registered = false;

    const float MaxWattage = 1f;
    const float MaxVoltage = 10f;

    public Solar() 
    {
        // Wattage of Solar Panel is variable based on sun direction
        SolarWattage = 0;
    }

    void OnDestroy()
    {
        // Remove ourselves from the circuit lab's list of dynamic objects.
        // (Previously done in a finalizer, but finalizers run on the GC thread
        // where touching Unity objects is unsafe.)
        if (registered && Lab != null)
        {
            Lab.UnregisterDynamicComponent(this);
            registered = false;
        }
    }

    protected override void Start()
    {
        // Set wattage and voltage label text
        if (labelWattageText != null)
            labelWattageText.text = SolarWattage.ToString("0.0") + "W";
        if (labelVoltageText != null)
            labelVoltageText.text = SolarVoltage.ToString("0.0") + "V";
    }

    protected override void Update ()
    {
        EnsureLabReference();
        if (Lab == null)
        {
            // Nothing works without a circuit lab; wait until one exists
            return;
        }

        if (!registered)
        {
            // Register as a dynamic component so we'll get coordinated UpdateState calls
            registered = true;
            Lab.RegisterDynamicComponent(this);
        }

        EnsureSun();

        // Make sure the sun is active when any solar panel is placed on the board
        if (IsPlaced && sun != null && !sun.activeInHierarchy)
        {
            sun.SetActive(true);
            if (sunActivationAudio != null)
                StartCoroutine(PlaySound(sunActivationAudio, 0f));
        }

        // Without a sun / cell reference, panel output stays dark (angle math needs both).
        if (sun == null || cell == null)
        {
            SolarWattage = 0;
            SolarVoltage = 0;
            SolarResistance = 0;
            if (labelWattageText != null)
                labelWattageText.text = "0.0W";
            if (labelVoltageText != null)
                labelVoltageText.text = "0.0V";
            return;
        }

        // Show/hide the labels
        if (labelWattage != null)
            labelWattage.gameObject.SetActive(IsActive && Lab.showLabels);
        if (labelVoltage != null)
            labelVoltage.gameObject.SetActive(IsActive && Lab.showLabels);

        // Cast a ray from the center of the panel so we can compute the relative angle of the sun
        Vector3 forwardPosition = cell.transform.position + cell.transform.forward * 0.5f;

        // Draw visible lines for the sun and normal vectors to aid in debugging
        if (IsPlaced)
        {
            //DrawLine(cell.transform.position, sun.transform.position, Color.green);
            //DrawLine(cell.transform.position, forwardPosition, Color.red);
        }

        // Find the angle between the sun and the panel normal vector
        float angle = Vector3.Angle(cell.transform.forward, sun.transform.position - cell.transform.position);

        // Use a basic linear function for now to compute wattage based on angle to the sun
        SolarWattage = 0;
        SolarVoltage = 0;
        SolarResistance = 0;
        if (angle < 90f)
        {
            float pctWattage = 1f - (angle / 90f);
            SolarWattage = MaxWattage * pctWattage;

            // Compute voltage and resistance from wattage to simulate the variable
            // current that a solar panel produces in different lighting conditions:
            //
            //  - Voltage ramps linearly to max and flatlines at max once angle to sun is less than 80 degrees
            //  - Current ramps up as angle approaches 0 (we calculate the effective current with I = W / V, and
            //    then calculate a resistance that will give us that effective current using R = V / I)
            float pctVoltage = Math.Min(1f, (90f - angle) / 10f);
            SolarVoltage = MaxVoltage * pctVoltage;
            if (SolarVoltage > 1e-6f)
            {
                float current = SolarWattage / SolarVoltage;
                SolarResistance = (current > 1e-6f) ? (SolarVoltage / current) : 0f;
            }
            else
            {
                // Panel is effectively dark; report no output instead of dividing by ~0
                SolarVoltage = 0f;
                SolarResistance = 0f;
            }
        }

        // Update label text
        if (labelWattageText != null)
            labelWattageText.text = SolarWattage.ToString("0.0") + "W";
        if (labelVoltageText != null)
            labelVoltageText.text = SolarVoltage.ToString("0.0") + "V";
    }

    public bool UpdateState(int numActiveCircuits)
    {
        // If the panel output has changed since our last UpdateState call, return true
        // to let the circuit lab know that it needs to run an updated simulation.
        bool simulate = (previousWattage != SolarWattage);
        previousWattage = SolarWattage;

        return simulate;
    }

    void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        GameObject myLine = new GameObject();
        myLine.transform.position = start;
        myLine.AddComponent<LineRenderer>();
        LineRenderer lr = myLine.GetComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.001f;
        lr.endWidth = 0.001f;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        GameObject.Destroy(myLine, Time.deltaTime);
    }

    public override void SetActive(bool isActive, bool isForward)
    {
        IsActive = isActive;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!cooldownActive && other != null && other.gameObject.name.Contains("Pinch"))
            TryRotatePanel(1);
    }

    /// <summary>
    /// Desktop LMB/scroll while holding the solar part rotates the panel toward/away from
    /// the MiniatureSun so Sun Power is playable without VR pinch.
    /// </summary>
    public void DesktopActivate(int delta)
    {
        TryRotatePanel(delta == 0 ? 1 : delta);
    }

    void TryRotatePanel(int steps)
    {
        if (cooldownActive || panel == null)
            return;
        if (steps == 0)
            steps = 1;

        var rotation = panel.transform.localEulerAngles;
        rotation.z += rotationIncrement * Mathf.Sign(steps);
        panel.transform.localEulerAngles = rotation;

        if (rotatePanelAudio != null)
            StartCoroutine(PlaySound(rotatePanelAudio, 0f));

        cooldownActive = true;
        Invoke(nameof(Cooldown), 0.5f);
    }

    void EnsureSun()
    {
        if (sun != null)
            return;
        // Prefer the lab MiniatureSun (starts inactive until a panel is placed).
        GameObject found = GameObject.Find("MiniatureSun");
        if (found == null)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "MiniatureSun")
                {
                    found = all[i].gameObject;
                    break;
                }
            }
        }
        sun = found;
    }

    void Cooldown()
    {
        cooldownActive = false;
    }
}
