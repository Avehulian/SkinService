using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SkinService
{
    /// <summary>
    /// Body sent by the client to /skin-service/{pmc|scav}/voice/change.
    /// </summary>
    public record VoiceRequestData : IRequestData
    {
        [JsonPropertyName("voiceId")]
        public string? VoiceId { get; set; }
    }
}
