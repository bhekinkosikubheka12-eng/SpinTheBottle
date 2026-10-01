using System.Text.Json.Serialization;

namespace SpinTheBottle.Models;

public class Bet
{
    [JsonPropertyName("betId")]
    public string BetId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("chosenSide")]
    public string ChosenSide { get; set; } = string.Empty;

    [JsonPropertyName("winningSide")]
    public string WinningSide { get; set; } = string.Empty;

    [JsonPropertyName("payout")]
    public decimal Payout { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("serverSeed")]
    public string ServerSeed { get; set; } = string.Empty;
}
