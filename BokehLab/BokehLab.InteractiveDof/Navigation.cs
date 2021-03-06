﻿namespace BokehLab.InteractiveDof
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using OpenTK;
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Input;

    /// <summary>
    /// Represents the extrinsic camera parameters defining the camera space,
    /// such as camera position and orientation.
    /// </summary>
    class Navigation
    {
        public Camera Camera { get; set; }

        // Indicates that the scene or the parameters of the view changed.
        // If there is any incremental rendering it should be refreshed.
        private bool isViewDirty = false;
        public bool IsViewDirty
        {
            get { return isViewDirty; }
            set
            {
                WasViewDirtyInPrevFrame = isViewDirty;
                isViewDirty = value;
            }
        }

        public bool WasViewDirtyInPrevFrame { get; set; }

        static readonly float DeltaShift = 0.5f;
        static readonly float MaxSensorTiltAngle = OpenTK.MathHelper.PiOver2;

        #region Camera position

        private Vector3 position;

        public Vector3 Position
        {
            get { return position; }
            set { position = value; modelViewDirty = true; }
        }
        private Vector3 view;

        public Vector3 View
        {
            get { return view; }
            set { view = value; modelViewDirty = true; }
        }
        private Vector3 up;

        public Vector3 Up
        {
            get { return up; }
            set { up = value; modelViewDirty = true; }
        }
        private Vector3 right;

        public Vector3 Right
        {
            get { return right; }
            set { right = value; modelViewDirty = true; }
        }

        private Matrix4 modelView;
        public Matrix4 ModelView
        {
            get
            {
                if (modelViewDirty)
                {
                    modelView = ComputeModelView();
                    modelViewDirty = false;
                }
                return modelView;
            }
        }
        bool modelViewDirty = true;

        /// <summary>
        /// Produce a first-person shooter model view matrix.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The goal is a comfortable control witout rotation around the view vector.
        /// </para>
        /// <para>
        /// The formulas were inspired by Kevin R. Harris: http://www.codesampler.com/
        /// </para>
        /// </remarks>
        /// <returns></returns>
        private Matrix4 ComputeModelView()
        {
            view.Normalize();
            right = Vector3.Cross(view, Up);
            right.Normalize();
            up = Vector3.Cross(right, View);
            up.Normalize();

            Vector4 lastRow = new Vector4(
                Vector3.Dot(position, right),
                Vector3.Dot(position, up),
                Vector3.Dot(position, view),
                1);

            Matrix4 m = new Matrix4(
                right.X, up.X, view.X, 0,
                right.Y, up.Y, view.Y, 0,
                right.Z, up.Z, view.Z, 0,
                lastRow.X, lastRow.Y, lastRow.Z, lastRow.W);
            return m;
        }

        #endregion

        public Navigation()
        {
            Camera = new Camera();
            ResetNavigation();
        }

        public void ResetNavigation()
        {
            Position = new Vector3(0, -2, 0);
            View = -Vector3.UnitZ;
            Up = Vector3.UnitY;
            Right = Vector3.UnitX;

            modelView = ComputeModelView();
        }

        public void ResetCamera()
        {
            float aspectRatio = Camera.AspectRatio;
            Camera = new Camera();
            Camera.AspectRatio = aspectRatio;
        }

        public void OnUpdateFrame(FrameEventArgs e, KeyboardDevice Keyboard)
        {
            float deltaShift = DeltaShift;
            if (Keyboard[Key.ShiftLeft] || Keyboard[Key.ShiftRight])
            {
                deltaShift /= 10.0f;
            }

            if (Keyboard[Key.W])
            {
                Position += deltaShift * View;
                IsViewDirty = true;
            }
            else if (Keyboard[Key.S])
            {
                Position -= deltaShift * View;
                IsViewDirty = true;
            }

            if (Keyboard[Key.D])
            {
                Position -= deltaShift * Right;
                IsViewDirty = true;
            }
            else if (Keyboard[Key.A])
            {
                Position += deltaShift * Right;
                IsViewDirty = true;
            }

            if (Keyboard[Key.E])
            {
                Position -= deltaShift * Up;
                IsViewDirty = true;
            }
            else if (Keyboard[Key.Q])
            {
                Position += deltaShift * Up;
                IsViewDirty = true;
            }

            if (Keyboard[Key.Plus])
            {
                Camera.Lens.FocalLength *= 1.05f;
                IsViewDirty = true;
            }
            else if (Keyboard[Key.Minus])
            {
                Camera.Lens.FocalLength /= 1.05f;
                IsViewDirty = true;
            }

            bool perspectiveChanged = false;
            if (Keyboard[Key.Insert])
            {
                Camera.FieldOfView /= 1.1f;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.Delete])
            {
                Camera.FieldOfView *= 1.1f;
                perspectiveChanged = true;
            }

            if (Keyboard[Key.R])
            {
                if (Keyboard[Key.ShiftLeft] || Keyboard[Key.ShiftRight])
                {
                    ResetNavigation();
                }
                else
                {
                    ResetCamera();
                }
                IsViewDirty = true;
                perspectiveChanged = true;
            }

            float viewRotDelta = 0.025f;
            if (Keyboard[Key.Up])
            {
                RotateViewVertical(-viewRotDelta);
            }
            else if (Keyboard[Key.Down])
            {
                RotateViewVertical(viewRotDelta);
            }
            if (Keyboard[Key.Right])
            {
                RotateViewHorizontal(viewRotDelta);
            }
            else if (Keyboard[Key.Left])
            {
                RotateViewHorizontal(-viewRotDelta);
            }

            if (Keyboard[Key.Home])
            {
                Camera.Lens.ApertureNumber *= 1.05f;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.End])
            {
                Camera.Lens.ApertureNumber /= 1.05f;
                perspectiveChanged = true;
            }
            if (Keyboard[Key.PageUp])
            {
                Camera.FocusZ = Math.Min(Camera.FocusZ * 1.05f, -float.Epsilon);
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.PageDown])
            {
                Camera.FocusZ = Math.Min(Camera.FocusZ / 1.05f, -float.Epsilon);
                perspectiveChanged = true;
            }

            if (Keyboard[Key.I])
            {
                float shiftY = BokehLab.Math.MathHelper.Clamp(Camera.LensShift.Y + 0.01f, -Camera.Lens.ApertureRadius, Camera.Lens.ApertureRadius);
                Camera.LensShift = new Vector2(Camera.LensShift.X, shiftY);
                IsViewDirty = true;
            }
            else if (Keyboard[Key.K])
            {
                float shiftY = BokehLab.Math.MathHelper.Clamp(Camera.LensShift.Y - 0.01f, -Camera.Lens.ApertureRadius, Camera.Lens.ApertureRadius);
                Camera.LensShift = new Vector2(Camera.LensShift.X, shiftY);
                IsViewDirty = true;
            }
            else if (Keyboard[Key.L])
            {
                float shiftX = BokehLab.Math.MathHelper.Clamp(Camera.LensShift.X + 0.01f, -Camera.Lens.ApertureRadius, Camera.Lens.ApertureRadius);
                Camera.LensShift = new Vector2(shiftX, Camera.LensShift.Y);
                IsViewDirty = true;
            }
            else if (Keyboard[Key.J])
            {
                float shiftX = BokehLab.Math.MathHelper.Clamp(Camera.LensShift.X - 0.01f, -Camera.Lens.ApertureRadius, Camera.Lens.ApertureRadius);
                Camera.LensShift = new Vector2(shiftX, Camera.LensShift.Y);
                IsViewDirty = true;
            }

            // sensor tilt
            if (Keyboard[Key.X])
            {
                float rotX = BokehLab.Math.MathHelper.Clamp(Camera.SensorRotation.X + 0.01f, -MaxSensorTiltAngle, MaxSensorTiltAngle);
                Camera.SensorRotation = new Vector2(rotX, Camera.SensorRotation.Y);
                IsViewDirty = true;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.Z])
            {
                float rotX = BokehLab.Math.MathHelper.Clamp(Camera.SensorRotation.X - 0.01f, -MaxSensorTiltAngle, MaxSensorTiltAngle);
                Camera.SensorRotation = new Vector2(rotX, Camera.SensorRotation.Y);
                IsViewDirty = true;
                perspectiveChanged = true;
            }
            if (Keyboard[Key.V])
            {
                float rotY = BokehLab.Math.MathHelper.Clamp(Camera.SensorRotation.Y + 0.01f, -MaxSensorTiltAngle, MaxSensorTiltAngle);
                Camera.SensorRotation = new Vector2(Camera.SensorRotation.X, rotY);
                IsViewDirty = true;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.C])
            {
                float rotY = BokehLab.Math.MathHelper.Clamp(Camera.SensorRotation.Y - 0.01f, -MaxSensorTiltAngle, MaxSensorTiltAngle);
                Camera.SensorRotation = new Vector2(Camera.SensorRotation.X, rotY);
                IsViewDirty = true;
                perspectiveChanged = true;
            }

            // sensor shift
            if (Keyboard[Key.N])
            {
                float shiftX = Camera.SensorShift2.X + 0.01f * Camera.SensorSize.X;
                Camera.SensorShift2 = new Vector2(shiftX, Camera.SensorShift2.Y);
                IsViewDirty = true;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.B])
            {
                float shiftX = Camera.SensorShift2.X - 0.01f * Camera.SensorSize.X;
                Camera.SensorShift2 = new Vector2(shiftX, Camera.SensorShift2.Y);
                IsViewDirty = true;
                perspectiveChanged = true;
            }
            if (Keyboard[Key.Comma])
            {
                float shiftY = Camera.SensorShift2.Y + 0.01f * Camera.SensorSize.Y;
                Camera.SensorShift2 = new Vector2(Camera.SensorShift2.X, shiftY);
                IsViewDirty = true;
                perspectiveChanged = true;
            }
            else if (Keyboard[Key.M])
            {
                float shiftY = Camera.SensorShift2.Y - 0.01f * Camera.SensorSize.Y;
                Camera.SensorShift2 = new Vector2(Camera.SensorShift2.X, shiftY);
                IsViewDirty = true;
                perspectiveChanged = true;
            }

            if (perspectiveChanged)
            {
                Camera.UpdatePerspective();
                Matrix4 perspective = Camera.Perspective;

                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadMatrix(ref perspective);
                IsViewDirty = true;
            }
        }

        public void MouseMoveUpdateView(Vector2 delta)
        {
            if (delta.X != 0)
            {
                RotateViewHorizontal(delta.X);
            }

            if (delta.Y != 0)
            {
                RotateViewVertical(delta.Y);
            }
        }

        private void RotateViewVertical(float delta)
        {
            float angle = 2 * Camera.FieldOfView * delta;
            Matrix4 rot = Matrix4.CreateFromAxisAngle(Right, angle);
            View = Vector3.TransformVector(View, rot);
            Up = Vector3.TransformVector(Up, rot);
            IsViewDirty = true;
        }

        private void RotateViewHorizontal(float delta)
        {
            float angle = 2 * Camera.FieldOfView * delta;
            Matrix4 rot = Matrix4.CreateRotationY(angle);
            View = Vector3.TransformVector(View, rot);
            Up = Vector3.TransformVector(Up, rot);
            IsViewDirty = true;
        }

        public void MouseMoveUpdateFocus(Vector2 delta)
        {
            if (delta.Y != 0)
            {
                Camera.FocusZ += delta.Y * Camera.FocusZ;
                IsViewDirty = true;
            }
        }

        public void MouseWheelUpdateFocus(float delta)
        {
            if (delta != 0)
            {
                Camera.FocusZ *= (float)Math.Pow(1.1, delta);
                IsViewDirty = true;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Navigation {");
            sb.AppendLine(Camera.ToString());
            sb.AppendFormat("  Position: {0},", Position);
            sb.AppendLine();
            sb.AppendFormat("  View: {0},", View);
            sb.AppendLine();
            sb.AppendFormat("  Up: {0},", Up);
            sb.AppendLine();
            sb.AppendFormat("  Right: {0}", Right);
            sb.AppendLine();
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
