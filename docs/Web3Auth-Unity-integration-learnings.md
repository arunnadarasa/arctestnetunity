# Web3Auth / MetaMask Embedded Wallets — Unity integration learnings

This document summarizes what we learned while wiring **MetaMask Embedded Wallets (Web3Auth)** into **KrumpKraft** (Unity 6), including **what worked**, **what failed**, and **what remains unresolved** on the hosted auth page (`auth.web3auth.io`).

---

## Context

- **SDK**: Web3Auth Unity plugin under `Assets/Plugins/Web3AuthSDK/`.
- **App integration**: `Assets/krumpkraft-unity/Assets/Scripts/Services/Web3AuthWalletProvider.cs` (reflection-based so it stays compatible with the Web3Auth assembly).
- **Config**: `Assets/krumpkraft-unity/Assets/Resources/WalletConfig.json` + `WalletConfig.cs`.
- **Dashboard**: Web3Auth / MetaMask developer dashboard — project **KrumpKraft**, **Sapphire Devnet** (`sapphire_devnet`), **Plug and Play, Single Factor Auth**.

---

## Successes (verified in code or runtime)

### Session store and hosted v10 shape

- **Session API v2**: `Web3AuthApi` base URL **`https://session.web3auth.io/v2`** so `store/get` and `store/set` match what `auth.web3auth.io` expects (avoid empty `{}` / blank UI from legacy endpoints).
- **`allowedOrigin` / `Origin`**: Used on create/logout/authorize flows so session encryption lines up with the redirect origin (e.g. `http://localhost:<port>`).
- **Hosted v10 arrays**: `loginConfig` and `authConnectionConfig` must be **JSON arrays** in the session payload, not only string-keyed maps. **`PatchV10AuthConnectionIntoInitParams()`** in `Web3Auth.cs` merges from `Web3AuthOptions.loginConfig` or injects a default Google row when empty.
- **`authorizeSession`**: Guard when `response.message` is null; order of operations with `initParams` so merge is correct.

### Spurious logout

- **`OnWeb3AuthLogout`**: Ignore logout events that fire while a connect is in progress (`_pendingConnectCallback`), which can be triggered by stale session refresh.

### Configuration surface

- **`WalletConfig`**: `web3AuthClientId`, `web3AuthNetwork` (`SAPPHIRE_DEVNET` / `SAPPHIRE_MAINNET`), optional `web3AuthRedirectUrl`, custom Google via `web3AuthVerifier` + `googleClientId`, optional `defaultGoogleAuthConnectionId` for default (non-custom) Google.
- **Scene vs JSON**: `Web3Auth` component’s serialized `clientId` should match `WalletConfig` — mismatch breaks project binding.

### Custom Google connection (`arc-unity`)

Runtime logs and Unity `paramMap` confirmed:

- **`loginProvider`**: `CUSTOM_VERIFIER`
- **`authConnectionId`**: `arc-unity` (must match **Auth Connection ID** in the dashboard)
- **`loginConfig`**: Dictionary keyed by **`arc-unity`** with `LoginConfigItem`: `verifier`, `typeOfLogin = GOOGLE`, **`clientId` = Google OAuth Web Client ID** (`.apps.googleusercontent.com`)
- **`options.defaultGoogleAuthConnectionId`**: Set to **`arc-unity`** when using custom verifier so `PatchV10` and options stay aligned.

`createSession` **succeeds** (a `loginId` is returned; `finalUriBuilderToOpen` logs a valid `auth.web3auth.io/.../start#b64Params=...` URL).

### Google Cloud (required for custom OAuth)

- **Authorized redirect URIs**: `https://auth.web3auth.io/auth` (per MetaMask / Web3Auth docs).
- **Authorized JavaScript origins**: `https://auth.web3auth.io` — needed for OAuth flows that run in context of the hosted auth origin; leaving this empty can break or degrade login even when redirect URI is correct.
- **Testing / Audience**: While the OAuth app is in **Testing**, only **test users** listed in Google Cloud can complete Google sign-in.

### Web3Auth dashboard

- **Domains / allowlist**: `http://localhost` (and any other origins you use) under **Project Settings → Domains** for devnet.
- **Authentication → Social connections → Google**: Custom connection **`arc-unity`** with the same **Google Client ID** as in `WalletConfig.json`.

---

## Failures / dead ends (did not fix the hosted error by themselves)

Symptom on **`auth.web3auth.io`**:  
**“Invalid auth connection or custom auth connection data not available”** (sometimes after a blank page earlier in the thread).

### Wrong default connection IDs (non-custom flows)

- **`w3a-google-demo`**: Documented in MetaMask **Embedded Wallets** samples for *implicit* Google login — **not guaranteed** for every dashboard project. Using it with an arbitrary Web3Auth **Client ID** often produced **invalid auth connection**.
- **`w3a-google`**: Tried as a Plug & Play–style default — **still** rejected for this project until the real issue was understood to be **custom** `arc-unity`, not defaults.

### Experiments on `LoginParams` (custom `arc-unity` path)

Runtime instrumentation showed **`loginConfig` already contained** the full Google OAuth `clientId` (72 chars) and correct verifier. We still tried:

