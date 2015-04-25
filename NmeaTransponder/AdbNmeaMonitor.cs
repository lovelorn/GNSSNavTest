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
        ///// <summary>
        ///// 获取或者设置NMEA数据队列，每一个元素代表一条NMEA语句
        ///// </summary>
        //public List<string> NmeaData { get; set; }
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

        private string mAdbFilePath = "";
        private Process mProcess;
        private Thread mMonitorThread;
        private bool needEndThread = false;

        private static bool IsHasReceivedMessage = false;
        private static string OneLine = "";
        private static int BufferSize = 1024*1024;
        private static AdbNmeaStream adbNmeaStream = new AdbNmeaStream();

        private const string ErrorRemind = "数据接收异常，请检测以下内容：\r\n\r\n1、手机是否已连接，并且正确安装了驱动程序\r\n\r\n2、手机的'连接USB后启动调试模式'是否已经开启\r\n\r\n3、nmea2log APP是否已经开启并且运行";

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

            string cmdstring = "\"" + adbFilePath + "\" logcat -v tag -s NMEA2LOG";

            mProcess = new Process();
            mProcess.StartInfo.FileName = "cmd.exe";
            mProcess.StartInfo.UseShellExecute = false;
            mProcess.StartInfo.RedirectStandardInput = true;
            mProcess.StartInfo.RedirectStandardOutput = true;
            mProcess.StartInfo.RedirectStandardError = true;
            mProcess.StartInfo.CreateNoWindow = true;
            mProcess.Start();

            mProcess.StandardInput.WriteLine(cmdstring);
            //NmeaData.Clear();
            needEndThread = false;

            mMonitorThread = new Thread(new ThreadStart(NmeaMonitorThreadHandleUsingManualReset));
            mMonitorThread.Start();
            while (mMonitorThread.IsAlive == false) ;
            return AdbStartResult.Success;
        }

        public void StopReceive()
        {
            if (mMonitorThread == null)
                return;
            if (mMonitorThread.IsAlive == false)
                return;
            needEndThread = true;
            mMonitorThread.Abort();
            while (mMonitorThread.IsAlive == true) ;
        }

        private void NmeaMonitorThreadHandle()
        {
            StreamReader sr = mProcess.StandardOutput;
            char[] buffer = new char[1024];
            string line = "";
            while (true)
            {
                if (needEndThread == true)
                {
                    sr.Close();
                    needEndThread = false;
                    mProcess.Kill();
                    ClearAdbProcess();
                    return;
                }

                try
                {
                    line = sr.ReadLine();
                    if (line == null)
                    {
                        sr.Close();
                        needEndThread = false;
                        mProcess.Kill();
                        ClearAdbProcess();
                        return;
                    }
                }
                catch
                {
                    sr.Close();
                    needEndThread = false;
                    mProcess.Kill();
                    ClearAdbProcess();
                    return;
                }

                line += "\n";

                if (line.Contains("$GP"))
                {
                    int Index = line.IndexOf("$GP");
                    string substring = line.Substring(Index);

                    string[] cmds = substring.Split(new char[] { '$' });
                    foreach (string singel in cmds)
                    {
                        string result = singel;
                        if (result.Contains("GP"))
                        {
                            if (result.Contains("\n"))
                            {
                                int enterIndex = result.IndexOf('\n');
                                result = result.Remove(enterIndex);
                            }
                            string oneline = "$" + result + "\r\n";

                            //如果需要转发
                            if (TransponderNmeaData == true && TransponderStream != null)
                            {
                                //byte[] streambuffer = Encoding.Default.GetBytes(oneline);
                                TransponderStream.Write(oneline);
                            }
                            ////往队列中添加信息
                            //lock (NmeaData)
                            //{
                            //    NmeaData.Add(oneline);
                            //}

                            if (OnReceive != null)
                            {
                                OnReceive(oneline, new EventArgs());
                            }
                        }
                    }
                }
            }
        }

        private void NmeaMonitorThreadHandleUsingManualReset()
        {
            StreamReader sr = mProcess.StandardOutput;
            byte[] buffer = new byte[BufferSize];

            ManualResetEvent mre = new ManualResetEvent(false);
            AdbAsyncState adbasyncstate = new AdbAsyncState();
            adbasyncstate.Stream = sr;
            adbasyncstate.Buffer = buffer;
            adbasyncstate.EvtHandle = mre;

            while (true)
            {
                if (needEndThread == true)
                {
                    sr.Close();
                    needEndThread = false;
                    mProcess.Kill();
                    ClearAdbProcess();
                    return;
                }

                IsHasReceivedMessage = false;
                OneLine = "";
                sr.BaseStream.BeginRead(buffer, 0, BufferSize, new AsyncCallback(AsyncReadCallback), adbasyncstate);
                adbasyncstate.EvtHandle.Reset();

                if (adbasyncstate.EvtHandle.WaitOne(10000,false) == true)
                {
                    if (IsHasReceivedMessage == false)
                    {
                        if (OnAdbError != null)
                        {
                            OnAdbError(ErrorRemind, new EventArgs());
                        }
                    }
                    int count = adbNmeaStream.Count;
                    for (int i = 0; i < count; i++)
                    {
                        string message = adbNmeaStream.ReadOne();
                        //如果需要转发
                        if (TransponderNmeaData == true && TransponderStream != null)
                        {
                            //byte[] streambuffer = Encoding.Default.GetBytes(oneline);
                            TransponderStream.Write(message);
                        }
                        ////往队列中添加信息
                        //lock (NmeaData)
                        //{
                        //    NmeaData.Add(oneline);
                        //}

                        if (OnReceive != null)
                        {
                            OnReceive(message, new EventArgs());
                        }
                    }
                }
                else
                {
                    if(OnAdbError != null)
                    {
                        OnAdbError(ErrorRemind,new EventArgs());
                    }
                }

            }
        }


        private static void AsyncReadCallback(IAsyncResult asyncResult)
        {
            AdbAsyncState asyncState = (AdbAsyncState)asyncResult.AsyncState;
            int readCn = asyncState.Stream.BaseStream.EndRead(asyncResult);
            //判断是否读到内容
            if (readCn > 0)
            {
                byte[] buffer;
                if (readCn == BufferSize)
                    buffer = asyncState.Buffer;
                else
                {
                    buffer = new byte[readCn];
                    Array.Copy(asyncState.Buffer, 0, buffer, 0, readCn);
                }

                //输出读取内容值
                OneLine = Encoding.Default.GetString(buffer);
                IsHasReceivedMessage = true;
                adbNmeaStream.Write(OneLine);
            }
            else
            {
                IsHasReceivedMessage = false;
            }
            asyncState.EvtHandle.Set();

            //if (readCn < BufferSize)
            //{
            //asyncState.EvtHandle.Set();
            //}
            //else
            //{
            //    Array.Clear(asyncState.Buffer, 0, BufferSize);
            //    //再次执行异步读取操作
            //    asyncState.Stream.BaseStream.BeginRead(asyncState.Buffer, 0, BufferSize, new AsyncCallback(AsyncReadCallback), asyncState);
            //}


        }


    }
}
