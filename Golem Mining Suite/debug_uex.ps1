
try {
    Write-Host "Fetching terminals..."
    $response = Invoke-RestMethod -Uri "https://uexcorp.space/api/terminals"
    Write-Host "Terminals fetched. Count: $($response.data.Count)"
    
    Write-Host "Scanning for missing star_system_name..."
    $response.data | ForEach-Object {
        if ($_.id -eq 21) {
             Write-Host "FOUND ID 21 IN TERMINALS LIST: Name='$($_.name)', System='$($_.star_system_name)'"
        }
        if ([string]::IsNullOrWhiteSpace($_.star_system_name)) {
            Write-Host "Terminal ID $($_.id) ($($_.name)) has missing/empty star_system_name"
        }
    }
    
    Write-Host "Fetching prices..."
    $prices = Invoke-RestMethod -Uri "https://uexcorp.space/api/commodities_prices_all"
    
    Write-Host "Scanning for price entries with unknown terminals..."
    $terminalIds = $response.data.id
    $unknownCount = 0
    
    # Specific IDs reported by user
    $targetIds = @(21, 89, 90, 436, 443)

    $prices.data | ForEach-Object {
        if ($null -ne $_.id_terminal -and $_.id_terminal -gt 0) {
            
            # Check for reported IDs specifically to identify them
            if ($targetIds -contains $_.id_terminal) {
                Write-Host "FOUND REPORTED ID $($_.id_terminal): Name='$($_.terminal_name)'"
            }

            if ($terminalIds -notcontains $_.id_terminal) {
                # distinct logging
            }
        }
    }
    
    Write-Host "Found $unknownCount price entries with unknown terminals."
    Write-Host "Done."
} catch {
    Write-Host "Error: $_"
}
