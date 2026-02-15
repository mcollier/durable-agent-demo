using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class RedactPiiToolTests
{
    [Fact]
    public void WhenCalled_ThenReturnsPrefixedWithRedacted()
    {
        var result = RedactPiiTool.RedactPII("some sensitive text");

        Assert.StartsWith("[REDACTED] ", result);
    }

    [Fact]
    public void WhenCalled_ThenPreservesOriginalInput()
    {
        const string input = "user@example.com called 555-1234";
        var result = RedactPiiTool.RedactPII(input);

        Assert.Equal($"[REDACTED] {input}", result);
    }

    [Fact]
    public void WhenEmptyString_ThenReturnsRedactedPrefix()
    {
        var result = RedactPiiTool.RedactPII(string.Empty);

        Assert.Equal("[REDACTED] ", result);
    }
}
