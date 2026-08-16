#Requires -Version 7.0

<#
.SYNOPSIS
    Brings up the LexTime development database and fills it with a realistic dataset.

.DESCRIPTION
    Verifies prerequisites, starts the SQL Server container, waits for it to accept
    queries, applies migrations and stored procedures, seeds roughly 400,000 time entries,
    verifies their distribution, and prints a development bearer token.

    Safe to run repeatedly. A second run against a complete environment reports what it
    skipped and changes nothing.

    Requires only Docker and the .NET SDK. Migrations are applied by the application
    itself rather than by the dotnet-ef global tool, so there is nothing to install.

.PARAMETER Reset
    Drops and recreates the database, then migrates and reseeds. Leaves the container
    running and untouched — to discard the container and its storage entirely, use
    'docker compose down -v'. Never prompts: this switch is the confirmation.

.PARAMETER SkipSeed
    Brings the environment up and applies the schema without generating data. For
    iterating on schema changes.

.EXAMPLE
    pwsh ./scripts/Initialize-LocalDb.ps1
    Cold start, or a no-op if the environment is already complete.

.EXAMPLE
    pwsh ./scripts/Initialize-LocalDb.ps1 -Reset
    Discards the data and rebuilds it from scratch without restarting the container.

.NOTES
    Exit codes are defined in specs/002-bootstrap-and-seed/contracts/bootstrap-cli.md.
#>
[CmdletBinding()]
param(
    [switch]$Reset,
    [switch]$SkipSeed
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:TotalSteps = 8
$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$script:ApiProject = Join-Path $script:RepoRoot 'src/LexTime.Api'
$script:ContainerName = 'lextime-sqlserver'
$script:SaPassword = 'LexTime!Dev2026'
$script:ReadinessTimeoutSeconds = 120

# Exit codes, mirroring contracts/bootstrap-cli.md.
$script:ExitPrerequisite = 1
$script:ExitNotReady = 2
$script:ExitOperationFailed = 3
$script:ExitSeedFailed = 4

<#
.SYNOPSIS
    Writes one aligned progress line.
.DESCRIPTION
    "Skipped" must be visually distinguishable from "done". A script that reports success
    identically either way gives a developer no way to tell a working environment from a
    no-op (FR-004).
.PARAMETER Number
    Which step this is, for the [n/8] prefix.
.PARAMETER Name
    Short label for the step.
.PARAMETER Result
    What happened: 'ok', 'skipped', or a short phrase.
#>
function Write-Step {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Number,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Result
    )

    $label = "$Name ".PadRight(32, '.')
    Write-Host ("[{0}/{1}] {2} {3}" -f $Number, $script:TotalSteps, $label, $Result)
}

<#
.SYNOPSIS
    Reports a failure in one plain sentence and exits with a specific code.
.DESCRIPTION
    A stack trace alone is not a compliant failure (FR-011). Every exit path names its
    cause before anything else is printed.
.PARAMETER Message
    One sentence naming what went wrong.
.PARAMETER Code
    The exit code from the contract.
#>
function Stop-WithError {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Message,
        [Parameter(Mandatory)][int]$Code
    )

    Write-Host ''
    Write-Host "FAILED: $Message" -ForegroundColor Red
    exit $Code
}

<#
.SYNOPSIS
    Runs an external command, capturing its output and exit code without letting stderr
    abort the script.
.DESCRIPTION
    With $ErrorActionPreference = 'Stop', redirecting a native command's stderr through
    '2>&1' turns any stderr line into a terminating error even when the command succeeded.
    Docker and dotnet both write informational lines there, so without this the script
    aborts on a working environment and blames the wrong thing, which is precisely the
    failure FR-011 exists to prevent.
.PARAMETER Executable
    The command to run.
.PARAMETER Arguments
    Arguments to pass to it.
#>
function Invoke-Native {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Arguments
    )

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $Executable @Arguments 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }

    return [pscustomobject]@{
        ExitCode = $code
        Output   = ($output | Out-String).Trim()
    }
}

<#
.SYNOPSIS
    Runs a maintenance verb on the API host and returns its output and exit code.
.DESCRIPTION
    '--no-launch-profile' is not optional: without it 'dotnet run' honours
    launchSettings.json and forces the Development environment regardless of what this
    script sets, which has already made one verification appear to pass when it had not
    run at all.

    '--no-build' is safe here only because Invoke-Build ran first in this same invocation.
    Using it without a preceding build is how this repository twice produced a false pass.
