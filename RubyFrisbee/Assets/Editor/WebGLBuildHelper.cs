using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RubyFrisbee.Editor
{
  /// <summary>
  /// Configures the project for WebGL builds playable in mobile browsers.
  /// </summary>
  public static class WebGLBuildHelper
  {
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    const string DefaultBuildFolder = "Builds/WebGL";

    [MenuItem("Ruby Frisbee/Configure WebGL Build")]
    public static void ConfigureWebGLBuild()
    {
      if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
      {
        Debug.LogError("Failed to switch to WebGL. Install WebGL Build Support in Unity Hub.");
        return;
      }

      var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
      EditorBuildSettings.scenes = scenes;

      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
      PlayerSettings.runInBackground = true;
      PlayerSettings.defaultWebScreenWidth = 960;
      PlayerSettings.defaultWebScreenHeight = 600;

      Debug.Log("WebGL build configured. Scene added to build settings. Compression disabled for easier static hosting.");
    }

    [MenuItem("Ruby Frisbee/Build WebGL")]
    public static void BuildWebGL()
    {
      ConfigureWebGLBuild();

      if (!System.IO.File.Exists(ScenePath))
      {
        Debug.LogError("Game scene not found. Run Ruby Frisbee -> Setup Game Scene first.");
        return;
      }

      var outputPath = System.IO.Path.Combine(DefaultBuildFolder, "index.html");
      var report = BuildPipeline.BuildPlayer(
        new[] { ScenePath },
        outputPath,
        BuildTarget.WebGL,
        BuildOptions.None);

      if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
      {
        CopyVercelConfig();
        Debug.Log($"WebGL build succeeded: {outputPath}\nDeploy to Vercel: cd {DefaultBuildFolder} && npx vercel --prod");
      }
      else
      {
        Debug.LogError($"WebGL build failed: {report.summary.result}");
      }
    }

    static void CopyVercelConfig()
    {
      var source = System.IO.Path.Combine("deploy", "vercel.json");
      var destination = System.IO.Path.Combine(DefaultBuildFolder, "vercel.json");

      if (!System.IO.File.Exists(source))
      {
        return;
      }

      System.IO.File.Copy(source, destination, true);
    }
  }
}
