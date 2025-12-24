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
        /// Bind next step function
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public AsyncStep Bind(Func<Context, Task<Context>> next)
        {
            _next = new AsyncStep(next);
            return _next;
        }

        /// <summary>
        /// Bind next step
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public AsyncStep Bind(AsyncStep next)
        {
            _next = next;
            return _next;
        }

        /// <summary>
        /// Trigger chain of async steps by supplying inital context
        /// </summary>
        public async Task<AsyncStep> Bind(Context context)
        {
            if (this.HasProblem)
            {
                return this;
            }

            if (_next == null)
            {
                this._problem = new InvalidOperationException("No next step defined");
                return this;
            }

            if(_step == null)
            {
                this._problem = new InvalidOperationException("No step function defined");
                return this;
            }

            try
            {
                context = await _step(context);
                return await _next.Bind(context);
            }

            catch (Exception ex)
            {
                _problem = ex;
                return this;
            }

        }

        /// <summary>
        /// Trigger chain of async steps by supplying inital context
        /// </summary>
        public static Task<AsyncStep> operator |(Context context, AsyncStep step)
        {
            return step.Bind(context);
        }

        public static AsyncStep operator |(AsyncStep first, Func<Context, Task<Context>> next)
        {
            return first.Bind(next);
        }

        public static AsyncStep operator |(AsyncStep first, AsyncStep next)
        {
            return first.Bind(next);
        }
    }
}
