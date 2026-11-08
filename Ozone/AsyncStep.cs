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
        readonly AsyncStep? _subChain = null;
        AsyncStep? _next = null;
        Context? _context = null;
        bool _stopped = false;

        /// <summary>
        /// Result of the step function on bound context.
        /// </summary>
        public Context? Context => _context;

        /// <summary>
        /// True when this step is a gate (see <see cref="BreakUnless"/>) that stopped the chain on
        /// the last <see cref="Bind"/>, so the steps after it did not run. <see cref="Bind"/> returns the
        /// gate in that case, so check this on its result to tell a stopped chain from a finished one.
        /// </summary>
        public bool Stopped => _stopped;

        /// <summary>
        /// Wraps a step so it can be chained with <c>
        /// </c>.
        /// </summary>
        public AsyncStep(Func<Context, Task<Context>> step)
        {
            _step = step;
            _next = null;
        }

        AsyncStep(string name, Func<Context, Task<bool>> proceed)
        {
            _gateName = name;
            _proceed = proceed;
        }

        // Runs a linked multi-step chain as one step. It has no trace line of its own: its steps trace themselves.
        AsyncStep(AsyncStep subChain)
        {
            _subChain = subChain;
        }

        /// <summary>
        /// Creates a gate: when the chain reaches it, the rest of the chain runs only if
        /// <paramref name="proceed"/> returns true. Otherwise the chain stops here: <see cref="Bind"/>
        /// returns the gate, with <see cref="Stopped"/> set and the gate's input context as
        /// <see cref="Context"/>. The context passes through unchanged when the chain goes on.
        /// A gate stops only the chain it is linked into: a multi-step chain linked as the right-hand
        /// operand of <c>|</c> runs as one step, so in <c>a | (gate | b) | c</c> a stop skips <c>b</c> but
        /// not <c>c</c>. <see cref="Flow.While(Func{Ozone.Context, Task{bool}}, AsyncStep, int)"/> ends its
        /// loop when the chain stops. An exception thrown by <paramref name="proceed"/> fails the chain
        /// with <see cref="OzoneException"/>.
        /// <code>
        /// ctx | (AsyncStep.BreakUnless("HasNext", c => Flow.ExistsOnXPath(c, "//a[@rel='next']")) | Flow.FindOnXPath("//a[@rel='next']") | Flow.Click)
        /// </code>
        /// </summary>
        /// <param name="name">Step name for the trace; the arguments come from <paramref name="proceed"/>'s captured values.</param>
        /// <param name="proceed">Decides whether the rest of the chain runs.</param>
        public static AsyncStep BreakUnless(string name, Func<Context, Task<bool>> proceed)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(proceed);
            return new AsyncStep(name, proceed);
        }

        /// <summary>
        /// Trace execution
        /// </summary>
        /// <returns>The step name and its arguments, or null for a step that is not traced itself (an empty step or a nested chain).</returns>
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
        /// Link next step. A single step joins this chain; a chain of several steps is linked as
        /// one step (it runs as a whole, and a gate in it stops only that chain).
        /// </summary>
        public AsyncStep Link(AsyncStep next)
        {
            ArgumentNullException.ThrowIfNull(next);

            if (next._next != null)
            {
                next = new AsyncStep(subChain: next);
            }

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
            _stopped = false;

            if (_step != null)
            {
                string? trace = Flow.Log(MethodTrace());
                _context = await _step(context)
                    ?? context.CreateProblem($"{trace}: step returned no context");
            }
            else if (_proceed != null) // this is a gate
            {
                string? trace = Flow.Log(MethodTrace());
                bool proceed = false;

                try
                {
                    proceed = await _proceed(context);
                }
                catch (Exception x)
                {
                    context.CreateProblem(x); // always throws OzoneException
                }

                _context = context;

                if (!proceed)
                {
                    Flow.Log($"{trace}: rest of the chain skipped");
                    _stopped = true;
                    return this;
                }
            }
            else if (_subChain != null)
            {
                // Bind fails if a step returns no context, so Context is set. A stop inside the
                // nested chain ends only that chain, so this step's Stopped stays false.
                _context = (await _subChain.Bind(context)).Context!;
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
