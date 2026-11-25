using System;
using System.Collections.Generic;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

internal static partial class OpcUaBatchSnapshot
{
    private const long BaseBatchOverheadBytes = 256;
    private const long BaseItemOverheadBytes = 128;
    private const long MinimumStringArrayElementOverheadBytes = 32;
    private const long GenericArrayElementEstimatedBytes = 16;
    private const long MinimumVariantBytes = 32;
    private const long GuidByteSize = 16;
    private const long Utf16CharacterBytes = sizeof(char);
    private const int MaxEstimationDepth = 16;

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
            if (rawItem.Value is null ||
                !itemsByClientHandle.TryGetValue(rawItem.ClientHandle, out (string Key, NodeId NodeId) metadata))
            {
                continue;
            }

            DataValue clonedValue = new(rawItem.Value);
            bool overflow = clonedValue.StatusCode.Overflow;
            long itemBytes = AddSaturating(BaseItemOverheadBytes, EstimateVariantBytes(clonedValue.WrappedValue, 0));
            bytes = AddSaturating(bytes, itemBytes);
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

    public static long EstimateRawBatchBytes(OpcUaRawDataChangeBatch rawBatch)
    {
        long bytes = BaseBatchOverheadBytes;
        for (int i = 0; i < rawBatch.Items.Count; i++)
        {
            DataValue? value = rawBatch.Items[i].Value;
            if (value is null)
            {
                continue;
            }

            long itemBytes = AddSaturating(BaseItemOverheadBytes, EstimateVariantBytes(value.WrappedValue, 0));
            bytes = AddSaturating(bytes, itemBytes);
        }

        return bytes;
    }

    public static long AddSaturating(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return left + right;
        }

        return left >= long.MaxValue - right ? long.MaxValue : left + right;
    }

    private static long MultiplySaturating(long count, long elementSize)
    {
        if (count <= 0 || elementSize <= 0)
        {
            return 0;
        }

        return count >= long.MaxValue / elementSize ? long.MaxValue : count * elementSize;
    }

    private static long EstimateVariantBytes(Variant variant, int depth)
    {
        if (variant == Variant.Null || variant.Value is null)
        {
            return 0;
        }

        return EstimateValueBytes(variant.Value, depth);
    }

    private static long EstimateValueBytes(object? value, int depth)
    {
        if (value is null)
        {
            return 0;
        }

        if (depth >= MaxEstimationDepth)
        {
            return MinimumVariantBytes;
        }

        return value switch
        {
            byte[] bytes => bytes.Length,
            sbyte[] signedBytes => signedBytes.Length,
            bool[] booleans => booleans.Length,
            short[] int16Values => MultiplySaturating(int16Values.Length, sizeof(short)),
            ushort[] uint16Values => MultiplySaturating(uint16Values.Length, sizeof(ushort)),
            int[] int32Values => MultiplySaturating(int32Values.Length, sizeof(int)),
            uint[] uint32Values => MultiplySaturating(uint32Values.Length, sizeof(uint)),
            long[] int64Values => MultiplySaturating(int64Values.Length, sizeof(long)),
            ulong[] uint64Values => MultiplySaturating(uint64Values.Length, sizeof(ulong)),
            float[] floats => MultiplySaturating(floats.Length, sizeof(float)),
            double[] doubles => MultiplySaturating(doubles.Length, sizeof(double)),
            DateTime[] dateTimes => MultiplySaturating(dateTimes.Length, sizeof(long)),
            Guid[] guids => MultiplySaturating(guids.Length, GuidByteSize),
            Uuid[] uuids => MultiplySaturating(uuids.Length, GuidByteSize),
            StatusCode[] statusCodes => MultiplySaturating(statusCodes.Length, sizeof(uint)),
            string text => MultiplySaturating(text.Length, Utf16CharacterBytes),
            string[] strings => EstimateStringArrayBytes(strings),
            byte[][] byteArrays => EstimateByteArrayArrayBytes(byteArrays),
            Variant variantValue => EstimateVariantBytes(variantValue, depth + 1),
            Variant[] variants => EstimateVariantArrayBytes(variants, depth + 1),
            Matrix matrix => EstimateMatrixBytes(matrix, depth + 1),
            ExtensionObject extensionObject => EstimateExtensionObjectBytes(extensionObject, depth + 1),
            ExtensionObject[] extensionObjects => EstimateExtensionObjectArrayBytes(extensionObjects, depth + 1),
            LocalizedText localizedText => EstimateLocalizedTextBytes(localizedText),
            LocalizedText[] localizedTexts => EstimateLocalizedTextArrayBytes(localizedTexts),
            QualifiedName qualifiedName => EstimateQualifiedNameBytes(qualifiedName),
            QualifiedName[] qualifiedNames => EstimateQualifiedNameArrayBytes(qualifiedNames),
            System.Xml.XmlElement xmlElement => EstimateXmlElementBytes(xmlElement),
            System.Xml.XmlElement[] xmlElements => EstimateXmlElementArrayBytes(xmlElements),
            NodeId nodeId => EstimateNodeIdBytes(nodeId),
            NodeId[] nodeIds => EstimateNodeIdArrayBytes(nodeIds),
            ExpandedNodeId expandedNodeId => EstimateExpandedNodeIdBytes(expandedNodeId),
            ExpandedNodeId[] expandedNodeIds => EstimateExpandedNodeIdArrayBytes(expandedNodeIds),
            DataValue dataValue => EstimateDataValueBytes(dataValue, depth + 1),
            DataValue[] dataValues => EstimateDataValueArrayBytes(dataValues, depth + 1),
            object?[] objects => EstimateObjectArrayBytes(objects, depth + 1),
            Array genericArray => Math.Max(MinimumVariantBytes, MultiplySaturating(genericArray.LongLength, GenericArrayElementEstimatedBytes)),
            _ => MinimumVariantBytes
        };
    }
}
