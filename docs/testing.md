# 빌드와 테스트

제품 및 테스트 라이브러리는 `netstandard2.0`, 실행 러너는 `net48`을 대상으로 한다. Windows, .NET Framework 4.8 개발자 팩/런타임, C# 12와 `.slnx`를 지원하는 .NET SDK가 필요하다.

저장소 루트에서 실행한다.

```powershell
dotnet build FieldLink.slnx -c Release
dotnet run --project tests/FieldLink.Communication.TestRunner/FieldLink.Communication.TestRunner.csproj -c Release --no-build
```

러너는 실패하거나 필터와 일치하는 검사가 없으면 종료 코드 1을 반환한다. `dotnet test`는 이 콘솔 러너를 대신하지 않는다. `--no-build`는 앞선 빌드가 성공했을 때 사용한다.

이름 일부로 범위를 줄일 수 있다.

```powershell
dotnet run --project tests/FieldLink.Communication.TestRunner/FieldLink.Communication.TestRunner.csproj -c Release --no-build -- Standards
dotnet run --project tests/FieldLink.Communication.TestRunner/FieldLink.Communication.TestRunner.csproj -c Release --no-build -- Robot
```

## 기준 자료

`tests/FieldLink.Communication.Tests/Contracts/`의 다음 파일은 설명서가 아니라 테스트 입력이다. 문서 정리 과정에서 기존 Git 기준 내용 그대로 이동했다.

- `public-api.txt`: 공개 API 스냅샷. 서명 변경을 검출한다.
- `fixture-integrity.json`: 골든 패킷 파일의 정규화된 텍스트 해시. 기준 파일이 몰래 바뀌지 않았는지 검사한다.
- `fanuc-member-migration.json`: FANUC 모델 멤버 대응과 타입 검증 자료.

프로젝트는 이들을 실행 경로의 `Contracts/`에 복사한다. 원본 골든 데이터는 `Fixtures/`에 있다. 골든 벡터는 과거 구현의 동작 기록이며 공식 규격 준수의 증명이 아니다. 이번 규격 교정으로 바뀐 KUKA/YRC 사례는 원래 입력과 기대값을 그대로 검사한 뒤, 출처가 명시된 새 결과도 정확히 비교한다. 벡터를 제외하거나 기준 JSON/해시를 재생성하지 않았다.

## 검증 범위

테스트는 메모리·로컬 루프백·가상 Serial 전송을 사용한다. 실제 PLC/로봇/COM 포트의 설정, 케이블, 펌웨어, 응답 지연과 물리 타이밍은 검증하지 않는다. 테스트 개수와 실제 실행 결과는 [공식 문서 검증 보고서](standards/README.md)를 참고한다.
