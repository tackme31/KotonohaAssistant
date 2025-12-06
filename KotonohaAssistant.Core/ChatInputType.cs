using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KotonohaAssistant.Core;

[JsonConverter(typeof(StringEnumConverter))]
public enum ChatInputType
{
    User,
    Instruction
}
