# Security

## Reporting a vulnerability

Please report security issues privately via GitHub's "Report a vulnerability" (Security Advisories) on this repository rather than opening a public issue.

## Deployment checklist

- Generate a unique `JWT_SIGNING_KEY` (32+ random characters, e.g. `openssl rand -base64 48`) and keep it in a secret store.
- Use strong, unique `POSTGRES_PASSWORD` and `ADMIN_PASSWORD` values; rotate the bootstrap admin password after first login.
- Terminate TLS in front of the `web` container (load balancer / ingress). The API trusts `X-Forwarded-*` headers because it is designed to run only behind that proxy. Don't expose port 5080 publicly in production, and set `API_ENABLE_DOCS=false` there.
- Keep `ANTHROPIC_API_KEY` in a secret store; the AI endpoints are rate limited, but you should also set spend limits in the Anthropic Console.
- Set `Database__SeedDemoData=false` for a real store.

## Known limitations

- Access tokens are stored in `localStorage`, so an XSS bug could steal them. The strict CSP reduces that risk; an httpOnly-cookie session or a backend-for-frontend would remove it.
- There are no refresh tokens or token revocation; sessions expire after `Jwt__AccessTokenMinutes`.
- Rate limiting is in-memory per instance; use a distributed limiter (e.g. Redis) when scaling out.
