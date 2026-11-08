using System.Text;

namespace Ozone
{
    /// <summary>
    /// Container for function Func&lt;Context,Task&lt;Context&gt;&gt;
    /// </summary>
    public class AsyncStep
    {
        readonly Func<Context, Task<Context>>? _step = null;
        readonly Func<Context, Task<bool>>? _proceed = null;
        readonly string? _gateName = null;
        AsyncStep? _next = null;
        Context? _context = null;

        /// <summary>
        /// Result of the step function on bound context.
        /// </summary>
        public Context? Context => _context;

        /// <summary>
        /// Wraps a step so it can be chained with <c>
        /// </c>.
        /// </summary>
        public AsyncStep(Func<Context, Task<Context>> step)
        {
            _step = step;
            _next = null;
        }

        /// <summary>
        /// Creates a gate: when the chain reaches it, the rest of the chain runs only if
        /// <paramref name="proceed"/> returns true. Otherwise the chain stops here with the
        /// gate's input context as its result. The chain itself is not changed, so it can be
        /// run again (for example by <see cref="Flow.While(Func{Ozone.Context, Task{bool}}, AsyncStep, int)"/>).
        /// </summary>
        /// <param name="name">Step name for the trace; the arguments come from <paramref name="proceed"/>'s captured values.</param>
        /// <param name="proceed">Decides whether the rest of the chain runs.</param>
        internal AsyncStep(string name, Func<Context, Task<bool>> proceed)
        {
            _gateName = name;
            _proceed = proceed;
        }

        /// <summary>
        /// Trace execution
        /// </summary>
        /// <returns></returns>
        public string? MethodTrace()
        {
            if (_proceed != null)
            {
                return $"{_gateName}({FormatTarget(_proceed.Target)})";
            }

            if (_step == null)
            {
                return null;
            }

            return $"{ExtractMethodName(_step.Method.Name)}({FormatTarget(_step.Target)})";
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
        /// <exception cref="OzoneException">A step failed or returned no context.</exception>
        public async Task<AsyncStep> Bind(Context context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (_step != null)
            {
                string? trace = Flow.Log(MethodTrace());
                _context = await _step(context)
                    ?? context.CreateProblem($"{trace}: step returned no context");
            }
            else if (_proceed != null) // this is a gate
            {
                string? trace = Flow.Log(MethodTrace());
                _context = context;

                if (!await _proceed(context))
                {
                    Flow.Log($"{trace}: rest of the chain skipped");
                    return this;
                }
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

        
        /// <summary>
        /// Formats the closure's captured arguments for the trace log. By default only
        /// primitive values are printed; strings (selectors included), chars and arrays
        /// are redacted. <see cref="Flow.TraceSensitiveValues"/> prints them all.
        /// Objects are always shown by type only.
        /// </summary>
        static string FormatTarget(object? target)
        {
            StringBuilder args = new();

            if (target == null) return args.ToString();

            bool showAll = Flow.TraceSensitiveValues;

            foreach (var field in target.GetType().GetFields())
            {
                object? argval = field.GetValue(target);
                Type fieldType = field.FieldType;

                if (argval == null)
                {
                    args.AppendWithComma($"{field.Name}=null");
                }
                else if (fieldType == typeof(string))
                {
                    args.AppendWithComma(showAll ? $"{field.Name}=\"{argval}\"" : $"{field.Name}=***");
                }
                else if (fieldType.IsPrimitive || fieldType.IsEnum)
                {
                    args.AppendWithComma(fieldType != typeof(char) ? $"{field.Name}={argval}"
                        : showAll ? $"{field.Name}='{argval}'" : $"{field.Name}=***");
                }
                else if (fieldType.IsArray)
                {
                    var array = (Array)argval;
                    args.AppendWithComma(showAll
                        ? $"{field.Name}=[{string.Join(", ", array.Cast<object?>().Select(v => $"\"{v}\""))}]"
                        : $"{field.Name}=[{array.Length} item(s)]");
                }
                else
                {
                    args.AppendWithComma($"{field.Name}=[{fieldType.Name}]");
                }
            }

            return args.ToString();
        }

        static string ExtractMethodName(string reflectedName)
        {
            // Compiler-generated lambda names look like "<Find>b__0_0"; keep the enclosing method name.
            if (reflectedName.Length > 0 && reflectedName[0] == '<')
            {
                int end = reflectedName.IndexOf('>');
                if (end > 1)
                {
                    return reflectedName[1..end];
                }
            }

            return reflectedName;
        }

    }
}
