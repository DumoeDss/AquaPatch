using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace AquaSys.Patch.Encryption
{
    public class AESEncrypt
    {
        // 加密识别头（用来识别文件是否已经加密过）
        private const string AES_HEAD = "AESEncrypt";

        private const string StrSalt = "Y2hpc2FpbWFqb3NheW95bw==";

        private const string StrIV = "bWFob3VzaG9qb3NheW95bw==";

        /// <summary>
        /// 文件加密，传入文件路径
        /// </summary>
        /// <param name="path"></param>
        /// <param name="EncrptyKey"></param>
        public static void Encrypt(string path, string destPath, string EncrptyKey)
        {
            if (!File.Exists(path))
                return;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    if (fs != null)
                    {
                        byte[] headBuff = new byte[10];
                        fs.Read(headBuff, 0, headBuff.Length);
                        string headTag = Encoding.UTF8.GetString(headBuff);
                        if (headTag == AES_HEAD)
                        {
                            return;
                        }
                        //加密并且写入字节头
                        fs.Seek(0, SeekOrigin.Begin);
                        byte[] buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, Convert.ToInt32(fs.Length));
                        byte[] headBuffer = Encoding.UTF8.GetBytes(AES_HEAD);
                        using (FileStream ws = new FileStream(destPath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                        {
                            ws.Write(headBuffer, 0, headBuffer.Length);
                            byte[] EncBuffer = Encrypt(buffer, EncrptyKey);
                            ws.Write(EncBuffer, 0, EncBuffer.Length);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// 流解密
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="EncrptyKey"></param>
        /// <returns></returns>
        public static byte[] Decrypt(Stream stream, string EncrptyKey)
        {
            byte[] headBuff = new byte[10];
            stream.Read(headBuff, 0, headBuff.Length);
            string headTag = Encoding.UTF8.GetString(headBuff);
            if (headTag == AES_HEAD)
            {
                byte[] buffer = new byte[stream.Length - headBuff.Length];
                stream.Read(buffer, 0, Convert.ToInt32(stream.Length - headBuff.Length));
                stream.Seek(0, SeekOrigin.Begin);
                stream.SetLength(0);
                byte[] DecBuffer = Decrypt(buffer, EncrptyKey);
                return DecBuffer;
            }
            else
            {
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, Convert.ToInt32(stream.Length));
                return buffer;
            }
        }      

        /// <summary>
        /// 文件解密
        /// </summary>
        /// <returns></returns>
        public static byte[] DecryptFile(string path, string EncrptyKey)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            byte[] DecBuffer = null;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs != null)
                    {
                        byte[] headBuff = new byte[10];
                        fs.Read(headBuff, 0, headBuff.Length);
                        string headTag = Encoding.UTF8.GetString(headBuff);
                        if (headTag == AES_HEAD)
                        {
                            byte[] buffer = new byte[fs.Length - headBuff.Length];
                            fs.Read(buffer, 0, Convert.ToInt32(fs.Length - headBuff.Length));
                            DecBuffer = Decrypt(buffer, EncrptyKey);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            return DecBuffer;
        }

        /// <summary>
        /// 加密字符串
        /// </summary>
        /// <param name="EncryptString">待加密密文</param>
        /// <param name="EncryptKey">加密密钥</param>
        public static string Encrypt(string EncryptString, string EncryptKey)
        {
            return Convert.ToBase64String(Encrypt(Encoding.Default.GetBytes(EncryptString), EncryptKey));
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="EncryptString">待加密密文</param>
        /// <param name="EncryptKey">加密密钥</param>
        public static byte[] Encrypt(byte[] EncryptByte, string EncryptKey)
        {
            if (EncryptByte.Length == 0) { throw (new Exception("明文不得为空")); }
            if (string.IsNullOrEmpty(EncryptKey)) { throw (new Exception("密钥不得为空")); }
            byte[] m_strEncrypt;
            byte[] m_btIV = Convert.FromBase64String(StrIV);
            byte[] m_salt = Convert.FromBase64String(StrSalt);
            Rijndael m_AESProvider = Rijndael.Create();
            try
            {
                using (MemoryStream m_stream = new MemoryStream())
                {
                    PasswordDeriveBytes pdb = new PasswordDeriveBytes(EncryptKey, m_salt);
                    ICryptoTransform transform = m_AESProvider.CreateEncryptor(pdb.GetBytes(32), m_btIV);
                    using (CryptoStream m_csstream = new CryptoStream(m_stream, transform, CryptoStreamMode.Write))
                    {
                        m_csstream.Write(EncryptByte, 0, EncryptByte.Length);
                        m_csstream.FlushFinalBlock();
                        m_strEncrypt = m_stream.ToArray();
                    }
                }
            }
            catch (IOException ex) { throw ex; }
            catch (CryptographicException ex) { throw ex; }
            catch (ArgumentException ex) { throw ex; }
            catch (Exception ex) { throw ex; }
            finally { m_AESProvider.Clear(); }
            return m_strEncrypt;
        }


        /// <summary>
        /// 解密字符串
        /// </summary>
        /// <param name="DecryptString">待解密密文</param>
        /// <param name="DecryptKey">解密密钥</param>
        public static string Decrypt(string DecryptString, string DecryptKey)
        {
            return Convert.ToBase64String(Decrypt(Encoding.Default.GetBytes(DecryptString), DecryptKey));
        }

        /// <summary>
        /// AES 解密(高级加密标准，是下一代的加密算法标准，速度快，安全级别高，目前 AES 标准的一个实现是 Rijndael 算法)
        /// </summary>
        /// <param name="DecryptString">待解密密文</param>
        /// <param name="DecryptKey">解密密钥</param>
        public static byte[] Decrypt(byte[] DecryptByte, string DecryptKey)
        {
            if (DecryptByte.Length == 0) { throw (new Exception("密文不得为空")); }
            if (string.IsNullOrEmpty(DecryptKey)) { throw (new Exception("密钥不得为空")); }
            byte[] m_strDecrypt;
            byte[] m_btIV = Convert.FromBase64String(StrIV);
            byte[] m_salt = Convert.FromBase64String(StrSalt);
            Rijndael m_AESProvider = Rijndael.Create();
            try
            {
                using (MemoryStream m_stream = new MemoryStream())
                {
                    PasswordDeriveBytes pdb = new PasswordDeriveBytes(DecryptKey, m_salt);
                    ICryptoTransform transform = m_AESProvider.CreateDecryptor(pdb.GetBytes(32), m_btIV);
                    using (CryptoStream m_csstream = new CryptoStream(m_stream, transform, CryptoStreamMode.Write))
                    {
                        m_csstream.Write(DecryptByte, 0, DecryptByte.Length);
                        m_csstream.FlushFinalBlock();
                        m_strDecrypt = m_stream.ToArray();
                    }
                }
            }
            catch (IOException ex) { throw ex; }
            catch (CryptographicException ex) { throw ex; }
            catch (ArgumentException ex) { throw ex; }
            catch (Exception ex) { throw ex; }
            finally { m_AESProvider.Clear(); }
            return m_strDecrypt;
        }

    }
}