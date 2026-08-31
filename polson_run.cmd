@echo off
REM Runs one turn against a standalone project, with the terminal as the director.
REM
REM   polson_run.cmd path\to\project [--prompt "..."] [--fresh] [--unattended]
REM
REM The developer path: no browser, one turn per invocation, the agent's questions printed and your
REM reply typed back. For the browser over the same run_turn, use polson_webapp.cmd.
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
    echo usage: polson_run.cmd ^<project-dir^> [--prompt "..."] [--fresh] [--unattended]
    echo        one project, not a directory of them
    echo        make one with:  bin\cli\Polson.CLI.exe create-project ^<parent^> ^<id^> agy --standalone
    echo        for the browser instead:  polson_webapp.cmd ^<projects-dir^>
    exit /b 1
)

"%VENV%" "%ROOT%src\webapp\run_studio.py" %*
exit /b %ERRORLEVEL%
