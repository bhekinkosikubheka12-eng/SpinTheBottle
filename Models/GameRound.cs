using System.Text.Json.Serialization;

namespace SpinTheBottle.Models;

public class GameRound
{
    [JsonPropertyName("roundId")]
    public string RoundId { get; set; } = string.Empty;

    [JsonPropertyName("finalAngle")]
    public double FinalAngle { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty; // "NORTH" or "SOUTH"

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
