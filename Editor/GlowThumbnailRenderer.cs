using UnityEditor;
using UnityEngine;

namespace Glowbeam.Effects.Editor
{
    public class GlowThumbnailRenderer
    {
        private PreviewRenderUtility previewRenderUtility;
        private Mesh fullscreenQuad;

        public GlowThumbnailRenderer()
        {
            previewRenderUtility = new PreviewRenderUtility();
            previewRenderUtility.camera.orthographic = true;
            previewRenderUtility.camera.orthographicSize = 1.0f;
            previewRenderUtility.camera.transform.position = new Vector3(0, 0, -2);
            previewRenderUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            previewRenderUtility.camera.backgroundColor = Color.black;

            fullscreenQuad = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-1, -1, 0),
                    new Vector3(-1,  1, 0),
                    new Vector3( 1,  1, 0),
                    new Vector3( 1, -1, 0)
                },
                uv = new[]
                {
                    new Vector2(0, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 0)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            fullscreenQuad.RecalculateNormals();
        }

        public Texture2D RenderThumbnail(Shader shader, Texture2D scanTex, Texture2D depthTex, int targetWidth)
        {
            if (shader == null)
            {
                return null;
            }

            float aspect = scanTex != null ? (float)scanTex.height / scanTex.width : 1.0f;
            int targetHeight = Mathf.RoundToInt(targetWidth * aspect);

            var material = new Material(shader);

            if (scanTex != null)
            {
                material.SetTexture("_ScanTex", scanTex);
                material.SetVector("_Resolution", new Vector2(scanTex.width, scanTex.height));
            }

            if (depthTex != null)
            {
                material.SetTexture("_DepthTex", depthTex);
            }

            Vector4 originalTime = Shader.GetGlobalVector("_Time");
            Shader.SetGlobalVector("_Time", new Vector4(0.05f, 1.0f, 2.0f, 3.0f));

            Rect rect = new Rect(0, 0, targetWidth, targetHeight);
            previewRenderUtility.BeginPreview(rect, GUIStyle.none);
            previewRenderUtility.DrawMesh(fullscreenQuad, Matrix4x4.identity, material, 0);
            previewRenderUtility.camera.Render();
            var rendered = previewRenderUtility.EndPreview();

            Shader.SetGlobalVector("_Time", originalTime);
            Object.DestroyImmediate(material);

            if (rendered == null)
            {
                return null;
            }

            RenderTexture tempRT = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rendered, tempRT);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tempRT;

            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tempRT);

            return result;
        }

        public void Cleanup()
        {
            if (previewRenderUtility != null)
            {
                previewRenderUtility.Cleanup();
                previewRenderUtility = null;
            }

            if (fullscreenQuad != null)
            {
                Object.DestroyImmediate(fullscreenQuad);
                fullscreenQuad = null;
            }
        }
    }
}
