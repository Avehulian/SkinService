using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SkinService
{
    /// <summary>
    /// Body sent by the client to /skin-service/{pmc|scav}/change.
    /// </summary>
    public record SkinRequestData : IRequestData
    {
        [JsonPropertyName("bodyId")]
        public string? BodyId { get; set; }

        [JsonPropertyName("feetId")]
        public string? FeetId { get; set; }

        [JsonPropertyName("headId")]
        public string? HeadId { get; set; }

        [JsonPropertyName("handsId")]
        public string? HandsId { get; set; }
    }
}
