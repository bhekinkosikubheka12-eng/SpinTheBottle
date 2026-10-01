using System.Security.Cryptography;
using SpinTheBottle.Models;

namespace SpinTheBottle.Services;

public class BettingEngineService : IBettingEngineService
{
    private readonly IFirebaseService _firebaseService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BettingEngineService> _logger;

    public int HouseBiasRating { get; }
    public decimal Multiplier { get; }
    public decimal TrialBonusAmount { get; }
    public decimal MinBet { get; }
    public decimal MaxBet { get; }

    public BettingEngineService(
        IFirebaseService firebaseService,
        IConfiguration configuration,
        ILogger<BettingEngineService> logger)
    {
        _firebaseService = firebaseService;
        _configuration = configuration;
        _logger = logger;

        // Read HouseBiasRating (clamped 1 to 10, default 7)
        int rawRating = _configuration.GetValue<int>("BettingEngine:HouseBiasRating", 7);
        HouseBiasRating = Math.Clamp(rawRating, 1, 10);

        Multiplier = _configuration.GetValue<decimal>("BettingEngine:NorthMultiplier", 1.96m);
        TrialBonusAmount = _configuration.GetValue<decimal>("BettingEngine:TrialBonusAmount", 50.00m);
        MinBet = _configuration.GetValue<decimal>("BettingEngine:MinBet", 1.00m);
        MaxBet = _configuration.GetValue<decimal>("BettingEngine:MaxBet", 1000.00m);

        _logger.LogInformation("BettingEngineService initialized. HouseBiasRating: {Rating}, WinProbability: {Prob:P1}",
            HouseBiasRating, CalculateWinProbability());
    }

    /// <summary>
    /// Calculates the probability of the player winning based on HouseBiasRating (1 to 10).
    /// 1 = 90% Player Win
    /// 5 = 50% Fair Coin Flip
    /// 10 = 10% Player Win (90% House Win)
    /// </summary>
    public double CalculateWinProbability()
    {
        if (HouseBiasRating <= 5)
        {
            // 1 -> 0.90, 2 -> 0.80, 3 -> 0.70, 4 -> 0.60, 5 -> 0.50
            return 0.90 - (HouseBiasRating - 1) * 0.10;
        }
        else
        {
            // 5 -> 0.50, 6 -> 0.42, 7 -> 0.34, 8 -> 0.26, 9 -> 0.18, 10 -> 0.10
            return 0.50 - (HouseBiasRating - 5) * 0.08;
        }
    }

    /// <summary>
    /// Trial spin for first-time unauthenticated guests.
    /// 100% guaranteed to win for the chosen side.
    /// </summary>
    public async Task<SpinResult> ExecuteTrialSpinAsync(BetSide chosenSide)
    {
        // 100% guaranteed win for trial
        BetSide winningSide = chosenSide;
        double targetAngle = GenerateSafeAngleForSide(winningSide);
        string roundId = "trial_" + Guid.NewGuid().ToString("N")[..8];
        string serverSeed = GenerateServerSeed();

        var round = new GameRound
        {
            RoundId = roundId,
            FinalAngle = targetAngle,
            Outcome = winningSide == BetSide.North ? "NORTH" : "SOUTH",
            CreatedAt = DateTime.UtcNow
        };

        await _firebaseService.RecordGameRoundAsync(round);

        return new SpinResult
        {
            Success = true,
            IsTrial = true,
            IsWin = true,
            WinningSide = winningSide,
            TargetAngle = targetAngle,
            DurationSeconds = 4.0,
            WagerAmount = 0.00m,
            Payout = TrialBonusAmount,
            NewBalance = TrialBonusAmount,
            RoundId = roundId,
            ServerSeed = serverSeed
        };
    }

