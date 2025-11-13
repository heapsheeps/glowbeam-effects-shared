# Lightform Effects Package

**Version:** 1.0.0  
**Unity Compatibility:** 2022.3+  
**Requires:** Universal Render Pipeline 17.0.0+

## Overview

Core shader library and utilities for the Lightform effects system. This package provides shared functionality used across Lightform projects, including HLSL shader libraries, shader template processing, and auxiliary image generation.

## Contents

### ShaderLibrary/

**Core.hlsl** - Shared HLSL library providing:
- `EffectMain()` entry point function
- Texture sampling functions (`SAMPLE_SCAN()`, `SAMPLE_DEPTH()`, etc.)
- Global variables (`LocalUV`, `GlobalUV`, `_Time`, etc.)
- Helper functions (color space conversion, math utilities)

### Runtime/

**ShaderTemplateProcessor.cs** - Processes simplified shader code and generates full URP shaders:
- Parses property declarations
- Generates HLSL variable declarations
- Injects code into templates
- Validates shader code

**AuxiliaryImageGenerator.cs** - Generates processed texture variants (available at runtime and in editor):
- RGB_CARTOONIZED - Posterized, edge-detected version
- EDGE_DISTANCE - Distance field from edges
- EDGE_TRACE - Edge tracing information
- NORMALS - Surface normals from depth

Namespace: `Lightform.Effects`

### Editor/

(Currently empty - reserved for editor-only utilities)

### Shaders/

**EffectTemplate.shader.txt** - Full shader template with placeholders for code generation

## Usage

### As a Dependency

Add to your package.json:
```json
{
  "dependencies": {
    "com.lightform.effects": "1.0.0"
  }
}
```

### Using ShaderTemplateProcessor

```csharp
using Lightform.Effects;

// Validate user code
if (ShaderTemplateProcessor.ValidateUserCode(userCode, out string error))
{
    // Generate full shader
    string fullShader = ShaderTemplateProcessor.ProcessShaderCode(
        userCode, 
        "MyEffectName"
    );
}
```

### Using Core.hlsl in Shaders

```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.lightform.effects/ShaderLibrary/Core.hlsl"

float4 EffectMain()
{
    float2 uv = LocalUV;
    float4 scan = SAMPLE_SCAN();
    return scan;
}
```

### Using AuxiliaryImageGenerator

```csharp
using Lightform.Effects;

var generator = new AuxiliaryImageGenerator(isOpenCVAvailable: true);
var auxImages = generator.Generate(scanTexture, depthTexture);

if (auxImages != null)
{
    Texture2D cartoonized = auxImages.RGB_CARTOONIZED;
    Texture2D edges = auxImages.EDGE_DISTANCE;
    // ... use images
    
    auxImages.Cleanup(); // Clean up when done
}
```

## Assembly Definitions

- **Runtime:** `Lightform.Effects` - Available at runtime and in editor
  - Contains: ShaderTemplateProcessor, AuxiliaryImageGenerator
- **Editor:** `Lightform.Effects.Editor` - Editor-only utilities (currently empty)

## Optional Dependencies

- **OpenCVForUnity** - Required for AuxiliaryImageGenerator to generate processed images. Falls back to simple implementations if not available.

## Package Structure

```
com.lightform.effects/
├── package.json
├── README.md
├── Runtime/
│   ├── Lightform.Effects.asmdef
│   ├── ShaderTemplateProcessor.cs
│   └── AuxiliaryImageGenerator.cs
├── Editor/
│   └── Lightform.Effects.Editor.asmdef
├── ShaderLibrary/
│   └── Core.hlsl
└── Shaders/
    └── EffectTemplate.shader.txt
```

## License

MIT

## Repository

https://github.com/heapsheeps/glowbeam-unity-effect-editor

## Support

For issues and feature requests, please use the GitHub issue tracker.
