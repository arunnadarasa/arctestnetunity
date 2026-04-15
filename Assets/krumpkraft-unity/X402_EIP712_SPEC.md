# x402 EIP-712 spec (Unity client)

Unity signs the **TransferWithAuthorization** digest with the following. The adapter contract must reconstruct the **same** digest when verifying the x402 signature; otherwise it will revert with "Invalid x402 signature".

**Adapter verified:** `EVVMNativeX402Adapter.sol` (USDC Krump) already uses `address(this)` for struct `to` and domain `verifyingContract`, and the same type strings and digest formula. So the contract side is correct; any remaining mismatch is in encoding (chainId, nonce, amount, or byte layout).

**Digest verified:** Unity’s EIP-712 digest has been compared with viem (`scripts/verify-x402-digest.mjs`) and matches. If the contract still reverts, run `scripts/verify-x402-digest.mjs`; it also recovers the signer from digest + v,r,s. If recovery equals `from`, the signature is valid and the only remaining cause is the **contract computing a different digest**. **Relayer must use chainId 1315:** The adapter uses `block.chainid` in the domain. If the relayer sends the tx on another chain, the contract's digest will not match and ecrecover will fail. The x402 payload now includes **`chainId`: 1315**. The relayer **must** send `payViaEVVMWithX402` on chain **1315** (Aeneid/Krump). Before calling the contract, assert `client.chain.id === 1315` (or use the payload's `chainId` to select the correct RPC). **Krump Verify relayer** (`relayer/server.js`) is already configured for chain 1315, so no change needed there. **Relayer forwarding:** Pass `v`, `r`, `s` from the request body to `payViaEVVMWithX402` unchanged—`v` must be the number 27 or 28 (Unity sends 28 when that recovers to `from`); if the relayer coerces or defaults `v`, the contract will revert. Unity logs x402 v, r, s, and payload args to `.cursor/debug.log`.

## Domain (EIP-712)

- **Type:** `EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)`
- **name:** `"USDC Dance"`
- **version:** `"1"`
- **chainId:** `1315`
- **verifyingContract:** adapter address `0xDf5eaED856c2f8f6930d5F3A5BCE5b5d7E4C73cc`

## Struct (typed data)

- **Type:** `TransferWithAuthorization(address from,address to,uint256 amount,uint256 validAfter,uint256 validBefore,bytes32 nonce)`
- **from:** signer (payer) address
- **to:** **adapter address** `0xDf5eaED856c2f8f6930d5F3A5BCE5b5d7E4C73cc` (not the payment recipient)
- **amount:** payment amount (uint256, e.g. 10000 for 0.01 USDC 6 decimals)
- **validAfter:** 0
- **validBefore:** Unix timestamp (e.g. now + 3600)
- **nonce:** random bytes32 (0x + 64 hex)

## Digest

- `digest = keccak256("\x19\x01" || domainSeparator || structHash)`
- Client signs this digest; v, r, s are sent to the relayer with the same nonce and validAfter/validBefore.

## Adapter verification

When the adapter verifies the x402 signature it must:

1. Build **domainSeparator** with the same domain (name, version, chainId, **verifyingContract = address(this)**).
2. Build **structHash** with: from = first parameter, **to = address(this)** (adapter), amount, validAfter, validBefore, nonce from the call args.
3. Compute digest = `keccak256("\x19\x01" || domainSeparator || structHash)`.
4. Recover signer with ecrecover(digest, v, r, s) and require recovered == from.

If the adapter uses the **second parameter** (payment recipient) as the struct’s `to` when building the hash, the digest will not match what the client signed and the contract will revert with "Invalid x402 signature". The client intentionally signs with **to = adapter** so the adapter is authorized to pull USDC.

---

## Cross-check digest (if still "Invalid x402 signature")

Unity logs the x402 digest and inputs to `.cursor/debug.log` when you use "Pay via x402". Compare that digest with a reference implementation:

1. **Same inputs:** from, to = adapter `0xDf5eaED856c2f8f6930d5F3A5BCE5b5d7E4C73cc`, amount (e.g. 10000), validAfter = 0, validBefore (Unix), nonce (bytes32 hex).
2. **Domain:** `EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)` with name "USDC Dance", version "1", chainId 1315, verifyingContract = adapter.
3. **Struct:** `TransferWithAuthorization(address from,address to,uint256 amount,uint256 validAfter,uint256 validBefore,bytes32 nonce)`.
4. **Digest:** `keccak256("\x19\x01" || domainSeparator || structHash)`.

If the logged digest from Unity differs from the one computed in JS (e.g. viem) or Solidity with the same inputs, the bug is in Unity’s encoding (e.g. chainId, nonce, or uint256 byte order).

**Script:** From repo root: `cd scripts && npm install && node verify-x402-digest.mjs` (reads `../.cursor/debug.log` by default, or pass the log path as the first argument). Prints Unity vs viem digest and MATCH or MISMATCH.
