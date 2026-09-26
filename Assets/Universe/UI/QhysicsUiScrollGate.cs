using System.Collections.Generic;

namespace RealityEngine.UI
{
    /// <summary>
    /// Tiny static gate: while any scrollable world panel (challenge list, skills panel) is open it
    /// "captures" the mouse wheel / right thumbstick so the hotbar and carry pouch don't also react.
    /// </summary>
    public static class QhysicsUiScrollGate
    {
        static readonly HashSet<string> Owners = new HashSet<string>();

        public static bool IsCaptured => Owners.Count > 0;

        public static void Set(string owner, bool captured)
        {
            if (string.IsNullOrEmpty(owner))
                return;
            if (captured)
                Owners.Add(owner);
            else
                Owners.Remove(owner);
        }

        public static void ClearAll()
        {
            Owners.Clear();
        }
    }
}
