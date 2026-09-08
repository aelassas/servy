/**
 * Small Node.js utility to test environment variables in Servy.
 *
 * Usage example:
 * .\servy-cli.exe install --name "ServyEnvTest" --path "C:\Program Files\nodejs\node.exe" --params "C:\path\to\nodejs-app-env-vars\index.js" --env "var1=val1;var2=val2;"
 *
 * This script writes all environment variables except those in baselineEnvKeys to 'output.txt' in the script directory.
 * The variables themselves are never written to the console; only fixed marker lines go to stdout and stderr.
 */

import process from "node:process"
import { filePath, writeEnvDump, registerShutdownHandlers, keepAliveOnTty } from "./envDumpFixture.js"

// Reset output.txt and dump the non-baseline environment variables into it
writeEnvDump(filePath)

// Handle Ctrl+C (SIGINT) and other termination signals, before the first await
registerShutdownHandlers(filePath)

// simulate some work
await new Promise((res) => setTimeout(res, 2 * 1000))
process.stdout.write('stdout boo!\n')
process.stderr.write('stderr boo!\n')

// keep Node alive until key press (interactive) or until signalled (service)
// if (!keepAliveOnTty()) {
//   setInterval(() => {}, 1 << 30)   // stay alive; SIGINT/SIGTERM handlers above do the shutdown
// }
