# Generate Changelog JSON from GitHub Releases

param(
    [Parameter(Mandatory=$true)]
    [string]$Repository,

    [Parameter(Mandatory=$true)]
    [string]$Token,

    [Parameter(Mandatory=$false)]
    [int]$MaxReleases = 20
)

$ErrorActionPreference = "Stop"

Write-Host "Fetching releases from GitHub API..." -ForegroundColor Cyan
Write-Host "Repository: $Repository" -ForegroundColor Gray

# GitHub API endpoint
$apiUrl = "https://api.github.com/repos/${Repository}/releases?per_page=${MaxReleases}"

Write-Host "API URL: $apiUrl" -ForegroundColor Gray

# Set up headers with authentication
$headers = @{
    "Accept" = "application/vnd.github+json"
    "Authorization" = "Bearer $Token"
    "X-GitHub-Api-Version" = "2022-11-28"
}

try {
    # Fetch releases
    $response = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method Get

    Write-Host "Found $($response.Count) releases" -ForegroundColor Green

    # Build JSON structure
    $changelogData = @{
        generated_at = (Get-Date -Format "o")
        repository = $Repository
        releases = @()
    }

    foreach ($release in $response) {
        $releaseInfo = @{
            tag_name = $release.tag_name
            name = $release.name
            published_at = $release.published_at
            prerelease = $release.prerelease
            draft = $release.draft
            body = $release.body
            html_url = $release.html_url
            author = @{
                login = $release.author.login
                html_url = $release.author.html_url
            }
        }

        $changelogData.releases += $releaseInfo
        Write-Host "  - $($release.tag_name) ($($release.published_at))" -ForegroundColor Gray
    }

    # Convert to JSON
    $jsonOutput = $changelogData | ConvertTo-Json -Depth 10 -Compress:$false

    # Write to file
    $outputPath = "docs/changelog.json"
    $jsonOutput | Out-File -FilePath $outputPath -Encoding UTF8 -NoNewline

    Write-Host "`nChangelog JSON generated successfully at: $outputPath" -ForegroundColor Green
    Write-Host "Total releases: $($changelogData.releases.Count)" -ForegroundColor Green

} catch {
    Write-Host "Error fetching releases: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
