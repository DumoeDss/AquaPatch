using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;
namespace AquaSys.Tools
{
    public static class StreamTools
    {
        #region Base64
        public static string ToBase64(string str)
        {
            byte[] b = Encoding.Default.GetBytes(str);
            str = Convert.ToBase64String(b);
            return str;

        }

        public static string FromBase64(string str)
        {
            try
            {
                byte[] c = Convert.FromBase64String(str);
                str = Encoding.Default.GetString(c);
                return str;
            }
            catch (Exception)
            {
                return null;
            }

        }
        #endregion

        #region Deserialize
        public static T DeserializeObject<T>(string data) where T : class
        {
            try
            {
                T result = JsonConvert.DeserializeObject<T>(data);

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                return null;
            }
        }

        public static T DeserializeObject<T>(string data, params JsonConverter[] converters) where T : class
        {
            try
            {
                T result = JsonConvert.DeserializeObject<T>(data, converters);
                return result;
            }
            catch (Exception)
            {
                return null;
            }

        }

        public static T DeserializeObject<T>(string data, JsonSerializerSettings? settings) where T : class
        {
            try
            {
                T result = JsonConvert.DeserializeObject<T>(data, settings);
                return result;
            }
            catch (Exception)
            {
                return null;
            }

        }

        public static T DeserializeObjectFromFilePath<T>(string path) where T : class
        {
            try
            {
                T result = JsonConvert.DeserializeObject<T>(ReadFile(path));

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"DeserializeObjectFromFilePath error {ex.ToString()}" );
                return null;
            }

        }

        public static T DeserializeObjectFromFilePath<T>(string path, params JsonConverter[] converters) where T : class
        {
            try
            {
                T result = JsonConvert.DeserializeObject<T>(ReadFile(path), converters);

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                return null;
            }

        }

        public static T DeserializeObjectFromFilePath<T>(string path, JsonSerializerSettings? settings) where T : class
        {
            try
            {
                T result = JsonConvert.DeserializeObject<T>(ReadFile(path), settings);

                return result;
            }
            catch (Exception)
            {

                return null;
            }

        }
        #endregion

        #region Serialize

        public static void SerializeObject(string path, object obj)
        {
            string data = JsonConvert.SerializeObject(obj);
            WriteFile(path, data);
        }

        public static void SerializeObject(string path, object obj, params JsonConverter[] converters)
        {
            string data = JsonConvert.SerializeObject(obj, converters);
            WriteFile(path, data);
        }
        public static void SerializeObject(string path, object obj, JsonSerializerSettings? settings)
        {
            string data = JsonConvert.SerializeObject(obj, settings);
            WriteFile(path, data);
        }

        public static string SerializeObject(object obj)
        {
            string data = JsonConvert.SerializeObject(obj);
            return data;
        }

        public static string SerializeObject(object obj, params JsonConverter[] converters)
        {
            string data = JsonConvert.SerializeObject(obj, converters);
            return data;
        }
        public static string SerializeObject(object obj, JsonSerializerSettings? settings)
        {
            string data = JsonConvert.SerializeObject(obj, settings);
            return data;
        }
        #endregion

        #region ReadWrite
        public static string ReadFile(string path)
        {
            string strResult = "";
            if (File.Exists(path))
            {
                using (FileStream fileStream = File.OpenRead(path))
                {
                    using (StreamReader streamReader = new StreamReader(fileStream))
                    {
                        strResult = streamReader.ReadToEnd();
                    }
                }
            }
            return strResult;
        }

        public static void WriteFile(string path, string data)
        {
            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Directory.Exists)
            {
                Directory.CreateDirectory(fileInfo.Directory.FullName);
            }
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }

            using (FileStream fileStream = new FileStream(path, FileMode.CreateNew))
            {
                using (StreamWriter streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.Write(data);
                }
            }

        }
        #endregion

        #region Binary
        public static void WriteBinaryFile(string path, object data)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Directory.Exists)
                {
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                }
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                }
                using (FileStream fileStream = new FileStream(path, FileMode.CreateNew))
                {
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    binaryFormatter.Serialize(fileStream, data);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
            }
        }

        public static T ReadBinaryFile<T>(string path)
        {
            T data;
            if (File.Exists(path))
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open))
                {
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    data = (T)binaryFormatter.Deserialize(fileStream);
                    return data;
                }
            }
            else
            {
                return default(T);
            }
        }

        public static bool ReadBinaryFile<T>(string path, out T data)
        {
            if (File.Exists(path))
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open))
                {
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    data = (T)binaryFormatter.Deserialize(fileStream);
                    return true;
                }
            }
            else
            {
                data = default(T);
                return false;
            }
        }
        #endregion

        #region Bytes
        public static void WriteByteFile(string path, byte[] byteData)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Directory.Exists)
                {
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                }
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                }
                using (FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate))
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    fileStream.Write(byteData, 0, byteData.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
            }
        }
        public static byte[] ReadByteFile(string path)
        {
            if (File.Exists(path))
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open))
                {
                    byte[] datas = new byte[fileStream.Length];
                    fileStream.Seek(0, SeekOrigin.Begin);
                    fileStream.Read(datas, 0, datas.Length);
                    return datas;
                }
            }
            return null;
        }

        public static void WriteByteFile(string path, string data)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Directory.Exists)
                {
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                }
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                }
                using (FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate))
                {
                    byte[] byteData;
                    byteData = Encoding.UTF8.GetBytes(data);
                    fileStream.Seek(0, SeekOrigin.Begin);
                    fileStream.Write(byteData, 0, byteData.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
            }
        }

        #endregion

    }
}