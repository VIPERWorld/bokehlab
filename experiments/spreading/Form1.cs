﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Drawing.Imaging;
using libpfm;

// TODO:
// - add controls for selecting the blur source (depth map, procedure, constant, ...)
// - add controls for selecting the image to show (input, depth-map, output)

namespace spreading
{
    public partial class Form1 : Form
    {
        protected Bitmap inputLdrImage = null;
        protected Bitmap outputLdrImage = null;

        protected PFMImage inputHdrImage = null;
        protected PFMImage outputHdrImage = null;

        protected PFMImage depthMap = null;

        public Form1()
        {
            InitializeComponent();
            blurRadiusNumeric.Value = RectangleSpreadingFilter.DEFAULT_BLUR_RADIUS;
            imageTypeComboBox.SelectedIndex = 0;
        }

        private void buttonLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Title = "Open Image File";
            ofd.Filter = "PNG Files|*.png" +
                "|PFM Files|*.pfm" +
                "|Bitmap Files|*.bmp" +
                "|Gif Files|*.gif" +
                "|JPEG Files|*.jpg" +
                "|TIFF Files|*.tif" +
                "|All Image types|*.png;*.pfm;*.bmp;*.gif;*.jpg;*.tif";

            ofd.FilterIndex = 7;
            ofd.FileName = "";
            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            if (ofd.FileName.EndsWith(".pfm"))
            {
                inputHdrImage = PFMImage.LoadImage(ofd.FileName);
                inputLdrImage = inputHdrImage.ToLdr();
            }
            else
            {
                inputLdrImage = (Bitmap)Image.FromFile(ofd.FileName);
                inputHdrImage = PFMImage.FromLdr(inputLdrImage);
            }
            pictureBox1.Image = inputLdrImage;

            outputHdrImage = null;
            outputLdrImage = null;

            imageSizeLabel.Text = String.Format("{0}x{1}", inputHdrImage.Width, inputHdrImage.Height);
        }

        private void loadDepthMapButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Title = "Open depth map";
            ofd.Filter = "PNG Files|*.png" +
                "|PFM Files|*.pfm" +
                "|Bitmap Files|*.bmp" +
                "|Gif Files|*.gif" +
                "|JPEG Files|*.jpg" +
                "|TIFF Files|*.tif" +
                "|All Image types|*.png;*.pfm;*.bmp;*.gif;*.jpg;*.tif";

            ofd.FilterIndex = 7;
            ofd.FileName = "";
            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            if (ofd.FileName.EndsWith(".pfm"))
            {
                depthMap = PFMImage.LoadImage(ofd.FileName);
            }
            else
            {
                depthMap = PFMImage.FromLdr((Bitmap)Image.FromFile(ofd.FileName));
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            PFMImage hdrImageToSave = outputHdrImage;
            Bitmap ldrImageToSave = outputLdrImage;
            if ((outputLdrImage == null) || (outputHdrImage == null))
            {
                hdrImageToSave = inputHdrImage;
                ldrImageToSave = inputLdrImage;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save output file";
            sfd.Filter = "JPEG Files|*.jpg|PNG Files|*.png|PFM Files|*.pfm";
            sfd.AddExtension = true;
            sfd.FileName = "";
            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            if (sfd.FileName.EndsWith(".pfm"))
            {
                hdrImageToSave.SaveImage(sfd.FileName);
            }
            else if (sfd.FileName.EndsWith(".png"))
            {
                ldrImageToSave.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
            }
            else if (sfd.FileName.EndsWith(".jpg"))
            {
                ldrImageToSave.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        private void buttonRecode_Click(object sender, EventArgs e)
        {
            filterImage();
        }

        private void blurRadiusNumeric_ValueChanged(object sender, EventArgs e)
        {
            filterImage();
        }

        private void filterImage()
        {
            if ((inputLdrImage == null) || (inputLdrImage == null)) return;
            Cursor.Current = Cursors.WaitCursor;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            RectangleSpreadingFilter filter = new RectangleSpreadingFilter()
            {
                MaxBlurRadius = (int)blurRadiusNumeric.Value
            };
            try
            {
                outputHdrImage = filter.SpreadPSF(inputHdrImage, outputHdrImage, depthMap);
                outputLdrImage = outputHdrImage.ToLdr();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace, "Error");
            }

            sw.Stop();
            labelElapsed.Text = String.Format("Elapsed time: {0:f}s", 1.0e-3 * sw.ElapsedMilliseconds);

            if (outputLdrImage != null)
            {
                pictureBox1.Image = outputLdrImage;
            }
            else
            {
                pictureBox1.Image = null;
                outputLdrImage = null;
            }

            Cursor.Current = Cursors.Default;
        }

        private void imageTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // TODO: use enum
            switch (imageTypeComboBox.SelectedItem.ToString())
            {
                case "Original":
                    pictureBox1.Image = inputLdrImage;
                    break;
                case "Filtered":
                    pictureBox1.Image = outputLdrImage;
                    break;
                case "Depth map":
                    pictureBox1.Image = (depthMap != null) ? depthMap.ToLdr() : null;
                    break;
            }
        }

        private void imageSizeOrigButton_Click(object sender, EventArgs e)
        {
            pictureBox1.SizeMode = PictureBoxSizeMode.AutoSize;
            pictureBox1.Dock = DockStyle.None;
        }

        private void imageSizeStretchButton_Click(object sender, EventArgs e)
        {
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.Dock = DockStyle.Fill;
        }
    }
}