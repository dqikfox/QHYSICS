using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Breadboard resistor for CircuitLab. Implements IResistor so SpiceSharp treats it as
/// a resistive element (CircuitLab qualifies SpiceSharp.Components.Resistor separately).
/// </summary>
public class Resistor : CircuitComponent, IResistor
{
    public GameObject resistorBody;
    public GameObject labelResistance;
    public TMP_Text labelResistanceText;
    public GameObject labelCurrent;
    public TMP_Text labelCurrentText;

    [Tooltip("Resistance in ohms. Typical values 100-10000.")]
    public float resistance = 470f;

    public float Resistance => resistance;

    static readonly float[] ResistancePresets = { 100f, 220f, 470f, 1000f, 4700f };

    static readonly Color NormalColor = new Color(0.55f, 0.35f, 0.2f);
    static readonly Color ActiveColor = new Color(0.7f, 0.5f, 0.25f);

    const string OhmSuffix = "\u03A9";

    XRGrabInteractable _grab;
    bool _grabWired;
    Material _bodyMat;

    /// <summary>
    /// Build a grabable breadboard resistor part with the colliders / rigidbody / tag
    /// CircuitLab + PegMgr + Dispenser expect (mirrors ComponentBulb).
    /// </summary>
    public static Resistor CreateBreadboardPart(Transform parent, string objectName = "ComponentResistor")
    {
        var go = new GameObject(objectName);
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        try { go.tag = "Resistor"; }
        catch (UnityException) { /* tag missing from TagManager — bootstrap adds it */ }

        go.layer = 3; // Grab

        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(0.05f, 0.15f, 0.05f);
        box.center = Vector3.zero;
        box.isTrigger = false;

        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.025f;
        sphere.center = new Vector3(0f, 0.05f, 0f);

        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = 0.02f;
        capsule.height = 0.03f;
        capsule.direction = 1;
        capsule.center = new Vector3(0f, 0f, 0.005f);

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        var resistor = go.AddComponent<Resistor>();
        if (go.GetComponent<XRGrabInteractable>() == null)
            go.AddComponent<XRGrabInteractable>();

        resistor.EnsureVisuals();
        resistor.WireGrab();
        return resistor;
    }

    public void CycleResistance(int dir)
    {
        int idx = System.Array.IndexOf(ResistancePresets, resistance);
        if (idx < 0) idx = 2;
        idx = (idx + dir + ResistancePresets.Length) % ResistancePresets.Length;
        resistance = ResistancePresets[idx];
        RefreshResistanceLabel();
    }

    public override void Adjust()
    {
        CycleResistance(+1);
    }

    protected override void Start()
    {
        base.Start();
        EnsurePhysicsPresent();
        EnsureVisuals();
        WireGrab();
    }

    void OnEnable()
    {
        WireGrab();
    }

    void OnDisable()
    {
        UnwireGrab();
    }

    void OnDestroy()
    {
        UnwireGrab();
        if (_bodyMat != null)
        {
            if (Application.isPlaying) Destroy(_bodyMat);
            else DestroyImmediate(_bodyMat);
            _bodyMat = null;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (IsClone)
            return;

        bool showLabels = Lab != null && Lab.showLabels && IsActive && IsCurrentSignificant() && !IsShortCircuit;
        if (labelResistance != null)
            labelResistance.SetActive(showLabels);
        if (labelCurrent != null)
            labelCurrent.SetActive(showLabels);
    }

    void EnsurePhysicsPresent()
    {
        if (GetComponent<BoxCollider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.05f, 0.15f, 0.05f);
        }
        if (GetComponent<SphereCollider>() == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.025f;
            sphere.center = new Vector3(0f, 0.05f, 0f);
        }
        if (GetComponent<CapsuleCollider>() == null)
        {
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = 0.02f;
            capsule.height = 0.03f;
        }
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }
        try
        {
            if (gameObject.tag != "Resistor")
                gameObject.tag = "Resistor";
        }
        catch (UnityException) { /* TagManager may not have Resistor yet */ }

