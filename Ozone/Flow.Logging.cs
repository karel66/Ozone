namespace Ozone
{
    /// <summary>
    /// Logging helpers.
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// When true, step traces log every captured argument value in full, including
        /// text typed into fields (passwords, tokens, personal data). Off by default, so only
        /// locator-like arguments are shown and other strings are logged as <c>***</c>.
        /// Intended for local debugging; do not enable where logs are stored or shared.
        /// </summary>
        public static bool TraceSensitiveValues { get; set; }

        public static string? Log(string? message)
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
