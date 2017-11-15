$versionFile = Get-Content "version.json" | ConvertFrom-Json
$versionFile.BuildTime = Get-Date ((Get-Date).ToUniversalTime()) -UFormat "%d-%m-%Y"
$versionFile.Build += 1
$versionFile.VersionString = "{0}.{1}.{2}-{3}.{4}+{5}" -f $versionFile.Major,$versionFile.Minor,$versionFile.Patch,$versionFile.Environment,$versionFile.Build,$versionFile.BuildTime
ConvertTo-Json $versionFile | Set-Content "version.json"