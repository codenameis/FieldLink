using System;
using System.Collections.Generic;
using Xunit;

namespace FieldLink.Plc.Protocols.Shared.Tests;

public sealed class RequestChunkerTests
{
    [Fact]
    public void CalculateElementLimit_MultiWord_AlignsToElementBoundary()
    {
        // 32비트 값(워드 2개): 규격 한도 125 워드 -> 124 워드 (원소 62개) (사양서 §4, D3)
        int elements = RequestChunker.CalculateElementLimit(rawWordLimit: 125, wordsPerElement: 2);
        Assert.Equal(62, elements);

        // 64비트 값(워드 4개): 규격 한도 125 워드 -> 124 워드 (원소 31개)
        int elements64 = RequestChunker.CalculateElementLimit(rawWordLimit: 125, wordsPerElement: 4);
        Assert.Equal(31, elements64);

        // 단일 워드: 125
        int single = RequestChunker.CalculateElementLimit(rawWordLimit: 125, wordsPerElement: 1);
        Assert.Equal(125, single);
    }

    [Fact]
    public void Plan_WhenTotalCountWithinLimit_CreatesSingleChunk()
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalCount: 50, maxElementsPerChunk: 100);

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].ElementOffset);
        Assert.Equal(50, chunks[0].ElementCount);
    }

    [Fact]
    public void Plan_WhenTotalCountExceedsLimit_SplitsIntoMultipleChunks()
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalCount: 250, maxElementsPerChunk: 100);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(0, chunks[0].ElementOffset);
        Assert.Equal(100, chunks[0].ElementCount);

        Assert.Equal(100, chunks[1].ElementOffset);
        Assert.Equal(100, chunks[1].ElementCount);

        Assert.Equal(200, chunks[2].ElementOffset);
        Assert.Equal(50, chunks[2].ElementCount);
    }

    [Fact]
    public void Plan_InvalidArguments_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestChunker.Plan(totalCount: 0, maxElementsPerChunk: 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestChunker.Plan(totalCount: 10, maxElementsPerChunk: 0));
    }
}
