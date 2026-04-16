using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Base class for build pipelines. Can be extended by projects to override specific build steps.
/// </summary>
public class BuildPipelineBase
    {
        public virtual void BUILD_WIN() => BuildInternal(BuildTarget.StandaloneWindows64);
        public virtual void BUILD_OSX() => BuildInternal(BuildTarget.StandaloneOSX);
        public virtual void BUILD_LINUX() => BuildInternal(BuildTarget.StandaloneLinux64);
        public virtual void BUILD_ANDROID() => BuildInternal(BuildTarget.Android);
        public virtual void BUILD_IOS() => BuildInternal(BuildTarget.iOS);

        /// <summary>
        /// Core build logic that coordinates the build steps.
        /// </summary>
        protected virtual void BuildInternal(BuildTarget target)
        {
            LogEnvironmentVariables();

            if (!TryGetEnv("OUTPUT_PATH", out var outputPath))
            {
                Debug.LogError("[BUILD] Missing arg OUTPUT_PATH");
                EditorApplication.Exit(1);
                return;
            }

            var buildNumber = GetBuildNumber();
            var commitHash = GetCommitHash();

            if (!string.IsNullOrEmpty(buildNumber) && !string.IsNullOrEmpty(commitHash))
            {
                var shortHash = commitHash.Length >= 5 ? commitHash.Substring(0, 5) : commitHash;
                PlayerSettings.bundleVersion = $"{PlayerSettings.bundleVersion}.{buildNumber}-{shortHash}";
            }

            var scriptingImplementation = GetScriptingImplementation();
            var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            var namedTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);

            PlayerSettings.SetScriptingBackend(namedTarget, scriptingImplementation);

            ApplyScriptingDefines(namedTarget, targetGroup);

            var scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BUILD] No scenes configured");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[BUILD] Target: {target}");
            Debug.Log($"[BUILD] Scenes: {string.Join(", ", scenes)}");

            var buildPlayerOptions = GetBuildPlayerOptions(target, scenes, outputPath);
            
            var report = UnityEditor.BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("[BUILD] Build failed");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("[BUILD] Build succeeded");
            }
        }

        /// <summary>
        /// Retrieves the build number from environment variables.
        /// </summary>
        protected virtual string GetBuildNumber()
        {
            if (!TryGetEnv("BUILD_NUMBER", out var buildNumber))
            {
                Debug.LogError("[BUILD] Missing arg BUILD_NUMBER");
                EditorApplication.Exit(1);
                return null;
            }
            return buildNumber;
        }

        /// <summary>
        /// Retrieves the commit hash from environment variables.
        /// </summary>
        protected virtual string GetCommitHash()
        {
            if (!TryGetEnv("COMMIT_HASH", out var commitHash))
            {
                Debug.LogError("[BUILD] Missing arg COMMIT_HASH");
                EditorApplication.Exit(1);
                return null;
            }
            return commitHash;
        }

        /// <summary>
        /// Configures the scripting backend (Mono/IL2CPP) based on environment variables.
        /// </summary>
        protected virtual ScriptingImplementation GetScriptingImplementation()
        {
            if (!TryGetEnv("SCRIPTING_BACKEND", out var scriptingBackend))
            {
                Debug.Log("[BUILD] Missing arg SCRIPTING_BACKEND, defaulting to MONO");
                scriptingBackend = "MONO";
            }

            return scriptingBackend.ToUpper() switch
            {
                "MONO" => ScriptingImplementation.Mono2x,
                _ => ScriptingImplementation.IL2CPP
            };
        }

        /// <summary>
        /// Applies project-specific scripting defines (e.g., ENV_PRODUCTION).
        /// </summary>
        protected virtual void ApplyScriptingDefines(NamedBuildTarget namedTarget, BuildTargetGroup targetGroup)
        {
            if (TryGetEnv("TARGET_ENV", out var targetEnv) && targetEnv.ToUpper() == "PRODUCTION")
            {
                var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                if (!currentDefines.Contains("ENV_PRODUCTION"))
                {
                    var newDefines = string.IsNullOrEmpty(currentDefines) ? "ENV_PRODUCTION" : currentDefines + ";ENV_PRODUCTION";
                    PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
                    Debug.Log($"[BUILD] Added ENV_PRODUCTION to scripting define symbols for {targetGroup}");
                }
            }
        }

        /// <summary>
        /// Configures build options for Unity's BuildPipeline.
        /// </summary>
        protected virtual BuildPlayerOptions GetBuildPlayerOptions(BuildTarget target, string[] scenes, string outputPath)
        {
            return new BuildPlayerOptions
            {
                target = target,
                scenes = scenes,
                locationPathName = outputPath,
                options = BuildOptions.None
            };
        }

        protected bool TryGetEnv(string name, out string value)
        {
            value = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrEmpty(value);
        }

        protected bool TryGetArg(string name, out string value)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith(name + "="))
                {
                    value = arg[(name.Length + 1)..];
                    return true;
                }
            }

            value = null;
            return false;
        }

        protected string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        protected void LogEnvironmentVariables()
        {
            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            var log = "[BUILD] Environment variables: [ ";
            foreach (DictionaryEntry de in environmentVariables)
            {
                log += $"{de.Key}={de.Value}, ";
            }
            Debug.Log(log + " ]");
        }

