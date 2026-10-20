[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/build.yml)
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg?branch=main)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![codecov](https://img.shields.io/codecov/c/github/aelassas/servy/main?label=coverage&t=9)](https://codecov.io/gh/aelassas/servy)
[![docs](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

# Servy

Servy lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, CPU affinity, logging, health checks, environment variables, dependencies, pre-launch and post-launch hooks, pre-stop and post-stop hooks, and parameters.

Starting from v10.2, Servy provides enterprise-grade per-service isolation between custom accounts, authenticated encryption, and automated ACL hardening. All releases feature signed binaries, SBOMs, and continuous vulnerability scanning. See [Security](https://github.com/aelassas/servy/wiki/Security) and [Architecture](https://github.com/aelassas/servy/wiki/Architecture) for details.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for monitoring and managing all installed services in real time.

Servy continuously monitors your app, restarting it automatically if it crashes, hangs, or stops. It allows non-service apps to run in the background and start automatically at system boot, even before logon, without rewriting them as services. Use it to run Node.js, Python, .NET, Java, Go, Rust, PHP, or Ruby applications; keep web servers, LLMs, background workers, sync tools, or daemons alive after reboots; and automate task runners, schedulers, or scripts in production with built-in health checks, logging, and restart policies.

## Support

Servy is free and open source. If it helped you, saved you time, or powered your production workflows, consider supporting the project via [GitHub Sponsors](https://github.com/sponsors/aelassas), [PayPal](https://www.paypal.me/aelassaspp), [Liberapay](https://liberapay.com/aelassas/), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas) to keep it active, independent, and free for everyone.

## Stats for Nerds

[![LoC - Prod](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-prod.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Tests](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-tests.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![LoC - Total](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/loc-total.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml)
[![GitHub Downloads](https://img.shields.io/github/downloads/aelassas/servy/total?t=3)](https://servy-win.github.io/downloads)

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt). It ships no third-party executables: the processes locking a file are found through the Windows Restart Manager API.

## Acknowledgments

Thanks to [SignPath](https://signpath.io/?utm_source=foundation&utm_medium=github&utm_campaign=servy) and the [SignPath Foundation](https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for providing free code signing, to [Christophe Rogiers](https://github.com/Christophe-Rogiers) for his valuable contributions, and to everyone who tested Servy, reported issues, or suggested improvements on GitHub and Reddit.

<p>
  <a href="https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy">
    <img alt="SignPath Foundation" src="https://aelassas.github.io/content/signpath.png?v=2" width="54" height="51">
  </a>
</p>
