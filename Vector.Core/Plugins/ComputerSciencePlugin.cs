using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace Vector.Core.Plugins;

public class ComputerSciencePlugin
{
    [KernelFunction, Description("Converts numbers between different bases (Binary, Octal, Decimal, Hex).")]
    public string BaseConvert(
        [Description("The number to convert (as a string)")] string number,
        [Description("From Base (2, 8, 10, 16)")] int fromBase,
        [Description("To Base (2, 8, 10, 16)")] int toBase)
    {
        try
        {
            // Convert to integer (Long to support 64-bit)
            long value = Convert.ToInt64(number, fromBase);

            // Convert to target base
            string result = toBase switch
            {
                2 => Convert.ToString(value, 2),
                8 => Convert.ToString(value, 8),
                10 => value.ToString(),
                16 => Convert.ToString(value, 16).ToUpper(),
                _ => throw new ArgumentException("Unsupported base. Use 2, 8, 10, or 16.")
            };

            if (toBase == 16) return "0x" + result;
            if (toBase == 2) return "0b" + result;
            return result;
        }
        catch (Exception ex)
        {
            return $"Conversion Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Computes cryptographic hashes (MD5, SHA1, SHA256, SHA512) for a given string.")]
    public string ComputeHash(
        [Description("The input string")] string input,
        [Description("The algorithm (MD5, SHA256, SHA512)")] string algorithm)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes;

            // FIX: Use specific classes instead of obsolete 'HashAlgorithm.Create'
            using HashAlgorithm hasher = algorithm.ToUpperInvariant() switch
            {
                "MD5" => MD5.Create(),
                "SHA1" => SHA1.Create(),
                "SHA256" => SHA256.Create(),
                "SHA512" => SHA512.Create(),
                _ => throw new ArgumentException("Unknown algorithm. Try MD5, SHA256, or SHA512.")
            };

            hashBytes = hasher.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            return $"Hashing Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Performs bitwise operations (AND, OR, XOR, NOT, LSHIFT, RSHIFT) on integers.")]
    public string BitwiseOp(
        [Description("Operation: AND, OR, XOR, NOT, LSHIFT, RSHIFT")] string op,
        [Description("Value A")] long a,
        [Description("Value B (Ignored for NOT)")] long b = 0)
    {
        try
        {
            return op.ToUpperInvariant() switch
            {
                "AND" => (a & b).ToString(),
                "OR" => (a | b).ToString(),
                "XOR" => (a ^ b).ToString(),
                "NOT" => (~a).ToString(),
                "LSHIFT" => (a << (int)b).ToString(),
                "RSHIFT" => (a >> (int)b).ToString(),
                _ => "Error: Unknown bitwise operation"
            };
        }
        catch (Exception ex)
        {
            return $"Bitwise Error: {ex.Message}";
        }
    }

    [KernelFunction, Description("Converts data sizes (e.g., Bytes to MB, GB to KB).")]
    public string DataUnitConvert(
        [Description("Value")] double value,
        [Description("From Unit (B, KB, MB, GB, TB)")] string fromUnit,
        [Description("To Unit (B, KB, MB, GB, TB)")] string toUnit)
    {
        try
        {
            double bytes = fromUnit.ToUpperInvariant() switch
            {
                "B" => value,
                "KB" => value * 1024,
                "MB" => value * Math.Pow(1024, 2),
                "GB" => value * Math.Pow(1024, 3),
                "TB" => value * Math.Pow(1024, 4),
                _ => throw new ArgumentException("Unknown source unit")
            };

            double result = toUnit.ToUpperInvariant() switch
            {
                "B" => bytes,
                "KB" => bytes / 1024,
                "MB" => bytes / Math.Pow(1024, 2),
                "GB" => bytes / Math.Pow(1024, 3),
                "TB" => bytes / Math.Pow(1024, 4),
                _ => throw new ArgumentException("Unknown target unit")
            };

            return result.ToString("F4");
        }
        catch (Exception ex)
        {
            return $"Unit Error: {ex.Message}";
        }
    }
}