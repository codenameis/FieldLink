using System;
using System.Collections.Generic;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

internal static class OpcUaBatchSnapshot
{
    private const long BaseBatchOverheadBytes = 256;
    private const long BaseItemOverheadBytes = 128;
    private const long MinimumStringArrayElementOverheadBytes = 32;
    private const long GenericArrayElementEstimatedBytes = 16;
    private const long MinimumVariantBytes = 32;
    private const long Utf16CharacterBytes = sizeof(char);

    public static bool TryBuild(
        OpcUaRawDataChangeBatch rawBatch,
        IReadOnlyDictionary<uint, (string Key, NodeId NodeId)> itemsByClientHandle,
        out OpcUaDataChangeBatch? ownedBatch,
        out long estimatedBytes)
    {
        List<OpcUaDataChangeItem> items = new(rawBatch.Items.Count);
        long bytes = BaseBatchOverheadBytes;

        for (int i = 0; i < rawBatch.Items.Count; i++)
        {
            OpcUaRawDataChangeItem rawItem = rawBatch.Items[i];
            if (!itemsByClientHandle.TryGetValue(rawItem.ClientHandle, out (string Key, NodeId NodeId) metadata))
            {
                continue;
            }

            DataValue clonedValue = new(rawItem.Value);
            bool overflow = clonedValue.StatusCode.Overflow;
            bytes += BaseItemOverheadBytes + EstimateVariantBytes(clonedValue.WrappedValue);
            items.Add(new OpcUaDataChangeItem(metadata.Key, metadata.NodeId, clonedValue, overflow));
        }

        if (items.Count == 0)
        {
            ownedBatch = null;
            estimatedBytes = 0;
            return false;
        }

        ownedBatch = new OpcUaDataChangeBatch(rawBatch.SequenceNumber, rawBatch.PublishTime, items);
        estimatedBytes = bytes;
        return true;
    }

    private static long EstimateVariantBytes(Variant variant)
    {
        if (variant == Variant.Null || variant.Value is null)
        {
            return 0;
        }

        return variant.Value switch
        {
            byte[] bytes => bytes.Length,
            sbyte[] signedBytes => signedBytes.Length,
            bool[] booleans => booleans.Length,
            short[] int16Values => int16Values.Length * sizeof(short),
            ushort[] uint16Values => uint16Values.Length * sizeof(ushort),
            int[] int32Values => int32Values.Length * sizeof(int),
            uint[] uint32Values => uint32Values.Length * sizeof(uint),
            long[] int64Values => int64Values.Length * sizeof(long),
            ulong[] uint64Values => uint64Values.Length * sizeof(ulong),
            float[] floats => floats.Length * sizeof(float),
            double[] doubles => doubles.Length * sizeof(double),
            string text => text.Length * Utf16CharacterBytes,
            string[] strings => EstimateStringArrayBytes(strings),
            byte[][] byteArrays => EstimateByteArrayArrayBytes(byteArrays),
            Variant[] variants => EstimateVariantArrayBytes(variants),
            Array genericArray => Math.Max(MinimumVariantBytes, genericArray.Length * GenericArrayElementEstimatedBytes),
            _ => MinimumVariantBytes
        };
    }

    private static long EstimateStringArrayBytes(string[] strings)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < strings.Length; i++)
        {
            total += MinimumStringArrayElementOverheadBytes + ((strings[i]?.Length ?? 0) * Utf16CharacterBytes);
        }

        return total;
    }

    private static long EstimateByteArrayArrayBytes(byte[][] byteArrays)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < byteArrays.Length; i++)
        {
            total += MinimumStringArrayElementOverheadBytes + (byteArrays[i]?.Length ?? 0);
        }

        return total;
    }

    private static long EstimateVariantArrayBytes(Variant[] variants)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < variants.Length; i++)
        {
            total += GenericArrayElementEstimatedBytes + EstimateVariantBytes(variants[i]);
        }

        return total;
    }
}
