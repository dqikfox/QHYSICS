using System;
using System.IO;
using UnityEngine;

namespace RealityEngine.Experiments
{
    /// <summary>
    /// JSON save/load under Application.persistentDataPath/RealityEngine/experiments/.
    /// </summary>
    public static class ExperimentStore
    {
        public const string RelativeFolder = "RealityEngine/experiments";

        public static string DirectoryPath =>
            Path.Combine(Application.persistentDataPath, "RealityEngine", "experiments");

        public static void EnsureDirectory()
        {
            string dir = DirectoryPath;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static string Save(ExperimentRunRecord record)
        {
            if (record == null)
                return null;
            EnsureDirectory();
            if (string.IsNullOrEmpty(record.savedUtc))
                record.savedUtc = DateTime.UtcNow.ToString("o");
            string id = record.definition != null && !string.IsNullOrEmpty(record.definition.id)
                ? Sanitize(record.definition.id)
                : "run";
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string file = Path.Combine(DirectoryPath, id + "_" + stamp + ".json");
            string json = JsonUtility.ToJson(record, true);
            File.WriteAllText(file, json);
            File.WriteAllText(Path.Combine(DirectoryPath, "latest.json"), json);
            return file;
        }

        public static ExperimentRunRecord LoadLatest()
        {
            EnsureDirectory();
            string latest = Path.Combine(DirectoryPath, "latest.json");
            if (File.Exists(latest))
                return LoadFile(latest);

            string[] files = ListFiles();
            if (files == null || files.Length == 0)
                return null;
            return LoadFile(files[0]);
        }

        public static string[] ListFiles()
        {
            EnsureDirectory();
            string[] files = Directory.GetFiles(DirectoryPath, "*.json");
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return files;
        }

        public static ExperimentRunRecord LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json))
                return null;
            return JsonUtility.FromJson<ExperimentRunRecord>(json);
        }

        static string Sanitize(string id)
        {
            char[] chars = id.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
