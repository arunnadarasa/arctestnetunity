#!/usr/bin/env node
/**
 * Reads Unity's x402 digest log from .cursor/debug.log and recomputes the
 * EIP-712 digest with viem. Use to verify Unity's encoding matches the adapter.
 *
 * Usage:
 *   npm install viem
 *   node verify-x402-digest.mjs [path-to-debug.log]
 *
 * Default log path: ../.cursor/debug.log (from scripts/ dir)
 */

import { readFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";
import { hashTypedData, recoverAddress } from "viem";

const __dirname = dirname(fileURLToPath(import.meta.url));
const defaultLogPath = resolve(__dirname, "..", ".cursor", "debug.log");
const logPath = process.argv[2] || defaultLogPath;

const DOMAIN = {
  name: "USDC Dance",
  version: "1",
  chainId: 1315,
  verifyingContract: "0xDf5eaED856c2f8f6930d5F3A5BCE5b5d7E4C73cc",
};

const TYPES = {
  TransferWithAuthorization: [
    { name: "from", type: "address" },
    { name: "to", type: "address" },
    { name: "amount", type: "uint256" },
    { name: "validAfter", type: "uint256" },
    { name: "validBefore", type: "uint256" },
    { name: "nonce", type: "bytes32" },
  ],
};

async function main() {
  let raw;
  try {
    raw = readFileSync(logPath, "utf8");
  } catch (e) {
    console.error("Failed to read log:", e.message);
    console.error("Usage: node verify-x402-digest.mjs [path-to-debug.log]");
    process.exit(1);
  }

  const digestLines = raw.split("\n").filter((l) => l.includes('"message":"x402 digest"'));
  const line = digestLines.length > 0 ? digestLines[digestLines.length - 1] : null;
  if (!line) {
    console.error("No 'x402 digest' line in log. Run Pay via x402 in Unity first.");
    process.exit(1);
  }

  let parsed;
  try {
    parsed = JSON.parse(line);
  } catch (e) {
    console.error("Invalid JSON in log line:", e.message);
    process.exit(1);
  }

  const d = parsed.data || {};
  const digestUnity = (d.digest || "").toLowerCase();
  const from = (d.from || "").trim();
  const x402To = (d.x402To || "").trim();
  const amountWei = d.amountWei != null ? String(d.amountWei) : "0";
  const validAfter = Number(d.validAfter) || 0;
  const validBefore = Number(d.validBefore) || 0;
  const nonceFull = (d.nonceFull || "").trim();

  if (!digestUnity || !from || !x402To || !nonceFull) {
    console.error("Missing required fields in log (digest, from, x402To, nonceFull).");
    process.exit(1);
  }

  const message = {
    from: from,
    to: x402To,
    amount: BigInt(amountWei),
    validAfter: BigInt(validAfter),
    validBefore: BigInt(validBefore),
    nonce: nonceFull.startsWith("0x") ? nonceFull : "0x" + nonceFull,
  };

  const digestViem = hashTypedData({
    domain: DOMAIN,
    types: TYPES,
    primaryType: "TransferWithAuthorization",
    message,
  });

  const unityNorm = digestUnity.startsWith("0x") ? digestUnity : "0x" + digestUnity;
  const match = unityNorm.toLowerCase() === digestViem.toLowerCase();

  console.log("Inputs:");
  console.log("  from:", from);
  console.log("  to (adapter):", x402To);
  console.log("  amountWei:", amountWei);
  console.log("  validAfter:", validAfter, " validBefore:", validBefore);
  console.log("  nonce:", nonceFull.slice(0, 18) + "...");
  console.log("");
  console.log("Unity digest: ", unityNorm);
  console.log("Viem digest:  ", digestViem);
  console.log("");
  if (match) {
    console.log("Digest: MATCH — Unity encoding matches viem/adapter.");
  } else {
    console.log("Digest: MISMATCH — Check chainId, nonce (bytes32), amount/validAfter/validBefore (uint256), or address encoding in Unity.");
    process.exit(1);
  }

  // Signature recovery: prove that (digest, v, r, s) recovers to `from` (use last matching line = same run as digest)
  const sigLines = raw.split("\n").filter((l) => l.includes('"message":"x402 signature and payload args"'));
  const sigLine = sigLines.length > 0 ? sigLines[sigLines.length - 1] : null;
  if (sigLine) {
    try {
      const sigParsed = JSON.parse(sigLine);
      const sd = sigParsed.data || {};
      const v = Number(sd.v);
      const r = (sd.r || "").trim();
      const s = (sd.s || "").trim();
      if (v !== undefined && r && s && r.length >= 64 && s.length >= 64) {
        const rHex = (r.startsWith("0x") ? r.slice(2) : r).slice(0, 64).padStart(64, "0");
        const sHex = (s.startsWith("0x") ? s.slice(2) : s).slice(0, 64).padStart(64, "0");
        const tryRecover = async (vVal) => {
          const vHex = (vVal <= 1 ? 27 + vVal : vVal).toString(16).padStart(2, "0");
          return recoverAddress({ hash: unityNorm, signature: "0x" + rHex + sHex + vHex });
        };
        const rec27 = await tryRecover(27);
        const rec28 = await tryRecover(28);
        const recoveryMatch27 = rec27.toLowerCase() === from.toLowerCase();
        const recoveryMatch28 = rec28.toLowerCase() === from.toLowerCase();
        console.log("Recovery (v=27):", rec27);
        console.log("Recovery (v=28):", rec28);
        if (recoveryMatch27 || recoveryMatch28) {
          const usedV = recoveryMatch27 ? 27 : 28;
          console.log("Recovery: MATCH — Signature valid with v=" + usedV + "; recovered signer equals from.");
          console.log("");
          console.log("If the contract still reverts, the adapter is computing a different digest (e.g. relayer chainId != 1315).");
        } else {
          console.log("Recovery: MISMATCH — Neither v=27 nor v=28 recovers to from. Signature may be for a different digest or key.");
          console.log("Check: Unity must sign the exact 32-byte digest (no extra hashing); Nethereum Sign(byte[]) must sign raw hash.");
          process.exit(1);
        }
      }
    } catch (e) {
      console.log("Recovery: skipped (parse error or missing v,r,s):", e.message);
    }
  } else {
    console.log("Recovery: skipped (no 'x402 signature and payload args' line in log).");
  }

  process.exit(0);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
