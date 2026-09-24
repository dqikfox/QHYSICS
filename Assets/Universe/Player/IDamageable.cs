namespace RealityEngine.Player
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(float amount, UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitNormal);
    }
}
