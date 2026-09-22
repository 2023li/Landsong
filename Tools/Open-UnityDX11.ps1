param([string]$UnityEditorPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$versionText = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Raw
if ($versionText -notmatch '(?m)^m_EditorVersion: ([\w.]+)\s*$') { throw 'Cannot read the project Unity version.' }
if (-not $UnityEditorPath) {
    $UnityEditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$($Matches[1])/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityEditorPath)) { throw 'Unity Editor not found. Pass -UnityEditorPath with the installed Unity.exe path.' }
# This only selects the editor graphics API; release build settings are unchanged.
& $UnityEditorPath '-projectPath' $projectRoot '-force-d3d11'
