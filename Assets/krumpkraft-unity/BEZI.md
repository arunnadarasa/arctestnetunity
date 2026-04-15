# KrumpKraft Unity — Overview for Bezi (A to Z)

This document explains the **KrumpKraft Unity** project from start to finish so Bezi designers and developers can understand what it is, how it works, and how to use or extend it inside Bezi.

---

## A. What This Project Is

**KrumpKraft Unity** is a Unity module that lets users:

1. **Connect a wallet** (stub, private key, or MetaMask Embedded Wallets / Web3Auth).
2. **See token balances** for **JAB** (KRUMP) and **USDC Krump** on **KrumpChain**.
3. **Sign payments** (EVVM Core.pay) in **JAB** or **USDC Krump** and optionally send the signed payload to a relay or copy it for manual submit.

It targets **EVVM 1140** (KrumpChain), which is deployed on **Story Aeneid Testnet** (Chain ID 1315). No MetaMask extension is required when using the embedded-wallet option; the module is built to work with standard Unity and is **Bezi-compatible** (no deprecated SDKs).

---

## B. Core Concepts (Glossary)

| Term | Meaning |
|------|--------|
| **EVVM** | Ethereum Virtual Virtual Machine — virtual chain layer on top of a host chain. Users sign messages; “fishers” execute them on-chain. |
| **EVVM ID 1140 / KrumpChain** | The virtual chain this module uses. All pay payloads and contract logic refer to **1140**. |
| **Story Aeneid Testnet** | The **host chain** (Chain ID **1315**) where EVVM 1140 is deployed. RPC and block explorer use this chain. |
| **JAB (KRUMP)** | Principal token of KrumpChain. Address `0x…01`, 18 decimals. |
| **USDC Krump** | ERC-20–style token. Address `0xd35890acdf3BFFd445C2c7fC57231bDE5cAFbde5`, 6 decimals. |
| **Core.pay** | EVVM contract function: pay from one address to another in a given token. Authorized by an **EIP-191** signature. |
| **Wallet provider** | Implementation of “connect + sign” (e.g. stub, private key, Web3Auth). The app uses one at a time, chosen in **KrumpKraftManager**. |
| **Relay / Fisher** | Optional backend that receives a signed pay payload and executes it on-chain. The module can “Copy payload” for manual submit if no relay is configured. |

---

## C. Network and Chain IDs

- **Story Aeneid Testnet (host)**  
  - **Chain ID:** 1315  
  - **RPC:** https://aeneid.storyrpc.io  
  - **Explorer:** https://aeneid.storyscan.io  

- **EVVM 1140 (KrumpChain)**  
  - Used for: pay payloads, Core contract address, token addresses.  
  - Not a separate RPC; it runs **on** Story.  

The module uses **1140** for signing and contract context, and **1315** (Story) for RPC and explorer links by default.

---

## D. Project Layout (Files and Folders)

```
krumpkraft-unity/
├── LICENSE
├── README.md
├── BEZI.md                    ← this document
├── .gitignore
├── Assets/
│   ├── Resources/             ← config (loaded at runtime)
│   │   ├── ChainSettings.json   (chain 1140/1315, RPC, explorer)
│   │   ├── MoralisConfig.json   (Moralis API key, base URL)
│   │   └── WalletConfig.json    (optional: private key or Web3Auth Client ID)
│   ├── Scenes/
│   │   └── KrumpKraftPaymentTest.unity   (test scene)
│   └── Scripts/
│       ├── KrumpKraftManager.cs          (entry point; loads config, creates services)
│       ├── Core/                          (config, chain, API, wallet interface)
│       │   ├── ChainSettings.cs
│       │   ├── ConfigLoader.cs
│       │   ├── ContractAddresses.cs
│       │   ├── MoralisConfig.cs
│       │   ├── MoralisApiClient.cs
│       │   ├── IWalletProvider.cs
│       │   ├── WalletConfig.cs
│       │   └── PayPayloadBuilder.cs       (EIP-191 pay message + Keccak256)
│       ├── Services/
│       │   ├── WalletService.cs
│       │   ├── SimpleWalletProvider.cs
│       │   ├── PrivateKeyWalletProvider.cs
│       │   ├── Web3AuthWalletProvider.cs
│       │   ├── TokenService.cs
│       │   ├── KrumpVerifier.cs
│       │   └── X402PaymentService.cs
│       └── UI/
│           ├── WalletConnectUI.cs
│           ├── TokenBalanceUI.cs
│           ├── PaymentTestUI.cs
│           ├── DanceMoveUI.cs
│           └── AutoUISetup.cs
```

