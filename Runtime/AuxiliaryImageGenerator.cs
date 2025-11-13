using UnityEngine;
using System;
using System.Collections.Generic;

// Conditionally import OpenCV types only when available
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityIntegration;

namespace Lightform.Effects
{
    /// <summary>
    /// Generates auxiliary images from scan textures for use in effects
    /// Requires OpenCVForUnity package (optional dependency)
    /// Can be used at runtime or in editor
    /// </summary>
    public class AuxiliaryImageGenerator
    {
        private readonly bool _isOpenCVAvailable;
        
        /// <summary>
        /// Creates a new AuxiliaryImageGenerator
        /// </summary>
        /// <param name="isOpenCVAvailable">Whether OpenCVForUnity is installed and available</param>
        public AuxiliaryImageGenerator(bool isOpenCVAvailable)
        {
            _isOpenCVAvailable = isOpenCVAvailable;
        }
        
        /// <summary>
        /// Container for all auxiliary images
        /// </summary>
        public class AuxiliaryImages
        {
            public Texture2D RGB_CARTOONIZED;
            public Texture2D EDGE_DISTANCE;
            public Texture2D EDGE_TRACE;
            public Texture2D NORMALS;

            public void Cleanup()
            {
                if (RGB_CARTOONIZED != null) UnityEngine.Object.DestroyImmediate(RGB_CARTOONIZED);
                if (EDGE_DISTANCE != null) UnityEngine.Object.DestroyImmediate(EDGE_DISTANCE);
                if (EDGE_TRACE != null) UnityEngine.Object.DestroyImmediate(EDGE_TRACE);
                if (NORMALS != null) UnityEngine.Object.DestroyImmediate(NORMALS);
            }
        }

        /// <summary>
        /// Generates all auxiliary images from a scan texture
        /// </summary>
        public AuxiliaryImages Generate(Texture2D scanTexture, Texture2D depthTexture)
        {
            if (scanTexture == null)
            {
                UnityEngine.Debug.LogWarning("[Lightform Effect Editor] scan texture null, skipping auxiliary image generation.");
                return null;
            }

            if (depthTexture == null)
            {
                UnityEngine.Debug.LogWarning("[Lightform Effect Editor] depth texture null, skipping auxiliary image generation.");
                return null;
            }
            
            // Check if OpenCVForUnity is available
            if (!_isOpenCVAvailable)
            {
                UnityEngine.Debug.LogWarning("[Lightform Effects] OpenCV For Unity is not installed. Auxiliary images will not be generated. Falling back to simple implementations.");
                return null;
            }

            UnityEngine.Debug.Log("[OpenCV] Starting auxiliary image generation...");

            var aux = new AuxiliaryImages();
            
            int width = scanTexture.width;
            int height = scanTexture.height;

            // Make the texture readable by copying it via RenderTexture
            Texture2D readableScanTexture = MakeTextureReadable(scanTexture);
            Texture2D readableDepthTexture = MakeTextureReadable(depthTexture);
            
            if (readableScanTexture == null || readableDepthTexture == null)
            {
                UnityEngine.Debug.LogError("Failed to make texture readable for auxiliary image generation");
                return null;
            }

            // Generate each auxiliary image
            aux.RGB_CARTOONIZED = GenerateCartoonized(readableScanTexture, width, height);
            aux.EDGE_DISTANCE = GenerateEdgeDistance(readableScanTexture, width, height);
            aux.EDGE_TRACE = GenerateEdgeTrace(readableScanTexture, width, height);
            aux.NORMALS = GenerateNormals(readableDepthTexture, width, height);

            // Cleanup temporary readable copy
            UnityEngine.Object.DestroyImmediate(readableScanTexture);
            UnityEngine.Object.DestroyImmediate(readableDepthTexture);

            UnityEngine.Debug.Log("[OpenCV] Auxiliary image generation complete!");
            return aux;
        }
        
        /// <summary>
        /// Creates a readable copy of a texture using RenderTexture
        /// </summary>
        private Texture2D MakeTextureReadable(Texture2D source)
        {
            if (source == null)
                return null;

            RenderTexture tmp = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );

            Graphics.Blit(source, tmp);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            
            Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new UnityEngine.Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readable.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            return readable;
        }

