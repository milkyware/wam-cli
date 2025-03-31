[CmdletBinding()]
param (
)
begin
{
    $InformationPreference = "Continue"
    if ($env:RUNNER_DEBUG)
    {
        $DebugPreference = "Continue"
        $VerbosePreference = "Continue"
    }
}
process
{
    Write-Debug "Extracting tag"
    $tag = git describe --tags --abbrev=0
    Write-Information "Found tag: $tag"
    "tag=$tag" >> $env:GITHUB_OUTPUT

    Write-Debug "Parsing version"
    $match = $tag -match "(\d+\.?){1,3}"
    if (-not $match)
    {
        throw "Unable to parse version from tag '$tag'"
    }

    $version = [version]::Parse($Matches[0])
    "version=$version" >> $env:GITHUB_OUTPUT
    "major=$($version.Major)" >> $env:GITHUB_OUTPUT
    "minor=$($version.Minor)" >> $env:GITHUB_OUTPUT

    Write-Debug "Counting commits"
    $totalCommits = git rev-list --count HEAD
    "total_commits=$totalCommits" >> $env:GITHUB_OUTPUT

    Write-Debug "Counting commits since tag"
    $commitCountSinceTag = git rev-list --count $tag..HEAD
    "commits_since_tag=$commitCountSinceTag" >> $env:GITHUB_OUTPUT
}