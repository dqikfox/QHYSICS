using UnityEngine;
using RealityEngine.Audio;
using RealityEngine.Combat;
using RealityEngine.Player;
using RealityEngine.UI;
using RealityEngine.Visualization;
using RealityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RealityEngine.Stations
{
    /// <summary>
    /// Travel points to the Giza monuments (toolbelt WORLD tab: Sphinx / Khufu / Khafre / Menkaure / Plaza,
    /// desktop Home key cycles them). Moves the XR Origin (never the camera) exactly like arena / station travel:
    /// face the target, keep the tracked head (not the rig origin) over the landing point, CharacterController
    /// disabled during the move. Landing points are resolved at runtime from the monument roots (no scene edits)
    /// and snapped to solid ground with a headroom check.
    /// </summary>
    public sealed class GizaTravel : MonoBehaviour
    {
        public static readonly string[] Destinations = { "sphinx", "khufu", "khafre", "menkaure", "plaza" };
        static int _cycle = -1;
        static GizaTravel _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isPlaying || _runner != null)
                return;
            var go = new GameObject("QhysicsGizaTravel");
            _runner = go.AddComponent<GizaTravel>();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null || CombatInputGate.Blocked)
                return;
            if (kb.homeKey.wasPressedThisFrame)
                CycleNext();
