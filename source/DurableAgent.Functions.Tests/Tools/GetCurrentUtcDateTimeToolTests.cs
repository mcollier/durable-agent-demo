using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class GetCurrentUtcDateTimeToolTests
{
    [Fact]
    public void WhenCalled_ThenReturnsIso8601String()
    {
        var result = GetCurrentUtcDateTimeTool.GetCurrentUtcDateTime();

        // Should parse as a valid round-trip DateTime
        Assert.True(DateTime.TryParseExact(
            result,
            "o",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out _));
    }

    [Fact]
    public void WhenCalled_ThenReturnsTimeCloseToNow()
    {
        var before = DateTime.UtcNow;
        var result = GetCurrentUtcDateTimeTool.GetCurrentUtcDateTime();
        var after = DateTime.UtcNow;

        var parsed = DateTime.Parse(result, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.InRange(parsed, before, after);
    }
}
