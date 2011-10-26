﻿namespace BokehLab.RayTracing.Test
{
    using System;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using BokehLab.Math;
    using OpenTK;
    using BokehLab.RayTracing;
    using BokehLab.FloatMap;
    using System.Drawing;

    class HeightFieldTest
    {
        public void TrySimpleHeightfield1x1()
        {
            FloatMapImage data = new FloatMapImage(1, 1, PixelFormat.Greyscale);
            data.Image[0, 0, 0] = 0.5f;
            HeightField heightfield = new HeightField(new[] { data });

            Vector3d start = new Vector3d(0.1, 0.1, 0.25);
            Vector3d end = new Vector3d(0.2, 0.2, 0.75);
            IntersectAndReport(heightfield, start, end);
        }

        public void TrySimpleHeightfield1x1WithPerpendicularRay()
        {
            FloatMapImage data = new FloatMapImage(1, 1, PixelFormat.Greyscale);
            data.Image[0, 0, 0] = 0.5f;
            HeightField heightfield = new HeightField(new[] { data });

            Vector3d start = new Vector3d(0.1, 0.1, 0.25);
            Vector3d end = new Vector3d(0.1, 0.1, 0.75);
            IntersectAndReport(heightfield, start, end);
        }

        public void TrySimpleHeightfield2x2()
        {
            FloatMapImage data = new FloatMapImage(2, 2, PixelFormat.Greyscale);
            data.Image[0, 0, 0] = 0.5f;
            data.Image[0, 1, 0] = 0.5f;
            data.Image[1, 0, 0] = 0.5f;
            data.Image[1, 1, 0] = 0.5f;
            HeightField heightfield = new HeightField(new[] { data });

            Vector3d start = new Vector3d(0.5, 1.5, 0.0);
            Vector3d end = new Vector3d(2, 2, 1);
            IntersectAndReport(heightfield, start, end);
        }

        private static void IntersectAndReport(HeightField heightfield, Vector3d start, Vector3d end)
        {
            Ray ray = new Ray(start, end - start);
            Intersection intersection = heightfield.Intersect(ray);
            Console.WriteLine((intersection != null) ? intersection.Position.ToString() : "no intersection");
        }

        private static void MeasureIntersectionPerformance(HeightField heightfield, Vector3d start, Vector3d end, int iterations)
        {
            Ray ray = new Ray(start, end - start);
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                Intersection intersection = heightfield.Intersect(ray);
            }
            sw.Stop();

            Console.WriteLine("Iterations: {0}", iterations);
            Console.WriteLine("Total time: {0} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("Average time: {0} ms", sw.ElapsedMilliseconds / (double)iterations);
            Console.WriteLine("Throughput: {0} isec/s", iterations / ((double)sw.ElapsedMilliseconds * 0.001));
        }

        private static void TryFootprintTraversalPerformance()
        {
            //HeightField heightfield = new HeightField(1000, 1000);
            //Vector3d start = new Vector3d(2.5, 1.5, 0);
            //Vector3d end = new Vector3d(999.5, 998.5, 1);
            //int iterations = 10000;

            HeightField heightfield = new HeightField(200, 200);
            Vector3d start = new Vector3d(169, 181, 0);
            Vector3d end = new Vector3d(14, 191, 1);
            int iterations = 1000;
            MeasureIntersectionPerformance(heightfield, start, end, iterations);
        }

        private static void TryIntersectionPerformance()
        {
            List<FloatMapImage> layers = new List<FloatMapImage>();
            layers.Add(((Bitmap)Bitmap.FromFile("../../data/2011-05-30_04-50-47_depth_0.png")).ToFloatMap());
            layers.Add(((Bitmap)Bitmap.FromFile("../../data/2011-05-30_04-50-47_depth_1.png")).ToFloatMap());
            layers.Add(((Bitmap)Bitmap.FromFile("../../data/2011-05-30_04-50-47_depth_2.png")).ToFloatMap());
            layers.Add(((Bitmap)Bitmap.FromFile("../../data/2011-05-30_04-50-47_depth_3.png")).ToFloatMap());
            HeightField heightfield = new HeightField(layers);
            Vector3d start = new Vector3d(169, 181, 0);
            Vector3d end = new Vector3d(14, 191, 1);
            int iterations = 100000;
            MeasureIntersectionPerformance(heightfield, start, end, iterations);
        }
    }
}
