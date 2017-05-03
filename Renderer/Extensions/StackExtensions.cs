using System.Collections.Generic;

namespace KiCadDoxer.Renderer.Extensions
{
    internal static class StackExtensions
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