All C# lives in the **KrumpKraft** namespace.

---

## E. How It Fits Together (Architecture)

1. **KrumpKraftManager** (single instance, usually on one GameObject)  
   - Loads `ChainSettings`, `MoralisConfig`, and optionally `WalletConfig` from **Resources**.  
   - Creates **WalletService**, **TokenService**, **MoralisApiClient**.  
   - Chooses the **wallet provider** (Simple Stub, Private Key Config, Web3Auth Embedded Wallet, or Custom).  

2. **WalletService**  
   - Exposes: Connect, Disconnect, SignMessage, ConnectedAddress, IsConnected, and events (OnConnected, OnDisconnected).  
   - Delegates to the active **IWalletProvider** (e.g. SimpleWalletProvider, Web3AuthWalletProvider).  

3. **TokenService**  
   - Fetches **JAB** and **USDC Krump** balances via **MoralisApiClient** (REST v2).  
   - Uses the chain ID from **ChainSettings** (for Moralis you may try 1315 or 1140 depending on support).  

4. **UI components**  
   - **WalletConnectUI:** Connect/Disconnect, address label, optional “connected” panel.  
   - **TokenBalanceUI:** One per token (JAB or USDC Krump); shows balance and optional refresh.  
   - **PaymentTestUI:** Recipient, token choice (JAB/USDC Krump), amount, **Sign pay**, **Copy payload** (and optional relay).  
   - They read **KrumpKraftManager.Instance** to get **WalletService** and **TokenService**.  

5. **PayPayloadBuilder**  
   - Builds the EVVM pay signature payload and EIP-191 message.  
   - Used when the user taps **Sign pay**; the wallet provider signs that message.  

6. **AutoUISetup**  
   - Can **create** a default hierarchy (Canvas, panels, **Web3Auth** placeholder) and **wire** references to the UI components so the test scene works with minimal manual setup.  

---

## F. User Flow (What Happens at Runtime)

1. Scene loads → **KrumpKraftManager.Awake** runs → configs loaded, services created, wallet provider set.  
2. User taps **Connect** (WalletConnectUI) → WalletService.Connect → provider runs (e.g. Web3Auth login or stub connect).  
3. On success → OnConnected → UI refreshes; TokenBalanceUI requests JAB and USDC Krump balances via TokenService (Moralis).  
4. User fills **recipient**, **token** (JAB or USDC Krump), **amount** and taps **Sign pay** → PaymentTestUI builds the pay payload, asks WalletService.SignMessage → provider returns signature → UI shows “Signed” and stores the full signed payload.  
5. User can **Copy payload** (clipboard) or use **Send to relay** if a relay URL is set (e.g. X402PaymentService).  
6. **Disconnect** clears the wallet state and updates the UI.  

No backend is required for signing; a relay is optional for executing the payment on-chain.

---

## G. Configuration (What to Set)

| File | Purpose |
|------|--------|
| **ChainSettings.json** | Chain name, EVVM/chain IDs, RPC URL, block explorer, native symbol. Preconfigured with Story Aeneid (1315) and EVVM 1140. |
| **MoralisConfig.json** | Moralis API key, base URL, version. Used for token balance requests. |
| **WalletConfig.json** | Optional. For **Private Key Config:** `usePrivateKey`, `privateKey`, `address`. For **Web3Auth:** `web3AuthClientId`, `web3AuthRedirectUrl`. Keep secrets out of version control (e.g. .gitignore). |

Contract addresses (Core, NameService, Staking, Estimator, Treasury, P2PSwap, JAB, USDC Krump) are in **ContractAddresses.cs**. Change them there if you deploy to a different EVVM or network.

---

## H. Wallet Options (Summary)

| Mode (in KrumpKraftManager) | Use case | Notes |
|-----------------------------|----------|--------|
| **Simple Stub** | UI/flow testing | Fixed address, fake signature; no real signing. |
| **Private Key Config** | Dev/test | Key in WalletConfig.json; needs Nethereum for signing. Do not commit the file. |
| **Web3Auth Embedded Wallet** | Production-style embedded wallet | Install MetaMask Embedded Wallets (Web3Auth) Unity package; add Web3Auth component (e.g. on “Web3Auth” placeholder from AutoUISetup); set Client ID in WalletConfig; Nethereum for signing. |
| **Custom** | Your own wallet (e.g. WalletConnect) | Implement **IWalletProvider**, assign to WalletService after Manager runs. |

