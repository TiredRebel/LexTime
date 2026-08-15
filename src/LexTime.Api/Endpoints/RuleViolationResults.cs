using LexTime.Domain.Rules;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LexTime.Api.Endpoints;

/// <summary>
/// Turns refused rules into a problem response a caller can act on.
/// </summary>
/// <remarks>
/// FR-010 forbids "invalid request" as compliance, and SC-004 is judged by someone who has not
/// read the code and must be able to say what to change from the response alone. Both audiences
/// are served: the detail sentence for a human, the rule name and offending value in the
/// extension members for a client that wants to highlight the field that failed.
/// </remarks>
public static class RuleViolationResults
{
    /// <summary>
    /// Builds the 400 response for a set of refused rules.
    /// </summary>
    /// <remarks>
    /// <b>Every violated rule is listed, not just the first.</b> A submission wrong in three ways
    /// should not take three round trips to discover it.
    /// <para>
    /// 400 rather than 422: every violation here is a well-formed request the domain refuses, and
    /// the rest of this API already answers refusals with 400 and a problem document. One status
    /// with one meaning is worth more than a finer taxonomy nobody branches on.
    /// </para>
    /// </remarks>
    /// <param name="violations">The rules that refused the submission. Never empty when this is called.</param>
    /// <returns>A 400 problem response naming each rule and the value that broke it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="violations"/> is null.</exception>
    public static ProblemHttpResult Problem(IReadOnlyList<RuleViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        return TypedResults.Problem(
            title: "Domain rule violated",
            detail: string.Join(" ", violations.Select(v => v.Detail)),
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["violations"] = violations
                    .Select(v => new
                    {
                        rule = v.Rule.ToString(),
                        offendingValue = v.OffendingValue,
                        detail = v.Detail,
                    })
                    .ToArray(),
            });
    }
}
