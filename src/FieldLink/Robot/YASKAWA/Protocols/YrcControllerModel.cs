using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>
    /// YRC 로봇의 종류
    /// </summary>
    public enum YrcControllerModel
    {
        /// <summary>
        /// YRC1000 모델, 6축 로봇 포함
        /// </summary>
        YRC1000,
        /// <summary>
        /// YRC100 모델, 7축 로봇 포함
        /// </summary>
        YRC100
    }
}
