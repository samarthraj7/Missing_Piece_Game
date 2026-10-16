using UnityEditor;
using UnityEngine;

public static class EchoWebBuild
{
    [MenuItem("Echo/Build WebGL to docs")]
    public static void Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "docs",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Echo WebGL build: {report.summary.result} ({report.summary.totalErrors} errors)");
        if (Application.isBatchMode && report.summary.totalErrors > 0)
            EditorApplication.Exit(1);
    }
}
