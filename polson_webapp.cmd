@echo off
REM Runs the Polson studio against a standalone project directory.
REM
REM   polson_webapp.cmd path\to\project [--prompt "..."] [--fresh] [--unattended]
REM
REM Today this is the terminal orchestrator: one turn per invocation, with the terminal acting as
REM the director. Milestone 6 puts a browser over the same run_turn, and this script is where that
REM will start from, which is why it is named for the destination rather than the current step.
REM
REM The project must be a --standalone one. A managed project is refused by name, because its host
REM owns tool policy and running it here would apply none at all.

setlocal
set "ROOT=%~dp0"
set "VENV=%ROOT%python\Scripts\python.exe"

if not exist "%VENV%" (
    echo error: no virtual environment at %ROOT%python
    echo        create one with:  python -m venv python
    echo        then install the SDK:  python\Scripts\python.exe -m pip install google-antigravity
    exit /b 1
)

if "%~1"=="" (
    echo usage: polson_webapp.cmd ^<project-dir^> [--prompt "..."] [--fresh] [--unattended]
    echo        make a project with:  bin\cli\Polson.CLI.exe create-project ^<parent^> ^<id^> agy --standalone
    exit /b 1
)

"%VENV%" "%ROOT%src\webapp\run_studio.py" %*
exit /b %ERRORLEVEL%
