using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Qixr.Companion
{
    public static class QuantumCompanionConfiguration
    {
        public const string FileName = "qixr-companion.json";
        public static string OverridePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Validate(CompanionConfig config, out string error)
        {
            error = null;
            if (config == null) error = "Pairing configuration is missing.";
            else if (!Uri.TryCreate(config.serverUrl, UriKind.Absolute, out var url) ||
                url.Scheme != Uri.UriSchemeHttps || url.IsLoopback || !string.IsNullOrEmpty(url.UserInfo) ||
                url.AbsolutePath != "/" || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
                error = "Use an HTTPS LAN hostname or address, without a path. Localhost is the headset.";
            else if (!Regex.IsMatch(config.certificateSha256 ?? "", "\\A[0-9a-fA-F]{64}\\z"))
                error = "A 64-digit SHA-256 leaf certificate fingerprint is required.";
            else if (!Regex.IsMatch(config.roomId ?? "", "\\A[a-zA-Z0-9_-]{1,64}\\z"))
                error = "Room ID must contain 1-64 letters, numbers, underscores or hyphens.";
            else if (config.authenticationMode != "token" && config.authenticationMode != "password")
                error = "Authentication mode must be token or password.";
            else if ((config.authenticationMode == "token" && InvalidToken(config.joinToken)) ||
                (!string.IsNullOrEmpty(config.authorityToken) && InvalidToken(config.authorityToken)))
                error = "Install real role-specific pairing credentials from the operator.";
            else if (config.authenticationMode == "password" && !string.IsNullOrEmpty(config.joinToken))
                error = "Password profiles must not embed a room join token.";
            else if (config.language != "en" && config.language != "ko") error = "Language must be en or ko.";
            else if (config.simulationMode != "standalone" && config.simulationMode != "fusion")
                error = "Simulation mode must be standalone or fusion.";
            else if (string.IsNullOrEmpty(config.authorityToken) && config.simulationMode != "fusion")
                error = "A join-only participant requires Fusion mode and the designated authority.";
            else if ((config.participantId ?? "").Length > 96 || (config.participantId ?? "").IndexOfAny(new[] {'/', '\\', '\r', '\n'}) >= 0)
                error = "Participant ID is invalid. Leave it empty for a device-specific identity.";
            return error == null;
        }

        private static bool InvalidToken(string token) => string.IsNullOrWhiteSpace(token) ||
            token.Length > 2048 || Regex.IsMatch(token, "\\s") ||
            Regex.IsMatch(token, "replace|placeholder|from_operator|room_join_token|empty_for", RegexOptions.IgnoreCase);

        public static bool TryParse(string json, out CompanionConfig config, out string error)
        {
            config = null;
            try
            {
                config = JsonUtility.FromJson<CompanionConfig>(json);
                if(config!=null && json.IndexOf("\"authenticationMode\"",StringComparison.Ordinal)<0)
                    config.authenticationMode="token";
                if(config!=null && json.IndexOf("\"simulationMode\"",StringComparison.Ordinal)<0)
                    config.simulationMode=string.IsNullOrEmpty(config.authorityToken)?"fusion":"standalone";
            }
            catch (Exception) { error = "Pairing JSON could not be read."; return false; }
            return Validate(config, out error);
        }

        public static void SaveOverride(string json)
        {
            if (!TryParse(json, out _, out var error)) throw new ArgumentException(error);
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporary = OverridePath + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(OverridePath)) File.Replace(temporary, OverridePath, null);
            else File.Move(temporary, OverridePath);
        }
    }
}
