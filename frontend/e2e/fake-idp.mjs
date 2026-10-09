// A tiny stand-in for Google's OAuth/OIDC endpoints, used only by the E2E suite. It enforces the same
// contracts the real flow relies on (PKCE S256, client secret, redirect URI, nonce in a signed RS256 ID token)
// so the real API code — state cookie, code exchange, ID-token validation — runs end to end.
import { createServer } from "node:http";
import { createHash, generateKeyPairSync, sign, randomBytes } from "node:crypto";

const PORT = Number(process.env.IDP_PORT ?? 5190);
const CLIENT_ID = process.env.IDP_CLIENT_ID ?? "e2e-client";
const CLIENT_SECRET = process.env.IDP_CLIENT_SECRET ?? "e2e-secret";
const REDIRECT_URI = process.env.IDP_REDIRECT_URI ?? "http://localhost:3100/api/auth/google/callback";

const { privateKey, publicKey } = generateKeyPairSync("rsa", { modulusLength: 2048 });
const jwk = { ...publicKey.export({ format: "jwk" }), kid: "e2e-key", use: "sig", alg: "RS256" };

let identity = { sub: "e2e-google-sub-1", email: "gina.e2e@example.test", email_verified: true, given_name: "Gina", family_name: "Googler" };
const codes = new Map(); // code → { nonce, challenge }

const b64url = (buf) => Buffer.from(buf).toString("base64url");

function idToken(nonce) {
  const now = Math.floor(Date.now() / 1000);
  const header = b64url(JSON.stringify({ alg: "RS256", typ: "JWT", kid: "e2e-key" }));
  const payload = b64url(JSON.stringify({
    iss: "https://accounts.google.com", aud: CLIENT_ID, iat: now, exp: now + 600, nonce, ...identity,
  }));
  const signature = sign("RSA-SHA256", Buffer.from(`${header}.${payload}`), privateKey);
  return `${header}.${payload}.${b64url(signature)}`;
}

async function body(req) {
  const chunks = [];
  for await (const c of req) chunks.push(c);
  return Buffer.concat(chunks).toString();
}

createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);
  const json = (status, value) => { res.writeHead(status, { "Content-Type": "application/json" }); res.end(JSON.stringify(value)); };

  if (url.pathname === "/health") return json(200, { ok: true });

  // Test control: choose who "Google" will say the user is.
  if (url.pathname === "/__identity" && req.method === "POST") {
    identity = { ...identity, ...JSON.parse(await body(req)) };
    return json(200, identity);
  }

  if (url.pathname === "/authorize") {
    const q = url.searchParams;
    if (q.get("client_id") !== CLIENT_ID || q.get("redirect_uri") !== REDIRECT_URI
      || q.get("response_type") !== "code" || q.get("code_challenge_method") !== "S256"
      || !q.get("state") || !q.get("nonce") || !q.get("code_challenge")) {
      return json(400, { error: "invalid_request" });
    }
    const code = randomBytes(16).toString("hex");
    codes.set(code, { nonce: q.get("nonce"), challenge: q.get("code_challenge") });
    const back = new URL(REDIRECT_URI);
    back.searchParams.set("code", code);
    back.searchParams.set("state", q.get("state"));
    res.writeHead(302, { Location: back.toString() });
    return res.end();
  }

  if (url.pathname === "/token" && req.method === "POST") {
    const form = new URLSearchParams(await body(req));
    const entry = codes.get(form.get("code") ?? "");
    codes.delete(form.get("code") ?? ""); // single use
    const verifierHash = createHash("sha256").update(form.get("code_verifier") ?? "").digest("base64url");
    if (!entry || form.get("client_id") !== CLIENT_ID || form.get("client_secret") !== CLIENT_SECRET
      || form.get("redirect_uri") !== REDIRECT_URI || verifierHash !== entry.challenge) {
      return json(400, { error: "invalid_grant" });
    }
    return json(200, { access_token: "unused", token_type: "Bearer", id_token: idToken(entry.nonce) });
  }

  if (url.pathname === "/jwks") return json(200, { keys: [jwk] });
  json(404, { error: "not_found" });
}).listen(PORT, () => console.log(`fake idp on :${PORT}`));