    /// <summary>
    /// Real spin executed server-side.
    /// Deducts wager atomically, calculates bias-adjusted outcome, computes safe physics angle,
    /// credits payout if won, and records immutable ledger in Firebase.
    /// </summary>
    public async Task<SpinResult> ExecuteRealSpinAsync(string userId, BetSide chosenSide, decimal wagerAmount)
    {
        if (wagerAmount < MinBet || wagerAmount > MaxBet)
        {
            return new SpinResult
            {
                Success = false,
                ErrorMessage = $"Wager must be between R{MinBet:F2} and R{MaxBet:F2}."
            };
        }

        // 1. Atomic balance deduction
        var deductResult = await _firebaseService.DeductWagerAsync(userId, wagerAmount);
        if (!deductResult.Success)
        {
            return new SpinResult
            {
                Success = false,
                ErrorMessage = deductResult.ErrorMessage,
                NewBalance = deductResult.NewBalance
            };
        }

        decimal balanceAfterDeduct = deductResult.NewBalance;

        // 2. Server-side authoritative outcome calculation based on HouseBiasRating
        double winProbability = CalculateWinProbability();
        double roll = RandomNumberGenerator.GetInt32(0, 100000) / 100000.0;
        bool isWin = roll < winProbability;

        BetSide winningSide = isWin 
            ? chosenSide 
            : (chosenSide == BetSide.North ? BetSide.South : BetSide.North);

        // 3. Generate safe landing angle strictly in the target hemisphere
        double targetAngle = GenerateSafeAngleForSide(winningSide);

        // 4. Calculate payout
        decimal payout = isWin ? Math.Round(wagerAmount * Multiplier, 2) : 0.00m;
        decimal finalBalance = balanceAfterDeduct;

        if (isWin && payout > 0)
        {
            var creditResult = await _firebaseService.CreditWinAsync(userId, payout);
            if (creditResult.Success)
            {
                finalBalance = creditResult.NewBalance;
            }
        }

        // 5. Generate identifiers & server seed
        string betId = "bet_" + Guid.NewGuid().ToString("N")[..12];
        string roundId = "rnd_" + Guid.NewGuid().ToString("N")[..12];
        string serverSeed = GenerateServerSeed();

        // 6. Record Bet and GameRound
        var bet = new Bet
        {
            BetId = betId,
            UserId = userId,
            Amount = wagerAmount,
            ChosenSide = chosenSide == BetSide.North ? "NORTH" : "SOUTH",
            WinningSide = winningSide == BetSide.North ? "NORTH" : "SOUTH",
            Payout = payout,
            Timestamp = DateTime.UtcNow,
            ServerSeed = serverSeed
        };

        var round = new GameRound
        {
            RoundId = roundId,
            FinalAngle = targetAngle,
            Outcome = winningSide == BetSide.North ? "NORTH" : "SOUTH",
            CreatedAt = DateTime.UtcNow
        };

        // Fire-and-forget record or await
        await Task.WhenAll(
            _firebaseService.RecordBetAsync(bet),
            _firebaseService.RecordGameRoundAsync(round)
        );

        // Vary duration slightly for realistic natural physics feel (3.8s to 4.2s)
        double durationSeconds = 3.9 + (RandomNumberGenerator.GetInt32(0, 300) / 1000.0);

        return new SpinResult
        {
            Success = true,
            IsTrial = false,
            IsWin = isWin,
            WinningSide = winningSide,
            TargetAngle = targetAngle,
            DurationSeconds = durationSeconds,
            WagerAmount = wagerAmount,
            Payout = payout,
            NewBalance = finalBalance,
            RoundId = roundId,
            BetId = betId,
            ServerSeed = serverSeed
        };
    }

    /// <summary>
    /// Generates a random landing degree strictly within the target hemisphere zone,
    /// guaranteeing that it rejects any angles within +/- 10 degrees of 90° and 270°.
    /// North zone: [300° to 60°] across 0°/360°.
    /// South zone: [120° to 240°].
    /// </summary>
    public double GenerateSafeAngleForSide(BetSide side)
    {
        double angle;
        int attempts = 0;

        do
        {
            attempts++;
            if (side == BetSide.North)
            {
                // North zone: 300° to 360° or 0° to 60° (total 120° span centered at 0°)
                // Random offset between -58° and +58° around 0° (keeps 2° extra cushion from boundaries)
                double offset = (RandomNumberGenerator.GetInt32(-5800, 5801)) / 100.0;
                angle = (360.0 + offset) % 360.0;
            }
            else
            {
                // South zone: 120° to 240° (total 120° span centered at 180°)
                // Random offset between -58° and +58° around 180°
                double offset = (RandomNumberGenerator.GetInt32(-5800, 5801)) / 100.0;
                angle = 180.0 + offset;
            }
        } while (!IsSafeAngle(angle) && attempts < 10);

        return Math.Round(angle, 2);
    }

    /// <summary>
    /// Strictly rejects angles within +/- 10 degrees of dividing boundary lines (90° and 270°).
    /// </summary>
    public bool IsSafeAngle(double angle)
    {
        angle = ((angle % 360.0) + 360.0) % 360.0;

        // Rejected zones: [80°, 100°] and [260°, 280°]
        if (angle >= 80.0 && angle <= 100.0)
            return false;

        if (angle >= 260.0 && angle <= 280.0)
            return false;

        return true;
    }

    public BetSide GetSideFromAngle(double angle)
    {
        angle = ((angle % 360.0) + 360.0) % 360.0;

        // North zone corresponds to [300° to 60°]
        if (angle >= 300.0 || angle <= 60.0)
            return BetSide.North;

        // South zone corresponds to [120° to 240°]
        if (angle >= 120.0 && angle <= 240.0)
            return BetSide.South;

        // If in intermediate zone, pick closest hemisphere
        if (angle > 60.0 && angle < 120.0)
            return angle < 90.0 ? BetSide.North : BetSide.South;

        return angle < 270.0 ? BetSide.South : BetSide.North;
    }

    private static string GenerateServerSeed()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
