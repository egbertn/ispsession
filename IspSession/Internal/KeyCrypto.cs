using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace NCV.ISPSession.Internal;

internal static class KeyCrypto
{
    private const int MAX_STACK = 2000;
    private static readonly ConcurrentDictionary<string, byte[]> _pwCache = [];
    private static readonly byte[] FixedSalt = Encoding.UTF8.GetBytes("ispsession_v1_salt");

    private static byte[] DeriveKeyFromPassword(string password)
    {
        if (!_pwCache.TryGetValue(password, out byte[]? key))
        {
            Span<byte> pwdUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(password)];
            Encoding.UTF8.GetBytes(password, pwdUtf8);
            // OWASP recommendation for PBKDF2-SHA256 (2023+).
            // Result is cached in _pwCache so the cost is paid once per process per passphrase.
            // Bumping this is a breaking change: existing Redis entries encrypted with a different
            // iteration count cannot be decrypted and need to be flushed/expired.
            key = Rfc2898DeriveBytes.Pbkdf2(
                pwdUtf8,
                FixedSalt,
                600_000,
                HashAlgorithmName.SHA256,
                16);
            _pwCache.TryAdd(password, key);
        }
        return key;
    }

    internal static ReadOnlyMemory<byte> EncryptToBytes(Stream inputStream, string passphrase)
    {
        using Aes aes = Aes.Create();
        aes.Key = DeriveKeyFromPassword(passphrase);
        aes.GenerateIV();

        int estimatedCapacity = (int)inputStream.Length + aes.BlockSize / 8 * 2;
        using var output = new MemoryStream(estimatedCapacity);
        output.Write(aes.IV);

        using CryptoStream cryptoStream = new(output, aes.CreateEncryptor(), CryptoStreamMode.Write);
        inputStream.CopyTo(cryptoStream);
        cryptoStream.FlushFinalBlock();
        return output.ToArray();
    }

    internal static ReadOnlySpan<byte> Encrypt(ReadOnlySpan<char> clearText, string passphrase, byte[]? iv = null)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKeyFromPassword(passphrase);

        int utf8Length = Encoding.UTF8.GetByteCount(clearText);
        int estimatedCapacity = utf8Length + aes.BlockSize / 8 * 2;
        using var output = new MemoryStream(estimatedCapacity);

        if (iv == null)
        {
            aes.GenerateIV();
            output.Write(aes.IV);
        }
        else
        {
            aes.IV = iv;
        }

        using var cryptoStream = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write);
        byte[]? rented = null;
        try
        {
            Span<byte> buffer = utf8Length < MAX_STACK ? stackalloc byte[utf8Length] : (rented = ArrayPool<byte>.Shared.Rent(utf8Length));
            Encoding.UTF8.GetBytes(clearText, buffer);
            cryptoStream.Write(buffer[..utf8Length]);
            cryptoStream.FlushFinalBlock();
            return output.ToArray();
        }
        finally
        {
            if (rented != null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    internal static Stream DecryptToStream(byte[] encrypted, string passphrase)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKeyFromPassword(passphrase);
        aes.IV = encrypted.AsSpan(0, aes.BlockSize / 8).ToArray();

        MemoryStream input = new(encrypted)
        {
            Position = aes.BlockSize / 8
        };

        using CryptoStream cryptoStream = new(input, aes.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen: true);
        MemoryStream output = new();
        cryptoStream.CopyTo(output);
        output.Position = 0;
        return output;
    }
    internal static byte[] DecryptToBytes(byte[] encrypted, string passphrase)
    {
        using Aes aes = Aes.Create();
        aes.Key = DeriveKeyFromPassword(passphrase);
        aes.IV = encrypted.AsSpan(0, aes.BlockSize / 8).ToArray();

        MemoryStream input = new(encrypted)
        {
            Position = aes.BlockSize / 8
        };
        using var cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var output = new MemoryStream();
        cryptoStream.CopyTo(output);
        return output.ToArray();
    }

    internal static (bool Success, string? Value) Decrypt(byte[] encrypted, string passphrase, byte[]? iv = null)
    {
        using Aes aes = Aes.Create();
        aes.Key = DeriveKeyFromPassword(passphrase);

        MemoryStream input = new(encrypted);
        if (iv == null)
        {
            aes.IV = encrypted.AsSpan(0, aes.BlockSize / 8).ToArray();
            input.Position = aes.BlockSize / 8;
        }
        else
        {
            aes.IV = iv;
        }

        using var cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
        MemoryStream output = new();
        try
        {
            cryptoStream.CopyTo(output);
            output.Position = 0;
            using StreamReader reader = new(output);
            return (true, reader.ReadToEnd());
        }
        catch
        {
            return (false, null);
        }
    }
}
