using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static RubysFrisbee.Assets.Editor.RubyAssetUtility;

namespace RubysFrisbee.Assets.Editor
{
    /// <summary>Deferred, once-per-version installer. Generated assets are owned by this package.</summary>
    [InitializeOnLoad]
    internal static class RubyPackageSetup
    {
        static bool queued, manualRequested, running;
        static double readySince = -1;
        static RubyPackageSetup()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess()) QueueAutomatic();
        }

        internal static void QueueAutomatic()
        {
            if (running || AssetDatabase.IsAssetImportWorkerProcess()) return;
            queued = true; readySince = -1;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Ruby's Frisbee/Setup or Repair")]
        static void SetupOrRepair()
        {
            if (running) { Debug.LogWarning("Ruby setup is already running."); return; }
            manualRequested = true;
            QueueAutomatic();
            Debug.Log("Ruby setup queued. It will start after asset import and script compilation finish, outside Play mode.");
        }

        [MenuItem("Tools/Ruby's Frisbee/Validate Package")]
        static void ValidateMenu()
        {
            if (Busy() || running)
            {
                Debug.LogWarning("Ruby validation is unavailable while importing, compiling, building, or playing. Finish that operation and choose Validate Package again.");
                return;
            }
            var report = NewReport("unknown");
            try
            {
                var manifest = ReadManifest();
                report.packageVersion = manifest.packageVersion;
                RubyPackageValidator.Validate(manifest, report);
            }
            catch (Exception e) { report.errors.Add(e.Message); }
            // Validation is read-only: never creates assets or changes the install success stamp.
            report.success = report.errors.Count == 0;
            LogReport(report, true);
        }

        static bool Busy() => AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer;

        static void Tick()
        {
            if (!queued || running) return;
            if (Busy()) { readySince = -1; return; }
            if (readySince < 0) { readySince = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - readySince < 2.0) return;
            EditorApplication.update -= Tick;
            queued = false;
            bool manual = manualRequested; manualRequested = false;
            if (!File.Exists(ManifestPath) && !manual) return;
            string version = "unknown";
            try
            {
                if (File.Exists(ManifestPath)) version = JsonUtility.FromJson<RubyAssetManifest>(File.ReadAllText(ManifestPath))?.packageVersion ?? "unknown";
            }
            catch { /* Full parsing below produces the actionable report. */ }
            string sessionKey = "RubysFrisbee.SetupAttempt." + Application.dataPath + "." + version;
            if (!manual && (SuccessfulVersion(version) || SessionState.GetBool(sessionKey, false))) return;
            // Stop a malformed package causing an import/reload retry storm. Explicit repair always bypasses this guard.
            SessionState.SetBool(sessionKey, true);
            Run(version);
        }

        internal static void BuildSynchronouslyForBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the Setup or Repair menu outside batch mode.");
            if (Busy() || running) throw new InvalidOperationException("Ruby batch setup requires compilation/import/build activity to finish first.");
            EditorApplication.update -= Tick;
            queued = false;
            var manifest = ReadManifest();
            Run(manifest.packageVersion);
            if (!SuccessfulVersion(manifest.packageVersion))
                throw new InvalidOperationException("Ruby setup failed. See " + ReportPath + " and the Unity log.");
        }

        static bool SuccessfulVersion(string version)
        {
            if (!File.Exists(ReportPath)) return false;
            try
            {
                var saved = JsonUtility.FromJson<RubySetupReport>(File.ReadAllText(ReportPath));
                return saved != null && saved.success && saved.packageVersion == version;
            }
            catch { return false; }
        }
        static RubySetupReport NewReport(string version) => new RubySetupReport
        {
            packageVersion = version, utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, success = false
        };
        static void Stage(RubySetupReport report, string name, Action action)
        {
            try { action(); report.completedStages.Add(name); }
            catch (Exception e) { throw new InvalidOperationException("Stage '" + name + "' failed: " + e.Message, e); }
        }

        static void Run(string candidateVersion)
        {
            running = true;
            var report = NewReport(candidateVersion);
            RubyAssetManifest manifest = null;
            try
            {
                // Invalidate the old success before any repair, even if a later stage or Editor session fails.
                WriteReport(report);
                manifest = ReadManifest();
                report.packageVersion = manifest.packageVersion;
                report.completedStages.Add("Manifest preflight");
                if (!Application.unityVersion.StartsWith("6000.0.", StringComparison.Ordinal))
                    report.warnings.Add("Authored for Unity 6000.0.34f1 Built-in 2D; current Editor is " + Application.unityVersion + ". Verify imports and runtime playback in this version.");
                Stage(report, "Add missing sorting layers", () => RubyAnimationBuilder.InstallSortingLayers(manifest));
                Stage(report, "Configure single-sprite imports", () => RubyAnimationBuilder.ImportTextures(manifest, report));
                Stage(report, "Build sprite animation clips", () => RubyAnimationBuilder.BuildClips(manifest, report));
                Stage(report, "Build Ruby and FX controllers", () => RubyAnimationBuilder.BuildControllers(manifest, report));
                Stage(report, "Build native sprite atlases", () => RubyAnimationBuilder.BuildAtlases(manifest, report));
                Stage(report, "Build visual prefabs", () => RubyVisualBuilder.BuildPrefabs(manifest, report));
                Stage(report, "Build and validate native audio", () =>
                {
                    // Audio's initial prefab creation is also isolated from user scenes.
                    InScratchScene(() => RubyAudioSetup.Build(Root, manifest.audio));
                    RubyPackageValidator.ValidateAudio(manifest); // Missing/import-failed mixer is fatal.
                    report.Generated(Root + "/Generated/Audio/RubyAudioBank.asset");
                    report.Generated(PrefabPath("RubyAudioRig"));
                    ++report.prefabCount;
                });
                Stage(report, "Create preview only if absent", () => RubyVisualBuilder.CreatePreviewIfMissing(manifest, report));
                AssetDatabase.SaveAssets();
                RubyPackageValidator.Validate(manifest, report);
                if (report.errors.Count == 0) report.completedStages.Add("All generated assets validated");
                report.success = report.errors.Count == 0;
            }
            catch (Exception e)
            {
                report.errors.Add(e.Message);
                report.success = false;
                Debug.LogException(e);
            }
            finally
            {
                try { WriteReport(report); }
                catch (Exception e)
                {
                    report.success = false;
                    report.errors.Add("Could not write setup report: " + e.Message);
                    Debug.LogError("Ruby setup could not persist a success stamp. Check write permissions for " + ReportPath + ". " + e.Message);
                }
                running = false;
                LogReport(report, false);
            }
        }

        static void InScratchScene(Action action)
        {
            var active = SceneManager.GetActiveScene();
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try { SceneManager.SetActiveScene(scratch); action(); }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (scratch.IsValid() && scratch.isLoaded) EditorSceneManager.CloseScene(scratch, true);
            }
        }
        static void WriteReport(RubySetupReport report)
        {
            EnsureParent(ReportPath);
            // Directly update this single known JSON asset; never delete user folders or write TagManager YAML.
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceSynchronousImport);
        }
        static void LogReport(RubySetupReport report, bool validationOnly)
        {
            string label = validationOnly ? "Ruby validation" : "Ruby setup";
            foreach (string warning in report.warnings) Debug.LogWarning(label + ": " + warning);
            if (!report.success)
            {
                Debug.LogError(label + " FAILED; no success stamp was set by this run.\n" + string.Join("\n", report.errors.Select(e => "- " + e)) +
                    (validationOnly ? "\nValidation is read-only. Repair from Tools/Ruby's Frisbee/Setup or Repair." : "\nReport: " + ReportPath + "\nFix the listed issue, then choose Tools/Ruby's Frisbee/Setup or Repair."));
                return;
            }
            Debug.Log(validationOnly ? "Ruby package validation passed in this Editor session. Test animation/audio playback in Play mode before game integration." :
                "Ruby setup completed for package " + report.packageVersion + ": " + report.textureCount + " textures, " + report.clipCount + " clips, " +
                report.controllerCount + " controllers, " + report.atlasCount + " atlases, " + report.prefabCount + " prefabs. Report: " + ReportPath +
                ". Existing user scenes were preserved. This is an asset preview, not full game integration.");
        }
    }

    internal sealed class RubyPackageImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // Schedule only; never mutate/import assets from an importer callback.
            bool Relevant(string path) => path.StartsWith(Root + "/", StringComparison.Ordinal) &&
                !path.StartsWith(Root + "/Generated/", StringComparison.Ordinal);
            if (imported.Any(Relevant) || moved.Any(Relevant)) RubyPackageSetup.QueueAutomatic();
        }
    }
}
