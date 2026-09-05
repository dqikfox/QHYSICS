using UnityEngine;

namespace RealityEngine.UI
{
    /// <summary>Shared lab actions: reset CircuitLab experiment without fake stubs.</summary>
    public static class QhysicsLabActions
    {
        public static bool ResetCircuitLab()
        {
            var lab = UnityEngine.Object.FindAnyObjectByType<CircuitLab>(FindObjectsInactive.Include);
            if (lab == null)
            {
                Debug.LogWarning("QHYSICS: CircuitLab not found — cannot reset experiment.");
                return false;
            }
            lab.Reset();
            Time.timeScale = 1f;
            Debug.Log("QHYSICS: CircuitLab.Reset() done.");
            return true;
        }
    }
}