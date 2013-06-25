AsyncBridge
===========

What is it?
-----------

A library to help bridge C# Async Function execution from synchronous threads, such as in Windows Forms and ASP.NET.

Why was it made?
----------------

C# 5.0 introduced async and await for concurrent task execution. This is an incredible feature that allows programmers to take advantage of asynchronous blocking to execute code more efficiently. However, there is a problem. await only works from within async functions, and outside of async functions, there is no guarantee that waiting on a task to finish won't result in a deadlock, unless you are executing a console application. From windows forms or ASP.NET contexts, the following example will result in a deadlock that will never return.

### Deadlock Example

From inside a Windows Forms or ASP.NET application, the following code will cause a deadlock.

```csharp
public async Task<string> AsyncString()
{
    await Task.Delay(1000);
    return "TestAsync";
}

public void Test()
{
    
    var task = AsyncString();
    task.Wait();
    
    // This line will never be reached
    string res = task.Result;
}
```

The reason this deadlock occurs is because the await on Task.Delay(1000) yields execution back to the calling context, which then tries to synchronously block waiting for the task to finish, but the task is never re-entered.

AsyncBridge resolves this problem by creating a new SynchronizationContext to run the tasks on, and awaiting on each event in a loop until execution of all tasks completes.

Example Usage
-------------

Create an AsyncBridge by calling the static `AsyncHelper.Wait` static accessor within a using directive. From there, call the `AsyncBridge.Run(Task<T> task, Action<Task<T>> callback)` function, which optionally takes a callback after the task has completed. This can be used to extract method results into the synchronous method.

A typical usage example is shown below:
```csharp
public async Task<string> AsyncString()
{
    await Task.Delay(1000);
    return "TestAsync";
}

public void Test()
{
    string string1 = "";
    string string2 = "";

    using (var A = AsyncHelper.Wait)
    {
        A.Run(AsyncString(), x => string1 = x.Result);
        A.Run(AsyncString(), x => string2 = x.Result);
    }
    
    // Total Execution time at this point will be ~1000ms, not ~2000ms
    // The value of string1 = "TestAsync"
    // The value of string2 = "TestAsync"
}
```

All async functions called by AsyncBridge.Run(Task) inside of the using scope will be executed asynchronously. The async tasks are waited on in a nondeadlocking manner in the destructor of AsyncBridge.

Exception Handling
------------------

One of the most annoying problems in an async context can be dealing with exceptions. AsyncBridge bubbles up exceptions that occur and stops execution. An example of error handling can be seen below:
```csharp
public async Task<string> AsyncStringException()
{
    await Task.Delay(1000);
    throw new Exception("Test Exception.");
}

public void Test()
{
    string string1 = "";
    string string2 = "";

    try
    {
        using (var A = AsyncHelper.Wait)
        {
            A.Run(AsyncString(), x => string1 = x.Result);
            A.Run(AsyncString(), x => string2 = x.Result);
        }
    }
    catch (Exception e)
    {
        // e.Message                 = "AsyncHelpers.Run method threw an exception."
        // e.InnerException.Message) = "Test Exception."
        // Handle exception
        // ...
    }
}
```

Inspiration
-----------

This project was inspired by:

- [AsyncInline](http://social.msdn.microsoft.com/Forums/en-US/163ef755-ff7b-4ea5-b226-bbe8ef5f4796/is-there-a-pattern-for-calling-an-async-method-synchronously)
- [Tame](https://github.com/okws/sfslite/wiki/tame)
- [Team](https://github.com/Sidnicious/team)
