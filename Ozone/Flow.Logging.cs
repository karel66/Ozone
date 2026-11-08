using System.Text.RegularExpressions;

namespace Ozone
{
    /// <summary>
    /// Logging helpers.
    /// </summary>
    public partial class Flow
    {
        /// <summary>
        /// When true, step traces and problem messages log every string value in full: selectors,
        /// XPaths, searched text and text typed into fields (passwords, tokens, personal data).
        /// Off by default, so all of these are logged as <c>***</c>; only numbers, booleans and
        /// enums are shown. A wrapped exception's message (for example from Playwright) keeps only its
        /// first line, with quoted text replaced by <c>***</c>; the inner exception keeps the full message.
        /// Intended for local debugging; do not enable where logs are stored or shared.
        /// </summary>
        public static bool TraceSensitiveValues { get; set; }

        /// <summary>
        /// Formats a caller-supplied string (selector, XPath, searched text) for a log or problem
        /// message: quoted when <see cref="TraceSensitiveValues"/> is on, otherwise <c>***</c>.
        /// </summary>
        internal static string Sensitive(string? value) =>
            TraceSensitiveValues ? $"'{value}'" : "***";

        /// <summary>
        /// The message of an exception from outside Ozone (usually Playwright), for a log or problem
        /// message. Playwright messages quote the selector and add a call log with page HTML, so unless
        /// <see cref="TraceSensitiveValues"/> is on, only the first line is kept and quoted text in it is
        /// replaced with <c>***</c>. The exception itself keeps its full message.
        /// </summary>
        internal static string Sensitive(Exception x)
        {
            if(TraceSensitiveValues)
            {
                return x.Message;
            }

            string firstLine = x.Message.Split('\n', 2)[0].TrimEnd('\r');
            return QuotedText().Replace(firstLine, "***");
        }

        [GeneratedRegex(@"""[^""]*""|'[^']*'")]
        private static partial Regex QuotedText();

        /// <summary>
        /// Writes a timestamped message to the console.
        /// </summary>
        /// <returns>The message, so the call can be used inline.</returns>
        public static string? Log(string? message)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
            return message;
        }

        /// <summary>
        /// Writes a timestamped error message, with the exception details if given. The exception
        /// message is redacted unless <see cref="TraceSensitiveValues"/> is on.
        /// </summary>
        /// <returns>The formatted error message.</returns>
        public static string LogError(string message, Exception? x = null)
        {
            string result = "*** ERROR *** " + message;
            if (x != null) result += TraceSensitiveValues ? ": " + x : $": {x.GetType().Name}: {Sensitive(x)}{Environment.NewLine}{x.StackTrace}";
            Log(result);
            return result;
        }
    }
}
