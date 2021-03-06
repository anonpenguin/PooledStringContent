# PooledStringContent

## What is it?

If you're writing a .NET library/app that sends HTTP requests, then you may have used the `StringContent` class to translate strings to an `HttpContent`, which you can PUT/POST with. `PooledStringContent` is a drop-in replacement for `StringContent` you can use to cut down on memory usage in your library/app.

## Installation

You can get it via NuGet:

```powershell
Install-Package Pooling.Net.Http
```

## How do I use it?

Simply replace the lines where you use `StringContent`:

```cs
using (var content = new StringContent("..."))
{
    return await httpClient.PostAsync(uri, content).ConfigureAwait(false);
}
```

with `PooledStringContent`:

```cs
using (var content = new PooledStringContent("..."))
{
    return await httpClient.PostAsync(uri, content).ConfigureAwait(false);
}
```

Encodings other than UTF-8 are also supported, e.g. you can write `new PooledStringContent("foobar", Encoding.Unicode)`.

## What benefits does it offer?

Performance. If your application/library is using a lot of memory when making HTTP requests, then this may help as it uses buffer pooling for the encoded bytes. It rents these buffers using the new [ArrayPool APIs](https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/ArrayPool.cs) added to .NET Core.

If you're interested more in how it works, take a look at the source code [here.](src/Pooling.Net.Http/PooledStringContent.cs)

## Is this only compatible with .NET Core?

No, it should be compatible with the .NET Framework/other environments as well, because it targets the [.NET Platform Standard](https://github.com/dotnet/corefx/blob/master/Documentation/architecture/net-platform-standard.md).

## Pitfalls

### #1: Not disposing the content #

It is very important that you make sure to dispose the `PooledStringContent` after using it. This is because unlike `StringContent`, it actually does important work when disposing by returning its rented array to the buffer pool. If you don't dispose it, it will end up eventually depleting the buffer pool and creating new arrays each time, which could result in *less* performance for your app.

In short, change any code like this:

```cs
var content = new StringContent("...");
return await httpClient.PostAsync(uri, content).ConfigureAwait(false);
```

to use a `using` statement, like the example above:

```cs
using (var content = new PooledStringContent("..."))
{
    return await httpClient.PostAsync(uri, content).ConfigureAwait(false);
}
```

Note that this may lead to our next pitfall, which is...

### #2: Disposing before you finish the request #

An optimization you can make with `Task`-returning methods is, if all you do is some synchronous work then await another async action at the end, i.e.

```cs
public async Task<HttpResponseMessage> Foo()
{
    var content = new StringContent("...");
    return await _httpClient.PostAsync(_uri, content).ConfigureAwait(false);
}
```

you can instead return the `Task` directly like so:

```cs
public Task<HttpResponseMessage> Foo() // note: no async
{
    var content = new StringContent("...");
    return _httpClient.PostAsync(_uri, content); // note: no await
}
```

Now, if you made the change I noted above, your code may now look like this:

```cs
public Task<HttpResponseMessage> Foo()
{
    using (var content = new PooledStringContent("..."))
    {
        return _httpClient.PostAsync(_uri, content);
    }
}
```

But wait! `PostAsync` is no longer the last operation in the method, since after it's returned `content.Dispose` is called in a `finally` block. (`using` expands to a `try-finally` block when compiled.) This means that if the `HttpClient` attempts to read from the content after performing an asynchronous operation, the content will already have been disposed and will no longer be valid.

The fix for this is to switch back to using async/await:

```cs
public async Task<HttpResponseMessage> Foo()
{
    using (var content = new PooledStringContent("..."))
    {
        return await _httpClient.PostAsync(_uri, content).ConfigureAwait(false);
    }
}
```

See [this StackOverflow question](http://stackoverflow.com/q/31195467/4077294) for more on this.

## Building

Interested in building the repo? Please make sure you have the [.NET Core RTM tooling](https://www.microsoft.com/net/core) installed.

To build the source:

```bash
cd src
dotnet restore
dotnet build
```

To run tests:

```bash
cd test
dotnet restore
dotnet test
```

## License

[MIT](LICENSE)
