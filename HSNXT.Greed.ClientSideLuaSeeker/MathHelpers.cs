using System;
using System.Collections.Generic;
using System.Linq;

namespace HSNXT.Greed.ClientSideLuaSeeker
{
    public static class MathHelpers
    {
        // https://stackoverflow.com/a/22702269
        
        /// <summary>
        /// Partitions the given list around a pivot element such that all elements on left of pivot are &lt;= pivot
        /// and the ones at thr right are > pivot. This method can be used for sorting, N-order statistics such as
        /// as median finding algorithms.
        /// Pivot is selected ranodmly if random number generator is supplied else its selected as last element in the list.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 171
        /// </summary>
        private static int Partition<T>(this IList<T> list, int start, int end, Random rnd = null)
            where T : IComparable<T>
        {
            if (rnd != null)
                list.Swap(end, rnd.Next(start, end + 1));

            var pivot = list[end];
            var lastLow = start - 1;
            for (var i = start; i < end; i++)
            {
                if (list[i].CompareTo(pivot) <= 0)
                    list.Swap(i, ++lastLow);
            }

            list.Swap(end, ++lastLow);
            return lastLow;
        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
        /// Note: specified list would be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        public static T NthOrderStatistic<T>(this IList<T> list, int n, Random rnd = null) where T : IComparable<T>
        {
            return NthOrderStatistic(list, n, 0, list.Count - 1, rnd);
        }

        private static T NthOrderStatistic<T>(this IList<T> list, int n, int start, int end, Random rnd)
            where T : IComparable<T>
        {
            while (true)
            {
                var pivotIndex = list.Partition(start, end, rnd);
                if (pivotIndex == n)
                    return list[pivotIndex];

                if (n < pivotIndex)
                    end = pivotIndex - 1;
                else
                    start = pivotIndex + 1;
            }
        }

        public static void Swap<T>(this IList<T> list, int i, int j)
        {
            if (i == j) //This check is not required but Partition function may make many calls so its for perf reason
                return;
            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }

        /// <summary>
        /// Note: specified list would be mutated in the process.
        /// </summary>
        public static T Median<T>(IList<T> list) where T : IComparable<T>
        {
            return list.NthOrderStatistic((list.Count - 1) / 2);
        }

        public static double Median<T>(IReadOnlyList<T> sequence, Func<T, double> getValue = null)
        {
            if (getValue == null) getValue = e => (double) (object) e;
            
            var arr = new double[sequence.Count];
            for (var i = 0; i < arr.Length; i++)
            {
                arr[i] = getValue(sequence[i]);
            }
            
            var mid = (arr.Length - 1) / 2;
            return arr.NthOrderStatistic(mid);
        }

        public static double Mean<T>(IReadOnlyList<T> sequence, Func<T, double> getValue = null)
        {
            if (getValue == null) getValue = e => (double) (object) e;

            return sequence.Sum(getValue) / sequence.Count;
        }
        
        // https://stackoverflow.com/a/2253903
        public static double StdDev<T>(IReadOnlyList<T> sequence, Func<T, double> getValue = null)
        {
            if (getValue == null) getValue = e => (double) (object) e;

            var arr = new double[sequence.Count];
            for (var i = 0; i < arr.Length; i++)
            {
                arr[i] = getValue(sequence[i]);
            }

            if (arr.Length <= 1) return 0;
            
            //Compute the Average
            var avg = arr.Average();

            //Perform the Sum of (value-avg)^2
            var sum = arr.Sum(d => (d - avg) * (d - avg));

            //Put it all together
            return Math.Sqrt(sum / arr.Length);
        }
    }
}