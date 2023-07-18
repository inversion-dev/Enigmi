using Domain.ValueObjects;

namespace Enigmi.Infrastructure.Services.ImageProcessing;

public interface IImageProcessingService
{
    byte[] ResizeImage(byte[] imageBytes, int maxHeight, int maxWidth);
    byte[] CropImage(byte[] imageBytes, int startX, int startY, int width, int height);
    (int height, int width) GetImageDimensions(byte[] imageBytes);
    byte[] NormalizeImage(byte[] originalBytes, int maxLength);
}