// netstandard2.0에는 init 접근자와 record에 필요한 컴파일러 지원 타입이 없어 직접 공급한다.
// 이 파일은 Directory.Build.props가 모든 제품 프로젝트에 링크로 포함한다.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
