namespace FrameFlux.Converters
{
    internal class Tensors
    {
        static float[] ConvertToTensorFormat(byte[] pixels, int width, int height)
        {
            // pixels -> RGBA (4 bytes), need normalized RGBA float 0..1
            int pixelCount = width * height;
            float[] tensorData = new float[3 * pixelCount]; // 3 channels, ignore alpha channel

            for (int i = 0; i < pixelCount; i++)
            {
                int baseIdx = i * 4;
                tensorData[i] = pixels[baseIdx + 0] / 255f;         // R
                tensorData[i + pixelCount] = pixels[baseIdx + 1] / 255f; // G
                tensorData[i + 2 * pixelCount] = pixels[baseIdx + 2] / 255f; // B
            }

            return tensorData;
        }

    }
}
