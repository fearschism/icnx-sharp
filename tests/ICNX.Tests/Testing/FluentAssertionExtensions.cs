using System;
using FluentAssertions;
using FluentAssertions.Primitives;
using FluentAssertions.Execution;

namespace ICNX.Tests.Testing;

/// <summary>
/// Provides a Contain overload that accepts StringComparison for tests that use this pattern.
/// </summary>
public static class FluentAssertionExtensions
{
    public static AndConstraint<StringAssertions> Contain(this StringAssertions assertions, string expected, StringComparison comparisonType)
    {
        var subject = assertions.Subject ?? string.Empty;
        Execute.Assertion
            .ForCondition(subject.IndexOf(expected ?? string.Empty, comparisonType) >= 0)
            .FailWith("Expected {context:subject} to contain {0} with comparison {1}", expected, comparisonType);

        return new AndConstraint<StringAssertions>(assertions);
    }
}
