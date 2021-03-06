﻿namespace BokehLab.Demo.ComplexLensTracing2d
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;
    using BokehLab.Math;
    using BokehLab.RayTracing;
    using BokehLab.RayTracing.Lens;
    using OpenTK;

    public partial class BiconvexLensForm : Form
    {
        private BiconvexLens biconvexLens;
        private ComplexLens complexLens;
        private Ray incomingRay;
        private Ray outgoingRay;
        private Vector3d backLensPos;
        private Ray complexOutgoingRay;

        bool initialized = false;

        public BiconvexLensForm()
        {
            InitializeComponent();
            biconvexLens = new BiconvexLens()
            {
                CurvatureRadius = 150,
                ApertureRadius = 100,
                RefractiveIndex = Materials.Fixed.GLASS_CROWN_BK7
            };
            complexLens = ComplexLens.CreateBiconvexLens(150, 100, 0);
            double directionPhi = Math.PI;
            incomingRay = new Ray(new Vector3d(70, 0, 150), new Vector3d(Math.Sin(directionPhi), 0, Math.Cos(directionPhi)));

            rayDirectionPhiNumeric.Value = (decimal)directionPhi;
            curvatureRadiusNumeric.Value = (decimal)biconvexLens.CurvatureRadius;
            apertureRadiusNumeric.Value = (decimal)biconvexLens.ApertureRadius;
            FillVectorToControls(incomingRay.Origin, rayOriginXNumeric, rayOriginYNumeric, rayOriginZNumeric);
            initialized = true;
            Recompute();
        }

        private void Recompute()
        {
            if (!initialized)
            {
                return;
            }

            incomingRay.Origin = GetVectorFromControls(rayOriginXNumeric, rayOriginYNumeric, rayOriginZNumeric);
            double directionPhi = (double)rayDirectionPhiNumeric.Value;
            incomingRay.Direction = new Vector3d(Math.Sin(directionPhi), 0, Math.Cos(directionPhi));

            Intersection backInt = biconvexLens.Intersect(incomingRay);
            if (backInt != null)
            {
                outgoingRay = biconvexLens.Transfer(incomingRay.Origin, backInt.Position);
                backLensPos = backInt.Position;
            }
            else
            {
                outgoingRay = null;
                backLensPos = Vector3d.Zero;
            }
            backInt = complexLens.Intersect(incomingRay);
            if (backInt != null)
            {
                complexOutgoingRay = complexLens.Transfer(incomingRay.Origin, backInt.Position);
            }
            else
            {
                complexOutgoingRay = null;
            }
            drawingPanel.Invalidate();
        }

        private void drawingPanel_Paint(object sender, PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            float panelHalfWidth = e.ClipRectangle.Width / 2.0f;
            float panelHalfHeight = e.ClipRectangle.Height / 2.0f;
            g.TranslateTransform(panelHalfWidth, panelHalfHeight);
            g.ScaleTransform(1.0f, -1.0f);

            // draw X axis
            g.DrawLine(Pens.Black, -panelHalfWidth, 0, panelHalfWidth, 0);

            // draw Y axis
            g.DrawLine(Pens.Black, 0, -panelHalfWidth, 0, panelHalfWidth);

            //g.ScaleTransform(-1.0f, 1.0f);
            //g.TranslateTransform(200.0f, 0.0f);

            PaintScene(g);
        }

        private void PaintScene(Graphics g)
        {
            // draw a circlular lens
            //DrawCircle(g, Pens.Blue, new Point(), (float)Bench.Elements.Radius);

            //g.TranslateTransform((float)-Bench.LensCenter, 0.0f);

            // draw spheres
            DrawCircle(g, Pens.Blue, Vector3dToPoint(biconvexLens.backSurface.Center), (float)biconvexLens.backSurface.Radius);
            DrawCircle(g, Pens.Purple, Vector3dToPoint(biconvexLens.frontSurface.Center), (float)biconvexLens.frontSurface.Radius);

            // draw incoming ray
            if (incomingRay.Direction != Vector3d.Zero)
            {
                Point origin = Vector3dToPoint(incomingRay.Origin);
                Point target;
                if (outgoingRay != null)
                {
                    target = Vector3dToPoint(backLensPos);
                }
                else
                {
                    target = Vector3dToPoint(incomingRay.Origin + 1000 * Vector3d.Normalize(incomingRay.Direction));
                }
                g.DrawLine(Pens.Green, origin, target);
            }

            if (backLensPos != Vector3d.Zero)
            {
                FillSquare(g, Brushes.Red, Vector3dToPoint(backLensPos), 3);
            }

            // draw outgoing ray from biconvex lens
            if ((outgoingRay != null) && (outgoingRay.Direction != Vector3d.Zero))
            {
                Point origin = Vector3dToPoint(outgoingRay.Origin);
                Point target = Vector3dToPoint(outgoingRay.Origin + 1000 * Vector3d.Normalize(outgoingRay.Direction));
                g.DrawLine(Pens.Red, origin, target);
                g.DrawLine(Pens.Green, Vector3dToPoint(backLensPos), origin);

                // draw normal
                g.DrawLine(Pens.Purple, origin, Vector3dToPoint(outgoingRay.Origin + 20 * -biconvexLens.frontSurface.GetNormal(outgoingRay.Origin)));
            }

            // draw outgoing ray from complex lens
            if ((complexOutgoingRay != null) && (complexOutgoingRay.Direction != Vector3d.Zero))
            {
                Point origin = Vector3dToPoint(complexOutgoingRay.Origin);
                Point target = Vector3dToPoint(complexOutgoingRay.Origin + 1000 * Vector3d.Normalize(complexOutgoingRay.Direction));
                g.DrawLine(Pens.Brown, origin, target);
                g.DrawLine(Pens.DarkGreen, Vector3dToPoint(backLensPos), origin);

                // draw normal
                g.DrawLine(Pens.Purple, origin, Vector3dToPoint(complexOutgoingRay.Origin + 20 * -complexLens.ElementSurfaces.Last().SurfaceNormalField.GetNormal(complexOutgoingRay.Origin)));

                // draw normal
                g.DrawLine(Pens.Purple, Vector3dToPoint(backLensPos), Vector3dToPoint(backLensPos + 20 * -complexLens.ElementSurfaces.First().SurfaceNormalField.GetNormal(backLensPos)));
            }
        }

        private Point Vector3dToPoint(Vector3d vector)
        {
            return new Point((int)vector.Z, (int)vector.X);
        }

        private void DrawCircle(Graphics g, Pen pen, Point center, float radius)
        {
            g.DrawEllipse(pen, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
        }

        private void FillSquare(Graphics g, Brush brush, Point center, float radius)
        {
            g.FillRectangle(brush, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
        }

        private Vector3d GetVectorFromControls(
           NumericUpDown xNumeric,
           NumericUpDown yNumeric,
           NumericUpDown zNumeric)
        {
            return new Vector3d(
                (double)xNumeric.Value,
                (double)yNumeric.Value,
                (double)zNumeric.Value);
        }

        private void FillVectorToControls(Vector3d vector,
          NumericUpDown xNumeric,
          NumericUpDown yNumeric,
          NumericUpDown zNumeric)
        {
            xNumeric.Value = (decimal)vector.X;
            yNumeric.Value = (decimal)vector.Y;
            zNumeric.Value = (decimal)vector.Z;
        }

        private void SceneControlsValueChanged(object sender, EventArgs e)
        {
            Recompute();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Recompute();
        }

        private void curvatureRadiusNumeric_ValueChanged(object sender, EventArgs e)
        {
            biconvexLens.CurvatureRadius = (double)curvatureRadiusNumeric.Value;
            Recompute();
        }

        private void apertureRadiusNumeric_ValueChanged(object sender, EventArgs e)
        {
            biconvexLens.ApertureRadius = (double)apertureRadiusNumeric.Value;
            Recompute();
        }

        private void BiconvexLensForm_Resize(object sender, EventArgs e)
        {
            Recompute();
        }
    }
}
