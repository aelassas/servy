import logging

from _fixture_logging import run_fixture, setup_logging

setup_logging(__file__, "test_pre.log")

def main():
    logging.info("Service started")
    try:
        logging.info("abcd&é секунды 同时也感觉没有想象的那么好用 - äöü ß ñ © ™ 🌍")
    except Exception:
        logging.exception("Error logging the multi-encoding test line")

if __name__ == '__main__':
    run_fixture(main)
