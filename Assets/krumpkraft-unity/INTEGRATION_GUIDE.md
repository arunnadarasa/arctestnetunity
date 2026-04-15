# EVVM Story + x402 + USDC Krump + IP — Unity Integration Guide

This guide explains how to create the exact files and scripts needed to integrate **EVVM Story** (KrumpChain 1140), **x402**, **USDC Krump**, and **$IP** into your Unity game so other developers can add the same functionality.

---

## 1. Prerequisites & Versions

### Unity
- **Unity 2022.3 LTS** or **Unity 6** (tested on 6000.0.68f1).
- Build target: Windows, Mac, Linux, or your platform.

### Required Unity packages (Package Manager)
Add these in **Window → Package Manager** (or via `Packages/manifest.json`):

| Package | Source | Purpose |
|--------|--------|---------|
| **TextMeshPro** | Built-in (Unity Registry) | All UI text (balances, buttons, status). |
| **com.nethereum.unity** | Git URL (see below) | EIP-191 signing, Web3 RPC, and (with Nethereum.Signer) x402 EIP-712 recovery. |

**Nethereum.Unity** (add to `manifest.json` under `dependencies`):

```json
"com.nethereum.unity": "https://github.com/Nethereum/Nethereum.Unity.git"
```

Optional:
- **MetaMask Embedded Wallets (Web3Auth) Unity** — only if you want embedded wallets (no extension). Get the package from [MetaMask Embedded Wallets SDK for Unity](https://docs.metamask.io/embedded-wallets/sdk/unity).

### Network (Story Aeneid Testnet)
- **Chain ID:** 1315  
- **RPC:** `https://aeneid.storyrpc.io`  
- **Block explorer:** `https://aeneid.storyscan.io`  

EVVM **1140** (KrumpChain) runs on this host chain; all contract calls and x402 use chain **1315**.

---

## 2. Folder Structure to Create

Create this under your Unity project’s `Assets` (e.g. `Assets/KrumpKraft/` or copy the whole `krumpkraft-unity` folder):

```
Assets/
  YourKrumpFolder/
    Assets/
      Scripts/
        Core/           # Contracts, config, EIP-712, hashing
        Services/       # Wallet, RPC, transactions, tokens
        UI/             # WalletConnect, TokenBalance, PaymentTest
      Scenes/
        KrumpKraftPaymentTest.unity   # Optional test scene
      Resources/        # JSON configs (loaded at runtime)
    INTEGRATION_GUIDE.md
    README.md
    X402_EIP712_SPEC.md
```

All paths below are relative to `YourKrumpFolder/Assets/` (e.g. `Scripts/Core/ContractAddresses.cs`).

---

## 3. Configuration Files (Resources)

Place these in `Assets/Resources/` so `ConfigLoader` can load them by name (no extension).

### 3.1 `ChainSettings.json`

```json
{
  "chainName": "KrumpChain",
  "chainId": 1140,
  "rpcUrl": "https://aeneid.storyrpc.io",
  "blockExplorer": "https://aeneid.storyscan.io",
  "nativeCurrencySymbol": "JAB",
  "chainType": 1,
  "hostChainId": 1315,
  "hostRpcUrl": "https://aeneid.storyrpc.io",
  "hostBlockExplorer": "https://aeneid.storyscan.io",
  "krumpKraftApiBaseUrl": "http://localhost:3099"
}
```

- **hostChainId** must be **1315** for x402 (adapter uses `block.chainid`).
- **krumpKraftApiBaseUrl**: optional Bezi/KrumpKraft API for JAB balance; can stay `http://localhost:3099` or point to your API.

### 3.2 `MoralisConfig.json` (optional)

Used only if you want to use Moralis REST APIs (e.g. for token balances or future features). Can be minimal:

```json
{
  "apiKey": "",
  "baseUrl": "https://deep-index.moralis.io/api",
  "version": "v2"
}
```

Leave `apiKey` empty if you rely only on RPC balance (JAB/USDC/$IP via `RpcBalanceService`). See **§11. Moralis** below for when and how to use it.

### 3.3 `WalletConfig.json` (optional; add to .gitignore)

For **Private Key** or **Web3Auth** wallet mode:

```json
{
  "usePrivateKey": false,
  "privateKey": "",
  "address": "",
  "web3AuthClientId": "",
  "web3AuthRedirectUrl": ""
}
```

- **Private key (dev only):** set `usePrivateKey: true`, `privateKey: "0x..."`. Never commit this file with a real key.  
- **Web3Auth:** set `web3AuthClientId` from the Embedded Wallets dashboard.

---

## 4. Core Scripts

### 4.1 `Scripts/Core/ContractAddresses.cs`

Defines EVVM 1140 contract addresses on Story Aeneid (1315). Must match deployed contracts.

```csharp
namespace KrumpKraft
{
    public static class ContractAddresses
    {
        public const string Core = "0xa6a02e8e17b819328ddb16a0ad31dd83dd14ba3b";
        public const string NameService = "0x2136Bb44415B6227Ab46f9080739b8A54aE56B6B";
        public const string Staking = "0x8F89cBe21d5d03cF24F0C2b084E377bdbbce72CD";
        public const string Estimator = "0x33569b317a2901f11D84D2067E060014f8C6208c";
        public const string Treasury = "0x977126dd6B03cAa3A87532784E6B7757aBc9C1cc";
        public const string P2PSwap = "0xdb1934b7d039FAb96D0F041053E4056ce4954AD2";

        public const string JabPrincipal = "0x0000000000000000000000000000000000000001";
        public const string USDCKrump = "0xd35890acdf3BFFd445C2c7fC57231bDE5cAFbde5";

        /// <summary>EVVM x402 adapter (verifyingContract and message.to for EIP-712 TransferWithAuthorization).</summary>
        public const string EvvmX402Adapter = "0xDf5eaED856c2f8f6930d5F3A5BCE5b5d7E4C73cc";

        public const string ZeroAddress = "0x0000000000000000000000000000000000000000";
    }
}
```

- **EvvmX402Adapter** is required for x402 (Pay via x402 with USDC Krump). Domain and struct hashes in your EIP-712 code must use this as `verifyingContract` and struct `to`.

### 4.2 `Scripts/Core/ChainSettings.cs`

Serializable settings loaded from `ChainSettings.json`. Must include `hostChainId`, `hostRpcUrl`, `hostBlockExplorer` for x402 and RPC.

(Use the existing `ChainSettings.cs` from the repo: chain name, chainId, rpcUrl, blockExplorer, nativeCurrencySymbol, chainType, hostChainId, hostRpcUrl, hostBlockExplorer, krumpKraftApiBaseUrl, and the property accessors.)

### 4.3 `Scripts/Core/ConfigLoader.cs`

Generic loader that reads JSON from `Resources/{filenameWithoutExtension}` and deserializes with `JsonUtility`. Used for `ChainSettings`, `MoralisConfig`, `WalletConfig`.

### 4.4 `Scripts/Core/WalletConfig.cs` & `MoralisConfig.cs`

Serializable classes for `WalletConfig.json` and `MoralisConfig.json` (fields and helpers as in the repo).

### 4.5 `Scripts/Core/IWalletProvider.cs`

Interface for wallet abstraction:

- `Connect(Action<bool, string> onComplete)`  
- `Disconnect()`  
- `SignMessage(string message, Action<bool, string> onComplete)`  
- `SignHash(byte[] hash, Action<bool, string> onComplete)` — required for x402 (EIP-712 digest).  
- `string ConnectedAddress`, `bool IsConnected`, optional `string GetPrivateKey()` for TransactionService.

### 4.6 `Scripts/Core/X402Eip712.cs`

EIP-712 for **TransferWithAuthorization** (USDC Dance):

- **Domain:** name `"USDC Dance"`, version `"1"`, **chainId 1315**, **verifyingContract = EvvmX402Adapter**.
- **Struct:** `TransferWithAuthorization(address from, address to, uint256 amount, uint256 validAfter, uint256 validBefore, bytes32 nonce)` with **to = EvvmX402Adapter** (not the payment recipient).
- **Digest:** `keccak256("\x19\x01" || domainSeparator || structHash)`; client signs this digest.

Implement `DigestTransferWithAuthorization(from, toAdapter, amountWei, validAfter, validBefore, nonceBytes32Hex)` and use a **Keccak256** implementation (e.g. in `PayPayloadBuilder.cs` the project uses a built-in Keccak256Impl; no extra package).

See **X402_EIP712_SPEC.md** in the repo for the exact type strings and relayer checklist.

### 4.7 `Scripts/Core/PayPayloadBuilder.cs`

- **EVVM pay payload:** Builds the message string for Core.pay (EVVM ID 1140, core contract, hash payload, executor, nonce, isAsyncExec) and its **keccak256** for EIP-191 signing.  
- **Keccak256:** Either use the same in-repo Keccak256 implementation or Nethereum’s `Sha3Keccack` if available.

Required for: JAB pay, and for x402 the relayer payload (nonce, validAfter, validBefore, v, r, s, recipient, amount, etc.) is built in the UI layer using this and `X402Eip712`.

---

## 5. Services

### 5.1 `Scripts/Services/WalletService.cs`

Singleton-like service that holds an `IWalletProvider`. Exposes Connect/Disconnect/SignMessage/SignHash and events `OnConnected` / `OnDisconnected`. Used by UI and `TransactionService`.

### 5.2 `Scripts/Services/SimpleWalletProvider.cs` & `PrivateKeyWalletProvider.cs` & `Web3AuthWalletProvider.cs`

- **SimpleWalletProvider:** Stub for testing (fixed address, fake sign).  
- **PrivateKeyWalletProvider:** Reads `WalletConfig`, uses Nethereum for signing (EIP-191 and raw hash for x402).  
- **Web3AuthWalletProvider:** Uses Web3Auth/MetaMask Embedded Wallets; requires Nethereum for SignHash (x402). Must use **v = 27 or 28** so that `ecrecover(digest, v, r, s)` equals the signer (see X402_EIP712_SPEC and relayer notes).

### 5.3 `Scripts/Services/TransactionService.cs`

- Subscribes to wallet connect/disconnect; builds Web3/Account via Nethereum using the provider’s private key.  
- **JAB:** `SendJABToCore(toAddress, token, amountWei, priorityFeeWei, senderExecutor, nonce, isAsyncExec, signature, onComplete)`.  
- **USDC Krump:** `SendERC20Transfer(USDCKrump, recipient, amountWei, onComplete)`.  
- **$IP (native):** send native currency on chain 1315 (e.g. `eth_sendTransaction` with value).  
- **Approve / Deposit:** `ApproveErc20`, `DepositToTreasury` for USDC Krump when using EVVM deposit flow.  
- Optional: method that calls the x402 adapter (e.g. `payViaEVVMWithX402`) if you execute from Unity; usually the **relayer** does that.

Attach `TransactionService` to a GameObject in the scene; it gets `KrumpKraftManager.Instance` and `WalletService` in `Start`.

### 5.4 `Scripts/Services/RpcBalanceService.cs`

- **GetJABBalance(wallet, callback):** `eth_call` to Core `getBalance(user, JabPrincipal)`.  
- **GetUSDCBalance(wallet, callback):** `eth_call` to USDC Krump `balanceOf(wallet)`.  
- **GetNativeBalance(wallet, callback):** `eth_getBalance` (for **$IP** on Story = native token).  

Uses `ChainSettings.hostRpcUrl` and optional `krumpKraftApiBaseUrl` (for Bezi API). Requires `TokenService` for decimals and formatting.

### 5.5 `Scripts/Services/TokenService.cs`

Constants (e.g. JAB 18 decimals, USDC Krump 6 decimals) and balance formatting. Used by `RpcBalanceService` and UI.

### 5.6 `Scripts/Services/X402PaymentService.cs` / `KrumpVerifier.cs`

Optional verification/relay helpers; at minimum the **relayer** (e.g. Krump Verify `relayer/server.js`) must use **chainId 1315** and forward v,r,s correctly (see X402_EIP712_SPEC).

---

## 6. UI Scripts

### 6.1 `Scripts/KrumpKraftManager.cs`

- Singleton; in `Awake` loads `ChainSettings`, `MoralisConfig`, `WalletConfig`, creates `WalletService`, sets provider (SimpleStub / PrivateKeyConfig / Web3AuthEmbeddedWallet / Custom), creates `MoralisApiClient`, `TokenService`, `RpcBalanceService`.  
- Exposes: `ChainSettings`, `WalletService`, `TokenService`, `RpcBalanceService`, etc.  
- Attach to a single GameObject (e.g. “KrumpKraftManager”) in the first scene that uses EVVM/x402.

### 6.2 `Scripts/UI/WalletConnectUI.cs`

Wires Connect/Disconnect buttons and address label to `WalletService`. Subscribes to `OnConnected` / `OnDisconnected`.

### 6.3 `Scripts/UI/TokenBalanceUI.cs`

- **TokenType** enum: `Jab`, `USDCKrump`, `IP`.  
- One panel per token: JAB, USDC Krump, $IP.  
- On Refresh: calls `RpcBalanceService.GetJABBalance`, `GetUSDCBalance`, or `GetNativeBalance` and sets a TMP_Text label.  
- Requires `KrumpKraftManager` and `RpcBalanceService`/`WalletService` in scene.

### 6.4 `Scripts/UI/PaymentTestUI.cs` (or your own payment UI)

- Recipient input, amount input, token dropdown (JAB, USDC Krump, $IP).  
- **Pay:**  
  - **JAB:** Build pay payload (PayPayloadBuilder), sign with wallet (EIP-191), send via `TransactionService.SendJABToCore`.  
  - **USDC Krump (direct):** `TransactionService.SendERC20Transfer(USDCKrump, recipient, amountWei, ...)`.  
  - **USDC Krump (x402):** Build EIP-712 digest with `X402Eip712.DigestTransferWithAuthorization(from, EvvmX402Adapter, amountWei, validAfter, validBefore, nonce)`, sign digest with `SignHash`, build relayer payload (v, r, s, recipient, amount, nonce, validAfter, validBefore, …), POST to relayer.  
  - **$IP:** `TransactionService` send native value to recipient.  
- **Sign Pay** / **Copy Payload** / **Send to Relay** as in the reference scene.  
- Optional: Approve/Deposit buttons for USDC Krump when using EVVM treasury flow.  
- Transaction link: open block explorer (use `ChainSettings.HostBlockExplorer`) for the tx hash returned by relay/transaction service.

Use **ChainSettings.HostBlockExplorer** and **hostChainId 1315** for all explorer links and relayer payloads.

---

## 7. Scene Setup (Minimal)

1. **Canvas** (Screen Space – Overlay or your choice).  
2. **KrumpKraftManager** GameObject with `KrumpKraftManager` script; set Wallet Provider Mode (Simple Stub / Private Key / Web3Auth / Custom).  
3. **TransactionService** on a GameObject (same or child) so it can subscribe to `WalletService`.  
4. **Wallet panel:** Connect / Disconnect buttons, address label; assign to **WalletConnectUI**.  
5. **Balance panels:** Three rows (e.g. Vertical Layout Group):  
    - JAB row: Text + Refresh button; **TokenBalanceUI** with `TokenType.Jab`.  
    - USDC Krump row: Text + Refresh; **TokenBalanceUI** with `TokenType.USDCKrump`.  
    - $IP row: Text + Refresh; **TokenBalanceUI** with `TokenType.IP`.  
6. **Payment panel:**  
    - Recipient (TMP_InputField), Amount (TMP_InputField), Token dropdown (JAB / USDC Krump / $IP).  
    - Buttons: Pay (or Sign Pay), Copy Payload, Send to Relay (for x402).  
    - Status text, optional transaction link (TMP_Text, open explorer on click).  
7. Assign all references in **PaymentTestUI** (or your script): recipientInput, amountInput, tokenDropdown, signPayButton, copyPayloadButton, sendToRelayButton, statusText, transactionLinkText, relayUrl if needed.

Layout: Use RectTransforms and optionally Layout Elements (min/preferred height) so the $IP row doesn’t overlap USDC Krump; keep transaction link on the right so it doesn’t cover the amount field (see reference scene).

---

## 8. x402 (Pay via x402) — Checklist

- **Chain ID:** All EIP-712 and relayer calls must use **1315** (Story Aeneid).  
- **EIP-712:** Domain and struct as in **X402_EIP712_SPEC.md**; `to` and `verifyingContract` = **EvvmX402Adapter**.  
- **Signing:** Client signs the **digest** (32 bytes); wallet returns v, r, s (v must be 27 or 28; if relayer gets “Invalid x402 signature”, try the other v).  
- **Relayer:** Must send the transaction on chain **1315** and pass v,r,s unchanged to the adapter’s `payViaEVVMWithX402`.  
- **Contract:** Adapter must build the same domain and struct and recover signer with `ecrecover(digest, v, r, s)`.

---

## 9. Tokens Summary

| Token   | Type     | Address / source              | Decimals | Usage                          |
|--------|----------|-------------------------------|----------|---------------------------------|
| JAB    | EVVM     | JabPrincipal (0x...01)        | 18       | Core.getBalance, Core.pay       |
| USDC Krump | ERC-20 | ContractAddresses.USDCKrump   | 6        | balanceOf, transfer, x402       |
| $IP    | Native   | Chain 1315 native             | 18       | eth_getBalance, eth_sendTransaction |

---

## 10. Dependencies Summary

| Dependency        | Version / source              | Required for                          |
|-------------------|--------------------------------|----------------------------------------|
| Unity             | 2022.3+ / 6.x                 | Project                                |
| TextMeshPro       | From Unity Registry           | All UI text                            |
| Nethereum.Unity   | Git: Nethereum.Unity          | Signing, Web3, RPC, x402 recovery      |
| Web3Auth (Unity)  | From MetaMask/Web3Auth        | Optional; embedded wallets only        |
| Moralis           | External API (no Unity package) | Optional; token balances via REST. Balance UI uses RPC by default. |

---

## 11. Moralis (Optional)

**Moralis** is a third-party API for blockchain data (token balances, NFTs, etc.). In this integration it is **optional**.

### When is Moralis used?

- **KrumpKraftManager** always creates a **MoralisApiClient** and **TokenService** (TokenService takes Moralis in its constructor). So `MoralisConfig.json` is loaded if present; if missing, a default empty config is used.
- **Balance display in the reference app** does **not** use Moralis. JAB, USDC Krump, and $IP balances are fetched via **RpcBalanceService** (direct `eth_call` / `eth_getBalance` on Story Aeneid RPC). So you do **not** need a Moralis API key for the built-in balance panels to work.
- **TokenService** exposes `GetJabBalance`, `GetUSDCKrumpBalance`, and `GetBalance(wallet, tokenAddress, decimals, callback)` which call **Moralis** `GET /v2/{address}/erc20?chain=...`. You can use these from your own scripts if you prefer Moralis over RPC (e.g. for multiple tokens in one request or for chains Moralis supports).

### When you might want Moralis

- You want to show **many ERC‑20 tokens** in one request instead of one RPC call per token.
- You use other Moralis features (NFTs, history, etc.) elsewhere in your game.
- You prefer their rate limits / caching over direct RPC.

### Setup (if you use it)

1. **Get an API key:** Sign up at [moralis.io](https://moralis.io), create a project, and copy the **Web3 API Key** from the dashboard.
2. **Put it in config:** In `Resources/MoralisConfig.json` set `"apiKey": "YOUR_KEY"`. Do **not** commit real keys; use a template or env-based config in production.
3. **Chain ID:** Moralis API uses `chain` in the query string. For Story Aeneid use `chain=0x523` (1315 in hex) or the decimal value your client sends (MoralisApiClient uses `chainId.ToString("x")` so it sends hex). Confirm on [Moralis Supported Chains](https://docs.moralis.io/supported-chains) if **1315** (Story) is listed; if not, balances for this chain may need to come from RPC only (which the reference already does).

### Config summary

| Field     | Description |
|----------|-------------|
| `apiKey` | Your Moralis Web3 API key. Empty = no Moralis requests. |
| `baseUrl`| `https://deep-index.moralis.io/api` (default). |
| `version`| `v2` for REST v2. |

**Bottom line:** You can leave `apiKey` empty and rely on **RpcBalanceService** for JAB, USDC Krump, and $IP. Add Moralis only if you want to use TokenService’s Moralis-based balance methods or other Moralis features.

---

## 12. Optional: Bezi API & Relayer

- **JAB balance:** Can come from your Bezi/KrumpKraft API (`krumpKraftApiBaseUrl`). If not set, the reference uses RPC (Core.getBalance) only.  
- **Relayer:** For “Pay via x402”, a backend (e.g. Krump Verify `relayer/server.js`) must accept the signed payload and call the x402 adapter on chain 1315. Set **relayUrl** in your payment UI (e.g. `https://your-relayer.example.com` or `http://localhost:7350`).  
- **Verification:** Use `scripts/verify-x402-digest.mjs` (see X402_EIP712_SPEC) to compare Unity’s EIP-712 digest with a reference implementation if you hit “Invalid x402 signature”.

---

## 13. Quick Start for New Projects

1. Copy the `krumpkraft-unity` folder into `Assets/` (or create the structure above and copy the scripts).  
2. Add **Nethereum.Unity** and **TextMeshPro** to the project.  
3. Create **Resources/ChainSettings.json** (and optionally MoralisConfig.json, WalletConfig.json).  
4. Create a scene with **KrumpKraftManager**, **TransactionService**, **WalletConnectUI**, three **TokenBalanceUI** (JAB, USDC Krump, IP), and your payment UI wired to **PaymentTestUI** (or equivalent).  
5. Set Wallet Provider Mode on KrumpKraftManager; for real signing (JAB pay, x402), use Private Key or Web3Auth and ensure Nethereum is installed.  
6. For x402: ensure relayer uses **chainId 1315** and that EIP-712 uses **EvvmX402Adapter** as `to` and `verifyingContract`.

This gives you EVVM Story (1140), x402 (USDC Krump), JAB, and $IP in Unity with the same contracts and behavior as the reference implementation.
