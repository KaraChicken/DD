using System;

namespace DDTank.LocalData
{
    /// <summary>Result codes compatible with the legacy first-character registration procedure.</summary>
    public sealed class CharacterCreationResult
    {
        /// <summary>Creates a result using the legacy procedure return code.</summary>
        public CharacterCreationResult(int resultCode, long playerId)
        {
            ResultCode = resultCode;
            PlayerId = playerId;
        }

        /// <summary>0 = success, 2 = nickname already used, 3 = player is not awaiting creation.</summary>
        public int ResultCode { get; }

        /// <summary>The local player identifier, or zero when no pending player exists.</summary>
        public long PlayerId { get; }

        /// <summary>Gets whether the character was created.</summary>
        public bool Success => ResultCode == 0;
    }
}
