using FieldLink.PlcDrivers.Common;
using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace FieldLink.Robot.ABB.Protocols
{
    /// <summary>ABB 응답의 span과 li 항목을 해석합니다. 원본의 XHTML 및 정규식 가정을 유지합니다.</summary>
    public static class AbbResponseParser
    {
        /// <summary>지정 class의 첫 span 문자열을 반환합니다.</summary>
        public static OperationResult<string> ParseSpanByClass(string content, string className)
        {
            Match match = Regex.Match(content, $"<span class=\"{className}\">[^<]+");
            if (!match.Success)
                return new OperationResult<string>($"Parse None class [{className}] Span\r\n{content}");
            return OperationResult.CreateSuccessResult(match.Value.Substring(match.Value.IndexOf('>') + 1));
        }

        /// <summary>일치하는 span들을 현재 문화권의 실수 배열로 변환합니다.</summary>
        public static OperationResult<double[]> ParseDoubleListSpanByClass(string content, string className)
        {
            MatchCollection match = Regex.Matches(content, $"<span class=\"{className}\">[^<]+");
            double[] array = new double[match.Count];
            for (int i = 0; i < match.Count; i++)
            {
                array[i] = Convert.ToDouble(match[i].Value.Substring(match[i].Value.IndexOf('>') + 1));
            }

            return OperationResult.CreateSuccessResult(array);
        }

        /// <summary>일치하는 span들을 지정 변환으로 JSON 배열 문자열로 만듭니다.</summary>
        public static OperationResult<string> ParseListSpanByClass<T>(string content, string className, Func<string, T> trans)
        {
            MatchCollection match = Regex.Matches(content, $"<span class=\"{className}\">[^<]+");
            JArray jArray = new JArray();
            for (int i = 0; i < match.Count; i++)
            {
                jArray.Add(trans(match[i].Value.Substring(match[i].Value.IndexOf('>') + 1)));
            }

            return OperationResult.CreateSuccessResult(jArray.ToString());
        }

        /// <summary>li 직속 span의 class를 키로 JSON 객체를 만듭니다.</summary>
        public static JObject ParseListByClass(string content)
        {
            XElement xml = XElement.Parse(content);
            JObject json = new JObject();
            foreach (XElement item in xml.Elements("span"))
            {
                json.Add(item.Attribute("class").Value, item.Value);
            }

            return json;
        }

        /// <summary>첫 번째 일치 li의 span들을 JSON 객체 문자열로 만듭니다.</summary>
        public static OperationResult<string> ParseJObjectByClass(string content, string className)
        {
            Match match = Regex.Match(content, $"<li class=\"{className}\"[\\S\\s]+?</li>");
            if (!match.Success)
                return new OperationResult<string>($"Parse None class [{className}] List\r\n{content}");
            return OperationResult.CreateSuccessResult(ParseListByClass(match.Value).ToString());
        }

        /// <summary>일치하는 li들을 지정 개수까지 JSON 배열 문자열로 만듭니다.</summary>
        public static OperationResult<string> ParseJArrayByClass(string content, string className, int maxCount = int.MaxValue)
        {
            MatchCollection match = Regex.Matches(content, $"<li class=\"{className}\"[\\S\\s]+?</li>");
            JArray jArray = new JArray();
            for (int i = 0; i < match.Count; i++)
            {
                if (i >= maxCount)
                    break;
                jArray.Add(ParseListByClass(match[i].Value));
            }

            return OperationResult.CreateSuccessResult(jArray.ToString());
        }
    }
}
