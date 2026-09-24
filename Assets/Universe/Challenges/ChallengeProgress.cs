using System;
using System.IO;
using UnityEngine;

namespace RealityEngine.Challenges
{
    /// <summary>
    /// Per-challenge persistent record: stars earned, best time, best component count, unlocked state.
    /// </summary>
    [Serializable]
    public class ChallengeEntry
    {
        public string id = "";
        public int stars;
        public float bestTimeSeconds;
        public int bestComponentCount;
        public bool unlocked;
    }

    /// <summary>
    /// Top-level save file containing all challenge entries.
    /// </summary>
    [Serializable]
    public class ChallengeProgressFile
    {
        public ChallengeEntry[] entries = new ChallengeEntry[0];
    }

    /// <summary>
    /// JSON persistence under Application.persistentDataPath/RealityEngine/challenges/.
    /// Follows ExperimentStore conventions: JsonUtility, EnsureDirectory, single-file save/load.
    /// </summary>
    public static class ChallengeProgress
    {
        const string FileName = "progress.json";

        public static string DirectoryPath =>
            Path.Combine(Application.persistentDataPath, "RealityEngine", "challenges");

        public static void EnsureDirectory()
        {
            string dir = DirectoryPath;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>Load the entire progress file, or a fresh empty file if none exists.</summary>
        public static ChallengeProgressFile Load()
        {
            EnsureDirectory();
            string path = Path.Combine(DirectoryPath, FileName);
            if (!File.Exists(path))
                return new ChallengeProgressFile();
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json))
                    return new ChallengeProgressFile();
                ChallengeProgressFile file = JsonUtility.FromJson<ChallengeProgressFile>(json);
                return file ?? new ChallengeProgressFile();
            }
            catch (Exception e)
            {
                Debug.LogWarning("ChallengeProgress: failed to parse " + path + ": " + e.Message);
                return new ChallengeProgressFile();
            }
        }

        /// <summary>Save the entire progress file atomically.</summary>
        public static void Save(ChallengeProgressFile data)
        {
            if (data == null)
                return;
            EnsureDirectory();
            string path = Path.Combine(DirectoryPath, FileName);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }

        /// <summary>Find or create an entry for the given challenge id.</summary>
        public static ChallengeEntry GetOrCreate(ChallengeProgressFile file, string id, bool unlocked)
        {
            if (file.entries != null)
            {
                for (int i = 0; i < file.entries.Length; i++)
                {
                    if (file.entries[i] != null && file.entries[i].id == id)
                        return file.entries[i];
                }
            }

            var entry = new ChallengeEntry
            {
                id = id,
                stars = 0,
                bestTimeSeconds = 0f,
                bestComponentCount = 0,
                unlocked = unlocked
            };

            int oldLen = file.entries == null ? 0 : file.entries.Length;
            var newArr = new ChallengeEntry[oldLen + 1];
            if (file.entries != null)
                Array.Copy(file.entries, newArr, oldLen);
            newArr[oldLen] = entry;
            file.entries = newArr;
            return entry;
        }

        /// <summary>Update an entry with a new result, keeping best values.</summary>
        public static void RecordResult(ChallengeEntry entry, int stars, float timeSeconds, int componentCount)
        {
            if (entry == null)
                return;
            if (stars > entry.stars)
                entry.stars = stars;
            if (entry.bestTimeSeconds <= 0f || timeSeconds < entry.bestTimeSeconds)
                entry.bestTimeSeconds = timeSeconds;
            if (componentCount > 0 && (entry.bestComponentCount <= 0 || componentCount < entry.bestComponentCount))
                entry.bestComponentCount = componentCount;
        }
    }
}
