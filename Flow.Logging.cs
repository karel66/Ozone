/*
* Oxygen.Flow.Playwright library
*/

using System;

namespace Ozone
{
    /// <summary>
    /// Logging helpers.
    /// </summary>
    public partial class Flow
    {
        public static string Log(string message)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
            return message;
        }

        public static string LogError(string message, Exception? x = null)
        {
            string result = "*** ERROR *** " + message;
            if (x != null) result += ": " + x;
            Log(result);
            return result;
        }
    }
}
