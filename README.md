AsyncBridge
===========

What is it?
-----------

A library to help bridge C# Async Function execution from synchronous threads, such as in Windows Forms and ASP.NET.

Why was it made?
----------------

C# 5.0 introduced async and await for concurrent task execution. This is an incredible feature that allows programmers to take advantage of asynchronous blocking to execute code more efficiently. However, there is a problem. await only works from within async functions, and outside of async functions, there is no guarantee that waiting on a task to finish won't result in a deadlock, unless you are executing a console application. From windows forms or ASP.NET contexts, the following example will result in a deadlock that will never return.

#### Deadlock Example

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

The reason this deadlock occurs is because the await on Task.Delay(1000) yields execution back to the calling context, which then tries to synchronously block waiting for the task to finish, but the task is never re-entered because only one thread can run a synchronization context as a time.

AsyncBridge resolves this problem by creating a new SynchronizationContext to run the tasks on, and awaiting on each event in a loop until execution of all tasks completes.

Example Usage
-------------

Create an AsyncBridge by calling the static `AsyncHelper.Wait` static accessor within a using directive. From there, call the `AsyncBridge.Run(Task<T> task, Action<Task<T>> callback)` function, which optionally takes a callback after the task has completed. This can be used to extract method results into the synchronous method. Any return values you want to extract through callbacks should first have a value initialized before the using statement.

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
    string s = "";

    try
    {
        using (var A = AsyncHelper.Wait)
        {
            A.Run(AsyncString(), x => s = x.Result);
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


Fire and Forget
---------------

The "Fire and Forget" pattern is an excellent way to keep the UI or ASP.NET request thread responsive by executing longer running tasks in the background. A good example is database logging for non user-facing data (I.E. The user doesn't need to know that a database action succeeded). This pattern can be accomplished in a variety of different ways, however in Windows Forms or ASP.NET context, most of them don't work, or have serious flaws.

The two natural seeming choices are executing an `async void` method, or an `async Task` method without waiting for it to complete.

Async void methods at first appear to do exactly what it wanted -- asynchronously run a task without any regard for it's result. However, ASP.NET and Windows Forms threads will finish everything in a synchronization context before returning, so pages will not serve until any `async void` methods called have completed, which is the complete opposite of fire and forget -- fire and wait. Calling an async Task method and not waiting on the result has the same behavior.

In order to actually get the desired behavior, one needs to run the fire and forget method in a new thread. The Task thread pool works well for this meaning Task.Run is a good choice. The caveat of Task.Run is that any exceptions that occur unhandled will sit around occupying memory until they are dealt with. Since Fire and forget method exceptions usually aren't at the top level, memory leaks can occur. In order to resolve this, you need to wrap try/catch around the awaited task which Task.Run will execute. Care must be taken to actually await the task, because otherwise the exception will remain uncaught as the try/catch will be wrapping the start of the task rather than the task in its entirety. This is what the AsyncHelper.FireAndForget function does.

#### Fire and Forget Example

```csharp
private async Task FAFExample()
{
    await Task.Delay(1000);
    throw new Exception("Test exception");
}

public void Test()
{
    AsyncHelper.FireAndForget(() => FAFExample()); // Will silently ignore exceptions
    AsyncHelper.FireAndForget(                     // Will handle exceptions by writing
        () => FAFExample(),                        // e.Message to the console
        e => Console.WriteLine(e.Message));        // e.Message = "Test exception"
        
    // These lines will be reached immediately, not
    // ~1000ms or ~2000ms from the previous lines)
    // and method execution will not block.
}
```

This can be used in any context, from async methods or synchronous methods, and from inside or outside of `using(AsyncBridge)` blocks

Inspiration
-----------

This project was inspired by:

- [AsyncInline](http://social.msdn.microsoft.com/Forums/en-US/163ef755-ff7b-4ea5-b226-bbe8ef5f4796/is-there-a-pattern-for-calling-an-async-method-synchronously)
- [Tame](https://github.com/okws/sfslite/wiki/tame)
- [Team](https://github.com/Sidnicious/team)
