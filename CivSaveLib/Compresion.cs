using ICSharpCode.SharpZipLib.GZip;
using System.IO;

namespace CivSaveLib
{
    public class Compresion
    {
        public static byte[] CompressBytes(byte[] uncompressedBytes)
        {
            byte[] result = null;

            using (var memStream = new MemoryStream())
            {
                using (var zipStream = new GZipOutputStream(memStream))
                {
                    zipStream.SetLevel(3);

                    zipStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
                    zipStream.Finish();

                    result = memStream.ToArray();

                    zipStream.Close();
                    memStream.Close();
                }
            }

            return result;
        }

        public static byte[] DecompressBytes(byte[] compressedBytes)
        {
            byte[] result = null;

            using (var sourceStream = new MemoryStream(compressedBytes))
            {
                using (var zipStream = new GZipInputStream(sourceStream))
                {
                    using (var destStream = new MemoryStream())
                    {
                        zipStream.CopyTo(destStream);
                        zipStream.Close();

                        result = destStream.ToArray();
                        destStream.Close();
                    }
                }
            }

            return result;
        }
    }
}