        /// <summary>
        /// Generates a cartoonized version of the scan texture (RGB, 8-bit)
        /// Based on Lightform C++ implementation
        /// </summary>
        private Texture2D GenerateCartoonized(Texture2D scan, int width, int height)
        {
            if (!_isOpenCVAvailable)
            {
                return GenerateCartoonizedFallback(scan, width, height);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.name = "RGB_CARTOONIZED";

            try
            {
                const int numDownSamples = 2;
                const int numBilateralFilters = 7;
                
                // Convert Unity Texture2D to OpenCV Mat
                Mat img = new Mat(height, width, CvType.CV_8UC3);
                OpenCVMatUtils.Texture2DToMat(scan, img);
                Imgproc.cvtColor(img, img, Imgproc.COLOR_RGBA2RGB);
                
                Mat nodalCol = img.clone();
                Size originalSize = img.size();
                
                // Step 1: Downsample
                for (int i = 0; i < numDownSamples; i++)
                {
                    Mat downsampled = new Mat();
                    Imgproc.pyrDown(img, downsampled);
                    img.Dispose();
                    img = downsampled;
                }
                
                // Step 2: Apply bilateral filters
                for (int i = 0; i < numBilateralFilters; i++)
                {
                    Mat filtered = new Mat();
                    Imgproc.bilateralFilter(img, filtered, 9, 9, 7);
                    img.Dispose();
                    img = filtered;
                }
                
                // Step 3: Upsample
                for (int i = 0; i < numDownSamples; i++)
                {
                    Mat upsampled = new Mat();
                    Imgproc.pyrUp(img, upsampled);
                    img.Dispose();
                    img = upsampled;
                }
                
                // Step 4: Correct size if needed
                if (img.width() != originalSize.width || img.height() != originalSize.height)
                {
                    Mat resized = new Mat();
                    Imgproc.resize(img, resized, originalSize, 0, 0, Imgproc.INTER_LINEAR);
                    img.Dispose();
                    img = resized;
                }
                
                // Step 5: Convert smoothed image to grayscale
                Mat imgGray = new Mat();
                Imgproc.cvtColor(img, imgGray, Imgproc.COLOR_RGB2GRAY);
                
                // Step 6: Process original for edge detection
                Mat nodalGray = new Mat();
                Imgproc.cvtColor(nodalCol, nodalGray, Imgproc.COLOR_RGB2GRAY);
                
                Mat nodalGrayBlur = new Mat();
                Imgproc.medianBlur(nodalGray, nodalGrayBlur, 7);
                
                // Step 7: Detect edges using adaptive threshold
                Mat nodalEdgesGray = new Mat();
                Imgproc.adaptiveThreshold(
                    nodalGrayBlur,
                    nodalEdgesGray,
                    255,
                    Imgproc.ADAPTIVE_THRESH_MEAN_C,
                    Imgproc.THRESH_BINARY,
                    9,
                    2
                );
                
                // Step 8: Combine edges with smoothed color image
                Mat nodalEdgesColor = new Mat();
                Imgproc.cvtColor(nodalEdgesGray, nodalEdgesColor, Imgproc.COLOR_GRAY2RGB);
                
                Mat renderedImgColor = new Mat();
                Core.bitwise_and(nodalEdgesColor, img, renderedImgColor);
                
                // Convert back to Unity Texture2D
                result = MatToTexture2D(renderedImgColor, TextureFormat.RGB24);
                result.name = "RGB_CARTOONIZED";
                
                // Cleanup
                img.Dispose();
                nodalCol.Dispose();
                imgGray.Dispose();
                nodalGray.Dispose();
                nodalGrayBlur.Dispose();
                nodalEdgesGray.Dispose();
                nodalEdgesColor.Dispose();
                renderedImgColor.Dispose();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[OpenCV] Cartoonize failed: {e.Message}\n{e.StackTrace}");
                result = GenerateCartoonizedFallback(scan, width, height);
            }

            return result;
        }
        
        private Texture2D GenerateCartoonizedFallback(Texture2D scan, int width, int height)
        {
            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] pixels = scan.GetPixels();
            Color[] output = new Color[pixels.Length];
            
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float r = Mathf.Floor(c.r * 4) / 4f;
                float g = Mathf.Floor(c.g * 4) / 4f;
                float b = Mathf.Floor(c.b * 4) / 4f;
                output[i] = new Color(r, g, b, 1f);
            }
            
