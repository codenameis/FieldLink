using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FieldLink.Secs.Types
{
    internal static class SecsXml
    {
        internal static XElement Encode(SecsValue value, int depth)
        {
            CheckDepth(depth);
            var element = new XElement(value.ItemType.ToString(), new XAttribute("Length", value.Length));
            if (value.ItemType == SecsItemType.List)
            {
                foreach (var child in SecsValue.RequireList(value))
                    element.Add(Encode(child, depth + 1));
            }
            else if (value.ItemType != SecsItemType.None)
            {
                string text;
                if (value.ItemType == SecsItemType.Binary || value.ItemType == SecsItemType.JIS8)
                    text = BitConverter.ToString((byte[])value.Value).Replace("-", "");
                else if (value.Value is Array array)
                    text = "[" + string.Join(",", array.Cast<object>().Select(Format)) + "]";
                else
                    text = Format(value.Value);
                element.SetAttributeValue("Value", text);
            }
            return element;
        }
        internal static SecsValue Decode(XElement element, int depth)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            CheckDepth(depth);
            if (!Enum.TryParse(element.Name.LocalName, out SecsItemType type) || !Enum.IsDefined(typeof(SecsItemType), type))
                throw new InvalidDataException("Unknown SECS XML type.");
            if (type == SecsItemType.List)
                return new SecsValue(type, element.Elements().Select(e => Decode(e, depth + 1)).ToArray());
            if (type == SecsItemType.None)
                return new SecsValue();
            string text = (string)element.Attribute("Value") ?? "";
            if (type == SecsItemType.ASCII)
                return new SecsValue(text);
            if (type == SecsItemType.Binary || type == SecsItemType.JIS8)
            {
                text = text.Replace(" ", "").Replace("-", "");
                if (text.Length % 2 != 0)
                    throw new InvalidDataException("Odd hexadecimal XML length.");
                return new SecsValue(type, Enumerable.Range(0, text.Length / 2)
                    .Select(i => byte.Parse(text.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray());
            }
            Type primitive = SecsValue.ElementType(type);
            if (!text.StartsWith("[", StringComparison.Ordinal))
                return new SecsValue(type, Convert.ChangeType(text, primitive, CultureInfo.InvariantCulture));
            if (!text.EndsWith("]", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid XML array.");
            string body = text.Substring(1, text.Length - 2);
            string[] parts = body.Length == 0 ? new string[0] : body.Split(',');
            Array values = Array.CreateInstance(primitive, parts.Length);
            for (int i = 0; i < parts.Length; i++)
                values.SetValue(Convert.ChangeType(parts[i], primitive, CultureInfo.InvariantCulture), i);
            return new SecsValue(type, values);
        }
        private static string Format(object value) => value is float f ? f.ToString("R", CultureInfo.InvariantCulture) :
            value is double d ? d.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture);
        private static void CheckDepth(int depth)
        {
            if (depth > 64)
                throw new InvalidDataException("SECS nesting exceeds 64 levels.");
        }
    }
}
