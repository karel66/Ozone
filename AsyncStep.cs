using System.Text;

namespace Ozone
{
    /// <summary>
    /// Container for function Func&lt;Context,Task&lt;Context&gt;&gt;
    /// </summary>
    public class AsyncStep
    {
        readonly Func<Context, Task<Context>>? _step = null;
        AsyncStep? _next = null;
        Context? _context = null;

        /// <summary>
        /// Result of the step function on bound context.
        /// </summary>
        public Context? Context => _context;

        public AsyncStep(Func<Context, Task<Context>> step)
        {
            _step = step;
            _next = null;
        }

        /// <summary>
        /// Trace execution
        /// </summary>
        /// <returns></returns>
        public string? MethodTrace()
        {
            if (_step == null)
            {
                return null;
            }

            var name = _step.Method.Name;

            if (name[0] == '<')
            {
                name = name[1..name.IndexOf('>')];
            }

            return $"{name}({FormatTarget(_step.Target)})";
        }

        /// <summary>
        /// Link next step to the chain.
        /// </summary>
        public AsyncStep Link(Func<Context, Task<Context>> next) => Link(new AsyncStep(next));

        /// <summary>
        /// Link next step.
        /// </summary>
        public AsyncStep Link(AsyncStep next)
        {
            if (_next == null)
            {
                _next = next;
            }
            else
            {
                _next.Link(next);
            }

            return this;
        }

        /// <summary>
        /// Trigger chain of async steps by supplying inital context
        /// </summary>
        public async Task<AsyncStep> Bind(Context context)
        {
            if (_step != null)
            {
                Flow.Log(MethodTrace());
                _context = await _step(context);
            }
            else // this is empty step
            {
                _context = context;
            }

            if (_next != null)
            {
                return await _next.Bind(_context);
            }
            else // this is last step
            {
                return this;
            }
        }

        /// <summary>
        /// Bind() method shortcut.
        /// </summary>
        public static Task<AsyncStep> operator |(Context context, AsyncStep step)
        {
            return step.Bind(context);
        }

        /// <summary>
        /// Link() method shortcut.
        /// </summary>
        public static AsyncStep operator |(AsyncStep first, Func<Context, Task<Context>> next)
        {
            return first.Link(next);
        }

        /// <summary>
        /// Link() method shortcut.
        /// </summary>
        public static AsyncStep operator |(AsyncStep first, AsyncStep next)
        {
            return first.Link(next);
        }

        static string FormatTarget(object? target)
        {
            StringBuilder args = new();

            if (target == null) return args.ToString();

            Type type = target.GetType();

            foreach (var field in type.GetFields())
            {
                object? argval = field.GetValue(target);

                if (argval == null)
                {
                    args.AppendWithComma($"{field.Name}=null");
                }
                else if (field.FieldType == typeof(string))
                {
                    args.AppendWithComma($"{field.Name}=\"{argval}\"");
                }
                else if (field.FieldType == typeof(char))
                {
                    args.AppendWithComma($"{field.Name}='{argval}'");
                }
                else if (field.FieldType.IsValueType)
                {
                    args.AppendWithComma($"{field.Name}={argval}");
                }
                else if (field.FieldType.IsArray)
                {
                    args.AppendWithComma($"{field.Name}=[");
                    foreach (object value in (Array)argval)
                    {
                        args.Append($"\"{value}\", ");
                    }
                    args.Append(']');
                }
                else if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    args.AppendWithComma($"{field.Name}=[{field.FieldType.Name}]");
                }
                else
                {
                    args.AppendWithComma($"{field.Name}={{");
                    foreach (var prop in field.FieldType.GetProperties().Select(p => p.Name))
                    {
                        try
                        {
                            object? val = field.FieldType
                                .InvokeMember(prop, System.Reflection.BindingFlags.GetProperty, null, argval, null, null);
                            args.Append($"{prop}:\"{val}\", ");
                        }
                        catch (Exception x)
                        {
                            args.Append($"{prop}:\"<Exception of type {x.GetType().Name}>\", ");
                        }
                    }
                    args.Append('}');
                }
            }

            return args.ToString();
        }

        static string ExtractMethodName(string reflectedName)
        {
            string name = reflectedName;
            if (!string.IsNullOrEmpty(name) && name[0] == '<')
            {
                name = name[1..name.IndexOf('>')];
            }
            return name;
        }

    }
}