﻿using BokehLab.FloatMap;

// TODO:
// - shouldn't the table be of the same size as the input image?
//   - now it seems it has to be larger by 1px
// - should the spreading table and normalization channel be squashed into
//   one image for better locality?
// - could alpha channel be used as a normalization channel?
// - implement better PSFs:
//   - perimeter spreading
//   - polynomial spreading

namespace BokehLab.Spreading
{
    public abstract class AbstractSpreadingFilter : ISpreadingFilter
    {
        public BlurMap Blur;

        public FloatMapImage FilterImage(FloatMapImage inputImage, FloatMapImage outputImage)
        {
            if (inputImage == null) return null;

            uint width = inputImage.Width;
            uint height = inputImage.Height;

            if (width < 1 || height < 1) return null;

            if (outputImage == null)
            {
                outputImage = new FloatMapImage(width, height, inputImage.PixelFormat);
            }

            FloatMapImage spreadingTable = new FloatMapImage(width + 1, height + 1, inputImage.PixelFormat);
            FloatMapImage normalizationTable = new FloatMapImage(width + 1, height + 1, PixelFormat.Greyscale);

            // initialize the spreading table to 0.0 and the normalization channel to 1.0
            InitializeTables(spreadingTable, normalizationTable);

            // phase 1: distribute corners into the table
            Spread(inputImage, spreadingTable, normalizationTable);

            // phase 2: accumulate the corners into rectangles
            Integrate(spreadingTable, normalizationTable);

            Normalize(spreadingTable, normalizationTable, outputImage);

            spreadingTable.Dispose();
            normalizationTable.Dispose();

            return outputImage;
        }

        private static void InitializeTables(FloatMapImage spreadingTable, FloatMapImage normalizationTable)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Reset();
            //sw.Start();

            uint bands = spreadingTable.ChannelsCount;
            // NOTE: the following properties are stored as an optimization
            float[, ,] spreadingImage = spreadingTable.Image;
            float[, ,] normalizationImage = normalizationTable.Image;
            int tableWidth = (int)spreadingTable.Width;
            int tableHeight = (int)spreadingTable.Height;

            for (int x = 0; x < tableWidth; x++)
            {
                for (int y = 0; y < tableHeight; y++)
                {
                    for (int band = 0; band < bands; band++)
                    {
                        spreadingImage[x, y, band] = 0;
                    }
                    normalizationImage[x, y, 0] = 0;
                }
            }
            //Console.WriteLine("Initializing spreading and normalization tables: {0} ms", sw.ElapsedMilliseconds);
        }

        private void Spread(FloatMapImage inputImage, FloatMapImage spreadingTable, FloatMapImage normalizationTable)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Reset();
            //sw.Start();

            uint bands = inputImage.ChannelsCount;
            uint width = inputImage.Width;
            uint height = inputImage.Height;

            float[, ,] origImage = inputImage.Image;
            float[, ,] spreadingImage = spreadingTable.Image;
            float[, ,] normalizationImage = normalizationTable.Image;

            int tableWidth = (int)spreadingTable.Width;
            int tableHeight = (int)spreadingTable.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float radius = Blur.GetPSFRadius(x, y);

                    // spread PSFs of a non-integer radius using two weighted
                    // PSFs of integer size close to the original radius
                    int smallerRadius = (int)radius;
                    int biggerRadius = smallerRadius + 1;
                    float weight = biggerRadius - radius;

                    //SpreadPSF(x, y, smallerRadius, 1.0f, origImage, spreadingImage, normalizationImage, tableWidth, tableHeight, bands);

                    SpreadPSF(x, y, smallerRadius, weight, origImage, spreadingImage, normalizationImage, tableWidth, tableHeight, bands);
                    SpreadPSF(x, y, biggerRadius, 1 - weight, origImage, spreadingImage, normalizationImage, tableWidth, tableHeight, bands);
                }
            }
            //Console.WriteLine("Phase 1, spreading: {0} ms", sw.ElapsedMilliseconds);
        }

        protected abstract void SpreadPSF(int x, int y, int radius, float weight, float[, ,] origImage, float[, ,] spreadingImage, float[, ,] normalizationImage, int tableWidth, int tableHeight, uint bands);

        protected abstract void Integrate(FloatMapImage spreadingTable, FloatMapImage normalizationTable);

        protected static void IntegrateVertically(FloatMapImage spreadingTable, FloatMapImage normalizationTable)
        {
            uint bands = spreadingTable.ChannelsCount;
            float[, ,] spreadingImage = spreadingTable.Image;
            float[, ,] normalizationImage = normalizationTable.Image;
            int tableWidth = (int)spreadingTable.Width;
            int tableHeight = (int)spreadingTable.Height;

            for (int x = 0; x < tableWidth; x++)
            {
                for (int y = 1; y < tableHeight; y++)
                {
                    for (int band = 0; band < bands; band++)
                    {
                        spreadingImage[x, y, band] += spreadingImage[x, y - 1, band];
                    }
                    normalizationImage[x, y, 0] += normalizationImage[x, y - 1, 0];
                }
            }
        }

        protected static void IntegrateHorizontally(FloatMapImage spreadingTable, FloatMapImage normalizationTable)
        {
            uint bands = spreadingTable.ChannelsCount;
            float[, ,] spreadingImage = spreadingTable.Image;
            float[, ,] normalizationImage = normalizationTable.Image;
            int tableWidth = (int)spreadingTable.Width;
            int tableHeight = (int)spreadingTable.Height;

            for (int y = 0; y < tableHeight; y++)
            {
                for (int x = 1; x < tableWidth; x++)
                {
                    for (int band = 0; band < bands; band++)
                    {
                        spreadingImage[x, y, band] += spreadingImage[x - 1, y, band];
                    }
                    normalizationImage[x, y, 0] += normalizationImage[x - 1, y, 0];
                }
            }
        }

        private static void Normalize(FloatMapImage spreadingTable, FloatMapImage normalizationTable, FloatMapImage outputImage)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Reset();
            //sw.Start();

            uint bands = outputImage.ChannelsCount;
            float[, ,] image = outputImage.Image;
            float[, ,] spreadingImage = spreadingTable.Image;
            float[, ,] normalizationImage = normalizationTable.Image;
            int width = (int)outputImage.Width;
            int height = (int)outputImage.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float normalization = 1 / normalizationImage[x, y, 0];
                    for (int band = 0; band < bands; band++)
                    {
                        image[x, y, band] = spreadingImage[x, y, band] * normalization;
                    }
                }
            }
            //Console.WriteLine("Normalizing to output image: {0} ms", sw.ElapsedMilliseconds);
        }
    }
}