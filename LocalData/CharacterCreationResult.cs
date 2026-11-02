using System;

namespace DDTank.LocalData
{
    /// <summary>Result codes compatible with the legacy first-character registration procedure.</summary>
    public sealed class CharacterCreationResult
    {
        public CharacterCreationResult(int resultCode, long playerId)
        {
            ResultCode = resultCode;
            PlayerId = playerId;
        }

        /// <summary>0 = success, 2 = nickname already used, 3 = player is not awaiting creation.</summary>
        public int ResultCode { get; }

        /// <summary>The local player identifier, or zero when no pending player exists.</summary>
        public long PlayerId { get; }

        public bool Success => ResultCode == 0;
    }
}
