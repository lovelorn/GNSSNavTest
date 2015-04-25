using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using NMEAMonitor;

namespace GNSSNavTest
{
    public partial class Form1 : Form
    {
        private SerialPort mSerial = new SerialPort();

        AdbNmeaMonitor AdbMonitor = new AdbNmeaMonitor();

        bool AutoClear = true;

        int AutoClearThreshold = 1000;

        int rowCounter = 0;
        int byteCounter = 0;
        int rowIndex = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            this.toolStripComboBox2.SelectedIndex = 2;

            刷新串口列表ToolStripMenuItem_Click(null, null);

            this.自动清除ToolStripMenuItem.Checked = AutoClear;

            AdbMonitor.ClearAdbProcess();
            AdbMonitor.OnReceive += new EventHandler(OnAdbReceive);
            AdbMonitor.OnAdbError += new EventHandler(OnAdbErrorHandler);
            ShowThreshold();
            this.toolStripStatusLabel3.Text = String.Format("条数统计：{0}", rowCounter);
            this.toolStripStatusLabel1.Text = String.Format("字节统计：{0}", byteCounter);

            this.开始ToolStripMenuItem.Enabled = true;
            this.toolStripButton_Start.Enabled = true;

            this.toolStripButton_Stop.Enabled = false;
            this.停止ToolStripMenuItem.Enabled = false;
            this.toolStripStatusLabel4.Text = String.Format("开始时间：{0}", DateTime.Now);
        }

        private void ShowThreshold()
        {
            this.行ToolStripMenuItem.Checked = (AutoClearThreshold == 100);
            this.行ToolStripMenuItem1.Checked = (AutoClearThreshold == 1000);
            this.行ToolStripMenuItem2.Checked = (AutoClearThreshold == 5000);
            this.行ToolStripMenuItem.Enabled = AutoClear;
            this.行ToolStripMenuItem1.Enabled = AutoClear;
            this.行ToolStripMenuItem2.Enabled = AutoClear;
        }

        private void 刷新串口列表ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.toolStripComboBox1.Items.Clear();
            this.toolStripComboBox1.Items.Add("不转发");
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();
            Array.Sort(ports);
            this.toolStripComboBox1.Items.AddRange(ports);
            this.toolStripComboBox1.SelectedIndex = 0;



        }

        private void 自动清除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutoClear = !AutoClear;
            this.自动清除ToolStripMenuItem.Checked = AutoClear;
            ShowThreshold();
        }

        private void 行ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutoClearThreshold = 100;
            ShowThreshold();
        }

        private void 行ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AutoClearThreshold = 1000;
            ShowThreshold();
        }

        private void 行ToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            AutoClearThreshold = 5000;
            ShowThreshold();
        }

        private void toolStripButton_Start_Click(object sender, EventArgs e)
        {
            this.dataGridView1.Rows.Clear();
            rowCounter = 0;
            byteCounter = 0;
            rowIndex = 0;
            this.Cursor = Cursors.WaitCursor;
            AdbMonitor.StartReceive("adb.exe");

            this.开始ToolStripMenuItem.Enabled = false;
            this.toolStripButton_Start.Enabled = false;

            this.toolStripButton_Stop.Enabled = true;
            this.停止ToolStripMenuItem.Enabled = true;
            this.toolStripStatusLabel4.Text = String.Format("开始时间：{0}", DateTime.Now);

        }

        private void OnAdbReceive(object sender, EventArgs e)
        {
            string nmea = (string)sender;
            if (this.InvokeRequired)
            {
                RecviveDelegate rd = new RecviveDelegate(OnAdbReceiveDelegate);
                this.Invoke(rd, new object[] { nmea });
            }
            else
            {
                OnAdbReceiveDelegate(nmea);
            }

        }
        delegate void RecviveDelegate(string nmea);
        private void OnAdbReceiveDelegate(string nmea)
        {
            try
            {
                if(this.Cursor != Cursors.Arrow)
                    this.Cursor = Cursors.Arrow;
                if (AutoClear == true && this.dataGridView1.Rows.Count >= AutoClearThreshold)
                {
                    this.dataGridView1.Rows.Clear();
                    rowIndex = 0;
                }
                rowIndex++;
                int Index = this.dataGridView1.Rows.Add(new object[] {rowIndex.ToString(), DateTime.Now.ToString(), nmea });
                this.dataGridView1.CurrentCell = this.dataGridView1.Rows[Index].Cells[0];
                rowCounter++;
                byteCounter += nmea.Length;
                this.toolStripStatusLabel3.Text = String.Format("条数统计：{0}", rowCounter);
                this.toolStripStatusLabel1.Text = String.Format("字节统计：{0}", byteCounter);
            }
            catch
            {
                rowCounter = 0;
            }
        }

        private void OnAdbErrorHandler(object sender, EventArgs e)
        {
            string message = (string)sender;
            if (this.InvokeRequired)
            {
                ErrorDelegate rd = new ErrorDelegate(OnAdbErrorDelegate);
                this.Invoke(rd, new object[] { message });
            }
            else
            {
                OnAdbErrorDelegate(message);
            }
        }

        delegate void ErrorDelegate(string message);
        private void OnAdbErrorDelegate(string message)
        {
            MessageBox.Show(message, "提示");
            toolStripButton_Stop_Click(null, null);
        }


        private void toolStripButton_Stop_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Arrow;
            AdbMonitor.StopReceive();
            this.开始ToolStripMenuItem.Enabled = true;
            this.toolStripButton_Start.Enabled = true;

            this.toolStripButton_Stop.Enabled = false;
            this.停止ToolStripMenuItem.Enabled = false;

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (AdbMonitor.IsAlive)
                AdbMonitor.StopReceive();
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.toolStripComboBox1.SelectedItem.ToString() == "不转发")
            {

                AdbMonitor.TransponderNmeaData = false;
                AdbMonitor.TransponderStream = null;
                if (mSerial.IsOpen == true)
                {
                    mSerial.Close();
                }
                this.toolStripComboBox1.Enabled = true;
                this.刷新串口列表ToolStripMenuItem.Enabled = true;
                this.toolStripComboBox2.Enabled = true;
            }
            else
            {
                AdbMonitor.TransponderNmeaData = false;
                AdbMonitor.TransponderStream = null;
                if (mSerial.IsOpen == true)
                {
                    mSerial.Close();
                }
                string Comport = this.toolStripComboBox1.SelectedItem.ToString();
                mSerial.PortName = Comport;
                mSerial.BaudRate = Convert.ToInt32(this.toolStripComboBox2.SelectedItem.ToString());
                try
                {
                    mSerial.Open();
                }
                catch
                {
                    MessageBox.Show("无法开启" + Comport + ",串口被占用!");
                    this.toolStripComboBox1.SelectedIndex = 0;
                    return;
                }
                AdbMonitor.TransponderNmeaData = true;
                AdbMonitor.TransponderStream = new System.IO.StreamWriter(mSerial.BaseStream);
                AdbMonitor.TransponderStream.AutoFlush = true;
                this.toolStripComboBox1.Enabled = true;
                this.刷新串口列表ToolStripMenuItem.Enabled = false;
                this.toolStripComboBox2.Enabled = false;
            }


        }

        private void 清除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.dataGridView1.Rows.Clear();
            rowIndex = 0;

        }




    }
}
