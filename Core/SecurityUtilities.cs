using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BhavaniSupportSuite.Core;

public static class SecurityUtilities
{
    // Input sanitization for command line arguments
    private static readonly Regex ShellMetaChars = new(@"[;&|`$(){}[\]\\<>'""]", RegexOptions.Compiled);
    private static readonly Regex PathTraversal = new(@"\.\./|\.\.\\", RegexOptions.Compiled);
    private static readonly Regex ValidArgChars = new(@"^[a-zA-Z0-9._\-/:@\\]*$", RegexOptions.Compiled);
    
    // DPAPI encryption for sensitive data
    public static string Protect(string plainText, DataProtectionScope scope = DataProtectionScope.CurrentUser)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var data = Encoding.UTF8.GetBytes(plainText);
        var protectedData = ProtectedData.Protect(data, null, scope);
        return Convert.ToBase64String(protectedData);
    }

    public static string Unprotect(string protectedText, DataProtectionScope scope = DataProtectionScope.CurrentUser)
    {
        if (string.IsNullOrEmpty(protectedText)) return string.Empty;
        try
        {
            var data = Convert.FromBase64String(protectedText);
            var unprotectedData = ProtectedData.Unprotect(data, null, scope);
            return Encoding.UTF8.GetString(unprotectedData);
        }
        catch
        {
            return string.Empty; // Return empty if decryption fails (corrupted/old data)
        }
    }

    // Sanitize command line arguments to prevent injection
    public static string SanitizeArgument(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        // Remove shell metacharacters
        var sanitized = ShellMetaChars.Replace(input, string.Empty);
        
        // Remove path traversal attempts
        sanitized = PathTraversal.Replace(sanitized, string.Empty);
        
        // Only allow safe characters
        if (!ValidArgChars.IsMatch(sanitized))
        {
            // Fallback: only keep alphanumeric and safe punctuation
            var sb = new StringBuilder();
            foreach (char c in sanitized)
            {
                if (char.IsLetterOrDigit(c) || "._-/@\\".Contains(c))
                    sb.Append(c);
            }
            sanitized = sb.ToString();
        }
        
        // Trim and limit length
        sanitized = sanitized.Trim();
        return sanitized.Length > 260 ? sanitized.Substring(0, 260) : sanitized;
    }

    // Sanitize file paths
    public static string SanitizePath(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        var sanitized = PathTraversal.Replace(input, string.Empty);
        
        // Allow only valid path characters
        var invalidChars = Path.GetInvalidPathChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c.ToString(), string.Empty);
        }
        
        sanitized = sanitized.Trim();
        if (sanitized.Length == 0) return string.Empty;
        return sanitized.Length > 260 ? sanitized.Substring(0, 260) : sanitized;
    }

    // Validate IP address format
    public static bool IsValidIpAddress(string ip)
    {
        return System.Net.IPAddress.TryParse(ip, out _);
    }

    // Validate CIDR notation
    public static bool IsValidCidr(string cidr)
    {
        if (string.IsNullOrEmpty(cidr)) return false;
        var parts = cidr.Split('/');
        if (parts.Length != 2) return false;
        if (!System.Net.IPAddress.TryParse(parts[0], out _)) return false;
        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32) return false;
        return true;
    }

    // Validate port number
    public static bool IsValidPort(int port)
    {
        return port > 0 && port <= 65535;
    }

    // Compute SHA256 checksum
    public static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Compute SHA256 for byte array
    public static string ComputeSha256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Verify file checksum
    public static bool VerifyChecksum(string filePath, string expectedChecksum)
    {
        try
        {
            var actual = ComputeSha256(filePath);
            return string.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // Generate secure random string
    public static string GenerateSecureRandom(int length = 32)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
    
    // Validate that input contains only safe characters for command arguments
    public static bool IsSafeArgument(string input)
    {
        if (string.IsNullOrEmpty(input)) return true;
        return ValidArgChars.IsMatch(input);
    }
}