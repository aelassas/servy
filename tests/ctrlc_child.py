# Manual test fixture: despite the name, the ROOT of the deepest tree rather than anybody's
# child. Nothing spawns it. It spawns ctrlc2.py and notepad.exe and then runs forever,
# producing ctrlc_child -> (ctrlc2 -> ctrlc) plus notepad: three levels and a GUI leaf,
# the widest case Servy's recursive termination has to reach. The actual leaf of the
# ladder is ctrlc.py, which is the one that really is a child.
#
# The child interpreter comes from PYTHON_EXE, falling back to sys.executable; the Notepad
# path comes from NOTEPAD_EXE, falling back to %SystemRoot%\System32\notepad.exe.
# Logs to logs/ctrlc_child.log next to this script; set SERVY_TEST_LOG_DIR to write elsewhere.
#
# It deliberately does not terminate the processes it spawned: the tree is meant to be left
# standing, so that what gets measured is Servy's own teardown.
#
# Install with:
# .\servy-cli.exe install --name "ServyCtrlCTree" --path "C:\path\to\python.exe" --params "C:\path\to\tests\ctrlc_child.py" --env "PYTHON_EXE=C:\path\to\python.exe"

import time
import sys
import logging
import os
import subprocess

from _fixture_logging import run_fixture, setup_logging

SCRIPT_DIR = setup_logging(__file__)

def main():
    logging.info("Service started")

    python_exe = os.environ.get("PYTHON_EXE") or sys.executable
    if not python_exe or not os.path.exists(python_exe):
        logging.error("PYTHON_EXE is not set and sys.executable is unusable; cannot spawn the child tree")
        sys.exit(1)

    notepad_exe = os.environ.get("NOTEPAD_EXE") or os.path.join(
        os.environ.get("SystemRoot", r"C:\Windows"), "System32", "notepad.exe")

    spawned = []
    try:
        # spawn child process
        py_proc = subprocess.Popen([python_exe, os.path.join(SCRIPT_DIR, "ctrlc2.py")])
        spawned.append(py_proc)
    except OSError:
        logging.exception("Failed to spawn the Python child (%s)", python_exe)
        sys.exit(1)

    try:
        notepad_proc = subprocess.Popen([notepad_exe])
        spawned.append(notepad_proc)
    except OSError:
        logging.exception("Failed to spawn the native grandchild (%s)", notepad_exe)
        for p in spawned:
            try:
                p.kill()
            except Exception:
                pass
        sys.exit(1)

    logging.info(f"Spawned PIDs: ctrlc2={py_proc.pid}, notepad={notepad_proc.pid}")

    try:
        while True:
            logging.info("(ctrlc_child) abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍")
            time.sleep(3)
    except Exception:
        logging.exception("Error in loop")

if __name__ == '__main__':
    run_fixture(main, "(ctrlc_child) Service stopped!")
