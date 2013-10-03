using System;
using System.Collections.Generic;
using System.Linq;
using System.Option;
using System.Text;
using System.Threading.Tasks;

namespace AsyncBridge
{
    public static class AsyncBridgeOptionExtensions
    {
        public static void Run<T>(
            this AsyncHelper.AsyncBridge A, Task<T> task, out Option<T> option)
        {
            var _option = Option.Some(default(T));
            option = _option;
            A.Run(task, (result) => _option.Value = result);
        }
    }
}
