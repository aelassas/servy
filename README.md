[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/build.yml)
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=net48)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/net48?label=coverage&t=9)](https://app.codecov.io/gh/aelassas/servy/tree/net48)
[![docs](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

# Servy

## .NET Framework 4.8 Version

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, CPU affinity, logging, health checks, environment variables, dependencies, pre-launch and post-launch hooks, pre-stop and post-stop hooks, and parameters.

This .NET Framework 4.8 version is designed for compatibility with older Windows operating systems, from Windows 7 SP1 to Windows 11 and Windows Server.

Starting from v10.2, Servy provides enterprise-grade per-service isolation between custom accounts, authenticated encryption (DPAPI, HKDF, AES-256 + HMAC-SHA256), and automated vault ACL hardening. All releases feature signed binaries, SBOMs, and continuous vulnerability scanning. See [Security.md](https://github.com/aelassas/servy/wiki/Security) and [Architecture.md](https://github.com/aelassas/servy/wiki/Architecture) for details.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for monitoring and managing all installed services in real time.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It allows non-service apps to run in the background and start automatically at system boot, even before logon, without rewriting them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, LLMs, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for third-party software notices and licenses.
