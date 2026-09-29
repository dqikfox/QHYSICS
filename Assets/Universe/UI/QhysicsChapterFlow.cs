using UnityEngine;
using UnityEngine.UI;
using RealityEngine.Challenges;
using RealityEngine.Experiments;
using RealityEngine.Player;
using RealityEngine.XR;

namespace RealityEngine.UI
{
    /// <summary>
    /// Modal UI flags shared by the chapter flow (chapter-complete screen). Desktop frees the cursor and
    /// blocks combat input while a modal or the boot menu is up; XR keeps the rays visible (QhysicsXrRayPolicy).
    /// </summary>
    public static class QhysicsModal
    {
        public static bool ChapterCompleteOpen { get; set; }

        public static bool AnyOpen => ChapterCompleteOpen;

        /// <summary>Desktop needs a free cursor for the boot menu and chapter-complete screen.</summary>
        public static bool CursorWanted => QhysicsUiState.BootMenuOpen || AnyOpen;

        /// <summary>
        /// World-space canvases use TrackedDeviceGraphicRaycaster (XR rays). On desktop the mouse needs the plain
        /// GraphicRaycaster (screen point through the view camera), so enable it only when XR is not running.
        /// </summary>
        public static void SyncDesktopRaycaster(Canvas canvas)
        {
            if (canvas == null)
                return;
            bool desktop = !QhysicsUiState.IsXr;
            GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr != null && gr.enabled != desktop)
                gr.enabled = desktop;
            if (desktop)
            {
                var dpc = DesktopPlayerController.Instance;
                if (dpc != null && dpc.MainCamera != null)
                {
                    Camera c = dpc.MainCamera.GetComponent<Camera>();
                    if (c != null && c.isActiveAndEnabled)
                        canvas.worldCamera = c;
                }
            }
        }
    }

    /// <summary>
    /// Chapter 1 flow helpers: Continue target from saved progress, level start, world entry.
    /// Levels are the ChapterCatalog Chapter 1 challenges (progress.json via ChallengeManager).
    /// </summary>
    public static class QhysicsChapterFlow
    {
        public static ChapterDefinition Chapter1 => ChapterCatalog.Get(ChapterCatalog.Chapter1Id);

        public static string FinaleId => ChapterCatalog.Chapter1Ids[ChapterCatalog.Chapter1Ids.Length - 1];

        /// <summary>True when any Chapter 1 level has a star.</summary>
        public static bool HasProgress()
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            if (mgr == null)
                return false;
            string[] ids = ChapterCatalog.Chapter1Ids;
            for (int i = 0; i < ids.Length; i++)
            {
                if (mgr.GetStars(ids[i]) > 0)
                    return true;
            }
            return false;
        }

        /// <summary>Last (highest) unlocked Chapter 1 level; index -1 when the manager is not ready.</summary>
        public static int ContinueIndex()
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            if (mgr == null)
                return -1;
            string[] ids = ChapterCatalog.Chapter1Ids;
            for (int i = ids.Length - 1; i >= 0; i--)
            {
                if (mgr.IsUnlocked(ids[i]))
                    return i;
            }
            return 0;
        }

        public static string LevelTitle(int index)
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            string[] ids = ChapterCatalog.Chapter1Ids;
            if (index < 0 || index >= ids.Length)
                return string.Empty;
            ChallengeDefinition def = mgr != null ? mgr.FindChallenge(ids[index]) : null;
            return def != null ? def.title : ids[index];
        }

        public static int StarsTotal()
        {
            return ChapterCatalog.CountStars(Chapter1, ChallengeManager.Instance);
        }

        public static int StarsMax => ChapterCatalog.Chapter1Ids.Length * 3;

        /// <summary>Build the lab, park the player on the plaza (same as the old Enter Sandbox).</summary>
        public static void EnterWorld()
        {
            Time.timeScale = 1f;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var lab = InductionLabBootstrap.EnsureLabInScene(scene);
            if (lab != null)
                lab.BuildLab();
            LabPlayerSpawn.EnsureApplied();
            QhysicsDesktopBootstrap.Ensure();
            LabPlayerSpawn.RecalibratePlayerHeight(force: true);
        }

        /// <summary>Start a Chapter 1 level by index (abandons any active challenge). Intro card follows automatically.</summary>
        public static bool StartLevel(int index)
        {
            ChallengeManager mgr = ChallengeManager.Instance;
            string[] ids = ChapterCatalog.Chapter1Ids;
            if (mgr == null || index < 0 || index >= ids.Length)
                return false;
            if (mgr.IsChallengeActive)
                mgr.AbandonChallenge();
            bool ok = mgr.StartChallenge(ids[index]);
            if (!ok)
                Debug.LogWarning("QHYSICS: Chapter 1 level " + (index + 1) + " (" + ids[index] + ") is locked or missing.");
            return ok;
        }
    }
}
