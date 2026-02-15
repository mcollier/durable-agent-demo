using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class FlavorTests
{
    [Fact]
    public void WhenCreatedWithRequiredProperties_ThenPropertiesAreSet()
    {
        var flavor = CreateTestFlavor();

        Assert.Equal("flv-001", flavor.FlavorId);
        Assert.Equal("Mint Condition", flavor.Name);
        Assert.Equal("Classic", flavor.Category);
        Assert.True(flavor.ContainsDairy);
        Assert.False(flavor.ContainsNuts);
        Assert.Equal("Cool mint with dark chocolate chips.", flavor.Description);
    }

    [Fact]
    public void WhenTwoFlavorsHaveSameValues_ThenTheyAreEqual()
    {
        var a = CreateTestFlavor();
        var b = CreateTestFlavor();

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoFlavorsHaveDifferentIds_ThenTheyAreNotEqual()
    {
        var a = CreateTestFlavor();
        var b = CreateTestFlavor() with { FlavorId = "flv-999" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenFlavorContainsNuts_ThenContainsNutsIsTrue()
    {
        var flavor = CreateTestFlavor() with { ContainsNuts = true };

        Assert.True(flavor.ContainsNuts);
    }

    [Fact]
    public void WhenFlavorIsDairyFree_ThenContainsDairyIsFalse()
    {
        var flavor = CreateTestFlavor() with { ContainsDairy = false };

        Assert.False(flavor.ContainsDairy);
    }

    private static Flavor CreateTestFlavor() => new()
    {
        FlavorId = "flv-001",
        Name = "Mint Condition",
        Category = "Classic",
        ContainsDairy = true,
        ContainsNuts = false,
        Description = "Cool mint with dark chocolate chips."
    };
}
