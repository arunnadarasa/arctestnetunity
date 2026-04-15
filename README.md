# Arc Testnet for Unity (KrumpKraft)

Unity 6 project integrating **MetaMask Embedded Wallets / Web3Auth** for wallet connect, with Arc testnet–oriented payment flows.

## Setup

1. Open the project in **Unity 6** (folder containing `Assets`, `Packages`, `ProjectSettings`).
2. Copy `Assets/krumpkraft-unity/Assets/Resources/WalletConfig.example.json` to `WalletConfig.json` in the same folder and fill in:
   - **web3AuthClientId** — from [Web3Auth / MetaMask Embedded Wallets dashboard](https://dashboard.web3auth.io/)
   - **web3AuthVerifier** — Auth Connection ID for custom Google (e.g. `arc-unity`)
   - **googleClientId** — Google OAuth 2.0 Web client ID (Google Cloud Console)
3. Ensure **Authorized redirect URIs** in Google Cloud include `https://auth.web3auth.io/auth`, and **Authorized JavaScript origins** include `https://auth.web3auth.io` for hosted login.

## Docs

- [Web3Auth / Unity integration learnings](docs/Web3Auth-Unity-integration-learnings.md) — debugging notes, what worked, and hosted-auth caveats.

## License

See [LICENSE](LICENSE) if present in this repository.
