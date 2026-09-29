using System;
using System.Text;
using System.Text.RegularExpressions;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.FANUC.Protocols
{
    /// <summary>FANUC 기본 할당 영역을 별도 상태 스냅샷으로 해석합니다.</summary>
    public static class FanucSnapshotParser
    {
        /// <summary>D1부터 6130워드(12260바이트) 이상을 해석합니다. 실패 중 부분 객체를 노출하지 않습니다.</summary>
        public static OperationResult<FanucControllerSnapshot> Parse(byte[] content, Encoding encoding)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            if (content.Length < 12260)
                throw new ArgumentException("The FANUC assignment block requires 12260 bytes.", nameof(content));
            var snapshot = new FanucControllerSnapshot();

            IProtocolValueConverter byteTransform = new ProtocolValueConverter();
            string[] assignments = FanucProtocol.GetAssignmentCommands();
            int[] offsets = new int[assignments.Length - 1];
            for (int i = 1; i < assignments.Length; i++)
            {
                var matches = Regex.Matches(assignments[i], "[0-9]+");
                offsets[i - 1] = (int.Parse(matches[0].Value) - 1) * 2;
            }

            snapshot.AlarmList = GetFanucAlarmArray(byteTransform, content, offsets[0], 5, encoding);
            snapshot.AlarmCurrent = FanucAlarm.ParseFrom(byteTransform, content, offsets[1], encoding);
            snapshot.AlarmPassword = FanucAlarm.ParseFrom(byteTransform, content, offsets[2], encoding);
            snapshot.CurrentPose = FanucPose.ParseFrom(byteTransform, content, offsets[3]);
            snapshot.CurrentUserFramePose = FanucPose.ParseFrom(byteTransform, content, offsets[4]);
            snapshot.CurrentPose2 = FanucPose.ParseFrom(byteTransform, content, offsets[5]);
            snapshot.CurrentPose3 = FanucPose.ParseFrom(byteTransform, content, offsets[6]);
            snapshot.CurrentPose4 = FanucPose.ParseFrom(byteTransform, content, offsets[7]);
            snapshot.CurrentPose5 = FanucPose.ParseFrom(byteTransform, content, offsets[8]);
            snapshot.Task = FanucTask.ParseFrom(byteTransform, content, offsets[9], encoding);
            snapshot.TaskIgnoreMacro = FanucTask.ParseFrom(byteTransform, content, offsets[10], encoding);
            snapshot.TaskIgnoreKarel = FanucTask.ParseFrom(byteTransform, content, offsets[11], encoding);
            snapshot.TaskIgnoreMacroKarel = FanucTask.ParseFrom(byteTransform, content, offsets[12], encoding);
            snapshot.Group1PositionRegisters = GetFanucPoseArray(byteTransform, content, offsets[13], 10, encoding);
            snapshot.Group2PositionRegisters = GetFanucPoseArray(byteTransform, content, offsets[14], 4, encoding);
            snapshot.Group3PositionRegisters = GetFanucPoseArray(byteTransform, content, offsets[15], 10, encoding);
            snapshot.Group4PositionRegisters = GetFanucPoseArray(byteTransform, content, offsets[16], 10, encoding);
            snapshot.Group5PositionRegisters = GetFanucPoseArray(byteTransform, content, offsets[17], 10, encoding);
            snapshot.FastClock = BitConverter.ToInt32(content, offsets[18]);
            snapshot.Timer10Value = BitConverter.ToInt32(content, offsets[19]);
            snapshot.CurrentGroupAngle = BitConverter.ToSingle(content, offsets[20]);
            snapshot.DutyTemperature = BitConverter.ToSingle(content, offsets[21]);
            snapshot.Timer10Comment = encoding.GetString(content, offsets[22], 80).Trim('\u0000');
            snapshot.Timer2Comment = encoding.GetString(content, offsets[23], 80).Trim('\u0000');
            snapshot.Group1Tool1Pose = FanucPose.ParseFrom(byteTransform, content, offsets[24]);
            snapshot.KclCommands = encoding.GetString(content, offsets[25], 80).Trim('\u0000');
            snapshot.IntegerRegisters = byteTransform.ReadInt32(content, offsets[26], 5);
            snapshot.RealRegisters = byteTransform.ReadSingle(content, offsets[27], 5);
            snapshot.CombinedPositionRegisters = new FanucPose[10];
            for (int i = 0; i < snapshot.CombinedPositionRegisters.Length; i++)
            {
                snapshot.CombinedPositionRegisters[i] = new FanucPose();
                snapshot.CombinedPositionRegisters[i].Xyzwpr = byteTransform.ReadSingle(content, offsets[29] + i * 50, 9);
                snapshot.CombinedPositionRegisters[i].Config = FanucPose.TransConfigStringArray(byteTransform.ReadInt16(content, offsets[29] + 36 + i * 50, 7));
                snapshot.CombinedPositionRegisters[i].Joint = byteTransform.ReadSingle(content, offsets[30] + i * 36, 9);
            }

            snapshot.DigitalInputComments = GetStringArray(content, offsets[31], 80, 3, encoding);
            snapshot.DigitalOutputComments = GetStringArray(content, offsets[32], 80, 3, encoding);
            snapshot.RobotInputComments = GetStringArray(content, offsets[33], 80, 3, encoding);
            snapshot.RobotOutputComments = GetStringArray(content, offsets[34], 80, 3, encoding);
            snapshot.UserInputComments = GetStringArray(content, offsets[35], 80, 3, encoding);
            snapshot.UserOutputComments = GetStringArray(content, offsets[36], 80, 3, encoding);
            snapshot.SystemInputComments = GetStringArray(content, offsets[37], 80, 3, encoding);
            snapshot.SystemOutputComments = GetStringArray(content, offsets[38], 80, 3, encoding);
            snapshot.WeldInputComments = GetStringArray(content, offsets[39], 80, 3, encoding);
            snapshot.WeldOutputComments = GetStringArray(content, offsets[40], 80, 3, encoding);
            snapshot.WeldSystemInputComments = GetStringArray(content, offsets[41], 80, 3, encoding);
            snapshot.AnalogInputComments = GetStringArray(content, offsets[42], 80, 3, encoding);
            snapshot.AnalogOutputComments = GetStringArray(content, offsets[43], 80, 3, encoding);
            snapshot.GroupInputComments = GetStringArray(content, offsets[44], 80, 3, encoding);
            snapshot.GroupOutputComments = GetStringArray(content, offsets[45], 80, 3, encoding);
            snapshot.StringRegisterValues = GetStringArray(content, offsets[46], 80, 3, encoding);
            snapshot.StringRegisterComments = GetStringArray(content, offsets[47], 80, 3, encoding);

        
            return OperationResult.CreateSuccessResult(snapshot);
        }

        private static string[] GetStringArray(byte[] content, int index, int length, int arraySize, Encoding encoding)
        {
            string[] array = new string[arraySize];
            for (int i = 0; i < arraySize; i++)
                array[i] = encoding.GetString(content, index + length * i, length).TrimEnd('\u0000');
            return array;
        }

        private static FanucPose[] GetFanucPoseArray(IProtocolValueConverter byteTransform, byte[] content, int index, int arraySize, Encoding encoding)
        {
            FanucPose[] array = new FanucPose[arraySize];
            for (int i = 0; i < arraySize; i++)
                array[i] = FanucPose.ParseFrom(byteTransform, content, index + i * 100);
            return array;
        }

        private static FanucAlarm[] GetFanucAlarmArray(IProtocolValueConverter byteTransform, byte[] content, int index, int arraySize, Encoding encoding)
        {
            FanucAlarm[] array = new FanucAlarm[arraySize];
            for (int i = 0; i < arraySize; i++)
                array[i] = FanucAlarm.ParseFrom(byteTransform, content, index + 200 * i, encoding);
            return array;
        }
    }
}
