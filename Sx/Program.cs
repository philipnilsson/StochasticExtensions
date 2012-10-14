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
            var test = from x in (-10).RangeTo(10)
                       from y in (-10).RangeTo(10)
                       let z = x*x + y*y
                       where z >= 5*5
                       where z <= 8*8 
                       select new { x = x, y = y };

            foreach (var t in test.Take(10).Next())
                Console.WriteLine(t);

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
             
            Console.WriteLine(lorem.Next());
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

        private static IEnumerable<A> NoRepeat<A>(int count, int tries, IRnd<A> rnd) where A: IEquatable<A>
        {
            A last = default(A); 
            A[] arr = new A[count];
            for (int i = 0; i < count; i++)
            {
                last = i == 0 
                    ? rnd.Next(tries) 
                    : rnd.Where(a => !a.Equals(last)).Next(tries);
                arr[i] = last;
            }
            return arr;
        }

        public static IRnd<IEnumerable<A>> Sequence<A>(this IEnumerable<IRnd<A>> seq)
        {
            return new FromFunc<IEnumerable<A>>(tries =>
                seq.Select(rnd => rnd.Next(tries))
            );
        }

        public static IRnd<B> Select<A, B>(this IRnd<A> source, Func<A, B> func)
        {
            return new FromFunc<B>(tries => 
                func(source.Next(tries))
            );
        }

        public static IRnd<B> SelectMany<A, B>(this IRnd<A> source, Func<A, IRnd<B>> func)
        {
            return source.SelectMany(func, (_, b) => b);
        }

        public static IRnd<C> SelectMany<A, B, C>(this IRnd<A> source, Func<A, IRnd<B>> func, Func<A,B,C> select)
        {
            return new FromFunc<C>(tries => 
            {
                var a = source.Next(tries);
                return select(a, func(a).Next(tries));
            });
        }

        public static IRnd<A> Where<A>(this IRnd<A> source, Func<A, bool> predicate)
        {
            return new FromFunc<A>(tries =>
            {
                for (int i = 0; i < tries; i++)
                {
                    var a = source.Next(1);
                    if (predicate(a))
                        return a;
                    Console.WriteLine("Rejected: " + a);
                }
                throw new Exception(
                    "Sampling exited after " + Sx.NumberTries + " tries.");
            });
        }

        public static IRnd<A> Uniform<A>(this IEnumerable<A> @enum)
        {
            return new FromEnum<A>(@enum);
        }

        public static IRnd<int> RangeTo(this int from, int to)
        {
            return new FromFunc<int>(_ =>
                Sx.Random.Next(from, to + 1)
            );
        }

        public static A Next<A>(this IRnd<A> rnd)
        {
            return rnd.Next(Sx.NumberTries);
        }

        [ThreadStatic]
        private static Random rng;
        private static int lastSeed = new Random().Next();
        internal static Random Random
        {
            get { return rng ?? (rng = new Random(lastSeed = Interlocked.Increment(ref lastSeed))); }
        }

        [ThreadStatic]
        public static int NumberTries = 100;
    }

    public interface IRnd<A>
    {
        A Next(int tries);
    }

    public class ConstraintSatisfactionFailed : Exception { }

    public class RndResult<A>
    {
        private A result;

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

        public A GetResult()
        {
            if (!WasSuccessful)
                throw new ConstraintSatisfactionFailed();
            return result;
        }
    }

    public class FromFunc<T> : IRnd<T>
    {
        private Func<int, T> action; 

        public FromFunc(Func<int, T> action)
        {
            this.action = action;
        }

        public T Next(int tries)
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

        public A Next(int tries)
        {
            arr = arr ?? ts.ToArray();
            return arr[Sx.Random.Next(arr.Length)];
        }
    }
}