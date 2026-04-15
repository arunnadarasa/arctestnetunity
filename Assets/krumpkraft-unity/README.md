# KrumpKraft Unity

Unity module for **KrumpChain** (EVVM Story, Chain ID **1140**) with **JAB (KRUMP)**, **USDC Krump**, and **$IP** token support. Use it to connect a wallet, display balances, and sign EVVM payment payloads (Core.pay) and **x402** (Pay via x402 with USDC Krump).

**→ For a step-by-step guide to integrate EVVM Story, x402, USDC Krump, and IP into your own Unity game, see [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md).**

## Dependencies

- **Unity** 2019.4+ (or as required by your target platform).
- **TextMeshPro** (Unity Package Manager) for the built-in UI scripts.
- **Nethereum** (e.g. Nethereum.Unity or Nethereum.Signer) for real EIP-191 signing when using Private Key Config or Web3Auth.
- **MetaMask Embedded Wallets (Web3Auth) Unity package** only when using Wallet Provider Mode **Web3Auth Embedded Wallet**.

## Setup

1. **Add to your Unity project:** Copy the entire `krumpkraft-unity/` folder into your Unity project's `Assets` folder, or open the folder as a separate Unity project.
2. **Moralis API key:** Edit `Assets/Resources/MoralisConfig.json` and set your `apiKey`. Base URL is Moralis REST v2. For balance queries, try `chain=1315` (Story Aeneid Testnet) or `chain=1140` if supported; see [evvm.info](https://www.evvm.info) for RPC/fallback.
3. **Chain/RPC:** `ChainSettings.json` is preconfigured with **Story Aeneid Testnet** (host chain for EVVM). RPC: [https://aeneid.storyrpc.io](https://aeneid.storyrpc.io), Explorer: [https://aeneid.storyscan.io](https://aeneid.storyscan.io).
4. **JAB balance API (Bezi):** `RpcBalanceService` fetches JAB balance from a small HTTP API. Set **`krumpKraftApiBaseUrl`** in `ChainSettings.json` (e.g. `http://localhost:3099`). Run the **Bezi API** from the main KrumpKraft repo: `cd bezi-api && npm install && npm start`. See main repo `bezi-api/README.md`.

## EVVM 1140 on Story Aeneid Testnet

**EVVM 1140** (KrumpChain) is deployed on **Story**. The host chain is:

- **Network:** Story Aeneid Testnet  
- **Chain ID:** 1315  
- **RPC:** [https://aeneid.storyrpc.io](https://aeneid.storyrpc.io)  
- **Blockscout:** [https://aeneid.storyscan.io](https://aeneid.storyscan.io)  

The module uses **EVVM ID 1140** for pay payloads and Core contract context; RPC and explorer use Story Aeneid (1315) by default.

**Tokens (KrumpChain / EVVM 1140):**
- **Principal token:** JAB (KRUMP) at `0x0000000000000000000000000000000000000001` (18 decimals)
- **USDC Krump:** `0xd35890acdf3BFFd445C2c7fC57231bDE5cAFbde5` (6 decimals)

Contract addresses (Core, NameService, Staking, Estimator, Treasury, P2PSwap) are in `ContractAddresses.cs` and match the KrumpChain deployment on Story.

## Test scene: payment with JAB and USDC Krump

1. Open **Assets/Scenes/KrumpKraftPaymentTest.unity**.
2. Select the **KrumpKraftManager** GameObject and add the **KrumpKraftManager** script. Optionally add **AutoUISetup**: enable **Create Hierarchy If Missing** to auto-create Canvas + panels when none exist; then add buttons/inputs under each panel and use **Run Auto Setup** to wire references. Or build the Canvas and UI yourself and assign references in the Inspector.
3. Enter Play mode.
4. **Connect wallet** via the WalletConnectUI (connect/disconnect; address shown when connected).
5. **Balances:** The scene shows JAB and USDC Krump balances (refreshed via Moralis REST v2 for chain 1140 when supported).
6. **Payment panel:**
   - Enter **recipient** address.
   - Choose token: **JAB** or **USDC Krump**.
   - Enter **amount**.
   - Click **Sign pay** to build the EVVM Core.pay message (EIP-191) and sign with your wallet.
   - Optionally **Copy payload** for manual submit to a fisher/relay, or **Send to relay** if a relay URL is configured.

No backend is required for signing; execution can be done by pasting the signed payload into your relay or using a configured relay URL later. For **builds**, add **Assets/Scenes/KrumpKraftPaymentTest.unity** to **File → Build Settings → Scenes In Build** if you want the test scene included.

## Wallet: stub, private key, MetaMask Embedded Wallets, or custom

The project does **not** use an external wallet by default. You choose how the “wallet” is provided:

- **Simple stub (default)**  
  **KrumpKraftManager** → Wallet Provider Mode = **Simple Stub**. Uses a fixed test address and a fake signature. Good for testing UI and flow only; no real signing.

- **Private key (dev/test only)**  
  1. Set **Wallet Provider Mode** to **Private Key Config** on KrumpKraftManager.  
  2. Create or edit **Assets/Resources/WalletConfig.json** and set `"usePrivateKey": true`, `"privateKey": "0xYourHexKey"` (and optionally `"address"` for display if you don’t add Nethereum).  
  3. **Important:** Add `Assets/Resources/WalletConfig.json` to `.gitignore` so the key is never committed.  
  4. For **real signing** (EIP-191), add the **Nethereum** package (e.g. Nethereum.Unity or Nethereum.Signer). Without Nethereum, Connect still works with the address from config, but Sign pay will fail.  
  Use this only in development; never ship an app that loads a private key from config.

- **MetaMask Embedded Wallets (Unity)**  
  Real embedded wallets (social login, etc.) with **no extension required**.  
  1. Install the [MetaMask Embedded Wallets SDK for Unity](https://docs.metamask.io/embedded-wallets/sdk/unity) (Web3Auth Unity package). Add the **Web3Auth** component to a GameObject in your scene (e.g. the **Web3Auth** placeholder created by **AutoUISetup** when you use **Create Default Hierarchy**, or the same GameObject as KrumpKraftManager).  
  2. In **KrumpKraftManager**, set Wallet Provider Mode to **Web3Auth Embedded Wallet**.  
  3. In **Assets/Resources/WalletConfig.json**, set `"web3AuthClientId": "YOUR_CLIENT_ID"` (get your Client ID from the [Embedded Wallets dashboard](https://docs.metamask.io/embedded-wallets/sdk/unity); prefer an env variable or gitignored config so the ID isn’t committed). Optionally set `"web3AuthRedirectUrl"` (defaults to `torusapp://com.torus.Web3AuthUnity/auth`).  
  4. Add **Nethereum** (e.g. Nethereum.Unity) so **Sign pay** works; the provider uses the key from the Web3Auth login response to sign EIP-191 messages.  
  If the SDK’s login event can’t be wired automatically, call `Web3AuthWalletProvider.SetLoginResult(response.privKey)` from your own Web3Auth `onLogin` handler.

- **Privy**  
  For another embedded-wallet option in Unity, see [Privy’s Unity SDK](https://docs.privy.io/basics/unity/installation). You can implement an **IWalletProvider** that uses Privy’s auth/signing and assign it when using Wallet Provider Mode **Custom**.

- **Custom (e.g. MetaMask extension / WalletConnect)**  
  Implement your own **IWalletProvider** (connect and sign via your SDK), assign it to **WalletService** from code after KrumpKraftManager runs, and set Wallet Provider Mode to **Custom** if you add that path in the manager.

## Bezi compatibility

This module is built to be Bezi-compatible: it uses standard Unity APIs and does not rely on deprecated SDKs. Use your Bezi project’s recommended way to integrate Unity assets.

## License

MIT. See [LICENSE](LICENSE).
