using System.Text.Json.Serialization;

namespace SpinTheBottle.Models;

public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; } = 0.00m;

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("isBonusRedeemed")]
    public bool IsBonusRedeemed { get; set; } = false;

    [JsonPropertyName("sharedContactsCount")]
    public int SharedContactsCount { get; set; } = 0;

    [JsonPropertyName("isTrialCompleted")]
    public bool IsTrialCompleted { get; set; } = false;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
