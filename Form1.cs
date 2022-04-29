using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using Microsoft.VisualBasic;
using System.Windows.Forms.DataVisualization.Charting;
using System.Globalization;
using System.Data.SqlClient;
using System.Configuration;

namespace Arbeidskrav4SSC
{
    public partial class Form1 : Form
    {
        //Lists for reading measurements
        List<int> rawReading = new List<int>();
        List<float> scaledReading = new List<float>();
        List<DateTime> rawTime = new List<DateTime>();
        List<DateTime> scaledTime = new List<DateTime>();

        //Lists for Instrument paramtaerers
        List<String> TagName = new List<String>();
        List<String> Desc = new List<String>();
        List<String> DAU = new List<String>();
        List<String> Area = new List<String>();
        List<String> Channel = new List<String>();
        List<String> LRV = new List<String>();
        List<String> URV = new List<String>();
        List<String> AL = new List<String>();
        List<String> AH = new List<String>();
        List<String> Scan = new List<String>();


        string conSSC = ConfigurationManager.ConnectionStrings["conSSC"].ConnectionString;
        public Form1()
        {
            InitializeComponent();

            //Importing from Database to comboboxes
            ImportToComboBoxA();
            ImportToComboBoxDAU();
            ImportToComboBoxDAUID();

            //Formatting of the graph
            chart1.Series[0].XValueType = ChartValueType.DateTime;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm:ss";
            chart1.ChartAreas["ChartArea1"].AxisX.Title = "Time";
            chart1.ChartAreas["ChartArea1"].AxisY.Title = "Measurement Reading";

            int scan_frequence = int.Parse(labelScan.Text) * 1000;

            //Tab 1 items
            comboBoxPort.Items.AddRange(SerialPort.GetPortNames());
            comboBoxPort.Text = "--Select--";
            string[] bitRates = new string[] {"1200","2400", "4800t", "9600", "19200"
            , "38400","57600", "115200"
            };
            comboBoxBit.Items.AddRange(bitRates);
            comboBoxBit.SelectedIndex = comboBoxBit.Items.IndexOf("9600");

            //serial Port connection
            serialPort1.DataReceived += new SerialDataReceivedEventHandler(DataRecievedHandler);


            //timers
            StatusCheck.Interval = 10000;
            StatusCheck.Tick += new EventHandler(StatusCheck_Tick);
            timerRaw.Interval = scan_frequence;
            timerRaw.Tick += new EventHandler(timerRaw_Tick);
            timerScaled.Interval = scan_frequence;
            timerScaled.Tick += new EventHandler(timerScaled_Tick);

        }

        
        

        //Void that takes care of commands sent to Instrument(Arduino)
        void DataRecievedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            //Variables for Sensor
            string RecievedData = ((SerialPort)sender).ReadLine();
            string[] recievedData = RecievedData.Split(';');
            string[] graphData = RecievedData.Split(';');

            int iVab;
            float scaled;

            if (recievedData[0] == "readconf")
            {
                textBoxStatus.Invoke((MethodInvoker)delegate
                {
                    labelTag.Text = recievedData[1];
                    labelLRV.Text = recievedData[2];
                    labelURV.Text = recievedData[3];
                    labelAL.Text = recievedData[4];
                    labelAH.Text = recievedData[5];
                });
            }

            if (recievedData[0] == "writeconf")
            {
                if (recievedData[1] == "1\r")
                {
                    MessageBox.Show("Upload Succuessfull");
                    serialPort1.WriteLine("readconf");
                }
                if (recievedData[1] == "0\r")
                {
                    MessageBox.Show("Wrong Password");
                }
            }

