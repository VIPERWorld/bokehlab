﻿namespace BokehLab.InteractiveDof.DepthPeeling
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BokehLab.Math;
    using OpenTK;
    using OpenTK.Graphics.OpenGL;
    using BokehLab.InteractiveDof;

    class DepthPeeler : AbstractRendererModule
    {
        /// <summary>
        /// Number of depth peeling layers (color and depth textures).
        /// </summary>
        /// <remarks>
        /// 8 layers are almost always enough.
        /// </remarks>
        public static readonly int LayerCount = 4;
        public static readonly int PackedLayerCount = LayerCount / 4;

        static readonly string PeelingVertexShaderPath = "DepthPeeling/DepthPeelerVS.glsl";
        static readonly string PeelingFragmentShaderPath = "DepthPeeling/DepthPeelerFS.glsl";

        static readonly string PackingVertexShaderPath = "DepthPeeling/DepthPackerVS.glsl";
        static readonly string PackingFragmentShaderPath = "DepthPeeling/DepthPackerFS.glsl";

        uint colorTextures;
        uint depthTextures;
        uint packedDepthTextures;

        public uint ColorTextures { get { return colorTextures; } }
        public uint DepthTextures { get { return depthTextures; } }
        public uint PackedDepthTextures { get { return packedDepthTextures; } }

        /// <summary>
        /// Frame-buffer Object to which the current color and depth texture
        /// can be attached.
        /// </summary>
        uint fboHandle;

        public uint FboHandle { get; set; }

        int peelingVertexShader;
        int peelingFragmentShader;
        int peelingShaderProgram;

        int packingVertexShader;
        int packingFragmentShader;
        int packingShaderProgram;

        Vector2 depthTextureSizeInv;

        public void PeelLayers(Scene scene)
        {
            // put the results into color and depth textures via FBO
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fboHandle);

            AttachLayerTextures(0);

            GL.Enable(EnableCap.Texture2D);

            // draw the first layer without the depth peeling shader
            // (there is no previous depth layer to compare)
            scene.ShaderManager.DepthPeelingData.Enabled = false;

            scene.Draw();

            // draw the rest of layers with depth peeling
            scene.ShaderManager.DepthPeelingData.Enabled = true;

            // Use an other texture unit than 0 as drawing the scene with
            // fixed-function pipeline might use it by default.
            GL.ActiveTexture(TextureUnit.Texture8);
            // Use the previous depth layer for manual depth comparisons.
            GL.BindTexture(TextureTarget.Texture2DArray, depthTextures);
            scene.ShaderManager.DepthPeelingData.DepthTexture = 8; // TextureUnit.Texture8
            scene.ShaderManager.DepthPeelingData.DepthTextureSizeInv = depthTextureSizeInv;

            for (int i = 1; i < LayerCount; i++)
            {
                AttachLayerTextures(i);
                scene.ShaderManager.DepthPeelingData.PreviousLayerIndex = i - 1;

                scene.Draw();
            }
            GL.ActiveTexture(TextureUnit.Texture8);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0);

            GL.Ext.FramebufferTextureLayer(
                FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt,
                0, 0, 0);
            GL.Ext.FramebufferTextureLayer(
                FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext,
                0, 0, 0);

            //GL.BindTexture(TextureTarget.Texture2DArray, colorTextures);
            //GL.Ext.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);

            PackDepthImages();
        }

        /// <summary>
        /// Packs four single-channel depth images into one four-channel image.
        /// </summary>
        private void PackDepthImages()
        {
            // for visualization
            //GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);

            GL.ClearColor(0f, 0f, 0f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Ext.FramebufferTextureLayer(
                FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext,
                packedDepthTextures, 0, 0);

            var result = GL.Ext.CheckFramebufferStatus(FramebufferTarget.FramebufferExt);
            if (result != FramebufferErrorCode.FramebufferCompleteExt)
            {
                throw new ApplicationException(string.Format("Bad FBO: {0}", result));
            }

            GL.UseProgram(packingShaderProgram);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, depthTextures);
            GL.Uniform1(GL.GetUniformLocation(packingShaderProgram, "depthTexture"), 0);

            LayerHelper.DrawQuad();

            GL.UseProgram(0);

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);

            GL.ActiveTexture(TextureUnit.Texture0);

            //GL.BindTexture(TextureTarget.Texture2DArray, packedDepthTextures);
            //GL.Ext.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        #region IRendererModule Members

        public override void Initialize(int width, int height)
        {
            base.Initialize(width, height);

            ShaderLoader.CreateSimpleShaderProgram(
               PeelingVertexShaderPath, PeelingFragmentShaderPath,
               out peelingVertexShader, out peelingFragmentShader, out peelingShaderProgram);

            ShaderLoader.CreateSimpleShaderProgram(
               PackingVertexShaderPath, PackingFragmentShaderPath,
               out packingVertexShader, out packingFragmentShader, out packingShaderProgram);

            GL.Ext.GenFramebuffers(1, out fboHandle);

            GL.Enable(EnableCap.Texture2D);
        }

        public override void Dispose()
        {
            if (fboHandle != 0)
                GL.Ext.DeleteFramebuffers(1, ref fboHandle);

            if (peelingShaderProgram != 0)
                GL.DeleteProgram(peelingShaderProgram);
            if (peelingVertexShader != 0)
                GL.DeleteShader(peelingVertexShader);
            if (peelingFragmentShader != 0)
                GL.DeleteShader(peelingFragmentShader);

            if (packingShaderProgram != 0)
                GL.DeleteProgram(packingShaderProgram);
            if (packingVertexShader != 0)
                GL.DeleteShader(packingVertexShader);
            if (packingFragmentShader != 0)
                GL.DeleteShader(packingFragmentShader);

            base.Dispose();
        }

        protected override void Enable()
        {
            CreateLayerTextures(Width, Height);
            depthTextureSizeInv = new Vector2(1.0f / Width, 1.0f / Height);
        }

        protected override void Disable()
        {
            if (colorTextures != 0)
                GL.DeleteTexture(colorTextures);
            if (depthTextures != 0)
                GL.DeleteTexture(depthTextures);
            if (packedDepthTextures != 0)
                GL.DeleteTexture(packedDepthTextures);
        }

        #endregion

        private void CreateLayerTextures(int width, int height)
        {
            if (LayerCount < 1)
            {
                throw new ArgumentException("At least one layer is needed.");
            }

            // create textures
            colorTextures = (uint)GL.GenTexture();
            depthTextures = (uint)GL.GenTexture();
            packedDepthTextures = (uint)GL.GenTexture();

            // setup color texture
            GL.BindTexture(TextureTarget.Texture2DArray, colorTextures);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba16f, width, height, LayerCount, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
            //GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba8, width, height, LayerCount, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // setup depth texture
            GL.BindTexture(TextureTarget.Texture2DArray, depthTextures);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.DepthComponent32f, width, height, LayerCount, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            //GL.TexImage3D(TextureTarget.Texture2DArray, 0, (PixelInternalFormat)All.DepthComponent32, width, height, LayerCount, 0, PixelFormat.DepthComponent, PixelType.UnsignedInt, IntPtr.Zero);
            //GL.TexImage3D(TextureTarget.Texture2DArray, 0, (PixelInternalFormat)All.DepthComponent16, width, height, LayerCount, 0, PixelFormat.DepthComponent, PixelType.UnsignedShort, IntPtr.Zero);
            // things go horribly wrong if DepthComponent's Bitcount does not match the main Framebuffer's Depth
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // setup depth texture
            GL.BindTexture(TextureTarget.Texture2DArray, packedDepthTextures);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba16f, width, height, PackedLayerCount, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
            //GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba, width, height, PackedLayerCount, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            //GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.Texture2DArray, 0);
        }

        private void AttachLayerTextures(int layerIndex)
        {
            // Attach color and depth from the selected layer.
            // Assumes that a FBO is bound.
            GL.Ext.FramebufferTextureLayer(
                FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext,
                colorTextures, 0, layerIndex);
            GL.Ext.FramebufferTextureLayer(
                FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt,
                depthTextures, 0, layerIndex);

            var result = GL.Ext.CheckFramebufferStatus(FramebufferTarget.FramebufferExt);
            if (result != FramebufferErrorCode.FramebufferCompleteExt)
            {
                throw new ApplicationException(string.Format("Bad FBO: {0}", result));
            }
        }
    }
}
