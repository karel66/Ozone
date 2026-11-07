namespace Ozone
{
    public class OzoneException : Exception
    {
        public OzoneException()
        {
        }

        public OzoneException(string? message) : base(message)
        {
        }

        public OzoneException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
