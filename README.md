[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/build.yml)
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/main?label=coverage&t=9)](https://codecov.io/gh/aelassas/servy)
[![docs](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

## Servy

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, CPU affinity, logging, health checks, environment variables with expansion, dependencies, pre-launch and post-launch hooks, pre-stop and post-stop hooks, and parameters. Services can run as Local System, a local or domain account, or a Group Managed Service Account (gMSA).

Servy keeps your apps running with advanced recovery options. Health checks and automatic recovery are optional and run only when enabled and configured. When they are, Servy monitors each process with health checks and heartbeat pings to services such as healthchecks.io, and restarts it according to a configurable recovery policy when it crashes, hangs, or stops. It can send failure alerts through Windows notifications, email, and webhook-based channels. It also stops processes cleanly with `Ctrl+C` or a close-window signal, forwards the stop request to child processes, and terminates the whole process tree so that no orphaned processes remain.

Servy captures stdout and stderr to log files with size-based or date-based rotation, so logs never grow without limit. Servy Manager adds real-time CPU and RAM usage graphs, live stdout and stderr tailing with search, and a dependency tree showing the status of each service.

Starting from v10.2, Servy provides enterprise-grade per-service isolation between custom accounts, authenticated encryption, and automated ACL hardening. All releases feature signed binaries, SBOMs, and continuous vulnerability scanning. See [Security](https://github.com/aelassas/servy/wiki/Security) and [Architecture](https://github.com/aelassas/servy/wiki/Architecture) for details.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. Service configurations can be exported and imported for backup and migration. Servy runs on Windows 10, Windows 11, and Windows Server 2016 or later on x64 and ARM64, and a .NET Framework 4.8 build also supports Windows 7 SP1 and Windows Server 2008 R2.

## Quick Links

* [Download](https://github.com/aelassas/servy/releases/latest)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Getting Started](https://github.com/aelassas/servy/wiki/Overview)
* [Full Documentation](https://github.com/aelassas/servy/wiki)

## Support

Servy is free and open source. If it helped you, saved you time, or powered your production workflows, consider supporting the project via [PayPal](https://www.paypal.me/aelassaspp), [GitHub Sponsors](https://github.com/sponsors/aelassas), [Liberapay](https://liberapay.com/aelassas/), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas) to keep it active, independent, and free for everyone.

## Stats for Nerds

[![LoC - Prod](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-prod.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Tests](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-tests.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Total](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-total.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![GitHub Downloads](https://img.shields.io/github/downloads/aelassas/servy/total?t=3)](https://servy-win.github.io/downloads)

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt). It ships no third-party binaries, external tools, or non-permissive code.

## Acknowledgments

Thanks to [SignPath](https://signpath.io/?utm_source=foundation&utm_medium=github&utm_campaign=servy) and the [SignPath Foundation](https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for providing free code signing, to [Christophe Rogiers](https://github.com/Christophe-Rogiers) for his valuable contributions, and to everyone who tested Servy, reported issues, or suggested improvements.

<p>
  <a href="https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy">
    <img alt="SignPath Foundation" src="https://github.com/user-attachments/assets/f6d0fb02-c2bf-4915-bc7e-dd3273a7927e" height="51">
  </a>
</p>
