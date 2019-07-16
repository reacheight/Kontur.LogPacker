using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Kontur.LogPacker
{
    public class LogCompressor
    {
        private const int MaxLineLength = 2000;
        private const int MaxTimeSpan = 999;
        private const int MaxNumberSpan = 999;
        
        public void Compress(Stream inputStream, Stream outputStream)
        {
            if (IsBinary(inputStream))
            {
                inputStream.CopyTo(outputStream);
                return;
            }

            using (var writer = new StreamWriter(outputStream)) 
            using (var reader = new StreamReader(inputStream))
            {
                if (IsCrlf(inputStream))
                {
                    writer.NewLine = "\r\n";
                }
                
                if (!TryParseLog(reader.ReadLine(), out var tokens))
                {
                    inputStream.Position = 0;
                    inputStream.CopyTo(outputStream);
                    return;
                }

                var (currentDate, currentNumber, firstType, firstMessage) = tokens;
                writer.WriteLine($"{currentDate:yyyyMMddHHmmssfff} {currentNumber} {CompressLogType(firstType)} {firstMessage}");
                
                while (!reader.EndOfStream)
                {
                    if (!TryParseLog(reader.ReadLine(), out var log))
                    {
                        writer.WriteLine(log.message);
                        continue;
                    }
                    
                    var (date, number, type, message) = log;
                    var timeSpan = (long) (date - currentDate).TotalMilliseconds;
                    var numberSpan = number - currentNumber;
                    string dateString;

                    (dateString, currentDate) = timeSpan > MaxTimeSpan
                        ? (date.ToString("yyyyMMddHHmmssfff"), date)
                        : (timeSpan.ToString(), currentDate);

                    (number, currentNumber) = numberSpan > MaxNumberSpan
                        ? (number, number)
                        : (numberSpan, currentNumber);
                    
                    writer.WriteLine($"{dateString} {number} {CompressLogType(type)} {message}");
                }
            }
        }

        public void Decompress(Stream inputStream, Stream outputStream)
        {
            if (IsBinary(inputStream))
            {
                inputStream.CopyTo(outputStream);
                return;
            }
            
            using (var writer = new StreamWriter(outputStream))
            using (var reader = new StreamReader(inputStream))
            {
                if (IsCrlf(inputStream))
                {
                    writer.NewLine = "\r\n";
                }
                
                if (!TryParseCompressedLog(reader.ReadLine(), DateTime.MinValue, out var tokens))
                {
                    inputStream.Position = 0;
                    inputStream.CopyTo(outputStream);
                    return;
                }

                var (currentDate, currentNumber, firstType, firstMessage, _) = tokens;
                writer.WriteLine(BuildLogLine(currentDate, currentNumber, firstType, firstMessage));

                while (!reader.EndOfStream)
                {
                    if (!TryParseCompressedLog(reader.ReadLine(), currentDate, out var log))
                    {
                        writer.WriteLine(log.message);
                        continue;
                    }

                    var (date, number, type, message, isNewDate) = log;
                    
                    currentDate = isNewDate
                        ? date
                        : currentDate;
                    
                    (number, currentNumber) = number > MaxNumberSpan
                        ? (number, number)
                        : (number + currentNumber, currentNumber);
                    
                    writer.WriteLine(BuildLogLine(date, number, type, message));
                }
            }
        }

        private bool TryParseLog(string log, out (DateTime date, long number, string type, string message) tokens)
        {
            var strings = log.Split(' ', 5, StringSplitOptions.RemoveEmptyEntries);

            if (strings.Length == 5
                && DateTime.TryParseExact(strings[0] + " " + strings[1], "yyyy-MM-dd HH:mm:ss,fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && long.TryParse(strings[2], out var number)
                && strings[3].Length <= 5)
            {
                tokens = (date, number, strings[3], strings[4]);
                return true;
            }
            
            tokens = (DateTime.MinValue, 0, string.Empty, log);
            return false;
        }

        private bool TryParseCompressedLog(string log, DateTime currentDate, out (DateTime date, long number, string type, string message, bool isNewDate) tokens)
        {
            var strings = log.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);

            if (strings.Length == 4
                && long.TryParse(strings[1], out var number)
                && strings[2].Length <= 5)
            {
                if (DateTime.TryParseExact(strings[0], "yyyyMMddHHmmssfff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    tokens = (date, number, strings[2], strings[3], true);
                    return true;
                }
                
                if (long.TryParse(strings[0], out var span))
                {
                    tokens = (currentDate.AddMilliseconds(span), number, strings[2], strings[3], false);
                    return true;
                }
            }

            tokens = (DateTime.MinValue, 0, string.Empty, log, false);
            return false;
        }

        private bool IsBinary(Stream stream)
        {
            var buffer = new byte[1024];
            stream.Read(buffer, 0, buffer.Length);
            stream.Position = 0;
            return buffer.Any(x => x == '\0');
        }

        private bool IsCrlf(Stream stream)
        {
            var buffer = new byte[MaxLineLength];
            stream.Read(buffer, 0, buffer.Length);
            stream.Position = 0;
            return buffer.Any(x => x == '\r');
        }

        private string BuildLogLine(DateTime date, long number, string type, string message)
            => $"{date:yyyy-MM-dd HH:mm:ss,fff} {number.ToString().PadRight(6)} " +
               $"{DecompressLogType(type).PadRight(5)} {message}";

        private string CompressLogType(string type)
            => type == "INFO"
                ? "1"
                : type == "ERROR"
                    ? "0"
                    : type;

        private string DecompressLogType(string type)
            => type == "1"
                ? "INFO"
                : type == "0"
                    ? "ERROR"
                    : type;
    }
}