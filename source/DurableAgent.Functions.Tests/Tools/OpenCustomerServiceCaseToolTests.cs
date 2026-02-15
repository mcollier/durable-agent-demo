using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class OpenCustomerServiceCaseToolTests
{
    [Fact]
    public void WhenCalled_ThenStartsWithCasePrefix()
    {
        var result = OpenCustomerServiceCaseTool.OpenCustomerServiceCase("fbk-001", "Customer complaint");

        Assert.StartsWith("CASE-", result);
    }

    [Fact]
    public void WhenCalled_ThenHasCorrectLength()
    {
        var result = OpenCustomerServiceCaseTool.OpenCustomerServiceCase("fbk-001", "details");

        // "CASE-" (5 chars) + 8-char GUID segment = 13 chars
        Assert.Equal(13, result.Length);
    }

    [Fact]
    public void WhenCalledTwice_ThenProducesUniqueCaseIds()
    {
        var id1 = OpenCustomerServiceCaseTool.OpenCustomerServiceCase("fbk-001", "details");
        var id2 = OpenCustomerServiceCaseTool.OpenCustomerServiceCase("fbk-001", "details");

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void WhenCalled_ThenSuffixIsUppercase()
    {
        var result = OpenCustomerServiceCaseTool.OpenCustomerServiceCase("fbk-001", "details");
        var suffix = result["CASE-".Length..];

        Assert.Equal(suffix.ToUpper(), suffix);
    }
}
