# NCV.ISPSession

A Redis-backed session and application state library for ASP.NET Core 8+.

Drop-in alternative to the built-in distributed session, with **encrypted Redis keys**, **per-key application state with expiration events**, **automatic compression**, and a **lightweight optimistic-concurrency strategy** that only writes when state actually changes.

```shell
dotnet add package NCV.ISPSession
```

MIT-licensed. No telemetry. No phone-home. Source on [GitHub](https://github.com/egbertn/ispsession).

---

## Why NCV.ISPSession instead of `Microsoft.Extensions.Caching.StackExchangeRedis`?

| Feature                                       | NCV.ISPSession | Microsoft.Extensions.Caching.StackExchangeRedis |
| --------------------------------------------- | :------------: | :---------------------------------------------: |
| Distributed session backed by Redis           |       ✅       |                       ✅                        |
| AES-encrypted Redis keys                      |       ✅       |                       ❌                        |
| Automatic Brotli compression of session blob  |       ✅       |                       ❌                        |
| Application-wide state (shared across nodes)  |       ✅       |                       ❌                        |
| Per-key expiration callbacks (keyspace `Ex`)  |       ✅       |                       ❌                        |
| Affinity via cookie, header **or** IP address |       ✅       |                       ❌                        |
| Optimistic concurrency — write-on-change only |       ✅       |                       ❌                        |
| Bundled GET/SET/DEL per ASP.NET request scope |       ✅       |                       ❌                        |

If you only need a plain key/value cache, the Microsoft package is fine. If you want session **and** application state with the features above, this library is built for that.

---

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Redis 4.0 or later

### Install

```shell
dotnet add package NCV.ISPSession
```

### Configure

Add a Redis connection string in `appsettings.json` (or `appsettings.Development.json`):

```json
{
  "ConnectionStrings": {
    "IspSession": "localhost:6379"
  }
}
```

Wire it up in `Program.cs` — see the [demo project](https://github.com/egbertn/ispsession/tree/master/Demo) for a complete example.

### Verify

Start your app and call any endpoint that uses session state. Hitting the same endpoint again should increment your counter:

```shell
curl https://localhost:7058/counterwithapp
```

```json
{
  "sessionCounter": 1,
  "isNewSession": true,
  "isExpiredSession": true,
  "sessionId": "39ecbd08-662e-4123-95e8-fc559446c73d",
  "appCounter": 2
}
```

### Optional: enable expiration events

To use the per-key expiration callbacks, Redis needs keyspace notifications. The library tries to set this dynamically, but the durable fix is to add this to `redis.conf`:

```text
notify-keyspace-events Ex
```

---

## How it works

- **SessionState** — all session variables are serialized to a single blob, optionally Brotli-compressed, AES-encrypted, and stored under one Redis key per session. Reads and writes are bundled per ASP.NET request scope; nothing hits Redis if you only read.
- **ApplicationState** — opt-in. Application-wide variables are stored as individual Redis keys so each one can have its own expiration and fire a callback when it expires.
- **Affinity** — session identity can be carried via cookie, custom header, or (with obfuscation for GDPR compliance) the client IP address. Useful for REST APIs where cookies aren't always available.

See [SessionState.cs](SessionState.cs) and [ApplicationState.cs](ApplicationState.cs) for the public surface.

---

## License

MIT — see [LICENSE.txt](LICENSE.txt). Free for any use, including commercial.

If the library saves you time, [buy me a coffee](https://buymeacoffee.com/egbert) ☕.

Copyright © 2024–2026 Nierop Computer Vision.
