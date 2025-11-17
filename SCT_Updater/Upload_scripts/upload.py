# build_and_upload.py
import sys
import argparse
import subprocess
import shutil
from pathlib import Path

try:
    from loguru import logger
except ImportError:
    print("Error: 'loguru' is not installed. Please run: pip install loguru")
    sys.exit(1)

# Настройка логгера
logger.remove()
logger.add(sys.stderr, level="INFO", format="{time:HH:mm:ss} | <level>{level: <8}</level> | {message}", colorize=True)


def run_command(command_list):
    """
    Helper to run a subprocess and stream its output live.
    """
    logger.info(f"Running command: {' '.join(command_list)}")
    try:
        subprocess.run(
            command_list,
            check=True,
            encoding='utf-8'
        )
        return True
    except subprocess.CalledProcessError as e:
        logger.error(f"Command failed with exit code {e.returncode}.")
        return False
    except FileNotFoundError:
        logger.error(f"Command not found: {command_list[0]}. Is it installed and in PATH?")
        return False


def main():
    # 1. Парсим аргументы
    parser = argparse.ArgumentParser(
        description="Build, package, and upload an application.",
        formatter_class=argparse.RawTextHelpFormatter
    )
    # ... (все аргументы остаются без изменений) ...
    parser.add_argument(
        "mode",
        choices=["zip", "files"],
        help="Manifest creation mode (e.g., 'files')."
    )
    parser.add_argument(
        "path",
        help="Path to the target dist directory (e.g., 'build/dist/ReportGenerator')."
    )
    parser.add_argument(
        "product_id",
        help="Unique product identifier (e.g., 'report_generator')."
    )
    parser.add_argument(
        "version",
        help="Version string (e.g., '1.0.0')."
    )
    parser.add_argument(
        "--upload",
        action="store_true",
        help="Automatically upload to Nextcloud after build (skips y/n prompt)."
    )

    args = parser.parse_args()

    # --- Вывод путей ---
    dist_path = Path(args.path)
    app_name = dist_path.name
    spec_file = f"{app_name}.spec"
    dist_root = dist_path.parent
    build_root = dist_root.parent / "build"

    # --- НОВЫЙ БЛОК: Находим pyinstaller.exe ---
    # Мы должны вызвать .exe напрямую, а не 'python -m PyInstaller'
    # 'sys.executable' указывает на .venv/Scripts/python.exe
    py_exe_path = Path(sys.executable)
    scripts_dir = py_exe_path.parent


    # --- Шаг 3: Запуск create_manifest.py (без изменений) ---
    logger.info("--- Running create_manifest.py ---")

    manifest_command = [
        "python", "create_manifest.py",
        args.mode,
        args.path,
        args.product_id,
        args.version
    ]

    if args.upload:
        logger.info("Adding --upload-now flag for create_manifest.")
        manifest_command.append("--upload-now")

    if not run_command(manifest_command):
        logger.error("Manifest creation failed. Aborting.")
        sys.exit(1)

    logger.success("Orchestration complete.")


if __name__ == "__main__":
    main()