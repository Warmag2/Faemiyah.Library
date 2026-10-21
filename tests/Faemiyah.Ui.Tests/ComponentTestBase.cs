using Bunit;
using NUnit.Framework;

namespace Faemiyah.Ui.Tests;

/// <summary>
/// Provides a fresh bUnit context for every test.
/// </summary>
public abstract class ComponentTestBase
{
    protected BunitContext Context { get; private set; } = null!;

    [SetUp]
    public void SetUpContext()
    {
        Context = new BunitContext();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TearDown]
    public void TearDownContext()
    {
        Context.Dispose();
    }
}
