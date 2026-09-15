<#
.SYNOPSIS
    Polson's entry point. Dispatches the verb to the .NET CLI or to Python.

.DESCRIPTION
    Polson's verbs do not all live behind the same runtime:

      server, eval, create-project, report, help, version    the .NET CLI, bin/cli
      studio                                                 Python, src/studio

    The split follows where the code already is. `server` IS the MCP server and `eval`, `report`
    and `create-project` are C#-fronted; the studio is a FastAPI application with its own argparse,
    and a CLI verb for it would re-declare every one of its options in a second place, then spawn
    Python anyway. Branching here is that dispatch without the second copy of the flags and without
    the process hop.

    Arguments are forwarded verbatim and the exit code is passed through, so anything documented
    for `polson report` or for `python -m studio` works here unchanged.

    Interpreter and binary are found in this order:

      POLSON_PYTHON, then the repo venv at python/, then python3 / python on PATH
      POLSON_CLI, then bin/cli/Polson.CLI.exe, then bin/cli/Polson.CLI.dll through dotnet

    POLSON_CLI may name either the managed dll or a self-contained executable; a .dll is run
    through `dotnet` and anything else is run directly.

    Keep this in step with the bash `polson`, which does the same job everywhere else.

    This replaced polson.cmd, which could not do the branching and carried a defect worth
    recording: it set ERROR_CODE=0 and then `exit /B %ERROR_CODE%`, so **every** invocation
    reported success and a failed render looked like a good one to anything reading the code.

.EXAMPLE
    .\polson.ps1 create-project acme --workflow drawing
    Generate a project directory for an agent to work in.

.EXAMPLE
    .\polson.ps1 studio projects --observe-only
    Watch a run you are driving from your own agent host. Nothing is started, nothing is spent.

.EXAMPLE
    .\polson.ps1 report projects/acme
    Summarise what actually happened in a run, from its event log.
#>

# No param block, deliberately: everything reaching this script belongs to the verb, and a
# parameter declared here would swallow the argument of whichever verb happened to share its name.
# $args is the whole command line, untouched.

# 'Continue', deliberately, and set rather than inherited. Windows PowerShell wraps a native
# command's stderr in an ErrorRecord when the stream is redirected, so under 'Stop' an ordinary
# diagnostic from the CLI becomes a terminating error HERE. Nothing below relies on 'Stop': the
# failures this script raises are `throw`, which terminates whatever the preference says.
$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest

$RepoRoot = $PSScriptRoot

# The routing table, and the only place a new Python verb needs adding.
$PythonVerbs = @{
    studio = 'studio'
}

$HelpTokens = @('help', '--help', '-h', '-?', '/?')

# $IsWindows is a PowerShell 6+ automatic variable and does not exist at all under Windows
# PowerShell 5.1, where Set-StrictMode makes reading it an error. Resolve the platform once.
if ($PSVersionTable.PSVersion.Major -lt 6) {
    $OnWindows = $true
} else {
    $OnWindows = [bool] $IsWindows
}

function Find-Python {
    # Trusted as given: a caller who set it knows better than this search does.
    if ($env:POLSON_PYTHON) { return $env:POLSON_PYTHON }

    # `python/` and not `python-adk/`. The two venvs are deliberately separate - ADK caps
    # websockets<16 where the studio's lock resolves it to 16.1.1, so in one shared environment the
    # second install silently wins and neither lock then describes what is installed. See CLAUDE.md.
    $candidates = @((Join-Path $RepoRoot 'python/bin/python3'), (Join-Path $RepoRoot 'python/bin/python'))
    if ($OnWindows) { $candidates = @(Join-Path $RepoRoot 'python/Scripts/python.exe') }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }

    # PATH last. On Windows this can find the App Execution Alias under WindowsApps, which exists as
    # a file and only ever prints "Python was not found"; here the failure is at least loud.
    foreach ($name in @('python3', 'python')) {
        $command = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue |
                   Select-Object -First 1
        if ($command) { return $command.Source }
    }

    throw @"
No usable Python found, and the studio is Python.
Create the repo venv and install the studio's lock:

    python -m venv python
    src\studio\install.cmd