            if (recievedData[0] == "readstatus")
            {
                textBoxStatus.Invoke((MethodInvoker)delegate
                {
                    if (recievedData[1] == "0\r")
                    {
                        textBoxSignal.Text = "Ok";
                        textBoxSignal.BackColor = Color.Empty;
                    }

                    if (recievedData[1] == "1\r")
                    {
                        textBoxSignal.Text = "Fail, check instrument";
                        if (textBoxSignal.Text == "-")
                        {
                            MessageBox.Show("Please check config settings");
                        }
                    }

                    if (recievedData[1] == "2\r")
                    {
                        textBoxSignal.Text = "Alarm Low!";
                        textBoxSignal.BackColor = Color.Red;
                        
                    }
                    if (recievedData[1] == "3\r")
                    {
                        textBoxSignal.Text = "Alarm High!";
                        textBoxSignal.BackColor = Color.Red;
                    }
                });
            }
            if (recievedData[0] == "readraw")
            {
                textBoxNumeric.Invoke((MethodInvoker)delegate
                {
                    string time = timerRaw.ToString();
                    textBoxNumeric.AppendText(DateTime.Now.ToString("HH:mm:ss") + ", " + recievedData[1] + ";" + "\r\n");
                    if (int.TryParse(graphData[1], out iVab))
                    {
                        rawReading.Add(iVab);
                        rawTime.Add(DateTime.Now);
                        
                        textBoxNumeric.Invoke((MethodInvoker)delegate
                        {
                            chart1.Series["Data"].Points.DataBindXY(rawTime, rawReading);
                        });
                        textBoxNumeric.Invoke((MethodInvoker)delegate
                        { chart1.Invalidate(); });
                        serialPort1.WriteLine("readstatus");
                        try
                        {
                            string TagName, Alarm_status;
                            DateTime TimeStamp = DateTime.Now;
                            TagName = labelTag.Text;
                            int Raw_Data = iVab;
                            Alarm_status = textBoxSignal.Text;
                            SqlConnection con = new SqlConnection(conSSC);
                            SqlCommand sql = new SqlCommand("uspInsertRLog", con);
                            sql.CommandType = CommandType.StoredProcedure;
                            con.Open();
                            //MessageBox.Show(@sql.CommandText);
                            sql.Parameters.Add(new SqlParameter("@TagName", TagName));
                            sql.Parameters.Add(new SqlParameter("@Raw_Data", Raw_Data));
                            sql.Parameters.Add(new SqlParameter("@Alarm_status", Alarm_status));
                            sql.Parameters.Add(new SqlParameter("@TimeStamp", TimeStamp));
                            //MessageBox.Show(@sql.CommandText);
                            sql.ExecuteNonQuery();
                            con.Close();
                        }
                        catch (Exception error)
                        {
                            MessageBox.Show(error.Message);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Gikk Ikke");
                    }
                });
            }

            if (recievedData[0] == "readscaled")
            {
                textBoxNumeric.Invoke((MethodInvoker)delegate
                {
                    serialPort1.WriteLine("readstatus");
                    textBoxNumeric.AppendText(DateTime.Now.ToString("HH:mm:ss") + ", " + recievedData[1] + ";" + "\r\n");
                    string data = graphData[1];
                    scaled = float.Parse(data, CultureInfo.InvariantCulture);
                    scaledReading.Add(scaled);
                    scaledTime.Add(DateTime.Now);
                    chart1.Series["Data"].Points.DataBindXY(scaledTime, scaledReading);
                    chart1.Invalidate();
                    try
                    {
                        string TagName, Alarm_status;
                        DateTime TimeStamp = DateTime.Now;
                        TagName = labelTag.Text;
                        float Scaled_Data = scaled;
                        Alarm_status = textBoxSignal.Text;
                        SqlConnection con = new SqlConnection(conSSC);
                        SqlCommand sql = new SqlCommand("uspInsertSLog", con);
                        sql.CommandType = CommandType.StoredProcedure;
                        con.Open();
                        //MessageBox.Show(@sql.CommandText);
                        sql.Parameters.Add(new SqlParameter("@TagName", TagName));
                        sql.Parameters.Add(new SqlParameter("@Scaled_Data", Scaled_Data));
                        sql.Parameters.Add(new SqlParameter("@Alarm_status", Alarm_status));
                        sql.Parameters.Add(new SqlParameter("@TimeStamp", TimeStamp));
                        //MessageBox.Show(@sql.CommandText);
                        sql.ExecuteNonQuery();
                        con.Close();
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(error.Message);
                    }

                });
            }
        }

