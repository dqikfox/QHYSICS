// Lets tools outside Unity enter/exit Play mode by dropping a trigger file.
// play.trigger -> EnterPlaymode, stop.trigger -> ExitPlaymode (in C:\Users\KING\projects\forge-tools).
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class ForgePlayTrigger
{
    const string Dir = @"C:\Users\KING\projects\forge-tools";
    static double _next;

    static ForgePlayTrigger()
    {
        EditorApplication.update += Poll;
        Debug.Log("ForgePlayTrigger: watching " + Dir + " for play.trigger / stop.trigger");
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < _next) return;
        _next = EditorApplication.timeSinceStartup + 1.0;
        try
        {
            string play = Path.Combine(Dir, "play.trigger");
            string stop = Path.Combine(Dir, "stop.trigger");
            if (File.Exists(play))
            {
                File.Delete(play);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    Debug.Log("ForgePlayTrigger: already in Play mode");
                else if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    Debug.Log("ForgePlayTrigger: editor busy (compiling/importing), retrying play shortly");
                    File.WriteAllText(play, "retry");
                    _next = EditorApplication.timeSinceStartup + 3.0;
                }
                else
                {
                    Debug.Log("ForgePlayTrigger: entering Play mode");
                    EditorApplication.EnterPlaymode();
                }
            }
            if (File.Exists(stop))
            {
                File.Delete(stop);
                if (EditorApplication.isPlaying)
                {
                    Debug.Log("ForgePlayTrigger: exiting Play mode");
                    EditorApplication.ExitPlaymode();
                }
                else Debug.Log("ForgePlayTrigger: not in Play mode");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("ForgePlayTrigger: " + e.Message);
        }
    }
}
