using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace LCDM4000InterfaceWrapper
{
    public enum ERR_CODE_DISPENSER { None, None_Error=32, Bill_Pick_Up_Error = 0x21, Jam_Between_CHK_and_DVT, Jam_Between_DVT_and_EJT, Jam_Between_EJT_and_EXIT, Note_Staying_in_EXIT, Ejecting_Note_Suspected_As_Rejected, Note_Count_Mismatch, Note_Should_be_Rejected_Passed_EJT, Dispensing_Too_Many_Notes_In_One = 0x2C, Rejecting_Too_Many_Notes_In_One, Abnormal_Termination_During_PURGE, Detecting_No_Cassette_Requested_To_Dispense_Bills = 0x44, Detecting_NEAREND_Status_In_Requested_Cassette, Note_From_Cassette1_Without_Request = 64 + 0x20, Note_From_Cassette2_Without_Request, Note_From_Cassette3_Without_Request, Note_From_Cassette4_Without_Request };

    public struct StatusResponse
    {
        public bool success;
        public ERR_CODE_DISPENSER mainError;
        public bool isCassette1Exists;
        public bool isCassette2Exists;
        public bool isCassette3Exists;
        public bool isCassette4Exists;
        public bool? isCassette1NearEnd;
        public bool? isCassette2NearEnd;
        public bool? isCassette3NearEnd;
        public bool? isCassette4NearEnd;
    }

    public struct DispenseMessage
    {
        public int cassette1Bills;
        public int cassette2Bills;
        public int cassette3Bills;
        public int cassette4Bills;
        public int timeOut;
    }

    public struct DispenseResult
    {
        public int cassette1Dispensed;
        public int cassette2Dispensed;
        public int cassette3Dispensed;
        public int cassette4Dispensed;
        public StatusResponse status;
    }
    /// <summary>
    /// Based on Document LCDM-4000 Interface Specification by Puloon Tech
    /// LCDM-4000 Cash Dispenser Class Library
    /// 2018.11 Ryu Youngseok
    /// </summary>
    public class BillDispenser
    {
        public static readonly ILog Logger = LCDM4000InterfaceWrapper.Logger.LogWriter;

        public Dictionary<byte, string> CommandNames = new Dictionary<byte, string>();
        private SerialPort internalSerialPort = new SerialPort();

        private readonly byte SOH = 0x01;
        private readonly byte STX = 0x02;
        private readonly byte ETX = 0x03;
        private readonly byte EOT = 0x04;
        private readonly byte ACK = 0x06;
        private readonly byte NAK = 0x15;
        private readonly byte RESET = 0x44;
        private readonly byte STATUS = 0x50;
        private readonly byte PURGE = 0x51;
        private readonly byte DISPENSE = 0x52;
        private readonly byte LASTSTATUS = 0x55;

        public BillDispenser()
        {
            internalSerialPort.BaudRate = 9600;
            internalSerialPort.StopBits = StopBits.One;
            internalSerialPort.Parity = Parity.None;
            internalSerialPort.DataBits = 8;
            SetCommandDictionary();
            Logger.Info("BillDispenser - Start Up");
        }

        public bool IsOpen
        {
            get
            {
                return internalSerialPort.IsOpen;
            }
        }

        private void SetCommandDictionary()
        {
            BindingFlags bindingFlags = BindingFlags.NonPublic |
                            BindingFlags.Instance |
                            BindingFlags.Static;

            var values = this.GetType()
                     .GetFields(bindingFlags)
                     .Select(field => field.GetValue(this))
                     .ToList();
            var names = typeof(BillDispenser).GetFields()
                            .Select(field => field.Name)
                            .ToList();
            foreach (FieldInfo field in typeof(BillDispenser).GetFields(bindingFlags))
            {
                if (field.IsInitOnly)
                {
                    CommandNames.Add((byte)field.GetValue(this), field.Name.ToUpperInvariant());
                }
            }
        }

        /// <summary>
        /// Set the serial port name.
        /// </summary>
        public bool SetPort(string _port)
        {
            try
            {
                internalSerialPort.PortName = _port;
                Logger.Info("BillDispenser - Setted Port " + _port);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private byte GetBCC(byte[] b)
        {
            byte result = 0x00;
            foreach (var x in b)
            {
                result = (byte)(result ^ x);
            }
            return result;
        }
        /// <summary>
        /// Reset the Dispenser.
        /// </summary>
        public void InitializeDispenser()
        {
            if (internalSerialPort.IsOpen == false) return;
            internalSerialPort.ReadExisting();
            WriteBytes(new byte[] { EOT, 0x30, STX, RESET, ETX }.ToList());
        }

        /// <summary>
        /// Open the port, and return bool value depends on success or not.
        /// </summary>
        /// <returns></returns>
        public bool OpenPort()
        {
            try
            {
                internalSerialPort.Open();
                Logger.Info("BillAccepter - Open Port");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Close the port, and return bool value depends on success or not.
        /// </summary>
        /// <returns></returns>
        public bool ClosePort()
        {
            try
            {
                internalSerialPort.Close();
                Logger.Info("BillDispenser - 포트 닫음 ");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<DispenseResult> DispenseAsync(DispenseMessage amount)
        {
            return Task.Run(() =>
            {
                return Dispense(amount);
            });
        }

        public Task<DispenseResult> GetLastStatusAsync()
        {
            return Task.Run(() =>
            {
                return GetLastStatus();
            });
        }

        public Task<StatusResponse> CheckStatusofDispenserAsync()
        {
            return Task.Run(() =>
            {
                return CheckStatusofDispenser();
            });
        }

        /// <summary>
        /// Check Status of Dispenser and returns result.
        /// </summary>
        /// <returns></returns>
        public StatusResponse CheckStatusofDispenser()
        {
            StatusResponse result = new StatusResponse();
            result.mainError = ERR_CODE_DISPENSER.None;
            if (internalSerialPort.IsOpen == false) return result;
            internalSerialPort.ReadExisting();
            WriteBytes(new byte[] { EOT, 0x30, STX, STATUS, ETX }.ToList());
            List<byte> buffer = new List<byte>();
            List<byte> MSG = new List<byte>();

            Thread.Sleep(50);

            byte[] buf = new byte[1];
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);
            if (buf[0] == NAK) return result;

            while (internalSerialPort.BytesToRead == 0) Thread.Sleep(100);


            while (internalSerialPort.BytesToRead > 0)
            {
                byte[] rawdata = new byte[internalSerialPort.BytesToRead];
                internalSerialPort.Read(rawdata, 0, rawdata.Length);
                buffer.AddRange(rawdata);
                Thread.Sleep(200);
            }
            WriteByte(ACK);
            Queue<int> SOHs = new Queue<int>();

            for (int i = 0; i < buffer.Count; i++) if (buffer[i] == SOH) SOHs.Enqueue(i);

            while (SOHs.Count > 0)
            {
                Queue<int> ETXs = new Queue<int>();
                int SOH = SOHs.Dequeue();
                for (int i = 0; i < buffer.Count; i++) if (buffer[i] == ETX) ETXs.Enqueue(i);
                while (ETXs.Count > 0)
                {
                    try
                    {
                        int ETX = ETXs.Dequeue();
                        byte lastitem = buffer[ETX + 1];
                        List<byte> cropped = buffer.GetRange(SOH, ETX + 1);
                        if (lastitem == GetBCC(cropped.ToArray()))
                        {
                            MSG = cropped;
                            buffer.Clear();
                            ETXs.Clear();
                            SOHs.Clear();
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
                buffer.RemoveRange(0, SOH);

            }
            result.mainError = (ERR_CODE_DISPENSER)MSG[4];
            result.isCassette1NearEnd = (MSG[6] & 8) != 0 ? true : false;
            if (MSG[7] == 0x31) result.isCassette1Exists = true;
            result.isCassette2NearEnd = (MSG[10] & 8) != 0 ? true : false;
            if (MSG[11] == 0x32) result.isCassette2Exists = true;
            result.isCassette3NearEnd = (MSG[15] & 8) != 0 ? true : false;
            if (MSG[15] == 0x33) result.isCassette3Exists = true;
            result.isCassette4NearEnd = (MSG[18] & 8) != 0 ? true : false;
            if (MSG[19] == 0x34) result.isCassette4Exists = true;
            result.success = true;
            BytesLog(MSG);
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);

            while (internalSerialPort.BytesToRead > 0) internalSerialPort.ReadExisting();

            return result;
        }

        /// <summary>
        /// Execute PURGE of dispenser.
        /// </summary>
        public void ClearErrors()
        {
            if (internalSerialPort.IsOpen == false) return;
            internalSerialPort.ReadExisting();
            WriteBytes(new byte[] { EOT, 0x30, STX, PURGE, ETX }.ToList());

            byte[] buf = new byte[1];
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);
            if (buf[0] == NAK) return;

            List<byte> buffer = new List<byte>();
            while (internalSerialPort.BytesToRead == 0) Thread.Sleep(100);

            while (internalSerialPort.BytesToRead > 0)
            {
                byte[] rawdata = new byte[internalSerialPort.BytesToRead];
                internalSerialPort.Read(rawdata, 0, rawdata.Length);
                buffer.AddRange(rawdata);
                Thread.Sleep(500);
            }

            WriteByte(ACK);
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);
            while (internalSerialPort.BytesToRead > 0) internalSerialPort.ReadExisting();

        }

        /// <summary>
        /// Send DISP message to dispenser.
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public DispenseResult Dispense(DispenseMessage amount)
        {
            DispenseResult result = new DispenseResult();
            List<byte> buffer = new List<byte>();
            List<byte> MSG = new List<byte>();
            if (internalSerialPort.IsOpen == false) return result;
            internalSerialPort.ReadExisting();
            byte timeout1 = amount.timeOut == 0 ? (byte)0x20 : (byte)0x1C;
            byte timeout2 = amount.timeOut == 0 ? (byte)0x20 : (byte)(0x30 + amount.timeOut);
            byte[] msg = { EOT, 0x30, STX, DISPENSE, (byte)(0x20 + amount.cassette1Bills), (byte)(0x20 + amount.cassette2Bills), (byte)(0x20 + amount.cassette3Bills), (byte)(0x20 + amount.cassette4Bills), timeout1, timeout2, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, ETX };
            RequestLog(amount.cassette1Bills, amount.cassette2Bills, amount.cassette3Bills, amount.cassette4Bills);
            WriteBytes(new byte[] { EOT, 0x30, STX, DISPENSE, (byte)(0x20 + amount.cassette1Bills), (byte)(0x20 + amount.cassette2Bills), (byte)(0x20 + amount.cassette3Bills), (byte)(0x20 + amount.cassette4Bills), timeout1, timeout2, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, ETX }.ToList());

            Thread.Sleep(50);

            byte[] buf = new byte[1];
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);
            if (buf[0] == NAK) return result;

            Thread.Sleep(500);

            while (internalSerialPort.BytesToRead == 0) Thread.Sleep(100);

            while (internalSerialPort.BytesToRead > 0)
            {
                byte[] rawdata = new byte[internalSerialPort.BytesToRead];
                internalSerialPort.Read(rawdata, 0, rawdata.Length);
                buffer.AddRange(rawdata);
                Thread.Sleep(500);
            }
            WriteByte(ACK);
            Queue<int> SOHs = new Queue<int>();

            for (int i = 0; i < buffer.Count; i++) if (buffer[i] == SOH) SOHs.Enqueue(i);

            while (SOHs.Count > 0)
            {
                Queue<int> ETXs = new Queue<int>();
                int SOH = SOHs.Dequeue();
                for (int i = 0; i < buffer.Count; i++) if (buffer[i] == ETX) ETXs.Enqueue(i);
                while (ETXs.Count > 0)
                {
                    try
                    {
                        int ETX = ETXs.Dequeue();
                        byte lastitem = buffer[ETX + 1];
                        List<byte> cropped = buffer.GetRange(SOH, ETX + 1);
                        if (lastitem == GetBCC(cropped.ToArray()))
                        {
                            MSG = cropped;
                            ETXs.Clear();
                            SOHs.Clear();
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                    buffer.RemoveRange(0, SOH);
                }
            }

            try
            {
                result.status.mainError = (ERR_CODE_DISPENSER)MSG[4];
                result.status.isCassette1Exists = MSG[8] == 0x30 ? false : true;
                result.status.isCassette1Exists = MSG[11] == 0x30 ? false : true;
                result.status.isCassette1Exists = MSG[14] == 0x30 ? false : true;
                result.status.isCassette1Exists = MSG[17] == 0x30 ? false : true;
                result.cassette1Dispensed = MSG[6] - 0x20;
                result.cassette2Dispensed = MSG[9] - 0x20;
                result.cassette3Dispensed = MSG[12] - 0x20;
                result.cassette4Dispensed = MSG[15] - 0x20;
                result.status.success = true;
                BytesLog(MSG);
                DispensedLog(result.cassette1Dispensed, result.cassette2Dispensed, result.cassette3Dispensed, result.cassette4Dispensed);
            }
            catch
            {
                result.status.success = false;
            }
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);
            while (internalSerialPort.BytesToRead > 0) internalSerialPort.ReadExisting();
            return result;
        }

        /// <summary>
        /// return LAST STATUS.
        /// </summary>
        /// <returns></returns>
        public DispenseResult GetLastStatus()
        {
            DispenseResult result = new DispenseResult();

            List<byte> MSG = new List<byte>();
            List<byte> buffer = new List<byte>();

            if (internalSerialPort.IsOpen == false) return result;
            internalSerialPort.ReadExisting();

            WriteBytes(new byte[] { EOT, 0x30, STX, LASTSTATUS, ETX }.ToList());

            Thread.Sleep(50);

            byte[] buf = new byte[1];
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);
            if (buf[0] == NAK) return result;

            Thread.Sleep(500);

            while (internalSerialPort.BytesToRead > 0)
            {
                byte[] rawdata = new byte[internalSerialPort.BytesToRead];
                internalSerialPort.Read(rawdata, 0, rawdata.Length);
                buffer.AddRange(rawdata);
                Thread.Sleep(500);
            }

            WriteByte(ACK);
            Queue<int> SOHs = new Queue<int>();
            for (int i = 0; i < buffer.Count; i++) if (buffer[i] == SOH) SOHs.Enqueue(i);
            while (SOHs.Count > 0)
            {
                Queue<int> ETXs = new Queue<int>();
                int SOH = SOHs.Dequeue();
                for (int i = 0; i < buffer.Count; i++) if (buffer[i] == ETX) ETXs.Enqueue(i);
                while (ETXs.Count > 0)
                {
                    try
                    {
                        int ETX = ETXs.Dequeue();
                        byte lastitem = buffer[ETX + 1];
                        List<byte> cropped = buffer.GetRange(SOH, ETX + 1);
                        if (lastitem == GetBCC(cropped.ToArray()))
                        {
                            MSG = cropped;
                            buffer.Clear();
                            ETXs.Clear();
                            SOHs.Clear();
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            BytesLog(MSG,5);
            int lastcmd = MSG[4];
            MSG.RemoveAt(4);
            result.status.mainError = (ERR_CODE_DISPENSER)MSG[4];
            result.status.isCassette1Exists = MSG[8] == 0x30 ? false : true;
            result.status.isCassette1Exists = MSG[11] == 0x30 ? false : true;
            result.status.isCassette1Exists = MSG[14] == 0x30 ? false : true;
            result.status.isCassette1Exists = MSG[17] == 0x30 ? false : true;
            result.cassette1Dispensed = MSG[6] - 0x20;
            result.cassette2Dispensed = MSG[9] - 0x20;
            result.cassette3Dispensed = MSG[12] - 0x20;
            result.cassette4Dispensed = MSG[15] - 0x20;
            DispensedLog(result.cassette1Dispensed, result.cassette2Dispensed, result.cassette3Dispensed, result.cassette4Dispensed, true);
            result.status.success = true;
            internalSerialPort.Read(buf, 0, 1);
            ByteLog(buf[0]);
            while (internalSerialPort.BytesToRead > 0) internalSerialPort.ReadExisting();
            return result;
        }

        private void WriteByte(byte b)
        {
            internalSerialPort.Write(new byte[] { b }, 0, 1);
            string log = "BillDispenser - Sent: ";
            try
            {
                log += string.Format("{0} ({1}) ", string.Concat("0x", String.Format("{0:X}", b).PadLeft(2, '0')), CommandNames[b]);
            }
            catch
            {
                log += string.Format("{0} ", string.Concat("0x", String.Format("{0:X}", b).PadLeft(2, '0')));
            }
            finally
            {
                Logger.Info(log);
            }
        }

        private void WriteBytes(List<byte> bytes)
        {
            byte result = 0x00;
            foreach (var x in bytes)
            {
                result = (byte)(result ^ x);
            }
            bytes.Add(result);
            byte[] writeBytes = bytes.ToArray();
            internalSerialPort.Write(writeBytes, 0, writeBytes.Length);
            string log = "BillDispenser - Sent: ";
            foreach (var b in bytes)
            {
                try
                {
                    log += string.Format("{0} ({1}) ", string.Concat("0x", String.Format("{0:X}", b).PadLeft(2, '0')), CommandNames[b]);
                }
                catch
                {
                    log += string.Format("{0} ", string.Concat("0x", String.Format("{0:X}", b).PadLeft(2, '0')));
                }
            }
            Logger.Info(log);
        }

        private void BytesLog(List<byte> buffer, int Errorpos=4)
        {
            string log = "BillDispenser - Received: ";
            for(int i = 0; i < buffer.Count; i++)
            {
                byte x = buffer[i];
                if (System.Enum.IsDefined(typeof(ERR_CODE_DISPENSER), (int)x) && i==Errorpos)
                {
                    log += string.Format("{0} (ERROR: {1}) ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')), (ERR_CODE_DISPENSER)(int)x);
                }
                else if (CommandNames.ContainsKey(x))
                {
                    log += string.Format("{0} ({1}) ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')), CommandNames[x]);
                }
                else
                {
                    log += string.Format("{0} ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')));
                }
            }
            Logger.Info(log);
        }

        private void DispensedLog(int C1,int C2,int C3,int C4,bool isLast=false)
        {
            string log = "BillDispenser -- Dispensed: ";
            log += string.Format("Cassette 1: {0}, Cassette 2: {1}, Cassette 3: {2}, Cassette 4: {3}", C1, C2, C3, C4);
            if (isLast) log += " (LAST STATUS)";
            Logger.Info(log);
        }

        private void RequestLog(int C1, int C2, int C3, int C4)
        {
            string log = "BillDispenser - Requested: ";
            log += string.Format("Cassette 1: {0}, Cassette 2: {1}, Cassette 3: {2}, Cassette 4: {3}", C1, C2, C3, C4);
            Logger.Info(log);
        }

        private void ByteLog(byte x, bool isError = false)
        {
            string log = "BillDispenser -- Received: ";
            if (System.Enum.IsDefined(typeof(ERR_CODE_DISPENSER), (int)x) && isError)
            {
                log += string.Format("{0} (ERROR: {1}) ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')), (ERR_CODE_DISPENSER)(int)x);
            }
            else if (CommandNames.ContainsKey(x))
            {
                log += string.Format("{0} ({1}) ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')), CommandNames[x]);
            }
            else
            {
                log += string.Format("{0} ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')));
            }
            Logger.Info(log);
        }
    }

}
