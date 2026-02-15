using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class FeedbackMessageTests
{
    private static CustomerInfo CreateTestCustomer() => new()
    {
        PreferredName = "Aidan",
        FirstName = "Aidan",
        LastName = "Smith",
        Email = "aidan@example.com",
        PhoneNumber = "555-0100",
        PreferredContactMethod = ContactMethod.Email
    };

    [Fact]
    public void WhenCreatedWithRequiredProperties_ThenPropertiesAreSet()
    {
        var customer = CreateTestCustomer();

        var message = new FeedbackMessage
        {
            FeedbackId = "fbk-10021",
            StoreId = "store-014",
            OrderId = "ord-77812",
            Customer = customer,
            Channel = "kiosk",
            Rating = 5,
            Comment = "Mint Condition is unreal. Best froyo I've had."
        };

        Assert.Equal("fbk-10021", message.FeedbackId);
        Assert.Equal("store-014", message.StoreId);
        Assert.Equal("ord-77812", message.OrderId);
        Assert.Equal(customer, message.Customer);
        Assert.Equal("kiosk", message.Channel);
        Assert.Equal(5, message.Rating);
        Assert.Equal("Mint Condition is unreal. Best froyo I've had.", message.Comment);
    }

    [Fact]
    public void WhenSubmittedAtNotProvided_ThenDefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var message = new FeedbackMessage
        {
            FeedbackId = "fbk-002",
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = CreateTestCustomer(),
            Channel = "web",
            Rating = 3,
            Comment = "Test"
        };

        var after = DateTimeOffset.UtcNow;

        Assert.InRange(message.SubmittedAt, before, after);
    }

    [Fact]
    public void WhenTwoMessagesHaveSameValues_ThenTheyAreEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var customer = CreateTestCustomer();

        var a = new FeedbackMessage
        {
            FeedbackId = "fbk-003",
            SubmittedAt = timestamp,
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = customer,
            Channel = "app",
            Rating = 4,
            Comment = "Same"
        };

        var b = new FeedbackMessage
        {
            FeedbackId = "fbk-003",
            SubmittedAt = timestamp,
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = customer,
            Channel = "app",
            Rating = 4,
            Comment = "Same"
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoMessagesHaveDifferentIds_ThenTheyAreNotEqual()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var customer = CreateTestCustomer();

        var a = new FeedbackMessage
        {
            FeedbackId = "fbk-004",
            SubmittedAt = timestamp,
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = customer,
            Channel = "kiosk",
            Rating = 5,
            Comment = "Same"
        };

        var b = new FeedbackMessage
        {
            FeedbackId = "fbk-005",
            SubmittedAt = timestamp,
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = customer,
            Channel = "kiosk",
            Rating = 5,
            Comment = "Same"
        };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenMessageToStringCalled_ThenContainsTypeName()
    {
        var message = new FeedbackMessage
        {
            FeedbackId = "fbk-006",
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = CreateTestCustomer(),
            Channel = "web",
            Rating = 3,
            Comment = "Test"
        };

        var str = message.ToString();

        Assert.Contains("FeedbackMessage", str);
        Assert.Contains("fbk-006", str);
    }

    [Fact]
    public void WhenMessageGetHashCodeCalled_ThenEqualObjectsHaveSameHash()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var customer = CreateTestCustomer();

        var a = new FeedbackMessage
        {
            FeedbackId = "fbk-007",
            SubmittedAt = timestamp,
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = customer,
            Channel = "web",
            Rating = 4,
            Comment = "Hash test"
        };

        var b = new FeedbackMessage
        {
            FeedbackId = "fbk-007",
            SubmittedAt = timestamp,
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = customer,
            Channel = "web",
            Rating = 4,
            Comment = "Hash test"
        };

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void WhenMessageCopiedWithNewRating_ThenOnlyRatingChanges()
    {
        var original = new FeedbackMessage
        {
            FeedbackId = "fbk-008",
            StoreId = "store-001",
            OrderId = "ord-001",
            Customer = CreateTestCustomer(),
            Channel = "web",
            Rating = 5,
            Comment = "Original"
        };

        var copy = original with { Rating = 1 };

        Assert.Equal(1, copy.Rating);
        Assert.Equal(original.FeedbackId, copy.FeedbackId);
        Assert.NotEqual(original, copy);
    }
}
