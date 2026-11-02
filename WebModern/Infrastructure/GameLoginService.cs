using System;
using System.Security.Cryptography;
using DDTank.LocalData;
using Microsoft.AspNetCore.Identity;

namespace DDTank.Web.Infrastructure
{
    /// <summary>Authenticates local accounts and issues short-lived opaque game tickets.</summary>
    public sealed class GameLoginService
    {
        private readonly CharacterCreationRepository _characters;
        private readonly IPasswordHasher<string> _passwordHasher;

        /// <summary>Creates the local game-login service.</summary>
        public GameLoginService(
            CharacterCreationRepository characters,
            IPasswordHasher<string> passwordHasher)
        {
            _characters = characters;
            _passwordHasher = passwordHasher;
        }

        /// <summary>Authenticates an account and issues a new random game ticket.</summary>
        public GameLoginResult? Login(string userName, string password, string? activeIp = null, string? site = null)
        {
            return LoginWithTicket(userName, password, GenerateTicket(), activeIp, site);
        }

        /// <summary>Authenticates an account and binds the supplied legacy Flash session password as its ticket.</summary>
        public GameLoginResult? LoginWithTicket(
            string userName,
            string password,
            string gameTicket,
            string? activeIp = null,
            string? site = null)
        {
            if (string.IsNullOrWhiteSpace(gameTicket))
                return null;

            var account = _characters.FindAccountByUserName(userName);
            if (account == null || !account.IsActive || string.IsNullOrEmpty(password))
                return null;

            var verification = _passwordHasher.VerifyHashedPassword(
                account.UserName, account.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
                return null;

            var player = _characters.GetPlayerLoginState(account.AccountId);
            if (player == null)
            {
                _characters.CreatePendingPlayer(account.AccountId, gameTicket, false, activeIp, site);
            }
            else
            {
                if (player.IsExist == 0)
                    return null;
                _characters.UpdateGameTicket(account.AccountId, gameTicket);
            }

            player = _characters.GetPlayerLoginState(account.AccountId);
            if (player == null || player.IsExist == 0)
                return null;

            return new GameLoginResult(account.AccountId, player, gameTicket, 300);
        }

        private static string GenerateTicket()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
