/**
 * Shared prologue for the nodejs-app-env-vars fixtures.
 *
 * console.js and index.js differ only in their tail; everything before it - the
 * output.txt reset and environment dump, the termination handlers and the
 * interactive keep-alive - lives here so a fix lands once instead of twice.
 * Extracted for #6259, mirroring baselineEnvKeys.js.
 */

import process from "node:process"
import fs from "node:fs"
import path from "node:path"
import { fileURLToPath } from "node:url"
import { baselineEnvKeys } from "./baselineEnvKeys.js"

// Get __dirname equivalent in ES modules
const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

// 'output.txt' in the fixture directory, which is this module's own directory
export const filePath = path.resolve(__dirname, "output.txt")

/**
 * Resets output.txt, then writes the timestamp, the command-line arguments and
 * every environment variable that is not in baselineEnvKeys. The variables
 * themselves never reach the console; only the two fixed marker lines do.
 */
export function writeEnvDump(filePath) {
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
}

/**
 * Handle Ctrl+C (SIGINT) and other termination signals. Call this before any
 * await: until it has run the process has no listener and Node terminates on
 * the signal without the graceful-shutdown line (#5814).
 */
export function registerShutdownHandlers(filePath) {
  for (const signal of ['SIGINT', 'SIGTERM', 'SIGQUIT']) {
    process.once(signal, () => {
      const msg = `Received ${signal} - shutting down gracefully...\n`
      process.stdout.write(msg)
      fs.appendFileSync(filePath, msg, "utf8")
      // Perform cleanup here (e.g., close DB connections, stop servers, etc.)
      process.exit(0)
    })
  }
}

/**
 * Keeps Node alive until a key press when running interactively. Returns true
 * when the keypress handler was installed, false when there is no TTY and the
 * caller has to keep the process alive itself.
 */
export function keepAliveOnTty() {
  if (!process.stdin.isTTY) {
    return false
  }

  process.stdin.setRawMode(true)
  process.stdin.resume()
  process.stdin.on('data', () => {
    process.stdout.write('Exiting...\n')
    process.exit(0)
  })
  return true
}
