using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace RibbonStudio.Backend
{
    public static class IconImages
    {
        public static ImageAsset Resize(ImageAsset image, int size)
        {
            if (image == null)
                return null;
            try
            {
                if (image.mediaType != "image/png" && image.mediaType != "image/jpeg")
                    throw new Exception();
                if (image.base64 == null || image.base64.Length > 16 * 1024 * 1024)
                    throw new Exception();
                byte[] bytes = Convert.FromBase64String(image.base64);
                if (bytes.Length > 12 * 1024 * 1024)
                    throw new Exception();
                using (var input = new MemoryStream(bytes))
                using (var source = Image.FromStream(input, true, true))
                {
                    if (source.Width > 8192 || source.Height > 8192 || source.Width < 1 || source.Height < 1 || source.GetFrameCount(new FrameDimension(source.FrameDimensionsList[0])) > 1)
                        throw new Exception();
                    if ((source.RawFormat.Guid == ImageFormat.Png.Guid ? "image/png" : source.RawFormat.Guid == ImageFormat.Jpeg.Guid ? "image/jpeg" : "") != image.mediaType)
                        throw new Exception();
                    using (var target = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                    {
                        using (var graphics = Graphics.FromImage(target))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            double ratio = Math.Min((double)size / source.Width, (double)size / source.Height);
                            int w = Math.Max(1, (int)Math.Round(source.Width * ratio)), h = Math.Max(1, (int)Math.Round(source.Height * ratio));
                            graphics.DrawImage(source, (size - w) / 2, (size - h) / 2, w, h);
                        }
                        using (var output = new MemoryStream())
                        {
                            target.Save(output, ImageFormat.Png);
                            return new ImageAsset { mediaType = "image/png", base64 = Convert.ToBase64String(output.ToArray()) };
                        }
                    }
                }
            }
            catch { throw new ApiError(422, "请选择有效的单帧 PNG/JPEG 图片（最大 12 MiB、8192×8192）"); }
        }
    }
}