| Experiment | Result |
|------------|--------|
| Set **`LoginParams.clientId`** to **Google OAuth** client id | Hosted error **persisted** |
| Set **`LoginParams.clientId`** to **Web3Auth project** id + **`ExtraLoginOptions.client_id`** = Google OAuth | Hosted error **persisted** |

**Conclusion:** Those shapes were **reverted**; the session payload was already correct **without** duplicating OAuth data on `LoginParams` for this flow. The failure was **not** explained by missing `clientId` on `LoginParams` alone.

### Signer configuration API

- **`GET .../api/configuration?project_id=...&network=sapphire_devnet&whitelist=true`** returns a **short** JSON (~346 bytes): network flags, whitelist, wallet connect, etc.
- It does **not** list **`arc-unity`** or auth-connection rows (`containsArcUnity: false` in debug runs). That is **expected** — auth connections are validated on **hosted auth**, not echoed wholesale in this response.

---

## Debug session (session id `6132dd`) — hypotheses vs evidence

| ID | Idea | Outcome |
|----|------|---------|
| **H1** | Custom verifier path not active at runtime | **Rejected** — `usesCustom: true`, verifier length matches `arc-unity` |
| **H2** | Wrong `loginProvider` / `authConnectionId` | **Rejected** — `CUSTOM_VERIFIER`, `arc-unity` |
| **H3** | Missing Google `clientId` in `initParams.loginConfig` | **Rejected** — `firstLoginConfigClientIdLen: 72` |
| **H4** | Wrong `defaultGoogleAuthConnectionId` on options | **Rejected** — `arc-unity` when custom |
| **H5** | Scene `Web3Auth.clientId` ≠ `WalletConfig` | **Rejected** — lengths match, not empty |
| **H6/H7** | Fix via `LoginParams` / `ExtraLoginOptions` | **Rejected as fix** — error reproduced with correct lengths logged |
| **H8** | Signer API would list `arc-unity` | **Rejected** — API body does not include it; **not** a bug in our parser |

Instrumentation was **removed** after analysis; no permanent NDJSON logging remains in the repo.

---

## What was *not* the root cause

- **“Fresh install” of all C# scripts** — not indicated. The assembled **`paramMap`** matched the dashboard and **`createSession` succeeded**.
- **Unity-only bug** in the custom verifier wiring — logs contradicted that for the final `arc-unity` + Google setup.

---

## Likely remaining causes (outside Unity)

1. **Google OAuth client propagation** — Google states OAuth client changes can take **minutes to hours**.
2. **Web3Auth hosted / backend** — Connection `arc-unity` must be **fully registered** for that **Client ID** on their side; intermittent “invalid auth connection” may require **Web3Auth support** with a failing **`loginId`** from `[W3A] finalUriBuilderToOpen`.
3. **Allowlist / origin** — Redirect uses **`http://localhost:<randomPort>/complete/`**; dashboard allowlists **`http://localhost`**. This is usually OK (signing often keys off `http://localhost`), but if issues persist, confirm with Web3Auth docs/support for **port-specific** dev URLs.

---

## Key files (reference)

| File | Role |
|------|------|
| `Assets/krumpkraft-unity/Assets/Resources/WalletConfig.json` | Client id, network, `arc-unity`, Google OAuth client id |
| `Assets/krumpkraft-unity/Assets/Scripts/Core/WalletConfig.cs` | Serialized fields + `ResolvedDefaultGoogleAuthConnectionId` for default Google |
| `Assets/krumpkraft-unity/Assets/Scripts/Services/Web3AuthWalletProvider.cs` | Connect, `setOptions`, `login` with `CUSTOM_VERIFIER` + `loginConfig` |
| `Assets/Plugins/Web3AuthSDK/Web3Auth.cs` | `setOptions`, `processRequest`, `PatchV10AuthConnectionIntoInitParams`, session |
| `Assets/Plugins/Web3AuthSDK/Api/Web3AuthApi.cs` | Session v2, `fetchProjectConfig` |
| `Assets/Plugins/Web3AuthSDK/Types/LoginParams.cs` | `loginProvider`, `authConnectionId`, `clientId`, etc. |

---

## Support checklist (copy-paste)

When opening a ticket with Web3Auth / MetaMask:

- Web3Auth **Client ID** (project)
- **Network**: Sapphire Devnet
- **Auth Connection ID**: `arc-unity`
- **Google OAuth Web Client ID** (full string)
- Example **failing `loginId`** or full **`b64Params`** payload from Unity console
- Note: **`/api/configuration` does not return auth connection names**; hosted still returns “invalid auth connection” despite **`loginConfig`** matching the dashboard

---

## Docs we aligned with

- [MetaMask — Embedded Wallets — Unity](https://docs.metamask.io/embedded-wallets/sdk/unity)
- [MetaMask — Google login (Embedded Wallets)](https://docs.metamask.io/embedded-wallets/authentication/social-logins/google/) — examples use `w3a-google-demo` in snippets; **your project** used a **custom** connection `arc-unity` instead.

---

*Generated from integration and debug work on the KrumpKraft EVVM Unity project.*