        if (!gameObject.name.StartsWith("Component"))
            gameObject.name = "ComponentResistor";
    }

    void EnsureVisuals()
    {
        if (resistorBody == null)
            BuildProceduralBody();

        if (labelResistance == null || labelResistanceText == null)
            CreateLabel(out labelResistance, out labelResistanceText, "RLabel", new Vector3(0f, 0.06f, 0f));
        if (labelCurrent == null || labelCurrentText == null)
            CreateLabel(out labelCurrent, out labelCurrentText, "ILabel", new Vector3(0f, -0.06f, 0f));

        RefreshResistanceLabel();
        UpdateBodyVisual();
    }

    void BuildProceduralBody()
    {
        resistorBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        resistorBody.name = "ResistorBody";
        resistorBody.transform.SetParent(transform, false);
        resistorBody.transform.localPosition = Vector3.zero;
        resistorBody.transform.localRotation = Quaternion.identity;
        resistorBody.transform.localScale = new Vector3(0.04f, 0.06f, 0.04f);

        // Root owns the CircuitLab colliders; strip the primitive's collider.
        var bodyCol = resistorBody.GetComponent<Collider>();
        if (bodyCol != null)
        {
            if (Application.isPlaying) Destroy(bodyCol);
            else DestroyImmediate(bodyCol);
        }

        var rend = resistorBody.GetComponent<Renderer>();
        if (rend != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _bodyMat = new Material(sh);
            ApplyColor(_bodyMat, NormalColor);
            rend.sharedMaterial = _bodyMat;
        }

        CreateLead(new Vector3(0f, 0.09f, 0f), "LeadTop");
        CreateLead(new Vector3(0f, -0.09f, 0f), "LeadBottom");
    }

    void CreateLead(Vector3 localPos, string leadName)
    {
        var lead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lead.name = leadName;
        lead.transform.SetParent(transform, false);
        lead.transform.localPosition = localPos;
        lead.transform.localScale = new Vector3(0.01f, 0.03f, 0.01f);
        var col = lead.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        var lr = lead.GetComponent<Renderer>();
        if (lr != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh);
            ApplyColor(mat, Color.gray);
            lr.sharedMaterial = mat;
        }
    }

    void CreateLabel(out GameObject go, out TMP_Text text, string labelName, Vector3 localOffset)
    {
        go = new GameObject(labelName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.4f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = FormatOhms(resistance);
        tmp.fontSize = 0.8f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        text = tmp;
        go.SetActive(false);
    }

    static void ApplyColor(Material mat, Color c)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        mat.color = c;
    }

    void UpdateBodyVisual()
    {
        if (resistorBody == null) return;
        var rend = resistorBody.GetComponent<Renderer>();
        if (rend == null) return;

        Color c = IsActive ? ActiveColor : NormalColor;
        if (IsShortCircuit) c = Color.red;

        if (_bodyMat == null)
            _bodyMat = rend.material;
        ApplyColor(_bodyMat, c);
    }

    void RefreshResistanceLabel()
    {
        if (labelResistanceText != null)
            labelResistanceText.text = FormatOhms(resistance);
    }

    static string FormatOhms(float r)
    {
        if (r >= 1000f)
            return (r * 0.001f).ToString("0.##") + "k" + OhmSuffix;
        return r.ToString("0.#") + OhmSuffix;
    }

    public override void SetActive(bool isActive, bool isForward)
    {
        base.SetActive(isActive, isForward);

        RefreshResistanceLabel();
        if (labelCurrentText != null && IsCurrentSignificant())
            labelCurrentText.text = (Current * 1000f).ToString("0.##") + "mA";

        RotateLabel(labelResistance, LabelAlignment.Top);
        RotateLabel(labelCurrent, LabelAlignment.Bottom);
        UpdateBodyVisual();
    }

    public override void SetCurrent(double current)
    {
        base.SetCurrent(current);
        if (labelCurrentText != null && IsActive && IsCurrentSignificant())
            labelCurrentText.text = (current * 1000f).ToString("0.##") + "mA";
    }

    public override void SetShortCircuit(bool isShortCircuit, bool isForward)
    {
        base.SetShortCircuit(isShortCircuit, isForward);
        UpdateBodyVisual();
    }

    void WireGrab()
    {
        if (_grabWired) return;
        _grab = GetComponent<XRGrabInteractable>();
        if (_grab == null) return;
        _grab.selectEntered.AddListener(OnGrabSelectEntered);
        _grab.selectExited.AddListener(OnGrabSelectExited);
        _grabWired = true;
    }

    void UnwireGrab()
    {
        if (!_grabWired || _grab == null) return;
        _grab.selectEntered.RemoveListener(OnGrabSelectEntered);
        _grab.selectExited.RemoveListener(OnGrabSelectExited);
        _grabWired = false;
    }

    void OnGrabSelectEntered(SelectEnterEventArgs _)
    {
        SelectEntered();
    }

    void OnGrabSelectExited(SelectExitEventArgs _)
    {
        SelectExited();
    }
}
