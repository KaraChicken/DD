namespace DDTank.LocalData
{
    /// <summary>Player state returned to login orchestration and Flash XML serialization.</summary>
    public sealed class PlayerLoginState
    {
        /// <summary>Creates a player login state snapshot.</summary>
        public PlayerLoginState(
            long playerId,
            string userName,
            string nickname,
            int sex,
            int isFirst,
            int isExist,
            int gp,
            int grade,
            int gold,
            int money,
            int hide,
            string style,
            string colors,
            string skin,
            int attack,
            int defence,
            int agility,
            int luck,
            string createdUtc)
        {
            PlayerId = playerId;
            UserName = userName;
            Nickname = nickname;
            Sex = sex;
            IsFirst = isFirst;
            IsExist = isExist;
            GP = gp;
            Grade = grade;
            Gold = gold;
            Money = money;
            Hide = hide;
            Style = style;
            Colors = colors;
            Skin = skin;
            Attack = attack;
            Defence = defence;
            Agility = agility;
            Luck = luck;
            CreatedUtc = createdUtc;
        }

        /// <summary>Gets the player identifier.</summary>
        public long PlayerId { get; }
        /// <summary>Gets the account name.</summary>
        public string UserName { get; }
        /// <summary>Gets the character nickname, empty while awaiting first-character creation.</summary>
        public string Nickname { get; }
        /// <summary>Gets the stored sex value (1 male, 0 female).</summary>
        public int Sex { get; }
        /// <summary>Gets the legacy first-character state flag.</summary>
        public int IsFirst { get; }
        /// <summary>Gets whether the player is active.</summary>
        public int IsExist { get; }
        /// <summary>Gets experience points.</summary>
        public int GP { get; }
        /// <summary>Gets character level.</summary>
        public int Grade { get; }
        /// <summary>Gets gold.</summary>
        public int Gold { get; }
        /// <summary>Gets money.</summary>
        public int Money { get; }
        /// <summary>Gets the appearance visibility mask.</summary>
        public int Hide { get; }
        /// <summary>Gets the character appearance style string.</summary>
        public string Style { get; }
        /// <summary>Gets the character appearance color string.</summary>
        public string Colors { get; }
        /// <summary>Gets the skin identifier.</summary>
        public string Skin { get; }
        /// <summary>Gets attack.</summary>
        public int Attack { get; }
        /// <summary>Gets defence.</summary>
        public int Defence { get; }
        /// <summary>Gets agility.</summary>
        public int Agility { get; }
        /// <summary>Gets luck.</summary>
        public int Luck { get; }
        /// <summary>Gets the player creation timestamp.</summary>
        public string CreatedUtc { get; }
        /// <summary>Gets whether the Flash client still needs to create its first character.</summary>
        public bool RequiresCharacterCreation => string.IsNullOrEmpty(Nickname);
    }
}