        //Timers for connection, status and datareadings
        private void StatusCheck_Tick(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                textBoxStatus.Clear();
                textBoxStatus.AppendText("Sensor connected. \r\nPlease go to configuration.");

            }
            else if (serialPort1.IsOpen == false)
            {
                StatusCheck.Stop();
                MessageBox.Show("Lost Connection. Please reconnect in setup!");
                textBoxStatus.Clear();
                textBoxStatus.AppendText("Disconnected.");
            }

        }
        private void timerRaw_Tick(object sender, EventArgs e)
        {
            try
            {
                serialPort1.WriteLine("readraw");
            }
            catch (System.InvalidOperationException)
            {
            }
        }

        private void timerScaled_Tick(object sender, EventArgs e)
        {
            try
            {
                serialPort1.WriteLine("readscaled");
            }
            catch (System.InvalidOperationException)
            {
            }
        }

        public class InstrumenWrite
        {
            public string Tagname;
            public string LRV;
            public string URV;
            public string AlarmL;
            public string AlarmH;
        }

        public class TimeConverter
        {
            public int Timer;
            public int Load;
        }

        //Voids to Import data from database
        void ImportToComboBoxA()
        {
            SqlConnection con = new SqlConnection(conSSC);
            string sqlQuery = "SELECT AreaName FROM AreaLocation ORDER BY AreaName ASC";
            SqlCommand sql = new SqlCommand(sqlQuery, con);
            con.Open();
            SqlDataReader dr = sql.ExecuteReader();
            while (dr.Read() == true)
            {
                sqlQuery = dr[0].ToString();
                comboBoxARDC.Items.Add(sqlQuery);
                comboBoxADAU.Items.Add(sqlQuery);
                comboBoxAreaIn.Items.Add(sqlQuery);
            }
            con.Close();
        }

        void ImportToComboBoxDAU()
        {
            SqlConnection con = new SqlConnection(conSSC);
            string sqlQuery = "SELECT RDC_ID FROM RemoteDataCollector ORDER BY RDC_ID ASC";
            SqlCommand sql = new SqlCommand(sqlQuery, con);
            con.Open();
            SqlDataReader dr = sql.ExecuteReader();
            while (dr.Read() == true)
            {
                sqlQuery = dr[0].ToString();
                comboBoxRDCID.Items.Add(sqlQuery);

            }
            con.Close();
        }
        void ImportToComboBoxDAUID()
        {
            SqlConnection con = new SqlConnection(conSSC);
            string sqlQuery = "SELECT DAU_ID FROM DataAcquisitionUnit ORDER BY DAU_ID ASC";
            SqlCommand sql = new SqlCommand(sqlQuery, con);
            con.Open();
            SqlDataReader dr = sql.ExecuteReader();
            while (dr.Read() == true)
            {
                sqlQuery = dr[0].ToString();
                comboBoxLDAU.Items.Add(sqlQuery);
            }
            con.Close();
        }

