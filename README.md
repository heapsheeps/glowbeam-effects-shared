# Glowbeam Effects Package

**Version:** 1.0.0  
**Unity Compatibility:** 2022.3+ (Unity 6 recommended)  
**Requires:** Universal Render Pipeline 17.0.0+

## Overview

Core shader library and utilities for the Glowbeam effects system. This package provides shared functionality for creating shader-based 2D effects for projection mapping, including HLSL shader libraries, shader template processing, and auxiliary image generation.

## Key Features

- **Simplified Shader Format** - Write effects with just properties and code (`.glow` files)
- **Automatic Code Generation** - Full URP shaders generated from simplified format
- **Helper Functions Support** - Write modular shader code with helper functions
- **Auxiliary Image Generation** - Automatic generation of processed texture variants
- **Line Mapping** - Compiler errors mapped back to simplified code
- **Multi-Platform Export** - Export to `.lfxpkg` bundles for Windows, macOS, and Android

## Contents

### ShaderLibrary/

**Core.hlsl** - Shared HLSL library providing:
- `EffectMain()` - User entry point function (no parameters, returns `float4`)
- **Texture Sampling Functions:**
  - `SAMPLE_SCAN()` - Original scan texture (RGBA)
  - `SAMPLE_DEPTH()` - Depth texture (R channel)
  - `SAMPLE_RGB_CARTOONIZED()` - Posterized RGB version
  - `SAMPLE_EDGE_DISTANCE()` - Distance from edges
  - `SAMPLE_EDGE_TRACE()` - Edge tracing (RG channels)
  - `SAMPLE_NORMALS()` - Surface normals from depth
- **Global Variables:**
  - `LocalUV` - Local vertex UV (0-1)
  - `GlobalUV` - Global screen UV (0-1)
  - `_Time` - Unity time (y = seconds)
  - `_Resolution` - Texture resolution
- **Helper Functions:**
  - `hsv2rgb()`, `rgb2hsv()` - Color space conversion
  - `map()` - Remap values
  - `grayscale()` - RGB to grayscale
  - Math constants: `PI`, `TWO_PI`, etc.

### Runtime/

**ShaderTemplateProcessor.cs** - Processes simplified shader code:
- Parses property declarations (`_Name("Display", Type) = value`)
- Generates HLSL variable declarations
- Preserves helper functions and `EffectMain()`
- Injects code into URP shader templates
- Maps compiler errors to simplified code line numbers

**AuxiliaryImageGenerator.cs** - Generates processed texture variants:
- **RGB_CARTOONIZED** - Pyramid + bilateral filter + adaptive threshold
- **EDGE_DISTANCE** - Canny + L2 distance transform
- **EDGE_TRACE** - Flood-fill trace + distance width
- **NORMALS** - Gaussian blur + Sobel gradients from depth

Namespace: `Glowbeam.Effects`

### Templates/

**EffectTemplate.shader.txt** - Full URP shader template with placeholders

## Simplified Shader Format (.glow files)

Effects are authored in a simplified format with just properties and code:

```hlsl
// Properties (optional)
_Duration("Duration", Float) = 10.0
_Color("Color", Color) = (1, 0.6, 0.2, 1)
_Intensity("Intensity", Range(0, 5)) = 1.0

// Helper functions (optional)
float pulse(float t, float width)
{
    return smoothstep(0.0, width, t) - smoothstep(1.0 - width, 1.0, t);
}

// Main function (required)
float4 EffectMain()
{
    float2 uv = LocalUV;
    float t = _Time.y / _Duration;
    
    float4 scan = SAMPLE_SCAN();
    float depth = SAMPLE_DEPTH();
    
    float3 color = scan.rgb * _Color.rgb * pulse(t, 0.2);
    
    return float4(color, 1.0);
}
```

### Property Types

- `Float` - Single float value
- `Range(min, max)` - Clamped float slider
- `Color` - RGBA color (float4)
- `Vector` - Float4 vector
- `Int` - Integer value

### What Gets Generated

The `ShaderTemplateProcessor` automatically generates:
1. **Full URP Shader Structure** - Tags, passes, pragmas
2. **Properties Block** - Unity shader properties
3. **HLSL Variables** - `float _Duration;`, `float4 _Color;`, etc.
4. **Your Code** - Helper functions + `EffectMain()`

## Usage

### As a Dependency

Add to your package.json:
```json
{
  "dependencies": {
    "com.glowbeam.effects": "1.0.0"
  }
}
```

### Using ShaderTemplateProcessor

```csharp
using Glowbeam.Effects;

// Read simplified .glow file
string glowCode = File.ReadAllText("MyEffect.glow");

// Validate
if (ShaderTemplateProcessor.ValidateUserCode(glowCode, out string error))
{
    // Generate full shader
    string fullShader = ShaderTemplateProcessor.ProcessShaderCode(
        glowCode, 
        "MyEffect"
    );
    
    // Write to .shader file and compile
    File.WriteAllText("Assets/Effects_Generated/MyEffect.shader", fullShader);
}
else
{
    Debug.LogError($"Validation failed: {error}");
}
```

