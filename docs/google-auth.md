# Google sign-in

OAuth 2.0 **authorization-code flow with PKCE (S256)**, OpenID Connect ID-token validation, `state` + `nonce`,
implemented in the API (`GoogleAuthController`). The browser never sees a client secret or an access token in a URL.

```
Browser ──GET /api/auth/google/start──────────────▶ API   sets HttpOnly g_oauth cookie {state, nonce, verifier}
Browser ◀──302 accounts.google.com/…──────────────  API
Browser ──(user consents at Google)──▶ GET /api/auth/google/callback?code&state
API validates state (cookie, constant-time, single use), exchanges the code (+ verifier + secret),
validates the ID token (RS256 signature vs Google JWKS, iss, aud, exp, nonce, email_verified)
API ──302 {frontend}/auth/google/complete?ticket=…──▶ Next route handler
Next ──POST /api/auth/google/exchange {ticket}────▶ API   single-use 60 s ticket (hashed in DB) → normal JWT pair
Next sets the usual HttpOnly session cookies and redirects to a sanitised same-site path.
```

Sign-out is the existing logout (revokes our refresh token). Password login is untouched.

## Account rules (`ExternalAccountService`)

| Situation | Result |
|---|---|
| `(Google, sub)` already linked | sign in as that account (suspended ⇒ refused) |
| Unknown identity, **no** local account with that email | create a new **Student** account (no password, email confirmed) |
| Unknown identity, local account exists, email **confirmed**, **not** Admin/Moderator | link and sign in |
| Unknown identity, local account exists but email **unconfirmed** or holds a **privileged role** | **refused** (`google_link_required`) — sign in with the existing method, then link from *Profil → Lier mon compte Google* |
| Google email not verified | refused |

This blocks pre-hijacking (an attacker registering a victim's address with a password first) and any privilege
gain through Google: new accounts are Students only and roles are never read from Google. A Google identity can
belong to only one account.

## Google Cloud Console

1. <https://console.cloud.google.com/> → create/select a project → **APIs & Services → OAuth consent screen**
   (External; app name, support email; scopes `openid`, `email`, `profile`; add test users while unpublished).
2. **Credentials → Create credentials → OAuth client ID → Web application.**
3. **Authorised redirect URIs** — exactly (scheme, host, path), one per environment:
   * dev: `http://localhost:3000/api/auth/google/callback`
   * prod: `https://<your-domain>/api/auth/google/callback`

   (The browser talks to the Next.js origin, which proxies `/api/*` to the API; the state cookie is therefore
   same-origin.) No JavaScript origins are needed.
4. Copy the client id and secret into the **API's** secrets (never into source control or the frontend):

```bash
cd src/USTHBStudy.API
dotnet user-secrets set "Authentication:Google:ClientId"     "<id>.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>"
dotnet user-secrets set "Authentication:Google:RedirectUri"  "http://localhost:3000/api/auth/google/callback"
dotnet user-secrets set "Authentication:Google:FrontendBaseUrl" "http://localhost:3000"
```

Production: environment variables `Authentication__Google__ClientId`, `…__ClientSecret`, `…__RedirectUri`,
`…__FrontendBaseUrl` (use `https://`; the flow cookie is then `Secure`). If any of ClientId / ClientSecret /
RedirectUri is empty, Google sign-in is **off**: `/api/auth/providers` reports it, the buttons are hidden and
`/api/auth/google/start` returns 404.

Notes: Data Protection keys protect the flow cookie — in a multi-instance deployment persist/share the key ring.
Behind a TLS-terminating proxy keep `FrontendBaseUrl` on `https://` (or configure forwarded headers).
