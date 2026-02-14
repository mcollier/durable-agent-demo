namespace DurableAgent.Core.Models;

/// <summary>
/// Represents the result returned by the customer service agent after processing feedback.
/// </summary>
public sealed record FeedbackResult
{
    /// <summary>Identifier of the feedback item that was processed.</summary>
    public required string FeedbackId { get; init; }

    /// <summary>Detected sentiment (e.g., "positive", "neutral", "negative").</summary>
    public required string Sentiment { get; init; }

    /// <summary>Risk assessment for the feedback.</summary>
    public required RiskAssessment Risk { get; init; }

    /// <summary>Recommended action code (e.g., "ISSUE_COUPON", "ESCALATE").</summary>
    public required string Action { get; init; }

    /// <summary>Customer-facing message to send.</summary>
    public required ResponseMessage Message { get; init; }

    /// <summary>Coupon details, if a coupon was generated.</summary>
    public CouponDetails? Coupon { get; init; }

    /// <summary>Follow-up disposition for the feedback.</summary>
    public required FollowUp FollowUp { get; init; }

    /// <summary>Tool calls made by the agent during processing.</summary>
    public IReadOnlyList<ToolCallRecord> ToolCalls { get; init; } = [];

    /// <summary>Agent confidence score (0.0–1.0).</summary>
    public required double Confidence { get; init; }
}

/// <summary>
/// Risk assessment flags for a piece of feedback.
/// </summary>
public sealed record RiskAssessment
{
    /// <summary>Indicates whether the feedback involves a health or safety concern.</summary>
    public required bool IsHealthOrSafety { get; init; }

    /// <summary>Indicates whether the feedback involves a food quality issue.</summary>
    public required bool IsFoodQualityIssue { get; init; }

    /// <summary>Keywords that triggered the risk flags.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];
}

/// <summary>
/// Customer-facing message content.
/// </summary>
public sealed record ResponseMessage
{
    /// <summary>Message subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Message body text.</summary>
    public required string Body { get; init; }
}

/// <summary>
/// Details of a coupon issued to a customer.
/// </summary>
public sealed record CouponDetails
{
    /// <summary>Coupon code for the customer to redeem.</summary>
    public required string Code { get; init; }

    /// <summary>Discount percentage (e.g., 25 for 25%).</summary>
    public required int DiscountPercent { get; init; }

    /// <summary>UTC expiration date and time for the coupon.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Follow-up disposition after processing feedback.
/// </summary>
public sealed record FollowUp
{
    /// <summary>Whether a human reviewer needs to handle the case.</summary>
    public required bool RequiresHuman { get; init; }

    /// <summary>Case identifier for human follow-up, if applicable.</summary>
    public string? CaseId { get; init; }
}

/// <summary>
/// Record of a tool call made by the agent during processing.
/// </summary>
public sealed record ToolCallRecord
{
    /// <summary>Name of the tool that was invoked.</summary>
    public required string ToolName { get; init; }

    /// <summary>Reference identifier for the tool result.</summary>
    public required string ResultRef { get; init; }
}
