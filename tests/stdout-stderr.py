import time
import sys

# C:\Users\aelassas\AppData\Local\Programs\Python\Python313\python.exe
# E:\dev\servy\src\tests\stdout-stderr.py
# E:\dev\servy\python_stdout_stderr.txt
# E:\dev\servy\python_stdout_stderr.txt

ONE_MEGABYTE = 1024 * 1024
data = b'a' * ONE_MEGABYTE
data_err = b'b' * ONE_MEGABYTE

while True:
    try:
        # Write 1MB to stdout
        sys.stdout.buffer.write(data)
        sys.stdout.flush()
        print("Wrote 1MB to stdout", file=sys.stdout)

        # Write 1MB to stderr
        sys.stderr.buffer.write(data_err)
        sys.stderr.flush()
        print("Wrote 1MB to stderr", file=sys.stderr)

    except Exception as e:
        print(f"An error occurred: {e}", file=sys.stderr)

    time.sleep(5)
