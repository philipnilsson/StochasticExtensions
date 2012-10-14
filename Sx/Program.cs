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
                       where x*x + y*y < 5*5
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
            return new FromFunc<IEnumerable<A>>(() => NoRepeat(count, rnd));
        }

        public static IRnd<IEnumerable<A>> TakeNoRepeat<A>(this IRnd<int> count, IRnd<A> rnd) 
            where A: IEquatable<A>
        {
            return count.SelectMany(i => i.TakeNoRepeat(rnd));
        }

        private static IEnumerable<A> NoRepeat<A>(int count, IRnd<A> rnd) where A: IEquatable<A>
        {
            A last = default(A); 
            A[] arr = new A[count];
            for (int i = 0; i < count; i++)
            {
                last = i == 0 
                    ? rnd.Next() 
                    : rnd.Where(a => !a.Equals(last)).Next();
                arr[i] = last;
            }
            return arr;
        }

        public static IRnd<IEnumerable<A>> Sequence<A>(this IEnumerable<IRnd<A>> seq)
        {
            return new FromFunc<IEnumerable<A>>(() =>
                seq.Select(rnd => rnd.Next())
            );
        }

        public static IRnd<B> Select<A, B>(this IRnd<A> source, Func<A, B> func)
        {
            return new FromFunc<B>(() => 
                func(source.Next())
            );
        }

        public static IRnd<B> SelectMany<A, B>(this IRnd<A> source, Func<A, IRnd<B>> func)
        {
            return source.SelectMany(func, (_, b) => b);
        }

        public static IRnd<C> SelectMany<A, B, C>(this IRnd<A> source, Func<A, IRnd<B>> func, Func<A,B,C> select)
        {
            return new FromFunc<C>(() => 
            {
                var a = source.Next();
                return select(a, func(a).Next());
            });
        }

        public static IRnd<A> Where<A>(this IRnd<A> source, Func<A, bool> predicate)
        {
            return new FromFunc<A>(() =>
            {
                for (int i = 0; i < Sx.NumberTries; i++)
                {
                    var a = source.Next();
                    if (predicate(a))
                        return a;
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
            return new FromFunc<int>(() =>
                Sx.Random.Next(from, to + 1)
            );
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
        A Next();
    }

    public class FromFunc<T> : IRnd<T>
    {
        private Func<T> action; 

        public FromFunc(Func<T> action)
        {
            this.action = action;
        }

        public T Next()
        {
            return action();
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

        public A Next()
        {
            arr = arr ?? ts.ToArray();
            return arr[Sx.Random.Next(arr.Length)];
        }
    }
}