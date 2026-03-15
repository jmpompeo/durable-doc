#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AssetContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseDir,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    if ($RelativePath -match '://') {
        throw "External assets are not supported: $RelativePath"
    }

    $resolvedBaseDir = [System.IO.Path]::GetFullPath($BaseDir)
    $assetPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedBaseDir $RelativePath))

    if (-not $assetPath.StartsWith($resolvedBaseDir, [System.StringComparison]::Ordinal)) {
        throw "Asset path escapes the dashboard directory: $RelativePath"
    }

    return [System.IO.File]::ReadAllText($assetPath)
}

$resolvedInputDir = [System.IO.Path]::GetFullPath($InputDir)
$resolvedOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$htmlPath = Join-Path $resolvedInputDir 'index.html'
$html = [System.IO.File]::ReadAllText($htmlPath)

$stylesheetPattern = '<link rel="stylesheet" href="([^"]+)">\s*'
$scriptPattern = '<script src="([^"]+)"></script>\s*'

$html = [System.Text.RegularExpressions.Regex]::Replace(
    $html,
    $stylesheetPattern,
    {
        param($match)
        $content = Get-AssetContent -BaseDir $resolvedInputDir -RelativePath $match.Groups[1].Value
        return "<style>`n$content`n</style>`n"
    })

$html = [System.Text.RegularExpressions.Regex]::Replace(
    $html,
    $scriptPattern,
    {
        param($match)
        $content = (Get-AssetContent -BaseDir $resolvedInputDir -RelativePath $match.Groups[1].Value) -replace '</script', '<\/script'
        return "<script>`n$content`n</script>`n"
    })

[System.IO.Directory]::CreateDirectory($resolvedOutputDir) | Out-Null
[System.IO.File]::WriteAllText((Join-Path $resolvedOutputDir 'index.html'), $html)
