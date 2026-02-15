using DurableAgent.Core.Models;
using DurableAgent.Functions.Activities;
using FakeItEasy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DurableAgent.Functions.Tests.Activities;

public class ProcessFeedbackActivityTests
{
    private static FeedbackMessage CreateTestFeedback(int rating = 5) => new()
    {
        FeedbackId = "fbk-10021",
        StoreId = "store-014",
        OrderId = "ord-77812",
        Customer = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Email
        },
        Channel = "kiosk",
        Rating = rating,
        Comment = "Mint Condition is unreal. Best froyo I've had."
    };

    private static FunctionContext CreateFakeFunctionContext()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var context = A.Fake<FunctionContext>();
        A.CallTo(() => context.InstanceServices).Returns(serviceProvider);

        return context;
    }

    [Fact]
    public void WhenValidInput_ThenReturnsProcessedResult()
    {
        var feedback = CreateTestFeedback();
        var context = CreateFakeFunctionContext();

        var result = ProcessFeedbackActivity.Run(feedback, context);

        Assert.Contains("fbk-10021", result);
        Assert.StartsWith("Processed feedback", result);
    }

    [Fact]
    public void WhenInputIsNull_ThenThrowsArgumentNullException()
    {
        var context = CreateFakeFunctionContext();

        Assert.Throws<ArgumentNullException>(() =>
            ProcessFeedbackActivity.Run(null!, context));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhenRatingBelowMinimum_ThenThrowsArgumentOutOfRangeException(int rating)
    {
        var feedback = CreateTestFeedback(rating);
        var context = CreateFakeFunctionContext();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProcessFeedbackActivity.Run(feedback, context));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(100)]
    public void WhenRatingAboveMaximum_ThenThrowsArgumentOutOfRangeException(int rating)
    {
        var feedback = CreateTestFeedback(rating);
        var context = CreateFakeFunctionContext();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProcessFeedbackActivity.Run(feedback, context));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void WhenRatingIsValid_ThenDoesNotThrow(int rating)
    {
        var feedback = CreateTestFeedback(rating);
        var context = CreateFakeFunctionContext();

        var result = ProcessFeedbackActivity.Run(feedback, context);

        Assert.NotNull(result);
    }
}
