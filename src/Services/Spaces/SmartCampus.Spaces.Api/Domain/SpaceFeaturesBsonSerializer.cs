using System.Collections;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SmartCampus.Spaces.Api.Domain;

public sealed class SpaceFeaturesBsonSerializer : SerializerBase<Dictionary<string, object>>
{
    public override Dictionary<string, object> Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args)
    {
        if (context.Reader.GetCurrentBsonType() == BsonType.Null)
        {
            context.Reader.ReadNull();
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        var document = BsonDocumentSerializer.Instance.Deserialize(context);
        return ConvertDocumentToDictionary(document);
    }

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        Dictionary<string, object>? value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        var document = new BsonDocument();
        foreach (var (key, entryValue) in value)
        {
            document[key] = ToBsonValue(entryValue);
        }

        BsonDocumentSerializer.Instance.Serialize(context, document);
    }

    private static Dictionary<string, object> ConvertDocumentToDictionary(BsonDocument document)
    {
        var result = new Dictionary<string, object>(document.ElementCount, StringComparer.OrdinalIgnoreCase);
        foreach (var element in document.Elements)
        {
            var converted = FromBsonValue(element.Value);
            if (converted is not null)
            {
                result[element.Name] = converted;
            }
        }

        return result;
    }

    private static object? FromBsonValue(BsonValue value)
    {
        return value switch
        {
            null or BsonNull => null,
            BsonBoolean b => b.Value,
            BsonInt32 i => i.Value,
            BsonInt64 l => l.Value,
            BsonDouble d => d.Value,
            BsonDecimal128 dec => Decimal128.ToDecimal(dec.Value),
            BsonString s => s.Value,
            BsonDateTime dt => dt.ToUniversalTime(),
            BsonArray arr => arr.Select(FromBsonValue).ToList(),
            BsonDocument doc when doc.ElementCount == 2 && doc.Contains("_t") && doc.Contains("_v")
                => FromBsonValue(doc["_v"]),
            BsonDocument doc => ConvertDocumentToDictionary(doc),
            _ => BsonTypeMapper.MapToDotNetValue(value)
        };
    }

    private static BsonValue ToBsonValue(object? value)
    {
        return value switch
        {
            null or BsonNull => BsonNull.Value,
            BsonValue bv => bv,
            JsonElement je => ConvertJsonElement(je),
            string str => new BsonString(str),
            bool b => b ? BsonBoolean.True : BsonBoolean.False,
            int i => new BsonInt32(i),
            long l => new BsonInt64(l),
            double d => new BsonDouble(d),
            float f => new BsonDouble(f),
            decimal dec => new BsonDecimal128(dec),
            DateTime dt => new BsonDateTime(
                dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt.ToUniversalTime()),
            DateTimeOffset dto => new BsonDateTime(dto.UtcDateTime),
            IDictionary legacyDict => ConvertLegacyDictionary(legacyDict),
            IEnumerable<KeyValuePair<string, object?>> kvpSeq => new BsonDocument(
                kvpSeq.Select(kvp => new BsonElement(kvp.Key, ToBsonValue(kvp.Value)))),
            IEnumerable enumerable => new BsonArray(enumerable.Cast<object?>().Select(ToBsonValue)),
            _ => BsonTypeMapper.MapToBsonValue(value)
        };
    }

    private static BsonDocument ConvertLegacyDictionary(IDictionary dictionary)
    {
        var doc = new BsonDocument();
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key?.ToString() is { } key)
            {
                doc[key] = ToBsonValue(entry.Value);
            }
        }

        return doc;
    }

    private static BsonValue ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => BsonNull.Value,
            JsonValueKind.True => BsonBoolean.True,
            JsonValueKind.False => BsonBoolean.False,
            JsonValueKind.String => new BsonString(element.GetString()!),
            JsonValueKind.Number when element.TryGetInt32(out var i32) => new BsonInt32(i32),
            JsonValueKind.Number when element.TryGetInt64(out var i64) => new BsonInt64(i64),
            JsonValueKind.Number => new BsonDouble(element.GetDouble()),
            JsonValueKind.Array => new BsonArray(element.EnumerateArray().Select(ConvertJsonElement)),
            JsonValueKind.Object => new BsonDocument(
                element.EnumerateObject().Select(p => new BsonElement(p.Name, ConvertJsonElement(p.Value)))),
            _ => BsonNull.Value
        };
    }
}
