import time
import sys

# os.path.expandvars("%PYTHON_EXE%")
# E:\dev\servy\src\tests\stdout.py
# E:\dev\servy\python_stdout.txt
# E:\dev\servy\python_stderr.txt

try:
    while True:
        print("stdout > Hello, World!", file=sys.stdout)
        time.sleep(0.5) 
        print("stderr > Hello, World!", file=sys.stderr)
        time.sleep(3)  # Wait 5 seconds between prints
except KeyboardInterrupt:
    # This catches Ctrl+C
    print("\nGoodbye! Script terminated.")
    sys.exit(0)
