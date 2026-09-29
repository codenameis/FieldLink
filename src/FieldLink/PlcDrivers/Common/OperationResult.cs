using System;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>프로토콜 처리의 성공 여부, 오류 설명 및 결과 데이터를 전달합니다.</summary>
    /// <remarks>IsSuccess가 true이면 결과 데이터를 사용합니다. false이면 ErrorCode와 Message를 확인합니다.</remarks>
    public class OperationResult
    {
        /// <summary>기본 결과 객체를 생성합니다.</summary>
        public OperationResult()
        {
        }

        /// <summary>지정한 메시지로 기본 결과 객체를 생성합니다.</summary>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(string msg)
        {
            this.Message = msg;
        }

        /// <summary>오류 코드와 메시지 텍스트로 객체를 생성합니다.</summary>
        /// <param name = "err">오류 코드</param>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(int err, string msg)
        {
            this.ErrorCode = err;
            this.Message = msg;
        }

        /// <summary>작업 성공 여부입니다.</summary>
        public bool IsSuccess { get; set; }
        /// <summary>오류에 대한 설명입니다.</summary>
        public string Message { get; set; } = ProtocolMessages.UnknownError;
        /// <summary>특정 오류 코드</summary>
        /// <remarks>장치가 반환한 오류 코드를 보존합니다. 코드만으로 원인을 확인할 수 없으면 Message를 함께 확인합니다.</remarks>
        public int ErrorCode { get; set; } = 10000;

        /// <summary>간편 장치 API가 제공하는 선택적 실패 원인과 분할 작업 진행 정보입니다.</summary>
        public OperationFailureDetails FailureDetails { get; set; }

        /// <summary>잘못된 코드와 텍스트 설명을 얻으세요.</summary>
        /// <returns>오류 코드와 오류 메시지가 포함되어 있습니다.</returns>
        public string ToMessageShowString() => $"{ProtocolMessages.ErrorCode}:{ErrorCode}{Environment.NewLine}{ProtocolMessages.TextDescription}:{Message}";
        /// <summary>다른 결과 클래스에서 복사된 오류 정보는 주로 오류 코드와 오류 메시지를 대상으로 합니다.</summary>
        /// <param name = "result">결과 클래스와 파생 클래스의 객체</param>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public void CopyErrorFromOther<TResult>(TResult result)
            where TResult : OperationResult
        {
            if (result != null)
            {
                ErrorCode = result.ErrorCode;
                Message = result.Message;
                FailureDetails = result.FailureDetails;
            }
        }

        /// <summary>현재 결과 객체를 지정한 제네릭 형식의 결과 객체로 변환합니다. 현재 결과가 실패이면 지정한 제네릭 형식의 실패 결과 객체를 반환합니다.</summary>
        /// <param name = "content">만약 이 작업이 성공한다면</param>
        /// <returns>최종 결과 객체</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T> Convert<T>(T content) => IsSuccess ? OperationResult.CreateSuccessResult(content) : OperationResult.CreateFailedResult<T>(this);
        /// <summary>현재 결과 객체를 지정한 제네릭 형식의 결과 객체로 변환하여 해당 형식의 실패 결과 객체를 즉시 반환합니다.</summary>
        /// <returns>최종 실패 결과 객체</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T> ConvertFailed<T>() => OperationResult.CreateFailedResult<T>(this);
        /// <summary>현재 결과 객체를 지정한 제네릭 형식의 결과 객체로 변환합니다. 현재 결과가 실패이면 지정한 제네릭 형식의 실패 결과 객체를 반환합니다.</summary>
        /// <param name = "content1">작업 성공 시 할당할 결과 내용 1</param>
        /// <param name = "content2">작업 성공 시 할당할 결과 내용 2</param>
        /// <returns>최종 결과 객체</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T1, T2> Convert<T1, T2>(T1 content1, T2 content2) => IsSuccess ? OperationResult.CreateSuccessResult(content1, content2) : OperationResult.CreateFailedResult<T1, T2>(this);
        /// <summary>현재 결과 객체를 지정한 제네릭 형식의 결과 객체로 변환하여 해당 형식의 실패 결과 객체를 즉시 반환합니다.</summary>
        /// <returns>최종 실패 결과 객체</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T1, T2> ConvertFailed<T1, T2>() => OperationResult.CreateFailedResult<T1, T2>(this);
        /// <summary>현재 결과 객체를 지정한 제네릭 형식의 결과 객체로 변환합니다. 현재 결과가 실패이면 지정한 제네릭 형식의 실패 결과 객체를 반환합니다.</summary>
        /// <param name = "content1">작업 성공 시 할당할 결과 내용 1</param>
        /// <param name = "content2">작업 성공 시 할당할 결과 내용 2</param>
        /// <param name = "content3">작업 성공 시 할당할 결과 내용 3</param>
        /// <returns>최종 결과 객체</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T1, T2, T3> Convert<T1, T2, T3>(T1 content1, T2 content2, T3 content3) => IsSuccess ? OperationResult.CreateSuccessResult(content1, content2, content3) : OperationResult.CreateFailedResult<T1, T2, T3>(this);
        /// <summary>현재 결과 객체를 지정한 제네릭 형식의 결과 객체로 변환하여 해당 형식의 실패 결과 객체를 즉시 반환합니다.</summary>
        /// <returns>최종 실패 결과 객체</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T1, T2, T3> ConvertFailed<T1, T2, T3>() => OperationResult.CreateFailedResult<T1, T2, T3>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        public OperationResult Then(Func<OperationResult> func) => IsSuccess ? func() : this;
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T> Then<T>(Func<OperationResult<T>> func) => IsSuccess ? func() : CreateFailedResult<T>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T1, T2> Then<T1, T2>(Func<OperationResult<T1, T2>> func) => IsSuccess ? func() : CreateFailedResult<T1, T2>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<T1, T2, T3> Then<T1, T2, T3>(Func<OperationResult<T1, T2, T3>> func) => IsSuccess ? func() : CreateFailedResult<T1, T2, T3>(this);
        /// <summary>다른 결과 객체의 오류 정보를 복사한 실패 결과 객체를 생성하여 반환합니다.</summary>
        /// <param name = "result">이전 결과 객체</param>
        /// <returns>제네릭 기본값 객체가 포함된 실패 결과 클래스</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<T> CreateFailedResult<T>(OperationResult result)
        {
            return new OperationResult<T>()
            {
                ErrorCode = result.ErrorCode,
                Message = result.Message,
                FailureDetails = result.FailureDetails,
            };
        }

        /// <summary>다른 결과 객체의 오류 정보를 복사한 실패 결과 객체를 생성하여 반환합니다.</summary>
        /// <param name = "result">이전 결과 객체</param>
        /// <returns>제네릭 기본값 객체가 포함된 실패 결과 클래스</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<T1, T2> CreateFailedResult<T1, T2>(OperationResult result)
        {
            return new OperationResult<T1, T2>()
            {
                ErrorCode = result.ErrorCode,
                Message = result.Message,
                FailureDetails = result.FailureDetails,
            };
        }

        /// <summary>다른 결과 객체의 오류 정보를 복사한 실패 결과 객체를 생성하여 반환합니다.</summary>
        /// <param name = "result">이전 결과 객체</param>
        /// <returns>제네릭 기본값 객체가 포함된 실패 결과 클래스</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<T1, T2, T3> CreateFailedResult<T1, T2, T3>(OperationResult result)
        {
            return new OperationResult<T1, T2, T3>()
            {
                ErrorCode = result.ErrorCode,
                Message = result.Message,
                FailureDetails = result.FailureDetails,
            };
        }

        /// <summary>성공한 결과 객체를 생성하고 반환합니다.</summary>
        /// <returns>성공의 대상이</returns>
        public static OperationResult CreateSuccessResult()
        {
            return new OperationResult()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = ProtocolMessages.SuccessText,
            };
        }

        /// <summary>성공한 결과 객체를 생성하고 반환합니다.</summary>
        /// <param name = "value">타입의 값 객체</param>
        /// <returns>성공의 대상이</returns>
        /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<T> CreateSuccessResult<T>(T value)
        {
            return new OperationResult<T>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = ProtocolMessages.SuccessText,
                Content = value
            };
        }

        /// <summary>성공한 결과 객체를 생성하고 반환합니다. 두 개의 변수 객체와 함께</summary>
        /// <param name = "value1">형식 1의 객체</param>
        /// <param name = "value2">형식 2의 객체</param>
        /// <returns>생성된 결과 객체</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<T1, T2> CreateSuccessResult<T1, T2>(T1 value1, T2 value2)
        {
            return new OperationResult<T1, T2>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = ProtocolMessages.SuccessText,
                Content1 = value1,
                Content2 = value2,
            };
        }

        /// <summary>성공한 결과 객체를 생성하고 반환합니다.</summary>
        /// <param name = "value1">형식 1의 객체</param>
        /// <param name = "value2">형식 2의 객체</param>
        /// <param name = "value3">형식 3의 객체</param>
        /// <returns>생성된 결과 객체</returns>
        /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "T3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<T1, T2, T3> CreateSuccessResult<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            return new OperationResult<T1, T2, T3>()
            {
                IsSuccess = true,
                ErrorCode = 0,
                Message = ProtocolMessages.SuccessText,
                Content1 = value1,
                Content2 = value2,
                Content3 = value3,
            };
        }
    }

    /// <summary>프로토콜 처리의 성공 여부, 오류 설명 및 결과 데이터를 전달합니다.</summary>
    /// <typeparam name = "T">결과 또는 변환 데이터의 형식입니다.</typeparam>
    public class OperationResult<T> : OperationResult
    {
        /// <summary>기본 결과 객체를 생성합니다.</summary>
        public OperationResult() : base()
        {
        }

        /// <summary>지정한 메시지로 기본 결과 객체를 생성합니다.</summary>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(string msg) : base(msg)
        {
        }

        /// <summary>오류 코드와 메시지 텍스트로 객체를 생성합니다.</summary>
        /// <param name = "err">오류 코드</param>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(int err, string msg) : base(err, msg)
        {
        }

        /// <summary>사용자 정의 제네릭 데이터</summary>
        public T Content { get; set; }

        /// <summary>사용자 지정 데이터 검사를 수행할 수 있는 검사 결과 객체를 반환합니다.</summary>
        /// <param name = "check">검사에 사용할 델리게이트 메서드</param>
        /// <param name = "message">검사 실패 시 오류 메시지</param>
        /// <returns>검사가 성공하면 객체 자체를 반환하고, 실패하면 오류 정보를 반환합니다.</returns>
        public OperationResult<T> Check(Func<T, bool> check, string message = "All content data check failed")
        {
            if (!IsSuccess)
                return this;
            if (check(Content))
                return this;
            return new OperationResult<T>(message);
        }

        /// <summary>사용자 지정 데이터 검사를 수행할 수 있는 검사 결과 객체를 반환합니다.</summary>
        /// <param name = "check">검사에 사용할 델리게이트 메서드</param>
        /// <returns>검사가 성공하면 객체 자체를 반환하고, 실패하면 오류 정보를 반환합니다.</returns>
        public OperationResult<T> Check(Func<T, OperationResult> check)
        {
            if (!IsSuccess)
                return this;
            OperationResult checkResult = check(Content);
            if (!checkResult.IsSuccess)
                return OperationResult.CreateFailedResult<T>(checkResult);
            return this;
        }

        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        public OperationResult Then(Func<T, OperationResult> func) => IsSuccess ? func(Content) : this;
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult> Then<TResult>(Func<T, OperationResult<TResult>> func) => IsSuccess ? func(Content) : CreateFailedResult<TResult>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult1, TResult2> Then<TResult1, TResult2>(Func<T, OperationResult<TResult1, TResult2>> func) => IsSuccess ? func(Content) : CreateFailedResult<TResult1, TResult2>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult1, TResult2, TResult3> Then<TResult1, TResult2, TResult3>(Func<T, OperationResult<TResult1, TResult2, TResult3>> func) => IsSuccess ? func(Content) : CreateFailedResult<TResult1, TResult2, TResult3>(this);
    }

    /// <summary>프로토콜 처리의 성공 여부, 오류 설명 및 결과 데이터를 전달합니다.</summary>
    /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
    /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
    public class OperationResult<T1, T2> : OperationResult
    {
        /// <summary>기본 결과 객체를 생성합니다.</summary>
        public OperationResult() : base()
        {
        }

        /// <summary>지정한 메시지로 기본 결과 객체를 생성합니다.</summary>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(string msg) : base(msg)
        {
        }

        /// <summary>오류 코드와 메시지 텍스트로 객체를 생성합니다.</summary>
        /// <param name = "err">오류 코드</param>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(int err, string msg) : base(err, msg)
        {
        }

        /// <summary>사용자 지정 제네릭 데이터 1</summary>
        public T1 Content1 { get; set; }
        /// <summary>사용자 지정 제네릭 데이터 2</summary>
        public T2 Content2 { get; set; }

        /// <summary>사용자 지정 데이터 검사를 수행할 수 있는 검사 결과 객체를 반환합니다.</summary>
        /// <param name = "check">검사에 사용할 델리게이트 메서드</param>
        /// <param name = "message">자유로이 지정할 수 있는 잘못된 정보</param>
        /// <returns>검사가 성공하면 객체 자체를 반환하고, 실패하면 오류 정보를 반환합니다.</returns>
        public OperationResult<T1, T2> Check(Func<T1, T2, bool> check, string message = "All content data check failed")
        {
            if (!IsSuccess)
                return this;
            if (check(Content1, Content2))
                return this;
            return new OperationResult<T1, T2>(message);
        }

        /// <summary>사용자 지정 데이터 검사를 수행할 수 있는 검사 결과 객체를 반환합니다.</summary>
        /// <param name = "check">검사에 사용할 델리게이트 메서드</param>
        /// <returns>검사가 성공하면 객체 자체를 반환하고, 실패하면 오류 정보를 반환합니다.</returns>
        public OperationResult<T1, T2> Check(Func<T1, T2, OperationResult> check)
        {
            if (!IsSuccess)
                return this;
            OperationResult checkResult = check(Content1, Content2);
            if (!checkResult.IsSuccess)
                return OperationResult.CreateFailedResult<T1, T2>(checkResult);
            return this;
        }

        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        public OperationResult Then(Func<T1, T2, OperationResult> func) => IsSuccess ? func(Content1, Content2) : this;
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult> Then<TResult>(Func<T1, T2, OperationResult<TResult>> func) => IsSuccess ? func(Content1, Content2) : CreateFailedResult<TResult>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult1, TResult2> Then<TResult1, TResult2>(Func<T1, T2, OperationResult<TResult1, TResult2>> func) => IsSuccess ? func(Content1, Content2) : CreateFailedResult<TResult1, TResult2>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult1, TResult2, TResult3> Then<TResult1, TResult2, TResult3>(Func<T1, T2, OperationResult<TResult1, TResult2, TResult3>> func) => IsSuccess ? func(Content1, Content2) : CreateFailedResult<TResult1, TResult2, TResult3>(this);
    }

    /// <summary>프로토콜 처리의 성공 여부, 오류 설명 및 결과 데이터를 전달합니다.</summary>
    /// <typeparam name = "T1">결과 또는 변환 데이터의 형식입니다.</typeparam>
    /// <typeparam name = "T2">결과 또는 변환 데이터의 형식입니다.</typeparam>
    /// <typeparam name = "T3">결과 또는 변환 데이터의 형식입니다.</typeparam>
    public class OperationResult<T1, T2, T3> : OperationResult
    {
        /// <summary>기본 결과 객체를 생성합니다.</summary>
        public OperationResult() : base()
        {
        }

        /// <summary>지정한 메시지로 기본 결과 객체를 생성합니다.</summary>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(string msg) : base(msg)
        {
        }

        /// <summary>오류 코드와 메시지 텍스트로 객체를 생성합니다.</summary>
        /// <param name = "err">오류 코드</param>
        /// <param name = "msg">오류 메시지</param>
        public OperationResult(int err, string msg) : base(err, msg)
        {
        }

        /// <summary>사용자 지정 제네릭 데이터 1</summary>
        public T1 Content1 { get; set; }
        /// <summary>사용자 지정 제네릭 데이터 2</summary>
        public T2 Content2 { get; set; }
        /// <summary>사용자 지정 제네릭 데이터 3</summary>
        public T3 Content3 { get; set; }

        /// <summary>사용자 지정 데이터 검사를 수행할 수 있는 검사 결과 객체를 반환합니다.</summary>
        /// <param name = "check">검사에 사용할 델리게이트 메서드</param>
        /// <param name = "message">검사 실패 시 오류 메시지</param>
        /// <returns>검사가 성공하면 객체 자체를 반환하고, 실패하면 오류 정보를 반환합니다.</returns>
        public OperationResult<T1, T2, T3> Check(Func<T1, T2, T3, bool> check, string message = "All content data check failed")
        {
            if (!IsSuccess)
                return this;
            if (check(Content1, Content2, Content3))
                return this;
            return new OperationResult<T1, T2, T3>(message);
        }

        /// <summary>사용자 지정 데이터 검사를 수행할 수 있는 검사 결과 객체를 반환합니다.</summary>
        /// <param name = "check">검사에 사용할 델리게이트 메서드</param>
        /// <returns>검사가 성공하면 객체 자체를 반환하고, 실패하면 오류 정보를 반환합니다.</returns>
        public OperationResult<T1, T2, T3> Check(Func<T1, T2, T3, OperationResult> check)
        {
            if (!IsSuccess)
                return this;
            OperationResult checkResult = check(Content1, Content2, Content3);
            if (!checkResult.IsSuccess)
                return OperationResult.CreateFailedResult<T1, T2, T3>(checkResult);
            return this;
        }

        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        public OperationResult Then(Func<T1, T2, T3, OperationResult> func) => IsSuccess ? func(Content1, Content2, Content3) : this;
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult> Then<TResult>(Func<T1, T2, T3, OperationResult<TResult>> func) => IsSuccess ? func(Content1, Content2, Content3) : CreateFailedResult<TResult>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult1, TResult2> Then<TResult1, TResult2>(Func<T1, T2, T3, OperationResult<TResult1, TResult2>> func) => IsSuccess ? func(Content1, Content2, Content3) : CreateFailedResult<TResult1, TResult2>(this);
        /// <summary>다음에 실행할 작업을 지정합니다. 현재 객체가 성공 상태이면 다음 작업의 실행 결과를 반환하고, 실패 상태이면 현재 객체 자체를 반환합니다.</summary>
        /// <param name = "func">현재 객체가 성공한 후 실행할 작업</param>
        /// <returns>전체 메서드 체인의 최종 성공 또는 실패 결과를 반환합니다.</returns>
        /// <typeparam name = "TResult1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TResult3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public OperationResult<TResult1, TResult2, TResult3> Then<TResult1, TResult2, TResult3>(Func<T1, T2, T3, OperationResult<TResult1, TResult2, TResult3>> func) => IsSuccess ? func(Content1, Content2, Content3) : CreateFailedResult<TResult1, TResult2, TResult3>(this);
    }
}
