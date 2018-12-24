using System;
using System.Collections.Generic;
using System.Linq;

namespace Hspi.Utils
{
    internal static class EnumUtil
    {
        public static IEnumerable<T> GetValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }
    }
}