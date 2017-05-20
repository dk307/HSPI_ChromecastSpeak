using System;
using System.Text;

namespace Hspi
{
    using static System.FormattableString;

    internal static class ExceptionHelper
    {
        public static string GetFullMessage(this Exception ex)
        {
            switch (ex)
            {
                case AggregateException aggregationException:
                    var stb = new StringBuilder();
                    foreach (var innerException in aggregationException.InnerExceptions)
                    {
                        stb.AppendLine(GetFullMessage(innerException));
                    }
                    return stb.ToString();

                case SharpCaster.Exceptions.MediaLoadException mediaLoadException:
                    return Invariant(
                    $@"Failed to start to play voice on {mediaLoadException.DeviceName} with Error:{mediaLoadException.FailureType}.
                       Make sure firewall from Device to HomeSeer is not blocking it.");

                case SharpCaster.Exceptions.ChromecastDeviceException chromeDeviceException:
                    return Invariant($"Failed to play on to {chromeDeviceException.DeviceName} with {chromeDeviceException.Message}");

                default:
                    return ex.Message;
            }
        }
    };
}