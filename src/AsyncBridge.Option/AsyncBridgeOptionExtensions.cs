using Functional.Option;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AsyncBridge
{
    /// <summary>
    /// Async Bridge Option Extensions
    /// </summary>
    public static class AsyncBridgeOptionExtensions
    {
        /// <summary>
        /// Run
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="A"></param>
        /// <param name="task"></param>
        /// <param name="option"></param>
        public static void Run<T>(
            this AsyncHelper.AsyncBridge A, Task<T> task, out Option<T> option)
        {
            var _option = Option.Some(default(T));
            option = _option;
            A.Run(task, (result) => SetOptionValue(_option, result));
        }

        /// <summary>
        /// Set Option Value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="option"></param>
        /// <param name="value"></param>
        private static void SetOptionValue<T>(Option<T> option, T value)
        {
            typeof(Option<T>).GetTypeInfo().DeclaredFields.First(x => x.Name == "_value").SetValue(option, value);
        }
    }
}
