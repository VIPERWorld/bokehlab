﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using libpfm;
using mathHelper;

namespace spreading
{
    public interface BlurFunction
    {
        float GetPSFRadius(int x, int y);
    }

    public class DepthMapBlur : BlurFunction
    {
        PFMImage DepthMap { get; set; }
        float MaxPSFRadius { get; set; }

        public DepthMapBlur(PFMImage depthMap, int maxPSFRadius)
        {
            DepthMap = depthMap;
            MaxPSFRadius = maxPSFRadius;
        }

        public float GetPSFRadius(int x, int y)
        {
            return DepthMap.Image[x, y, 0] * MaxPSFRadius;
        }
    }

    public class ThinLensDepthMapBlur : BlurFunction
    {
        public PFMImage DepthMap { get; set; }
        float FocalLength { get; set; }
        // TODO: is it radius or diameter?
        public float Aperture { get; set; }
        float NearPlane { get; set; }
        float FarPlane { get; set; }

        public float FocusPlane { get; set; }

        public ThinLensDepthMapBlur(float focalLength, float aperture, float near, float far, float focusPlane)
        {
            FocalLength = focalLength;
            Aperture = aperture;
            NearPlane = near;
            FarPlane = far;
            FocusPlane = focusPlane;
        }

        public float GetPSFRadius(int x, int y)
        {
            Debug.Assert(DepthMap != null);
            // map depth map value [0; 1] to z coordinate [near; far]
            float z = DepthMap.Image[x, y, 0] * (FarPlane - NearPlane) + NearPlane;
            // compute circle of confusion radius
            float cocRadius = Math.Abs(z - FocusPlane) * FocalLength * Aperture /
                (z * (FocusPlane - FocalLength));
            // the radius is for a perfect circular PSF with constant intensity
            return Math.Abs(cocRadius);
        }
    }

    public class ProceduralBlur : BlurFunction
    {
        public static readonly int DEFAULT_BLUR_RADIUS = 25;
        float MaxPSFRadius { get; set; }

        float widthInv;
        float heightInv;

        public ProceduralBlur(int width, int height, int maxPSFRadius)
        {
            widthInv = 1.0f / (float)width;
            heightInv = 1.0f / (float)height;
            MaxPSFRadius = maxPSFRadius;
        }

        public float GetPSFRadius(int x, int y)
        {
            // coordinates normalized to [0.0; 1.0]
            //float xNorm = x * widthInv;
            float yNorm = y * heightInv;
            return MaxPSFRadius * Math.Abs(2 * yNorm - 1);
        }
    }

    public class ConstantBlur : BlurFunction
    {
        float PSFRadius { get; set; }

        public ConstantBlur(int psfRadius)
        {
            PSFRadius = psfRadius;
        }

        public float GetPSFRadius(int x, int y)
        {
            return PSFRadius;
        }
    }
}