namespace Ozone
{
    /// <summary>
    /// Raised when a flow step fails. Wraps the original exception, if any, as <see cref="Exception.InnerException"/>.
    /// </summary>
    public class OzoneException : Exception
    {
        /// <summary>Creates an exception with the default message.</summary>
        public OzoneException()
        {
        }

        /// <summary>Creates an exception with the given message.</summary>
        public OzoneException(string? message) : base(message)
        {
        }

        /// <summary>Creates an exception with the given message and the exception that caused it.</summary>
        public OzoneException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