        void ViewSqlResultInDGVL(string sqlQuery)
        {
            try
            {
                SqlConnection con = new SqlConnection(conSSC);
                SqlDataAdapter sda;
                DataTable dt;
                con.Open();
                sda = new SqlDataAdapter(sqlQuery, con);
                dt = new DataTable();
                sda.Fill(dt);
                dgvLoad.DataSource = dt;

                con.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        void ViewSqlResultInDGVA(string sqlQuery)
        {
            try
            {
                SqlConnection con = new SqlConnection(conSSC);
                SqlDataAdapter sda;
                DataTable dt;
                con.Open();
                sda = new SqlDataAdapter(sqlQuery, con);
                dt = new DataTable();
                sda.Fill(dt);
                dgvAdd.DataSource = dt;
                con.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }


        void ViewSqlResultInDGVLD(string sqlQuery)
        {
            try
            {
                SqlConnection con = new SqlConnection(conSSC);
                SqlDataAdapter sda;
                DataTable dt;
                con.Open();
                sda = new SqlDataAdapter(sqlQuery, con);
                dt = new DataTable();
                sda.Fill(dt);
                dgvLD.DataSource = dt;
                con.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        void ViewSqlResultInDGVINST(string sqlQuery)
        {
            try
            {
                SqlConnection con = new SqlConnection(conSSC);
                SqlDataAdapter sda;
                DataTable dt;
                con.Open();
                sda = new SqlDataAdapter(sqlQuery, con);
                dt = new DataTable();
                sda.Fill(dt);
                dgvInst.DataSource = dt;

                con.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }


        //Click Events Tab 1
        private void buttonCon_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.Close();
                serialPort1.PortName = comboBoxPort.Text;
                StatusCheck.Start();
                while (serialPort1.IsOpen) ;
                serialPort1.Open();
                MessageBox.Show("Connection sucessfull. Go to Configuration");
            }
            catch (System.UnauthorizedAccessException)
            {
                StatusCheck.Stop();
                MessageBox.Show("Arduino Serial is open, close it!");
            }
            catch (System.ArgumentException)
            {
                StatusCheck.Stop();
                MessageBox.Show("Check if USB is connected or right port is chosen");
            }
            catch (System.IO.IOException)
            {
                StatusCheck.Stop();
                MessageBox.Show("Lost Connection");

            }
        }

        private void buttonDC_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
                StatusCheck.Stop();
                MessageBox.Show("Disconnected");
                textBoxStatus.Clear();
                textBoxStatus.AppendText("Disconnected.");
            }
            else
            {
                MessageBox.Show("Cannot disconnect when USB not plugged in");
            }
        }

        private void buttonViewRDCL_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM RemoteDataCollector ORDER BY RDC_ID ASC";
            ViewSqlResultInDGVL(sqlQuery);
        }

        private void buttonViewDAUL_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM DataAcquisitionUnit ORDER BY DAU_ID ASC";
            ViewSqlResultInDGVL(sqlQuery);
        }

        //Click event for Tab 2

