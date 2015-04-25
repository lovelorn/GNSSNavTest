using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdbStream
{
    public class AdbNmeaStream
    {
        #region 私有变量
        private string mMessageString = "";
        private List<string> mNmeaMessages = new List<string>();
        
        #endregion

        #region 公共属性
        /// <summary>
        /// 获取或者设置一个值，该值表示nmea信息头部。一般为$GP，$BD
        /// </summary>
        public string[] NmeaHeader { get; set; }
        /// <summary>
        /// 获取或者设置一个值，该值表示nmea信息的结束符，一般为\r\n
        /// </summary>
        public string NmeaNewLine { get; set; }
        /// <summary>
        /// 获取一个值，该值表示当前有效的nmea信息条数
        /// </summary>
        public int Count { get { return mNmeaMessages.Count; } }

        #endregion

        #region 析造函数
        public AdbNmeaStream()
        {
            NmeaHeader = new string[] {"$GP","$BD" };
            NmeaNewLine = "\r\r\r\n";
        }

        #endregion

        #region 公共方法
        /// <summary>
        /// 将nmea流中添加信息
        /// </summary>
        /// <param name="partialString">部分nmea流</param>
        public void Write(string partialString)
        {
            //lock(this)
            //{
                mMessageString += partialString;
                while (true)
                {
                    int StartIndex = -1;
                    int EndIndex = -1;
                    int i = 0;
                    //针对每一个header进行尝试
                    for (i = 0; i < NmeaHeader.Length; i++)
                    {
                        StartIndex = mMessageString.IndexOf(NmeaHeader[i]);
                        if (StartIndex < 0)
                            continue;
                        EndIndex = mMessageString.IndexOf(NmeaNewLine,StartIndex+NmeaHeader[i].Length);
                        if (EndIndex < 0)
                            continue;

                        string subString = mMessageString.Substring(StartIndex, EndIndex - StartIndex );
                        subString += "\r\n";
                        mNmeaMessages.Add(subString);
                        mMessageString = mMessageString.Substring(EndIndex + NmeaNewLine.Length);
                        break;
                    }
                    if (i >= NmeaHeader.Length)
                        break;
                }
            //}
        }

        /// <summary>
        /// 从nmea流中读取一条完整的nmea信息
        /// </summary>
        /// <returns></returns>
        public string ReadOne()
        {
            //lock (this)
            //{
                if (mNmeaMessages.Count == 0)
                    return null;
                else
                {
                    string result = mNmeaMessages[0];
                    mNmeaMessages.RemoveAt(0);
                    return result;
                }
            //}
        }

        #endregion

        #region 私有方法


        #endregion
    }
}
