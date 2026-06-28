import time
import sys

try:
    while True:
        print("Hello, World!")
        time.sleep(5)  # Wait 5 seconds between prints
except KeyboardInterrupt:
    # This catches Ctrl+C
    print("\nGoodbye! Script terminated.")
    sys.exit(0)
