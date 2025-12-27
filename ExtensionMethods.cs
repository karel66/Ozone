using System.Text;

namespace Ozone
{
    public static class ExtensionMethods
    {
        public static StringBuilder AppendWithComma(this StringBuilder sb, string text)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
            return sb.Append(text);
        }

        public static AsyncStep AsStep(this Func<Context, Task<Context>> stepFunc)
        {
            return new AsyncStep(stepFunc);
        }
    }
}

