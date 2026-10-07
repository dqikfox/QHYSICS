using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ForgeQuestBuild
{
    public static void BuildQuest()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Debug.Log("ForgeQuestBuild: scenes = " + string.Join(", ", scenes));
        var opts = new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = @"C:\Users\KING\projects\QHYSICS-builds\QHYSICS-2026-10-08.apk",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };
        var r = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"ForgeQuestBuild: RESULT {r.summary.result} size={r.summary.totalSize} errors={r.summary.totalErrors}");
        EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