.PARAMETER Arguments
    The verb and its options.
#>
function Invoke-Verb {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    return Invoke-Native -Executable 'dotnet' -Arguments (
        @('run', '--project', $script:ApiProject, '--no-launch-profile', '--no-build', '--') + $Arguments)
}

<#
.SYNOPSIS
    Builds the solution once, so later verb invocations can skip rebuilding.
#>
function Invoke-Build {
    [CmdletBinding()]
    param()

    $build = Invoke-Native -Executable 'dotnet' -Arguments @('build', $script:ApiProject, '--nologo', '-v', 'q')
    if ($build.ExitCode -ne 0) {
        Stop-WithError -Message 'The solution did not build. Run "dotnet build" to see the errors.' -Code $script:ExitOperationFailed
    }
}

Write-Host 'LexTime local environment'
Write-Host ''

# ---------------------------------------------------------------- 1. Prerequisites
# 'docker ps' rather than 'docker version': version can print a client version and exit 0
# while the daemon is unreachable, so it answers a different question than the one asked.
$docker = Invoke-Native -Executable 'docker' -Arguments @('ps', '--format', '{{.Names}}')
if ($docker.ExitCode -ne 0) {
    Stop-WithError -Message 'Docker is not responding. Start Docker Desktop (or your container runtime) and try again.' -Code $script:ExitPrerequisite
}

$dockerVersion = (Invoke-Native -Executable 'docker' -Arguments @('version', '--format', '{{.Server.Version}}')).Output

Push-Location $script:RepoRoot
try {
    $sdk = Invoke-Native -Executable 'dotnet' -Arguments @('--version')
}
finally {
    Pop-Location
}

if ($sdk.ExitCode -ne 0) {
    Stop-WithError -Message 'The .NET SDK pinned in global.json is not installed. Install 9.0.317 or a later 9.0.x and try again.' -Code $script:ExitPrerequisite
}

Write-Step -Number 1 -Name 'Prerequisites' -Result "ok (Docker $dockerVersion, SDK $($sdk.Output))"

# ---------------------------------------------------------------- 2. Container
$alreadyRunning = ($docker.Output -split "`n" | ForEach-Object { $PSItem.Trim() }) -contains $script:ContainerName

if ($alreadyRunning) {
    Write-Step -Number 2 -Name 'Container' -Result 'already running'
}
else {
    Push-Location $script:RepoRoot
    try {
        $up = Invoke-Native -Executable 'docker' -Arguments @('compose', 'up', '-d')
    }
    finally {
        Pop-Location
    }

    if ($up.ExitCode -ne 0) {
        Stop-WithError -Message "The database container did not start. If port 1433 is already in use, stop whatever is bound to it. Docker said: $($up.Output)" -Code $script:ExitPrerequisite
    }

    Write-Step -Number 2 -Name 'Container' -Result 'started'
}

# ---------------------------------------------------------------- 3. Readiness
# Container "started" and database "accepting queries" are different events. Polling with
# an actual query rather than sleeping a fixed interval (FR-009); the query runs inside the
# container, so no client tooling is needed on the host.
$deadline = (Get-Date).AddSeconds($script:ReadinessTimeoutSeconds)
$ready = $false
$waited = 0

while ((Get-Date) -lt $deadline) {
    $probe = Invoke-Native -Executable 'docker' -Arguments @(
        'exec', $script:ContainerName, '/opt/mssql-tools18/bin/sqlcmd',
        '-S', 'localhost', '-U', 'sa', '-P', $script:SaPassword, '-C', '-Q', 'SELECT 1')

    if ($probe.ExitCode -eq 0) {
        $ready = $true
        break
    }

    Start-Sleep -Seconds 2
    $waited += 2
}

if (-not $ready) {
    Stop-WithError -Message "The database did not accept a query within $script:ReadinessTimeoutSeconds seconds. The container is running but not serving." -Code $script:ExitNotReady
}

Write-Step -Number 3 -Name 'Readiness' -Result "ready after ${waited}s"

Invoke-Build

# ---------------------------------------------------------------- 4. Migrations
$migrateArgs = if ($Reset) { @('migrate', '--reset') } else { @('migrate') }
$migrate = Invoke-Verb -Arguments $migrateArgs

