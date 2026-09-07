# Manual test fixture: the leaf of the Ctrl+C ladder. Runs forever, spawns nothing, and
# logs a heartbeat line every 3 seconds plus a "Service stopped!" line on teardown, so a
# stop request can be observed end to end on a single process.
#
# Logs to logs/ctrlc.log next to this script; set SERVY_TEST_LOG_DIR to write elsewhere.
#
# Install with:
# .\servy-cli.exe install --name "ServyCtrlC" --path "C:\path\to\python.exe" --params "C:\path\to\tests\ctrlc.py"
#
# Ladder: ctrlc_child.py spawns ctrlc2.py and notepad.exe; ctrlc2.py spawns this script.

import time
import logging

from _fixture_logging import run_fixture, setup_logging

setup_logging(__file__)

def main():
    logging.info("Service started")
    try:
        while True:
            logging.info("(ctrlc) abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍")
            time.sleep(3)
    except Exception:
        logging.exception("Error in loop")

if __name__ == '__main__':
    run_fixture(main, "(ctrlc) Service stopped!")
