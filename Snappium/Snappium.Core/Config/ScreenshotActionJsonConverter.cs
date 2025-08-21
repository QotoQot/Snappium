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

        // Handle tap action (both cases)
        if (root.TryGetProperty("Tap", out var tapElement) || 
            root.TryGetProperty("tap", out tapElement))
        {
            var selector = JsonSerializer.Deserialize<Selector>(tapElement, options)
                ?? throw new JsonException("Invalid tap selector");
            return new ScreenshotAction { Tap = selector };
        }

        // Handle wait action (both cases)
        if (root.TryGetProperty("Wait", out var waitElement) || 
            root.TryGetProperty("wait", out waitElement))
        {
            var waitConfig = JsonSerializer.Deserialize<WaitConfig>(waitElement, options)
                ?? throw new JsonException("Invalid wait configuration");
            return new ScreenshotAction { Wait = waitConfig };
        }

        // Handle wait_for action (both cases)
        if (root.TryGetProperty("WaitFor", out waitForElement) || 
            root.TryGetProperty("waitFor", out waitForElement))
        {
            var waitForConfig = JsonSerializer.Deserialize<WaitForConfig>(waitForElement, options)
                ?? throw new JsonException("Invalid wait_for configuration");
            return new ScreenshotAction { WaitFor = waitForConfig };
        }

        // Handle capture action (both cases)
        if (root.TryGetProperty("Capture", out var captureElement) || 
            root.TryGetProperty("capture", out captureElement))
        {
            var captureConfig = JsonSerializer.Deserialize<CaptureConfig>(captureElement, options)
                ?? throw new JsonException("Invalid capture configuration");
            return new ScreenshotAction { Capture = captureConfig };
        }

        // If no known action type was found, provide helpful error message
        var availableProperties = string.Join(", ", root.EnumerateObject().Select(prop => prop.Name));
        throw new JsonException($"Unknown ScreenshotAction type. Found properties: [{availableProperties}]. Expected one of: Tap, Wait, WaitFor, Capture");
    }

    public override void Write(Utf8JsonWriter writer, ScreenshotAction value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write the appropriate action type
        if (value.Tap != null)
        {
            writer.WritePropertyName("Tap");
            JsonSerializer.Serialize(writer, value.Tap, options);
        }
        else if (value.Wait != null)
        {
            writer.WritePropertyName("Wait");
            JsonSerializer.Serialize(writer, value.Wait, options);
        }
        else if (value.WaitFor != null)
        {
            writer.WritePropertyName("WaitFor");
            JsonSerializer.Serialize(writer, value.WaitFor, options);
        }
        else if (value.Capture != null)
        {
            writer.WritePropertyName("Capture");
            JsonSerializer.Serialize(writer, value.Capture, options);
        }
        else
        {
            throw new JsonException("ScreenshotAction must have exactly one action type set (Tap, Wait, WaitFor, or Capture)");
        }

        writer.WriteEndObject();
    }
}