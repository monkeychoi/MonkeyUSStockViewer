using System.Text;
using System.Text.Json;
using MonkeyUSStockViewer.Models;

namespace MonkeyUSStockViewer.Services;

public sealed class KisRealtimeParser
{
    public KisParsedMessage Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new KisParsedMessage { Summary = "Empty message" };
        }

        var trimmed = message.Trim();
        if (trimmed.StartsWith('{'))
        {
            return ParseJson(trimmed);
        }

        return ParseRealtimeText(trimmed);
    }

    public string BuildFieldDump(KisRealtimeQuote quote)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Field dump: count={quote.FieldCount}");

        for (var i = 0; i < quote.Fields.Count; i++)
        {
            builder.AppendLine($"[{i}] {quote.Fields[i]}");
        }

        return builder.ToString().TrimEnd();
    }

    private static KisParsedMessage ParseJson(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            var trId = GetNestedString(root, "header", "tr_id");
            var rtCd = GetNestedString(root, "body", "rt_cd");
            var msgCd = GetNestedString(root, "body", "msg_cd");
            var msg1 = GetNestedString(root, "body", "msg1");

            var summaryParts = new List<string> { "JSON" };
            if (!string.IsNullOrWhiteSpace(trId))
            {
                summaryParts.Add($"tr_id={trId}");
            }

            if (!string.IsNullOrWhiteSpace(rtCd))
            {
                summaryParts.Add($"rt_cd={rtCd}");
            }

            if (!string.IsNullOrWhiteSpace(msgCd))
            {
                summaryParts.Add($"msg_cd={msgCd}");
            }

            if (!string.IsNullOrWhiteSpace(msg1))
            {
                summaryParts.Add($"msg1={msg1}");
            }

            return new KisParsedMessage
            {
                IsJson = true,
                TrId = trId,
                Summary = string.Join(", ", summaryParts)
            };
        }
        catch (JsonException ex)
        {
            return new KisParsedMessage
            {
                IsJson = true,
                Summary = $"JSON parse failed: {ex.Message}"
            };
        }
    }

    private static KisParsedMessage ParseRealtimeText(string message)
    {
        var pipeParts = message.Split('|');
        if (pipeParts.Length < 4)
        {
            return new KisParsedMessage { Summary = $"Text message: {message}" };
        }

        var trId = pipeParts[1];
        var fields = pipeParts[3].Split('^');

        var quote = new KisRealtimeQuote
        {
            Symbol = GetField(fields, 0),
            ExchangeTime = GetField(fields, 4),
            KoreaTime = GetField(fields, 6),
            LastPrice = GetField(fields, 10),
            FieldCount = fields.Length,
            Fields = fields
        };

        return new KisParsedMessage
        {
            IsRealtimeData = true,
            TrId = trId,
            Quote = quote,
            Summary = $"Realtime tr_id={trId}, symbol={quote.Symbol}, last={quote.LastPrice}, xhms={quote.ExchangeTime}, khms={quote.KoreaTime}, fields={quote.FieldCount}"
        };
    }

    private static string GetField(string[] fields, int index)
    {
        return index >= 0 && index < fields.Length ? fields[index] : string.Empty;
    }

    private static string GetNestedString(JsonElement root, string parentName, string childName)
    {
        if (!root.TryGetProperty(parentName, out var parent))
        {
            return string.Empty;
        }

        if (!parent.TryGetProperty(childName, out var child))
        {
            return string.Empty;
        }

        return child.GetString() ?? string.Empty;
    }
}
