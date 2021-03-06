﻿namespace BokehLab.InteractiveDof
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using OpenTK;

    // TODO: compute field of view from focal length and senzor size and position

    /// <summary>
    /// Represents the intrinsic camera parameters.
    /// </summary>
    /// <remarks>
    /// We are assuming the thin lens model with untilted sensor with an
    /// aperture at the lens principal plane.
    /// </remarks>
    class Camera
    {
        private float focusZ;
        /// <summary>
        /// Depth (signed Z coordinate) of the focus plane (sensor center
        /// image) in the camera space.
        /// </summary>
        /// <remarks>
        /// It should lie in the -z half-space.
        /// </remarks>
        public float FocusZ
        {
            get { return focusZ; }
            set
            {
                focusZ = value;
                sensorZ = Math.Max(Lens.Transform(new Vector3(0, 0, value)).Z, float.Epsilon);
            }
        }

        private float sensorZ;
        /// <summary>
        /// Depth (signed Z coordinate) of the sensor center in camera space.
        /// </summary>
        /// <remarks>
        /// It should lie in the +z half-space.
        /// </remarks>
        public float SensorZ
        {
            get { return sensorZ; }
            set
            {
                sensorZ = value;
                focusZ = Lens.Transform(new Vector3(0, 0, value)).Z;
            }
        }

        /// <summary>
        /// Sensor shift in the XY plane (without focusing).
        /// </summary>
        /// <remarks>
        /// The shift goes after the sensor rotation.
        /// </remarks>
        public Vector2 SensorShift2 { get; set; }

        /// <summary>
        /// Total sensor shift in the XYZ space (including its depth).
        /// </summary>
        public Vector3 SensorShift3
        {
            get
            {
                return new Vector3(SensorShift2.X, SensorShift2.Y, sensorZ);
            }
        }

        /// <summary>
        /// Sensor rotation (tilt) around its center.
        /// </summary>
        /// <remarks>
        /// The rotation goes before the shift. First the rotation around the
        /// X axis is applied, then around the rotated Y axis.
        /// </remarks>
        public Vector2 SensorRotation { get; set; }

        // vertiacal angle of view 27 degrees for 50mm lens on full frame film (36x24mm)
        public static readonly float DefaultFieldOfView = 0.471238f;

        private float fieldOfView = DefaultFieldOfView;
        /// <summary>
        /// Field of view of the camera.
        /// </summary>
        /// <remarks>
        /// Relates the sensor depth and size. Assuming the aperture coincides
        /// with the lens principal plane this is ok.
        /// </remarks>
        public float FieldOfView
        {
            get { return fieldOfView; }
            set
            {
                fieldOfView = BokehLab.Math.MathHelper.Clamp(value,
                    0.0000001f, OpenTK.MathHelper.Pi - 0.1f);
            }
        }

        private float aspectRatio = 1.0f;
        /// <summary>
        /// Sensor aspect ratio (width/height).
        /// </summary>
        public float AspectRatio
        {
            get { return aspectRatio; }
            set
            {
                aspectRatio = value;
            }
        }

        /// <summary>
        /// Sensor size in camera space.
        /// </summary>
        public Vector2 SensorSize
        {
            get
            {
                float height = 2 * sensorZ * (float)System.Math.Tan(0.5f * fieldOfView);
                float width = height * aspectRatio;
                return new Vector2(width, height);
            }
        }

        public ThinLens Lens { get; private set; }


        private float near = 0.25f;
        /// <summary>
        /// Unsigned near plane distance. The plane lies on -Near.
        /// </summary>
        public float Near
        {
            get { return near; }
            set
            {
                near = value;
                Perspective = GetPerspective();
            }
        }

        private float far = 100f;
        /// <summary>
        /// Unsigned far plane distance. The plane lies on -Far.
        /// </summary>
        public float Far
        {
            get { return far; }
            set
            {
                far = value;
                Perspective = GetPerspective();
            }
        }

        /// <summary>
        /// Perspective matrix of the center of the aperture. Indended to be
        /// used by OpenGL for rasterization.
        /// </summary>
        public Matrix4 Perspective { get; private set; }

        /// <summary>
        /// Frustum bounds (right, left, top,bottom).
        /// </summary>
        public Vector4 FrustumBounds { get; set; }

        // just for debugging purposes to get a single view from a lens sample position
        public Vector2 LensShift { get; set; }

        public Camera()
        {
            Lens = new ThinLens() { ApertureNumber = 2.8f, FocalLength = 0.05f };
            //FocusZ = -(20 * Lens.FocalLength);
            FocusZ = -4f;
            UpdatePerspective();
        }

        public void UpdatePerspective()
        {
            Perspective = GetPerspective();
            float yMax = near * (float)System.Math.Tan(0.5f * fieldOfView);
            float yMin = -yMax;
            float xMin = yMin * aspectRatio;
            float xMax = yMax * aspectRatio;
            FrustumBounds = new Vector4(xMax, xMin, yMax, yMin);
        }

        private Matrix4 GetPerspective()
        {
            //return Matrix4.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, near, far);
            return GetSensorShiftPerspective();
        }

        public Vector2 GetPinholePos(Vector2 lensSample)
        {
            return Lens.ApertureRadius * lensSample;
        }

        public Matrix4 GetMultiViewPerspective(Vector2 pinholePos)
        {
            //float mag = near / FocusZ; // == -((-near) / FocusZ)
            //Vector2 nearShift = mag * pinholePos;

            Vector2 nearShift = near * (pinholePos / FocusZ + SensorShift2 / -SensorZ);
            return CreatePerspectiveFieldOfViewOffCenter(FieldOfView, AspectRatio, near, far, nearShift, FocusZ);
        }

        public Matrix4 GetSensorShiftPerspective()
        {
            float mag = near / -SensorZ; // == -((-near) / -SenzorZ)
            Vector2 nearShift = mag * SensorShift2;
            return CreatePerspectiveFieldOfViewOffCenter(FieldOfView, AspectRatio, near, far, nearShift, FocusZ);
        }

        private Matrix4 CreatePerspectiveFieldOfViewOffCenter(
            float fovy,
            float aspect,
            float near,
            float far,
            Vector2 nearShift,
            float zFocus)
        {
            float yMax = near * (float)System.Math.Tan(0.5f * fovy);
            float yMin = -yMax;
            float xMin = yMin * aspect;
            float xMax = yMax * aspect;


            float right = xMax + nearShift.X;
            float left = xMin + nearShift.X;
            float top = yMax + nearShift.Y;
            float bottom = yMin + nearShift.Y;

            return Matrix4.CreatePerspectiveOffCenter(left, right, bottom, top, near, far);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Camera {");
            sb.AppendLine(Lens.ToString());
            sb.AppendFormat("  Focus plane Z: {0},", FocusZ);
            sb.AppendLine();
            sb.AppendFormat("  Sensor center Z: {0},", SensorZ);
            sb.AppendLine();
            sb.AppendFormat("  Sensor tilt: {0},", SensorRotation);
            sb.AppendLine();
            sb.AppendFormat("  Sensor shift: {0},", SensorShift2);
            sb.AppendLine();
            sb.AppendFormat("  Sensor size: {0},", SensorSize);
            sb.AppendLine();
            sb.AppendFormat("  Near: {0}, Far: {1},", Near, Far);
            sb.AppendLine();
            sb.AppendFormat("  Field of view: {0},", FieldOfView);
            sb.AppendLine();
            sb.AppendFormat("  Aspect ratio: {0}", AspectRatio);
            sb.AppendLine();
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
