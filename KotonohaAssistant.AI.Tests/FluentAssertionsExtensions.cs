using System.Text.Json.Nodes;
using FluentAssertions;
using FluentAssertions.Specialized;

namespace KotonohaAssistant.AI.Tests;

public static class FluentAssertionsExtensions
{
    public static void HavePropertyWithStringValue(this JsonNodeAssertions<JsonNode> should, string propertyName, string expectedValue)
    {
        should.HaveProperty(propertyName).Which.ToString().Should().Be(expectedValue);
    }

    public static void HavePropertyWithSubstringValue(this JsonNodeAssertions<JsonNode> should, string propertyName, string expectedValue)
    {
        should.HaveProperty(propertyName).Which.ToString().Should().Contain(expectedValue);
    }
}
