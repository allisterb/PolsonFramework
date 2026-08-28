@echo off
rem Installs the demo web app's Python dependencies into the repo's virtual environment.
rem
rem This script is run by a person, deliberately. Nothing in the build or any agent invokes it.

setlocal

rem %~dp0 is this script's own directory, so paths do not depend on the caller's working directory.
set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%..\.."
set "VENV_PIP=%REPO_ROOT%\python\Scripts\pip.exe"
set "REQUIREMENTS=%SCRIPT_DIR%requirements.txt"

if not exist "%VENV_PIP%" (
    echo error: no virtual environment at %REPO_ROOT%\python 1>&2
    echo        create it with:  python -m venv "%REPO_ROOT%\python" 1>&2
    echo        then copy the pip settings:  copy "%SCRIPT_DIR%pip.ini" "%REPO_ROOT%\python\pip.ini" 1>&2
    exit /b 1
)

if not exist "%REQUIREMENTS%" (
    echo error: %REQUIREMENTS% does not exist. 1>&2
    echo        it is generated, not written by hand. compile it first: 1>&2
    echo        "%REPO_ROOT%\python\Scripts\uv.exe" pip compile "%SCRIPT_DIR%requirements.in" --generate-hashes -o "%REQUIREMENTS%" 1>&2
    exit /b 1
)

rem Both flags are passed explicitly even though pip.ini sets only-binary, because pip.ini lives
rem inside the venv and is copied there by hand — if that step was missed, these are what still hold.
rem
rem   --require-hashes   every package, transitive ones included, must match its recorded digest
rem   --only-binary      no source distributions, so no setup.py executes during install
"%VENV_PIP%" install --require-hashes --only-binary=:all: -r "%REQUIREMENTS%"
if errorlevel 1 exit /b 1

endlocal
