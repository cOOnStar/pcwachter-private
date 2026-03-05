param(
  [string]$Repo=".",
  [string]$Out=".\_inventory_out",
  [string]$Frontend=""
)

$py = "python"
if (Get-Command python -ErrorAction SilentlyContinue) { $py = "python" }
elseif (Get-Command py -ErrorAction SilentlyContinue) { $py = "py -3" }
else { throw "Python not found. Install Python 3.10+ first." }

$cmd = "$py .\tools\pcw-inventory\generate_inventory.py --repo $Repo --out $Out"
if ($Frontend -ne "") { $cmd += " --frontend $Frontend" }

Write-Host "Running: $cmd"
Invoke-Expression $cmd
