using System.Text.Json.Nodes;
using FluentAssertions;
using FluentAssertions.Specialized;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Helpers;

public static class HelperExtensions
{
    public static void HavePropertyWithStringValue(this JsonNodeAssertions<JsonNode> should, string propertyName, string expectedValue)
    {
        should.HaveProperty(propertyName).Which.ToString().Should().Be(expectedValue);
    }

    public static JsonNode? AsJson(this ChatMessageContent content)
    {
        return content is [] ? null : JsonNode.Parse(content[0].Text);
    }
}
