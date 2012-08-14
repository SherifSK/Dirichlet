﻿#undef DIAG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Decompose.Numerics
{
    public class DivisorSummatoryFunctionOddUInt128 : IDivisorSummatoryFunction<UInt128>
    {
        private struct Region
        {
            public Region(UInt128 w, UInt128 h, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
            {
                this.w = w;
                this.h = h;
                this.a1 = a1;
                this.b1 = b1;
                this.c1 = c1;
                this.a2 = a2;
                this.b2 = b2;
                this.c2 = c2;
            }

            public UInt128 w;
            public UInt128 h;
            public UInt128 a1;
            public UInt128 b1;
            public UInt128 c1;
            public UInt128 a2;
            public UInt128 b2;
            public UInt128 c2;
        }

        public static readonly UInt128 C1 = 100;
        public static readonly UInt128 C2 = 30;

        private int threads;
        private UInt128 n;
        private UInt128 sum;
        private int unprocessed;
        private UInt128 xmanual;
        private ManualResetEventSlim finished;
        private BlockingCollection<Region> queue;
        private DivisionFreeDivisorSummatoryFunction manualAlgorithm;
        private IStore<MutableInteger>[] stores;

        public DivisorSummatoryFunctionOddUInt128(int threads)
        {
            this.threads = threads;
            queue = new BlockingCollection<Region>();
            finished = new ManualResetEventSlim();
            manualAlgorithm = new DivisionFreeDivisorSummatoryFunction(threads, false, true);
            stores = new IStore<MutableInteger>[Math.Max(threads, 1)];
            for (var i = 0; i < Math.Max(threads, 1); i++)
                stores[i] = new MutableIntegerStore(8);
        }

        public BigInteger Evaluate(BigInteger n)
        {
            return Evaluate((UInt128)n);
        }

        public UInt128 Evaluate(UInt128 n)
        {
            var xmax = IntegerMath.FloorSquareRoot(n);
            var s = Evaluate(n, 1, xmax);
            return 2 * s - xmax * xmax;
        }

        public BigInteger Evaluate(BigInteger n, BigInteger x0, BigInteger xmax)
        {
            return Evaluate((UInt128)n, (UInt128)x0, (UInt128)xmax);
        }

        public UInt128 Evaluate(UInt128 n, UInt128 x0, UInt128 xmax)
        {
            sum = 0;
            if (threads == 0)
            {
                AddToSum(EvaluateInternal(n, x0, xmax));
                AddToSum((UInt128)manualAlgorithm.Evaluate(n, 2 * x0 - 1, 2 * xmanual - 3));
                return sum;
            }

            // Create consumers.
            var consumers = threads;
            var tasks = new Task[consumers];
            for (var consumer = 0; consumer < consumers; consumer++)
            {
                var thread = consumer;
                tasks[consumer] = Task.Factory.StartNew(() => ConsumeRegions(thread));
            }

            // Produce work items.
            unprocessed = 1;
            finished.Reset();
            AddToSum(Processed(EvaluateInternal(n, x0, xmax)));
            finished.Wait();
            queue.CompleteAdding();

            // Add manual portion.
            AddToSum((UInt128)manualAlgorithm.Evaluate(n, 2 * x0 - 1, 2 * xmanual - 3));

            // Wait for completion.
            Task.WaitAll(tasks);

            return sum;
        }

        public void ConsumeRegions(int thread)
        {
            var s = (UInt128)0;
            var r = default(Region);
            while (queue.TryTake(out r, Timeout.Infinite))
                s += ProcessRegion(thread, r.w, r.h, r.a1, r.b1, r.c1, r.a2, r.b2, r.c2);
            AddToSum(s);
        }

        private void Enqueue(Region r)
        {
            Interlocked.Add(ref unprocessed, 1);
            queue.Add(r);
        }

        private UInt128 Processed(UInt128 result)
        {
            var value = Interlocked.Add(ref unprocessed, -1);
            if (value == 0)
                finished.Set();
            return result;
        }

        public UInt128 EvaluateInternal(UInt128 n, UInt128 x0, UInt128 xmax)
        {
            this.n = n;
            x0 = T1(x0 + 1);
            xmax = T1(xmax);
            if (x0 > xmax)
                return 0;
            var ymin = YFloor(xmax);
            var xmin = IntegerMath.Max(x0, IntegerMath.Min(T1(C1 * IntegerMath.CeilingRoot((2 * n), 3)), xmax));
#if DIAG
            Console.WriteLine("n = {0}, xmin = {1}, xmax = {2}", n, xmin, xmax);
#endif
            var s = (UInt128)0;
            var a2 = (UInt128)1;
            var x2 = xmax;
            var y2 = ymin;
            var c2 = a2 * x2 + y2;
            while (true)
            {
                var a1 = a2 + 1;
                var x4 = YTan(a1);
                var y4 = YFloor(x4);
                var c4 = a1 * x4 + y4;
                var x5 = x4 + 1;
                var y5 = YFloor(x5);
                var c5 = a1 * x5 + y5;
                if (x4 < xmin)
                    break;
                s += Triangle(c4 - c2 - x0) - Triangle(c4 - c2 - x5) + Triangle(c5 - c2 - x5);
                if (threads == 0)
                {
                    s += ProcessRegion(0, a1 * x2 + y2 - c5, a2 * x5 + y5 - c2, a1, 1, c5, a2, 1, c2);
                    while (queue.Count > 0)
                    {
                        var r = queue.Take();
                        s += ProcessRegion(0, r.w, r.h, r.a1, r.b1, r.c1, r.a2, r.b2, r.c2);
                    }
                }
                else
                    Enqueue(new Region(a1 * x2 + y2 - c5, a2 * x5 + y5 - c2, a1, 1, c5, a2, 1, c2));
                a2 = a1;
                x2 = x4;
                y2 = y4;
                c2 = c4;
            }
            s += (xmax - x0 + 1) * ymin + Triangle(xmax - x0);
            var rest = x2 - x0;
            s -= y2 * rest + a2 * Triangle(rest);
            xmanual = x2;
            return s;
        }

        public UInt128 ProcessRegion(int thread, UInt128 w, UInt128 h, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
#if DIAG
            Console.WriteLine("ProcessRegion: w = {0}, h = {1}, a1/b1 = {2}/{3}, a2/b2 = {4}/{5}, c1 = {6}, c2 = {7}", w, h, a1, b1, a2, b2, c1, c2);
#endif
            var s = (UInt128)0;
            while (true)
            {
                var a3 = a1 + a2;
                var b3 = b1 + b2;
                if (h > 0 && H(w, 1, a1, b1, c1, a2, b2, c2) <= n)
                {
                    s += w;
                    ++c2;
                    --h;
                }
                Debug.Assert(h == 0 || H(w, 0, a1, b1, c1, a2, b2, c2) <= n && H(w, 1, a1, b1, c1, a2, b2, c2) > n);
                if (w > 0 && H(1, h, a1, b1, c1, a2, b2, c2) <= n)
                {
                    s += h;
                    ++c1;
                    --w;
                }
                Debug.Assert(w == 0 || H(0, h, a1, b1, c1, a2, b2, c2) <= n && H(1, h, a1, b1, c1, a2, b2, c2) > n);
                var ab1 = a1 + b1;
                var a3b3 = (a1 + a2) * (b1 + b2);
                var abba = a1 * b2 + b1 * a2;
                var ab2 = 2 * a1 * b1;
                var u4 = UTan(ab1, abba, ab2, a3b3, c1);
                if (u4 <= 0)
                    return Processed(s + ProcessRegionManual(thread, w, h, a1, b1, c1, a2, b2, c2));
                var u5 = u4 + 1;
                UInt128 v4, v5;
                VFloor2(u4, a1, b1, c1, c2, abba, ab2, out v4, out v5);
                Debug.Assert(v4 == VFloor(u4, a1, b1, c1, a2, b2, c2));
                Debug.Assert(v5 == VFloor(u5, a1, b1, c1, a2, b2, c2));
                Debug.Assert(H(u4, v4, a1, b1, c1, a2, b2, c2) <= n && H(u4, v4 + 1, a1, b1, c1, a2, b2, c2) > n);
                Debug.Assert(H(u5, v5, a1, b1, c1, a2, b2, c2) <= n && H(u5, v5 + 1, a1, b1, c1, a2, b2, c2) > n);
                var v6 = u4 + v4;
                var u7 = u5 + v5;
                if (u4 <= C2 || v5 <= C2 || v6 >= h || u7 >= w)
                    return Processed(s + ProcessRegionManual(thread, w, h, a1, b1, c1, a2, b2, c2));
                if (v6 != u7)
                    s += Triangle(v6 - 1) - Triangle(v6 - u5) + Triangle(u7 - u5);
                else
                    s += Triangle(v6 - 1);
#if DIAG
                Console.WriteLine("ProcessRegion: s = {0}", s);
#endif
                if (threads == 0)
                {
                    Enqueue(new Region(u4, h - v6, a1, b1, c1, a3, b3, c1 + c2 + v6));
                    w -= u7;
                    h = v5;
                    a1 = a3;
                    b1 = b3;
                    c1 += c2 + u7;
                }
                else
                {
                    Enqueue(new Region(u4, h - v6, a1, b1, c1, a3, b3, c1 + c2 + v6));
                    Enqueue(new Region(w - u7, v5, a3, b3, c1 + c2 + u7, a2, b2, c2));
                    return Processed(s);
                }
            }
        }

        public UInt128 ProcessRegionManual(int thread, UInt128 w, UInt128 h, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            return w < h ? ProcessRegionManual(thread, w, a1, b1, c1, a2, b2, c2) : ProcessRegionManual(thread, h, b2, a2, c2, b1, a1, c1);
        }

#if false
        public UInt128 ProcessRegionManual(int thread, UInt128 w, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            if (w <= 1)
                return 0;

            var s = (UInt128)0;
            var umax = (long)w - 1;
            var t1 = (a1 * b2 + b1 * a2) << 1;
            var t2 = (c1 << 1) - a1 - b1;
            var t3 = (t2 << 2) + 12;
            var t4 = (a1 * b1) << 2;
            var t5 = t1 * (1 + c1) - a1 + b1 - t4 * c2;
            var t6 = IntegerMath.Square(t2 + 2) - t4 * n;

            var u = (long)1;
            while (true)
            {
                Debug.Assert((t5 - IntegerMath.CeilingSquareRoot(t6)) / t4 == VFloor(u, a1, b1, c1, a2, b2, c2));
                s += (t5 - IntegerMath.CeilingSquareRoot(t6)) / t4;
                if (u >= umax)
                    break;
                t5 += t1;
                t6 += t3;
                t3 += 8;
                ++u;
            }

            Debug.Assert(s == ProcessRegionHorizontal(w, 0, a1, b1, c1, a2, b2, c2));
#if DIAG
            Console.WriteLine("ProcessRegionManual: s = {0}", s);
#endif
            return s;
        }
#endif

#if true
        public UInt128 ProcessRegionManual(int thread, UInt128 w, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            if (w <= 1)
                return 0;

            var s = (UInt128)0;
            var umax = (long)w - 1;
            var t1 = (a1 * b2 + b1 * a2) << 1;
            var t2 = (c1 << 1) - a1 - b1;
            var t3 = (t2 << 2) + 12;
            var t4 = (a1 * b1) << 2;
            var t5 = t1 * (1 + c1) - a1 + b1 - t4 * c2;
            var t6 = IntegerMath.Square(t2 + 2) - t4 * n;

            var store = stores[thread];
            var sRep = store.Allocate().Set(0);
            var t1Rep = store.Allocate().Set(t1);
            var t3Rep = store.Allocate().Set(t3);
            var t4Rep = store.Allocate().Set(t4);
            var t5Rep = store.Allocate().Set(t5);
            var t6Rep = store.Allocate().Set(t6);
            var t7Rep = store.Allocate();
            var t8Rep = store.Allocate();

            var u = (long)1;
            while (true)
            {
                t8Rep.SetUnsignedDifference(t5Rep, t7Rep.SetCeilingSquareRoot(t6Rep, store))
                    .ModuloWithQuotient(t4Rep, t7Rep);
                sRep.SetUnsignedSum(sRep, t7Rep);
                if (u >= umax)
                    break;
                t5Rep.SetUnsignedSum(t5Rep, t1Rep);
                t6Rep.SetUnsignedSum(t6Rep, t3Rep);
                t3Rep.SetUnsignedSum(t3Rep, 8);
                ++u;
            }
            s = (UInt128)sRep;

            store.Release(sRep);
            store.Release(t1Rep);
            store.Release(t3Rep);
            store.Release(t4Rep);
            store.Release(t6Rep);
            store.Release(t7Rep);
            store.Release(t8Rep);

            Debug.Assert(s == ProcessRegionHorizontal(w, 0, a1, b1, c1, a2, b2, c2));
#if DIAG
            Console.WriteLine("ProcessRegionManual: s = {0}", s);
#endif
            return s;
        }
#endif

        public UInt128 ProcessRegionHorizontal(UInt128 w, UInt128 h, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            var s = (UInt128)0;
            for (var u = (UInt128)1; u < w; u++)
                s += VFloor(u, a1, b1, c1, a2, b2, c2);
            return s;
        }

        public UInt128 ProcessRegionVertical(UInt128 w, UInt128 h, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            var s = (UInt128)0;
            for (var v = (UInt128)1; v < h; v++)
                s += UFloor(v, a1, b1, c1, a2, b2, c2);
            return s;
        }

        public UInt128 H(UInt128 u, UInt128 v, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            var uu = u + c1;
            var vv = v + c2;
            return (((b2 * uu - b1 * vv) << 1) - 1) * (((a1 * vv - a2 * uu) << 1) - 1);
        }

        public UInt128 UTan(UInt128 ab1, UInt128 abba, UInt128 ab2, UInt128 a3b3, UInt128 c1)
        {
            return (ab1 + IntegerMath.FloorSquareRoot(IntegerMath.Square(abba + ab2) * n / a3b3) - (c1 << 1)) / 2;
        }

        public UInt128 UFloor(UInt128 v, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            return (2 * (a1 * b2 + b1 * a2) * (v + c2) + a2 - b2 - IntegerMath.CeilingSquareRoot(IntegerMath.Square(2 * (v + c2) - a2 - b2) - 4 * a2 * b2 * n)) / (4 * a2 * b2) - c1;
        }

        public UInt128 VFloor(UInt128 u, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 a2, UInt128 b2, UInt128 c2)
        {
            return (2 * (a1 * b2 + b1 * a2) * (u + c1) - a1 + b1 - IntegerMath.CeilingSquareRoot(IntegerMath.Square(2 * (u + c1) - a1 - b1) - 4 * a1 * b1 * n)) / (4 * a1 * b1) - c2;
        }

        public void VFloor2(UInt128 u1, UInt128 a1, UInt128 b1, UInt128 c1, UInt128 c2, UInt128 abba, UInt128 ab2, out UInt128 v1, out UInt128 v2)
        {
            var uu = (u1 + c1) << 1;
            var t1 = ab2 << 1;
            var t2 = abba * uu - a1 + b1 - t1 * c2;
            var t3 = uu - a1 - b1;
            var t4 = IntegerMath.Square(t3) - t1 * n;
            v1 = (t2 - IntegerMath.CeilingSquareRoot(t4)) / t1;
            v2 = (t2 + (abba << 1) - IntegerMath.CeilingSquareRoot((t4 + ((t3 + 1) << 2)))) / t1;
        }

        public static UInt128 Triangle(UInt128 a)
        {
            return a * (a + 1) >> 1;
        }

        private UInt128 T1(UInt128 x)
        {
            return (x + 1) >> 1;
        }

        private UInt128 YTan(UInt128 a)
        {
            return T1(IntegerMath.FloorSquareRoot((n / a)));
        }

        private UInt128 YFloor(UInt128 x)
        {
            return (n / ((x << 1) - 1) + 1) >> 1;
        }

        private void AddToSum(UInt128 s)
        {
            lock (this)
                sum += s;
        }
    }
}
