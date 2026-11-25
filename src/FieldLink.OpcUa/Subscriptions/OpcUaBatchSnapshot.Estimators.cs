using System.Collections.Generic;
using System.Xml;
using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

internal static partial class OpcUaBatchSnapshot
{
    private static long EstimateStringArrayBytes(string[] strings)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < strings.Length; i++)
        {
            long elementBytes = AddSaturating(
                MinimumStringArrayElementOverheadBytes,
                MultiplySaturating(strings[i]?.Length ?? 0, Utf16CharacterBytes));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateByteArrayArrayBytes(byte[][] byteArrays)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < byteArrays.Length; i++)
        {
            long elementBytes = AddSaturating(
                MinimumStringArrayElementOverheadBytes,
                byteArrays[i]?.Length ?? 0);
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateVariantArrayBytes(Variant[] variants, int depth)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < variants.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateVariantBytes(variants[i], depth));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateMatrixBytes(Matrix matrix, int depth)
    {
        long dimensionBytes = MultiplySaturating(matrix.Dimensions?.Length ?? 0, sizeof(int));
        long elementsBytes = EstimateValueBytes(matrix.Elements, depth);
        return AddSaturating(MinimumVariantBytes, AddSaturating(dimensionBytes, elementsBytes));
    }

    private static long EstimateExtensionObjectBytes(ExtensionObject? extensionObject, int depth)
    {
        if (extensionObject is null)
        {
            return MinimumVariantBytes;
        }

        long typeIdBytes = EstimateExpandedNodeIdBytes(extensionObject.TypeId);
        long bodyBytes = EstimateValueBytes(extensionObject.Body, depth);
        return AddSaturating(MinimumVariantBytes, AddSaturating(typeIdBytes, bodyBytes));
    }

    private static long EstimateExtensionObjectArrayBytes(ExtensionObject[] extensionObjects, int depth)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < extensionObjects.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateExtensionObjectBytes(extensionObjects[i], depth));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateLocalizedTextBytes(LocalizedText? localizedText)
    {
        if (localizedText is null)
        {
            return MinimumVariantBytes;
        }

        long localeBytes = MultiplySaturating(localizedText.Locale?.Length ?? 0, Utf16CharacterBytes);
        long textBytes = MultiplySaturating(localizedText.Text?.Length ?? 0, Utf16CharacterBytes);
        long total = AddSaturating(MinimumVariantBytes, AddSaturating(localeBytes, textBytes));

        if (localizedText.Translations is { Count: > 0 } translations)
        {
            foreach (KeyValuePair<string, string> entry in translations)
            {
                long entryBytes = AddSaturating(
                    MinimumStringArrayElementOverheadBytes,
                    MultiplySaturating((long)(entry.Key?.Length ?? 0) + (entry.Value?.Length ?? 0), Utf16CharacterBytes));
                total = AddSaturating(total, entryBytes);
            }
        }

        return total;
    }

    private static long EstimateLocalizedTextArrayBytes(LocalizedText[] localizedTexts)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < localizedTexts.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateLocalizedTextBytes(localizedTexts[i]));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateQualifiedNameBytes(QualifiedName? qualifiedName)
    {
        if (qualifiedName is null)
        {
            return MinimumVariantBytes;
        }

        return AddSaturating(
            MinimumVariantBytes,
            MultiplySaturating(qualifiedName.Name?.Length ?? 0, Utf16CharacterBytes));
    }

    private static long EstimateQualifiedNameArrayBytes(QualifiedName[] qualifiedNames)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < qualifiedNames.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateQualifiedNameBytes(qualifiedNames[i]));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateXmlElementBytes(XmlElement? xmlElement)
    {
        if (xmlElement is null)
        {
            return MinimumVariantBytes;
        }

        return AddSaturating(
            MinimumVariantBytes,
            MultiplySaturating(xmlElement.OuterXml?.Length ?? 0, Utf16CharacterBytes));
    }

    private static long EstimateXmlElementArrayBytes(XmlElement[] xmlElements)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < xmlElements.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateXmlElementBytes(xmlElements[i]));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateNodeIdBytes(NodeId? nodeId)
    {
        if (nodeId is null)
        {
            return MinimumVariantBytes;
        }

        long identifierBytes = nodeId.Identifier switch
        {
            string stringId => MultiplySaturating(stringId.Length, Utf16CharacterBytes),
            byte[] opaqueId => opaqueId.Length,
            _ => GuidByteSize
        };

        return AddSaturating(MinimumVariantBytes, identifierBytes);
    }

    private static long EstimateNodeIdArrayBytes(NodeId[] nodeIds)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < nodeIds.Length; i++)
        {
            long elementBytes = AddSaturating(GenericArrayElementEstimatedBytes, EstimateNodeIdBytes(nodeIds[i]));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateExpandedNodeIdBytes(ExpandedNodeId? expandedNodeId)
    {
        if (expandedNodeId is null)
        {
            return MinimumVariantBytes;
        }

        long namespaceBytes = MultiplySaturating(expandedNodeId.NamespaceUri?.Length ?? 0, Utf16CharacterBytes);
        long identifierBytes = expandedNodeId.Identifier switch
        {
            string stringId => MultiplySaturating(stringId.Length, Utf16CharacterBytes),
            byte[] opaqueId => opaqueId.Length,
            _ => GuidByteSize
        };

        return AddSaturating(MinimumVariantBytes, AddSaturating(namespaceBytes, identifierBytes));
    }

    private static long EstimateExpandedNodeIdArrayBytes(ExpandedNodeId[] expandedNodeIds)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < expandedNodeIds.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateExpandedNodeIdBytes(expandedNodeIds[i]));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateDataValueBytes(DataValue? dataValue, int depth)
    {
        if (dataValue is null)
        {
            return MinimumVariantBytes;
        }

        return AddSaturating(BaseItemOverheadBytes, EstimateVariantBytes(dataValue.WrappedValue, depth));
    }

    private static long EstimateDataValueArrayBytes(DataValue[] dataValues, int depth)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < dataValues.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateDataValueBytes(dataValues[i], depth));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }

    private static long EstimateObjectArrayBytes(object?[] objects, int depth)
    {
        long total = MinimumVariantBytes;
        for (int i = 0; i < objects.Length; i++)
        {
            long elementBytes = AddSaturating(
                GenericArrayElementEstimatedBytes,
                EstimateValueBytes(objects[i], depth));
            total = AddSaturating(total, elementBytes);
        }

        return total;
    }
}
