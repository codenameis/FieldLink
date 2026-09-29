using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.FileTransfer
{
    internal static class ContentHash
    {
        internal const string HeaderName = "X-Content-SHA256";
        internal static string ToHex(byte[] value) => BitConverter.ToString(value).Replace("-", string.Empty);
        internal static bool IsValid(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char character in value)
                if (!((character >= '0' && character <= '9') ||
                    (character >= 'A' && character <= 'F') || (character >= 'a' && character <= 'f')))
                    return false;
            return true;
        }
        internal static bool Matches(string expected, byte[] actual) =>
            IsValid(expected) && string.Equals(expected, ToHex(actual), StringComparison.OrdinalIgnoreCase);

        internal static async Task<byte[]> ComputeAsync(Stream stream, CancellationToken cancellationToken)
        {
            using (var digest = SHA256.Create())
            {
                byte[] buffer = new byte[81920];
                int count;
                while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                    digest.TransformBlock(buffer, 0, count, null, 0);
                digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return digest.Hash;
            }
        }
    }
}
