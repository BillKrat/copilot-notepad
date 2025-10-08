using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Adventures.Shared.Event
{
    /// <summary>
    /// Event args that normalizes several input types (raw json string, Dictionary, JsonObject, any POCO)
    /// into a <see cref="JsonObject"/> exposed via the <see cref="Json"/> property.
    /// </summary>
    public class JsonEventArgs : EventArgs
    {
        /// <summary>
        /// The normalized JSON object for the event.
        /// </summary>
        public JsonObject Json { get; }

        /// <summary>
        /// Create from a raw JSON string representing an object.
        /// </summary>
        public JsonEventArgs(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string is null/empty", nameof(json));

            var node = JsonNode.Parse(json) as JsonObject;
            if (node is null)
                throw new ArgumentException("JSON must represent an object (e.g. { \"key\": \"value\" })", nameof(json));
            Json = node;
        }

        /// <summary>
        /// Create from a dictionary of primitive / nested values.
        /// </summary>
        public JsonEventArgs(IDictionary<string, object?> dictionary)
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            Json = ToJsonObject(dictionary);
        }

        /// <summary>
        /// Create from a generic value which can be:
        /// - JsonObject / JsonNode
        /// - string containing JSON
        /// - IDictionary<string, object?>
        /// - Any POCO (will be serialized)
        /// </summary>
        public JsonEventArgs(object? value)
        {
            Json = Normalize(value) ?? new JsonObject();
        }

        /// <summary>
        /// Returns the JSON as a compact string.
        /// </summary>
        public override string ToString() => Json.ToJsonString();

        /// <summary>
        /// Try get a value by property name and deserialize to T.
        /// </summary>
        public bool TryGetProperty<T>(string name, out T? value)
        {
            value = default;
            if (Json.TryGetPropertyValue(name, out var node) && node is not null)
            {
                try
                {
                    value = node.Deserialize<T>();
                    return true;
                }
                catch
                {
                    // ignore
                }
            }
            return false;
        }

        private static JsonObject? Normalize(object? value)
        {
            switch (value)
            {
                case null:
                    return new JsonObject();
                case JsonObject jo:
                    return jo;
                case JsonNode node:
                    return node as JsonObject ?? new JsonObject { ["value"] = node }; // wrap if not an object
                case string s:
                    return (JsonNode.Parse(s) as JsonObject) ?? new JsonObject { ["value"] = JsonValue.Create(s) };
                case IDictionary<string, object?> dict:
                    return ToJsonObject(dict);
                default:
                    {
                        // Serialize arbitrary object then parse
                        var json = JsonSerializer.Serialize(value, value.GetType());
                        return JsonNode.Parse(json) as JsonObject ?? new JsonObject { ["value"] = JsonValue.Create(json) };
                    }
            }
        }

        private static JsonObject ToJsonObject(IDictionary<string, object?> dict)
        {
            var result = new JsonObject();
            foreach (var kvp in dict)
            {
                result[kvp.Key] = ToNode(kvp.Value);
            }
            return result;
        }

        private static JsonNode? ToNode(object? value)
        {
            if (value == null) return null;
            return value switch
            {
                JsonNode jn => jn,
                IDictionary<string, object?> d => ToJsonObject(d),
                IDictionary nonGenericDict => NonGenericDictToNode(nonGenericDict),
                IEnumerable<object?> list => new JsonArray(list.Select(ToNode).ToArray()),
                IEnumerable enumerable when enumerable is not string => new JsonArray(enumerable.Cast<object?>().Select(ToNode).ToArray()),
                _ => JsonSerializer.SerializeToNode(value, value.GetType())
            };
        }

        private static JsonNode NonGenericDictToNode(IDictionary dict)
        {
            var obj = new JsonObject();
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                obj[key] = ToNode(entry.Value);
            }
            return obj;
        }
    }
}
