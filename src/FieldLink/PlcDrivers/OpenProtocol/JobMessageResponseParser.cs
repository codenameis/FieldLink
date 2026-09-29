using FieldLink.PlcDrivers.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>JobMessage 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class JobMessageResponseParser
    {
        /// <summary>MID0031 응답의 고정 위치 필드를 해석합니다.</summary>
        /// <param name = "reply">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int[]> PraseMID0031(string reply)
        {
            try
            {
                int revision = Convert.ToInt32(reply.Substring(8, 3));
                int everyLen = revision == 1 ? 2 : 4;
                int count = Convert.ToInt32(reply.Substring(20, everyLen));
                int[] ints = new int[count];
                for (int i = 0; i < count; i++)
                {
                    ints[i] = Convert.ToInt32(reply.Substring(20 + everyLen + i * everyLen, everyLen));
                }

                return OperationResult.CreateSuccessResult(ints);
            }
            catch (Exception ex)
            {
                return new OperationResult<int[]>("MID0031 prase failed: " + ex.Message + Environment.NewLine + "Source: " + reply);
            }
        }

        /// <summary>MID0033 응답의 고정 위치 필드를 해석합니다.</summary>
        /// <param name = "reply">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<TighteningJob> PraseMID0033(string reply)
        {
            try
            {
                TighteningJob job = new TighteningJob();
                job.JobID = Convert.ToInt32(reply.Substring(22, 2));
                job.JobName = reply.Substring(26, 25).Trim();
                job.ForcedOrder = Convert.ToInt32(reply.Substring(53, 1));
                job.MaxTimeForFirstTightening = Convert.ToInt32(reply.Substring(56, 4));
                job.MaxTimeToCompleteJob = Convert.ToInt32(reply.Substring(62, 5));
                job.JobBatchMode = Convert.ToInt32(reply.Substring(69, 1));
                job.LockAtJobDone = reply[72] == '1';
                job.UseLineControl = reply[75] == '1';
                job.RepeatJob = reply[78] == '1';
                job.ToolLoosening = Convert.ToInt32(reply.Substring(81, 1));
                job.Reserved = Convert.ToInt32(reply.Substring(86, 1));
                job.JobList = new List<TighteningJobStep>();
                int number = Convert.ToInt32(reply.Substring(89, 2));
                for (int i = 0; i < number; i++)
                {
                    job.JobList.Add(new TighteningJobStep(reply.Substring(92 + i * 12, 11)));
                }

                return OperationResult.CreateSuccessResult(job);
            }
            catch (Exception ex)
            {
                return new OperationResult<TighteningJob>("MID0033 prase failed: " + ex.Message + Environment.NewLine + "Source: " + reply);
            }
        }
    }
}
