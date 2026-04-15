using System;
using System.Numerics;
using System.Text;

namespace KrumpKraft
{
    /// <summary>
    /// EIP-712 typed data for x402 TransferWithAuthorization (USDC Health).
    /// Domain: name "USDC Health", version "1", chainId 5042002, verifyingContract = EvvmX402Adapter.
    /// The signed message must have "to" = adapter (authorize adapter to pull USDCh). The payment recipient
    /// is only passed to the relayer/contract as the second arg to payViaEVVMWithX402; it is not part of the signed message.
    /// </summary>
    public static class X402Eip712
    {
        public const int ChainId = 5042002;
        public const string DomainName = "USDC Health";
        public const string DomainVersion = "1";

        /// <summary>
        /// Computes the EIP-712 digest for TransferWithAuthorization to sign.
        /// Message: from (signer), to = adapter only (not payment recipient), amount, validAfter (0), validBefore, nonce (bytes32).
        /// verifyingContract in domain must also be the adapter. Use ContractAddresses.EvvmX402Adapter for both.
        /// </summary>
        public static byte[] DigestTransferWithAuthorization(
            string fromAddress,
            string toAddressAdapter,
            string amountWei,
            ulong validAfter,
            ulong validBefore,
            string nonceBytes32Hex)
        {
            var domainSeparator = DomainSeparator(toAddressAdapter);
            var structHash = HashStructTransferWithAuthorization(fromAddress, toAddressAdapter, amountWei, validAfter, validBefore, nonceBytes32Hex);
            var data = new byte[2 + 32 + 32];
            data[0] = 0x19;
            data[1] = 0x01;
            Buffer.BlockCopy(domainSeparator, 0, data, 2, 32);
            Buffer.BlockCopy(structHash, 0, data, 34, 32);
            return Keccak256Hash.ComputeHash(data);
        }

        private static byte[] DomainSeparator(string verifyingContract)
        {
            var typeHash = Keccak256(Encoding.UTF8.GetBytes("EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)"));
            var encName = Keccak256(Encoding.UTF8.GetBytes(DomainName));
            var encVersion = Keccak256(Encoding.UTF8.GetBytes(DomainVersion));
            var encChainId = Uint256ToBytes(ChainId);
            var encVerifying = AddressToBytes32(verifyingContract);
            var encoded = new byte[32 + 32 + 32 + 32 + 32];
            Buffer.BlockCopy(typeHash, 0, encoded, 0, 32);
            Buffer.BlockCopy(encName, 0, encoded, 32, 32);
            Buffer.BlockCopy(encVersion, 0, encoded, 64, 32);
            Buffer.BlockCopy(encChainId, 0, encoded, 96, 32);
            Buffer.BlockCopy(encVerifying, 0, encoded, 128, 32);
            return Keccak256Hash.ComputeHash(encoded);
        }

        private static byte[] HashStructTransferWithAuthorization(
            string fromAddress,
            string toAddress,
            string amountWei,
            ulong validAfter,
            ulong validBefore,
            string nonceBytes32Hex)
        {
            var typeHash = Keccak256(Encoding.UTF8.GetBytes("TransferWithAuthorization(address from,address to,uint256 amount,uint256 validAfter,uint256 validBefore,bytes32 nonce)"));
            var amount = BigInteger.Parse(amountWei);
            var encoded = new byte[32 * 7];
            Buffer.BlockCopy(typeHash, 0, encoded, 0, 32);
            Buffer.BlockCopy(AddressToBytes32(fromAddress), 0, encoded, 32, 32);
            Buffer.BlockCopy(AddressToBytes32(toAddress), 0, encoded, 64, 32);
            Buffer.BlockCopy(Uint256ToBytes(amount), 0, encoded, 96, 32);
            Buffer.BlockCopy(Uint256ToBytes(validAfter), 0, encoded, 128, 32);
            Buffer.BlockCopy(Uint256ToBytes(validBefore), 0, encoded, 160, 32);
            Buffer.BlockCopy(HexToBytes32(nonceBytes32Hex), 0, encoded, 192, 32);
            return Keccak256Hash.ComputeHash(encoded);
        }

        private static byte[] Keccak256(byte[] data) => Keccak256Hash.ComputeHash(data);

        private static byte[] AddressToBytes32(string address)
        {
            var raw = (address ?? "").Trim();
            var hex = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw.Substring(2) : raw;
            hex = hex.ToLowerInvariant();
            if (hex.Length != 40) throw new ArgumentException("Invalid address length: " + (hex.Length));
            var bytes = new byte[32];
            for (var i = 0; i < 20; i++)
                bytes[12 + i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        private static byte[] Uint256ToBytes(BigInteger value)
        {
            var bytes = value.ToByteArray();
            if (bytes.Length > 0 && bytes[bytes.Length - 1] == 0 && value.Sign >= 0)
                Array.Resize(ref bytes, bytes.Length - 1);
            if (bytes.Length > 32)
                throw new ArgumentException("Value too large for uint256");
            var result = new byte[32];
            for (var i = 0; i < bytes.Length; i++)
                result[32 - bytes.Length + i] = bytes[bytes.Length - 1 - i];
            return result;
        }

        private static byte[] Uint256ToBytes(ulong value)
        {
            var result = new byte[32];
            for (var i = 0; i < 8; i++)
                result[31 - i] = (byte)((value >> (i * 8)) & 0xff);
            return result;
        }

        private static byte[] HexToBytes32(string hex)
        {
            if (hex == null || hex.Length != 66)
                throw new ArgumentException("nonce must be 0x + 64 hex chars");
            var h = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex.Substring(2).ToLowerInvariant() : hex.ToLowerInvariant();
            if (h.Length != 64) throw new ArgumentException("nonce must be 64 hex chars");
            var result = new byte[32];
            for (var i = 0; i < 32; i++)
                result[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            return result;
        }
    }
}
