/**
 * Small Node.js utility to test environment variables in Servy.
 * 
 * Usage example:
 * .\servy-cli.exe install --name "ServyEnvTest" --path "C:\Program Files\nodejs\node.exe" --params "C:\path\to\nodejs-app-env-vars\index.js" --env "var1=val1;var2=val2;"
 * 
 * This script writes all environment variables except those in baselineEnvKeys to 'output.txt' in the script directory,
 * and logs them to the console.
 */

import process from "node:process"
import { spawn } from "node:child_process"
import fs from "node:fs"
import path from "node:path"
import { fileURLToPath } from "node:url"
import { baselineEnvKeys } from "./baselineEnvKeys.js"

// Get __dirname equivalent in ES modules
const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const filePath = path.resolve(__dirname, "output.txt")

// Clear the file first (overwrite with empty string)
fs.writeFileSync(filePath, "", "utf8")

for (const [key, val] of Object.entries(process.env)) {
  if (!baselineEnvKeys.has(key)) {
    const line = `${key}=${val}\n`
    // Append each line to the file
    fs.appendFileSync(filePath, line, "utf8")
    console.log(line.trim()) // optional: print to console
  }
}
// process.exit(1)

// simulate some work
// await new Promise((res) => setTimeout(res, 6 * 1000))
process.stderr.write('boo!')

// process.exit(0)

// start child process notepad.exe (Windows) detached
const child = spawn('notepad.exe', [], {
  detached: true,   // let child live independently
  stdio: 'ignore'   // ignore stdio so parent can exit cleanly
})

// allow the child process to keep running after parent exits
child.unref()

// keep Node alive until key press
process.stdin.setRawMode(true)
process.stdin.resume()
process.stdin.on('data', () => {
  console.log('Exiting...')
  child.kill() // kill the child process
  process.exit(0)
})
