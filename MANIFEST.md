## Why Servy?

Whenever I needed to run an app as a Windows service, I usually relied on tools like sc.exe, NSSM, or WinSW. They get the job done, but in real projects, their limitations quickly became frustrating.

sc.exe only works with applications that are specifically designed to run as Windows services. It also always defaults to `C:\Windows\System32` as the working directory, which can break apps that rely on relative paths or local configuration files. NSSM is lightweight, but it doesn't offer monitoring, health checks, pre-launch and post-launch hooks, or a fully-featured user interface. WinSW is configurable, but it's XML-based, not very user-friendly for quick setups, and also lacks a proper UI.

After running into these issues too many times, I decided to build my own tool.

**The goals**

I wanted a solution that was easy to use, with a clean desktop app, but also scriptable through CLI and PowerShell for automation and CI/CD pipelines. It needed to be flexible enough to run any type of app—Node.js, Python, .NET, scripts, and more. It also had to be robust, with built-in logging, health checks, recovery options, pre-launch and post-launch hooks, CPU and RAM monitoring, and restart policies. Finally, it had to work across a wide range of Windows versions, from Windows 7 to Windows 11, including Server editions.

**The result**

The result is **Servy**, a tool that lets you run any app as a native Windows service with full control over the working directory, startup type, process priority, logging, health checks, environment variables, dependencies, hooks, and parameters. Servy is designed to be a full-featured alternative to NSSM, WinSW, and FireDaemon Pro.

Servy offers a desktop app, a CLI, and a PowerShell module that let you create, configure, and manage Windows services interactively or through scripts and CI/CD pipelines. It also includes a Manager app for easily monitoring and managing all installed services in real time.

## Points of Interest

While building Servy, I spent quite a bit of time working directly with the Win32 API to handle various system-level operations such as managing processes, installing services, checking service states, and dealing with permissions. It was challenging at first, but it gave me a deeper understanding of how Windows manages background applications under the hood.

Publishing Servy on GitHub has been a huge help in improving the tool. It allowed me to find and fix bugs more quickly and also add new features that users requested, like the ability to expand environment variables. Most of the bugs were straightforward to reproduce and fix, but one issue took much longer to solve. When stopping a service, sending a `Ctrl+C` signal to the child process caused the stdout and stderr pipes to be lost. This meant the service could no longer receive any messages from the running application. It took some time and careful debugging to understand what was going on and find the right solution, but in the end, the bug was fixed and everything worked as expected. The process also gave me a better understanding of how Windows handles process communication. After a lot of effort, I was able to fix all the reported bugs and implement all the requested features, making Servy more stable, reliable, and user-friendly. Sharing the project on GitHub also made it easier to get feedback and suggestions, which helped guide development and prioritize improvements.

I also used PowerShell extensively to automate repetitive tasks like building, testing, CI/CD pipelines, and publishing new versions.

Most of Servy's automation is powered by GitHub Actions, which runs automatically whenever I create a new release. With the GitHub Actions workflows I've set up, every time I publish a new release, the build is automatically pushed to WinGet, Chocolatey, and Scoop, and the version number is bumped for the next cycle. Setting this up took a fair amount of trial and error, but once everything started working, it completely changed the release process. Now maintaining and releasing Servy is almost effortless. Everything happens automatically, which saves a lot of time and makes it easier to focus on improving the tool instead of worrying about builds or deployments. Now the whole process of maintaining and releasing Servy is almost completely automatic. New versions are built, tested, and published with very little manual work, which saves a lot of time and makes updates much easier to manage.

That's it! I hope you find Servy useful and consider using it in your own projects. Feedback and contributions are welcome.
