using UnityEngine;
using UnityEngine.XR;

namespace RealityEngine.Player
{
    /// <summary>
    /// Converts raw XR InputDevices poses (tracking space) into world space.
    /// Raw devicePosition/deviceRotation are relative to the XR Origin's Camera Offset, so they are wrong
    /// whenever the rig is not at the world origin (e.g. spawned at the Giza plaza). The XR camera's parent
    /// IS that tracking space, so world = cameraParent.TransformPoint(pos), cameraParent.rotation * rot.
    /// </summary>
    public static class XrTrackingSpace
    {
        static Transform _space;
        static int _frame = -1;

        /// <summary>The Camera Offset transform (tracking-space parent of the XR camera), or null.</summary>
        public static Transform Space
        {
            get
            {
                if (_frame == Time.frameCount && _space != null)
                    return _space;
                _frame = Time.frameCount;
                _space = null;
                GameObject origin = GameObject.Find("XR Origin");
                if (origin != null)
                {
                    Transform off = origin.transform.Find("Camera Offset");
                    if (off != null)
                        _space = off;
                }
                if (_space == null)
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                        _space = cam.transform.parent;
                }
                return _space;
            }
        }

        public static Vector3 ToWorldPoint(Vector3 trackingPos)
        {
            Transform s = Space;
            return s != null ? s.TransformPoint(trackingPos) : trackingPos;
        }

        public static Quaternion ToWorldRotation(Quaternion trackingRot)
        {
            Transform s = Space;
            return s != null ? s.rotation * trackingRot : trackingRot;
        }

        public static Vector3 ToWorldVector(Vector3 trackingVec)
        {
            Transform s = Space;
            return s != null ? s.TransformVector(trackingVec) : trackingVec;
        }

        /// <summary>World-space controller pose for an XR node.</summary>
        public static bool TryGetWorldPose(XRNode node, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p))
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion r))
                return false;
            pos = ToWorldPoint(p);
            rot = ToWorldRotation(r);
            return true;
        }

        /// <summary>World-space controller linear velocity (includes rig scale, excludes rig motion).</summary>
        public static bool TryGetWorldVelocity(XRNode node, out Vector3 vel)
        {
            vel = Vector3.zero;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 v))
                return false;
            vel = ToWorldVector(v);
            return true;
        }
    }
}
