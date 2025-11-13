using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Glowbeam.Effects
{
    /// <summary>
    /// Processes simplified shader code and generates full shader code from templates
    /// </summary>
    public static class ShaderTemplateProcessor
    {
        private const string TEMPLATE_WITH_PLACEHOLDERS_PATH = "Packages/com.glowbeam.effects/Shaders/EffectTemplate.shader.txt";
        private const string DERIVED_PROPERTIES_PLACEHOLDER = "<DERIVED_USER_PROPERTIES_PLACEHOLDER>";
        private const string USER_CODE_PLACEHOLDER = "<USER_CODE_PLACEHOLDER>";
        private const string EFFECT_NAME_PLACEHOLDER = "MyEffect";

        /// <summary>
        /// Represents a parsed shader property
        /// </summary>
        private class ShaderProperty
        {
            public string Name { get; set; }
            public string HlslType { get; set; }
            public string FullDeclaration { get; set; }
        }

        /// <summary>
        /// Line mapping information for error reporting
        /// </summary>
        public class LineMapping
        {
            public int UserCodeStartLine = 73; // Line where user code starts in compiled shader
            public int NumPropertyLines = 0;   // Number of HLSL variable declarations
            public int EffectMainStartLineInSimplified = 0; // Where EffectMain starts in simplified code
        }
        
        private static LineMapping _lastLineMapping = null;
        
        /// <summary>
        /// Gets the line mapping from the last ProcessShaderCode call
        /// </summary>
        public static LineMapping GetLastLineMapping()
        {
            return _lastLineMapping;
        }
        
        /// <summary>
        /// Maps a line number from compiled shader to simplified shader
        /// </summary>
        public static int MapCompiledLineToSimplified(int compiledLine)
        {
            if (_lastLineMapping == null)
            {
                Debug.LogWarning($"[LineMapping] No mapping available, returning original line {compiledLine}");
                return compiledLine;
            }
            
            Debug.Log($"[LineMapping] Mapping line {compiledLine}:");
            Debug.Log($"  - UserCodeStartLine: {_lastLineMapping.UserCodeStartLine}");
            Debug.Log($"  - NumPropertyLines: {_lastLineMapping.NumPropertyLines}");
            Debug.Log($"  - EffectMainStartLineInSimplified: {_lastLineMapping.EffectMainStartLineInSimplified}");
            
            // If error is before user code section, can't map it
            if (compiledLine < _lastLineMapping.UserCodeStartLine)
            {
                Debug.LogWarning($"[LineMapping] Line {compiledLine} is before user code section, returning 0");
                return 0;
            }
            
            // Calculate offset into user code section
            int offsetInUserCode = compiledLine - _lastLineMapping.UserCodeStartLine;
            Debug.Log($"  - Offset into user code: {offsetInUserCode}");
            
            // Subtract the HLSL variable declarations (one per property)
            offsetInUserCode -= _lastLineMapping.NumPropertyLines;
            Debug.Log($"  - After subtracting properties: {offsetInUserCode}");
            
            // If still in variable declarations, map to simplified properties section
            if (offsetInUserCode < 0)
            {
                Debug.Log($"[LineMapping] Still in variable declarations, returning line 1");
                return 1; // Just point to first line of simplified code
            }
            
            // Now we're in EffectMain territory - add to where EffectMain starts in simplified
            int mappedLine = _lastLineMapping.EffectMainStartLineInSimplified + offsetInUserCode;
            Debug.Log($"[LineMapping] Mapped to line {mappedLine}");
            return mappedLine;
        }
        
        /// <summary>
        /// Processes simplified user shader code and generates full shader code
        /// </summary>
        /// <param name="userCode">The simplified shader code (properties + EffectMain)</param>
        /// <param name="effectName">The name of the effect</param>
        /// <returns>The full generated shader code</returns>
        public static string ProcessShaderCode(string userCode, string effectName)
        {
            if (string.IsNullOrEmpty(userCode))
            {
                Debug.LogError("User shader code is empty");
                return string.Empty;
            }

            // Load the template
            string template = LoadTemplateWithPlaceholders();
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            // Parse the user code
            var (properties, effectMainCode) = ParseUserCode(userCode);
            
            // Calculate line mapping for error reporting
            _lastLineMapping = new LineMapping();
            _lastLineMapping.NumPropertyLines = properties.Count;
            
            // Find where EffectMain starts in simplified code
            var lines = userCode.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("EffectMain()"))
                {
                    _lastLineMapping.EffectMainStartLineInSimplified = i + 1; // 1-indexed
                    break;
                }
            }

            // Generate property declarations for the Properties block
            string propertyDeclarations = GeneratePropertyDeclarations(properties);

            // Generate HLSL variable declarations
            string hlslVariables = GenerateHlslVariables(properties);

            // Combine HLSL variables with user's EffectMain code
            string fullUserCode = hlslVariables + "\n" + effectMainCode;

            // Replace placeholders
            string result = template
                .Replace(EFFECT_NAME_PLACEHOLDER, effectName)
                .Replace(DERIVED_PROPERTIES_PLACEHOLDER, propertyDeclarations)
                .Replace(USER_CODE_PLACEHOLDER, fullUserCode);

            return result;
        }

        /// <summary>
        /// Loads the shader template with placeholders
        /// </summary>
        private static string LoadTemplateWithPlaceholders()
        {
            if (!File.Exists(TEMPLATE_WITH_PLACEHOLDERS_PATH))
            {
                Debug.LogError($"Template not found at {TEMPLATE_WITH_PLACEHOLDERS_PATH}");
                return string.Empty;
            }

            try
            {
                return File.ReadAllText(TEMPLATE_WITH_PLACEHOLDERS_PATH);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load template: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Parses user code to extract properties and all user code (helper functions + EffectMain)
        /// </summary>
        private static (List<ShaderProperty> properties, string effectMainCode) ParseUserCode(string userCode)
        {
            var properties = new List<ShaderProperty>();
            var lines = userCode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            var propertyLines = new List<string>();
            var userCodeLines = new List<string>();  // ALL non-property code (helpers + EffectMain)

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
                    continue;

                // Try to parse as a property first
                var property = ParseProperty(line);
                if (property != null)
                {
                    // This is a valid property line
                    propertyLines.Add(line);
                    properties.Add(property);
                }
                else
                {
                    // Not a property - must be user code (helper functions or EffectMain)
                    userCodeLines.Add(line);
                }
            }

            // Reconstruct all user code (helper functions + EffectMain)
            string effectMainCode = string.Join("\n", userCodeLines);

            return (properties, effectMainCode);
        }

        /// <summary>
        /// Parses a single property line
        /// </summary>
        private static ShaderProperty ParseProperty(string line)
        {
            // Pattern to match property declarations
            // Format: _Name ("Display Name", Type) = DefaultValue
            // Examples:
            // _Intensity ("Intensity", Range(0,5)) = 1
            // _Color ("Color", Color) = (1,0.6,0.2,1)
            // _Speed ("Speed", Float) = 2.5
            
            // IMPORTANT: Must start with _ at the beginning (after optional whitespace)
            // This prevents matching function calls like SAMPLE_TEXTURE2D(_DepthTex, ...)
            var match = Regex.Match(line, @"^\s*(_\w+)\s*\(.*?,\s*(\w+)(?:\(.*?\))?\)\s*=");
            if (!match.Success)
                return null;

            string propertyName = match.Groups[1].Value;
            string propertyType = match.Groups[2].Value;
            string hlslType = MapToHlslType(propertyType);

            return new ShaderProperty
            {
                Name = propertyName,
                HlslType = hlslType,
                FullDeclaration = line.Trim()
            };
        }

        /// <summary>
        /// Maps Unity shader property types to HLSL types
        /// </summary>
        private static string MapToHlslType(string unityType)
        {
            switch (unityType.ToLower())
            {
                case "float":
                case "range":
                    return "float";
                case "int":
                    return "int";
                case "color":
                case "vector":
                    return "float4";
                case "2d":
                case "cube":
                case "3d":
                    // Textures are handled by Core.hlsl, but if user defines custom ones
                    // they need TEXTURE2D declarations
                    return "TEXTURE2D";
                default:
                    Debug.LogWarning($"Unknown property type: {unityType}, defaulting to float");
                    return "float";
            }
        }

        /// <summary>
        /// Generates property declarations for the Properties block
        /// </summary>
        private static string GeneratePropertyDeclarations(List<ShaderProperty> properties)
        {
            if (properties.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var prop in properties)
            {
                sb.AppendLine($"        {prop.FullDeclaration}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Generates HLSL variable declarations
        /// </summary>
        private static string GenerateHlslVariables(List<ShaderProperty> properties)
        {
            if (properties.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var prop in properties)
            {
                // Skip texture declarations as they're handled differently
                if (prop.HlslType == "TEXTURE2D")
                {
                    sb.AppendLine($"            TEXTURE2D({prop.Name});");
                    sb.AppendLine($"            SAMPLER(sampler{prop.Name});");
                }
                else
                {
                    sb.AppendLine($"            {prop.HlslType} {prop.Name};");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Validates user shader code
        /// </summary>
        public static bool ValidateUserCode(string userCode, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(userCode))
            {
                errorMessage = "Shader code is empty";
                return false;
            }

            // Check for EffectMain function
            if (!userCode.Contains("float4 EffectMain()"))
            {
                errorMessage = "Shader code must contain 'float4 EffectMain()' function";
                return false;
            }

            // Check for balanced braces
            int braceCount = 0;
            foreach (char c in userCode)
            {
                if (c == '{') braceCount++;
                if (c == '}') braceCount--;
                if (braceCount < 0)
                {
                    errorMessage = "Unbalanced braces in shader code";
                    return false;
                }
            }

            if (braceCount != 0)
            {
                errorMessage = "Unbalanced braces in shader code";
                return false;
            }

            return true;
        }
    }
}
