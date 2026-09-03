using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LunaGame.Editor
{
    public static class LunaBuild
    {
        private const string MainScene = "Assets/Scenes/Main.unity";
        private const string ApplicationId = "com.winstonetonton.lunagame";

        [MenuItem("Luna/Build/Android App Bundle")]
        public static void BuildAndroidAppBundle()
        {
            ConfigurePlayer();
            RequireSupport(BuildTargetGroup.Android, BuildTarget.Android, "Android Build Support");
            EditorUserBuildSettings.buildAppBundle = true;
            Build(BuildTarget.Android, "Builds/Android/LunaGame.aab", BuildOptions.None);
        }

        [MenuItem("Luna/Build/Android Development APK")]
        public static void BuildAndroidDevelopmentApk()
        {
            ConfigurePlayer();
            RequireSupport(BuildTargetGroup.Android, BuildTarget.Android, "Android Build Support");
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, "Builds/Android/LunaGame-dev.apk", BuildOptions.Development);
        }

        [MenuItem("Luna/Build/iOS Xcode Project")]
        public static void ExportIosXcodeProject()
        {
            ConfigurePlayer();
            RequireSupport(BuildTargetGroup.iOS, BuildTarget.iOS, "iOS Build Support");
            Build(BuildTarget.iOS, "Builds/iOS", BuildOptions.None);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Winston E Tonton";
            PlayerSettings.productName = "Luna Game";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationId);
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.iOS.buildNumber = "1";
        }

        private static void RequireSupport(BuildTargetGroup group, BuildTarget target, string module)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new InvalidOperationException($"{module} is not installed in this Unity Editor.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                throw new InvalidOperationException($"Could not switch Unity to {target}.");
        }

        private static void Build(BuildTarget target, string outputPath, BuildOptions options)
        {
            var fullPath = Path.GetFullPath(outputPath);
            var directory = target == BuildTarget.iOS ? fullPath : Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory ?? fullPath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = fullPath,
                target = target,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"{target} build failed: {report.summary.result}");
            Debug.Log($"Luna Game {target} build: {fullPath}");
        }
    }
}
