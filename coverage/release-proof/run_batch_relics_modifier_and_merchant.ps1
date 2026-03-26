$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
dotnet run --project "$repoRoot\tools\Stage70FullGameProof\Stage70FullGameProof.csproj" -- --run-batch relics_modifier_and_merchant "$repoRoot"