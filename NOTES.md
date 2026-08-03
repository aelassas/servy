## Why Servy?

I've been using NSSM for a while, but I kept facing the same issues again and again, so I ended up building my own tool and putting it on GitHub.

While NSSM is a lightweight Windows service wrapper, it hasn't seen an update in over a decade. It struggles with complex process tree cleanup and lacks essential features like pre/post start/stop hooks, date-based log rotation, CPU/RAM monitoring, CPU affinity, email notifications, heartbeat ping URLs, and advanced recovery options. That's why I ended up building Servy to fix these issues and add the missing features I needed.

## Points of Interest

While building Servy, I spent quite a bit of time working directly with the Win32 API to handle low-level system-level operations such as managing processes, installing services, checking service states, and dealing with permissions. While challenging, this deep dive gave me a much clearer understanding of how the Windows Service Control Manager coordinates background architectures under the hood.

Publishing Servy on GitHub has been a huge help in improving the tool and transformed its development. It allowed me to find and fix bugs more quickly and also add new features that users requested, like the ability to expand environment variables. Most of the issues were straightforward to reproduce and fix, but one issue took much longer to solve. When stopping a service, sending a `Ctrl+C` signal to the child process caused the `stdout` and `stderr` pipes to be lost. This meant the service could no longer receive any messages from the running application. It took some time and careful debugging to understand what was going on and find the right solution, but in the end, the bug was fixed and everything worked as expected. The process also gave me a better understanding of how Windows handles process communication. After a lot of effort, I was able to fix all the reported issues and implement all the requested features, making Servy more stable, reliable, and user-friendly. Sharing the project on GitHub also made it easier to get feedback and suggestions, which helped guide development and prioritize improvements.

Special thanks to the GitHub contributors who helped design Servy v7.9+ to be invisible yet invincible in terms of security. See the [Security Model](https://github.com/aelassas/servy/wiki/Security) for more details.

Posting Servy on Reddit also helped a lot in improving the project. Sharing it with the community meant that people could test it, give feedback, and suggest new features. Many of the ideas that made it into Servy came directly from users who tried it and pointed out what could be better. This kind of real-world input was really valuable because it showed me how people were actually using the tool, not just how I imagined it. It also helped me find small bugs or usability issues that I hadn't noticed before. Overall, putting Servy out there made it stronger, more polished, and more useful for everyone.

I also used PowerShell extensively to automate repetitive tasks like building, testing, CI/CD pipelines, and publishing new versions.

Most of Servy's automation is powered by GitHub Actions, which runs automatically whenever I create a new release. With the GitHub Actions workflows I've set up, every time I publish a new release, the build is automatically pushed to WinGet, Chocolatey, and Scoop, and the version number is bumped for the next cycle. Setting this up took a fair amount of trial and error, but once everything started working, it completely changed the release process. Now maintaining and releasing Servy is almost effortless. Everything happens automatically, which saves a lot of time and makes it easier to focus on improving the tool instead of worrying about builds or deployments. Now the whole process of maintaining and releasing Servy is almost completely automatic. New versions are built, tested, and published with very little manual work, which saves a lot of time and makes updates much easier to manage.

The digital signing integration took some time and effort to set up, as it required writing the entire build pipeline to automate code signing using SignPath and GitHub Actions but it was a critical step to ensure that the final binaries and installers are verified, trusted, and safe for production environments. For reference, here are the build pipelines:
* `main` branch: [publish.yml](https://github.com/aelassas/servy/blob/main/.github/workflows/publish.yml)
* `net48` branch: [publish.yml](https://github.com/aelassas/servy/blob/net48/.github/workflows/publish.yml)

Thanks to [SignPath](https://signpath.io/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for providing a free code signing service, and to the [SignPath Foundation](https://signpath.org/?utm_source=foundation&utm_medium=github&utm_campaign=servy) for supplying a free code signing certificate.

Thanks to [JetBrains](https://www.jetbrains.com/) for providing an [open-source license](https://www.jetbrains.com/community/opensource/) for their tools. Their software made it much easier to profile, debug, and optimize Servy, helping improve its performance and stability. Having access to these professional tools really made a difference during development and saved a lot of time.

Once again, special thanks to everyone who tested Servy, reported issues, and suggested improvements on GitHub and Reddit. Your feedback and contributions have shaped the project and improved it with every release.

That's it! I hope Servy saves you the troubleshooting time it was built to solve. If you end up using it in your own projects, feedback and contributions are always welcome.
