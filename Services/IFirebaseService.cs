using SpinTheBottle.Models;

namespace SpinTheBottle.Services;

public interface IFirebaseService
{
    Task<User?> GetUserByIdAsync(string userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> CreateUserAsync(string email, string passwordHash, decimal initialBonus = 50.00m, bool isTrialCompleted = true);
    Task UpdateUserAsync(User user);
    Task<(bool Success, decimal NewBalance, string ErrorMessage)> DeductWagerAsync(string userId, decimal amount);
    Task<(bool Success, decimal NewBalance)> CreditWinAsync(string userId, decimal payout);
    Task RecordBetAsync(Bet bet);
    Task RecordGameRoundAsync(GameRound round);
    Task<List<Bet>> GetRecentBetsAsync(int count = 10);
    Task<List<Bet>> GetUserBetsAsync(string userId, int count = 10);
    Task<List<GameRound>> GetRecentRoundsAsync(int count = 10);
    Task SetTrialCompletedAsync(string userId);
    Task SaveUserContactsAsync(string userId, List<ContactItem> contacts);
    Task<List<ContactItem>> GetUserContactsAsync(string userId);
}
