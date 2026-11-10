using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WordWars.EditorTools
{
    /// <summary>
    /// 一键导出 GitHub Pages 用的 WebGL 包到项目根目录 docs/。
    /// 菜单：Word Wars/Build/WebGL Demo
    /// 命令行：Unity -batchmode -quit -projectPath ... -executeMethod WordWars.EditorTools.WebGLBuilder.Build
    /// </summary>
    public static class WebGLBuilder
    {
        // 兜底：Build Settings 为空时才用这份写死的清单。
        // 2026-11-10：原来这里只有 MainMenu/LevelSelect/Game，漏了商店场景——网页版点「商店」进不去。
        // 改为优先读 Build Settings 里 enabled 的场景，以后加场景不用再改代码。
        private static readonly string[] FallbackScenes =
        {
            "Assets/_Project/Scenes/MainMenu.unity",
            "Assets/_Project/Scenes/LevelSelect.unity",
            "Assets/_Project/Scenes/Shop.unity",
            "Assets/_Project/Scenes/Game.unity",
        };

        private static string[] EnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            var settings = EditorBuildSettings.scenes;
            if (settings != null)
                foreach (var s in settings)
                    if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                        list.Add(s.path);
            return list.Count > 0 ? list.ToArray() : FallbackScenes;
        }

        [MenuItem("Word Wars/Build/WebGL Demo")]
        public static void Build()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(projectRoot, "docs");

            if (Directory.Exists(output))
                Directory.Delete(output, true);
            Directory.CreateDirectory(output);

            // GitHub Pages 不会替 Unity 的 .gz/.br 设置 Content-Encoding。
            // Gzip + Decompression Fallback 让浏览器在纯静态托管上也能加载。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.runInBackground = true;
            PlayerSettings.productName = "Word Wars";
            PlayerSettings.companyName = "Independent";

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[WebGLBuilder] Build failed: " + report.summary.result);
                EditorApplication.Exit(1);
                return;
            }

            // 阻止 GitHub Pages 的 Jekyll 处理 Unity 生成的文件（尤其 TemplateData）。
            File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
            Debug.Log("[WebGLBuilder] Build succeeded: " + output +
                      " size=" + (report.summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB");
        }
    }
}
