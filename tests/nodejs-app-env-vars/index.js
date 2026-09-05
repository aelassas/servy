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
import fs from "node:fs"
import path from "node:path"
import { fileURLToPath } from "node:url"
import { baselineEnvKeys } from "./baselineEnvKeys.js"

// Get __dirname equivalent in ES modules
const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const filePath = path.resolve(__dirname, "output.txt")

// Clear the file first (overwrite with empty string)
fs.writeFileSync(filePath, '', "utf8")

// Append the current timestamp
fs.appendFileSync(filePath, (new Date()).toISOString() + '\n', "utf8")

const [, , ...args] = process.argv
fs.appendFileSync(filePath, args.join(' ') + '\n', "utf8")

process.stderr.write('[stderr] abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍\n')
process.stdout.write('[stdout] abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍\n')

for (const [key, val] of Object.entries(process.env)) {
  if (!baselineEnvKeys.has(key)) {
    const line = `${key}=${val}\n`
    // Append each line to the file
    fs.appendFileSync(filePath, line, "utf8")
  }
}
fs.appendFileSync(filePath, '\n', "utf8")

// simulate some work
await new Promise((res) => setTimeout(res, 2 * 1000))
process.stdout.write('stdout boo!\n')
process.stderr.write('stderr boo!\n')

// Handle Ctrl+C (SIGINT) and other termination signals
for (const signal of ['SIGINT', 'SIGTERM', 'SIGQUIT']) {
  process.once(signal, () => {
    const msg = `Received ${signal} - shutting down gracefully...\n`
    process.stdout.write(msg)
    fs.appendFileSync(filePath, msg, "utf8")
    // Perform cleanup here (e.g., close DB connections, stop servers, etc.)
    process.exit(0)
  })
}

// keep Node alive until key press (interactive) or until signalled (service)
if (process.stdin.isTTY) {
  process.stdin.setRawMode(true)
  process.stdin.resume()
  process.stdin.on('data', () => {
    process.stdout.write('Exiting...\n')
    process.exit(0)
  })
} else {
  setInterval(() => {}, 1 << 30)   // stay alive; SIGINT/SIGTERM handlers above do the shutdown
}
