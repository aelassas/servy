from datetime import datetime
import sys
import logging
import os

# Force stdout to UTF-8
# sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

LOG_DIR = r"C:\test\logs"
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, "test_pre.log")

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
        current_datetime = datetime.now().strftime("%Y%m%d %H:%M:%S.%f")[:-3]
        logging.info(f"{current_datetime} > abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍")
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
