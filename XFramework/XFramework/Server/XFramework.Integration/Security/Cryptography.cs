using System.Security.Cryptography;
using System.Text;

namespace XFramework.Integration.Security;

public static class Cryptography
{
    public static string ToMd5(this string stringData)
    {
        var md5Provider = new MD5CryptoServiceProvider();
        var bytes = md5Provider.ComputeHash(new UTF8Encoding().GetBytes(stringData));
        return GetStringFromHash(bytes);
    }

    public static string ToSha256(this string stringData)
    {
        var sha256 = SHA256Managed.Create();
        var passwordByte = Encoding.UTF8.GetBytes(stringData);
        var bytes = sha256.ComputeHash(passwordByte);

        return GetStringFromHash(bytes);
    }

    public static string ToSha512(this string stringData)
    {
        var sha512 = SHA512Managed.Create();
        var passwordByte = Encoding.UTF8.GetBytes(stringData);
        var bytes = sha512.ComputeHash(passwordByte);

        return GetStringFromHash(bytes);
    }

    private static string GetStringFromHash(byte[] hash)
    {
        var result = new StringBuilder();
        foreach (var t in hash)
        {
            result.Append(t.ToString("X2"));
        }

        return result.ToString().ToLower();
    }

    public static string EncryptWith3Des(this string toEncrypt, string key, bool useHashing = false)
    {
        byte[] keyArray;
        var toEncryptArray = Encoding.UTF8.GetBytes(toEncrypt);

        //If hashing use get hashcode regards to your key
        if (useHashing)
        {
            var cryptoServiceProvider = new MD5CryptoServiceProvider();
            keyArray = cryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(key));
            //Always release the resources and flush data
            // of the Cryptographic service provide. Best Practice

            cryptoServiceProvider.Clear();
        }
        else
        {
            keyArray = Encoding.UTF8.GetBytes(key);
        }

        var tripleDesCryptoServiceProvider = new TripleDESCryptoServiceProvider
        {
            Key = keyArray,
            Mode = CipherMode.ECB,
            Padding = PaddingMode.PKCS7
        };

        //set the secret key for the tripleDES algorithm
        //mode of operation. there are other 4 modes.
        //We choose ECB(Electronic code Book)
        //padding mode(if any extra byte added)

        var cTransform = tripleDesCryptoServiceProvider.CreateEncryptor();
        //transform the specified region of bytes array to resultArray
        var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        //Release resources held by TripleDes Encryptor
        tripleDesCryptoServiceProvider.Clear();

        //Return the encrypted data into unreadable string format
        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }

    public static string DecryptWith3Des(this string cipherString, string key, bool useHashing = false)
    {
        byte[] keyArray;
        //get the byte code of the string

        var toEncryptArray = Convert.FromBase64String(cipherString);

        if (useHashing)
        {
            //if hashing was used get the hash code with regards to your key
            var md5CryptoServiceProvider = new MD5CryptoServiceProvider();
            keyArray = md5CryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(key));

            //release any resource held by the MD5CryptoServiceProvider
            md5CryptoServiceProvider.Clear();
        }
        else
        {
            //if hashing was not implemented get the byte code of the key
            keyArray = Encoding.UTF8.GetBytes(key);
        }

        var tripleDesCryptoServiceProvider = new TripleDESCryptoServiceProvider
        {
            Key = keyArray,
            Mode = CipherMode.ECB,
            Padding = PaddingMode.PKCS7
        };
        //set the secret key for the tripleDES algorithm
        //mode of operation. there are other 4 modes. 
        //We choose ECB(Electronic code Book)
        //padding mode(if any extra byte added)

        var cTransform = tripleDesCryptoServiceProvider.CreateDecryptor();
        var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        //Release resources held by TripleDes Encryptor                
        tripleDesCryptoServiceProvider.Clear();

        //return the Clear decrypted TEXT
        return Encoding.UTF8.GetString(resultArray);
    }

    public static string EncryptWithAes(this string text, string keyString)
    {
        var key = Encoding.UTF8.GetBytes(keyString);

        using (var aesAlg = Aes.Create())
        {
            using (var encryptor = aesAlg.CreateEncryptor(key, aesAlg.IV))
            {
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(text);
                    }

                    var iv = aesAlg.IV;
                    var decryptedContent = msEncrypt.ToArray();
                    var result = new byte[iv.Length + decryptedContent.Length];

                    Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                    Buffer.BlockCopy(decryptedContent, 0, result, iv.Length, decryptedContent.Length);

                    return Convert.ToBase64String(result);
                }
            }
        }
    }

    public static string DecryptWithAes(this string cipherText, string keyString)
    {
        var fullCipher = Convert.FromBase64String(cipherText);

        var iv = new byte[16];
        var cipher = new byte[fullCipher.Length - iv.Length]; //changes here

        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        // Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, fullCipher.Length - iv.Length); // changes here
        var key = Encoding.UTF8.GetBytes(keyString);

        using (var aesAlg = Aes.Create())
        {
            using (var decryptor = aesAlg.CreateDecryptor(key, iv))
            {
                string result;
                using (var msDecrypt = new MemoryStream(cipher))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {
                            result = srDecrypt.ReadToEnd();
                        }
                    }
                }

                return result;
            }
        }
    }
        
    public static string Encrypt(this string text, string keyString, PbeEncryptionAlgorithm encryptionAlgorithm = PbeEncryptionAlgorithm.Aes256Cbc)
    {
        var encryptedString = string.Empty;
        switch (encryptionAlgorithm)
        {
            case PbeEncryptionAlgorithm.Unknown:
                throw new ArgumentException("Encryption algorithm not set");
            case PbeEncryptionAlgorithm.Aes128Cbc:
                if (keyString.Length < 16)
                {
                    throw new ArgumentException("Encryption key should be at least 128 bits");
                }
                encryptedString = EncryptWithAes(text, keyString);
                break;
            case PbeEncryptionAlgorithm.Aes192Cbc:
                if (keyString.Length < 24)
                {
                    throw new ArgumentException("Encryption key should be at least 192 bits");
                }
                encryptedString = EncryptWithAes(text, keyString);
                break;
            case PbeEncryptionAlgorithm.Aes256Cbc:
                if (keyString.Length < 32)
                {
                    throw new ArgumentException("Encryption key should be at least 256 bits");
                }
                encryptedString = EncryptWithAes(text, keyString);
                break;
            case PbeEncryptionAlgorithm.TripleDes3KeyPkcs12:
                encryptedString = EncryptWith3Des(text, keyString);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(encryptionAlgorithm), encryptionAlgorithm, null);
        }

        return encryptedString;
    }

    public static string ToBase64(this string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    public static string FromBase64(this string base64EncodedData)
    {
        var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }
}