using System;

namespace FieldLink.Communication.Push
{
    internal static class PushTopic
    {
        internal static void Validate(string topic)
        {
            if (string.IsNullOrEmpty(topic) || topic.Length > 128)
                throw new ArgumentException("토픽은 1~128자여야 합니다.", nameof(topic));
            foreach (char value in topic)
                if (!((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') ||
                    (value >= '0' && value <= '9') || value == '_' || value == '-' || value == '.'))
                    throw new ArgumentException("토픽에는 영문자, 숫자, _, -, .만 사용할 수 있습니다.", nameof(topic));
        }
    }
}
