using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using MyLife.Crypto;

namespace MyLife.Tests.Crypto;

/// <summary>Prüft die C#-Fassade über myLife.crypto.js ohne Browser.</summary>
public sealed partial class JsMyLifeCryptoTests
{
    private static readonly string ModuleFile = Path.Combine(
        ResourceParityTests.FindRepositoryRoot(), "src", "MyLife.Client", "wwwroot", "js", "mylife.crypto.js");

    [GeneratedRegex(@"^export\s+(?:async\s+)?function\s+(\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex ExportedFunction();

    [Fact]
    public void EveryFacadeFunctionIsExportedByTheModule()
    {
        var exported = ExportedFunction().Matches(File.ReadAllText(ModuleFile)).Select(m => m.Groups[1].Value).ToHashSet();
        var used = typeof(JsFunctions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, name => Assert.Contains(name, exported));
    }

    [Fact]
    public void ModulePathPointsToTheClientAsset()
    {
        Assert.Equal("./js/mylife.crypto.js", JsMyLifeCrypto.ModulePath);
        Assert.True(File.Exists(ModuleFile));
    }

    [Theory]
    [InlineData("Error: mylife:decrypt-failed\n    at decrypt (mylife.crypto.js:10)", "mylife:decrypt-failed")]
    [InlineData("mylife:wrong-key-type", "mylife:wrong-key-type")]
    [InlineData("OperationError: The operation failed", "mylife:js-error")]
    public void ErrorCodesAreExtractedWithoutDetails(string message, string expected)
    {
        Assert.Equal(expected, JsMyLifeCrypto.ExtractCode(message));
    }

    [Fact]
    public async Task CallsAreForwardedToTheModuleAndErrorsBecomeCryptographicExceptions()
    {
        var module = new FakeModule();
        var runtime = new FakeRuntime(module);
        await using var crypto = new JsMyLifeCrypto(runtime);

        module.Result = new byte[] { 1, 2, 3 };
        var blob = await crypto.EncryptAsync(new CryptoKeyHandle("h1"), [9], "ctx");

        Assert.Equal([1, 2, 3], blob);
        Assert.Equal(("import", JsMyLifeCrypto.ModulePath), (runtime.Identifier, runtime.Argument));
        Assert.Equal("encrypt", module.Identifier);
        Assert.Equal("h1", module.Arguments![0]);
        Assert.Equal("ctx", module.Arguments[2]);

        module.Error = new JSException("Error: mylife:decrypt-failed");
        var error = await Assert.ThrowsAsync<CryptographicException>(() => crypto.DecryptAsync(new CryptoKeyHandle("h1"), [1], "ctx"));
        Assert.Equal("mylife:decrypt-failed", error.Message);
    }

    [Fact]
    public async Task WeakArgon2ParametersAreRejectedBeforeCallingJavaScript()
    {
        var module = new FakeModule();
        await using var crypto = new JsMyLifeCrypto(new FakeRuntime(module));

        await Assert.ThrowsAsync<CryptographicException>(() =>
            crypto.DeriveMasterKeyAsync("pw", new byte[16], new Argon2Parameters(1024, 1, 1)));
        Assert.Null(module.Identifier);
    }

    [Fact]
    public async Task CreateKeyPassesHandlesAsPlainStrings()
    {
        var module = new FakeModule();
        await using var crypto = new JsMyLifeCrypto(new FakeRuntime(module));
        module.ResultFactory = type => Activator.CreateInstance(type, "neu", new[] { new byte[] { 7 } })!;

        var created = await crypto.CreateKeyAsync([new(new CryptoKeyHandle("kek"), "vmk")]);

        Assert.Equal("neu", created.Handle.Id);
        Assert.Single(created.WrappedKeys);
        var wrappings = Assert.IsAssignableFrom<Array>(module.Arguments![0]);
        var first = wrappings.GetValue(0)!;
        Assert.Equal("kek", first.GetType().GetProperty("wrapper")!.GetValue(first));
        Assert.Equal("vmk", first.GetType().GetProperty("context")!.GetValue(first));
    }

    private sealed class FakeRuntime(IJSObjectReference module) : IJSRuntime
    {
        public string? Identifier { get; private set; }

        public object? Argument { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Identifier = identifier;
            Argument = args?[0];
            return ValueTask.FromResult((TValue)module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    private sealed class FakeModule : IJSObjectReference
    {
        public string? Identifier { get; private set; }

        public object?[]? Arguments { get; private set; }

        public object? Result { get; set; }

        public Func<Type, object>? ResultFactory { get; set; }

        public JSException? Error { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Identifier = identifier;
            Arguments = args;
            if (Error is not null)
            {
                throw Error;
            }

            if (typeof(TValue) == typeof(Microsoft.JSInterop.Infrastructure.IJSVoidResult))
            {
                return ValueTask.FromResult(default(TValue)!);
            }

            var result = ResultFactory is not null ? ResultFactory(typeof(TValue)) : Result;
            return ValueTask.FromResult((TValue)result!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
