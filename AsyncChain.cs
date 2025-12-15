
using System.Runtime.CompilerServices;

namespace Ozone
{
    public class AsyncChain
    {
        private readonly Func<Task<Context>> _taskSource;

        private AsyncChain(Func<Task<Context>> source)
        {
            _taskSource = source;
        }
        private AsyncChain(Context context)
        {
            _taskSource = async () => context;
        }

        public static AsyncChain From(Func<Task<Context>> source) => new(source);
        public static AsyncChain From(Context context) => new(context);

        public Task<Context> RunAsync() => _taskSource();

        public TaskAwaiter<Context> GetAwaiter() => _taskSource().GetAwaiter(); // enables await chain

        public AsyncChain Bind(Func<Context, Task<Context>> next)
        {
            var taskSource = _taskSource;

            return new AsyncChain(async () =>
            {
                var context = await taskSource();

                if (context.HasProblem)
                {
                    return context;
                }
                else
                {
                    try
                    {
                        return await next(context);
                    }
                    catch (Exception ex)
                    {
                        return context.CreateProblem(ex);
                    }
                }
            });
        }

        public static AsyncChain operator |(AsyncChain step, Func<Context, Task<Context>> next) => step.Bind(next);

    }

}
