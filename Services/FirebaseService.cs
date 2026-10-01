using System.Collections.Concurrent;
using Firebase.Database;
using Firebase.Database.Query;
using SpinTheBottle.Models;

namespace SpinTheBottle.Services;

public class FirebaseService : IFirebaseService
{
    private readonly FirebaseClient? _client;
    private readonly ILogger<FirebaseService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _userLocks = new();
    
    // In-memory fallback and cache store to guarantee high responsiveness and resilience
    private readonly ConcurrentDictionary<string, User> _userCache = new();
    private readonly ConcurrentDictionary<string, Bet> _betCache = new();
    private readonly ConcurrentDictionary<string, GameRound> _roundCache = new();
    private readonly ConcurrentDictionary<string, List<ContactItem>> _contactCache = new();

    public FirebaseService(IConfiguration configuration, ILogger<FirebaseService> logger)
    {
        _logger = logger;
        string? databaseUrl = configuration["Firebase:DatabaseUrl"];
        string? authSecret = configuration["Firebase:AuthSecret"];

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(authSecret))
                {
                    _client = new FirebaseClient(databaseUrl, new FirebaseOptions
                    {
                        AuthTokenAsyncFactory = () => Task.FromResult<string?>(authSecret)
                    });
                }
                else
                {
                    _client = new FirebaseClient(databaseUrl);
                }
                _logger.LogInformation("FirebaseClient initialized with endpoint: {Url}", databaseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FirebaseClient. Local fallback will be used.");
                _client = null;
            }
        }
        else
        {
            _logger.LogWarning("Firebase:DatabaseUrl not configured. In-memory mode active.");
        }
    }

    private SemaphoreSlim GetUserLock(string userId)
    {
        return _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        if (_client != null)
        {
            try
            {
                var user = await _client.Child("Users").Child(userId).OnceSingleAsync<User>();
                if (user != null && !string.IsNullOrWhiteSpace(user.Id))
                {
                    _userCache[userId] = user;
                    return user;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching user {UserId} from Firebase. Using cache.", userId);
            }
        }

        _userCache.TryGetValue(userId, out var cachedUser);
        return cachedUser;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        string normalizedEmail = email.Trim().ToLowerInvariant();

        // Check local cache first
        var cached = _userCache.Values.FirstOrDefault(u => u.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
        if (cached != null)
            return cached;

        if (_client != null)
        {
            try
            {
                var users = await _client.Child("Users").OnceAsync<User>();
                foreach (var item in users)
                {
                    if (item.Object != null && item.Object.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        _userCache[item.Object.Id] = item.Object;
                        return item.Object;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying users by email {Email} from Firebase.", normalizedEmail);
            }
        }

        return null;
    }

    public async Task<User> CreateUserAsync(string email, string passwordHash, decimal initialBonus = 50.00m, bool isTrialCompleted = true)
    {
        string userId = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = userId,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Balance = initialBonus,
            IsTrialCompleted = isTrialCompleted,
            CreatedAt = DateTime.UtcNow
        };

        _userCache[userId] = user;

        if (_client != null)
        {
            try
            {
                await _client.Child("Users").Child(userId).PutAsync(user);
                _logger.LogInformation("Created new user {UserId} with R{Bonus} in Firebase", userId, initialBonus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist new user {UserId} to Firebase. Stored locally.", userId);
            }
        }

        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        _userCache[user.Id] = user;

        if (_client != null)
        {
            try
            {
                await _client.Child("Users").Child(user.Id).PutAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user {UserId} in Firebase.", user.Id);
            }
        }
    }

    public async Task<(bool Success, decimal NewBalance, string ErrorMessage)> DeductWagerAsync(string userId, decimal amount)
    {
        if (amount <= 0)
            return (false, 0m, "Invalid wager amount.");

        var userLock = GetUserLock(userId);
        await userLock.WaitAsync();

        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return (false, 0m, "User not found.");

            if (user.Balance < amount)
                return (false, user.Balance, $"Insufficient balance. Current balance is R{user.Balance:F2}.");

            user.Balance -= amount;
            await UpdateUserAsync(user);

            return (true, user.Balance, string.Empty);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task<(bool Success, decimal NewBalance)> CreditWinAsync(string userId, decimal payout)
    {
        if (payout <= 0)
        {
            var existingUser = await GetUserByIdAsync(userId);
            return (true, existingUser?.Balance ?? 0m);
        }

        var userLock = GetUserLock(userId);
        await userLock.WaitAsync();

        try
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null)
                return (false, 0m);

            user.Balance += payout;
            await UpdateUserAsync(user);

            return (true, user.Balance);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task RecordBetAsync(Bet bet)
    {
        _betCache[bet.BetId] = bet;

        if (_client != null)
        {
            try
            {
                await _client.Child("Bets").Child(bet.BetId).PutAsync(bet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Bet {BetId} to Firebase.", bet.BetId);
            }
        }
    }

    public async Task RecordGameRoundAsync(GameRound round)
    {
        _roundCache[round.RoundId] = round;

        if (_client != null)
        {
            try
            {
                await _client.Child("GameRounds").Child(round.RoundId).PutAsync(round);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save GameRound {RoundId} to Firebase.", round.RoundId);
            }
        }
    }

    public async Task<List<Bet>> GetRecentBetsAsync(int count = 10)
    {
        if (_client != null)
        {
            try
            {
                var bets = await _client.Child("Bets").OrderByKey().LimitToLast(count).OnceAsync<Bet>();
                var list = bets.Select(b => b.Object).Where(b => b != null).OrderByDescending(b => b.Timestamp).ToList();
                if (list.Count > 0)
                {
                    foreach (var b in list) _betCache[b.BetId] = b;
                    return list;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch recent bets from Firebase. Using local cache.");
            }
        }

        return _betCache.Values.OrderByDescending(b => b.Timestamp).Take(count).ToList();
    }

    public async Task<List<Bet>> GetUserBetsAsync(string userId, int count = 10)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new List<Bet>();

        if (_client != null)
        {
            try
            {
                var bets = await _client.Child("Bets")
                    .OrderBy("userId")
                    .EqualTo(userId)
                    .LimitToLast(count)
                    .OnceAsync<Bet>();

                var list = bets.Select(b => b.Object).Where(b => b != null).OrderByDescending(b => b.Timestamp).ToList();
                if (list.Count > 0)
                    return list;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query user bets from Firebase. Using local cache.");
            }
        }

        return _betCache.Values
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.Timestamp)
            .Take(count)
            .ToList();
    }

    public async Task<List<GameRound>> GetRecentRoundsAsync(int count = 10)
    {
        if (_client != null)
        {
            try
            {
                var rounds = await _client.Child("GameRounds").OrderByKey().LimitToLast(count).OnceAsync<GameRound>();
                var list = rounds.Select(r => r.Object).Where(r => r != null).OrderByDescending(r => r.CreatedAt).ToList();
                if (list.Count > 0)
                {
                    foreach (var r in list) _roundCache[r.RoundId] = r;
                    return list;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch recent rounds from Firebase. Using local cache.");
            }
        }

        return _roundCache.Values.OrderByDescending(r => r.CreatedAt).Take(count).ToList();
    }

    public async Task SetTrialCompletedAsync(string userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user != null)
        {
            user.IsTrialCompleted = true;
            await UpdateUserAsync(user);
        }
    }

    public async Task SaveUserContactsAsync(string userId, List<ContactItem> contacts)
    {
        if (string.IsNullOrWhiteSpace(userId) || contacts == null)
            return;

        // Clone to preserve current snapshot
        var snapshot = contacts.Select(c => new ContactItem
        {
            Id = c.Id,
            Name = c.Name,
            PhoneNumber = c.PhoneNumber,
            IsSelected = c.IsSelected,
            AvatarColor = c.AvatarColor
        }).ToList();

        _contactCache[userId] = snapshot;

        if (_client != null)
        {
            try
            {
                // Securely save under Users/{userId}/Contacts in Firebase Realtime Database
                await _client.Child("Users").Child(userId).Child("Contacts").PutAsync(snapshot);
                _logger.LogInformation("Securely saved {Count} contacts for user {UserId} in Firebase.", snapshot.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist contacts for user {UserId} to Firebase. Cached locally.", userId);
            }
        }
    }

    public async Task<List<ContactItem>> GetUserContactsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new List<ContactItem>();

        if (_contactCache.TryGetValue(userId, out var cached) && cached.Any())
            return cached;

        if (_client != null)
        {
            try
            {
                var contacts = await _client.Child("Users").Child(userId).Child("Contacts").OnceSingleAsync<List<ContactItem>>();
                if (contacts != null)
                {
                    _contactCache[userId] = contacts;
                    return contacts;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching contacts for user {UserId} from Firebase.", userId);
            }
        }

        return new List<ContactItem>();
    }
}
