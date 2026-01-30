/**
 * Small Node.js utility to test environment variables in Servy.
 * 
 * Usage example:
 * .\\servy-cli.exe install --name "ServyEnvTest" --path "C:\\Program Files\\nodejs\\node.exe" --params "C:\\path\\to\\nodejs-app-env-vars\\index.js" --env "var1=val1;var2=val2;"
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
fs.appendFileSync(filePath, '', "utf8")

// Append the current timestamp
fs.appendFileSync(filePath, (new Date()).toISOString() + '\n', "utf8")

const [node, _, ...args] = process.argv
fs.appendFileSync(filePath, args.join(' ') + '\n', "utf8")

// let i = 0
// for (const arg of args) {
//   fs.appendFileSync(filePath, `arg[${i}]=${arg}` + '\n', "utf8")
//   i++
// }

process.stderr.write('[stderr] abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍\n')
process.stdout.write('[stdout] abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍\n')

for (const [key, val] of Object.entries(process.env)) {
  if (!baselineEnvKeys.has(key)) {
    const line = `${key}=${val}\n`
    // Append each line to the file
    fs.appendFileSync(filePath, line, "utf8")
    // console.log(line.trim()) // optional: print to console
  }
}
fs.appendFileSync(filePath, '\n', "utf8")
// process.exit(1)

// fs.writeFileSync(filePath, (new Date()).toISOString(), 'utf-8')

// simulate some work
// await new Promise((res) => setTimeout(res, 6 * 1000))
// process.stdout.write('stdout boo!\n')
// process.stderr.write('stderr boo!\n')

// process.exit(0)

// start child process notepad.exe (Windows) detached
// const child = spawn('notepad.exe', [], {
//   detached: true,   // let child live independently
//   stdio: 'ignore'   // ignore stdio so parent can exit cleanly
// })

// // allow the child process to keep running after parent exits
// child.unref()

// const child = spawn('C:\\Users\\aelassas\\AppData\\Local\\Programs\\Python\\Python313\\python.exe', ['-u', 'E:\\dev\\servy\\src\\tests\\ctrlc.py'])
// child.unref()

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

// while (true) {
//   process.stdout.write('[stdout] App is running. Press any key to stop.\n')
//   await wait(1000)
//   process.stderr.write('[stderr] App is running. Press any key to stop.\n')
//   await wait(2000)
// }

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

// Simulate long-running app:
// process.stdout.write('App is running. Press Ctrl+C to stop.')
// setInterval(() => { }, 1000)


// keep Node alive until key press
process.stdin.setRawMode(true)
process.stdin.resume()
process.stdin.on('data', () => {
  process.stdout.write('Exiting...\n')
  console.log('Exiting...')
  // child.kill() // kill the child process
  process.exit(0)
})
