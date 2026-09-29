param([string] $Filter)

# 현재 작업 폴더 대신 스크립트 위치를 기준으로 테스트 프로젝트를 찾는다.
$ErrorActionPreference = 'Stop'
$testProject = Join-Path $PSScriptRoot '..\tests\FieldLink.Communication.TestRunner\FieldLink.Communication.TestRunner.csproj'
$testProject = (Resolve-Path -LiteralPath $testProject).Path
$testArguments = @('run', '--project', $testProject, '-c', 'Release', '--', '--protocols')
if ($Filter) { $testArguments += $Filter }
& dotnet @testArguments
exit $LASTEXITCODE