### Line Number Mapping

When compiler errors occur, map them back to the simplified code:

```csharp
// After compilation, get error messages
var messages = GetShaderMessages(compiledPath);

foreach (var msg in messages)
{
    // Map compiled line to simplified line
    int simplifiedLine = ShaderTemplateProcessor.MapCompiledLineToSimplified(msg.line);
    
    Debug.LogError($"Error on line {simplifiedLine}: {msg.message}");
}
```

### Using AuxiliaryImageGenerator

```csharp
using Glowbeam.Effects;

var generator = new AuxiliaryImageGenerator(isOpenCVAvailable: true);
var auxImages = generator.Generate(scanTexture, depthTexture);

if (auxImages != null)
{
    // Use generated images in shader
    material.SetTexture("_RgbCartoonized", auxImages.RGB_CARTOONIZED);
    material.SetTexture("_EdgeDistance", auxImages.EDGE_DISTANCE);
    material.SetTexture("_EdgeTrace", auxImages.EDGE_TRACE);
    material.SetTexture("_Normals", auxImages.NORMALS);
    
    // Cleanup when done
    auxImages.Cleanup();
}
```

### Using Core.hlsl in Custom Shaders

```hlsl
Shader "Custom/MyEffect"
{
    Properties { /* ... */ }
    
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.glowbeam.effects/ShaderLibrary/Core.hlsl"
            
            float4 EffectMain()
            {
                float2 uv = LocalUV;
                float4 scan = SAMPLE_SCAN();
                float depth = SAMPLE_DEPTH();
                
                return scan * depth;
            }
            
            ENDHLSL
        }
    }
}
```

## Assembly Definitions

- **Runtime:** `Glowbeam.Effects`
  - Available at runtime and in editor
  - Contains: ShaderTemplateProcessor, AuxiliaryImageGenerator
- **Editor:** `Glowbeam.Effects.Editor`
  - Editor-only utilities (currently empty, reserved for future use)

## Workflow

1. **Author** - Write simplified shader in `.glow` format
2. **Process** - `ShaderTemplateProcessor` generates full URP shader
3. **Compile** - Unity compiles the generated shader
4. **Preview** - Test with Effect Editor
5. **Export** - Bundle to `.lfxpkg` for distribution

## Technical Details

### Shader Template Structure

The generated shader includes:
- URP-specific tags and pragmas
- Properties block (auto-generated from simplified format)
- HLSL includes (URP + Glowbeam Core.hlsl)
- User code section (helper functions + EffectMain)
- Blend modes, stencil, and advanced blend support

### Line Mapping Algorithm

Compiler errors are mapped from the compiled shader back to the simplified code:
1. Calculate offset from start of user code section
2. Subtract HLSL variable declarations (one per property)
3. Add offset to start of EffectMain in simplified code

This ensures error messages point to the correct line in your `.glow` file.

## Optional Dependencies

- **OpenCVForUnity** - Required for full auxiliary image generation
  - Falls back to simple implementations if not available
  - Only needed at edit time, not at runtime

## Package Structure

```
com.glowbeam.effects/
├── package.json
├── README.md
├── Runtime/
│   ├── Glowbeam.Effects.asmdef
│   ├── ShaderTemplateProcessor.cs
│   └── AuxiliaryImageGenerator.cs
├── Editor/
│   └── Glowbeam.Effects.Editor.asmdef
├── ShaderLibrary/
│   └── Core.hlsl
└── Templates/
    └── EffectTemplate.shader.txt
```

## Common Patterns

### Animating Over Time

```hlsl
float4 EffectMain()
{
    float t = _Time.y / _Duration;
    float progress = frac(t);  // Loop 0-1
    float pingpong = abs(frac(t * 0.5) * 2.0 - 1.0);  // Ping-pong 0-1-0
    
    return float4(progress, pingpong, 0, 1);
}
```

### Using Auxiliary Images

```hlsl
float4 EffectMain()
{
    float2 uv = LocalUV;
    
    float3 cartoon = SAMPLE_RGB_CARTOONIZED();
    float edgeDist = SAMPLE_EDGE_DISTANCE();
    float2 edgeTrace = SAMPLE_EDGE_TRACE();
    float3 normal = SAMPLE_NORMALS();
    
    // Edge detection
    bool isEdge = edgeDist < 0.1;
    
    return float4(cartoon, 1.0);
}
```

### Helper Functions

```hlsl
// Color palette function
float3 palette(float t, float3 a, float3 b, float3 c, float3 d)
{
    return a + b * cos(TWO_PI * (c * t + d));
}

float4 EffectMain()
{
    float t = _Time.y / _Duration;
    float3 color = palette(t, float3(0.5, 0.5, 0.5), float3(0.5, 0.5, 0.5), 
                           float3(1.0, 1.0, 1.0), float3(0.0, 0.1, 0.2));
    return float4(color, 1.0);
}
```

## License

MIT

## Repository

https://github.com/heapsheeps/glowbeam-unity-effect-editor

## Support

For issues and feature requests, please use the GitHub issue tracker.
