using SpinTheBottle.Models;

namespace SpinTheBottle.Services;

public interface IBettingEngineService
{
    int HouseBiasRating { get; }
    decimal Multiplier { get; }
    decimal TrialBonusAmount { get; }
    decimal MinBet { get; }
    decimal MaxBet { get; }
    double CalculateWinProbability();
    Task<SpinResult> ExecuteTrialSpinAsync(BetSide chosenSide);
    Task<SpinResult> ExecuteRealSpinAsync(string userId, BetSide chosenSide, decimal wagerAmount);
    double GenerateSafeAngleForSide(BetSide side);
    bool IsSafeAngle(double angle);
    BetSide GetSideFromAngle(double angle);
}
