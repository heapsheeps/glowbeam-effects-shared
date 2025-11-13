#ifndef GLOWBEAM_CORE_INCLUDED
#define GLOWBEAM_CORE_INCLUDED
    
    struct Attributes
    {
        float3 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionHCS : SV_POSITION;
        float2 uv          : TEXCOORD0;
    };

    // Common Parameters
    int _StencilRef;
    float4 _ScanTex_TexelSize;
    float2 _Resolution;
    float _Opacity;
    float _HueOffsetDeg, _SatMul, _LightnessOff, _ColorizeOn;

    // Common texture samplers for scan image and auxiliary images
    float2 GlobalUV; // aka frag coordinate in global screen space
    float2 LocalUV; // aka vertex-based coordinate in local space (mask space)

    TEXTURE2D(_MainTex);         SAMPLER(sampler_MainTex);
    TEXTURE2D(_ScanTex);         SAMPLER(sampler_ScanTex);
    TEXTURE2D(_DepthTex);        SAMPLER(sampler_DepthTex);
    TEXTURE2D(_RgbCartoonized);  SAMPLER(sampler_RgbCartoonized);
    TEXTURE2D(_EdgeDistance);    SAMPLER(sampler_EdgeDistance);
    TEXTURE2D(_EdgeTrace);       SAMPLER(sampler_EdgeTrace);
    TEXTURE2D(_Normals);         SAMPLER(sampler_Normals);
    
    float4 SAMPLE_MAIN_TEXTURE()      { return SAMPLE_TEXTURE2D(_MainTex,        sampler_MainTex,        LocalUV); }
    float4 SAMPLE_SCAN()              { return SAMPLE_TEXTURE2D(_ScanTex,        sampler_ScanTex,        GlobalUV); }
    float  SAMPLE_DEPTH()             { return SAMPLE_TEXTURE2D(_DepthTex,       sampler_DepthTex,       GlobalUV).r; }
    float3 SAMPLE_RGB_CARTOONIZED()   { return SAMPLE_TEXTURE2D(_RgbCartoonized, sampler_RgbCartoonized, GlobalUV).rgb; }
    float  SAMPLE_EDGE_DISTANCE()     { return SAMPLE_TEXTURE2D(_EdgeDistance,   sampler_EdgeDistance,   GlobalUV).r; }
    float2 SAMPLE_EDGE_TRACE()        { return SAMPLE_TEXTURE2D(_EdgeTrace,      sampler_EdgeTrace,      GlobalUV).rg; }
    float3 SAMPLE_NORMALS()           { return SAMPLE_TEXTURE2D(_Normals,        sampler_Normals,        GlobalUV).rgb; }

    // Feature toggles (compile-time)
    #if defined(FEATHER_ENABLED)
        TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
    #endif

    float4 _MainTex_ST;

    // Camera color (URP Opaque Texture must be ON, or provide your own copy)
    #if defined(ADV_BLEND)
        TEXTURE2D_X(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
    #endif

    // Entry point for custom user shader
    float4 EffectMain();

    float4 GLOWBEAM_FinalizeImpl(float4 c, float2 suv);
    #define GLOWBEAM_FINALIZE(COLOR) GLOWBEAM_FinalizeImpl((COLOR), GlobalUV)

    // Vertex and Fragment shaders
    Varyings Vert(Attributes v)
    {
        Varyings o;
        o.positionHCS = TransformObjectToHClip(v.positionOS);
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        return o;
    }

    float4 Frag(Varyings i) : SV_Target
    {
        LocalUV = i.uv;
        GlobalUV = i.positionHCS.xy / _ScaledScreenParams.xy;
        #if !UNITY_UV_STARTS_AT_TOP
            GlobalUV.y = 1.0 - GlobalUV.y;
        #endif

        float4 effectColor = EffectMain();
        return GLOWBEAM_FINALIZE(effectColor);
    }
    
    // Constants -----------------------
    static const float EPSILON = 1.0e-10;

    // Helper functions ----------------

    bool LF_isInEdge(float edgeDist, float edgeWidth)
    {
        // edgeWidth is in 0-1. If we want a slow slope from 0-0.5, we could do pow(x,4). Currently linear though.
        // return edgeDist <= pow(edgeWidth, 4.0);
        return edgeDist <= edgeWidth;
    }

    bool LF_isInEdge_deprecated(float edgeDist, float edgeWidth)
    {
        float edgeWidthRemapped = 5. * (0.1 + 0.9 * edgeWidth);
        return 255.0 * edgeDist < edgeWidthRemapped;
    }
    
    float rand(float2 co)
    {
        return frac(sin(dot(co, float2(12.9898,78.233))) * 43758.5453);
    }

    float3 hsv2rgb(float3 c)
    {
        float3 rgb = saturate(abs(fmod(c.x * 6.0 + float3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0);
        return c.z * lerp(float3(1.0, 1.0, 1.0), rgb, c.y);
    }

    float map(float value, float start1, float stop1, float start2, float stop2)
    {
        return start2 + (stop2 - start2) * ( (value - start1)/(stop1 - start1) );
    }

    float2 map(float2 value, float start1, float stop1, float start2, float stop2)
    {
        return start2 + (stop2 - start2) * ( (value - start1)/(stop1 - start1) );
    }

    float3 map(float3 value, float start1, float stop1, float start2, float stop2)
    {
        return start2 + (stop2 - start2) * ( (value - start1)/(stop1 - start1) );
    }

    float4 map(float4 value, float start1, float stop1, float start2, float stop2)
    {
        return start2 + (stop2 - start2) * ( (value - start1)/(stop1 - start1) );
    }

    float map_clamp(float value, float start1, float stop1, float start2, float stop2)
    {
        return clamp(map(value, start1, stop1, start2, stop2), start2, stop2);
    }

    float2 map_clamp(float2 value, float start1, float stop1, float start2, float stop2)
    {
        return clamp(map(value, start1, stop1, start2, stop2), start2, stop2);
    }

    float3 map_clamp(float3 value, float start1, float stop1, float start2, float stop2)
    {
        return clamp(map(value, start1, stop1, start2, stop2), start2, stop2);
    }

    float4 map_clamp(float4 value, float start1, float stop1, float start2, float stop2)
    {
        return clamp(map(value, start1, stop1, start2, stop2), start2, stop2);
    }

    float max3(float3 value)
    {
        return max(value.r, max(value.g, value.b));
    }

    float min3(float3 value)
    {
        return min(value.r, min(value.g, value.b));
    }

    float4 colorFromIntRgba(uint iColor)
    {
        uint4 ivColor = uint4(
            (iColor & 0xff000000u) >> 24u,
            (iColor & 0x00ff0000u) >> 16u,
            (iColor & 0x0000ff00u) >> 8u,
            (iColor & 0x000000ffu)
        );

        return float4(ivColor) / 255.0;
    }

    float4 colorFromIntAbgr(uint iColor)
    {
        uint4 ivColor = uint4(
            (iColor & 0x000000ffu),
            (iColor & 0x0000ff00u) >> 8u,
            (iColor & 0x00ff0000u) >> 16u,
            (iColor & 0xff000000u) >> 24u
        );

        return float4(ivColor) / 255.0;
    }

    float4 linearize(float4 gColor)
    {
        return float4(pow(gColor.rgb, float3(2.2, 2.2, 2.2)), gColor.a);
    }

    float4 gamma(float4 lColor)
    {
        return float4(pow(lColor.rgb, float3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2)), lColor.a);
    }

    float3 linearize(float3 gColor)
    {
        return pow(gColor, float3(2.2, 2.2, 2.2));
    }

    float3 gamma(float3 lColor)
    {
        return pow(lColor, float3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2));
    }

    float4 premultiply(float4 color)
    {
        return float4(color.rgb * color.a, color.a);
    }

    float4 depremultiply(float4 color)
    {
        return (color.a > EPSILON) ? float4(color.rgb / color.a, color.a) : float4(0.0, 0.0, 0.0, 0.0);
    }

    float grayscaleValue(float3 lColor)
    {
        return 0.2126 * lColor.r + 0.7152 * lColor.g + 0.0722 * lColor.b;
    }

    float4 grayscale(float4 lColor)
    {
        float value = grayscaleValue(lColor.rgb);
        return float4(value, value, value, lColor.a);
    }

    float3 grayscale(float3 lColor)
    {
        float value = grayscaleValue(lColor);
        return float3(value, value, value);
    }

    float3 rgbToHcv(float3 c)
    {
        float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
        float4 p = c.g < c.b ? float4(c.bg, K.wz) : float4(c.gb, K.xy);
        float4 q = c.r < p.x ? float4(p.xyw, c.r) : float4(c.r, p.yzx);

        // value
        float V = q.x;
        // chroma
        float C = V - min(q.w, q.y);
        // hue
        float H = abs(q.z + (q.w - q.y) / max(6.0 * C, 0.01));

        return float3(H, C, V);
    }

    float3 rgbToHsv(float3 c)
    {
        float3 hcv = rgbToHcv(c);

        // hue
        float H = hcv.x;
        // chroma
        float C = hcv.y;
        // value
        float V = hcv.z;
        // saturation
        float S = C / (V + EPSILON);

        return float3(H, S, V);
    }

    float3 rgbToHsl(float3 c)
    {
        float3 hcv = rgbToHcv(c);

        // hue
        float H = hcv.x;
        // chroma
        float C = hcv.y;
        // value
        float V = hcv.z;
        // luminance
        float L = V - C * 0.5;
        // saturation
        float S = C / max(1.0 - abs(L * 2.0 - 1.0), 0.01);

        return float3(H, S, L);
    }

    float3 hueToRgb(float H)
    {
        float R = abs(H * 6.0 - 3.0) - 1.0;
        float G = 2.0 - abs(H * 6.0 - 2.0);
        float B = 2.0 - abs(H * 6.0 - 4.0);
        return saturate(float3(R, G, B));
    }

    float3 hsvToRgb(float3 hsv)
    {
        float3 rgb = hueToRgb(hsv.x);
        return ((rgb - 1.0) * hsv.y + 1.0) * hsv.z;
    }

    float3 hslToRgb(float3 hsl)
    {
        float3 rgb = hueToRgb(hsl.x);
        float C = (1.0 - abs(2.0 * hsl.z - 1.0)) * hsl.y;
        return (rgb - 0.5) * C + hsl.z;
    }

    float adobeLum(float3 C)
    {
        return (0.3 * C.r + 0.59 * C.g + 0.11 * C.b);
    }

    float3 adobeClipColor(float3 C)
    {
        float l = adobeLum(C);
        float n = min3(C);
        float x = max3(C);
        if (n < 0.0)
        {
            C = float3(l, l, l) + ((C - float3(l, l, l)) * l) / (l - n);
        }
        else if (x > 1.0)
        {
            C = float3(l, l, l) + ((C - float3(l, l, l)) * (1.0 - l)) / (x - l);
        }
        return C;
    }

    float3 adobeSetLum(float3 C, float l)
    {
        float d = l - adobeLum(C);
        C += d;
        return adobeClipColor(C);
    }

    float adobeSat(float3 C)
    {
        return max3(C) - min3(C);
    }

    float3 adobeSetSat(float3 C, float s)
    {
        bool3 a = C.rgb > C.gbr;
        bool3 b = C.rgb > C.brg;
        int3 c = int3(a) + int3(b);
        float3 maxC = float3(c == int3(2, 2, 2));
        float3 midC = float3(c == int3(1, 1, 1));
        float3 minC = float3(c == int3(0, 0, 0));

        // actual value of max, mid and min component
        float maxV = dot(maxC, C);
        float midV = dot(midC, C);
        float minV = dot(minC, C);

        if (maxV > minV)
        {
            midV = ((midV - minV) * s) / (maxV - minV);
            maxV = s;
        }
        else
        {
            maxV = midV = 0.0;
        }
    //    minV = 0.0; // implicit done by not including the min factor below

        return maxV * maxC + midV * midC;
    }

    float4 applyHslOffset(float4 color, float3 hslMod)
    {
        float3 hslResult = rgbToHsl( saturate(color.rgb) );
        hslResult.r = frac(hslResult.r + hslMod.r / 2.0);
        hslResult.g = saturate(hslResult.g + hslMod.g);
        float L = hslMod.b;
        float4 colorResult = float4(hslToRgb(hslResult), color.a);
        colorResult.rgb = (L < 0.0) ? ((1.0 + L) * colorResult.rgb) : (1.0 - (1.0 - L) * (1.0 - colorResult.rgb));
        return colorResult;
    }


    // ---------- Mask/feather (guarded by FEATHER_ENABLED) ----------
    float4 Glowbeam_ApplyFeather(float4 c, float2 suv)
    {
        #if defined(FEATHER_ENABLED)
            float m = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, suv).r;
            clip(m - 1e-6);
            c.a *= m;
        #endif
        return c;
    }

    // ---------- HSL helpers ----------
    float3 rgb2hsl(float3 c)
    {
        float maxc = max(max(c.r,c.g), c.b);
        float minc = min(min(c.r,c.g), c.b);
        float L = 0.5 * (maxc + minc);
        float d = maxc - minc;
        float H = 0.0, S = 0.0;
        if (d > 1e-6)
        {
            S = (L > 0.5) ? (d / (2.0 - maxc - minc)) : (d / (maxc + minc));
            if (maxc == c.r)      H = (c.g - c.b) / d + (c.g < c.b ? 6.0 : 0.0);
            else if (maxc == c.g) H = (c.b - c.r) / d + 2.0;
            else                  H = (c.r - c.g) / d + 4.0;
            H /= 6.0;
        }
        return float3(H,S,L);
    }
    float hue2rgb(float p, float q, float t)
    {
        if (t < 0.0) t += 1.0;
        if (t > 1.0) t -= 1.0;
        if (t < 1.0/6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0/2.0) return q;
        if (t < 2.0/3.0) return p + (q - p) * (2.0/3.0 - t) * 6.0;
        return p;
    }
    float3 hsl2rgb(float3 hsl)
    {
        float H = hsl.x;
        float S = saturate(hsl.y);
        float L = saturate(hsl.z);
        if (S <= 1e-6) return float3(L,L,L);
        float q = (L < 0.5) ? (L * (1.0 + S)) : (L + S - L*S);
        float p = 2.0 * L - q;
        return float3(
            hue2rgb(p,q,H + 1.0/3.0),
            hue2rgb(p,q,H),
            hue2rgb(p,q,H - 1.0/3.0));
    }

    float3 saturate3(float3 v)
    {
        return saturate(v);
    }

    float3 safe_div(float3 num, float3 den)
    {
        return num / max(den, float3(1e-5,1e-5,1e-5));
    }

    // ---------- HSL (guarded by HSL_ENABLED) ----------
    float4 Glowbeam_ApplyHSL(float4 c)
    {
        #if defined(HSL_ENABLED)
            float3 hsl = rgb2hsl(saturate(c.rgb));

            // when _ColorizeOn is true:
            //   - replace hue with _HueOffsetDeg (interpreted as absolute hue)
            //   - replace saturation with _SatMul (interpreted as absolute saturation)
            // otherwise:
            //   - add hue offset (degrees)
            //   - multiply saturation
            float useColorize = step(0.5, _ColorizeOn);

            // compute new hue
            float hue = lerp(
                frac(hsl.x + _HueOffsetDeg / 360.0),   // additive hue shift
                frac(_HueOffsetDeg / 360.0),           // absolute hue (colorize)
                useColorize
            );

            // compute new saturation
            float sat = lerp(
                saturate(hsl.y * _SatMul),             // scale saturation
                saturate(_SatMul),                     // absolute saturation
                useColorize
            );

            // apply lightness adjustment as before
            float light = saturate(hsl.z + _LightnessOff);

            // recompose
            c.rgb = hsl2rgb(float3(hue, sat, light));
        #endif

        return c;
    }

    // ---------- ADVANCED BLEND (Overlay example) ----------
    // per-mode ops (src = top, dst = scene)
    float3 OverlayOp(float3 src, float3 dst)
    {
        float3 lo = 2.0 * dst * src;
        float3 hi = 1.0 - 2.0 * (1.0 - dst) * (1.0 - src);
        return lerp(lo, hi, step(0.5, dst));
    }

    float3 HardLightOp(float3 src, float3 dst)
    {
        // Overlay with roles swapped (branch by src)
        float3 lo = 2.0 * dst * src;
        float3 hi = 1.0 - 2.0 * (1.0 - dst) * (1.0 - src);
        return lerp(lo, hi, step(0.5, src));
    }

    float3 SoftLightOp(float3 src, float3 dst)
    {
        // SVG/Photoshop-friendly approximation (pegtop + sqrt variant)
        float3 result = 0;
        float3 m = step(0.5, src);
        // if src <= 0.5: dst - (1-2*src)*dst*(1-dst)
        float3 a = dst - (1.0 - 2.0*src) * dst * (1.0 - dst);
        // else: dst + (2*src - 1)*(g(dst) - dst), with g(x)=sqrt(x)
        float3 b = dst + (2.0*src - 1.0) * (sqrt(saturate(dst)) - dst);
        result = lerp(a, b, m);
        return result;
    }

    float3 ColorDodgeOp(float3 src, float3 dst)
    {
        // dst / (1 - src)
        return saturate3( safe_div(dst, 1.0 - src) );
    }

    float3 ColorBurnOp(float3 src, float3 dst)
    {
        // 1 - (1 - dst)/src
        return saturate3( 1.0 - safe_div(1.0 - dst, src) );
    }

    float3 LinearLightOp(float3 src, float3 dst)
    {
        // dst + 2*src - 1
        return saturate3(dst + 2.0*src - 1.0);
    }

    float3 VividLightOp(float3 src, float3 dst)
    {
        // if src < 0.5 -> ColorBurn with 2*src; else -> ColorDodge with 2*(src-0.5)
        float3 lo = ColorBurnOp(2.0*src, dst);
        float3 hi = ColorDodgeOp(2.0*(src - 0.5), dst);
        return lerp(lo, hi, step(0.5, src));
    }

    float3 PinLightOp(float3 src, float3 dst)
    {
        // if src < 0.5 -> min(dst, 2*src); else -> max(dst, 2*src-1)
        float3 lo = min(dst, 2.0*src);
        float3 hi = max(dst, 2.0*src - 1.0);
        return lerp(lo, hi, step(0.5, src));
    }

    float3 HardMixOp(float3 src, float3 dst)
    {
        // thresholded vivid light
        float3 v = VividLightOp(src, dst);
        return step(0.5, v); // 0 or 1 per channel
    }

    float3 DifferenceOp(float3 src, float3 dst)
    {
        return abs(dst - src);
    }

    float3 ExclusionOp(float3 src, float3 dst)
    {
        // a + b - 2ab
        return dst + src - 2.0 * dst * src;
    }

    float4 Glowbeam_ApplyAdvancedBlend(float4 c, float2 suv)
    {
        #if defined(ADV_BLEND)
            float3 dst = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, suv).rgb;
            float3 outRgb = c.rgb;
            
            switch (_AdvBlendMode)
            {
                case 1:  outRgb = OverlayOp    (c.rgb, dst); break;
                case 2:  outRgb = SoftLightOp  (c.rgb, dst); break;
                case 3:  outRgb = HardLightOp  (c.rgb, dst); break;
                case 4:  outRgb = ColorDodgeOp (c.rgb, dst); break;
                case 5:  outRgb = ColorBurnOp  (c.rgb, dst); break;
                case 6:  outRgb = LinearLightOp(c.rgb, dst); break;
                case 7:  outRgb = VividLightOp (c.rgb, dst); break;
                case 8:  outRgb = PinLightOp   (c.rgb, dst); break;
                case 9:  outRgb = HardMixOp    (c.rgb, dst); break;
                case 10: outRgb = DifferenceOp (c.rgb, dst); break;
                case 11: outRgb = ExclusionOp  (c.rgb, dst); break;
                default: break; // safety
            }

            // respect material alpha; do the "over" in shader, then overwrite to target
            float a = saturate(c.a);
            float3 composited = lerp(dst, outRgb, a);
            return float4(composited, 1.0);
        #else
            return c;
        #endif
    }


    // ---------- Finalize ----------
    float4 GLOWBEAM_FinalizeImpl(float4 c, float2 suv)
    {
        c = Glowbeam_ApplyFeather(c, suv);
        c = Glowbeam_ApplyHSL(c);
        c = Glowbeam_ApplyAdvancedBlend(c, suv);
        c.a *= _Opacity;
        return c;
    }

#endif
