using Domain.ValueObjects;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace Enigmi.Infrastructure.Services.ImageProcessing;
public class ImageProcessingService : IImageProcessingService
{
    public byte[] CropImage(byte[] imageBytes, int startX, int startY, int width, int height)
    {
        using Image image = Image.Load(imageBytes, out IImageFormat format);
        using var outStream = new MemoryStream();
        var clone = image.Clone(i => i.Crop(new Rectangle(startX, startY, width, height)));
        clone.Save(outStream, format);

        return outStream.ToArray();
    }

    public (int height, int width) GetImageDimensions(byte[] imageBytes)
    {
        using Image image = Image.Load(imageBytes);
        return (image.Height, image.Width);
    }

    public byte[] NormalizeImage(byte[] originalBytes, int maxLength)
    {
        using Image image = Image.Load(originalBytes, out IImageFormat format);
        var longestSide = Math.Max(image.Width, image.Height);
        
        if (longestSide > maxLength)
        {
            var newWidth = image.Width == longestSide ? maxLength : 0;
            var newHeight = image.Height == longestSide ? maxLength : 0;

            //passing 0 to either the width/height will keep aspect ratio
            image.Mutate(i => i.Resize(new Size(newWidth, newHeight)));
        }

        image.Mutate(i => i.Pad(maxLength, maxLength, Color.White));

        using var outStream = new MemoryStream();
        image.Save(outStream, format);
        return outStream.ToArray();
    }

    public byte[] ResizeImage(byte[] imageMemoryStream, int maxHeight, int maxWidth)
    {
        using Image image = Image.Load(imageMemoryStream, out IImageFormat format);

        var ratio = Math.Min(Convert.ToDecimal(maxWidth) / Convert.ToDecimal(image.Width), Convert.ToDecimal(maxHeight) / Convert.ToDecimal(image.Height));
        var resizeWidth = Convert.ToInt32(image.Width * ratio);
        var resizeHeight = Convert.ToInt32(image.Height * ratio);

        image.Mutate(x => x.Resize(resizeWidth, resizeHeight, KnownResamplers.Lanczos3));

        using var resizedStream = new MemoryStream();
        image.Save(resizedStream, format);

        return resizedStream.ToArray();
    }
}