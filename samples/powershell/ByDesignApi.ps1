# ByDesign API v2 - PowerShell sample
#
# Requires: PowerShell 7+
# Set these environment variables first (values come from the ByDesign back office):
#   BYDESIGN_API_URL        e.g. https://your-api-url (no trailing slash)
#   BYDESIGN_CLIENT_ID
#   BYDESIGN_CLIENT_SECRET
#   BYDESIGN_API_KEY

$ErrorActionPreference = 'Stop'

foreach ($name in 'BYDESIGN_API_URL', 'BYDESIGN_CLIENT_ID', 'BYDESIGN_CLIENT_SECRET', 'BYDESIGN_API_KEY') {
    if (-not [Environment]::GetEnvironmentVariable($name)) { throw "Set the $name environment variable." }
}

$apiUrl = $env:BYDESIGN_API_URL.TrimEnd('/')

# 1. Request an access token (valid for one hour - reuse it, don't request one per call)
$token = Invoke-RestMethod -Method Post -Uri "$apiUrl/oauth2/token" `
    -ContentType 'application/x-www-form-urlencoded' `
    -Body @{
        grant_type    = 'client_credentials'
        client_id     = $env:BYDESIGN_CLIENT_ID
        client_secret = $env:BYDESIGN_CLIENT_SECRET
        scope         = 'api/Read api/Write'
    }

$headers = @{
    Authorization = "Bearer $($token.access_token)"
    'x-api-key'   = $env:BYDESIGN_API_KEY
}

# 2. GET request: list inventory categories (requires api/Read)
$categories = Invoke-RestMethod -Method Get -Uri "$apiUrl/api/v2/inventory/categorydetail" -Headers $headers
$categories | ConvertTo-Json -Depth 10

# 3. POST request: look up customers by email (requires api/Write)
$lookup = @{ Email = 'jane.doe@example.com' } | ConvertTo-Json
$customers = Invoke-RestMethod -Method Post -Uri "$apiUrl/api/v2/customer/lookup" -Headers $headers `
    -ContentType 'application/json' -Body $lookup
$customers | ConvertTo-Json -Depth 10
