[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/build.yml) [![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/test.yml) [![Coverage Status](https://coveralls.io/repos/github/aelassas/servy/badge.svg?branch=main)](https://coveralls.io/github/aelassas/servy?branch=main) [![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) [![](https://img.shields.io/badge/docs-wiki-brightgreen)](https://github.com/aelassas/servy/wiki)

<!--
[![build](https://github.com/aelassas/servy/actions/workflows/build.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/build.yml) 
[![test](https://github.com/aelassas/servy/actions/workflows/test.yml/badge.svg)](https://github.com/aelassas/servy/actions/workflows/test.yml)
[![Build Status](https://aelassas.visualstudio.com/servy/_apis/build/status%2Faelassas.servy?branchName=main)](https://aelassas.visualstudio.com/servy/_build/latest?definitionId=4&branchName=main) 
[![](https://raw.githubusercontent.com/aelassas/servy/refs/heads/loc/badge.svg)](https://github.com/aelassas/servy/actions/workflows/loc.yml) 
[![codecov](https://codecov.io/gh/aelassas/servy/graph/badge.svg?token=26WZX2V4BG)](https://codecov.io/gh/aelassas/servy)
[![Coverage Status](https://coveralls.io/repos/github/aelassas/servy/badge.svg?branch=main)](https://coveralls.io/github/aelassas/servy?branch=main)
-->
<p align="center">
  <img src="https://servy-win.github.io/servy.png?d=7" alt="Servy" />
</p>
<p align="center">
  <a href="https://www.youtube.com/watch?v=JpmzZEJd4f0" target="_blank">
    <img src="https://img.shields.io/badge/Watch%20Demo-FF0033?style=for-the-badge&logo=youtube" alt="Watch Demo on YouTube" />
  </a>
</p>

# Servy

Servy lets you run any app as a Windows service with full control over working directory, startup type, logging, health checks, and parameters. A fully managed alternative to NSSM.

Servy offers both a GUI and a Command-Line Interface (CLI), enabling you to create, configure, and manage Windows services interactively or automate these tasks in scripts and CI/CD pipelines.

Servy solves a common limitation of Windows services by allowing you to set a custom working directory. The built-in `sc` tool only works with applications specifically designed to run as Windows services and always uses `C:\Windows\System32` with no way to change it. This can break apps that depend on relative paths, configuration files, or local assets. Servy lets you run any app as a service and define the startup directory explicitly, ensuring it behaves exactly as if launched from a shortcut or command prompt.

Servy is perfect for keeping non-service apps running in the background without rewriting them as services. Use it to run Node.js, Python, or .NET apps; keep web servers, sync tools, or daemons alive after reboots; and automate task runners or scripts in production with built-in health checks, and restart policies.

## Quick Links
* [Download](https://github.com/aelassas/servy/releases/latest)
* [Installation Guide](https://github.com/aelassas/servy/wiki/Installation-Guide)
* [Usage](https://github.com/aelassas/servy/wiki/Usage)
* [CLI](https://github.com/aelassas/servy/wiki/Servy-CLI)
* [Architecture](https://github.com/aelassas/servy/wiki/Architecture)
* [Troubleshooting](https://github.com/aelassas/servy/wiki/Troubleshooting)
* [FAQ](https://github.com/aelassas/servy/wiki/FAQ)

## Features

* Clean, simple UI
* CLI for full scripting and automated deployments
* Run any executable as a Windows service
* Set service name, description, startup type, priority, working directory, and parameters
* Redirect stdout/stderr to log files with automatic size-based rotation
* Prevent orphaned/zombie processes with improved lifecycle management and ensuring resource cleanup
* Health checks and automatic service recovery
* Monitor and manage services in real-time
* Compatible with Windows 7–11 x64 and Windows Server editions

## Roadmap

- [x] Windows Service creation via GUI
- [x] Logging stdout/stderr with size-based rotation
- [x] Service monitoring and heartbeat checks
- [x] Automatic restart on failure
- [x] CLI for full scripting and automated deployments
- [ ] Support environment variables for child processes ([#1](https://github.com/aelassas/servy/issues/1))
- [ ] Add "Log on as" configuration for Windows service
- [ ] Support service dependencies

## Support & Contributing

If this project helped you, saved you time, or inspired you in any way, please consider supporting its future growth and maintenance. You can show your support by starring the repository (it helps increase visibility and shows your appreciation), sharing the project (recommend it to colleagues, communities, or on social media), or making a donation (if you'd like to financially support the development) via [GitHub Sponsors](https://github.com/sponsors/aelassas) (one-time or monthly), [PayPal](https://www.paypal.me/aelassaspp), or [Buy Me a Coffee](https://www.buymeacoffee.com/aelassas). 

Open-source software requires time, effort, and resources to maintain—your support helps keep this project alive, up-to-date, and accessible to everyone. Every contribution, big or small, makes a difference and motivates continued work on features, bug fixes, and new ideas.

If you have suggestions, issues, or want to contribute, feel free to [open an issue](https://github.com/aelassas/servy/issues) or [submit pull request](https://github.com/aelassas/servy/pulls).

## License

Servy is [MIT licensed](https://github.com/aelassas/servy/blob/main/LICENSE.txt).