if ($migrate.ExitCode -ne 0) {
    Stop-WithError -Message "Applying migrations failed: $($migrate.Output)" -Code $script:ExitOperationFailed
}

Write-Step -Number 4 -Name 'Migrations' -Result $migrate.Output

# ---------------------------------------------------------------- 5. Stored procedures
$procedures = Invoke-Verb -Arguments @('apply-procedures')

if ($procedures.ExitCode -ne 0) {
    Stop-WithError -Message "Applying stored procedures failed: $($procedures.Output)" -Code $script:ExitOperationFailed
}

Write-Step -Number 5 -Name 'Stored procedures' -Result $procedures.Output

# ---------------------------------------------------------------- 6. Seed
if ($SkipSeed) {
    Write-Step -Number 6 -Name 'Seed' -Result 'skipped (-SkipSeed)'
}
else {
    $state = Invoke-Verb -Arguments @('state')
    if ($state.ExitCode -ne 0) {
        Stop-WithError -Message "Could not read the database state: $($state.Output)" -Code $script:ExitOperationFailed
    }

    $stateLines = $state.Output -split "`n"
    $stateName = $stateLines[0].Trim()

    switch ($stateName) {
        'Complete' {
            Write-Step -Number 6 -Name 'Seed' -Result "skipped, already seeded ($($stateLines[1].Trim()))"
        }
        'Partial' {
            # A seed interrupted midway leaves a database that looks populated and is not.
            # Topping it up would produce totals that are wrong in ways the rollup would
            # report faithfully, so this refuses instead (research.md R6).
            Stop-WithError -Message 'The database is partially seeded. Re-run with -Reset to rebuild it; this script will not top it up.' -Code $script:ExitSeedFailed
        }
        'Empty' {
            $seed = Invoke-Verb -Arguments @('seed')
            if ($seed.ExitCode -ne 0) {
                Stop-WithError -Message "Seeding failed: $($seed.Output)" -Code $script:ExitSeedFailed
            }

            Write-Step -Number 6 -Name 'Seed' -Result $seed.Output
        }
        default {
            Stop-WithError -Message "Unrecognised database state '$stateName'." -Code $script:ExitOperationFailed
        }
    }
}

# ---------------------------------------------------------------- 7. Verification
if ($SkipSeed) {
    Write-Step -Number 7 -Name 'Verification' -Result 'skipped (-SkipSeed)'
}
else {
    $verify = Invoke-Verb -Arguments @('verify-seed')

    # TrimEnd per line: splitting on "`n" leaves the carriage return behind, and Write-Host
    # then renders every check followed by a blank line.
    $verifyLines = @($verify.Output -split "`n" | ForEach-Object { $PSItem.TrimEnd() })
    $summary = $verifyLines[-1].Trim()

    if ($verify.ExitCode -ne 0) {
        # A band miss is a failure, not a warning. A seed that quietly falls outside its own
        # stated shape is worse than one that fails, because feature 003 will report on it
        # as though it were sound.
        Write-Host ''
        $verifyLines | ForEach-Object { Write-Host "        $PSItem" }
        Stop-WithError -Message "Seed verification failed: $summary" -Code $script:ExitSeedFailed
    }

    Write-Step -Number 7 -Name 'Verification' -Result $summary

    # Measured values are printed whether or not they passed. A check that only reports
    # "ok" tells a reader nothing about how close to a boundary the data sits.
    $verifyLines[0..($verifyLines.Length - 2)] | ForEach-Object { Write-Host "        $PSItem" }
}

# ---------------------------------------------------------------- 8. Development token
$token = Invoke-Verb -Arguments @('mint-token')

if ($token.ExitCode -ne 0) {
    Stop-WithError -Message "Minting a development token failed: $($token.Output)" -Code $script:ExitOperationFailed
}

Write-Step -Number 8 -Name 'Development token' -Result 'printed below'

Write-Host ''
Write-Host 'Development token (paste into the dashboard token field or Swagger authorize box):'
Write-Host ''
Write-Host $token.Output
Write-Host ''
Write-Host 'Next: dotnet run --project src/LexTime.Api'
Write-Host '      dotnet run --project src/LexTime.Api measure    # regenerate docs/performance.md'

exit 0
