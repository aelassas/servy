from datetime import datetime
import time
import sys
import logging
import os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LOG_DIR = os.environ.get("SERVY_TEST_LOG_DIR", os.path.join(SCRIPT_DIR, "logs"))
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
            logging.info(f"{current_datetime} > (ctrlc) abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍")
            time.sleep(3)
    except Exception:
        logging.exception("Error in loop")

if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        pass
    finally:
        logging.info("(ctrlc) Service stopped!")
        for handler in logging.root.handlers:
            handler.flush()
