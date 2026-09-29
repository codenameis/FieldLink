param(
    [Parameter(Mandatory = $true)][string] $SourceRoot,
    [string] $OutputPath,
    [string] $NewtonsoftAssemblyPath
)

# 이식한 코드를 사용하지 않고 원본 소스를 컴파일하여 기준 데이터를 만든다.
$ErrorActionPreference = 'Stop'
$sourceDirectory = (Resolve-Path -LiteralPath $SourceRoot).Path
$repositoryDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
if (-not $OutputPath) {
    $OutputPath = Join-Path $repositoryDirectory 'tests\FieldLink.Communication.Tests\Fixtures\robot-golden.json'
}
if (-not $NewtonsoftAssemblyPath) {
    $NewtonsoftAssemblyPath = Join-Path $sourceDirectory 'bin\Debug\Newtonsoft.Json.dll'
}
$jsonAssembly = (Resolve-Path -LiteralPath $NewtonsoftAssemblyPath).Path
$frameworkDirectory = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
$references = @(Get-ChildItem -LiteralPath $frameworkDirectory -Filter '*.dll' |
    Where-Object { $_.Name -notin @('System.EnterpriseServices.Wrapper.dll', 'System.EnterpriseServices.Thunk.dll') } |
    ForEach-Object { '/r:"' + $_.FullName + '"' })
$references += '/r:"' + $jsonAssembly + '"'
$dotnetCommand = (Get-Command dotnet -ErrorAction Stop).Source
$sdkVersion = (& $dotnetCommand --version).Trim()
$compilerPath = Join-Path (Split-Path $dotnetCommand -Parent) "sdk\$sdkVersion\Roslyn\bincore\csc.dll"
$workDirectory = Join-Path $env:TEMP ('fieldlink-robot-oracle-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workDirectory | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Invoke-OracleCompilation([string] $Name, [string[]] $CompilerArguments) {
    $responseFile = Join-Path $workDirectory ($Name + '.rsp')
    [IO.File]::WriteAllLines($responseFile, $CompilerArguments, $utf8)
    & $dotnetCommand $compilerPath ('@' + $responseFile)
    if ($LASTEXITCODE -ne 0) { throw "$Name 원본 기준 컴파일 실패: $LASTEXITCODE" }
}

$originalSources = @(Get-ChildItem -LiteralPath $sourceDirectory -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(?:bin|obj)\\' } |
    ForEach-Object { '"' + $_.FullName + '"' })
$baselinePath = Join-Path $workDirectory 'ProtocolReferenceBaseline.dll'
$generatorPath = Join-Path $workDirectory 'Golden.exe'
Invoke-OracleCompilation 'baseline' (@('/nologo', '/target:library', '/langversion:latest', '/define:NET451', '/unsafe', '/nostdlib+', ('/out:"' + $baselinePath + '"')) + $references + $originalSources)
$generatorSources = @('GenerateGolden.cs') | ForEach-Object { '"' + (Join-Path $PSScriptRoot $_) + '"' }
Invoke-OracleCompilation 'generator' (@('/nologo', '/target:exe', '/langversion:latest', '/nostdlib+', ('/out:"' + $generatorPath + '"')) + $references + $generatorSources)
Copy-Item -LiteralPath $jsonAssembly -Destination (Join-Path $workDirectory 'Newtonsoft.Json.dll')
& $generatorPath $sourceDirectory $OutputPath
if ($LASTEXITCODE -ne 0) { throw "원본 기준 데이터 생성 실패: $LASTEXITCODE" }
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
