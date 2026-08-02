<#
.SYNOPSIS
    Interactively scaffolds an Entity Framework Core migration.

.DESCRIPTION
    Prompts for the target project, migration name, and output directory, then runs
    'dotnet ef migrations add' with the correct arguments. Validates the environment
    first: dotnet-ef tool presence, the Microsoft.EntityFrameworkCore.Design package
    reference, migration name syntax, and existing ModelSnapshot location.

    Any parameter supplied on the command line skips the corresponding prompt, so the
    script can also run non-interactively in a build pipeline.

.PARAMETER Name
    Migration name. Must be a valid C# identifier (letters, digits, underscores; no
    leading digit). Example: InitialCreate

.PARAMETER Project
    Project name or path containing the DbContext. Matched against discovered .csproj
    files by name, so 'UserMicroService' is enough.

.PARAMETER OutputDir
    Directory for the generated migration files, relative to the project directory.
    Defaults to Data/Migrations.

.PARAMETER StartupProject
    Project whose host configuration is used to build the DbContext. Defaults to the
    value of -Project.

.PARAMETER Context
    DbContext class name. Only required when the project defines more than one.

.PARAMETER Namespace
    Override the namespace of the generated classes. Defaults to one derived from
    OutputDir.

.PARAMETER Apply
    Run 'dotnet ef database update' after a successful scaffold, without asking.

.PARAMETER NonInteractive
    Fail instead of prompting when a required value is missing.

.EXAMPLE
    .\New-EfMigration.ps1
    Prompts for everything.

.EXAMPLE
    .\New-EfMigration.ps1 -Name InitialCreate -Project UserMicroService
    Uses the default output directory, prompts for nothing.

.EXAMPLE
    .\New-EfMigration.ps1 -Name AddLastSeenIndex -Project UserMicroService -OutputDir Data/Migrations -Apply
    Fully specified, applies to the database afterwards.
#>

