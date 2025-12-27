namespace Ozone
{
    public class AsyncStep
    {
        readonly Func<Context, Task<Context>>? _step = null;
        AsyncStep? _next = null;
        Exception? _problem = null;

        public bool HasProblem => _problem != null;

        public AsyncStep(Func<Context, Task<Context>> step)
        {
            _step = step;
            _next = null;
        }

        /// <summary>
        /// Link next step function.
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
        /// Trigger chain of async steps by supplying inital context nad return the last successful step or the step with problem.
        /// </summary>
        public async Task<AsyncStep> Bind(Context context)
        {
            if (this.HasProblem)
            {
                return this;
            }

            try
            {
                if (_step != null)
                {
                    context = await _step(context);
                }

                if (_next != null)
                {
                    return await _next.Bind(context);
                }
            }

            catch (Exception ex)
            {
                _problem = ex;
            }

            return this;
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
    }
}
