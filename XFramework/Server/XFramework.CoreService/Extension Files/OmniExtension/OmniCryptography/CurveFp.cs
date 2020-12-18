using System.Numerics;

namespace OmniCryptography
{
    public class CurveFp
    {
        public BigInteger p { get; private set; }
        public BigInteger a { get; private set; }
        public BigInteger b { get; private set; }
        public CurveFp(BigInteger p, BigInteger a, BigInteger b)
        {
            this.p = p;
            this.a = a;
            this.b = b;
        }
    }
}
