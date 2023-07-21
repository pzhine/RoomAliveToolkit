namespace RealSenseServer
{
    public class ConvertRGB24
    {
        public static byte[] To8BitGrayscale(byte[] inputImageBytes, int width, int height)
        {
            int inputStride = width * 3; // 3 bytes per pixel for 24-bit color (RGB)
            int outputStride = width;    // 1 byte per pixel for 8-bit grayscale
            byte[] outputImageBytes = new byte[width * height];

            for (int y = 0; y < height; y++)
            {
                int inputOffset = y * inputStride;
                int outputOffset = y * outputStride;

                for (int x = 0; x < width; x++)
                {
                    // Convert RGB pixel to grayscale
                    byte r = inputImageBytes[inputOffset + x * 3];
                    byte g = inputImageBytes[inputOffset + x * 3 + 1];
                    byte b = inputImageBytes[inputOffset + x * 3 + 2];
                    byte grayValue = (byte)(0.299 * r + 0.587 * g + 0.114 * b);

                    outputImageBytes[outputOffset + x] = grayValue;
                }
            }

            return outputImageBytes;
        }

        public static byte[] ToYUY2(byte[] inputImageBytes, int width, int height)
        {
            int inputStride = width * 3; // 3 bytes per pixel for 24-bit color (RGB)
            int outputStride = width * 2; // 2 bytes per pixel for YUY2
            byte[] outputImageBytes = new byte[width * height * 2];

            for (int y = 0; y < height; y++)
            {
                int inputOffset = y * inputStride;
                int outputOffset = y * outputStride;

                for (int x = 0; x < width; x += 2)
                {
                    // Convert two consecutive RGB pixels to YUV
                    byte r1 = inputImageBytes[inputOffset + x * 3];
                    byte g1 = inputImageBytes[inputOffset + x * 3 + 1];
                    byte b1 = inputImageBytes[inputOffset + x * 3 + 2];

                    byte r2 = inputImageBytes[inputOffset + (x + 1) * 3];
                    byte g2 = inputImageBytes[inputOffset + (x + 1) * 3 + 1];
                    byte b2 = inputImageBytes[inputOffset + (x + 1) * 3 + 2];

                    byte y1 = (byte)(0.299 * r1 + 0.587 * g1 + 0.114 * b1);
                    byte y2 = (byte)(0.299 * r2 + 0.587 * g2 + 0.114 * b2);

                    byte u = (byte)(-0.14713 * r1 - 0.28886 * g1 + 0.436 * b1 + 128);
                    byte v = (byte)(0.615 * r1 - 0.51499 * g1 - 0.10001 * b1 + 128);

                    // Pack the Y, U, and Y, V values into YUY2 format
                    outputImageBytes[outputOffset] = y1;
                    outputImageBytes[outputOffset + 1] = u;
                    outputImageBytes[outputOffset + 2] = y2;
                    outputImageBytes[outputOffset + 3] = v;

                    outputOffset += 4; // Move to the next YUY2 pixel
                }
            }

            return outputImageBytes;
        }

        public static byte[] ToBGRA(byte[] inputImageBytes, int width, int height, byte alphaValue)
        {
            int inputStride = width * 3; // 3 bytes per pixel for 24-bit RGB
            int outputStride = width * 4; // 4 bytes per pixel for BGRA
            byte[] outputImageBytes = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                int inputOffset = y * inputStride;
                int outputOffset = y * outputStride;

                for (int x = 0; x < width; x++)
                {
                    // Extract RGB values from the input image
                    byte r = inputImageBytes[inputOffset + x * 3];
                    byte g = inputImageBytes[inputOffset + x * 3 + 1];
                    byte b = inputImageBytes[inputOffset + x * 3 + 2];

                    // Set BGRA values in the output image
                    outputImageBytes[outputOffset + x * 4] = b; // Blue
                    outputImageBytes[outputOffset + x * 4 + 1] = g; // Green
                    outputImageBytes[outputOffset + x * 4 + 2] = r; // Red
                    outputImageBytes[outputOffset + x * 4 + 3] = alphaValue; // Alpha
                }
            }

            return outputImageBytes;
        }
    }
}
