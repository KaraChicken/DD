using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DDTank.Web.Infrastructure
{
    /// <summary>Applies the legacy Flash nickname normalization and validation rules.</summary>
    public sealed class NicknamePolicy
    {
        private readonly int _maximumBytes;
        private readonly Encoding _encoding;
        private readonly HashSet<char> _illegalCharacters = new HashSet<char>();
        private readonly string[] _blockedTerms;

        /// <summary>Loads nickname settings and the legacy illegal-character list.</summary>
        public NicknamePolicy(IConfiguration configuration, IHostEnvironment environment)
        {
            var section = configuration.GetSection("CharacterCreation");
            _maximumBytes = section.GetValue("MaxNicknameBytes", 14);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var ansiCodePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            _encoding = Encoding.GetEncoding(ansiCodePage);

            var relativePath = section["IllegalCharactersFile"] ?? "Data/Illegalcharacters.txt";
            var path = Path.Combine(environment.ContentRootPath, relativePath);
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException("The legacy nickname illegal-character list is missing.", path);

            var lines = File.ReadAllLines(path);
            if (lines.Length > 0)
            {
                foreach (var character in lines[0])
                    _illegalCharacters.Add(character);
            }

            _blockedTerms = lines
                .Skip(1)
                .SelectMany(line => line.Split('|'))
                .Select(term => term.Trim())
                .Where(term => term.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>Normalizes and validates a nickname before first-character creation.</summary>
        public NicknameValidationResult Validate(string? rawNickname)
        {
            var nickname = (rawNickname ?? string.Empty).Trim().Replace(",", string.Empty);
            if (nickname.Length == 0)
                return NicknameValidationResult.Invalid(nickname, "Nickname is required.");

            if (_encoding.GetByteCount(nickname) > _maximumBytes)
                return NicknameValidationResult.Invalid(nickname, "Nickname is too long.");

            if (nickname.Any(character => _illegalCharacters.Contains(character)))
                return NicknameValidationResult.Invalid(nickname, "Nickname contains an illegal character.");

            foreach (var term in _blockedTerms)
            {
                if (nickname.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return NicknameValidationResult.Invalid(nickname, "Nickname contains a blocked term.");
            }

            return NicknameValidationResult.Valid(nickname);
        }
    }

    /// <summary>Outcome of applying the nickname policy.</summary>
    public sealed class NicknameValidationResult
    {
        private NicknameValidationResult(bool isValid, string nickname, string? errorMessage)
        {
            IsValid = isValid;
            Nickname = nickname;
            ErrorMessage = errorMessage;
        }

        /// <summary>Gets whether the nickname passed validation.</summary>
        public bool IsValid { get; }

        /// <summary>Gets the normalized nickname.</summary>
        public string Nickname { get; }

        /// <summary>Gets the validation error when invalid.</summary>
        public string? ErrorMessage { get; }

        internal static NicknameValidationResult Valid(string nickname)
        {
            return new NicknameValidationResult(true, nickname, null);
        }

        internal static NicknameValidationResult Invalid(string nickname, string errorMessage)
        {
            return new NicknameValidationResult(false, nickname, errorMessage);
        }
    }
}
