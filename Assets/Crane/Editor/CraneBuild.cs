using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScrapCrane.Editor
{
    public static class CraneBuild
    {
        const string ScenePath = "Assets/Scenes/PatioDeChatarra.unity";
        const string OutputPath = "Build/WebGL";

        // Available to the connected CLI after the synchronous build returns.
        public static string LastBuildReport { get; private set; } = "Sin build ejecutada.";

        [MenuItem("Grúa/Generar build WebGL")]
        public static void BuildWebGL()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Falta la escena del patio. Ejecutá CraneSceneBuilder.Build() primero.", ScenePath);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Esta instalación de Unity no tiene el módulo WebGL Build Support.");

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "PROJECT:Patio";
            PlayerSettings.defaultWebScreenWidth = 1600;
            PlayerSettings.defaultWebScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;
            Directory.CreateDirectory(OutputPath);
            AssetDatabase.SaveAssets();

            LastBuildReport = "Build WebGL en curso.";
            Debug.Log("Generando WebGL en " + Path.GetFullPath(OutputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            var summary = report.summary;
            LastBuildReport = string.Format(
                "WebGL: {0}; duración {1}; tamaño {2:N0} bytes; errores {3}; advertencias {4}; salida {5}",
                summary.result, summary.totalTime, summary.totalSize, summary.totalErrors,
                summary.totalWarnings, Path.GetFullPath(OutputPath));
            File.WriteAllText("Build/WebGLBuildReport.txt", LastBuildReport + Environment.NewLine);
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(LastBuildReport);
                throw new InvalidOperationException("La build WebGL no terminó correctamente. " + LastBuildReport);
            }
            Debug.Log(LastBuildReport);
        }
    }
}
