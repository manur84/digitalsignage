using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services
{
    /// <summary>
    /// Custom JSON converter for Message deserialization
    /// Handles type discrimination based on the Type field
    /// </summary>
    public class MessageJsonConverter : JsonConverter<Message>
    {
        public override bool CanWrite => false; // Use default serialization

        public override Message ReadJson(JsonReader reader, Type objectType, Message? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject jsonObject = JObject.Load(reader);

            // Get the message type
            string messageType = jsonObject["Type"]?.ToString() ?? jsonObject["type"]?.ToString() ?? string.Empty;

            Message message = messageType.ToUpper() switch
            {
                MessageTypes.Register or "REGISTER" => new RegisterMessage(),
                MessageTypes.Heartbeat or "HEARTBEAT" => new HeartbeatMessage(),
                MessageTypes.StatusReport or "STATUS_REPORT" => new StatusReportMessage(),
                MessageTypes.Log or "LOG" => new LogMessage(),
                MessageTypes.Screenshot or "SCREENSHOT" => new ScreenshotMessage(),
                MessageTypes.UpdateConfigResponse or "UPDATE_CONFIG_RESPONSE" => new UpdateConfigResponseMessage(),
                MessageTypes.RegistrationResponse or "REGISTRATION_RESPONSE" => new RegistrationResponseMessage(),
                MessageTypes.DisplayUpdate or "DISPLAY_UPDATE" => new DisplayUpdateMessage(),
                MessageTypes.Command or "COMMAND" => new CommandMessage(),
                MessageTypes.UpdateConfig or "UPDATE_CONFIG" => new UpdateConfigMessage(),
                MessageTypes.LayoutAssigned or "LAYOUT_ASSIGNED" => new LayoutAssignmentMessage(),
                MessageTypes.DataUpdate or "DATA_UPDATE" => new DataUpdateMessage(),
                _ => throw new JsonSerializationException($"Unknown message type: {messageType}")
            };

            // Populate the object from JSON
            using (JsonReader jsonReader = jsonObject.CreateReader())
            {
                serializer.Populate(jsonReader, message);
            }

            return message;
        }

        public override void WriteJson(JsonWriter writer, Message? value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Use default serialization");
        }
    }
}