            result.SetPixels(output);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Generates edge distance map (R only, 8-bit)
        /// </summary>
        private Texture2D GenerateEdgeDistance(Texture2D scan, int width, int height)
        {
            if (!_isOpenCVAvailable)
            {
                return GenerateEdgeDistanceFallback(scan, width, height);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.R8, false);
            result.name = "EDGE_DISTANCE";

            try
            {
                float edgeThreshold = 0.5f;
                bool removeEdgesAtZeroPixels = true;
                
                // Step 1: Convert to grayscale
                Mat gray = new Mat();
                Mat rgba = new Mat(height, width, CvType.CV_8UC4);
                OpenCVMatUtils.Texture2DToMat(scan, rgba);
                Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);
                
                // Step 2: Apply blur
                Mat grayBlur = new Mat();
                Imgproc.blur(gray, grayBlur, new Size(5, 5));
                
                // Step 3: Canny edge detection
                double threshold = (1.0 - edgeThreshold) * 1000.0;
                Mat cannyEdges = new Mat();
                Imgproc.Canny(grayBlur, cannyEdges, threshold, threshold * 2.0, 5);
                
                // Step 4: Remove edges at zero pixels
                if (removeEdgesAtZeroPixels)
                {
                    Mat mask = new Mat();
                    Imgproc.threshold(gray, mask, 0, 255, Imgproc.THRESH_BINARY);
                    
                    Mat maskEroded = new Mat();
                    Mat kernel = Imgproc.getStructuringElement(Imgproc.MORPH_RECT, new Size(5, 5));
                    Imgproc.erode(mask, maskEroded, kernel, new Point(-1, -1), 5);
                    
                    Core.bitwise_and(cannyEdges, maskEroded, cannyEdges);
                    
                    mask.Dispose();
                    maskEroded.Dispose();
                    kernel.Dispose();
                }
                
                // Step 5: Invert and distance transform
                Mat invertedEdges = new Mat();
                Core.bitwise_not(cannyEdges, invertedEdges);
                
                Mat distanceFromEdges = new Mat();
                Imgproc.distanceTransform(invertedEdges, distanceFromEdges, Imgproc.DIST_L2, 0);
                
                // Step 6: Normalize
                Mat normalized = new Mat();
                Core.normalize(distanceFromEdges, normalized, 0, 255, Core.NORM_MINMAX, CvType.CV_8U);
                
                // Convert to Unity texture
                result = MatToTexture2D(normalized, TextureFormat.R8);
                result.name = "EDGE_DISTANCE";
                
                // Cleanup
                rgba.Dispose();
                gray.Dispose();
                grayBlur.Dispose();
                cannyEdges.Dispose();
                invertedEdges.Dispose();
                distanceFromEdges.Dispose();
                normalized.Dispose();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[OpenCV] Edge distance failed: {e.Message}\n{e.StackTrace}");
                result = GenerateEdgeDistanceFallback(scan, width, height);
            }

