# ============================================================
# IcomRigControl — Windows code-signing shim (house pattern)
#
# This repo does NOT hold any signing config, secrets, or the signing tool.
# It DELEGATES to the ONE central shared signer used by every KE4CON app:
#     C:\Dev\Signing and Distribution\azure\sign.ps1
# which holds the single metadata.json (Azure Trusted / Artifact Signing:
# endpoint, account "aprscommandsign", profile "rospopo-public") and signs
# ALL of my apps under one publisher identity.
#
# Signing is OPTIONAL and CREDENTIAL-GATED. The central signer skips cleanly
# (leaving UNSIGNED output) unless you are logged in with `az login`. So the
# same build pipeline produces signed OR unsigned output with no changes.
#
# Resolution order for the central signer (first that exists wins):
#   1. $env:SIGN_SCRIPT               — full path to a sign.ps1
#   2. $env:SIGNING_DIR\sign.ps1      — a signing directory
#   3. C:\Dev\Signing and Distribution\azure\sign.ps1   (default)
# If none is found, this shim WARNS and SKIPS (unsigned), never failing.
#
# USAGE:
#   pwsh build/sign-windows.ps1 "path\to\App.exe" ["path\to\Setup.exe" ...]
# ============================================================
param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]] $Files
)

$ErrorActionPreference = "Stop"

# ── Resolve the central signer ────────────────────────────────────────────────
$signScript = $env:SIGN_SCRIPT
if (-not $signScript -and $env:SIGNING_DIR) {
    $signScript = Join-Path $env:SIGNING_DIR "sign.ps1"
}
if (-not $signScript) {
    $signScript = "C:\Dev\Signing and Distribution\azure\sign.ps1"
}

if (-not (Test-Path $signScript)) {
    Write-Warning "Central signer not found at '$signScript' - SKIPPING signing. Output will be UNSIGNED."
    Write-Warning "Set `$env:SIGN_SCRIPT or `$env:SIGNING_DIR to point at the shared sign.ps1 to enable signing."
    exit 0
}

# Delegate. The central signer itself gates on `az login` and skips cleanly if
# not logged in, so we simply forward the files to it.
Write-Host "Delegating to central signer: $signScript" -ForegroundColor Cyan
& $signScript @Files
exit $LASTEXITCODE
