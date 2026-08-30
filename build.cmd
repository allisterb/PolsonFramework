@echo off
echo Building Polson...
dotnet restore Polson.sln
dotnet build src\Polson.CLI\Polson.CLI.csproj /p:Configuration=Release