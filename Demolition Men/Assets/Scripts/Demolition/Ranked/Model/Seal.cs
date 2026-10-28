using System;
using System.Text;

namespace Demolition.Ranked
{
    /// <summary>
    /// Seals a run log with its attempt's key (docs/ranked.md §5): encrypted with an HMAC-SHA256
    /// keystream and signed with HMAC-SHA256, so a captured log can't be read, changed or sent
    /// again under another attempt. Written here rather than taken from System.Security.Cryptography
    /// so it works the same in every build, WebGL included; the tests hold it to .NET's.
    /// Not the lock: the key is in the running game. The server's replay is.
    /// </summary>
    public static class Seal
    {
        public const int TagBytes = 32;

        public static byte[] Close(byte[] plain, byte[] key)
        {
            byte[] sealedBytes = new byte[plain.Length + TagBytes];
            Xor(plain, sealedBytes, Derive(key, "enc"));
            byte[] tag = Hmac(Derive(key, "mac"), sealedBytes, plain.Length);
            Array.Copy(tag, 0, sealedBytes, plain.Length, TagBytes);
            return sealedBytes;
        }

        /// <summary>The log, or null when it wasn't sealed with this key or was changed since.</summary>
        public static byte[] Open(byte[] sealedBytes, byte[] key)
        {
            if (sealedBytes == null || key == null || sealedBytes.Length < TagBytes)
                return null;
            int length = sealedBytes.Length - TagBytes;
            byte[] tag = Hmac(Derive(key, "mac"), sealedBytes, length);
            int diff = 0; // every byte compared, whatever the first difference
            for (int i = 0; i < TagBytes; i++)
                diff |= tag[i] ^ sealedBytes[length + i];
            if (diff != 0)
                return null;
            var plain = new byte[length];
            Xor(sealedBytes, plain, Derive(key, "enc"), length);
            return plain;
        }

        private static byte[] Derive(byte[] key, string purpose) => Hmac(key, Encoding.ASCII.GetBytes(purpose));

        // Block i of the keystream is HMAC(key, i).
        private static void Xor(byte[] from, byte[] to, byte[] key, int length = -1)
        {
            if (length < 0)
                length = from.Length;
            var counter = new byte[4];
            byte[] block = null;
            for (int i = 0; i < length; i++)
            {
                if (i % 32 == 0)
                {
                    int n = i / 32;
                    counter[0] = (byte)(n >> 24);
                    counter[1] = (byte)(n >> 16);
                    counter[2] = (byte)(n >> 8);
                    counter[3] = (byte)n;
                    block = Hmac(key, counter);
                }
                to[i] = (byte)(from[i] ^ block[i % 32]);
            }
        }

        public static byte[] Hmac(byte[] key, byte[] data) => Hmac(key, data, data.Length);

        private static byte[] Hmac(byte[] key, byte[] data, int length)
        {
            if (key.Length > 64)
                key = Sha256(key);
            var inner = new byte[64 + length];
            var outer = new byte[64 + 32];
            for (int i = 0; i < 64; i++)
            {
                byte k = i < key.Length ? key[i] : (byte)0;
                inner[i] = (byte)(k ^ 0x36);
                outer[i] = (byte)(k ^ 0x5c);
            }
            Array.Copy(data, 0, inner, 64, length);
            Array.Copy(Sha256(inner), 0, outer, 64, 32);
            return Sha256(outer);
        }

        private static readonly uint[] K =
        {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
        };

        public static byte[] Sha256(byte[] data)
        {
            uint[] h = { 0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19 };
            int padded = (data.Length + 9 + 63) / 64 * 64;
            var msg = new byte[padded];
            Array.Copy(data, msg, data.Length);
            msg[data.Length] = 0x80;
            long bits = (long)data.Length * 8;
            for (int i = 0; i < 8; i++)
                msg[padded - 1 - i] = (byte)(bits >> (8 * i));
            var w = new uint[64];
            for (int chunk = 0; chunk < padded; chunk += 64)
            {
                for (int i = 0; i < 16; i++)
                {
                    int at = chunk + i * 4;
                    w[i] = (uint)msg[at] << 24 | (uint)msg[at + 1] << 16 | (uint)msg[at + 2] << 8 | msg[at + 3];
                }
                for (int i = 16; i < 64; i++)
                {
                    uint s0 = Rotr(w[i - 15], 7) ^ Rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
                    uint s1 = Rotr(w[i - 2], 17) ^ Rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
                    w[i] = w[i - 16] + s0 + w[i - 7] + s1;
                }
                uint a = h[0], b = h[1], c = h[2], d = h[3], e = h[4], f = h[5], g = h[6], hh = h[7];
                for (int i = 0; i < 64; i++)
                {
                    uint t1 = hh + (Rotr(e, 6) ^ Rotr(e, 11) ^ Rotr(e, 25)) + ((e & f) ^ (~e & g)) + K[i] + w[i];
                    uint t2 = (Rotr(a, 2) ^ Rotr(a, 13) ^ Rotr(a, 22)) + ((a & b) ^ (a & c) ^ (b & c));
                    hh = g;
                    g = f;
                    f = e;
                    e = d + t1;
                    d = c;
                    c = b;
                    b = a;
                    a = t1 + t2;
                }
                h[0] += a;
                h[1] += b;
                h[2] += c;
                h[3] += d;
                h[4] += e;
                h[5] += f;
                h[6] += g;
                h[7] += hh;
            }
            var digest = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                digest[i * 4] = (byte)(h[i] >> 24);
                digest[i * 4 + 1] = (byte)(h[i] >> 16);
                digest[i * 4 + 2] = (byte)(h[i] >> 8);
                digest[i * 4 + 3] = (byte)h[i];
            }
            return digest;
        }

        private static uint Rotr(uint x, int n) => (x >> n) | (x << (32 - n));
    }
}
