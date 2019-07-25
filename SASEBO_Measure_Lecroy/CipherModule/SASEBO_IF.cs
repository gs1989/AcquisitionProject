using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;
using FTD2XX_NET;
namespace SASEBO_Measure_Lecroy.CipherModule
{
    //================================================================ SASEBO_IF_Exception
    class SASEBO_G_Exception : Exception
    {
        public SASEBO_G_Exception(string msg) : base(msg) { }
    }

    //================================================================ RS232 for SASEBO-W VCP
    // (Support of RS-232 port on SASEBO-R/-G/-B is discontinued.)
    #region RS-232
    class RS232
    {
        private string port;
        private SerialPort serialport;

        //------------------------------------------------ Constructor
        public RS232(string port)
        {
            this.port = port;
        }

        //------------------------------------------------ open()
        public void open()
        {
            serialport = new SerialPort(port, 9600, Parity.None, 8, StopBits.One);
            serialport.Open();
            serialport.DtrEnable = false;
            serialport.RtsEnable = false;
            serialport.Handshake = Handshake.None;
            serialport.ReadTimeout = 3000;
            serialport.WriteTimeout = 3000;
        }

        //------------------------------------------------ close()
        public void close()
        {
            serialport.Close();
        }

        //------------------------------------------------ write()
        public void write(byte[] dat, int len)
        {
            serialport.Write(dat, 0, len);
        }

        //------------------------------------------------ read()
        public void read(byte[] dat, int len)
        {
            int count = 0;
            while (serialport.BytesToRead < len)
            {
                System.Threading.Thread.Sleep(50);//延时
                count++;
                if (count == 5)
                {
                    close();
                    open();
                    return;
                };
            };
            serialport.Read(dat, 0, len);
        }

        //------------------------------------------------ flush()
        public void flush()
        {
            serialport.RtsEnable = true;
            System.Threading.Thread.Sleep(200);
            serialport.RtsEnable = false;
            System.Threading.Thread.Sleep(500);

            while (serialport.BytesToRead > 0)
            {
                string st = serialport.ReadByte().ToString("X02") + " ";
                Debug.Write(st);
            }
            Debug.WriteLine("");
        }

        //------------------------------------------------ version()
        public string version()
        {
            serialport.RtsEnable = true;
            System.Threading.Thread.Sleep(200);
            serialport.RtsEnable = false;
            System.Threading.Thread.Sleep(500);

            string ver_st = "";
            while (serialport.BytesToRead > 0)
            {
                ver_st += serialport.ReadByte().ToString("X02") + " ";
            }
            return ver_st;
        }
    }
    #endregion

    //================================================================ FTDI_USB
    #region FTDI USB
    class FTDI_USB
    {
        volatile FTDI ftdi = new FTDI();
        Stopwatch sw = new Stopwatch();

        //------------------------------------------------ Constructor
        public FTDI_USB() { }

        //------------------------------------------------ open()
        public void open(uint idx)
        {
            uint num_device = 0;

            ftdi.GetNumberOfDevices(ref num_device);
            Debug.WriteLine("Number of FTDI devices : " + num_device);
            FTDI.FT_DEVICE_INFO_NODE[] device_list = new FTDI.FT_DEVICE_INFO_NODE[num_device];
            ftdi.GetDeviceList(device_list);

            foreach (FTDI.FT_DEVICE_INFO_NODE device in device_list)
            {
                Debug.WriteLine("--");
                Debug.WriteLine("Flags  : " + device.Flags);
                Debug.WriteLine("Type   : " + device.Type);
                Debug.WriteLine("ID	    : " + device.ID);
                Debug.WriteLine("LocID  : " + device.LocId);
                Debug.WriteLine("Serial : " + device.SerialNumber);
                Debug.WriteLine("Info   : " + device.Description);
            }

            ftdi.OpenByIndex(idx);
            if (!ftdi.IsOpen) throw (new SASEBO_G_Exception("Failed to open device."));
            ftdi.SetTimeouts(500, 500);
        }

        //------------------------------------------------ close()
        public void close()
        {
            ftdi.Close();
        }

        //------------------------------------------------ write()
        public void write(byte[] dat, int len)
        {
            uint wlen = 0;
            ftdi.Write(dat, len, ref wlen);
        }

        //------------------------------------------------ read()
        public void read(byte[] dat, int len)
        {
            uint rlen = 0;
            sw.Start();
            do
            {
                ftdi.GetRxBytesAvailable(ref rlen);
                if (sw.ElapsedMilliseconds > 1000) throw (new SASEBO_G_Exception("Read timeout"));
            } while (rlen < len);
            sw.Reset();
            ftdi.Read(dat, (uint)len, ref rlen);
        }
    }
    #endregion
}
