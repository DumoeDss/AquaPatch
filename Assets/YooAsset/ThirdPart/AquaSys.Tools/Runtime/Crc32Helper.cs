using Force.Crc32;
using System.IO;
using System.Text;
namespace AquaSys.Tools
{
    public class Crc32Helper
    {
        public static string CalcHash(string filePath)
        {
            if (!File.Exists(filePath))
                return "";
            using (var fs = new FileStream(filePath, FileMode.Open))
            {
                using (var crc = new Crc32Algorithm())
                {
                    var crc32bytes = crc.ComputeHash(fs);
                    return ToHash(crc32bytes);
                }
            }
        }

        public static string CalcHash(Stream fs)
        {
            using (var crc = new Crc32Algorithm())
            {
                var crc32bytes = crc.ComputeHash(fs);
                return ToHash(crc32bytes);
            }
        }

        public static uint CalcHashUInt(Stream stream)
        {
            using (var crc = new Crc32Algorithm())
            {
                var crc32bytes = crc.ComputeHash(stream);
                return System.BitConverter.ToUInt32(crc32bytes, 0);
            }
        }

        public static string CalcHash(byte[] bytes)
        {
            var hash = Crc32Algorithm.Compute(bytes).ToString("x2");
            return hash;
        }

        public static bool CheckHash(string filePath, string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return false;
            return CalcHash(filePath).Equals(hash);
        }

        public static bool CheckHash(FileStream fs, string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return false;
            return CalcHash(fs).Equals(hash);
        }

        public static bool CheckHash(byte[] bytes, string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return false;
            return CalcHash(bytes).Equals(hash);
        }

        static string ToHash(byte[] data)
        {
            var sb = new StringBuilder();
            foreach (var t in data)
                sb.Append(t.ToString("x2"));
            return sb.ToString();
        }
    }
}