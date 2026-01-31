using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Glowbeam.Effects.Editor
{
    public static class GlowEffectCompiler
    {
        private const string GeneratedRoot = "Assets/Effects_Generated";
        private const string CachePath = "Assets/Effects_Generated/.glowcache.json";
        private const string CompilerVersion = "1";

        private const string TemplatePath = "Packages/com.glowbeam.effects/Templates/EffectTemplate.shader.txt";
        private const string CorePath = "Packages/com.glowbeam.effects/ShaderLibrary/Core.hlsl";

        private const string DefaultScanTexturePath = "Packages/com.glowbeam.effects/Editor/Resources/DefaultTextures/default_scan.jpg";
        private const string DefaultDepthTexturePath = "Packages/com.glowbeam.effects/Editor/Resources/DefaultTextures/default_depth.jpg";

        [MenuItem("Glowbeam/Compile Glow Effects")]
        public static void CompileAllMenu()
        {
            CompileAllEffects();
        }

        public static void CompileAllEffects()
        {
            EnsureDirectory(GeneratedRoot);

            var cache = GlowCompileCache.Load(CachePath);
            string templateHash = GlowCompileCache.ComputeFileHash(TemplatePath);
            string coreHash = GlowCompileCache.ComputeFileHash(CorePath);

            if (string.IsNullOrEmpty(templateHash) || string.IsNullOrEmpty(coreHash))
            {
                Debug.LogError("[GlowEffectCompiler] Missing template or core shader files. Aborting compile.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string packageEffectsRoot = Path.Combine(projectRoot, "Packages/com.glowbeam.effects/Effects");

            var glowFiles = new List<string>();
            if (Directory.Exists(packageEffectsRoot))
            {
                glowFiles.AddRange(Directory.GetFiles(packageEffectsRoot, "*.glow", SearchOption.AllDirectories)
                    .Select(path => ToProjectPath(path)));
            }

            glowFiles = glowFiles
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .ToList();

            if (glowFiles.Count == 0)
            {
                Debug.Log("[GlowEffectCompiler] No .glow files found under Packages/com.glowbeam.effects/Effects.");
                return;
            }

            int compiledCount = 0;
            int skippedCount = 0;
            var usedCompiledPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Texture2D defaultScan = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultScanTexturePath);
            Texture2D defaultDepth = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultDepthTexturePath);
            if (defaultScan == null || defaultDepth == null)
            {
                Debug.LogWarning("[GlowEffectCompiler] Default textures not found. Thumbnails will use null textures.");
            }

            var thumbnailRenderer = new GlowThumbnailRenderer();

            try
            {
                foreach (var glowPath in glowFiles)
                {
                    string glowHash = GlowCompileCache.ComputeFileHash(glowPath);
                    if (string.IsNullOrEmpty(glowHash))
                    {
                        Debug.LogWarning($"[GlowEffectCompiler] Failed to read {glowPath}, skipping.");
                        continue;
                    }

                    string compiledPath = GetCompiledShaderPath(glowPath);
                    string thumbnailPath = GetThumbnailPath(glowPath);
                    if (!usedCompiledPaths.Add(compiledPath))
                    {
                        Debug.LogError($"[GlowEffectCompiler] Duplicate output path {compiledPath} from {glowPath}. Rename the effect to avoid collisions.");
                        continue;
                    }

                    var cacheEntry = cache.Get(glowPath);
                    bool isStale = cacheEntry == null
                        || cacheEntry.glowHash != glowHash
                        || cacheEntry.templateHash != templateHash
                        || cacheEntry.coreHash != coreHash
                        || cacheEntry.compilerVersion != CompilerVersion
                        || cacheEntry.compiledShaderPath != compiledPath
                        || cacheEntry.thumbnailPath != thumbnailPath
                        || string.IsNullOrEmpty(cacheEntry.compiledShaderPath)
                        || !File.Exists(cacheEntry.compiledShaderPath)
                        || string.IsNullOrEmpty(cacheEntry.thumbnailPath)
                        || !File.Exists(cacheEntry.thumbnailPath);

                    if (!isStale)
                    {
                        skippedCount++;
                        continue;
                    }

                    string effectName = Path.GetFileNameWithoutExtension(glowPath);
                    string glowCode = File.ReadAllText(glowPath);

                    if (!ShaderTemplateProcessor.ValidateUserCode(glowCode, out string validationError))
                    {
                        Debug.LogError($"[GlowEffectCompiler] Validation failed for {glowPath}: {validationError}");
                        continue;
                    }

                    string fullShaderCode = ShaderTemplateProcessor.ProcessShaderCode(glowCode, effectName);
                    if (string.IsNullOrEmpty(fullShaderCode))
                    {
                        Debug.LogError($"[GlowEffectCompiler] Failed to compile {glowPath}");
                        continue;
                    }

                    EnsureDirectory(Path.GetDirectoryName(compiledPath));
                    File.WriteAllText(compiledPath, fullShaderCode);
                    AssetDatabase.ImportAsset(compiledPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(compiledPath);
                    if (shader == null)
                    {
                        Debug.LogError($"[GlowEffectCompiler] Failed to load compiled shader at {compiledPath}");
                        continue;
                    }

                    Texture2D thumbnail = thumbnailRenderer.RenderThumbnail(shader, defaultScan, defaultDepth, 512);
                    if (thumbnail != null)
                    {
                        EnsureDirectory(Path.GetDirectoryName(thumbnailPath));
                        File.WriteAllBytes(thumbnailPath, thumbnail.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(thumbnail);
                        AssetDatabase.ImportAsset(thumbnailPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }

                    cache.Set(new GlowCompileCache.CacheEntry
                    {
                        glowPath = glowPath,
                        compiledShaderPath = compiledPath,
                        thumbnailPath = thumbnailPath,
                        glowHash = glowHash,
                        templateHash = templateHash,
                        coreHash = coreHash,
                        compilerVersion = CompilerVersion
                    });

                    compiledCount++;
                }
            }
            finally
            {
                thumbnailRenderer.Cleanup();
            }

            cache.Save(CachePath);
            AssetDatabase.Refresh();

            Debug.Log($"[GlowEffectCompiler] Compile complete. Compiled: {compiledCount}, Skipped: {skippedCount}");
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static string GetCompiledShaderPath(string glowPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(glowPath);
            string compiledFile = $"{fileName}.shader";
            string combined = Path.Combine(GeneratedRoot, compiledFile);
            return combined.Replace("\\", "/");
        }

        private static string GetThumbnailPath(string glowPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(glowPath);
            string thumbnailFile = $"{fileName}_thumbnail.png";
            string combined = Path.Combine(GeneratedRoot, thumbnailFile);
            return combined.Replace("\\", "/");
        }

        private static string ToProjectPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return string.Empty;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
            string normalized = absolutePath.Replace("\\", "/");
            if (!normalized.StartsWith(projectRoot))
            {
                return string.Empty;
            }

            string relative = normalized.Substring(projectRoot.Length + 1);
            return relative.Replace("\\", "/");
        }
    }

    public class GlowEffectCompileBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            GlowEffectCompiler.CompileAllEffects();
        }
    }
}
