using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncBridge
{
    using EventTask = Tuple<SendOrPostCallback, object>;
    using EventQueue = ConcurrentQueue<Tuple<SendOrPostCallback, object>>;

    /// <summary>
    /// A Helper class to run Asynchronous functions from synchronous ones
    /// </summary>
    public static class AsyncHelper
    {
        /// <summary>
        /// A class to bridge synchronous asynchronous methods
        /// </summary>
        public class AsyncBridge : IDisposable
        {
            private ExclusiveSynchronizationContext CurrentContext;
            private SynchronizationContext OldContext;
            private int TaskCount;

            /// <summary>
            /// Constructs the AsyncBridge by capturing the current
            /// SynchronizationContext and replacing it with a new
            /// ExclusiveSynchronizationContext.
            /// </summary>
            internal AsyncBridge()
            {
                OldContext = SynchronizationContext.Current;
                CurrentContext = 
                    new ExclusiveSynchronizationContext(OldContext);
                SynchronizationContext
                    .SetSynchronizationContext(CurrentContext);
            }

            /// <summary>
            /// Execute's an async task with a void return type
            /// from a synchronous context
            /// </summary>
            /// <param name="task">Task to execute</param>
            /// <param name="callback">Optional callback</param>
            public void Run(Task task, Action<Task> callback = null)
            {
                CurrentContext.Post(async _ =>
                {
                    try
                    {
                        Increment();
                        await task;

                        if (null != callback)
                        {
                            callback(task);
                        }
                    }
                    catch (Exception e)
                    {
                        CurrentContext.InnerException = e;
                    }
                    finally
                    {
                        Decrement();
                    }
                }, null);
            }

            /// <summary>
            /// Execute's an async task with a T return type
            /// from a synchronous context
            /// </summary>
            /// <typeparam name="T">The type of the task</typeparam>
            /// <param name="task">Task to execute</param>
            /// <param name="callback">Optional callback</param>
            public void Run<T>(Task<T> task, Action<Task<T>> callback = null)
            {
                if (null != callback)
                {
                    Run((Task)task, (finishedTask) =>
                        callback((Task<T>)finishedTask));
                }
                else
                {
                    Run((Task)task);
                }
            }

            /// <summary>
            /// Execute's an async task with a T return type
            /// from a synchronous context
            /// </summary>
            /// <typeparam name="T">The type of the task</typeparam>
            /// <param name="task">Task to execute</param>
            /// <param name="callback">
            /// The callback function that uses the result of the task
            /// </param>
            public void Run<T>(Task<T> task, Action<T> callback)
            {
                if (null != callback)
                {
                    Run(task, (t) => callback(t.Result));
                }
                else
                {
                    Run((Task)task);
                }
            }

            private void Increment()
            {
                Interlocked.Increment(ref TaskCount);
            }

            private void Decrement()
            {
                Interlocked.Decrement(ref TaskCount);
                if (TaskCount == 0)
                {
                    CurrentContext.EndMessageLoop();
                }
            }

            /// <summary>
            /// Disposes the object
            /// </summary>
            public void Dispose()
            {
                try
                {
                    CurrentContext.BeginMessageLoop();
                }
                finally
                {
                    SynchronizationContext
                        .SetSynchronizationContext(OldContext);
                }
            }
        }

        /// <summary>
        /// Creates a new AsyncBridge. This should always be used in
        /// conjunction with the using statement, to ensure it is disposed
        /// </summary>
        public static AsyncBridge Wait
        {
            get { return new AsyncBridge(); }
        }

        private static void Try(Action body)
        {
            try
            {
                body();
            }
            catch (AsyncBridgeUnwindException e)
            {
                var context = SynchronizationContext.Current
                    as ExclusiveSynchronizationContext;

                if (null != context && e.Depth > context.Depth)
                {
                    throw;
                }

                throw e.InnerException;
            }
        }

        private static void Try<E>(Action body, Action<E> onError)
            where E: Exception
        {
            try
            {
                Try(body);
            }
            catch (AsyncBridgeUnwindException e)
            {
                throw;
            }
            catch (E e)
            {
                onError(e);
            }
        }

        private static void Try<EOne, ETwo>(
            Action body,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo)
            where EOne : Exception
            where ETwo : Exception
        {
            try
            {
                Try(body);
            }
            catch (AsyncBridgeUnwindException e)
            {
                throw;
            }
            catch (EOne e)
            {
                onErrorOne(e);
            }
            catch (ETwo e)
            {
                onErrorTwo(e);
            }
        }

        private static void Try<EOne, ETwo, EThree>(
            Action body,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo,
            Action<EThree> onErrorThree)
            where EOne : Exception
            where ETwo : Exception
            where EThree : Exception
        {
            try
            {
                Try(body);
            }
            catch (AsyncBridgeUnwindException e)
            {
                throw;
            }
            catch (EOne e)
            {
                onErrorOne(e);
            }
            catch (ETwo e)
            {
                onErrorTwo(e);
            }
            catch (EThree e)
            {
                onErrorThree(e);
            }
        }

        private static T Try<T>(Func<T> body)
        {
            T result = default(T);
            Try(delegate { result = body(); });
            return result;
        }

        private static T Try<T,E>(Func<T> body, Action<E> onError)
            where E : Exception
        {
            T result = default(T);
            Try(delegate { result = body(); }, onError);
            return result;
        }

        private static T Try<T, EOne, ETwo>(
            Func<T> body,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo)
            where EOne : Exception
            where ETwo : Exception
        {
            T result = default(T);
            Try(delegate { result = body(); }, onErrorOne, onErrorTwo);
            return result;
        }

        private static T Try<T, EOne, ETwo, EThree>(
            Func<T> body,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo,
            Action<EThree> onErrorThree)
            where EOne : Exception
            where ETwo : Exception
            where EThree : Exception
        {
            T result = default(T);
            Try(delegate { result = body(); },
                onErrorOne,
                onErrorTwo,
                onErrorThree);
            return result;
        }

        private static void RunInternal(Action<AsyncBridge> body)
        {
            using (var Async = Wait)
            {
                body(Async);
            }
        }

        /// <summary>
        /// Runs the body from the provided lambda with a new AsyncBridge
        /// </summary>
        public static void Run(Action<AsyncBridge> body)
        {
            Try(() => RunInternal(body));
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error type in onError.
        /// </summary>
        public static void Run<E>(
            Action<AsyncBridge> body,
            Action<E> onError)
            where E : Exception
        {
            Try(() => RunInternal(body), onError);
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error types in onError.
        /// </summary>
        public static void Run<EOne, ETwo>(
            Action<AsyncBridge> body,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo)
            where EOne : Exception
            where ETwo : Exception
        {
            Try(() => RunInternal(body), onErrorOne, onErrorTwo);
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error types in onError.
        /// </summary>
        public static void Run<EOne, ETwo, EThree>(
            Action<AsyncBridge> body,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo,
            Action<EThree> onErrorThree)
            where EOne : Exception
            where ETwo : Exception
            where EThree : Exception
        {
            Try(() => RunInternal(body), onErrorOne, onErrorTwo, onErrorThree);
        }

        private static Task RunInternal(Func<Task> task)
        {
            Task t;
            using (var Async = Wait)
            {
                Async.Run((t = task()));
            }
            return t;
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously.
        /// </summary>
        public static void Run(Func<Task> task)
        {
            Try(() => RunInternal(task));
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error type in onError.
        /// </summary>
        public static void Run<E>(
            Func<Task> task,
            Action<E> onError)
            where E: Exception
        {
            Try(() => RunInternal(task), onError);
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error types in onError.
        /// </summary>
        public static void Run<EOne,ETwo>(
            Func<Task> task,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo)
            where EOne : Exception
            where ETwo : Exception
        {
            Try(() => RunInternal(task), onErrorOne, onErrorTwo);
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error types in onError.
        /// </summary>
        public static void Run<EOne, ETwo, EThree>(
            Func<Task> task,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo,
            Action<EThree> onErrorThree)
            where EOne : Exception
            where ETwo : Exception
            where EThree : Exception
        {
            Try(() => RunInternal(task), onErrorOne, onErrorTwo, onErrorThree);
        }

        private static T RunInternal<T>(Func<Task<T>> task)
        {
            T value = default(T);
            using (var Async = Wait)
            {
                Async.Run(task(), res => value = res);
            }
            return value;
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously.
        /// </summary>
        public static T Run<T>(Func<Task<T>> task)
        {
            return Try(() => RunInternal(task));
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error type in onError.
        /// </summary>
        public static T Run<T, E>(
            Func<Task<T>> task,
            Action<E> onError)
            where E : Exception
        {
            return Try(() => RunInternal(task), onError);
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error types in onError.
        /// </summary>
        public static T Run<T, EOne, ETwo>(
            Func<Task<T>> task,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo)
            where EOne : Exception
            where ETwo : Exception
        {
            return Try(() => RunInternal(task), onErrorOne, onErrorTwo);
        }

        /// <summary>
        /// Runs the Task from the provided function asynchronously. Catches
        /// the specified error types in onError.
        /// </summary>
        public static T Run<T, EOne, ETwo, EThree>(
            Func<Task<T>> task,
            Action<EOne> onErrorOne,
            Action<ETwo> onErrorTwo,
            Action<EThree> onErrorThree)
            where EOne : Exception
            where ETwo : Exception
            where EThree : Exception
        {
            return Try(() =>
                RunInternal(task),
                onErrorOne,
                onErrorTwo,
                onErrorThree);
        }

        /// <summary>
        /// Runs a task with the "Fire and Forget" pattern using Task.Run,
        /// and unwraps and handles exceptions
        /// </summary>
        /// <param name="task">A function that returns the task to run</param>
        /// <param name="handle">Error handling action, null by default</param>
        public static void FireAndForget(
            Func<Task> task,
            Action<Exception> handle = null)
        {
#if NET_45
            Task.Run(
#elif NET_40
            TaskEx.Run(
#endif
            () =>
            {
                ((Func<Task>)(async () =>
                {
                    try
                    {
                        await task();
                    }
                    catch (Exception e)
                    {
                        if (null != handle)
                        {
                            handle(e);
                        }
                    }
                }))();
            });
        }

        private class AsyncBridgeUnwindException : Exception
        {
            public AsyncBridgeUnwindException(
                int depth,
                Exception innerException,
                string message = "An exception was thrown in AsyncBridge.Run")
                : base(message, innerException)
            {
                Depth = depth;
            }
            public int Depth { get; private set; }
        }

        private class ExclusiveSynchronizationContext : SynchronizationContext
        {
            private readonly AutoResetEvent _workItemsWaiting =
                new AutoResetEvent(false);

            private bool _done;
            private EventQueue _items;

            protected ExclusiveSynchronizationContext _parent;
            protected ConcurrentBag<ExclusiveSynchronizationContext> _children;

            public Exception InnerException { get; internal set; }
            public int Depth { get; private set; }

            public ExclusiveSynchronizationContext(SynchronizationContext old)
            {
                ExclusiveSynchronizationContext oldEx =
                    old as ExclusiveSynchronizationContext;

                this._children = new ConcurrentBag<ExclusiveSynchronizationContext>();

                if (null != oldEx)
                {
                    this._parent = oldEx;
                    this._items = oldEx._items;
                    this._parent._children.Add(this);
                    this.Depth = this._parent.Depth + 1;
                }
                else
                {
                    this._items = new EventQueue();
                    this.Depth = 0;
                }
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException(
                    "We cannot send to our same thread");
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                _items.Enqueue(Tuple.Create(d, state));
                _workItemsWaiting.Set();
            }

            public void EndMessageLoop()
            {
                Post(_ => _done = true, null);
            }

            public void BeginMessageLoop()
            {
                while (!_done)
                {
                    EventTask task = null;

                    if (!_items.TryDequeue(out task))
                    {
                        task = null;
                    }

                    if (task != null)
                    {
                        task.Item1(task.Item2);
                        if (InnerException != null) // method threw an exeption
                        {
                            if (InnerException.GetType()
                                == typeof(AsyncBridgeUnwindException))
                            {
                                throw InnerException;
                            }

                            throw new AggregateException(
                                "AsyncBridge.Run method threw an exception.",
                                InnerException);
                        }
                    }
                    else
                    {
                        _workItemsWaiting.WaitOne();
                    }
                }
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }
        }
    }
}
