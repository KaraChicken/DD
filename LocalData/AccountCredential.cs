using System;

namespace DDTank.LocalData
{
    /// <summary>Account identity and password hash returned to the authentication layer.</summary>
    public sealed class AccountCredential
    {
        /// <summary>Creates an account credential snapshot.</summary>
        public AccountCredential(long accountId, string userName, string passwordHash, bool isActive)
        {
            AccountId = accountId;
            UserName = userName;
            PasswordHash = passwordHash;
            IsActive = isActive;
        }

        /// <summary>Gets the local account identifier.</summary>
        public long AccountId { get; }

        /// <summary>Gets the account name.</summary>
        public string UserName { get; }

        /// <summary>Gets the stored password hash; never return this value to clients.</summary>
        public string PasswordHash { get; }

        /// <summary>Gets whether the account is active.</summary>
        public bool IsActive { get; }
    }
}