or put python on PATH, or set POLSON_PYTHON.
"@
}

function Find-Cli {
    if ($env:POLSON_CLI) {
        if ([IO.Path]::GetExtension($env:POLSON_CLI) -eq '.dll') {
            return [pscustomobject]@{ Exe = 'dotnet'; Prefix = @($env:POLSON_CLI) }
        }
        return [pscustomobject]@{ Exe = $env:POLSON_CLI; Prefix = @() }
    }

    $apphost = if ($OnWindows) { 'Polson.CLI.exe' } else { 'Polson.CLI' }

    # One output directory rather than a Release/Debug search: the CLI builds to bin/cli, which is
    # what an agent harness's .mcp.json already points at.
    $dir = Join-Path $RepoRoot 'bin/cli'

    # The apphost when the build produced one, because it needs no dotnet on PATH.
    $exe = Join-Path $dir $apphost
    if (Test-Path -LiteralPath $exe -PathType Leaf) {
        return [pscustomobject]@{ Exe = $exe; Prefix = @() }
    }

    $dll = Join-Path $dir 'Polson.CLI.dll'
    if (Test-Path -LiteralPath $dll -PathType Leaf) {
        return [pscustomobject]@{ Exe = 'dotnet'; Prefix = @($dll) }
    }

    throw @"
The Polson CLI is not built. Run:
    .\build.cmd
or set POLSON_CLI to the path of Polson.CLI.dll.
"@
}

function Invoke-PolsonPython([string] $Verb, [string] $Module, [string[]] $Rest) {
    if ($null -eq $Rest) { $Rest = @() }
    $python = Find-Python

    # So that argparse opens with "usage: polson.ps1 studio" rather than naming a module nobody typed.
    $env:POLSON_VERB = "$(Split-Path -Leaf $PSCommandPath) $Verb"

    # `studio` is a package under src/, so src/ has to be importable. Set on the path rather than by
    # changing directory, so a relative argument still resolves against the caller's own cwd -
    # `.\polson.ps1 studio projects` means the projects directory they can see, not one under src/.
    $src = Join-Path $RepoRoot 'src'
    if ($env:PYTHONPATH) {
        $env:PYTHONPATH = "$src$([IO.Path]::PathSeparator)$env:PYTHONPATH"
    } else {
        $env:PYTHONPATH = $src
    }

    $line = @('-m', $Module) + @($Rest)
    & $python @line
    exit $LASTEXITCODE
}

# The CLI's own help cannot mention a verb it does not have, so this script says it afterwards.
function Show-ExtraVerbs {
    Write-Output '  studio            Serve the studio over a directory of projects: the live trace, the'
    Write-Output '                    renders beside the scripts that made them, and the sense-making curve.'
    Write-Output '                    Python, and needs the python/ venv - see README.md.'
    Write-Output ''
}

$verb = if ($args.Count -gt 0) { [string] $args[0] } else { '' }

# Assigned first and then replaced, NOT `$rest = if (...) {...} else { @() }`: PowerShell unrolls a
# script block's output and an empty array outputs nothing, so that form leaves $rest null - and
# $null.Count under Set-StrictMode is an error rather than 0.
$rest = @()
if ($args.Count -gt 1) { $rest = @($args[1..($args.Count - 1)]) }

# `polson help studio`, which the CLI would reject as a verb name it has never heard of.
if (($HelpTokens -contains $verb) -and $rest.Count -gt 0) {
    $topic = [string] $rest[0]
    if ($PythonVerbs.ContainsKey($topic)) {
        Invoke-PolsonPython $topic ($PythonVerbs[$topic]) @('--help')
    }
}

if ($PythonVerbs.ContainsKey($verb)) {
    Invoke-PolsonPython $verb ($PythonVerbs[$verb]) $rest
}

# Everything else is the CLI's, including an unknown verb: its parser writes a better error than a
# guess here would, and a verb added to the CLI works through this script without it being touched.
$cli = Find-Cli
$line = @($cli.Prefix) + @($args)
& $cli.Exe @line
$code = $LASTEXITCODE

if ($HelpTokens -contains $verb) { Show-ExtraVerbs }

exit $code
