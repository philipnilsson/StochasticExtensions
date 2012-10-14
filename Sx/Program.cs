using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sx
{
    class Program
    {
        static void Main(string[] args)
        {
            var test = from x in (-1.0).RangeTo(1.0)
                       from y in (-1.0).RangeTo(1.0)
                       where x*x + y*y <= 1.0
                       select new { x = x, y = y };

            float ok = 0;
            int count = 1000000;
            for (int i = 0; i < count; i++)
            {
                if (test.Next(1).WasSuccessful)
                    ok++;
            }
            Console.WriteLine(ok / count * 4);

            var dictionary = new[] { 
                "Lorem", "Ipsum", "Dolor", "Sit", "Amet", "Am" }.Uniform();

            var lorem =
                from paragraphs in 3.RangeTo(7).Take(
                from sentences  in 5.RangeTo(9).Take(
                from words      in 7.RangeTo(20).TakeNoRepeat(dictionary)

                let first = words.First()
                let rest  = words.Skip(1).Select(w => w.ToLower())

                select first + " " + string.Join(" ", rest))
                select string.Join(". ", sentences) + '.')
                select string.Join("\n\n", paragraphs);

            Console.WriteLine(lorem.Single());
        }
    }

    public interface IRnd<A>
    {
        RndResult<A> Next(int tries);
    }

    public class ConstraintSatisfactionFailed : Exception { }

    public class RndResult<A>
    {
        public RndResult()
        {
            this.WasSuccessful = false;
        }

        public RndResult(A result)
        {
            this.WasSuccessful = true;
            this.result = result;
        }

        public bool WasSuccessful { get; private set; }

        private readonly A result;
        public A Result
        {
            get
            {
                if (WasSuccessful)
                    return result;
                throw new ConstraintSatisfactionFailed();
            }
        }

        public override string ToString()
        {
            if (!WasSuccessful)
                return "Nothing";
            if (result == null)
                return "null";
            return result.ToString();
        }

    }


    public static class Sx
    {
        public static IRnd<IEnumerable<A>> Take<A>(this IRnd<A> rnd, int count)
        {
            return Enumerable.Range(0, count).Select(_ => rnd).Sequence();
        }

        public static IRnd<IEnumerable<A>> Take<A>(this IRnd<int> count, IRnd<A> rnd)
        {
            return count.SelectMany(i => rnd.Take(i));
        }

        public static IRnd<IEnumerable<A>> TakeNoRepeat<A>(this int count, IRnd<A> rnd) 
            where A: IEquatable<A>
        {
            return new FromFunc<IEnumerable<A>>(tries => NoRepeat(count, tries, rnd));
        }

        public static IRnd<IEnumerable<A>> TakeNoRepeat<A>(this IRnd<int> count, IRnd<A> rnd) 
            where A: IEquatable<A>
        {
            return count.SelectMany(i => i.TakeNoRepeat(rnd));
        }

        private static RndResult<IEnumerable<A>> NoRepeat<A>(int count, int tries, IRnd<A> rnd) where A: IEquatable<A>
        {
            A last = default(A); 
            A[] arr = new A[count];
            for (int i = 0; i < count; i++)
            {
                var res = i == 0 
                    ? rnd.Next(tries) 
                    : rnd.Where(a => !a.Equals(last)).Next(tries);
                if (!res.WasSuccessful)
                    return new RndResult<IEnumerable<A>>();
                last = res.Result;
                arr[i] = last;
            }
            return new RndResult<IEnumerable<A>>(arr);
        }

        public static IRnd<IEnumerable<A>> Sequence<A>(this IEnumerable<IRnd<A>> seq)
        {
            return new FromFunc<IEnumerable<A>>(tries =>
                seq.Select(rnd => rnd.Next(tries)).Sequence()
            );
        }

        public static IRnd<B> Select<A, B>(this IRnd<A> source, Func<A, B> func)
        {
            return new FromFunc<B>(tries => 
                from a in source.Next(tries)
                select func(a)
            );
        }

        public static IRnd<B> SelectMany<A, B>(this IRnd<A> source, Func<A, IRnd<B>> func)
        {
            return source.SelectMany(func, (_, b) => b);
        }

        public static IRnd<C> SelectMany<A, B, C>(this IRnd<A> source, Func<A, IRnd<B>> func, Func<A,B,C> selector)
        {
            return new FromFunc<C>(tries => 
            {
                return from a in source.Next(tries)
                       from b in func(a).Next(tries)
                       select selector(a, b);
            });
        }

        public static IRnd<A> Where<A>(this IRnd<A> source, Func<A, bool> predicate)
        {
            return new FromFunc<A>(tries =>
            {
                for (int i = 0; i < tries; i++)
                {
                    var ra = source.Next(1);
                    if (!ra.WasSuccessful)
                        continue;
                    var a = ra.Result;
                    if (predicate(a))
                        return ra;
                }
                return new RndResult<A>();
            });
        }

        public static IRnd<A> Uniform<A>(this IEnumerable<A> @enum)
        {
            return new FromEnum<A>(@enum);
        }

        public static IRnd<int> RangeTo(this int from, int to)
        {
            return new FromFunc<int>(_ =>
                new RndResult<int>(Sx.Random.Next(from, to + 1))
            );
        }

        public static IRnd<double> RangeTo(this double from, double to)
        {
            return new FromFunc<double>(_ =>
                new RndResult<double>(Sx.Random.NextDouble() * (to - from) + from)
            );
        }

        public static A Single<A>(this IRnd<A> rnd)
        {
            return rnd.Next(Sx.NumberTries).Result;
        }

        public static A Single<A>(this IRnd<A> rnd, int tries)
        {
            return rnd.Next(tries).Result;
        }

        [ThreadStatic]
        private static Random rng;
        private static int lastSeed = new Random().Next();
        internal static Random Random
        {
            get { return rng ?? (rng = new Random(lastSeed = Interlocked.Increment(ref lastSeed))); }
        }

        public static int NumberTries = 100;
    }

    internal static class Maybe 
    {

        internal static RndResult<B> Select<A, B>(this RndResult<A> res, Func<A, B> func)
        {
            if (!res.WasSuccessful)
                return new RndResult<B>();
            return new RndResult<B>(func(res.Result));
        }

        internal static RndResult<C> SelectMany<A, B, C>(
            this RndResult<A> res,
            Func<A, RndResult<B>> func,
            Func<A, B, C> selector)
        {
            if (!res.WasSuccessful)
                return new RndResult<C>();
            var resB = func(res.Result);
            if (!resB.WasSuccessful)
                return new RndResult<C>();
            return new RndResult<C>(selector(res.Result, resB.Result));
        }

        internal static RndResult<B> SelectMany<A,B>(this RndResult<A> res, Func<A, RndResult<B>> func)
        {
            return res.SelectMany(func, (_, b) => b);
        }

        internal static RndResult<IEnumerable<A>> Sequence<A>(this IEnumerable<RndResult<A>> seq)
        {
            var rs = new List<A>();
            foreach (var res in seq)
            {
                if (!res.WasSuccessful)
                    return new RndResult<IEnumerable<A>>();
                rs.Add(res.Result);
            }
            return new RndResult<IEnumerable<A>>(rs);
        }
    }

    public class FromFunc<A> : IRnd<A>
    {
        private Func<int, RndResult<A>> action; 

        public FromFunc(Func<int, RndResult<A>> action)
        {
            this.action = action;
        }

        public RndResult<A> Next(int tries)
        {
            return action(tries);
        }
    }

    public class FromEnum<A> : IRnd<A>
    {
        private IEnumerable<A> ts { get; set; }
        private A[] arr;

        public FromEnum(IEnumerable<A> @enum)
        {
            this.ts = @enum;
        }

        public RndResult<A> Next(int tries)
        {
            arr = arr ?? ts.ToArray();
            return new RndResult<A>(arr[Sx.Random.Next(arr.Length)]);
        }
    }
}