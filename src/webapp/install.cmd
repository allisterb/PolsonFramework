@echo off
rem Installs the demo web app's Python dependencies into the repo's virtual environment.
rem
rem This script is run by a person, deliberately. Nothing in the build or any agent invokes it.

setlocal

rem %~dp0 is this script's own directory, so paths do not depend on the caller's working directory.
set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%..\.."
rem pip reads its settings from the venv root, under a different name on each platform: pip.ini here,
rem pip.conf on Linux and macOS. Same contents, so install.sh copies the same file to the other name.
set "VENV_PIP=%REPO_ROOT%\python\Scripts\pip.exe"
set "VENV_PYTHON=%REPO_ROOT%\python\Scripts\python.exe"
set "VENV_CONFIG=%REPO_ROOT%\python\pip.ini"
set "TOOLS_DIR=%REPO_ROOT%\tools\python"
set "REQUIREMENTS=%SCRIPT_DIR%requirements.txt"

if not exist "%VENV_PIP%" (
    echo error: no virtual environment at %REPO_ROOT%\python 1>&2
    echo        create it with:  python -m venv "%REPO_ROOT%\python" 1>&2
    echo        then run this script again. 1>&2
    exit /b 1
)

if not exist "%REQUIREMENTS%" (
    echo error: %REQUIREMENTS% does not exist. 1>&2
    echo        it is generated, not written by hand. compile it first: 1>&2
    echo        "%REPO_ROOT%\python\Scripts\uv.exe" pip compile "%SCRIPT_DIR%requirements.in" --universal --python-version 3.13 --generate-hashes -o "%REQUIREMENTS%" 1>&2
    echo        --universal keeps the environment markers; without it the lock only installs 1>&2
    echo        on the platform it was compiled on. 1>&2
    exit /b 1
)

rem Is this environment new enough for the lock about to be installed into it? Asked with the venv's
rem own interpreter, and against the floor recorded in the lock's header, so neither half is a
rem constant kept in step by hand. See check_python.py for why it is worth asking before pip does.
"%VENV_PYTHON%" "%TOOLS_DIR%\check_python.py" "%REQUIREMENTS%"
if errorlevel 1 exit /b 1

rem The settings, into the venv where pip reads them. Copied on every install rather than once by
rem hand: python -m venv rewrites this directory on every rebuild, so a copy that lives only here is
rem destroyed by the next one - silently taking the wheels-only and single-index defaults with it.
rem The repo's pip.ini is the canonical one; this copy is derived and is overwritten each time.
rem
rem After the checks above, deliberately. Copying first means a missing venv fails on the copy rather
rem than on the message that says how to make one - the error the check exists to give.
copy /y "%TOOLS_DIR%\pip.ini" "%VENV_CONFIG%" >nul
if errorlevel 1 (
    echo error: could not write %VENV_CONFIG% 1>&2
    exit /b 1
)

rem Both flags are passed explicitly even though the settings just copied set only-binary. The copy
rem is one del from being gone, and the by-hand pip invocation the README documents has no such step
rem - these are what still hold when it is absent.
rem
rem   --require-hashes   every package, transitive ones included, must match its recorded digest
rem   --only-binary      no source distributions, so no setup.py executes during install
"%VENV_PIP%" install --require-hashes --only-binary=:all: -r "%REQUIREMENTS%"
if errorlevel 1 exit /b 1

endlocal
