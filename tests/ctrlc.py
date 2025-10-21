from datetime import datetime
import time
import sys
import logging
import os

LOG_DIR = r"C:\test\logs"
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, "test.log")

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] => %(message)s",
    handlers=[
        logging.FileHandler(LOG_FILE, encoding="utf-8"),
        logging.StreamHandler(sys.stdout)
    ]
)


def main():
    logging.info("Service started")
    try:
        while True:
            current_datetime = datetime.now().strftime("%Y%m%d %H:%M:%S.%f")[:-3]
            logging.info(current_datetime)
    
            time.sleep(3)
    except Exception as e:
        logging.exception(f"Error in loop: {e}")


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        pass
    finally:
        logging.info("Service stopped!")
        for handler in logging.root.handlers:
          handler.flush()