#endif
        }

        /// <summary>Cycle Sphinx -> Khufu -> Khafre -> Menkaure -> Plaza.</summary>
        public static bool CycleNext()
        {
            _cycle = (_cycle + 1) % Destinations.Length;
            return TravelTo(Destinations[_cycle]);
        }

        /// <summary>True when the toolbelt label/key is a Giza travel destination.</summary>
        public static bool IsDestination(string key)
        {
            return System.Array.IndexOf(Destinations, key) >= 0;
        }

        public static bool TravelTo(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            key = key.Trim().ToLowerInvariant();
            int idx = System.Array.IndexOf(Destinations, key);
            if (idx >= 0)
                _cycle = idx;

            if (key == "plaza")
            {
                LabPlayerSpawn.EnsureApplied();
                LabPlayerSpawn.RecalibratePlayerHeight(force: true);
                SyncDesktop();
                QhysicsSfx.Play2D(SfxId.Unlock, 0.5f);
                Debug.Log("QHYSICS: Travel -> Plaza.");
                return true;
            }

            if (!TryResolve(key, out Vector3 land, out Vector3 lookAt, out string label))
            {
                Debug.LogWarning("QHYSICS: Travel -> " + key + " failed (monument root not found in scene).");
                QhysicsSfx.Play2D(SfxId.Denied, 0.5f);
                return false;
            }
            if (!MoveOrigin(land, lookAt))
            {
                Debug.LogWarning("QHYSICS: Travel -> " + label + " failed (no XR Origin).");
                return false;
            }
            QhysicsSfx.Play2D(SfxId.Unlock, 0.5f);
            Debug.Log("QHYSICS: Travel -> " + label + " at " + land.ToString("F1") + ".");
            return true;
        }

        // ── Landing points ─────────────────────────────────────────

        static bool TryResolve(string key, out Vector3 land, out Vector3 lookAt, out string label)
        {
            land = lookAt = Vector3.zero;
            label = key;
            switch (key)
            {
                case "sphinx":
                {
                    label = "Sphinx";
                    GameObject s = GizaComplex.FindNamed(GizaSphinx.RootName);
                    if (s == null)
                        return false;
                    Transform t = s.transform;
                    // Sphinx local: +X face/east, toe tips at x~35.3, Sphinx Temple west wall at x~42.8, plinth to x~37.8.
                    // Court floor in front of the forepaws, facing the face (nemes head ~ local (40, 17, 0) is up-west of us).
                    lookAt = t.TransformPoint(new Vector3(33f, 14f, 0f));
                    float courtY = t.position.y;
                    Vector3[] candidates =
                    {
                        new Vector3(39.6f, 0f, 0f), new Vector3(39.6f, 0f, 3f), new Vector3(39.6f, 0f, -3f),
                        new Vector3(38.6f, 0f, 0f), new Vector3(32.5f, 0.7f, 0f) // last: plinth between the paws, before the Dream Stele
                    };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        Vector3 p = t.TransformPoint(candidates[i]);
                        if (FindGround(p, courtY - 2f, courtY + 2.5f, out Vector3 g) && HasHeadroom(g))
                        {
                            land = g;
                            return true;
                        }
                    }
                    // No collider hit: plinth top between the paws (Sphinx_Body mesh carries a collider).
                    land = t.TransformPoint(new Vector3(32.5f, 0.75f, 0f));
                    return true;
                }
                case "khufu":
                    label = "Khufu";
                    return PyramidNorth(KhufuPyramid.RootName, KhufuPyramid.BaseMeters, out land, out lookAt);
                case "khafre":
                    label = "Khafre";
                    return PyramidNorth(KhafrePyramid.RootName, KhafrePyramid.BaseMeters, out land, out lookAt);
                case "menkaure":
                    label = "Menkaure";
                    return PyramidNorth(MenkaurePyramid.RootName, MenkaurePyramid.BaseMeters, out land, out lookAt);
            }
            return false;
        }

        /// <summary>North face (entrance side), 18-30 m out from the base edge, facing the pyramid (south).</summary>
        static bool PyramidNorth(string rootName, float baseM, out Vector3 land, out Vector3 lookAt)
        {
            land = lookAt = Vector3.zero;
            GameObject go = GizaComplex.FindNamed(rootName);
            if (go == null)
                return false;
            Transform t = go.transform;
            float half = baseM * 0.5f;
            lookAt = t.TransformPoint(new Vector3(0f, baseM * 0.3f, 0f));
            float baseY = t.position.y;
            float[] outs = { 18f, 24f, 30f, 12f };
            float[] sides = { 0f, 6f, -6f };
            for (int i = 0; i < outs.Length; i++)
            {
                for (int j = 0; j < sides.Length; j++)
                {
                    Vector3 p = t.TransformPoint(new Vector3(sides[j], 0f, half + outs[i]));
                    if (FindGround(p, baseY - 6f, baseY + 14f, out Vector3 g) && HasHeadroom(g))
                    {
                        land = g;
                        return true;
                    }
                }
            }
            land = t.TransformPoint(new Vector3(0f, 0f, half + 18f)) + Vector3.up * 0.1f;
            return true;
        }

        /// <summary>Highest static, non-trigger surface at xz within [minY, maxY].</summary>
        static bool FindGround(Vector3 p, float minY, float maxY, out Vector3 ground)
        {
            ground = p;
            Vector3 from = new Vector3(p.x, maxY + 1.5f, p.z);
            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(from, Vector3.down, (maxY - minY) + 3f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestY = float.MinValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.attachedRigidbody != null || CombatInputGate.IsPlayerCollider(c))
                    continue;
                if (hits[i].normal.y < 0.6f)
                    continue;
                float y = hits[i].point.y;
                if (y < minY || y > maxY)
                    continue;
                if (y > bestY)
                {
                    bestY = y;
                    ground = hits[i].point;
                    found = true;
                }
            }
            return found;
        }

        static bool HasHeadroom(Vector3 ground)
        {
            Vector3 a = ground + Vector3.up * 0.45f;
            Vector3 b = ground + Vector3.up * 1.75f;
            Collider[] cols = UnityEngine.Physics.OverlapCapsule(a, b, 0.3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || cols[i].attachedRigidbody != null || CombatInputGate.IsPlayerCollider(cols[i]))
                    continue;
                return false;
            }
            return true;
        }

        // ── Rig move (same as CombatArena.TogglePlayerTeleport / LabStationHub.TeleportToStation) ──

        static bool MoveOrigin(Vector3 land, Vector3 lookAt)
        {
            GameObject originGo = GameObject.Find(LabPlayerSpawn.OriginName);
            if (originGo == null)
                return false;
            var cc = originGo.GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (cc != null)
                cc.enabled = false;
            Transform o = originGo.transform;
            Camera cam = CombatInputGate.ViewCamera();
            Vector3 look = lookAt - land;
            look.y = 0f;
            if (look.sqrMagnitude < 1e-6f)
                look = Vector3.forward;
            float camYaw = cam != null ? cam.transform.eulerAngles.y - o.eulerAngles.y : 0f;
            o.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(look).eulerAngles.y - camYaw, 0f);
            Vector3 headOffset = cam != null ? cam.transform.position - o.position : Vector3.zero;
            headOffset.y = 0f;
            o.position = land - headOffset + Vector3.up * 0.05f;
            if (cc != null)
                cc.enabled = had;
            SyncDesktop();
            return true;
        }

        static void SyncDesktop()
        {
            var desktop = DesktopPlayerController.Instance;
            if (desktop != null)
                desktop.SyncAfterTeleport();
        }
    }
}
