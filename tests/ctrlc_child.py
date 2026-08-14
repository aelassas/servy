import time
import sys
import logging
import os
import subprocess

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LOG_DIR = os.environ.get("SERVY_TEST_LOG_DIR", os.path.join(SCRIPT_DIR, "logs"))
os.makedirs(LOG_DIR, exist_ok=True)
LOG_FILE = os.path.join(LOG_DIR, f"{os.path.splitext(os.path.basename(__file__))[0]}.log")

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] => %(message)s",
    datefmt="%Y%m%d %H:%M:%S",
    handlers=[
        logging.FileHandler(LOG_FILE, encoding="utf-8"),
        logging.StreamHandler(sys.stdout)
    ]
)

def main():
    logging.info("Service started")

    python_exe = os.environ.get("PYTHON_EXE") or sys.executable
    if not python_exe or not os.path.exists(python_exe):
        logging.error("PYTHON_EXE is not set and sys.executable is unusable; cannot spawn the child tree")
        sys.exit(1)

    try:
        # spawn child process
        py_proc = subprocess.Popen([python_exe, os.path.join(SCRIPT_DIR, "ctrlc2.py")])
        notepad_proc = subprocess.Popen([r"C:\Windows\System32\notepad.exe"])
    except OSError:
        logging.exception("Failed to spawn the child process tree")
        sys.exit(1)

    logging.info(f"Spawned PIDs: ctrlc2={py_proc.pid}, notepad={notepad_proc.pid}")

    try:
        while True:
            logging.info("(ctrlc_child) abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍")
            time.sleep(3)
    except Exception:
        logging.exception("Error in loop")

if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        pass
    finally:
        logging.info("(ctrlc_child) Service stopped!")
        for handler in logging.root.handlers:
            handler.flush()
