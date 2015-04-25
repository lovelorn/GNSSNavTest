using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.IO.Ports;
using AdbStream;

namespace NMEAMonitor
{
    public enum AdbStartResult {Success,NotAdbFile,AdbFileNotExist, };

    class AdbAsyncState
    {
        public StreamReader Stream { get; set; }

        public byte[] Buffer { get; set; }

        public ManualResetEvent EvtHandle { get; set; }
    }

    public class AdbNmeaMonitor
    {
        #region 公有属性
        /// <summary>
        /// 获取一个值，该值表明adb监视器是否正在工作
        /// </summary>
        public bool IsAlive
        {
            get
            {
                if (mMonitorThread == null)
                    return false;
                return mMonitorThread.IsAlive;
            }
        }
        /// <summary>
        /// Reveice事件，当接收到一条nmea信息时触发
        /// </summary>
        public event EventHandler OnReceive;
        /// <summary>
        /// Adb Receive Error事件，表示adb连接异常
        /// </summary>
        public event EventHandler OnAdbError;
        /// <summary>
        /// 获取或者设置一个值，该值指示是否将接收到的nmea信息进行转发
        /// </summary>
        public bool TransponderNmeaData { get; set; }
        /// <summary>
        /// 获取或者设置一个值，该值表示对nmea信息进行转发的输出流
        /// </summary>
        public StreamWriter TransponderStream { get; set; }

        #endregion

        #region 私有变量
        private Process mProcess;
        private Thread mMonitorThread;
        private bool needEndThread = false;

        private static bool IsHasReceivedMessage = false;
        private static int BufferSize = 256;
        private static AdbNmeaStream adbNmeaStream = new AdbNmeaStream();

        private const string ErrorRemind = "数据接收异常，请检测以下内容：\r\n\r\n1、手机是否已连接，并且正确安装了驱动程序\r\n\r\n2、手机的'连接USB后启动调试模式'是否已经开启\r\n\r\n3、nmea2log APP是否已经开启并且运行";
        #endregion

        #region 构造函数
        public AdbNmeaMonitor()
        {
        }
        #endregion

        /// <summary>
        /// 清除任务管理器中所有adb.exe的任务
        /// </summary>
        public void ClearAdbProcess()
        {
            try
            {
                Process[] pro = Process.GetProcesses();//获取已开启的所有进程
                //遍历所有查找到的进程
                for (int i = 0; i < pro.Length; i++)
                {
                    //判断此进程是否是要查找的进程
                    if (pro[i].ProcessName.ToString().ToLower().Contains("adb") && pro[i].MainModule.FileName.Contains("adb.exe"))
                    {
                        pro[i].Kill();//结束进程
                    }
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// 开始进行adb方式的监控
        /// </summary>
        /// <param name="adbFilePath">adb文件路径</param>
        /// <returns></returns>
        public AdbStartResult StartReceive(string adbFilePath)
        {
            if (adbFilePath.Contains("adb.exe") == false)
            {
                return AdbStartResult.NotAdbFile;
            }
            if (File.Exists(adbFilePath) == false)
            {
                return AdbStartResult.AdbFileNotExist;
            }
            ClearAdbProcess();
            string cmdstring = "\"" + adbFilePath + "\" logcat -v tag -s NMEA2LOG";

            mProcess = new Process();
            mProcess.StartInfo.FileName = "cmd.exe";
            mProcess.StartInfo.UseShellExecute = false;
            mProcess.StartInfo.RedirectStandardInput = true;
            mProcess.StartInfo.RedirectStandardOutput = true;
            mProcess.StartInfo.RedirectStandardError = true;
            mProcess.StartInfo.CreateNoWindow = true;
            mProcess.Start();

            mProcess.OutputDataReceived += new DataReceivedEventHandler(OnDataReceived);
            mProcess.BeginOutputReadLine();

            mProcess.StandardInput.WriteLine(cmdstring);

            return AdbStartResult.Success; 
        }

        private void OnDataReceived(object Sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                int StartIndex = e.Data.IndexOf('$');
                if (StartIndex < 0)
                    return;
                string message = e.Data.Substring(StartIndex) + "\r\n";
                //如果需要转发
                if (TransponderNmeaData == true && TransponderStream != null)
                {
                    //byte[] streambuffer = Encoding.Default.GetBytes(oneline);
                    TransponderStream.Write(message);
                }
                if (OnReceive != null)
                {
                    OnReceive(message, new EventArgs());
                }
            }
        }

        public void StopReceive()
        {
            mProcess.CancelOutputRead();
            ClearAdbProcess();
            mProcess.Kill();
            mProcess.Close();
            return;
        }

    }
}
