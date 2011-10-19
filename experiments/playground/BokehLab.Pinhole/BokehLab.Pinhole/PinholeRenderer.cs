﻿#region --- License ---
/* Licensed under the MIT/X11 license.
 * Copyright (c) 2006-2008 the OpenTK Team.
 * This notice may not be removed from any source distribution.
 * See license.txt for licensing details.
 */
#endregion

namespace BokehLab.Pinhole
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using OpenTK;
    using OpenTK.Input;
    using OpenTK.Graphics;
    using OpenTK.Graphics.OpenGL;
    using System.Runtime.InteropServices;
    using BokehLab.Math;
    using System.Collections.Generic;
    using System.Linq;

    public class PinholeRenderer : GameWindow
    {
        public PinholeRenderer()
            : base(800, 600)
        {
        }

        Matrix4 scenePerspective;
        Matrix4 sceneModelView;

        float fieldOfView = OpenTK.MathHelper.PiOver4;
        float near = 0.1f;
        float far = 1000f;

        float apertureRadius = 0.05f;
        Vector2 pinholePos = Vector2.Zero; // position of the pinhole on the lens aperture
        float zFocal = 5; // focal plane depth

        Camera camera = new Camera();

        Scene scene;

        Vector2[] unitDiskSamples;

        bool mouseButtonLeftPressed = false;

        bool enableDofAccumulation = false;

        public static void RunExample()
        {

            using (PinholeRenderer example = new PinholeRenderer())
            {
                example.Run(30.0, 0.0);
            }
        }

        #region GameWindow event handlers

        protected override void OnLoad(EventArgs e)
        {
            scene = GenerateScene();

            Keyboard.KeyUp += KeyUp;
            Keyboard.KeyRepeat = true;
            Mouse.Move += MouseMove;
            Mouse.ButtonDown += MouseButtonHandler;
            Mouse.ButtonUp += MouseButtonHandler;
            Mouse.WheelChanged += MouseWheelChanged;

            camera.Position = new Vector3(0, 0, 3);

            unitDiskSamples = CreateLensSamples(6).ToArray();

            //GL.Enable(EnableCap.Lighting);
            //GL.Enable(EnableCap.Light0);
            //GL.Light(LightName.Light0, LightParameter.Ambient, new Color4(0.5f, 0.5f, 0.5f, 1));
            //GL.Light(LightName.Light0, LightParameter.Diffuse, new Color4(0.8f, 0.8f, 0.8f, 1));
            //GL.Light(LightName.Light0, LightParameter.Position, new Vector4(1, 5, 1, 1));
        }

        /// <summary>
        /// Generate a set of jittered uniform samples of a unit circle.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Vector2> CreateLensSamples(int sampleCount)
        {
            Sampler sampler = new Sampler();
            var jitteredSamples = sampler.GenerateJitteredSamples(sampleCount);
            return jitteredSamples.Select((sample) => (Vector2)Sampler.ConcentricSampleDisk(sample));
        }

        protected override void OnUnload(EventArgs e)
        {
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);

            UpdatePerspective();

            DrawScene();

            base.OnResize(e);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            float deltaShift = 0.1f;

            if (Keyboard[Key.Escape])
            {
                this.Exit();
                return;
            }

            if (Keyboard[Key.W])
            {
                camera.Position += deltaShift * camera.View;
            }
            else if (Keyboard[Key.S])
            {
                camera.Position -= deltaShift * camera.View;
            }

            if (Keyboard[Key.D])
            {
                camera.Position -= deltaShift * camera.Right;
            }
            else if (Keyboard[Key.A])
            {
                camera.Position += deltaShift * camera.Right;
            }

            if (Keyboard[Key.E])
            {
                camera.Position -= deltaShift * camera.Up;
            }
            else if (Keyboard[Key.Q])
            {
                camera.Position += deltaShift * camera.Up;
            }

            bool perspectiveChanged = false;
            if (Keyboard[Key.PageUp])
            {
                fieldOfView /= 1.1f;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.PageDown])
            {
                fieldOfView *= 1.1f;
                perspectiveChanged = true;
            }
            if (Keyboard[Key.T])
            {
                pinholePos += new Vector2(0, 0.1f);
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.G])
            {
                pinholePos += new Vector2(0, -0.1f);
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.H])
            {
                pinholePos += new Vector2(0.1f, 0);
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.F])
            {
                pinholePos += new Vector2(-0.1f, 0);
                perspectiveChanged = true;
            }
            if (Keyboard[Key.R])
            {
                // reset camera configuration
                pinholePos = Vector2.Zero;
                zFocal = 5;
                apertureRadius = 0.01f;
                fieldOfView = OpenTK.MathHelper.PiOver4;
                camera = new Camera();
                camera.Position = new Vector3(0, 0, 3);
                perspectiveChanged = true;
            }
            if (Keyboard[Key.Home])
            {
                apertureRadius *= 1.1f;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.End])
            {
                apertureRadius /= 1.1f;
                perspectiveChanged = true;
            }
            if (Keyboard[Key.Up])
            {
                zFocal *= 1.1f;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.Down])
            {
                zFocal /= 1.1f;
                perspectiveChanged = true;
            }
            if (perspectiveChanged)
            {
                UpdatePerspective();
            }
        }

        private void UpdatePerspective()
        {
            if (fieldOfView > (OpenTK.MathHelper.Pi - 0.1f))
            {
                fieldOfView = OpenTK.MathHelper.Pi - 0.1f;
            }
            else if (fieldOfView < 0.0000001f)
            {
                fieldOfView = 0.0000001f;
            }
            float aspectRatio = Width / (float)Height;
            scenePerspective = CreatePerspectiveFieldOfViewOffCenter(fieldOfView, aspectRatio, near, far, pinholePos, zFocal);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref scenePerspective);
        }

        Matrix4 CreatePerspectiveFieldOfViewOffCenter(
            float fovy,
            float aspect,
            float zNear,
            float zFar,
            Vector2 lensShift,
            float zFocal)
        {
            float yMax = zNear * (float)System.Math.Tan(0.5f * fovy);
            float yMin = -yMax;
            float xMin = yMin * aspect;
            float xMax = yMax * aspect;

            float mag = -near / zFocal;
            float right = xMax + lensShift.X * mag;
            float left = xMin + lensShift.X * mag;
            float top = yMax + lensShift.Y * mag;
            float bottom = yMin + lensShift.Y * mag;

            return Matrix4.CreatePerspectiveOffCenter(left, right, bottom, top, zNear, zFar);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            DrawScene();
            this.SwapBuffers();
        }

        #endregion

        #region Drawing the scene

        private void DrawScene()
        {
            GL.MatrixMode(MatrixMode.Modelview);
            Matrix4 modelView = camera.ModelView;
            GL.LoadMatrix(ref modelView);

            GL.ClearColor(0, 0, 0, 1);

            GL.PushAttrib(AttribMask.ViewportBit);
            {
                GL.Viewport(0, 0, Width, Height);

                GL.Clear(ClearBufferMask.AccumBufferBit);

                if (enableDofAccumulation)
                {
                    int iterations = unitDiskSamples.Length;
                    float iterationsInv = 1f / iterations;
                    for (int i = 0; i < iterations; i++)
                    {
                        GL.PushMatrix();
                        //pinholePos = new Vector2(
                        //    apertureRadius * (float)random.NextDouble(),
                        //    apertureRadius * (float)random.NextDouble());
                        pinholePos = apertureRadius * unitDiskSamples[i];
                        UpdatePerspective();
                        GL.Translate(-pinholePos.X, -pinholePos.Y, 0);

                        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                        scene.Draw();

                        GL.PopMatrix();
                        GL.Accum(AccumOp.Accum, iterationsInv);
                    }

                    GL.Accum(AccumOp.Return, 1f);

                }
                else
                {
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    scene.Draw();
                }
            }
            GL.PopAttrib();
        }

        #endregion

        #region User interaction

        private void KeyUp(object sender, KeyboardKeyEventArgs e)
        {
            //if (e.Key == Key.R)
            //{
            //    // recompute geometry and redraw layers
            //    scene = GenerateScene();
            //    DrawScene();
            //}
            //else
            if (e.Key == Key.F11)
            {
                bool isFullscreen = (WindowState == WindowState.Fullscreen);
                WindowState = isFullscreen ? WindowState.Normal : WindowState.Fullscreen;
            }
            else if (e.Key == Key.F2)
            {
                enableDofAccumulation = !enableDofAccumulation;
                pinholePos = Vector2.Zero;
                UpdatePerspective();
            }
        }

        private void MouseMove(object sender, MouseMoveEventArgs e)
        {
            if (mouseButtonLeftPressed)
            {
                if (e.XDelta != 0)
                {
                    float angle = 2 * fieldOfView * e.XDelta / (float)Width;
                    Matrix4 rot = Matrix4.CreateRotationY(angle);
                    camera.View = Vector3.TransformVector(camera.View, rot);
                    camera.Up = Vector3.TransformVector(camera.Up, rot);
                }

                if (e.YDelta != 0)
                {
                    float angle = 2 * fieldOfView * e.YDelta / (float)Height;
                    Matrix4 rot = Matrix4.CreateFromAxisAngle(camera.Right, angle);
                    camera.View = Vector3.TransformVector(camera.View, rot);
                    camera.Up = Vector3.TransformVector(camera.Up, rot);
                }
            }
        }

        private void MouseButtonHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                mouseButtonLeftPressed = e.IsPressed;
            }
        }

        private void MouseWheelChanged(object sender, MouseWheelEventArgs e)
        {
            zFocal *= (float)Math.Pow(1.1, e.DeltaPrecise);
        }

        private Scene GenerateScene()
        {
            return Scene.CreateRandomTriangles(10);
        }

        #endregion

    }
}