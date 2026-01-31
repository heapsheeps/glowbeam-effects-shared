using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Glowbeam.Effects.Editor
{
    [Serializable]
    public class GlowCompileCache
    {
        [Serializable]
        public class CacheEntry
        {
            public string glowPath;
            public string compiledShaderPath;
            public string thumbnailPath;
            public string glowHash;
            public string templateHash;
            public string coreHash;
            public string compilerVersion;
        }

        [Serializable]
        private class CacheFile
        {
            public List<CacheEntry> entries = new List<CacheEntry>();
        }

        private readonly Dictionary<string, CacheEntry> entriesByGlowPath = new Dictionary<string, CacheEntry>();

        public static string ComputeFileHash(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return string.Empty;
            }

            using (var sha = SHA256.Create())
            {
                byte[] bytes = File.ReadAllBytes(path);
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public CacheEntry Get(string glowPath)
        {
            if (string.IsNullOrEmpty(glowPath))
            {
                return null;
            }

            entriesByGlowPath.TryGetValue(glowPath, out var entry);
            return entry;
        }

        public void Set(CacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.glowPath))
            {
                return;
            }

            entriesByGlowPath[entry.glowPath] = entry;
        }

        public static GlowCompileCache Load(string cachePath)
        {
            var cache = new GlowCompileCache();

            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
            {
                return cache;
            }

            try
            {
                string json = File.ReadAllText(cachePath);
                var file = JsonUtility.FromJson<CacheFile>(json);
                if (file != null && file.entries != null)
                {
                    foreach (var entry in file.entries)
                    {
                        if (!string.IsNullOrEmpty(entry.glowPath))
                        {
                            cache.entriesByGlowPath[entry.glowPath] = entry;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GlowCompileCache] Failed to read cache: {e.Message}");
            }

            return cache;
        }

        public void Save(string cachePath)
        {
            if (string.IsNullOrEmpty(cachePath))
            {
                return;
            }

            try
            {
                var file = new CacheFile();
                foreach (var entry in entriesByGlowPath.Values)
                {
                    file.entries.Add(entry);
                }

                string json = JsonUtility.ToJson(file, true);
                File.WriteAllText(cachePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GlowCompileCache] Failed to write cache: {e.Message}");
            }
        }
    }
}
