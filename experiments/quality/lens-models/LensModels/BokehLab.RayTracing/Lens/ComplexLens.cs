﻿namespace BokehLab.RayTracing.Lens
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using OpenTK;
    using BokehLab.Math;

    // TODO:
    // - create lens from surface definition list
    // - it would be great if the lens could be defined in XML
    // - complex lenses are not symetric in general
    //   - for light tracing (traced from front to back) a reversed definition
    //     could be used along with a common back-to-front tracing algorithm
    // - sample only exit pupil not the whole back surface

    /// <summary>
    /// Complex lens model where lens are composed of multiple spherical or
    /// circular surfaces or circular stops.
    /// </summary>
    /// <remarks>
    /// Lens model by Kolb et al. Sequential ray tracing is performed from
    /// back to front.
    /// </remarks>
    public class ComplexLens : ILens, IIntersectable
    {
        public IList<ElementSurface> ElementSurfaces { get; private set; }

        private Sphere frontSphericalSurface;
        private Sphere backSphericalSurface;

        private double backSurfaceSinTheta;
        private double frontSurfaceSinTheta;

        public double MediumRefractiveIndex { get; set; }

        private static readonly double epsilon = 1e-6;

        /// <summary>
        /// Creates and initializes a new instance of a complex lens.
        /// </summary>
        /// <param name="surfaces">List of element surfaces, ordered from
        /// back to front.</param>
        public ComplexLens(IList<ElementSurface> surfaces)
        {
            ElementSurfaces = surfaces;

            ElementSurface frontSurface = surfaces.LastOrDefault((surface) => surface.Surface is Sphere);
            frontSphericalSurface = (Sphere)frontSurface.Surface;
            frontSurfaceSinTheta = frontSphericalSurface.GetCapElevationAngleSine(frontSurface.ApertureRadius);

            ElementSurface backSurface = surfaces.FirstOrDefault((surface) => surface.Surface is Sphere);
            backSphericalSurface = (Sphere)backSurface.Surface;
            backSurfaceSinTheta = backSphericalSurface.GetCapElevationAngleSine(backSurface.ApertureRadius);

            MediumRefractiveIndex = Materials.Fixed.AIR;
            frontSurface.NextRefractiveIndex = MediumRefractiveIndex;
        }

        /// <summary>
        /// Creates a new instance of a complex lens using a definition of
        /// elements.
        /// </summary>
        /// <remarks>
        /// The first and last surfaces have to be spherical. TODO: this is
        /// needed only for simpler sampling. In general planar surfaces or
        /// stops could be sampled too.
        /// </remarks>
        /// <param name="surfaceDefs">List of definitions of spherical or
        /// planar element surfaces or stops. Ordered from front to back.
        /// Must not be empty or null.
        /// </param>
        /// <param name="mediumRefractiveIndex">Index of refraction of medium
        /// outside the lens. It is assumed there is one medium on the scene
        /// side, senzor side and inside the lens.</param>
        /// <returns>The created complex lens instance.</returns>
        public static ComplexLens Create(
            IList<SphericalElementSurfaceDefinition> surfaceDefs,
            double mediumRefractiveIndex)
        {
            var surfaces = new List<ElementSurface>();
            double lastRefractiveIndex = mediumRefractiveIndex;
            double lastThickness = 0;

            // definition list is ordered from front to back, working list
            // must be ordered from back to front, so a conversion has to be
            // performed
            foreach (var definition in surfaceDefs.Reverse())
            {
                ElementSurface surface = new ElementSurface();
                surface.ApertureRadius = definition.ApertureRadius;
                surface.NextRefractiveIndex = lastRefractiveIndex;
                lastRefractiveIndex = definition.NextRefractiveIndex;
                if (definition.CurvatureRadius.HasValue)
                {
                    // spherical surface
                    double radius = definition.CurvatureRadius.Value;
                    // convexity reverses when converting from front-to-back
                    // back-to-front ordering
                    surface.Convex = radius < 0;
                    Sphere sphere = new Sphere()
                    {
                        Radius = Math.Abs(radius)
                    };
                    sphere.Center = Math.Sign(radius) *
                        sphere.GetCapCenter(surface.ApertureRadius, Vector3d.UnitZ);
                    surface.Surface = sphere;
                    surface.SurfaceNormalField = sphere;
                    // TODO
                    //surface.ShiftToNext = ...
                }
                else
                {
                    // planar surface
                    // both media are the same -> circular stop
                    // else -> planar element surface
                    surface.NextRefractiveIndex = definition.NextRefractiveIndex;
                    surface.Convex = true;
                    surface.Surface = new Circle()
                    {
                        Radius = definition.ApertureRadius
                    };

                    // TODO:
                    //surface.ShiftToNext = ...
                }
                surface.ShiftToNext = lastThickness;
                lastThickness = definition.Thickness;
            }

            ComplexLens lens = new ComplexLens(surfaces)
            {
                MediumRefractiveIndex = mediumRefractiveIndex
            };
            return lens;

            throw new NotImplementedException();
        }

        public static ComplexLens CreateBiconvexLens(double curvatureRadius, double apertureRadius)
        {
            //double thickness = 5;
            double thickness = 0;
            var surfaces = new List<ComplexLens.ElementSurface>();
            Sphere backSphere = new Sphere()
            {
                Radius = curvatureRadius,
            };
            backSphere.Center = backSphere.GetCapCenter(apertureRadius, -Vector3d.UnitZ);
            surfaces.Add(new ComplexLens.ElementSurface()
            {
                ApertureRadius = apertureRadius,
                NextRefractiveIndex = Materials.Fixed.GLASS_CROWN_K7,
                ShiftToNext = thickness, // DEBUG
                Surface = backSphere,
                SurfaceNormalField = backSphere,
                Convex = true
            });
            Sphere frontSphere = new Sphere()
            {
                Radius = curvatureRadius,
            };
            frontSphere.Center = frontSphere.GetCapCenter(apertureRadius, Vector3d.UnitZ);
            surfaces.Add(new ComplexLens.ElementSurface()
            {
                ApertureRadius = apertureRadius,
                ShiftToNext = 0,
                Surface = frontSphere,
                SurfaceNormalField = frontSphere,
                Convex = false
            });

            ComplexLens lens = new ComplexLens(surfaces);
            return lens;
        }

        #region ILens Members

        public Ray Transfer(Vector3d objectPos, Vector3d lensPos)
        {
            //Console.WriteLine("Complex lens");

            double lastRefractiveIndex = MediumRefractiveIndex;
            Vector3d shiftFromLensCenter = Vector3d.Zero;
            Ray incomingRay = new Ray(objectPos, lensPos - objectPos);
            //Console.WriteLine("Blue, {0}, ", incomingRay.ToLine());
            //Console.WriteLine("Incoming: {0}, ", Ray.NormalizeDirection(incomingRay).ToString());
            Ray outgoingRay = new Ray(incomingRay.Origin, incomingRay.Direction);

            foreach (ElementSurface surface in ElementSurfaces)
            {
                double nextRefractiveIndex = surface.NextRefractiveIndex;

                // Compute intersection

                incomingRay.Origin -= shiftFromLensCenter;
                // the intersection is done on the element surface in its
                // normalized position
                Intersection intersection = surface.Surface.Intersect(incomingRay);
                if (intersection == null)
                {
                    // no intersection at all
                    return null;
                }
                if (intersection.Position.Xy.LengthSquared >
                    surface.ApertureRadius * surface.ApertureRadius)
                {
                    // intersection is outside the aperture
                    return null;
                }
                intersection.Position += shiftFromLensCenter;

                // Compute refracted ray

                if (Math.Abs(lastRefractiveIndex - nextRefractiveIndex) > epsilon)
                {
                    // there is a change of refractive index
                    Vector3d intersectionPos = intersection.Position;
                    Vector3d normal = surface.SurfaceNormalField.GetNormal(intersectionPos);
                    if (!surface.Convex)
                    {
                        normal = -normal;
                    }
                    incomingRay.NormalizeDirection();
                    Vector3d refractedDirection = Ray.Refract(
                        incomingRay.Direction, normal, lastRefractiveIndex,
                        nextRefractiveIndex, false);
                    outgoingRay = new Ray(intersectionPos, refractedDirection);
                }
                else
                {
                    // there's no border between different media and thus no refraction
                    outgoingRay = new Ray(incomingRay.Origin, incomingRay.Direction);
                }

                lastRefractiveIndex = nextRefractiveIndex;
                shiftFromLensCenter += new Vector3d(0, 0, -surface.ShiftToNext);
                incomingRay = new Ray(outgoingRay.Origin, outgoingRay.Direction);
                //Console.WriteLine("Red, {0}, ", outgoingRay.ToLine());
                //Console.WriteLine("Outgoing: {0}, ", Ray.NormalizeDirection(outgoingRay).ToString());
            }

            outgoingRay.NormalizeDirection();
            return outgoingRay;
        }

        public Vector3d GetBackSurfaceSample(Vector2d sample)
        {
            Vector3d unitSphereSample = Sampler.UniformSampleSphere(
                sample, backSurfaceSinTheta, 1);
            return backSphericalSurface.Center + backSphericalSurface.Radius * unitSphereSample;
        }

        public Vector3d GetFrontSurfaceSample(Vector2d sample)
        {
            Vector3d unitSphereSample = Sampler.UniformSampleSphere(
                sample, frontSurfaceSinTheta, 1);
            return frontSphericalSurface.Center + frontSphericalSurface.Radius * (-unitSphereSample);
        }

        #endregion

        #region IIntersectable Members

        public Intersection Intersect(Ray ray)
        {
            return backSphericalSurface.Intersect(ray);
        }

        #endregion

        /// <summary>
        /// Representation of one element surface suitable for ray tracing.
        /// </summary>
        /// <remarks>
        /// The surfaces follow one after another from back of the lens to
        /// the front (the same way as ray tracing proceeds within the lens).
        /// </remarks>
        public class ElementSurface
        {
            /// <summary>
            /// The concrete intersectable surface object (sphere, plane, ...).
            /// </summary>
            public IIntersectable Surface;
            /// <summary>
            /// Surface which can be queried for normals.
            /// </summary>
            /// <remarks>
            /// This can be only a different view at one surface implementation
            /// (in addition to IIntersectable).
            /// </remarks>
            public INormalField SurfaceNormalField;
            /// <summary>
            /// Radius of aperture in the base (XY) plane.
            /// </summary>
            public double ApertureRadius;
            /// <summary>
            /// (Signed) shift from one element base (XY) plane to another.
            /// </summary>
            public double ShiftToNext;
            /// <summary>
            /// Index of refraction of the material after this surface.
            /// </summary>
            public double NextRefractiveIndex;
            /// <summary>
            /// Indicates whether the surface is convex when view from back.
            /// </summary>
            /// <remarks>
            /// Planar surface is convex.
            /// </remarks>
            public bool Convex;
        }

        /// <summary>
        /// Definition of one element surface. A list of such definitions
        /// should serve for creating an instance of the complex lens.
        /// </summary>
        /// <remarks>
        /// The surfaces follow one after another from front of the lens to
        /// the back. The format of definition should be suitable to the
        /// format in which the lens data are commonly available.
        /// </remarks>
        public class SphericalElementSurfaceDefinition
        {
            /// <summary>
            /// Radius of aperture in the base (XY) plane.
            /// </summary>
            public double ApertureRadius;
            /// <summary>
            /// Signed radius of curvature. Positive: convex from front,
            /// negative: concave from front. In case of no value the surface
            /// is not spherical but rather planar (a planar glass surface or
            /// a stop).
            /// </summary>
            public double? CurvatureRadius;
            /// <summary>
            /// (Unsigned) distance from this element's apex to the next
            /// element's apex.
            /// </summary>
            public double Thickness;
            /// <summary>
            /// Index of refraction of the material after this surface.
            /// </summary>
            public double NextRefractiveIndex;
        }


    }
}
