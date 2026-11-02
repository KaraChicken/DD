namespace DDTank.Web.Infrastructure
{
    /// <summary>Authenticated local player and the newly issued short-lived game ticket.</summary>
    public sealed class GameLoginResult
    {
        /// <summary>Creates a game login result.</summary>
        public GameLoginResult(long accountId, DDTank.LocalData.PlayerLoginState player, string gameTicket, int expiresInSeconds)
        {
            AccountId = accountId;
            Player = player;
            GameTicket = gameTicket;
            ExpiresInSeconds = expiresInSeconds;
        }

        /// <summary>Gets the account identifier.</summary>
        public long AccountId { get; }
        /// <summary>Gets the local player state.</summary>
        public DDTank.LocalData.PlayerLoginState Player { get; }
        /// <summary>Gets the opaque game ticket. Do not log this value.</summary>
        public string GameTicket { get; }
        /// <summary>Gets the ticket lifetime in seconds.</summary>
        public int ExpiresInSeconds { get; }
    }
}
