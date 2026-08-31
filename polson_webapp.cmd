@echo off
REM Serves the Polson studio - the browser over a project directory.
REM
REM   polson_webapp.cmd path\to\projects [--host 0.0.0.0] [--port 8000]
REM
REM The argument is the directory that *holds* project directories, not a project: the front page
REM lists what it finds there, and a brief typed into the form creates another one beside them.
REM Nothing starts an agent until someone presses a button.
REM
REM For one turn in the terminal instead, with no browser and you as the director, use polson_run.cmd.

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
    echo usage: polson_webapp.cmd ^<projects-dir^> [--host HOST] [--port PORT]
    echo        the directory holds projects; it is not itself one
    echo        serves http://127.0.0.1:8000 by default
    echo        for one turn in the terminal instead:  polson_run.cmd ^<project-dir^>
    exit /b 1
)

"%VENV%" "%ROOT%src\webapp\serve_studio.py" %*
exit /b %ERRORLEVEL%
