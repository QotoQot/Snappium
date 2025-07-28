using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snappium.Core.Config;

/// <summary>
/// Custom JSON converter for ScreenshotAction polymorphic deserialization.
/// Replaces brittle JsonElement parsing with proper type-safe conversion.
/// </summary>
public sealed class ScreenshotActionJsonConverter : JsonConverter<ScreenshotAction>
{
    public override ScreenshotAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        // Handle tap action
        if (root.TryGetProperty("tap", out var tapElement))
        {
            var selector = JsonSerializer.Deserialize<Selector>(tapElement, options)
                ?? throw new JsonException("Invalid tap selector");
            return new ScreenshotAction { Tap = selector };
        }

        // Handle wait action
        if (root.TryGetProperty("wait", out var waitElement))
        {
            var waitConfig = JsonSerializer.Deserialize<WaitConfig>(waitElement, options)
                ?? throw new JsonException("Invalid wait configuration");
            return new ScreenshotAction { Wait = waitConfig };
        }

        // Handle wait_for action
        if (root.TryGetProperty("wait_for", out var waitForElement))
        {
            var waitForConfig = JsonSerializer.Deserialize<WaitForConfig>(waitForElement, options)
                ?? throw new JsonException("Invalid wait_for configuration");
            return new ScreenshotAction { WaitFor = waitForConfig };
        }

        // Handle capture action
        if (root.TryGetProperty("capture", out var captureElement))
        {
            var captureConfig = JsonSerializer.Deserialize<CaptureConfig>(captureElement, options)
                ?? throw new JsonException("Invalid capture configuration");
            return new ScreenshotAction { Capture = captureConfig };
        }

        // If no known action type was found, provide helpful error message
        var availableProperties = string.Join(", ", root.EnumerateObject().Select(prop => prop.Name));
        throw new JsonException($"Unknown ScreenshotAction type. Found properties: [{availableProperties}]. Expected one of: tap, wait, wait_for, capture");
    }

    public override void Write(Utf8JsonWriter writer, ScreenshotAction value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write the appropriate action type
        if (value.Tap != null)
        {
            writer.WritePropertyName("tap");
            JsonSerializer.Serialize(writer, value.Tap, options);
        }
        else if (value.Wait != null)
        {
            writer.WritePropertyName("wait");
            JsonSerializer.Serialize(writer, value.Wait, options);
        }
        else if (value.WaitFor != null)
        {
            writer.WritePropertyName("wait_for");
            JsonSerializer.Serialize(writer, value.WaitFor, options);
        }
        else if (value.Capture != null)
        {
            writer.WritePropertyName("capture");
            JsonSerializer.Serialize(writer, value.Capture, options);
        }
        else
        {
            throw new JsonException("ScreenshotAction must have exactly one action type set (Tap, Wait, WaitFor, or Capture)");
        }

        writer.WriteEndObject();
    }
}