#if UNITY_CLOUD_BUILD
        /// <summary>
        /// Entry point for Unity Cloud Build pre-export step.
        /// </summary>
        public virtual void PreExport(UnityEngine.CloudBuild.BuildManifestObject manifest)
        {
            Debug.Log($"[CLOUD BUILD] CloudBuildPreExport");
            
            var scriptingImplementation = GetCloudBuildScriptingImplementation();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone), scriptingImplementation);

            ApplyCloudBuildScriptingDefines();

            var buildNumber = manifest.GetValue("buildNumber", null);
            var commitHash = manifest.GetValue("scmCommitId", null);
            if (commitHash != null && commitHash.Length >= 5) commitHash = commitHash.Substring(0, 5);
            
            PlayerSettings.bundleVersion = $"{PlayerSettings.bundleVersion}.{buildNumber}-{commitHash}";
            Debug.Log($"[CLOUD BUILD] Version: {PlayerSettings.bundleVersion}");
        }

        protected virtual ScriptingImplementation GetCloudBuildScriptingImplementation()
        {
            if (!TryGetEnv("SCRIPTING_BACKEND", out var scriptingBackend))
            {
                Debug.Log("[CLOUD BUILD] Missing arg SCRIPTING_BACKEND, defaulting to IL2CPP");
                scriptingBackend = "IL2CPP";
            }

            return scriptingBackend.ToUpper() switch
            {
                "MONO" => ScriptingImplementation.Mono2x,
                _ => ScriptingImplementation.IL2CPP
            };
        }

        protected virtual void ApplyCloudBuildScriptingDefines()
        {
            if (TryGetEnv("TARGET_ENV", out var targetEnv) && targetEnv.ToUpper() == "PRODUCTION")
            {
                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone);
                var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                if (!currentDefines.Contains("ENV_PRODUCTION"))
                {
                    var newDefines = string.IsNullOrEmpty(currentDefines) ? "ENV_PRODUCTION" : currentDefines + ";ENV_PRODUCTION";
                    PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
                    Debug.Log($"[CLOUD BUILD] Added ENV_PRODUCTION to scripting define symbols for Standalone");
                }
            }
        }
#endif
    }

    /// <summary>
    /// Static entry point for Unity's command line build.
    /// Automatically detects and uses any class inheriting from <see cref="BuildPipelineBase"/>.
    /// </summary>
    public static class Build
    {
        private static BuildPipelineBase _instance;

        public static BuildPipelineBase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindCustomPipeline() ?? new BuildPipelineBase();
                }
                return _instance;
            }
        }

        private static BuildPipelineBase FindCustomPipeline()
        {
            var customType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t != typeof(BuildPipelineBase) && typeof(BuildPipelineBase).IsAssignableFrom(t));

            if (customType != null)
            {
                Debug.Log($"[BUILD] Using custom build pipeline: {customType.FullName}");
                return (BuildPipelineBase)Activator.CreateInstance(customType);
            }

            return null;
        }

        // Used by projects to inject custom behavior manually if needed
        public static void SetInstance(BuildPipelineBase newInstance) => _instance = newInstance;

        public static void BUILD_WIN() => Instance.BUILD_WIN();
        public static void BUILD_OSX() => Instance.BUILD_OSX();
        public static void BUILD_LINUX() => Instance.BUILD_LINUX();
        public static void BUILD_ANDROID() => Instance.BUILD_ANDROID();
        public static void BUILD_IOS() => Instance.BUILD_IOS();

#if UNITY_CLOUD_BUILD
        public static void PreExportStandalone(UnityEngine.CloudBuild.BuildManifestObject manifest) => Instance.PreExport(manifest);
#endif
}
