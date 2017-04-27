using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KiCadDoxer.Renderer.Extensions
{
    public static class StackExtensions
    {
        public static T PeekOrDefault<T>(this Stack<T> stack)
        {
            if (stack.Count == 0)
            {
                return default(T);
            }

            return stack.Peek();
        }
    }
}
