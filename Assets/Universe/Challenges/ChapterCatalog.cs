using System;
using System.Collections.Generic;

namespace RealityEngine.Challenges
{
    /// <summary>
    /// Code-table chapter definition: groups challenge ids into an ordered chapter.
    /// Kept as plain data (no ScriptableObject asset) so it ships through git without scene/asset edits.
    /// </summary>
    [Serializable]
    public sealed class ChapterDefinition
    {
        public string id;
        public int order;
        public string title;
        public string subtitle;
        /// <summary>Ordered challenge ids shown in this chapter.</summary>
        public string[] challengeIds;
        /// <summary>When true, entry N unlocks when entry N-1 has stars > 0 (in addition to any prerequisite chain).</summary>
        public bool sequentialUnlock;

        public int IndexOf(string challengeId)
        {
            if (challengeIds == null)
                return -1;
            for (int i = 0; i < challengeIds.Length; i++)
            {
                if (challengeIds[i] == challengeId)
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Static chapter table. Chapter 1 "Faraday's Bench" is the curated, beatable path; every other
    /// campaign challenge lives in "Sandbox / Extra" (hidden from Chapter 1, never deleted).
    /// </summary>
    public static class ChapterCatalog
    {
        public const string Chapter1Id = "ch1_faradays_bench";
        public const string ExtraId = "extra_sandbox";

        static ChapterDefinition[] _chapters;

        public static readonly string[] Chapter1Ids =
        {
            "bench_orientation", // new: place any gadget
            "first_light",
            "make_and_break",
            "current_control",
            "double_trouble",
            "spin_up",
            "lines_of_force",    // new: compass + magnet
            "induction",
            "the_dynamo",        // new: hand crank near coil
            "transformer"        // new finale: mutual coupler
        };

        /// <summary>Chapters sorted by order. The Extra chapter's id list is filled from the live campaign.</summary>
        public static ChapterDefinition[] All
        {
            get
            {
                if (_chapters == null)
                    Build(null);
                return _chapters;
            }
        }

        /// <summary>(Re)build with the campaign so Extra contains every id not in a curated chapter.</summary>
        public static void Build(ChallengeDefinition[] campaign)
        {
            var ch1 = new ChapterDefinition
            {
                id = Chapter1Id,
                order = 1,
                title = "Chapter 1: Faraday's Bench",
                subtitle = "Circuits, switches, fields and induction",
                challengeIds = Chapter1Ids,
                sequentialUnlock = true
            };

            var extra = new List<string>(48);
            if (campaign != null)
            {
                for (int i = 0; i < campaign.Length; i++)
                {
                    ChallengeDefinition d = campaign[i];
                    if (d == null || string.IsNullOrEmpty(d.id))
                        continue;
                    if (Array.IndexOf(Chapter1Ids, d.id) >= 0)
                        continue;
                    extra.Add(d.id);
                }
            }

            var ex = new ChapterDefinition
            {
                id = ExtraId,
                order = 99,
                title = "Sandbox / Extra",
                subtitle = "Harder solar + Faraday combos (original chain unlocks)",
                challengeIds = extra.ToArray(),
                sequentialUnlock = false
            };
            _chapters = new[] { ch1, ex };
        }

        public static ChapterDefinition Get(string chapterId)
        {
            ChapterDefinition[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].id == chapterId)
                    return all[i];
            }
            return null;
        }

        public static ChapterDefinition GetChapterFor(string challengeId)
        {
            ChapterDefinition[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].IndexOf(challengeId) >= 0)
                    return all[i];
            }
            return null;
        }

        /// <summary>
        /// For sequential chapters: true + previous id ("" when first). False for non-sequential chapters
        /// (their original prerequisite chain applies unchanged).
        /// </summary>
        public static bool TryGetPreviousInChapter(string challengeId, out string previousId)
        {
            previousId = null;
            ChapterDefinition ch = GetChapterFor(challengeId);
            if (ch == null || !ch.sequentialUnlock)
                return false;
            int idx = ch.IndexOf(challengeId);
            previousId = idx > 0 ? ch.challengeIds[idx - 1] : string.Empty;
            return true;
        }

        /// <summary>True when every challenge in the chapter has at least one star.</summary>
        public static bool IsChapterComplete(ChapterDefinition ch, ChallengeManager mgr)
        {
            if (ch == null || mgr == null || ch.challengeIds == null || ch.challengeIds.Length == 0)
                return false;
            for (int i = 0; i < ch.challengeIds.Length; i++)
            {
                if (mgr.GetStars(ch.challengeIds[i]) <= 0)
                    return false;
            }
            return true;
        }

        public static int CountStars(ChapterDefinition ch, ChallengeManager mgr)
        {
            if (ch == null || mgr == null || ch.challengeIds == null)
                return 0;
            int s = 0;
            for (int i = 0; i < ch.challengeIds.Length; i++)
                s += mgr.GetStars(ch.challengeIds[i]);
            return s;
        }
    }
}
