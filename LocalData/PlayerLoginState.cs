namespace DDTank.LocalData
{
    /// <summary>Player state returned to login orchestration.</summary>
    public sealed class PlayerLoginState
    {
        /// <summary>Creates a player login state snapshot.</summary>
        public PlayerLoginState(long playerId, string nickname, int sex, int isFirst)
        {
            PlayerId = playerId;
            Nickname = nickname;
            Sex = sex;
            IsFirst = isFirst;
        }

        /// <summary>Gets the player identifier.</summary>
        public long PlayerId { get; }

        /// <summary>Gets the character nickname, empty while awaiting first-character creation.</summary>
        public string Nickname { get; }

        /// <summary>Gets the stored sex value (1 male, 0 female).</summary>
        public int Sex { get; }

        /// <summary>Gets the legacy first-character state flag.</summary>
        public int IsFirst { get; }

        /// <summary>Gets whether the Flash client still needs to create its first character.</summary>
        public bool RequiresCharacterCreation => string.IsNullOrEmpty(Nickname);
    }
}
