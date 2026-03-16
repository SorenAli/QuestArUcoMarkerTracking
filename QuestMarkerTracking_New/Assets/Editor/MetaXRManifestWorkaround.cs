// Workaround for Meta XR SDK 85.0.0 bug:
// UpdateManifestWithCodeSample tries to read an aapt-processed manifest that only
// exists after Gradle runs, but it's called in IPostGenerateGradleAndroidProject
// (which runs BEFORE Gradle). This script pre-creates the expected file.
#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

public class MetaXRManifestWorkaround : IPostGenerateGradleAndroidProject
{
    // Run before Meta XR SDK's UpdateManifestWithCodeSample (callbackOrder 0)
    public int callbackOrder => -1;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // The Meta XR SDK script (UpdateManifestWithCodeSample) searches ALL AndroidManifest.xml
        // files recursively, including stale Gradle build artifacts from previous builds like:
        // xrmanifest.androidlib/build/intermediates/aapt_friendly_merged_manifests/.../AndroidManifest.xml
        // These aapt-processed files are deleted by Bee during the build but AFTER Directory.GetFiles
        // already found them, causing a DirectoryNotFoundException in XmlDocument.Load.
        // Fix: delete the stale build/ dir before the Meta SDK script searches.
        string buildDir = Path.Combine(path, "xrmanifest.androidlib", "build");
        if (Directory.Exists(buildDir))
        {
            Directory.Delete(buildDir, recursive: true);
            Debug.Log($"[MetaXRManifestWorkaround] Deleted stale Gradle build dir: {buildDir}");
        }
    }
}
#endif