[CmdletBinding()]
param(
    [string] $Name,
    [string] $Project,
    [string] $OutputDir,
    [string] $StartupProject,
    [string] $Context,
    [string] $Namespace,
    [switch] $Apply,
    [switch] $NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Output helpers
# ---------------------------------------------------------------------------

function Write-Step  { param([string] $Message) Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Ok    { param([string] $Message) Write-Host "    $Message" -ForegroundColor Green }
function Write-Warn  { param([string] $Message) Write-Host "    $Message" -ForegroundColor Yellow }
function Write-Info  { param([string] $Message) Write-Host "    $Message" -ForegroundColor DarkGray }

function Stop-WithError {
    param([string] $Message)
    Write-Host "`nERROR: $Message" -ForegroundColor Red
    exit 1
}

function Read-Choice {
    <#
        Yes/no prompt. Returns $true or $false. Honours -NonInteractive by
        returning the supplied default without asking.
    #>
    param(
        [string] $Question,
        [bool]   $Default = $false
    )

    if ($NonInteractive) { return $Default }

    $hint = if ($Default) { 'Y/n' } else { 'y/N' }
    while ($true) {
        $answer = (Read-Host "    $Question [$hint]").Trim()
        if ([string]::IsNullOrEmpty($answer)) { return $Default }
        switch -Regex ($answer) {
            '^(y|yes)$' { return $true }
            '^(n|no)$'  { return $false }
            default     { Write-Warn "Please answer y or n." }
        }
    }
}

# ---------------------------------------------------------------------------
# Locate the repository root
# ---------------------------------------------------------------------------

$root = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
Push-Location $root

try {
    Write-Host ""
    Write-Host "  EF Core Migration Generator" -ForegroundColor White
    Write-Host "  Working directory: $root" -ForegroundColor DarkGray

    # -----------------------------------------------------------------------
    # 1. Verify the dotnet-ef tool
    # -----------------------------------------------------------------------

    Write-Step "Checking prerequisites"

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Stop-WithError "The .NET SDK is not on PATH. Install it from https://dotnet.microsoft.com/download"
    }

    $efVersion = & dotnet ef --version 2>&1 | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "The dotnet-ef tool is not installed."
        if (Read-Choice "Install it globally now?" $true) {
            & dotnet tool install --global dotnet-ef
            if ($LASTEXITCODE -ne 0) { Stop-WithError "Installation of dotnet-ef failed." }
            Write-Ok "dotnet-ef installed. You may need to open a new shell if the command is not found."
        }
        else {
            Stop-WithError "dotnet-ef is required. Install with: dotnet tool install --global dotnet-ef"
        }
    }
    else {
        Write-Ok "dotnet-ef $efVersion"
    }

    # -----------------------------------------------------------------------
    # 2. Discover and select the project
    # -----------------------------------------------------------------------

    Write-Step "Selecting project"

    $candidates = @(
        Get-ChildItem -Path $root -Filter *.csproj -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.vs)[\\/]' } |
            Sort-Object Name
    )

    if ($candidates.Count -eq 0) {
        Stop-WithError "No .csproj files found beneath $root"
    }

    $projectFile = $null

    if ($Project) {
        $projectFile = $candidates | Where-Object {
            $_.BaseName -ieq $Project -or $_.FullName -ieq (Resolve-Path -LiteralPath $Project -ErrorAction SilentlyContinue)
        } | Select-Object -First 1

        if (-not $projectFile) {
            Stop-WithError "Project '$Project' not found. Available: $($candidates.BaseName -join ', ')"
        }
    }
    elseif ($candidates.Count -eq 1) {
        $projectFile = $candidates[0]
        Write-Info "Only one project found, selecting it automatically."
    }
    elseif ($NonInteractive) {
        Stop-WithError "-Project is required when multiple projects exist."
    }
    else {
        Write-Host ""
        for ($i = 0; $i -lt $candidates.Count; $i++) {
            $relative = $candidates[$i].FullName.Substring($root.Length).TrimStart('\', '/')
            Write-Host ("    [{0}] {1}" -f ($i + 1), $relative)
        }
        Write-Host ""

        while (-not $projectFile) {
            $answer = (Read-Host "    Project number").Trim()
            $index = 0
            if ([int]::TryParse($answer, [ref] $index) -and $index -ge 1 -and $index -le $candidates.Count) {
                $projectFile = $candidates[$index - 1]
            }
            else {
                Write-Warn "Enter a number between 1 and $($candidates.Count)."
            }
        }
    }

    $projectDir  = $projectFile.Directory.FullName
    $projectName = $projectFile.BaseName
    Write-Ok "Project: $projectName"

    if (-not $StartupProject) { $StartupProject = $projectFile.FullName }

    # -----------------------------------------------------------------------
    # 3. Verify the Design package reference
    # -----------------------------------------------------------------------

    $csprojText = Get-Content -LiteralPath $projectFile.FullName -Raw

    if ($csprojText -notmatch 'Microsoft\.EntityFrameworkCore\.Design') {
        Write-Warn "Microsoft.EntityFrameworkCore.Design is not referenced by $projectName."
        Write-Info "dotnet ef cannot scaffold without it."

        if (Read-Choice "Add the package now?" $true) {
            & dotnet add "$($projectFile.FullName)" package Microsoft.EntityFrameworkCore.Design
            if ($LASTEXITCODE -ne 0) { Stop-WithError "Failed to add Microsoft.EntityFrameworkCore.Design." }
            Write-Ok "Package added."
        }
        else {
            Stop-WithError "Microsoft.EntityFrameworkCore.Design is required."
        }
    }
    else {
        Write-Ok "Microsoft.EntityFrameworkCore.Design is referenced."
    }

    # -----------------------------------------------------------------------
    # 4. Migration name
    # -----------------------------------------------------------------------

    Write-Step "Migration name"

    $namePattern = '^[A-Za-z_][A-Za-z0-9_]*$'

    if ($Name -and $Name -notmatch $namePattern) {
        Stop-WithError "'$Name' is not a valid migration name. Use letters, digits and underscores, starting with a letter."
    }

    if (-not $Name) {
        if ($NonInteractive) { Stop-WithError "-Name is required." }

        Write-Info "Becomes a C# class name. Examples: InitialCreate, AddUserLastSeenIndex"
        while (-not $Name) {
            $entry = (Read-Host "    Migration name").Trim()
            if ([string]::IsNullOrWhiteSpace($entry)) {
                Write-Warn "A name is required."
            }
            elseif ($entry -notmatch $namePattern) {
                Write-Warn "Letters, digits and underscores only, and it cannot start with a digit."
            }
            else {
                $Name = $entry
            }
        }
    }

    Write-Ok "Name: $Name"

    # -----------------------------------------------------------------------
    # 5. Output directory
    # -----------------------------------------------------------------------

    Write-Step "Output directory"

    # If a snapshot already exists, EF ignores --output-dir and writes alongside it.
    $existingSnapshot = Get-ChildItem -Path $projectDir -Filter '*ModelSnapshot.cs' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Select-Object -First 1

    if ($existingSnapshot) {
        $snapshotDir = $existingSnapshot.Directory.FullName.Substring($projectDir.Length).TrimStart('\', '/')
        Write-Warn "An existing migration set was found in '$snapshotDir'."
        Write-Info "EF places new migrations next to the existing snapshot, so the output directory is ignored."
        $OutputDir = $snapshotDir -replace '\\', '/'
    }
    else {
        if (-not $OutputDir) {
            if ($NonInteractive) {
                $OutputDir = 'Data/Migrations'
            }
            else {
                Write-Info "Relative to the project directory. Press Enter for the default."
                $entry = (Read-Host "    Output directory [Data/Migrations]").Trim()
                $OutputDir = if ([string]::IsNullOrWhiteSpace($entry)) { 'Data/Migrations' } else { $entry }
            }
        }

        $OutputDir = $OutputDir -replace '\\', '/'

        if ([System.IO.Path]::IsPathRooted($OutputDir)) {
            Stop-WithError "The output directory must be relative to the project, not an absolute path."
        }
    }

    Write-Ok "Output: $OutputDir"

    # -----------------------------------------------------------------------
    # 6. DbContext selection
    # -----------------------------------------------------------------------

    Write-Step "Resolving DbContext"

    if (-not $Context) {
        $listOutput = & dotnet ef dbcontext list `
            --project "$($projectFile.FullName)" `
            --startup-project "$StartupProject" `
            --json 2>&1

        if ($LASTEXITCODE -eq 0) {
            # The tool prints build noise before the JSON payload; take from the first '['.
            $joined = ($listOutput | Out-String)
            $start  = $joined.IndexOf('[')

            if ($start -ge 0) {
                try {
                    $contexts = $joined.Substring($start) | ConvertFrom-Json
                    $names = @($contexts | ForEach-Object { ($_.fullName -split '\.')[-1] })

                    if ($names.Count -eq 1) {
                        $Context = $names[0]
                        Write-Ok "Context: $Context"
                    }
                    elseif ($names.Count -gt 1) {
                        if ($NonInteractive) { Stop-WithError "-Context is required; found: $($names -join ', ')" }

                        Write-Host ""
                        for ($i = 0; $i -lt $names.Count; $i++) {
                            Write-Host ("    [{0}] {1}" -f ($i + 1), $names[$i])
                        }
                        Write-Host ""

                        while (-not $Context) {
                            $answer = (Read-Host "    Context number").Trim()
                            $index = 0
                            if ([int]::TryParse($answer, [ref] $index) -and $index -ge 1 -and $index -le $names.Count) {
                                $Context = $names[$index - 1]
                            }
                            else {
                                Write-Warn "Enter a number between 1 and $($names.Count)."
                            }
                        }
                    }
                }
                catch {
                    Write-Info "Could not parse the context list; letting EF resolve it."
                }
            }
        }
        else {
            Write-Info "Context discovery failed; letting EF resolve it. Errors will surface below."
        }
    }
    else {
        Write-Ok "Context: $Context"
    }

    # -----------------------------------------------------------------------
    # 7. Confirm and scaffold
    # -----------------------------------------------------------------------

    Write-Step "Ready to scaffold"

    $efArgs = @(
        'ef', 'migrations', 'add', $Name,
        '--project',         $projectFile.FullName,
        '--startup-project', $StartupProject,
        '--output-dir',      $OutputDir
    )

    if ($Context)   { $efArgs += @('--context',   $Context) }
    if ($Namespace) { $efArgs += @('--namespace', $Namespace) }

    Write-Info "dotnet $($efArgs -join ' ')"
    Write-Host ""

    if (-not (Read-Choice "Proceed?" $true)) {
        Write-Host "`nCancelled." -ForegroundColor Yellow
        exit 0
    }

    Write-Host ""
    & dotnet @efArgs

    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "Migration scaffolding failed. See the output above."
    }

    # -----------------------------------------------------------------------
    # 8. Report generated files
    # -----------------------------------------------------------------------

    Write-Step "Generated files"

    $migrationPath = Join-Path $projectDir ($OutputDir -replace '/', [System.IO.Path]::DirectorySeparatorChar)

    if (Test-Path -LiteralPath $migrationPath) {
        Get-ChildItem -Path $migrationPath -Filter '*.cs' -File |
            Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-2) } |
            ForEach-Object {
                $relative = $_.FullName.Substring($root.Length).TrimStart('\', '/')
                Write-Ok $relative
            }
    }

    Write-Info "Review the Up() method before applying, then commit all three files."

    # -----------------------------------------------------------------------
    # 9. Optionally apply
    # -----------------------------------------------------------------------

    $shouldApply = $Apply -or (Read-Choice "Apply this migration to the database now?" $false)

    if ($shouldApply) {
        Write-Step "Applying migration"
        Write-Info "Uses host configuration (user secrets / appsettings), not container settings."

        $updateArgs = @(
            'ef', 'database', 'update',
            '--project',         $projectFile.FullName,
            '--startup-project', $StartupProject
        )
        if ($Context) { $updateArgs += @('--context', $Context) }

        Write-Host ""
        & dotnet @updateArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Warn "The update failed. The migration files are still valid."
            Write-Info "Check that the database server is reachable and the connection string resolves on this machine."
            exit 1
        }

        Write-Ok "Database updated."
    }
    else {
        Write-Info "Apply later with: dotnet ef database update --project $projectName --startup-project $projectName"
    }

    Write-Host "`nDone.`n" -ForegroundColor Green
}
finally {
    Pop-Location
}