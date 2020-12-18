using System.Numerics;

namespace OmniCryptography
{
    public class Point
    {
        public static readonly Point INFINITY = new Point(null, default(BigInteger), default(BigInteger));
        public CurveFp Curve { get; private set; }
        public BigInteger X { get; private set; }
        public BigInteger Y { get; private set; }

        public Point(CurveFp curve, BigInteger x, BigInteger y)
        {
            this.Curve = curve;
            this.X = x;
            this.Y = y;
        }
        public Point Double()
        {
            if (this == INFINITY)
                return INFINITY;

            BigInteger p = this.Curve.p;
            BigInteger a = this.Curve.a;
            BigInteger l = ((3 * this.X * this.X + a) * InverseMod(2 * this.Y, p)) % p;
            BigInteger x3 = (l * l - 2 * this.X) % p;
            BigInteger y3 = (l * (this.X - x3) - this.Y) % p;
            return new Point(this.Curve, x3, y3);
        }
        public override string ToString()
        {
            if (this == INFINITY)
                return "infinity";
            return string.Format("({0},{1})", this.X, this.Y);
        }
        public static Point operator +(Point left, Point right)
        {
            if (right == INFINITY)
                return left;
            if (left == INFINITY)
                return right;
            if (left.X == right.X)
            {
                if ((left.Y + right.Y) % left.Curve.p == 0)
                    return INFINITY;
                else
                    return left.Double();
            }

            var p = left.Curve.p;
            var l = ((right.Y - left.Y) * InverseMod(right.X - left.X, p)) % p;
            var x3 = (l * l - left.X - right.X) % p;
            var y3 = (l * (left.X - x3) - left.Y) % p;
            return new Point(left.Curve, x3, y3);
        }
        public static Point operator *(Point left, BigInteger right)
        {
            var e = right;
            if (e == 0 || left == INFINITY)
                return INFINITY;
            var e3 = 3 * e;
            var negativeLeft = new Point(left.Curve, left.X, -left.Y);
            var i = LeftmostBit(e3) / 2;
            var result = left;
            while (i > 1)
            {
                result = result.Double();
                if ((e3 & i) != 0 && (e & i) == 0)
                    result += left;
                if ((e3 & i) == 0 && (e & i) != 0)
                    result += negativeLeft;
                i /= 2;
            }
            return result;
        }

        private static BigInteger LeftmostBit(BigInteger x)
        {
            BigInteger result = 1;
            while (result <= x)
                result = 2 * result;
            return result / 2;
        }
        private static BigInteger InverseMod(BigInteger a, BigInteger m)
        {
            while (a < 0) a += m;
            if (a < 0 || m <= a)
                a = a % m;
            BigInteger c = a;
            BigInteger d = m;

            BigInteger uc = 1;
            BigInteger vc = 0;
            BigInteger ud = 0;
            BigInteger vd = 1;

            while (c != 0)
            {
                BigInteger r;
                //q, c, d = divmod( d, c ) + ( c, );
                var q = BigInteger.DivRem(d, c, out r);
                d = c;
                c = r;

                //uc, vc, ud, vd = ud - q*uc, vd - q*vc, uc, vc;
                var uct = uc;
                var vct = vc;
                var udt = ud;
                var vdt = vd;
                uc = udt - q * uct;
                vc = vdt - q * vct;
                ud = uct;
                vd = vct;
            }
            if (ud > 0) return ud;
            else return ud + m;
        }
    }
}
