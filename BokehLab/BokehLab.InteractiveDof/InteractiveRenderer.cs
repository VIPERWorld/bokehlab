﻿namespace BokehLab.InteractiveDof
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;
    using BokehLab.InteractiveDof.DepthPeeling;
    using BokehLab.InteractiveDof.MultiViewAccum;
    using BokehLab.InteractiveDof.NeighborhoodBuffers;
    using BokehLab.InteractiveDof.RayTracing;
    using OpenTK;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Input;

    // TODO:
    // - better acceleration of height field intersection with N-buffers
    //   - take a bounding footpring for all lens rays and clip them together
    // - fix the counter for the whole multi-view accumulation
    // - make sample counts configurable at run-time
    // - create a configuration panel to switch the methods and control parameters
    // - umbra depth peeling, extended-umbra depth peeling

    public class InteractiveRenderer : GameWindow
    {
        public InteractiveRenderer()
            //: base(256, 256)
            //: base(512, 512)
            //: base(300, 200)
            : base(450, 300)
        //: base(800, 600)
        {
        }

        Navigation navigation = new Navigation();

        Scene scene;

        bool mouseButtonLeftPressed = false;
        bool mouseButtonRightPressed = false;

        Mode renderingMode = Mode.Pinhole;

        RenderingInfo renderingInfo = new RenderingInfo();

        MultiViewAccumulation multiViewAccum = new MultiViewAccumulation();
        DepthPeeler depthPeeler = new DepthPeeler();
        NBuffers nBuffers = new NBuffers();
        ImageBasedRayTracer ibrt = new ImageBasedRayTracer();
        LayerVisualizer layerVisualizer = new LayerVisualizer();
        NBuffersVisualizer nBuffersVisualizer = new NBuffersVisualizer();
        //OrderIndependentTransparency transparency = new OrderIndependentTransparency();

        List<IRendererModule> modules = new List<IRendererModule>();

        public static void RunExample()
        {
            using (InteractiveRenderer example = new InteractiveRenderer())
            {
                example.Run(30.0, 0.0);
            }
        }

        #region GameWindow event handlers

        protected override void OnLoad(EventArgs e)
        {
            CheckFboExtension();

            // the scene can be create after the GL context is prepared
            scene = new Scene();

            Keyboard.KeyUp += KeyUp;
            Keyboard.KeyRepeat = true;
            Mouse.Move += MouseMove;
            Mouse.ButtonDown += MouseButtonHandler;
            Mouse.ButtonUp += MouseButtonHandler;
            Mouse.WheelChanged += MouseWheelChanged;

            modules.Add(multiViewAccum);
            modules.Add(depthPeeler);
            modules.Add(nBuffers);
            modules.Add(ibrt);
            modules.Add(layerVisualizer);
            modules.Add(nBuffersVisualizer);
            //modules.Add(transparency);
            // also the modules can be initialized after the GL context is prepared
            foreach (var module in modules)
            {
                module.Initialize(Width, Height);
            }
            ibrt.DepthPeeler = depthPeeler;
            //transparency.DepthPeeler = depthPeeler;
            layerVisualizer.DepthPeeler = depthPeeler;
            nBuffersVisualizer.NBuffers = nBuffers;
            ibrt.NBuffers = nBuffers;
            ibrt.Navigation = navigation;
            multiViewAccum.Navigation = navigation;

            GL.Enable(EnableCap.DepthTest);
            GL.ClearDepth(1.0f);

            //GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);

            //GL.Light(LightName.Light0, LightParameter.Position, new float[] { 3.0f, 3.0f, 3.0f });
            //GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0.3f, 0.3f, 0.3f, 1.0f });
            //GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //GL.Light(LightName.Light0, LightParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //GL.Light(LightName.Light0, LightParameter.SpotExponent, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //GL.LightModel(LightModelParameter.LightModelAmbient, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });
            //GL.LightModel(LightModelParameter.LightModelTwoSide, 1);
            //GL.LightModel(LightModelParameter.LightModelLocalViewer, 1);
            //GL.Enable(EnableCap.Lighting);
            //GL.Enable(EnableCap.Light0);

            //GL.Material(MaterialFace.Front, MaterialParameter.Ambient, new float[] { 0.3f, 0.3f, 0.3f, 1.0f });
            //GL.Material(MaterialFace.Front, MaterialParameter.Diffuse, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //GL.Material(MaterialFace.Front, MaterialParameter.Specular, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //GL.Material(MaterialFace.Front, MaterialParameter.Emission, new float[] { 0.0f, 0.0f, 0.0f, 1.0f });

            //GL.ShadeModel(ShadingModel.Smooth);

            GL.Enable(EnableCap.Texture2D);

            OnResize(new EventArgs());
        }

        private void CheckFboExtension()
        {
            if (!GL.GetString(StringName.Extensions).Contains("EXT_framebuffer_object"))
            {
                MessageBox.Show(
                     "Framebuffer Objects are not supported by the GPU.",
                     "FBOs not supported",
                     MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Exit();
            }
        }

        protected override void OnUnload(EventArgs e)
        {
            foreach (var module in modules)
            {
                module.Dispose();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);

            navigation.Camera.AspectRatio = Width / (float)Height;
            navigation.Camera.UpdatePerspective();
            navigation.IsViewDirty = true;

            GL.MatrixMode(MatrixMode.Projection);
            Matrix4 perspective = navigation.Camera.Perspective;
            GL.LoadMatrix(ref perspective);
            GL.MatrixMode(MatrixMode.Modelview);

            foreach (var module in modules)
            {
                module.Resize(Width, Height);
            }

            base.OnResize(e);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            if (Keyboard[Key.Escape])
            {
                this.Exit();
                return;
            }

            navigation.OnUpdateFrame(e, Keyboard);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.MatrixMode(MatrixMode.Modelview);
            Matrix4 modelView = navigation.ModelView;
            GL.LoadMatrix(ref modelView);

            float cumulativeMilliseconds = 0;
            float averageFrameTime = 0;
            bool accumulation = false;

            switch (renderingMode)
            {
                case Mode.Pinhole:
                    scene.Draw();
                    break;
                case Mode.MultiViewAccum:
                    multiViewAccum.AccumulateAndDraw(scene, navigation);
                    cumulativeMilliseconds = multiViewAccum.CumulativeMilliseconds;
                    averageFrameTime = multiViewAccum.AverageFrameTime;
                    accumulation = true;
                    break;
                case Mode.ImageBasedRayTracing:
                    depthPeeler.PeelLayers(scene);
                    nBuffers.CreateNBuffers(depthPeeler);
                    ibrt.DrawSingleFrame(scene, navigation);
                    //ImageBasedRayTracer.IbrtPlayground.TraceRay(navigation.Camera);
                    break;
                case Mode.IncrementalImageBasedRayTracing:
                    if (navigation.IsViewDirty)
                    {
                        depthPeeler.PeelLayers(scene);
                        nBuffers.CreateNBuffers(depthPeeler);
                    }
                    // disable incremental rendering during active navigation
                    ibrt.IncrementalModeEnabled = !navigation.WasViewDirtyInPrevFrame;
                    if (ibrt.IncrementalModeEnabled)
                    {
                        ibrt.AccumulateAndDraw(scene, navigation);
                        accumulation = true;
                    }
                    else
                    {
                        ibrt.DrawSingleFrame(scene, navigation);
                        accumulation = false;
                    }
                    cumulativeMilliseconds = ibrt.CumulativeMilliseconds;
                    averageFrameTime = ibrt.AverageFrameTime;
                    break;
                case Mode.LayerVisualizer:
                    depthPeeler.PeelLayers(scene);
                    layerVisualizer.Draw();
                    break;
                case Mode.NBuffersVisualizer:
                    depthPeeler.PeelLayers(scene);
                    nBuffers.CreateNBuffers(depthPeeler);
                    nBuffersVisualizer.Draw();
                    break;
                //case Mode.OrderIndependentTransparency:
                //    depthPeeler.PeelLayers(scene);
                //    transparency.Draw();
                //    break;
                default:
                    Debug.Assert(false, "Unknown rendering mode");
                    break;
            }
            navigation.WasViewDirtyInPrevFrame = false;

            renderingInfo.CurrentFps = (float)(1.0 / e.Time);
            renderingInfo.CurrentMilliseconds = (float)(1000 * e.Time);
            renderingInfo.AccumulationMilliseconds = cumulativeMilliseconds;
            renderingInfo.AverageFps = (averageFrameTime != 0) ? 1000f / averageFrameTime : 0;
            renderingInfo.AverageMilliseconds = averageFrameTime;

            string title = string.Format("BokehLab {0:0.0}FPS/{1:0.0}ms",
                renderingInfo.CurrentFps, renderingInfo.CurrentMilliseconds);
            if (accumulation && (averageFrameTime != 0))
            {
                title += string.Format(" acc {0:0.0}ms, avg {1:0.0}FPS/{2:0.0}ms",
                    renderingInfo.AccumulationMilliseconds, renderingInfo.AverageFps, renderingInfo.AverageMilliseconds);
            }
            this.Title = title;

            this.SwapBuffers();
        }

        #endregion

        #region User interaction

        private void KeyUp(object sender, KeyboardKeyEventArgs e)
        {
            //if (e.Key == Key.G)
            //{
            //    // recompute geometry and redraw layers
            //    scene = GenerateScene();
            //    navigation.IsViewDirty = true;
            //}
            //else
            if (e.Key == Key.F1)
            {
                MessageBox.Show(
@"===== Program help =====
BokehLab - interactive depth-of-field renderer.
Bohumír Zamečník, MFF UK, 2010-2011

=== Rendering modes ===
F1 - show this help dialog
F2 - plain rasterization (pinhole view)
F3 - (incremental) multi-view accumulation
    Tab - toggle incremental rendering
    [/] - decrease/increase the total number of samples
F4 - image-based ray tracing
    [/] - decrease/increase the total number of samples
F5 - incremental image-based ray tracing
F6 - visualization of layered depth images
    Tab - select layer type: color/depth/packed-depth
    O/P/U - select previous/next/first layer
F7 - visualization of N-buffers
    Tab - select channel mask: min/max, min, max
    O/P/U - select previous/next/first layer
F11 - toggle full screen
F12 - show information

=== Scene ===
F8 - toggle showing more complex models (dragon, teapot, etc.)
F9 - change the number of shown complex models
F10 - toggle showing white or colorized stars

=== Navigation ===
W/S/A/D/Q/E - go forwards/backwards/left/right/down/up
Up/down/right/left arrow - change orientation - look up/down/right/left
Mouse left button + drag - change orientation
Shift+[WSADQE] - move more precisely
Shift+R - reset navigation

=== Camera parameters ===
Page up/Page down - increase/decrease focus plane distance (focus forth/back)
Mouse right button + drag up/down - focus forth/back
Mouse wheel up/down - focus forth/back
Home/End - increase/decrease aperture radius
Plus/Minus - increase/decrease focal length
Delete/Insert - increase/decrease angle of view (zoom in/out)
X/Z - increase/decrease sensor tilt around X axis
V/C - increase/decrease sensor tilt around Y axis
N/B - increase/decrease sensor shift in X axis
,/M - increase/decrease sensor shift in Y axis
R - reset camera",
"BokehLab - interactive DoF rendering via image-based ray-tracing");
            }
            else if (e.Key == Key.F2)
            {
                if (renderingMode != Mode.Pinhole)
                {
                    foreach (var module in modules)
                    {
                        module.Enabled = false;
                    }
                    renderingMode = Mode.Pinhole;
                    navigation.IsViewDirty = true;
                }
            }
            else if (e.Key == Key.F3)
            {
                if (renderingMode != Mode.MultiViewAccum)
                {
                    foreach (var module in modules)
                    {
                        module.Enabled = false;
                    }
                    multiViewAccum.Enabled = true;
                    renderingMode = Mode.MultiViewAccum;
                    navigation.IsViewDirty = true;
                }
            }
            else if (e.Key == Key.F4)
            {
                if (renderingMode != Mode.ImageBasedRayTracing)
                {
                    foreach (var module in modules)
                    {
                        module.Enabled = false;
                    }
                    depthPeeler.Enabled = true;
                    ibrt.Enabled = true;
                    ibrt.IncrementalModeEnabled = false;
                    nBuffers.Enabled = true;
                    renderingMode = Mode.ImageBasedRayTracing;
                    navigation.IsViewDirty = true;
                }
            }
            else if (e.Key == Key.F5)
            {
                if (renderingMode != Mode.IncrementalImageBasedRayTracing)
                {
                    foreach (var module in modules)
                    {
                        module.Enabled = false;
                    }
                    depthPeeler.Enabled = true;
                    ibrt.Enabled = true;
                    ibrt.IncrementalModeEnabled = true;
                    nBuffers.Enabled = true;
                    renderingMode = Mode.IncrementalImageBasedRayTracing;
                    navigation.IsViewDirty = true;
                    navigation.WasViewDirtyInPrevFrame = false;
                }
            }
            else if (e.Key == Key.F6)
            {
                if (renderingMode != Mode.LayerVisualizer)
                {
                    foreach (var module in modules)
                    {
                        module.Enabled = false;
                    }
                    depthPeeler.Enabled = true;
                    layerVisualizer.Enabled = true;
                    renderingMode = Mode.LayerVisualizer;
                    navigation.IsViewDirty = true;
                }
            }
            else if (e.Key == Key.F7)
            {
                if (renderingMode != Mode.NBuffersVisualizer)
                {
                    foreach (var module in modules)
                    {
                        module.Enabled = false;
                    }
                    depthPeeler.Enabled = true;
                    nBuffers.Enabled = true;
                    nBuffersVisualizer.Enabled = true;
                    renderingMode = Mode.NBuffersVisualizer;
                    navigation.IsViewDirty = true;
                }
            }
            //else if (e.Key == Key.F8)
            //{
            //    if (renderingMode != Mode.OrderIndependentTransparency)
            //    {
            //        foreach (var module in modules)
            //        {
            //            module.Enabled = false;
            //        }
            //        depthPeeler.Enabled = true;
            //        transparency.Enabled = true;
            //        renderingMode = Mode.OrderIndependentTransparency;
            //        navigation.IsViewDirty = true;
            //    }
            //}
            else if (e.Key == Key.F8)
            {
                scene.BigModelsEnabled = !scene.BigModelsEnabled;
                navigation.IsViewDirty = true;
            }
            else if (e.Key == Key.F9)
            {
                scene.BigModelsEnabledCount += 1;
                navigation.IsViewDirty = true;
            }
            else if (e.Key == Key.F10)
            {
                scene.ColorizeStars = !scene.ColorizeStars;
                navigation.IsViewDirty = true;
            }
            else if (e.Key == Key.F11)
            {
                bool isFullscreen = (WindowState == WindowState.Fullscreen);
                WindowState = isFullscreen ? WindowState.Normal : WindowState.Fullscreen;
            }
            else if (e.Key == Key.F12)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(renderingInfo.ToString());
                sb.AppendLine(string.Format("Image resolution: {0}x{1}", Width, Height));
                sb.AppendLine();
                sb.AppendLine(navigation.ToString());
                if (ibrt.Enabled)
                {
                    sb.AppendLine(ibrt.ToString());
                }
                else if (multiViewAccum.Enabled)
                {
                    sb.AppendLine(multiViewAccum.ToString());
                }
                MessageBox.Show(sb.ToString(), "Camera and navigation info");
            }

            foreach (var module in modules)
            {
                module.OnKeyUp(sender, e);
            }
        }

        private void MouseMove(object sender, MouseMoveEventArgs e)
        {
            Vector2 delta = new Vector2(e.XDelta / (float)Width, e.YDelta / (float)Height);
            if (mouseButtonLeftPressed)
            {
                navigation.MouseMoveUpdateView(delta);
            }
            if (mouseButtonRightPressed)
            {
                navigation.MouseMoveUpdateFocus(delta);
            }
        }

        private void MouseButtonHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                mouseButtonLeftPressed = e.IsPressed;
            }
            if (e.Button == MouseButton.Right)
            {
                mouseButtonRightPressed = e.IsPressed;
            }
        }

        private void MouseWheelChanged(object sender, MouseWheelEventArgs e)
        {
            navigation.MouseWheelUpdateFocus(e.DeltaPrecise);
        }

        //private Scene GenerateScene()
        //{
        //    return Scene.CreateRandomTriangles(10);
        //}

        #endregion

        enum Mode
        {
            Pinhole,
            MultiViewAccum,
            ImageBasedRayTracing,
            IncrementalImageBasedRayTracing,
            LayerVisualizer,
            NBuffersVisualizer,
            //OrderIndependentTransparency,
        }

        class RenderingInfo
        {
            public float CurrentFps;
            public float CurrentMilliseconds;
            public float AccumulationMilliseconds;
            public float AverageFps;
            public float AverageMilliseconds;

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("RenderingInfo {");
                sb.AppendFormat("  Current frame FPS: {0},", CurrentFps);
                sb.AppendLine();
                sb.AppendFormat("  Current frame time: {0} ms,", CurrentMilliseconds);
                sb.AppendLine();
                sb.AppendFormat("  Accumulation time: {0} ms,", AccumulationMilliseconds);
                sb.AppendLine();
                sb.AppendFormat("  Average FPS: {0},", AverageFps);
                sb.AppendLine();
                sb.AppendFormat("  Average frame time: {0} ms", AverageMilliseconds);
                sb.AppendLine();
                sb.AppendLine("}");
                return sb.ToString();
            }
        }
    }
}