        private void buttonViewRDC_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM RemoteDataCollector ORDER BY RDC_ID ASC";
            ViewSqlResultInDGVA(sqlQuery);
        }

        private void buttonViewDAUA_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM DataAcquisitionUnit ORDER BY DAU_ID ASC";
            ViewSqlResultInDGVA(sqlQuery);
        }

        private void buttonViewArea_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM AreaLocation ORDER BY AreaName ASC";
            ViewSqlResultInDGVA(sqlQuery);
        }

        private void buttonAddArea_Click(object sender, EventArgs e)
        {
            try
            {
                string AreaName, AreaDesc;
                AreaDesc = textBoxADesc.Text; //Verdi hentes fra tekstboks og lagres i carMake-variabelen
                AreaName = textBoxAName.Text; //Verdien som skal inn i databasen hentes fra //tekstboks og lagres i regNumber-variabelen
                SqlConnection con = new SqlConnection(conSSC);
                SqlCommand sql = new SqlCommand("uspInsertArea", con);
                sql.CommandType = CommandType.StoredProcedure;
                con.Open();
                //MessageBox.Show(@sql.CommandText);
                sql.Parameters.Add(new SqlParameter("@AreaName", AreaName));
                sql.Parameters.Add(new SqlParameter("@AreaDescription", AreaDesc));
                //MessageBox.Show(@sql.CommandText);
                sql.ExecuteNonQuery();
                con.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        private void buttonAddRDC_Click(object sender, EventArgs e)
        {
            try
            {
                string AreaName, RDC_Type;
                RDC_Type = textBoxRDC_Type.Text; 
                AreaName = comboBoxARDC.Text;
                SqlConnection con = new SqlConnection(conSSC);
                SqlCommand sql = new SqlCommand("uspInsertRDC", con);
                sql.CommandType = CommandType.StoredProcedure;
                con.Open();
                //MessageBox.Show(@sql.CommandText);
                sql.Parameters.Add(new SqlParameter("@RDC_Type", RDC_Type));
                sql.Parameters.Add(new SqlParameter("@AreaName", AreaName));
                //MessageBox.Show(@sql.CommandText);
                sql.ExecuteNonQuery();
                con.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        private void buttonAddDAU_Click(object sender, EventArgs e)
        {
            try
            {
                string AreaName, RDC_ID, LinkCom;
                RDC_ID = comboBoxRDCID.Text; //Verdi hentes fra tekstboks og lagres i carMake-variabelen
                AreaName = comboBoxADAU.Text; //Verdien som skal inn i databasen hentes fra //tekstboks og lagres i regNumber-variabelen
                LinkCom = textBoxLinkCom.Text;
                SqlConnection con = new SqlConnection(conSSC);
                SqlCommand sql = new SqlCommand("uspInsertDAU", con);
                sql.CommandType = CommandType.StoredProcedure;
                con.Open();
                //MessageBox.Show(@sql.CommandText);
                sql.Parameters.Add(new SqlParameter("@LinkCommunication", LinkCom));
                sql.Parameters.Add(new SqlParameter("@RDC_ID", RDC_ID));
                sql.Parameters.Add(new SqlParameter("@AreaName", AreaName));
                //MessageBox.Show(@sql.CommandText);
                sql.ExecuteNonQuery();
                con.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        private void buttonViewScaled_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM DataLogScaled ORDER BY TimeStamp DESC";
            ViewSqlResultInDGVLD(sqlQuery);
        }

        private void buttonViewRaw_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM DataLogRaw ORDER BY TimeStamp DESC";
            ViewSqlResultInDGVLD(sqlQuery);
        }

        private void buttonRaw_Click(object sender, EventArgs e)
        {
            timerScaled.Stop();
            textBoxNumeric.Clear();
            if (serialPort1.IsOpen)
            {
                if (textBoxScan.Text == "")
                {
                    timerRaw.Start();
                    labelData.Text = "Raw Data.";
                }
                else
                {
                    timerRaw.Interval = int.Parse(textBoxScan.Text)*1000;
                    timerRaw.Start();
                    labelData.Text = "Raw Data.";
                }
            }
            else
            {
                MessageBox.Show("Not Connected");
            }
        }

        private void buttonScaled_Click(object sender, EventArgs e)
        {

            timerRaw.Stop();
            textBoxNumeric.Clear();
            if (serialPort1.IsOpen)
            {
                if (textBoxScan.Text == "")
                {
                    timerScaled.Start();
                    labelData.Text = "Raw Data.";
                }
                else
                {
                    timerScaled.Interval = int.Parse(textBoxScan.Text) * 1000;
                    timerScaled.Start();
                    labelData.Text = "Scaled Data.";
                }
            }
            else
            {
                MessageBox.Show("Not Connected");
            }
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                timerScaled.Stop();
                timerRaw.Stop();
                MessageBox.Show("Reading stopped");
            }
            else
            {
                MessageBox.Show("Not connected");
            }
        }

        private void buttonVC_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.WriteLine("readconf");
            }
            else
            {
                MessageBox.Show("Not Connected");
            }
        }

        private void buttonWNC_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                InstrumenWrite writenew = new InstrumenWrite();
                writenew.Tagname = textBoxTag.Text;
                writenew.LRV = textBoxLRV.Text;
                writenew.URV = textBoxURV.Text;
                writenew.AlarmL = textBoxAL.Text;
                writenew.AlarmH = textBoxAH.Text;
                string[] writeconf = { writenew.Tagname, writenew.LRV, writenew.URV, writenew.AlarmL, writenew.AlarmH };
                string Writeconf = string.Join(";", writeconf);
                string password = Interaction.InputBox("Enter Password: ", "Password", "..", 10, 10);
                serialPort1.WriteLine("writeconf>" + password + ">" + Writeconf);
                labelDesc.Text = textBoxDescrip.Text;
                labelDAU.Text = comboBoxLDAU.Text;
                labelChannel.Text = textBoxChannel.Text;
                labelArea.Text = comboBoxAreaIn.Text;
                labelScan.Text = textBoxScan.Text;
                int scan_frequence = int.Parse(textBoxScan.Text);


            }
            else
            {
                MessageBox.Show("Not connected");
            }

        }

        private void textBoxSignal_TextChanged(object sender, EventArgs e)
        {
            if (timerScaled.Enabled == true)
            {
                timerStatus.Start();
            }
            if (timerRaw.Enabled == true)
            {
                timerStatus.Start();
            }
        }

        private void buttonSC_Click(object sender, EventArgs e)
        {
            string Tagname, Description, DAU_ID, Area, Channel, LRV, URV, AL, AH, Scan;
            Tagname = textBoxTag.Text; //Verdi hentes fra tekstboks og lagres i carMake-variabelen
            Description = textBoxDescrip.Text;
            DAU_ID = comboBoxLDAU.Text; //Verdien som skal inn i databasen hentes fra //tekstboks og lagres i regNumber-variabelen
            Area = comboBoxAreaIn.Text;
            Channel = textBoxChannel.Text;
            LRV = textBoxLRV.Text;
            URV = textBoxURV.Text;
            AL = textBoxAL.Text;
            AH = textBoxAH.Text;
            Scan = textBoxScan.Text;
            SqlConnection con = new SqlConnection(conSSC);
            SqlCommand sql = new SqlCommand("uspInsertInstrument", con);
            sql.CommandType = CommandType.StoredProcedure;
            con.Open();
            //MessageBox.Show(@sql.CommandText);
            sql.Parameters.Add(new SqlParameter("@TagName", Tagname));
            sql.Parameters.Add(new SqlParameter("@Instrument_Description", Description));
            sql.Parameters.Add(new SqlParameter("@DAU_ID", DAU_ID));
            sql.Parameters.Add(new SqlParameter("@AreaName", Area));
            sql.Parameters.Add(new SqlParameter("@Channel", Channel));
            sql.Parameters.Add(new SqlParameter("@LRV", LRV));
            sql.Parameters.Add(new SqlParameter("@URV", URV));
            sql.Parameters.Add(new SqlParameter("@Alarm_Low", AL));
            sql.Parameters.Add(new SqlParameter("@Alarm_High", AH));
            sql.Parameters.Add(new SqlParameter("@Scan_HZ", Scan));
            //MessageBox.Show(@sql.CommandText);
            sql.ExecuteNonQuery();
            con.Close();
        }

        private void buttonLC_Click(object sender, EventArgs e)
        {
            SqlConnection con = new SqlConnection(conSSC);
            string sqlQuery = "SELECT TagName, Instrument_Description, DAU_ID, AreaName, Channel, LRV, URV, Alarm_Low, Alarm_High, Scan_HZ FROM Instrument";
            SqlCommand sql = new SqlCommand(sqlQuery, con);
            con.Open();
            SqlDataReader dr = sql.ExecuteReader();
            while (dr.Read() == true)
            {
                sqlQuery = dr[0].ToString();
                TagName.Add(sqlQuery);
                sqlQuery = dr[1].ToString();
                Desc.Add(sqlQuery);
                sqlQuery = dr[2].ToString();
                DAU.Add(sqlQuery);
                sqlQuery = dr[3].ToString();
                Area.Add(sqlQuery);
                sqlQuery = dr[4].ToString();
                Channel.Add(sqlQuery);
                sqlQuery = dr[5].ToString();
                LRV.Add(sqlQuery);
                sqlQuery = dr[6].ToString();
                URV.Add(sqlQuery);
                sqlQuery = dr[7].ToString();
                AL.Add(sqlQuery);
                sqlQuery = dr[8].ToString();
                AH.Add(sqlQuery);
                sqlQuery = dr[9].ToString();
                Scan.Add(sqlQuery);

            }
            con.Close();

            TimeConverter load = new TimeConverter();
            load.Load = Convert.ToInt32(textBoxRow.Text);
            textBoxTag.Text = TagName[load.Load];
            textBoxDescrip.Text = Desc[load.Load];
            comboBoxAreaIn.Text = Area[load.Load];
            textBoxChannel.Text = Channel[load.Load];
            textBoxLRV.Text = LRV[load.Load];
            textBoxURV.Text = URV[load.Load];
            textBoxAL.Text = AL[load.Load];
            textBoxAH.Text = AH[load.Load];
            textBoxScan.Text = Scan[load.Load];
        }

        private void buttonInst_Click(object sender, EventArgs e)
        {
            string sqlQuery = @"SELECT * FROM Instrument";
            ViewSqlResultInDGVINST(sqlQuery);
        }
       
    }

}
