import time
import sys

try:
    while True:
        print("stdout > Hello, World!", file=sys.stdout, flush=True)  
        time.sleep(0.5) 
        print("stderr > Hello, World!", file=sys.stderr, flush=True)
        time.sleep(3)  # Wait 3 seconds between prints
except KeyboardInterrupt:
    # This catches Ctrl+C
    print("\nGoodbye! Script terminated.")
    sys.exit(0)
