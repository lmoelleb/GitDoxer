using System;
using System.Collections.Generic;

namespace KiCadDoxer.Renderer.Extensions
{
    internal static class ArrayExtensions
    {
        public static IEnumerable<T> AsEnumerable<T>(this Array array)
        {
            foreach (T item in array)
            {
                yield return item;
            }
        }
    }
}