            return result;
        }
        
        private Texture2D GenerateEdgeDistanceFallback(Texture2D scan, int width, int height)
        {
            Texture2D result = new Texture2D(width, height, TextureFormat.R8, false);
            Color[] pixels = scan.GetPixels();
            Color[] output = new Color[pixels.Length];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float center = GetLuminance(pixels[idx]);
                    float right = x < width - 1 ? GetLuminance(pixels[idx + 1]) : center;
                    float down = y < height - 1 ? GetLuminance(pixels[idx + width]) : center;
                    float edgeStrength = Mathf.Clamp01((Mathf.Abs(right - center) + Mathf.Abs(down - center)) * 5f);
                    output[idx] = new Color(edgeStrength, 0, 0, 1f);
                }
            }
            
            result.SetPixels(output);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Generates edge trace map (RG only, 8-bit)
        /// </summary>
        private Texture2D GenerateEdgeTrace(Texture2D scan, int width, int height)
        {
            if (!_isOpenCVAvailable)
            {
                return GenerateEdgeTraceFallback(scan, width, height);
            }
            
            Texture2D result = new Texture2D(width, height, TextureFormat.RG16, false);
            result.name = "EDGE_TRACE";

            try
            {
                const float maxEdgeWidth = 20f;
                
                // Step 1: Get edges
                Mat rgba = new Mat(height, width, CvType.CV_8UC4);
                OpenCVMatUtils.Texture2DToMat(scan, rgba);
                
                Mat gray = new Mat();
                Imgproc.cvtColor(rgba, gray, Imgproc.COLOR_RGBA2GRAY);
                
                Mat grayBlur = new Mat();
                Imgproc.blur(gray, grayBlur, new Size(5, 5));
                
                Mat cannyEdges = new Mat();
                Imgproc.Canny(grayBlur, cannyEdges, 500, 1000, 5);
                
                // Step 2: Threshold
                Mat thresh = new Mat();
                Imgproc.threshold(cannyEdges, thresh, 128, 255, Imgproc.THRESH_BINARY);
                
                // Step 3: Flood-fill trace (simplified - full implementation would be complex)
                Mat traced = Mat.zeros(height, width, CvType.CV_8UC1);
                Mat visited = Mat.zeros(height, width, CvType.CV_8UC1);
                
                var queue = new LinkedList<(int x, int y, int val)>();
                byte[] threshData = new byte[width * height];
                thresh.get(0, 0, threshData);
                byte[] tracedData = new byte[width * height];
                byte[] visitedData = new byte[width * height];
                
                void TryInsertPt(int x, int y, int val)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height) return;
                    int idx = y * width + x;
                    if (visitedData[idx] > 0) return;
                    
                    visitedData[idx] = 255;
                    
                    if (threshData[idx] == 0)
                        queue.AddLast((x, y, val));
                    else
                        queue.AddFirst((x, y, val));
                }
                
                TryInsertPt(width / 2, height / 2, 1);
                
                int insertRadius = 2;
                while (queue.Count > 0)
                {
                    var pt = queue.First.Value;
                    queue.RemoveFirst();
                    
                    int idx = pt.y * width + pt.x;
                    if (threshData[idx] > 0)
                        tracedData[idx] = (byte)pt.val;
                    
                    int nextVal = pt.val + 1;
                    if (nextVal == 256) nextVal = 1;
                    
                    for (int yy = -insertRadius; yy <= insertRadius; yy++)
                    {
                        for (int xx = -insertRadius; xx <= insertRadius; xx++)
                        {
                            TryInsertPt(pt.x + xx, pt.y + yy, nextVal);
                        }
                    }
                }
                
                traced.put(0, 0, tracedData);
                
                // Step 4: Distance transform for width channel
                Mat edgesInverted = new Mat();
                Core.bitwise_not(thresh, edgesInverted);
                
                Mat distanceFromEdges = new Mat();
                Imgproc.distanceTransform(edgesInverted, distanceFromEdges, Imgproc.DIST_L2, 0);
                
                Mat distanceWidth = new Mat();
                Imgproc.threshold(distanceFromEdges, distanceFromEdges, maxEdgeWidth * 1.1, 0, Imgproc.THRESH_TRUNC);
                Core.normalize(distanceFromEdges, distanceWidth, 0, 255, Core.NORM_MINMAX, CvType.CV_8U);
                
                // Flip vertically to match Unity's coordinate system
                Core.flip(traced, traced, 0);
                Core.flip(distanceWidth, distanceWidth, 0);
                
                // Combine into RG texture
                byte[] tracedBytes = new byte[width * height];
                byte[] widthBytes = new byte[width * height];
                traced.get(0, 0, tracedBytes);
                distanceWidth.get(0, 0, widthBytes);
                
                Color[] output = new Color[width * height];
                for (int i = 0; i < width * height; i++)
                {
                    output[i] = new Color(tracedBytes[i] / 255f, widthBytes[i] / 255f, 0, 1f);
                }
                
                result.SetPixels(output);
                result.Apply();
                
                // Cleanup
                rgba.Dispose();
                gray.Dispose();
                grayBlur.Dispose();
                cannyEdges.Dispose();
                thresh.Dispose();
                traced.Dispose();
                visited.Dispose();
                edgesInverted.Dispose();
                distanceFromEdges.Dispose();
                distanceWidth.Dispose();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[OpenCV] Edge trace failed: {e.Message}\n{e.StackTrace}");
                result = GenerateEdgeTraceFallback(scan, width, height);
            }

            return result;
        }
        
        private Texture2D GenerateEdgeTraceFallback(Texture2D scan, int width, int height)
        {
            Texture2D result = new Texture2D(width, height, TextureFormat.RG16, false);
            Color[] pixels = scan.GetPixels();
            Color[] output = new Color[pixels.Length];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float center = GetLuminance(pixels[idx]);
                    float right = x < width - 1 ? GetLuminance(pixels[idx + 1]) : center;
                    float down = y < height - 1 ? GetLuminance(pixels[idx + width]) : center;
                    float dx = right - center;
                    float dy = down - center;
                    output[idx] = new Color(dx * 0.5f + 0.5f, dy * 0.5f + 0.5f, 0, 1f);
                }
            }
            
            result.SetPixels(output);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Generates normal map from depth texture (RGB, 8-bit)
        /// </summary>
        private Texture2D GenerateNormals(Texture2D depthOrScan, int width, int height)
        {
            if (!_isOpenCVAvailable)
            {
                return GenerateNormalsFallback(depthOrScan, width, height);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.name = "NORMALS";

            try
            {
                const float depthScale = 0.1f;

                // Convert to grayscale
                Mat rgba = new Mat(height, width, CvType.CV_8UC4);
                OpenCVMatUtils.Texture2DToMat(depthOrScan, rgba);

                Mat depth = new Mat();
                Imgproc.cvtColor(rgba, depth, Imgproc.COLOR_RGBA2GRAY);

                // Apply Gaussian blur (larger kernel for smoother normals)
                Mat depthSmooth = new Mat();
                Imgproc.GaussianBlur(depth, depthSmooth, new Size(11, 11), 0);

                // Compute gradients with Sobel
                Mat gradX = new Mat();
                Mat gradY = new Mat();
                Imgproc.Sobel(depthSmooth, gradX, CvType.CV_32F, 1, 0, 3, 1, 0, Core.BORDER_REFLECT);
                Imgproc.Sobel(depthSmooth, gradY, CvType.CV_32F, 0, 1, 3, 1, 0, Core.BORDER_REFLECT);

                // Get gradient data
                float[] gradXData = new float[width * height];
                float[] gradYData = new float[width * height];
                gradX.get(0, 0, gradXData);
                gradY.get(0, 0, gradYData);

                // Compute normals (flip Y axis to match Unity coordinate system)
                Color[] output = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIdx = y * width + x;
                        int dstIdx = (height - 1 - y) * width + x; // Flip vertically

                        float dx = gradXData[srcIdx] * depthScale;
                        float dy = gradYData[srcIdx] * depthScale;

                        Vector3 normal = new Vector3(-dx, -dy, 1f);
                        normal.Normalize();

                        output[dstIdx] = new Color(
                            normal.x * 0.5f + 0.5f,
                            normal.y * 0.5f + 0.5f,
                            normal.z * 0.5f + 0.5f,
                            1f
                        );
                    }
                }

                result.SetPixels(output);
                result.Apply();

                // Cleanup
                rgba.Dispose();
                depth.Dispose();
                depthSmooth.Dispose();
                gradX.Dispose();
                gradY.Dispose();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[OpenCV] Normals generation failed: {e.Message}\n{e.StackTrace}");

                // Fallback
                Color[] pixels = depthOrScan.GetPixels();
                Color[] output = new Color[pixels.Length];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = y * width + x;
                        float center = GetLuminance(pixels[idx]);
                        float right = x < width - 1 ? GetLuminance(pixels[idx + 1]) : center;
                        float down = y < height - 1 ? GetLuminance(pixels[idx + width]) : center;

                        float dx = (right - center) * 0.5f;
                        float dy = (down - center) * 0.5f;

                        Vector3 normal = new Vector3(-dx, -dy, 1f).normalized;

                        output[idx] = new Color(
                            normal.x * 0.5f + 0.5f,
                            normal.y * 0.5f + 0.5f,
                            normal.z * 0.5f + 0.5f,
                            1f
                        );
                    }
                }

                result.SetPixels(output);
                result.Apply();
            }

            return result;
        }
    
        private Texture2D GenerateNormalsFallback(Texture2D depthOrScan, int width, int height)
        {
            
            // Fallback implementation
            Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.name = "NORMALS";

            Color[] pixels = depthOrScan.GetPixels();
            Color[] output = new Color[pixels.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float center = GetLuminance(pixels[idx]);
                    float right = x < width - 1 ? GetLuminance(pixels[idx + 1]) : center;
                    float down = y < height - 1 ? GetLuminance(pixels[idx + width]) : center;

                    float dx = (right - center) * 0.5f;
                    float dy = (down - center) * 0.5f;

                    Vector3 normal = new Vector3(-dx, -dy, 1f).normalized;

                    output[idx] = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1f
                    );
                }
            }

            result.SetPixels(output);
            result.Apply();
            return result;
        }

        private float GetLuminance(Color c)
        {
            return c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
        }
        
        /// <summary>
        /// Converts OpenCV Mat to Unity Texture2D
        /// </summary>
        private Texture2D MatToTexture2D(Mat mat, TextureFormat format)
        {
            int width = mat.width();
            int height = mat.height();
            
            Texture2D texture = new Texture2D(width, height, format, false);
            OpenCVMatUtils.MatToTexture2D(mat, texture);
            
            return texture;
        }
    }
}
