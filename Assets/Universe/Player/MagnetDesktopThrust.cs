using UnityEngine;

namespace RealityEngine.Player
{
    /// <summary>
    /// Desktop LMB/scroll while holding a bar magnet applies an impulse along the dipole axis
    /// so Faraday induction is playable without VR throw velocity (ACTION -> RESPONSE).
    /// Honesty: classical rigidbody impulse - not a rail launcher, not scripted path through the coil.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagnetDesktopThrust : MonoBehaviour, IDesktopActivatable
    {
        [SerializeField] float impulsePerClick = 0.22f;
        [SerializeField] float cooldownSeconds = 0.2f;

        Rigidbody _body;
        float _cooldownUntil;

        public void DesktopActivate(int delta)
        {
            if (Time.unscaledTime < _cooldownUntil)
                return;
            if (delta == 0)
                delta = 1;

            if (_body == null)
                _body = GetComponent<Rigidbody>();
            if (_body == null || _body.isKinematic)
                return;

            // Dipole / bar axis is local up (N toward +Y). Sign follows scroll / LMB.
            Vector3 axis = transform.up;
            float mag = Mathf.Max(0.02f, impulsePerClick) * Mathf.Sign(delta);
            _body.AddForce(axis * mag, ForceMode.Impulse);
            _cooldownUntil = Time.unscaledTime + Mathf.Max(0.05f, cooldownSeconds);
        }
    }
}
