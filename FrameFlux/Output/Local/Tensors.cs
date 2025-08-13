namespace FrameFlux.Output.Local
{
    internal class Tensors
    {
        private float[]? _tensorBuffer;



        // Convert byte array pixels to a NCHW tensor format ready for ONNX inference. 

        public unsafe float[] ToTensors(byte[] pixels, int width, int height)
        {
            int pixelCount = width * height;

            // Now reuse the buffer if it exists and has the correct size
            if (_tensorBuffer == null || _tensorBuffer.Length != 3 * pixelCount)
                _tensorBuffer = new float[3 * pixelCount];

            fixed (byte* pPixels = pixels)
            fixed (float* pTensor = _tensorBuffer)
            {
                byte* src = pPixels;
                float* dstR = pTensor;
                float* dstG = pTensor + pixelCount;
                float* dstB = pTensor + 2 * pixelCount;

                for (int i = 0; i < pixelCount; i++)
                {
                    *dstR++ = *src++ / 255f; // R
                    *dstG++ = *src++ / 255f; // G
                    *dstB++ = *src++ / 255f; // B
                    src++;                    // Alphe layer, uneeded for RGB so we skip it 
                }
            }

            return _tensorBuffer;
        }
    }


}
