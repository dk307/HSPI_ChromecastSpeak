using System;
using System.Text;

namespace Hspi
{
    internal static class ExceptionHelper
    {
        public static string GetFullMessage(this Exception ex)
        {
            var aggregationException = ex as AggregateException;

            if (aggregationException != null)
            {
                var stb = new StringBuilder();

                foreach (var innerException in aggregationException.InnerExceptions)
                {
                    stb.AppendLine(GetFullMessage(innerException));
                }

                return stb.ToString();
            }
            else
            {
                return ex.Message;
            }
        }
    };
}