[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z.-]+)?$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    if ((git branch --show-current) -ne 'main') { throw 'Releases must be created from main.' }
    if (git status --porcelain) { throw 'The working tree must be clean.' }

    [xml]$props = Get-Content -Raw 'Directory.Build.props'
    $declaredVersion = $props.Project.PropertyGroup.VersionPrefix
    if ($Version -ne $declaredVersion) { throw "Version $Version does not match VersionPrefix $declaredVersion." }
    if (-not (Select-String -Quiet -LiteralPath 'CHANGELOG.md' -Pattern "## [$Version]" -SimpleMatch)) {
        throw "CHANGELOG.md does not contain a $Version release section."
    }

    dotnet restore ShellStudio.sln
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    dotnet build ShellStudio.sln -c Release --no-restore -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    dotnet run --project tests/ShellManager.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed.' }

    $tag = "v$Version"
    if ($PSCmdlet.ShouldProcess($tag, 'Create signed annotated release tag')) {
        git tag -s $tag -m "Shell Studio $tag"
        if ($LASTEXITCODE -ne 0) { throw 'Tag signing failed.' }
        git verify-tag $tag
        if ($LASTEXITCODE -ne 0) { throw 'Tag signature verification failed.' }
        Write-Host "Created and verified $tag. Push it with: git push origin $tag"
    }
}
finally {
    Pop-Location
}
