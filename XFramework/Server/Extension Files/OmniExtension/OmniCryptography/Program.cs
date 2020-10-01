using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace OmniCryptography
{
    class Program
    {
        static void Main(string[] args)
        {
            PublicPrivateKeyPairDemo();
            SignatureDemo();
            TransactionsDemo();
        }

        private static void PublicPrivateKeyPairDemo()
        {
            string privateKey = "123";
            string publicKey = GetPublicKeyFromPrivateKeyEx(privateKey);
            Console.WriteLine("GetPublicKeyFromPrivateKeyEx: " + publicKey);

            privateKey = "123";
            publicKey = GetPublicKeyFromPrivateKey(privateKey).ToLower();
            Console.WriteLine("GetPublicKeyFromPrivateKey: " +  publicKey);
        }

        private static void SignatureDemo()
        {
            var privateKey = "68040878110175628235481263019639686";

            var publicKey = GetPublicKeyFromPrivateKeyEx(privateKey);

            var message = "Hello World!";

            var signature = GetSignature(privateKey, message);
            Console.WriteLine(signature);

            var isvalid = VerifySignature(message, publicKey, signature);
            Console.WriteLine("Valid signature? " + isvalid);
        }

        private static void TransactionsDemo()
        {
            var privateKey = "68040878110175628235481263019639686";
            var publicKey = GetPublicKeyFromPrivateKeyEx(privateKey);
            var transaction = new Transaction();
            transaction.FromPublicKey = publicKey;
            transaction.ToPublicKey = "QrSNX7KxzGnQqauPiXKxP58nhukU252RKAmSqg17L8h7BpU984g4mxHck6cLzhArADz2p1xo3BwAsbiaLhQaziyu";
            transaction.Amount = 10;
            transaction.Signature = GetSignature(privateKey, transaction.ToString());

            Console.WriteLine("Transaction signature: " + transaction.Signature);

            bool isValidTransaction = VerifySignature(transaction.ToString(), publicKey, transaction.Signature);
            Console.WriteLine("Valid transaction message: " + isValidTransaction);
        }


        public static bool VerifySignature(string message, string publicKey, string signature)
        {
            var curve = SecNamedCurves.GetByName("secp256k1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            var publicKeyBytes = Base58Encoding.Decode(publicKey);

            var q = curve.Curve.DecodePoint(publicKeyBytes);

            var keyParameters = new
                    Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(q,
                    domain);

            ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

            signer.Init(false, keyParameters);
            signer.BlockUpdate(Encoding.ASCII.GetBytes(message), 0, message.Length);

            var signatureBytes = Base58Encoding.Decode(signature);

            return signer.VerifySignature(signatureBytes);
        }


        public static string GetSignature(string privateKey, string message)
        {
            var curve = SecNamedCurves.GetByName("secp256k1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            var keyParameters = new
                    ECPrivateKeyParameters(new Org.BouncyCastle.Math.BigInteger(privateKey),
                    domain);

            ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

            signer.Init(true, keyParameters);
            signer.BlockUpdate(Encoding.ASCII.GetBytes(message), 0, message.Length);
            var signature = signer.GenerateSignature();
            return Base58Encoding.Encode(signature);
        }

        public static string GetPublicKeyFromPrivateKeyEx(string privateKey)
        {
            var curve = SecNamedCurves.GetByName("secp256k1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            var d = new Org.BouncyCastle.Math.BigInteger(privateKey);
            var q = domain.G.Multiply(d);

            var publicKey = new ECPublicKeyParameters(q, domain);
            return Base58Encoding.Encode(publicKey.Q.GetEncoded());
        }


        private static string GetPublicKeyFromPrivateKey(string privateKey)
        {
            var p = BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F", NumberStyles.HexNumber);
            var b = (BigInteger)7;
            var a = BigInteger.Zero;
            var Gx = BigInteger.Parse("79BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798", NumberStyles.HexNumber);
            var Gy = BigInteger.Parse("483ADA7726A3C4655DA4FBFC0E1108A8FD17B448A68554199C47D08FFB10D4B8", NumberStyles.HexNumber);

            CurveFp curve256 = new CurveFp(p, a, b);
            Point generator256 = new Point(curve256, Gx, Gy);

            var secret = BigInteger.Parse(privateKey, NumberStyles.HexNumber);
            var pubkeyPoint = generator256 * secret;
            return pubkeyPoint.X.ToString("X") + pubkeyPoint.Y.ToString("X");
        }
    }


    class Transaction
    {
        public string FromPublicKey { get; internal set; }
        public string ToPublicKey { get; internal set; }
        public int Amount { get; internal set; }
        public string Signature { get; internal set; }

        public override string ToString()
        {
            return $"{this.Amount}:{this.FromPublicKey}:{this.ToPublicKey}";
        }
    }

    /*
     * Original Source: https://bitcoin.stackexchange.com/a/25039
     */
    

  
}
