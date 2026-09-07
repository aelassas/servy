# Shared logging setup and teardown for the manual Python fixtures in this directory.
#
# ctrlc.py, ctrlc2.py, ctrlc_child.py and script.py each opened with the same logging
# preamble and closed with the same __main__ epilogue, copied verbatim. Every fix to one
# of those blocks had to be applied four times, and #3588 became a separate straggler
# issue because one copy was missed. Both blocks live here once instead.

import logging
import os
import sys


def setup_logging(script_file, log_name=None):
    """Configure root logging for a fixture and return the fixture's own directory.

    Lines go to <SERVY_TEST_LOG_DIR or ./logs>/<log_name, or the script's own name>.log
    and to stdout, in the format every fixture has always used. Pass __file__ as
    script_file; log_name overrides the derived file name.
    """
    script_dir = os.path.dirname(os.path.abspath(script_file))
    log_dir = os.environ.get("SERVY_TEST_LOG_DIR", os.path.join(script_dir, "logs"))
    os.makedirs(log_dir, exist_ok=True)
    log_file = os.path.join(
        log_dir,
        log_name or f"{os.path.splitext(os.path.basename(script_file))[0]}.log")

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] => %(message)s",
        datefmt="%Y%m%d %H:%M:%S",
        handlers=[
            logging.FileHandler(log_file, encoding="utf-8"),
            logging.StreamHandler(sys.stdout)
        ]
    )
    return script_dir


def run_fixture(main, stopped_message="Service stopped!"):
    """Run a fixture's main(), swallow Ctrl+C, then always log and flush the teardown line."""
    try:
        main()
    except KeyboardInterrupt:
        pass
    finally:
        logging.info(stopped_message)
        for handler in logging.root.handlers:
            handler.flush()
