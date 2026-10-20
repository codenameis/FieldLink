using System;
using System.Collections.Generic;

namespace FieldLink.Plc.Protocols.Shared;

/// <summary>장치 한도를 초과하는 읽기·쓰기 요청을 여러 개의 안전한 단위로 분할하는 계획기입니다.</summary>
public static class RequestChunker
{
    /// <summary>분할된 단일 요청의 원소 오프셋과 원소 수입니다.</summary>
    public readonly struct Chunk
    {
        /// <summary>청크 정보를 초기화합니다.</summary>
        public Chunk(int elementOffset, int elementCount)
        {
            ElementOffset = elementOffset;
            ElementCount = elementCount;
        }

        /// <summary>전체 요청 내에서의 시작 원소 오프셋입니다.</summary>
        public int ElementOffset { get; }

        /// <summary>이 청크에서 처리할 원소 수입니다.</summary>
        public int ElementCount { get; }
    }

    /// <summary>다중 워드 자료형이 분할 경계에서 잘리지 않도록 안전한 요청 한도를 계산합니다.</summary>
    /// <param name="rawWordLimit">프로토콜 규격의 최대 워드 수 한도입니다.</param>
    /// <param name="wordsPerElement">원소 1개당 워드 수입니다. (예: 32비트=2, 64비트=4, 16비트/비트=1)</param>
    /// <returns>원소 경계에 정렬된 최대 원소 수입니다.</returns>
    public static int CalculateElementLimit(int rawWordLimit, int wordsPerElement)
    {
        if (rawWordLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawWordLimit), rawWordLimit, "한도는 1 이상이어야 합니다.");
        }

        if (wordsPerElement <= 1)
        {
            return rawWordLimit;
        }

        int alignedWords = rawWordLimit - (rawWordLimit % wordsPerElement);
        return alignedWords / wordsPerElement;
    }

    /// <summary>전체 원소 수와 청크당 최대 원소 수에 맞춰 순차 청크 목록을 만듭니다.</summary>
    /// <param name="totalCount">처리할 총 원소 수입니다.</param>
    /// <param name="maxElementsPerChunk">청크 하나당 최대 원소 수입니다.</param>
    /// <returns>순서대로 실행할 청크 목록입니다.</returns>
    public static List<Chunk> Plan(int totalCount, int maxElementsPerChunk)
    {
        if (totalCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "원소 수는 1 이상이어야 합니다.");
        }

        if (maxElementsPerChunk <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxElementsPerChunk), maxElementsPerChunk, "청크 한도는 1 이상이어야 합니다.");
        }

        var chunks = new List<Chunk>((totalCount + maxElementsPerChunk - 1) / maxElementsPerChunk);
        int currentOffset = 0;

        while (currentOffset < totalCount)
        {
            int remaining = totalCount - currentOffset;
            int count = Math.Min(remaining, maxElementsPerChunk);

            chunks.Add(new Chunk(currentOffset, count));
            currentOffset += count;
        }

        return chunks;
    }
}