For Bezi, **Simple Stub** is enough to drive the UI; **Web3Auth Embedded Wallet** gives real logins and signing without the MetaMask extension.

---

## I. Test Scene and AutoUISetup

- **Scene:** `Assets/Scenes/KrumpKraftPaymentTest.unity`.  
- Contains: Main Camera, Directional Light, **KrumpKraftManager** GameObject (add the script if missing).  
- **AutoUISetup** (on the same or another GameObject):  
  - **Create Hierarchy If Missing:** Creates Canvas, EventSystem, **Web3Auth** placeholder, and panels (Wallet, JAB balance, USDC balance, Payment) with the right scripts. You still add buttons/inputs under each panel, then **Run Auto Setup** to wire references.  
  - **Run Auto Setup:** Finds WalletConnectUI, TokenBalanceUI, PaymentTestUI under the Canvas and assigns their buttons/labels/inputs by name.  

For **Web3Auth**, attach the Web3Auth component from the Embedded Wallets package to the **Web3Auth** GameObject (or to KrumpKraftManager).

---

## J. Dependencies

- **Unity** 2019.4+ (or per your/Bezi’s target).  
- **TextMeshPro** (Unity Package Manager) for the built-in UI.  
- **Nethereum** (e.g. Nethereum.Unity) for real EIP-191 signing when using Private Key or Web3Auth.  
- **MetaMask Embedded Wallets (Web3Auth) Unity package** only when using **Web3Auth Embedded Wallet** mode.  

No deprecated or Bezi-incompatible SDKs are used.

---

## K. Bezi Integration (How to Use This in Bezi)

- **Import:** Copy the **krumpkraft-unity** folder into your Unity project’s Assets (or use Bezi’s recommended way to bring in Unity assets).  
- **Scenes:** Open or duplicate `KrumpKraftPaymentTest` as needed; add the scene to Build Settings if it’s part of a build.  
- **Single entry point:** Ensure one **KrumpKraftManager** in the scene; it loads config and creates services. Optionally add **AutoUISetup** to create/wire UI.  
- **Wallet:** Choose a provider in KrumpKraftManager (e.g. Simple Stub for prototyping, Web3Auth for real wallets).  
- **Config:** Set **MoralisConfig** (and **WalletConfig** if using private key or Web3Auth) in **Assets/Resources**; keep secrets out of repo.  
- **Custom UI:** You can replace or restyle the provided UI; the logic lives in WalletConnectUI, TokenBalanceUI, PaymentTestUI and only needs the same services from KrumpKraftManager.  
- **Bezi flows:** Use your Bezi project’s recommended patterns for scene flow, UI, and where to trigger “Connect” or “Sign pay” from Bezi-designed screens.  

The module does not depend on Bezi-specific APIs; it uses standard Unity and can be treated as a self-contained “wallet + balances + sign pay” block that you plug into your Bezi experience.

---

## L. Quick Reference: Key Scripts and Their Roles

| Script | Role |
|--------|------|
| **KrumpKraftManager** | Entry point; loads config; creates WalletService, TokenService, MoralisApiClient; selects wallet provider. |
| **WalletService** | Single place for connect/disconnect/sign; forwards to IWalletProvider. |
| **TokenService** | JAB and USDC Krump balances via Moralis (and chain from ChainSettings). |
| **PayPayloadBuilder** | Builds EVVM pay message and EIP-191 payload for signing. |
| **WalletConnectUI** | Connect/Disconnect buttons, address label, connected panel. |
| **TokenBalanceUI** | Shows one token balance (JAB or USDC Krump); refresh button. |
| **PaymentTestUI** | Recipient, token, amount, Sign pay, Copy payload, optional relay. |
| **AutoUISetup** | Create default Canvas/panels/Web3Auth placeholder; wire UI references. |
| **ContractAddresses** | All contract and token addresses for EVVM 1140 / KrumpChain. |

---

This gives Bezi a full A–Z picture: what the project is, how chains and wallets work, how the code is structured, how config and UI fit in, and how to use it inside Bezi without relying on deprecated SDKs.
