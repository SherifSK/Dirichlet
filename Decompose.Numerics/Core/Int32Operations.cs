﻿using System.Numerics;

namespace Decompose.Numerics
{
    public class Int32Operations : Operations<int>
    {
        public override Integer<int> Wrap(int value) { return new Integer<int>(value, this); }
        public override int Zero { get { return 0; } }
        public override int One { get { return 1; } }
        public override int Two { get { return 2; } }
        public override bool IsUnsigned { get { return false; } }
        public override int Convert(int a) { return (int)a; }
        public override BigInteger ToBigInteger(int a) { return a; }
        public override int Add(int a, int b) { return a + b; }
        public override int Subtract(int a, int b) { return a - b; }
        public override int Multiply(int a, int b) { return a * b; }
        public override int Divide(int a, int b) { return a / b; }
        public override int Modulus(int a, int b) { return a % b; }
        public override int Negate(int a) { return 0 - a; }
        public override int LeftShift(int a, int n) { return a << n; }
        public override int RightShift(int a, int n) { return a >> n; }
        public override int And(int a, int b) { return a & b; }
        public override int Or(int a, int b) { return a | b; }
        public override int ExclusiveOr(int a, int b) { return a ^ b; }
        public override bool IsZero(int a) { return a == 0; }
        public override bool IsOne(int a) { return a == 1; }
        public override bool IsEven(int a) { return (a & 1) == 0; }
        public override bool Equals(int x, int y) { return x.Equals(y); }
        public override int GetHashCode(int obj) { return obj.GetHashCode(); }
        public override int Compare(int x, int y) { return x.CompareTo(y); }
        public override int ModularProduct(int a, int b, int modulus) { return (int)((long)a * b % modulus); }
        public override int ModularPower(int value, int exponent, int modulus) { return IntegerMath.ModularPower(value, exponent, modulus); }
        public override uint LeastSignificantWord(int a) { return (uint)(a & uint.MaxValue); }
    }
}