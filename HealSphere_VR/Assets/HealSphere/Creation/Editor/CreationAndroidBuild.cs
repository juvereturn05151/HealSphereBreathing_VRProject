using System;
using System.IO;
using HealSphere.Wellness.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace HealSphere.Creation.Editor
{
    public static class CreationAndroidBuild
    {
        public static void Build()
        {
            CreationShaderRetention.Ensure();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support for the existing Unity version.");
            var output = Environment.GetEnvironmentVariable("HEALSPHERE_APK_OUTPUT") ?? "Builds/HealSphereCreation/HealSphereCreation.apk";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { CreationSceneAssembler.ScenePath, MomentExperienceAssembler.ScenePath },
                locationPathName = output, target = BuildTarget.Android, options = BuildOptions.Development
            });
            File.WriteAllText("Library/HealSphereCreationAndroidBuild.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nBytes: " + report.summary.totalSize + "\n" + Path.GetFullPath(output));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Creation Android build failed; inspect Editor log.");
        }
    }
}
