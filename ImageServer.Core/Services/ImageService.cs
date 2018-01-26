using System;
using System.Collections.Generic;
using System.Linq;
using ImageMagick;
using ImageServer.Core.Model;

namespace ImageServer.Core.Services
{
    public class ImageService : IImageService
    {
        public byte[] GetImageAsBytes(int requestWidth, int requestHeight, int quality, byte[] bytes, string options, out string mimeType, CustomRatio ratio = null)
        {
            MagickImageInfo originalImageInfo = new MagickImageInfo(bytes);

            if (originalImageInfo.Format == MagickFormat.Gif || originalImageInfo.Format == MagickFormat.Gif87)
            {
                mimeType = "image/gif";
                return ProcessGif(requestWidth, requestHeight, quality, bytes, options);
            }

            return ProcessImage(requestWidth, requestHeight, quality, bytes, options, ratio, out mimeType);
        }

        private static byte[] ProcessGif(int requestWidth, int requestHeight, int quality, byte[] bytes, string options)
        {
            using (MagickImageCollection collection = new MagickImageCollection(bytes))
            {
                collection.Coalesce();

                // the height will be calculated with the aspect ratio.
                foreach (var magickImage in collection)
                {
                    var img = (MagickImage)magickImage;
                    ResizeSingleImage(requestWidth, requestHeight, quality, options, img);
                }

                collection.RePage();

                collection.Optimize();
                collection.OptimizeTransparency();

                // Save the result
                return collection.ToByteArray();
            }
        }

        private static byte[] ProcessImage(int requestWidth, int requestHeight,
            int quality, byte[] bytes, string options, CustomRatio ratio, out string mimeType)
        {
            if (ratio != null)
            {
                using (MagickImage image = new MagickImage(bytes))
                {
                    CropSingleImage(ratio, image);

                    bytes = image.ToByteArray();
                }
            }

            using (MagickImage image = new MagickImage(bytes))
            {
                ResizeSingleImage(requestWidth, requestHeight, quality, options, image);

                // return the result
                if (image.HasAlpha)
                {
                    mimeType = "image/png";
                    bytes = image.ToByteArray(MagickFormat.Png);
                }
                else
                {
                    mimeType = "image/jpeg";
                    bytes = image.ToByteArray(MagickFormat.Pjpeg);
                }
            }

            return bytes;
        }

        private static void CropSingleImage(CustomRatio ratio, MagickImage image)
        {
            var cropWidth = Math.Abs(ratio.X2 - ratio.X1);
            var cropHeight = Math.Abs(ratio.Y2 - ratio.Y1);

            if (image.BaseWidth == cropWidth && image.BaseHeight == cropHeight) //requested image is same size
            {
                return;
            }

            var cropSize = new MagickGeometry(cropWidth, cropHeight)
            {
                IgnoreAspectRatio = false, //keep aspect ratio!
                FillArea = false,
                X = ratio.X1,
                Y = ratio.Y1
            };
            image.Crop(cropSize);

        }

        private static void ResizeSingleImage(int requestWidth, int requestHeight, int quality, string options, MagickImage image)
        {
            if (image.BaseWidth == requestWidth && image.BaseHeight == requestHeight) //requested image is same size
            {
                return;
            }
            if (requestWidth == 0 && requestHeight == 0) //requested image is same size
            {
                return;
            }

            if (options.Contains("f") || options.Contains("t")) //scale with aspect of image
            {
                var size = new MagickGeometry(requestWidth, requestHeight);
                image.Thumbnail(size);
            }
            else if (requestWidth == 0 || requestHeight == 0) //scale with aspect of image
            {
                var size = new MagickGeometry(requestWidth, requestHeight);
                image.Thumbnail(size);
            }
            else // This will resize the image to a fixed size without maintaining the aspect ratio.
            {
                var size = new MagickGeometry(requestWidth, requestHeight)
                {
                    IgnoreAspectRatio = false, //keep aspect ratio!
                    FillArea = true
                };
                image.Resize(size);
                image.Crop(size, Gravity.Center);
            }

            image.Quality = quality;

            if (options.Contains("g")) //grayscale
                image.Grayscale(PixelIntensityMethod.Average);

            image.Strip();
        }

        public string GetVersion()
        {
            return MagickNET.Version;
        }

        public string GetFeatures()
        {
            return MagickNET.Features.Trim();
        }

        public List<MagickFormatInfo> GetSupportedFormats()
        {
            return MagickNET.SupportedFormats.ToList();
        }
    }
}