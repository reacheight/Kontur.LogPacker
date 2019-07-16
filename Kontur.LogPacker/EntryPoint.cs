using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Kontur.LogPacker
{
    internal static class EntryPoint
    {
        public static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                var (inputFile, outputFile) = (args[0], args[1]);
                
                if (File.Exists(inputFile))
                {
                    Compress(inputFile, outputFile);
                    return;
                }
            }

            if (args.Length == 3 && args[0] == "-d")
            {
                var (inputFile, outputFile) = (args[1], args[2]);
                
                if (File.Exists(inputFile))
                {
                    Decompress(inputFile, outputFile);
                    return;
                }
            }
            
            ShowUsage();
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine($"{AppDomain.CurrentDomain.FriendlyName} [-d] <inputFile> <outputFile>");
            Console.WriteLine("\t-d flag turns on the decompression mode");
            Console.WriteLine();
        }

        private static void Compress(string inputFile, string outputFile)
        {
            using (var inputStream = File.OpenRead(inputFile))
            using (var tmpStream = File.OpenWrite("tmp"))
            {
                new LogCompressor().Compress(inputStream, tmpStream);
            }
            
            using (var tmpStream = File.OpenRead("tmp"))
            using (var outputStream = File.OpenWrite(outputFile))
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal, true))
                    tmpStream.CopyTo(gzipStream);
            }
            
            File.Delete("tmp");
        }

        private static void Decompress(string inputFile, string outputFile)
        {
            using (var inputStream = File.OpenRead(inputFile))
            using (var tmpStream = File.OpenWrite("tmp"))
            {
                using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, true))
                    gzipStream.CopyTo(tmpStream);
            }
            
            using (var tmpStream = File.OpenRead("tmp"))
            using (var outputStream = File.OpenWrite(outputFile))
            {
                new LogCompressor().Decompress(tmpStream, outputStream);
            }
            
            File.Delete("tmp");
        }
    }
}