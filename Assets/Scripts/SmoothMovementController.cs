using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

public class SmoothMovementController : MonoBehaviour
{
    public float speed = 1;
    public XRNode inputSource;
    public float gravity = -9.81f;
    public LayerMask groundLayer;
    public float additionalHeight = 0.2f;
    public Collider tableCollider;

    private XROrigin origin;
    private Vector2 inputAxis;
    private CharacterController character;
    private float fallingSpeed = 0f;
    private float colliderThreshold = 100.0f;
    private bool locomotionPaused;

    void Start()
    {
        character = GetComponent<CharacterController>();
        origin = GetComponent<XROrigin>();
    }

    void Update()
    {
        if (character == null)
            character = GetComponent<CharacterController>();
        if (origin == null)
            origin = GetComponent<XROrigin>();

        locomotionPaused = IsHeadsetLost();
        if (locomotionPaused)
        {
            inputAxis = Vector2.zero;
            return;
        }

        InputDevice device = InputDevices.GetDeviceAtXRNode(inputSource);
        if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.primary2DAxis, out inputAxis))
            inputAxis = Vector2.zero;
    }

    void FixedUpdate()
    {
        if (locomotionPaused)
            return;
        if (character == null || !character.enabled)
            return;
        if (origin == null || origin.Camera == null)
            return;

        CapsuleFollowHeadset();

        Quaternion headYaw = Quaternion.Euler(0, origin.Camera.transform.eulerAngles.y, 0);
        Vector3 direction = headYaw * new Vector3(inputAxis.x, 0, inputAxis.y);
        character.Move(direction * Time.fixedDeltaTime * speed);

        if (IsGrounded())
            fallingSpeed = 0f;
        else
            fallingSpeed += gravity * Time.fixedDeltaTime;
        character.Move(Vector3.up * fallingSpeed * Time.fixedDeltaTime);
    }

    void CapsuleFollowHeadset()
    {
        if (character == null || origin == null || origin.Camera == null)
            return;

        character.height = origin.CameraInOriginSpaceHeight + additionalHeight;
        Vector3 capsuleCenter = transform.InverseTransformPoint(origin.Camera.gameObject.transform.position);
        Vector3 newCenter = new Vector3(capsuleCenter.x, character.height / 2 + character.skinWidth, capsuleCenter.z);

        if (tableCollider != null)
        {
            Vector3 newWorldCenter = transform.TransformPoint(newCenter);
            Vector3 closest = tableCollider.ClosestPoint(newWorldCenter);
            float diff = Mathf.Abs(closest.sqrMagnitude - newWorldCenter.sqrMagnitude);
            if (diff > colliderThreshold)
                character.center = newCenter;
        }
        else
            character.center = newCenter;
    }

    bool IsGrounded()
    {
        if (character == null)
            return false;
        Vector3 rayStart = transform.TransformPoint(character.center);
        float rayLength = character.center.y + 0.01f;
        RaycastHit hitInfo;
        int mask = groundLayer.value == 0 ? ~0 : groundLayer.value;
        bool hasHit = Physics.SphereCast(rayStart, character.radius, Vector3.down, out hitInfo, rayLength, mask);
        return hasHit;
    }

    static bool IsHeadsetLost()
    {
        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid)
            return false;
        InputTrackingState state;
        if (head.TryGetFeatureValue(CommonUsages.trackingState, out state))
        {
            if ((state & InputTrackingState.Position) == 0 && (state & InputTrackingState.Rotation) == 0)
                return true;
        }
        bool tracked;
        if (head.TryGetFeatureValue(CommonUsages.isTracked, out tracked) && !tracked)
            return true;
        return false;
    }
}
