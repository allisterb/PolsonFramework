@echo off
pushd
@setlocal
set ERROR_CODE=0

bin\cli\Polson.CLI.exe %*

:end
@endlocal
popd
exit /B %ERROR_CODE%