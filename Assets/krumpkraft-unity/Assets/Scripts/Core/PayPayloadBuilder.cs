using System;
using System.Text;
using UnityEngine;

namespace KrumpKraft
{
    public static class PayPayloadBuilder
    {
        public const int EvvmId = 1163;

        public static string BuildSignaturePayload(
            string toAddress,
            string toIdentity,
            string token,
            string amountWei,
            string priorityFeeWei,
            string senderExecutor,
            string nonce,
            bool isAsyncExec)
        {
            // Match web (VerifyForm): keccak256(abi.encode('pay', to, '', token, amount, 0))
            var hashPayload = HashDataForPayAbi(toAddress, token, amountWei);
            var core = ContractAddresses.Core.ToLower();
            var executor = string.IsNullOrEmpty(senderExecutor) ? ContractAddresses.ZeroAddress : senderExecutor.ToLower();
            var message = $"{EvvmId},{core},{hashPayload},{executor},{nonce},{isAsyncExec.ToString().ToLower()}";
            return message;
        }

        /// <summary>
        /// EVVM hash payload matching web (VerifyForm): keccak256(encodeAbiParameters([string, address, string, address, uint256, uint256], ['pay', to, '', token, amount, 0n])).
        /// </summary>
        public static string HashDataForPayAbi(string toAddress, string token, string amountWei)
        {
            try
            {
                var amountBigInt = System.Numerics.BigInteger.Parse(amountWei);
                var encoded = EncodeAbiPayTuple(toAddress, token, amountBigInt);
                var hash = Keccak256(encoded);
                return "0x" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PayPayloadBuilder] HashDataForPayAbi failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// ABI-encode ('pay', toAddress, '', token, amount, 0) for EVVM Core hash. Layout: [off1, addr_to, off2, addr_token, amount, 0] then dynamic: len+"pay" padded, len("").
        /// </summary>
        private static byte[] EncodeAbiPayTuple(string toAddress, string token, System.Numerics.BigInteger amount)
        {
            const int headWords = 6;
            const int headLen = headWords * 32;
            int offPay = headLen;
            int payDataLen = 32 + 32;
            int offEmpty = offPay + payDataLen;
            var list = new System.Collections.Generic.List<byte>();
            list.AddRange(BigIntegerToBytes32(offPay));
            list.AddRange(AddressToBytes32RightAligned(toAddress));
            list.AddRange(BigIntegerToBytes32(offEmpty));
            list.AddRange(AddressToBytes32RightAligned(token));
            list.AddRange(BigIntegerToBytes32(amount));
            list.AddRange(BigIntegerToBytes32(System.Numerics.BigInteger.Zero));
            var payBytes = Encoding.UTF8.GetBytes("pay");
            list.AddRange(BigIntegerToBytes32(payBytes.Length));
            list.AddRange(payBytes);
            for (int i = payBytes.Length; i < 32; i++) list.Add(0);
            list.AddRange(BigIntegerToBytes32(0));
            return list.ToArray();
        }

        public static string HashDataForPay(string toAddress, string toIdentity, string token, string amountWei, string priorityFeeWei)
        {
            try
            {
                return HashDataForPayAbi(toAddress, token, amountWei);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PayPayloadBuilder] HashDataForPay failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        
        private static int PaddedLength(int dataLength)
        {
            return ((dataLength + 31) / 32) * 32;
        }
        
        private static byte[] AddressToBytes32RightAligned(string address)
        {
            var addressHex = address.ToLower();
            if (addressHex.StartsWith("0x"))
                addressHex = addressHex.Substring(2);
                
            if (addressHex.Length != 40)
                throw new ArgumentException($"Invalid address length: {addressHex.Length}");
            
            var result = new byte[32];
            var addressBytes = HexStringToByteArray(addressHex);
            
            // Address is right-aligned (last 20 bytes, first 12 bytes are zero)
            Buffer.BlockCopy(addressBytes, 0, result, 12, 20);
            
            return result;
        }
        
        private static byte[] AddressToBytes32LeftPadded(string address)
        {
            var addressHex = address.ToLower();
            if (addressHex.StartsWith("0x"))
                addressHex = addressHex.Substring(2);
                
            if (addressHex.Length != 40)
                throw new ArgumentException($"Invalid address length: {addressHex.Length}");
            
            var result = new byte[32];
            var addressBytes = HexStringToByteArray(addressHex);
            
            // Address in first 20 bytes, zeros in last 12 bytes
            Buffer.BlockCopy(addressBytes, 0, result, 0, 20);
            
            return result;
        }
        
        private static byte[] AddressToBytes20(string address)
        {
            var addressHex = address.ToLower();
            if (addressHex.StartsWith("0x"))
                addressHex = addressHex.Substring(2);
                
            if (addressHex.Length != 40)
                throw new ArgumentException($"Invalid address length: {addressHex.Length}");
            
            return HexStringToByteArray(addressHex);
        }
        
        private static byte[] EncodeString(string str)
        {
            var strBytes = Encoding.UTF8.GetBytes(str);
            var result = new System.Collections.Generic.List<byte>();
            
            // Length (32 bytes)
            result.AddRange(Uint256ToBytes(strBytes.Length));
            
            // Data (padded to multiple of 32 bytes)
            result.AddRange(strBytes);
            var padding = (32 - (strBytes.Length % 32)) % 32;
            result.AddRange(new byte[padding]);
            
            return result.ToArray();
        }
        
        private static byte[] Uint256ToBytes(int value)
        {
            return BigIntegerToBytes32(new System.Numerics.BigInteger(value));
        }
        
        private static byte[] HexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have even length");
            
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        
        private static byte[] BigIntegerToBytes32(System.Numerics.BigInteger value)
        {
            var bytes = value.ToByteArray();
            var result = new byte[32];
            
            if (bytes.Length > 32)
                throw new ArgumentException("BigInteger too large for bytes32");
            
            if (value.Sign >= 0)
            {
                Array.Reverse(bytes);
                var destIndex = 32 - bytes.Length;
                Buffer.BlockCopy(bytes, 0, result, destIndex, bytes.Length);
            }
            else
            {
                for (int i = 0; i < 32; i++)
                    result[i] = 0xFF;
                Array.Reverse(bytes);
                var destIndex = 32 - bytes.Length;
                Buffer.BlockCopy(bytes, 0, result, destIndex, bytes.Length);
            }
            
            return result;
        }

        public static string Eip191Message(string payload)
        {
            var msg = Encoding.UTF8.GetBytes(payload);
            var prefix = "\x19Ethereum Signed Message:\n" + msg.Length;
            var prefixBytes = Encoding.UTF8.GetBytes(prefix);
            var combined = new byte[prefixBytes.Length + msg.Length];
            Buffer.BlockCopy(prefixBytes, 0, combined, 0, prefixBytes.Length);
            Buffer.BlockCopy(msg, 0, combined, prefixBytes.Length, msg.Length);
            return "0x" + BitConverter.ToString(Keccak256(combined)).Replace("-", "").ToLowerInvariant();
        }

        private static byte[] Keccak256(byte[] data)
        {
            return Keccak256Hash.ComputeHash(data);
        }

        private static string Keccak256Hex(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = Keccak256(bytes);
            return "0x" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    public static class Keccak256Hash
    {
        public static byte[] ComputeHash(byte[] data)
        {
            if (data == null) return new byte[32];
            return Keccak256Impl.ComputeHash(data);
        }
    }

    internal static class Keccak256Impl
    {
        public static byte[] ComputeHash(byte[] data)
        {
            var sponge = new byte[200];
            var rate = 136;
            var pos = 0;
            while (pos < data.Length)
            {
                var block = Math.Min(rate, data.Length - pos);
                XorInto(sponge, 0, data, pos, block);
                pos += block;
                if (block == rate)
                    KeccakF1600(sponge);
            }
            // Pad10*1: first 1 at first byte after absorbed data in rate, last 0x80 at end of rate
            sponge[pos % rate] ^= 0x01;
            sponge[rate - 1] ^= 0x80;
            KeccakF1600(sponge);
            var result = new byte[32];
            Buffer.BlockCopy(sponge, 0, result, 0, 32);
            return result;
        }

        private static void XorInto(byte[] dst, int dstOff, byte[] src, int srcOff, int len)
        {
            for (var i = 0; i < len; i++)
                dst[dstOff + i] ^= src[srcOff + i];
        }

        private static void KeccakF1600(byte[] state)
        {
            ulong[] A = new ulong[25];
            for (int i = 0; i < 25; i++)
                A[i] = BitConverter.ToUInt64(state, i * 8);
            KeccakF1600Round(A);
            for (int i = 0; i < 25; i++)
            {
                var bytes = BitConverter.GetBytes(A[i]);
                Buffer.BlockCopy(bytes, 0, state, i * 8, 8);
            }
        }

        private static void KeccakF1600Round(ulong[] A)
        {
            ulong C0, C1, C2, C3, C4, D0, D1, D2, D3, D4;
            for (int r = 0; r < 24; r++)
            {
                C0 = A[0] ^ A[5] ^ A[10] ^ A[15] ^ A[20];
                C1 = A[1] ^ A[6] ^ A[11] ^ A[16] ^ A[21];
                C2 = A[2] ^ A[7] ^ A[12] ^ A[17] ^ A[22];
                C3 = A[3] ^ A[8] ^ A[13] ^ A[18] ^ A[23];
                C4 = A[4] ^ A[9] ^ A[14] ^ A[19] ^ A[24];
                D0 = C4 ^ RotL64(C1, 1);
                D1 = C0 ^ RotL64(C2, 1);
                D2 = C1 ^ RotL64(C3, 1);
                D3 = C2 ^ RotL64(C4, 1);
                D4 = C3 ^ RotL64(C0, 1);
                for (int i = 0; i < 25; i += 5)
                    A[i] ^= D0;
                for (int i = 1; i < 25; i += 5)
                    A[i] ^= D1;
                for (int i = 2; i < 25; i += 5)
                    A[i] ^= D2;
                for (int i = 3; i < 25; i += 5)
                    A[i] ^= D3;
                for (int i = 4; i < 25; i += 5)
                    A[i] ^= D4;
                ulong B0 = A[0], B1 = RotL64(A[6], 44), B2 = RotL64(A[12], 43), B3 = RotL64(A[18], 21), B4 = RotL64(A[24], 14);
                ulong B5 = RotL64(A[3], 28), B6 = RotL64(A[9], 20), B7 = RotL64(A[10], 3), B8 = RotL64(A[16], 45), B9 = RotL64(A[22], 61);
                ulong B10 = RotL64(A[1], 1), B11 = RotL64(A[7], 6), B12 = RotL64(A[13], 25), B13 = RotL64(A[19], 8), B14 = RotL64(A[20], 18);
                ulong B15 = RotL64(A[4], 27), B16 = RotL64(A[5], 36), B17 = RotL64(A[11], 10), B18 = RotL64(A[17], 15), B19 = RotL64(A[23], 56);
                ulong B20 = RotL64(A[2], 62), B21 = RotL64(A[8], 55), B22 = RotL64(A[14], 39), B23 = RotL64(A[15], 41), B24 = RotL64(A[21], 2);
                A[0] = B0 ^ ((~B1) & B2);
                A[1] = B1 ^ ((~B2) & B3);
                A[2] = B2 ^ ((~B3) & B4);
                A[3] = B3 ^ ((~B4) & B0);
                A[4] = B4 ^ ((~B0) & B1);
                A[5] = B5 ^ ((~B6) & B7);
                A[6] = B6 ^ ((~B7) & B8);
                A[7] = B7 ^ ((~B8) & B9);
                A[8] = B8 ^ ((~B9) & B5);
                A[9] = B9 ^ ((~B5) & B6);
                A[10] = B10 ^ ((~B11) & B12);
                A[11] = B11 ^ ((~B12) & B13);
                A[12] = B12 ^ ((~B13) & B14);
                A[13] = B13 ^ ((~B14) & B10);
                A[14] = B14 ^ ((~B10) & B11);
                A[15] = B15 ^ ((~B16) & B17);
                A[16] = B16 ^ ((~B17) & B18);
                A[17] = B17 ^ ((~B18) & B19);
                A[18] = B18 ^ ((~B19) & B15);
                A[19] = B19 ^ ((~B15) & B16);
                A[20] = B20 ^ ((~B21) & B22);
                A[21] = B21 ^ ((~B22) & B23);
                A[22] = B22 ^ ((~B23) & B24);
                A[23] = B23 ^ ((~B24) & B20);
                A[24] = B24 ^ ((~B20) & B21);
                A[0] ^= RC[r];
            }
        }

        private static ulong RotL64(ulong x, int n) => (x << n) | (x >> (64 - n));

        private static readonly ulong[] RC = {
            0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808aUL, 0x8000000080008000UL,
            0x000000000000808bUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
            0x000000000000008aUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000aUL,
            0x000000008000808bUL, 0x800000000000008bUL, 0x8000000000008089UL, 0x8000000000008003UL,
            0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800aUL, 0x800000008000000aUL,
            0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL
        };
    }
}
