import time
import sys

# C:\Users\aelassas\AppData\Local\Programs\Python\Python313\python.exe
# E:\dev\servy\src\tests\stdout.py
# E:\dev\servy\python_stdout.txt
# E:\dev\servy\python_stderr.txt

try:
    while True:
        print("Hello, World!")
        time.sleep(5)  # Wait 5 seconds between prints
except KeyboardInterrupt:
    # This catches Ctrl+C
    print("\nGoodbye! Script terminated.")
    sys.exit(0)
