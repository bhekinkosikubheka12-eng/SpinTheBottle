namespace SpinTheBottle.Models;

public class SpinResult
{
    public double TargetAngle { get; set; }
    public double DurationSeconds { get; set; } = 4.0;
    public BetSide WinningSide { get; set; }
    public bool IsWin { get; set; }
    public decimal WagerAmount { get; set; }
    public decimal Payout { get; set; }
    public decimal NewBalance { get; set; }
    public string RoundId { get; set; } = string.Empty;
    public string BetId { get; set; } = string.Empty;
    public string ServerSeed { get; set; } = string.Empty;
    public bool IsTrial { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
