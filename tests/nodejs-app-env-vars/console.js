/**
 * Small Node.js utility to test environment variables in Servy.
 *
 * Usage example:
 * .\servy-cli.exe install --name "ServyEnvTest" --path "C:\Program Files\nodejs\node.exe" --params "C:\path\to\nodejs-app-env-vars\console.js" --env "var1=val1;var2=val2;"
 *
 * This script writes all environment variables except those in baselineEnvKeys to 'output.txt' in the script directory.
 * The variables themselves are never written to the console; only fixed marker lines go to stdout and stderr.
 */

import process from "node:process"
import { filePath, writeEnvDump, registerShutdownHandlers, keepAliveOnTty } from "./envDumpFixture.js"

// Reset output.txt and dump the non-baseline environment variables into it
writeEnvDump(filePath)

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

// Handle Ctrl+C (SIGINT) and other termination signals, before the first await
registerShutdownHandlers(filePath)

// keep Node alive until key press (interactive) or until signalled (service)
keepAliveOnTty()

const logCount = 2
while (true) {
  for (let i = 0; i < logCount; i++) {
    process.stdout.write(`[stdout] App is running log ${i + 1}/${logCount}\n`)
    await wait(1000)
    process.stderr.write(`[stderr] App is running log ${i + 1}/${logCount}\n`)
  }
  process.stdout.write(`[stdout] ${new Date().toISOString()} \n`)
  process.stdout.write('--------------------------------\n')
  await wait(2000)
}
