using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SASEBO_Measure_Lecroy.CipherModule;
using System.Threading.Tasks;
namespace SASEBO_Measure_Lecroy
{
    class Measurement
    {
        Random ra;
        private short _handle;
        private int _channelCount;
        private int _digitalPorts;

        public const int QUAD_SCOPE = 4;
        public const int DUAL_SCOPE = 2;
        public ushort[] inputRanges = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };
        public short[] measurements;
        public short[][] Mulmeasurements;
        public CipherModule.GeneralDevice cipher_hw;
        public CipherTool.IBlockCipher cipher_sw;
        public FileStream fs;
        public BinaryWriter wr;

        public static UnivariateTtest Tvla=null;
        public static UnivariateTtest Tvla1 = null;

        short[] temp ;
        double[] doubletemp;
        byte[] text_ans;
        byte[] text_in;
        byte[] text_out;
        //public byte[] plain;
        //public byte[] cipher;
        /****************************************************************************
         *读取设备信息
         ****************************************************************************/
        void GetDeviceInfo()
        {
            int variant = 0;
            string[] description = {
                           "Driver Version    ",
                           "USB Version       ",
                           "Hardware Version  ",
                           "Variant Info      ",
                           "Serial            ",
                           "Cal Date          ",
                           "Kernel Ver        ",
                           "Digital Hardware  ",
                           "Analogue Hardware "
                         };

            System.Text.StringBuilder line = new System.Text.StringBuilder(80);

            if (_handle >= 0)
            {
                for (int i = 0; i < 9; i++)
                {
                    short requiredSize;
                    Imports.GetUnitInfo(_handle, line, 80, out requiredSize, i);

                    if (i == 3)
                    {
                        if (line.ToString().EndsWith("MSO"))
                        {
                            variant = Convert.ToInt16((line.ToString().Remove(4, 3)));  // Use the numeric part of the variant number
                        }
                        else if (line.ToString().EndsWith("A"))
                        {
                            variant = Convert.ToInt16((line.ToString().Remove(4, 1)));  // Handle 'A' variants
                        }
                        else
                        {
                            variant = Convert.ToInt16(line.ToString());
                        }

                    }

                    Console.WriteLine("{0}: {1}", description[i], line);
                }

                switch (variant)
                {
                    case (int)Imports.Model.PS2205MSO:
                       // _firstRange = Imports.Range.Range_50MV;
                       // _lastRange = Imports.Range.Range_20V;
                        _channelCount = DUAL_SCOPE;
                        _digitalPorts = 2;
                        break;

                    default:
                       // _firstRange = Imports.Range.Range_50MV;
                       // _lastRange = Imports.Range.Range_20V;
                        _channelCount = DUAL_SCOPE;
                        _digitalPorts = 0;
                        break;

                }
            }
        }


        /****************************************************************************
        * 切换电源
        ****************************************************************************/
        //public uint PowerSourceSwitch(short handle, uint status)
        //{
        //    char ch;

        //    switch (status)
        //    {
        //        case Imports.PICO_POWER_SUPPLY_NOT_CONNECTED:
        //            {
        //                Console.WriteLine("5V Power Supply not connected");
        //                Console.WriteLine("Powering the unit via USB");
        //                status = Imports.ChangePowerSource(handle, status);
        //            }
        //            break;

        //        case Imports.PICO_POWER_SUPPLY_CONNECTED:
        //            Console.WriteLine("Using 5V power supply voltage");
        //            status = Imports.ChangePowerSource(handle, status);
        //            break;

        //        case Imports.PICO_POWER_SUPPLY_UNDERVOLTAGE:
        //            do
        //            {
        //                Console.WriteLine("");
        //                Console.WriteLine("\nUSB not supplying required voltage");
        //                Console.WriteLine("\nPlease plug in the +5V power supply,");
        //                Console.WriteLine("then hit a key to continue, or Esc to exit...");
        //                ch = Console.ReadKey().KeyChar;

        //                if (ch == 0x1B)
        //                {
        //                    Environment.Exit(0);
        //                }
        //                status = PowerSourceSwitch(handle, Imports.PICO_POWER_SUPPLY_CONNECTED);
        //            }
        //            while (status == (short)Imports.PICO_POWER_SUPPLY_REQUEST_INVALID);
        //            break;
        //    }
        //    return status;
        //}

        /****************************************************************************
         * 打开设备
         ****************************************************************************/
        public short deviceOpen()
        {
            short status = Imports.OpenUnit(out _handle, null);

            return status;
        }

        /****************************************************************************
         * mv_to_adc
         *
         * 将mv转换为计数
         *
         ****************************************************************************/
        short mv_to_adc(short mv, short ch)
        {
            return (short)((mv * Imports.MaxValue) / inputRanges[ch]);
        }
        //准备示波器：打开，A通道采集，20MV范围，-105mv偏移，外部触发，采样率1GHz
        public bool PrepareScope(int samples,uint delay)
        {
            //1、打开示波器
            if (deviceOpen() != 0)
            {
                System.Console.WriteLine("打开设备失败!\n");
                return false;
            }
            //GetDeviceInfo();
            //2、设置通道
            short ret = 0;
            //1表示DC,0表示AC
            //A通道打开，DC，范围为20mv,偏移-105MV
            //ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 1, Imports.Range.Range_500MV, (float)-1.05);
            ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 0, Imports.Range.Range_500MV, (float)0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelA出错！");
                return false;
            }
            //B通道关闭
            Imports.SetChannel(_handle, Imports.Channel.ChannelB, 1, 0, Imports.Range.Range_5V, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelB出错！");
                return false;
            }
            //3、设置采样率
            int timeIntervalNanoseconds, max_samples;
            //获取最大采样点数
            Imports.GetTimebase(_handle, 6, (int)samples, out timeIntervalNanoseconds, 0, out max_samples, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("GetTimebase出错！");
                return false;
            }
            //4、设置触发信号
            short triggerVoltage = mv_to_adc((short)1000, (short)Imports.Range.Range_5V); // ChannelInfo stores ADC counts
            ret = Imports.SetTrigger(_handle, 1, Imports.Channel.ChannelB, triggerVoltage, Imports.ThresholdDirection.Rising, (uint)delay, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetSimpleTrigger出错！");
                return false;
            }
            return true;
        }

        //准备示波器：打开，A通道采集，20MV范围，-105mv偏移，外部触发，采样率1GHz
        public bool PrepareScope(int samples, uint delay,uint timebase)
        {
            //1、打开示波器
            if (deviceOpen() != 0)
            {
                System.Console.WriteLine("打开设备失败!\n");
                return false;
            }
            //GetDeviceInfo();
            //2、设置通道
            short ret = 0;
            //1表示DC,0表示AC
            //A通道打开，DC，范围为20mv,偏移-105MV
            //ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 1, Imports.Range.Range_500MV, (float)-1.05);
            ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 1, Imports.Range.Range_50MV, (float)0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelA出错！");
                return false;
            }
            //B通道关闭
            Imports.SetChannel(_handle, Imports.Channel.ChannelB, 1, 1, Imports.Range.Range_5V, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelB出错！");
                return false;
            }
            //3、设置采样率
            int timeIntervalNanoseconds, max_samples;
            //获取最大采样点数
            Imports.GetTimebase(_handle, timebase, (int)samples, out timeIntervalNanoseconds, 0, out max_samples, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("GetTimebase出错！");
                return false;
            }
            //4、设置触发信号
            short triggerVoltage = mv_to_adc((short)1000, (short)Imports.Range.Range_5V); // ChannelInfo stores ADC counts
            ret = Imports.SetTrigger(_handle, 1, Imports.Channel.ChannelB, triggerVoltage, Imports.ThresholdDirection.Rising, (uint)delay, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetSimpleTrigger出错！");
                return false;
            }
            return true;
        }
        //准备示波器：打开，A通道采集，20MV范围，-105mv偏移，外部触发，采样率1GHz
        public bool PrepareScope_Rapid(int samples, uint delay,ushort nSegments)
        {
            //1、打开示波器
            if (deviceOpen() != 0)
            {
                System.Console.WriteLine("打开设备失败!\n");
                return false;
            }
            //GetDeviceInfo();
            //2、设置通道
            short ret = 0;
            //1表示DC,0表示AC
            //A通道打开，DC，范围为20mv,偏移-105MV
            //ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 1, Imports.Range.Range_500MV, (float)-1.05);
            ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 0, Imports.Range.Range_1V, (float)0.2);
            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelA出错！");
                return false;
            }
            //B通道关闭
            ret =Imports.SetChannel(_handle, Imports.Channel.ChannelB, 1, 0, Imports.Range.Range_5V, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelB出错！");
                return false;
            }

            //3* Rapid mode steup
            int nMaxSamples=0;
            ret =Imports.MemorySegments(_handle,nSegments,out nMaxSamples);

            if (ret != 0)
            {
                System.Console.WriteLine("MemorySegments failed!");
                return false;
            }
            ret = Imports.SetNoOfRapidCaptures(_handle, nSegments);
            if (ret != 0)
            {
                System.Console.WriteLine("MemorySegments failed!");
                return false;
            }
            System.Console.WriteLine("MaxSamples={0}",nMaxSamples);
            //3、设置采样率
            int timeIntervalNanoseconds, max_samples;
            //获取最大采样点数
            Imports.GetTimebase(_handle, 3, (int)samples, out timeIntervalNanoseconds, 0, out max_samples, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("GetTimebase出错！");
                return false;
            }
            //4、设置触发信号
            short triggerVoltage = mv_to_adc((short)1000, (short)Imports.Range.Range_5V); // ChannelInfo stores ADC counts
            ret = Imports.SetTrigger(_handle, 1, Imports.Channel.ChannelB, triggerVoltage, Imports.ThresholdDirection.Rising, (uint)delay, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetSimpleTrigger出错！");
                return false;
            }
            return true;
        }

        public bool PrepareScope_Rapid(int samples, uint delay, uint timebase,ushort nSegments)
        {
            //1、打开示波器
            if (deviceOpen() != 0)
            {
                System.Console.WriteLine("打开设备失败!\n");
                return false;
            }
            //GetDeviceInfo();
            //2、设置通道
            short ret = 0;
            //1表示DC,0表示AC
            //A通道打开，DC，范围为20mv,偏移-105MV
            //ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 1, Imports.Range.Range_500MV, (float)-1.05);
            ret = Imports.SetChannel(_handle, Imports.Channel.ChannelA, 1, 1, Imports.Range.Range_500MV, (float)-0.15);

            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelA出错！");
                return false;
            }
            //B通道关闭
            ret = Imports.SetChannel(_handle, Imports.Channel.ChannelB, 1, 1, Imports.Range.Range_5V, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetChannelB出错！");
                return false;
            }

            //3* Rapid mode steup
            int nMaxSamples = 0;
            ret = Imports.MemorySegments(_handle, nSegments, out nMaxSamples);

            if (ret != 0)
            {
                System.Console.WriteLine("MemorySegments failed!");
                return false;
            }
            ret = Imports.SetNoOfRapidCaptures(_handle, nSegments);
            if (ret != 0)
            {
                System.Console.WriteLine("MemorySegments failed!");
                return false;
            }
            System.Console.WriteLine("MaxSamples={0}", nMaxSamples);
            //3、设置采样率
            int timeIntervalNanoseconds, max_samples;
            //获取最大采样点数
            Imports.GetTimebase(_handle, timebase, (int)samples, out timeIntervalNanoseconds, 0, out max_samples, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("GetTimebase出错！");
                return false;
            }
            //4、设置触发信号
            short triggerVoltage = mv_to_adc((short)2000, (short)Imports.Range.Range_5V); // ChannelInfo stores ADC counts

            ret = Imports.SetTrigger(_handle, 1, Imports.Channel.ChannelB, triggerVoltage, Imports.ThresholdDirection.Rising, (uint)delay, 0);
            if (ret != 0)
            {
                System.Console.WriteLine("SetSimpleTrigger出错！");
                return false;
            }
            return true;
        }
        //准备密码设备：打开，置入密钥
        public bool PrepareCryptoModule(string portname)
        {
            try
            {
                cipher_hw = new GeneralDevice(portname);//指定当前硬件设备接口实例为SASEBO_G
                cipher_sw = (CipherTool.IBlockCipher)new CipherTool.AES();//指定软件实现接口实例为AES
                //准备硬件设备
            }
            catch (Exception e)
            {
                System.Console.WriteLine("{0}", e.Message);
                return false;
            }
            return true;
        }
        //中心化
        public void centeredTrace(short[] t)
        {
            double mean = 0;
            int len = t.Length;
            for (int i = 0; i < len; i++)
            {
                mean += t[i];
            }
            mean = mean / len;
            for (int i = 0; i < len; i++)
            {
                t[i] = (short)(t[i] - mean);
            }
        }
        //采集一条曲线
        //StartPercent表示触发信号之前的采集所占比例
        public bool GetOneTrace(int samples,int mlen,double StartPercent,byte[] plain,byte[] cipher,uint timebase,bool fixedkey,byte[] key)
        {
            short[] temp=new short[samples+100];
            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }

            //明密文

            byte[] text_ans = new byte[mlen];
            byte[] text_in = new byte[mlen*2];
            byte[] text_out = new byte[mlen];

            //生成随机明文
            for (int i = 0; i < mlen*2; i++)
            {
                plain[i] = (byte)ra.Next(256);
                //text_in[i] = plain[i];
            }
            //Change key to a constant
            
            if (fixedkey)
            {
                for (int i = 0; i < mlen; i++)
                    plain[i] = key[i];
            }
            for (int i = 0; i < mlen * 2; i++)
            {
                //plain[i] = (byte)ra.Next(256);
                text_in[i] = plain[i];
            }

            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * StartPercent), (int)(samples * (1 - StartPercent)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");

           
          


            try
            {

                //执行硬件加密
                text_out=cipher_hw.Encrypt_RandomKey(text_in);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }
            for (int i = 0; i < mlen; i++)
                cipher[i] = text_out[i];
            //System.Console.Write("硬件加密结果:\t");
            //for (int i = 0; i < mlen; i++)
            //    System.Console.Write("{0:x2}", cipher[i]);
            //System.Console.WriteLine("");
            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
                return false;
            Array.Copy(temp, measurements, samples);
            // 比较结果

            //bool error = false;
            //for (int i = 0; i < mlen; i++)
            //    if (text_out[i] != text_ans[i])
            //        error = true;
            //if (error)
            //{
            //    System.Console.WriteLine("加密结果不同！");
            //    return false;
            //}
            //else
            //    System.Console.WriteLine("加密结果相同");
            return true;
        }
        public bool GetOneTrace_Ttest_SITI4(int samples, int mlen, byte[] plain, byte[] cipher, bool Ttest, bool RNGOn)
        {

            //明密文
            byte[] text_ans = new byte[mlen];
            byte[] text_in = new byte[mlen];
            byte[] text_out = new byte[mlen];

            //Random plaintext
            ra.NextBytes(plain);
            //Ensure each byte is whether 0 or 0xff
            for (int i = 0; i < mlen; i++)
            {
                if((plain[i] & 0x01) == 0)
                    plain[i] = 0;
                else
                    plain[i] = 0xff;
            }
            //RNG On or OFF
            if (!RNGOn)
            {
                 for (int i = 0; i < mlen-4; i++)
                     plain[i] = 0;
            }
            //Change the last share according to Ttest flag
            if (Ttest)
            {
                byte[] tempb=new byte[4];
                for(int j=0;j<4;j++)
                    tempb[j]=0;
                for (int i = 0; i < mlen - 4; i++)
                    tempb[i % 4] = (byte)(tempb[i % 4] ^ plain[i]);
                plain[mlen - 4] = tempb[0];
                plain[mlen - 3] = tempb[1];
                plain[mlen - 2] = tempb[2];
                plain[mlen - 1] = tempb[3];
            }

            for (int i = 0; i < mlen; i++)
            {
                text_in[i] = plain[i];
            }

            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 0), (int)(samples * (1 -0)), 1, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.Encrypt(text_in);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }
            for (int i = 0; i < mlen; i++)
                cipher[i] = text_out[i];
            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
            {
                System.Console.WriteLine("Overflow!");
                return false;
            }
            Array.Copy(temp, measurements, samples);
            // Verify Sbox
            //if (Verify_SITI4(plain, cipher))
                return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
           // }
           
        }

        public bool GetOneTrace_Ttest_SITI8(int samples, int mlen, byte[] plain, byte[] cipher, bool Ttest, bool RNGOn)
        {

            //明密文
            byte[] text_ans = new byte[mlen];
            byte[] text_in = new byte[mlen];
            byte[] text_out = new byte[mlen];

            //Random plaintext
            ra.NextBytes(plain);
            //Ensure each byte is whether 0 or 0x0f or 0xf0 or 0xff
            for (int i = 0; i < mlen; i++)
            {
                byte v = 0;
                if ((plain[i] & 0x80) == 0)
                    v = 0;
                else
                    v = 0xf0;
                if ((plain[i] & 0x01) > 0)
                    v = (byte)(v^0xf);
                plain[i] = v;
            }
            //RNG On or OFF
            if (!RNGOn)
            {
                for (int i = 0; i < mlen - 4; i++)
                    plain[i] = 0;
            }
            //Change the last share according to Ttest flag
            if (Ttest)
            {
                byte[] tempb = new byte[4];
                for (int j = 0; j < 4; j++)
                    tempb[j] = 0;
                for (int i = 0; i < mlen - 4; i++)
                    tempb[i % 4] = (byte)(tempb[i % 4] ^ plain[i]);
                plain[mlen - 4] = tempb[0];
                plain[mlen - 3] = tempb[1];
                plain[mlen - 2] = tempb[2];
                plain[mlen - 1] = tempb[3];
            }

            for (int i = 0; i < mlen; i++)
            {
                text_in[i] = plain[i];
            }

            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 0), (int)(samples * (1 - 0)), 1, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.Encrypt(text_in);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }
            for (int i = 0; i < mlen; i++)
                cipher[i] = text_out[i];
            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
            {
                System.Console.WriteLine("Overflow!");
                Array.Sort(temp);
                return false;
            }
            Array.Copy(temp, measurements, samples);
            // Verify Sbox
            //if (Verify_SITI8(plain, cipher))
                return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
            //}
        }

        public bool GetOneTrace_Ttest_MaskedAES(int samples, int mlen, byte[] plain, byte[] cipher, bool Ttest, bool RNGOn,uint timebase)
        {

            //明密文
            //byte[] text_ans = new byte[mlen];
            //byte[] text_in = new byte[mlen];
            //byte[] text_out = new byte[mlen];

            ////Random plaintext
            //ra.NextBytes(plain);
            //byte[] key = { 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
            ////Change the last share according to Ttest flag
            //if (Ttest)
            //{ 
            //    for (int i = 0; i < mlen; i=i+1)
            //        plain[i] = (byte)(plain[i % 2] ^ key[i%2] ^ key[i]);
            //    //plain[mlen - 1] = (byte)(0x52 ^ key[mlen - 1]);
            //    //plain[mlen - 2] = (byte)(0x52 ^ key[mlen - 2]);
            //}

            byte[] text_ans = new byte[mlen+6];
            byte[] text_in = new byte[mlen+6];
            byte[] text_out = new byte[mlen];

            //Random plaintext
            ra.NextBytes(plain);
            ra.NextBytes(text_in);
            byte[] key = { 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
            //Change the last share according to Ttest flag
            if (Ttest)
            {
                for (int i = 0; i < mlen; i = i + 1)
                    plain[i] = (byte)(plain[0] ^ key[0] ^ key[i]);
                //plain[mlen - 1] = (byte)(0x52 ^ key[mlen - 1]);
                //plain[mlen - 2] = (byte)(0x52 ^ key[mlen - 2]);
            }

            for (int i = 0; i < mlen; i++)
            {
                text_in[i] = plain[i];
            }

            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.Encrypt_Masked(text_in,mlen);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }
            for (int i = 0; i < mlen; i++)
                cipher[i] = text_out[i];
            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
            {
                System.Console.WriteLine("Overflow!");
                Array.Sort(temp);
                return false;
            }
            Array.Copy(temp, measurements, samples);
            // Verify Sbox
            //if (Verify_SITI8(plain, cipher))
            return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
            //}
        }
        public bool GetOneTrace_TestCollision(int samples, int mlen, byte[] plain, byte[] cipher, bool Ttest,uint timebase)
        {

            byte[] text_ans = new byte[mlen + 6];
            byte[] text_in = new byte[mlen + 6];
            byte[] text_out = new byte[mlen];

            //Random plaintext
            ra.NextBytes(plain);
            ra.NextBytes(text_in);
            byte[] key = { 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
            //Change the last share according to Ttest flag
            if (Ttest)
            {
                for (int i = 0; i < mlen; i = i + 4)
                    plain[i] = (byte)(plain[0] ^ key[0] ^ key[i]);
            }

            for (int i = 0; i < mlen; i++)
            {
                text_in[i] = plain[i];
            }

            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.Encrypt_Masked(text_in, mlen);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }
            for (int i = 0; i < mlen; i++)
                cipher[i] = text_out[i];
            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
            {
                System.Console.WriteLine("Overflow!");
                Array.Sort(temp);
                return false;
            }
            Array.Copy(temp, measurements, samples);
            // Verify Sbox
            //if (Verify_SITI8(plain, cipher))
            return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
            //}
        }
        public bool GetMultipleTrace_RapidTtest_MaskedAES(int samples, int mlen,bool[] Ttest, bool RNGOn,int nSeg)
        {

            //明密文
            byte[][] text_in = new byte[nSeg][];
            byte[][] text_out = new byte[nSeg][];
            byte[] key = { 0x2B, 0x7E, 0x15, 0x16, 0x28, 0xAE, 0xD2, 0xA6,
                      0xAB, 0xF7, 0x15, 0x88, 0x09, 0xCF, 0x4F, 0x3C };
            //Random plaintext
            for(int i=0;i<nSeg;i++)
            {
                text_in[i]=new byte[mlen];
                text_out[i]=new byte[mlen];
                ra.NextBytes(text_in[i]);
                //RNG On or OFF
                if (!RNGOn)
                {
                    text_in[i][mlen - 6] = 0;
                    text_in[i][mlen - 5] = 0;
                    text_in[i][mlen - 4] = 0;
                    text_in[i][mlen - 3] = 0;
                    text_in[i][mlen - 2] = 0;
                    text_in[i][mlen - 1] = 0;
                }
               
                if (Ttest[i])
                {
                    for (int j = 0; j < mlen ; j++)
                        if ((j & 0x04) == 0)
                            text_in[i][j] = (byte)(0x00^key[j] );
                        else
                            text_in[i][j] = (byte)(0x00^key[j] );
                   
                }
            }
            short ret = 0;
            for (int i = 0; i < nSeg; i++)
            {
                ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, Mulmeasurements[i], samples, (ushort)i, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer Failed!");
                    return false;
                }
            }

            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), 3, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock Error!");
            for (int i = 0; i < nSeg; i++)
            {
                try
                {

                    //执行硬件加密
                    text_out[i] = cipher_hw.Encrypt_Masked(text_in[i], mlen);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("   failed: {0}", e.Message);
                    return false;
                }
            }
            

            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(5000);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);



            //7、测量完成，取回数据
            short[] overflow = new short[nSeg];
            uint getSamples = (uint)samples;

            ret = Imports.GetValuesRapid(_handle, ref getSamples, (ushort)0, (ushort)(nSeg-1),1,Imports.DownSamplingMode.None,  overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            for (int i = 0; i < nSeg; i++)
            {
                if (overflow[i] != 0)
                {
                    System.Console.WriteLine("Overflow!");
                    return false;
                }
            }
           
            return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
            //}
        }

        uint SetBit(uint v, int ind)
        {
            return (v | (uint)(0x01 << ind));
        }
        uint GetBit(uint v, int ind)
        {
            return ((v & (uint)(0x01 << ind))==0)?(uint)0:(uint)1;
        }
        uint ClearBit(uint v, int ind)
        {
            uint mask=(uint)(0x01 << ind);
            mask=~mask;
            return (v &mask);
        }
        //Create 2 shared 32-bit operands, all $s$ shares of the same bit will be put together
        public void ProduceShares_shareindex(byte[] input, int shares, bool Ttest)
        {
            int blocks = 32 / shares;
            uint temp1 = 0;
            uint temp2 = 0;
            uint op1 = ((uint)input[0] << 24) | ((uint)input[1] << 16) | ((uint)input[2] << 8) | (uint)(input[3]);
            uint op2 = ((uint)input[4] << 24) | ((uint)input[5] << 16) | ((uint)input[6] << 8) | (uint)(input[7]);
            for (int i = 0; i < 32; i++)
            {
                if (i >= 32)//Using the same shares
                {
                   if (GetBit(op1, i % shares) == 1)
                        op1 = SetBit(op1, i);
                    else
                        op1 = ClearBit(op1, i);
                    if (GetBit(op2, i % shares) == 1)
                        op2 = SetBit(op2, i);
                    else
                        op2 = ClearBit(op2, i);
                    //Set all other bits to 0
                    //if (i >=4)
                    //{
                    //    op1 = ClearBit(op1, i);
                    //    op2 = ClearBit(op2, i);
                   // }
                    continue;
                }
                if (Ttest)
                {
                    if (i % shares == (shares - 1))
                    {
                        if (temp1 == 1)
                            op1 = SetBit(op1, i);
                        else
                            op1 = ClearBit(op1, i);
                        if (temp2 == 1)
                            op2 = SetBit(op2, i);
                        else
                            op2 = ClearBit(op2, i);
                        continue;
                    }
                    if (i % shares == 0)
                    {
                        temp1 = GetBit(op1, i);
                        temp2 = GetBit(op2, i);
                        continue;
                    }
                    temp1 ^= GetBit(op1, i);
                    temp2 ^= GetBit(op2, i);
                }
                    /*
                else
                {
                    {
                        if (i % shares == (shares - 1))
                        {
                            if (temp1 == 1)
                                op1 = ClearBit(op1, i);
                            else
                                op1 = SetBit(op1, i);
                            if (temp2 == 1)
                                op2 = ClearBit(op2, i);
                            else
                                op2 = SetBit(op2, i);
                            continue;
                        }
                        if (i % shares == 0)
                        {
                            temp1 = GetBit(op1, i);
                            temp2 = GetBit(op2, i);
                            continue;
                        }
                        temp1 ^= GetBit(op1, i);
                        temp2 ^= GetBit(op2, i);
                    }
                }
                     * */
               
            }
            //if (!Ttest)
            //    return;
            //Ignore Op1
            input[0]=(byte)((op1>>24)&0xff);
            input[1]=(byte)((op1>>16)&0xff);
            input[2]=(byte)((op1>>8)&0xff);
            input[3] = (byte)((op1) & 0xff);
            input[4] = (byte)((op2 >> 24) & 0xff);
            input[5] = (byte)((op2 >> 16) & 0xff);
            input[6] = (byte)((op2 >> 8) & 0xff);
            input[7] = (byte)((op2) & 0xff);
        }
        public bool GetOneTrace_Ttest_BitInteraction(int samples,uint timebase, int instr,int shares, bool Ttest)
        {

            byte[] text_in = new byte[9];
            byte[] text_out = new byte[4];

            //Random plaintext
            ra.NextBytes(text_in);
            //Change the last share according to Ttest flag
            ProduceShares_shareindex(text_in, shares,Ttest);
            text_in[8] = (byte)instr;

            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.SendAReadBack(text_in, 4);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }
            
            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
            {
                System.Console.WriteLine("Overflow!");
                Array.Sort(temp);
                return false;
            }
            Array.Copy(temp, measurements, samples);
            // Verify Sbox
            //if (Verify_SITI8(plain, cipher))
            return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
            //}
        }
        public bool GetOneTrace_Ttest_BitInteraction_RapidRepeat(byte[] plain,int samples, uint timebase, int instr, int shares, bool Ttest,int repeat)
        {

            byte[] text_in = new byte[9];
            byte[] text_out = new byte[4];
            short ret = 0;
            //Random plaintext
            ra.NextBytes(text_in);
            //text_in[4] = plain[4];
            //text_in[5] = plain[5];
            //text_in[6] = plain[6];
            //text_in[7] = plain[7];
            //Change the last share according to Ttest flag
            ProduceShares_shareindex(text_in, shares, Ttest);

            plain[4] = text_in[4];
            plain[5] = text_in[5];
            plain[6] = text_in[6];
            plain[7] = text_in[7];
            text_in[8] = (byte)instr;
           // text_in[9] = (byte)((repeat/10)&0xff);
            for (int i = 0; i < repeat; i++)
            {
                ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, Mulmeasurements[i], samples, (ushort)i, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer Failed!");
                    return false;
                }
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.SendAReadBack(text_in, 4);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }

            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(100);//延时
                c++;
                if (c == 10)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short[] overflow = new short[repeat];
            uint getSamples = (uint)samples;

            ret = Imports.GetValuesRapid(_handle, ref getSamples, (ushort)0, (ushort)(repeat - 1), 1, Imports.DownSamplingMode.None, overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            for (int i = 0; i < repeat; i++)
            {
                if (overflow[i] != 0)
                {
                    System.Console.WriteLine("Overflow!");
                    Imports.Stop(_handle);
                    return false;
                }
            }
            Imports.Stop(_handle);
            return true;
           
        }
        public bool GetOneTrace_Ttest_Sbox(int samples, uint timebase, bool Ttest)
        {

            byte[] text_in = new byte[16];
            byte[] text_out = new byte[16];
            byte[] key = { 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
            //Random plaintext
            ra.NextBytes(text_in);
            //Change the last share according to Ttest flag
            if (Ttest)
            {
                for (int i = 0; i < 16; i++)
                    text_in[i] = key[i];
            }

            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.Encrypt(text_in);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }

            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
            {
                System.Console.WriteLine("Overflow!");
                Array.Sort(temp);
                return false;
            }
            Array.Copy(temp, measurements, samples);
            // Verify Sbox
            //if (Verify_SITI8(plain, cipher))
            return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
            //}
        }
        public bool GetOneTrace_TAttack_Sbox(int samples, uint timebase,byte[] plain,byte[] cipher)
        {

            byte[] text_in = new byte[16];
            byte[] text_out = new byte[16];
            //Change the last share according to Ttest flag

                for (int i = 0; i < 16; i++)
                    text_in[i] = plain[i];

            short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
            if (ret != 0)
            {
                System.Console.WriteLine("SetDataBuffer出错！");
                return false;
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.Encrypt(text_in);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }

            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 5)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short overflow = 0;
            uint getSamples = (uint)samples;
            ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            if (overflow != 0)
            {
                System.Console.WriteLine("Overflow!");
                Array.Sort(temp);
                return false;
            }
            Array.Copy(temp, measurements, samples);
            // Verify Sbox
            //if (Verify_SITI8(plain, cipher))
            for (int i = 0; i < 16; i++)
                cipher[i] = text_out[i];
            return true;
            //else
            //{
            //    System.Console.WriteLine("Incorrect Sbox!");
            //    return false;
            //}
        }
        public bool GetOneTrace_TAttack_Sbox_RapidRepeat(int samples, uint timebase, byte[] plain, byte[] cipher, int repeat, bool fresh, byte internal_repeat, byte block_repeat)
        {

            byte[] text_in = new byte[18];
            byte[] text_out = new byte[16];
            //Change the last share according to Ttest flag
            short ret = 0;
            for (int i = 0; i < 16; i++)
                text_in[i] = plain[i];
            //M3-4shares-
            //if (fresh)
           //     text_in[16] = 0xff;
           // else
            //    text_in[16] = 0;
            //text_in[17] = internal_repeat;
            text_in[16] = block_repeat;
            text_in[17] = internal_repeat;
            //M3---
            //M0---
            //if (fresh)
            //    text_in[16] = 0xff;
            //else
            //    text_in[16] = 0;
            //text_in[17] = block_repeat;
            //text_in[18] = internal_repeat;
            //M0-4shares-
            //M3-2shares-
            
            //text_in[16] = block_repeat;
            //text_in[17] = internal_repeat;
           
            //M3-2shares-

            for (int i = 0; i < repeat; i++)
            {
                ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, Mulmeasurements[i], samples, (ushort)i, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer Failed!");
                    return false;
                }
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * (1-0)), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");
            try
            {

                //执行硬件加密
                text_out = cipher_hw.SendAReadBack(text_in,16);

            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }

            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 50)
                    return false;
            }
            while (red == 0);

            //7、测量完成，取回数据
            short[] overflow = new short[repeat];
            uint getSamples = (uint)samples;

            ret = Imports.GetValuesRapid(_handle, ref getSamples, (ushort)0, (ushort)(repeat - 1), 1, Imports.DownSamplingMode.None, overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }

            for (int i = 0; i < repeat; i++)
            {
                if (overflow[i] != 0 )
                {
                  //  for(int j=230;j<240;j++)
                 //   if (Mulmeasurements[i][j] >= 32512 && Mulmeasurements[i][j] <= -32512)
                 //   {
                        System.Console.WriteLine("Overflow!");
                        return false;
                 //   }
                }
            }

            for (int i = 0; i < 16; i++)
                cipher[i] = text_out[i];
            return true;

        }

        public bool GetOneTrace_TAttack_Sbox_RapidRepeat_Thread(int samples, uint timebase, byte[] plain, byte[] cipher, int repeat, bool fresh, byte internal_repeat, byte block_repeat, Task pth,double[][] values)
        {

            byte[] text_in = new byte[18];
            byte[] text_out = new byte[16];
            //Change the last share according to Ttest flag
            short ret = 0;
            for (int i = 0; i < 16; i++)
                text_in[i] = plain[i];
            //M3-4shares-
            //if (fresh)
            //     text_in[16] = 0xff;
            // else
            //    text_in[16] = 0;
            //text_in[17] = internal_repeat;
            text_in[16] = block_repeat;
            text_in[17] = internal_repeat;
           
            //M3---
            //M0---
            //if (fresh)
            //    text_in[16] = 0xff;
            //else
            //    text_in[16] = 0;
            //text_in[17] = block_repeat;
            //text_in[18] = internal_repeat;
            //M0-4shares-
            //M3-2shares-

            //text_in[16] = block_repeat;
            //text_in[17] = internal_repeat;

            //M3-2shares-

            for (int i = 0; i < repeat; i++)
            {
                ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, Mulmeasurements[i], samples, (ushort)i, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer Failed!");
                    return false;
                }
            }
            ////5、开始测量
            int time_interval_ms = 0;
            ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
            if (ret != 0)
                System.Console.WriteLine("RunBlock出错！");

           
            try
            {

                //执行硬件加密
                text_out = cipher_hw.SendAReadBack(text_in, 16);

            }
            catch (Exception e)
            {
                System.Console.WriteLine("   failed: {0}", e.Message);
                return false;
            }

            short red = 0;
            int c = 0;
            do
            {
                Imports.IsReady(_handle, out red);
                if (c > 0)
                    System.Threading.Thread.Sleep(50);//延时
                c++;
                if (c == 50)
                    return false;
            }
            while (red == 0);

            //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
           // stopwatch.Start();
            //Check the thread
            if (pth != null)
            {
                //wait until pth finish
                pth.Wait();
            }
            //stopwatch.Stop();
            //Console.WriteLine("Ttest Waiting Time=" + stopwatch.ElapsedTicks);
            //7、测量完成，取回数据
            short[] overflow = new short[repeat];
            uint getSamples = (uint)samples;

            ret = Imports.GetValuesRapid(_handle, ref getSamples, (ushort)0, (ushort)(repeat - 1), 1, Imports.DownSamplingMode.None, overflow);
            if (ret != 0)
            {
                System.Console.WriteLine("GetValues出错！");
                return false;
            }
            for (int i = 0; i < repeat; i++)
            {
                if (overflow[i] != 0)
                {
                    System.Console.WriteLine("Overflow!");
                    return false;
                }
            }
           

            for (int i = 0; i < 16; i++)
                cipher[i] = text_out[i];
            for (int i = 0; i < repeat; i++)
                for (int j = 0; j < samples; j++)
                    values[i][j] = Mulmeasurements[i][j];
            return true;

        }

        public bool GetOneTrace_PowerModel(int samples, int mlen, byte[] plain, byte[] cipher,int ind,int repeat)
        {

            //明密文
            byte[] text_ans = new byte[mlen];
            byte[] text_in = new byte[mlen];
            byte[] text_out = new byte[mlen];

            //Random plaintext
            ra.NextBytes(plain);

            plain[28] = (byte)(ind / 5000+19);
            
            for (int i = 0; i < mlen; i++)
            {
                text_in[i] = plain[i];
            }
            for (int i = 0; i < measurements.Length; i++)
                doubletemp[i] = 0;
            for (int time = 0; time < repeat; time++)
            {
                short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer出错！");
                    return false;
                }
                ////5、开始测量
                int time_interval_ms = 0;
                ret = Imports.RunBlock(_handle, (int)(samples * 0), (int)(samples * (1 - 0)), 1, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
                if (ret != 0)
                    System.Console.WriteLine("RunBlock出错！");
                try
                {

                    //执行硬件加密
                    text_out = cipher_hw.Encrypt(text_in);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("   failed: {0}", e.Message);
                    return false;
                }
                for (int i = 0; i < mlen; i++)
                    cipher[i] = text_out[i];
                short red = 0;
                int c = 0;
                do
                {
                    Imports.IsReady(_handle, out red);
                    if (c > 0)
                        System.Threading.Thread.Sleep(50);//延时
                    c++;
                    if (c == 5)
                        return false;
                }
                while (red == 0);

                //7、测量完成，取回数据
                short overflow = 0;
                uint getSamples = (uint)samples;
                ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
                if (ret != 0)
                {
                    System.Console.WriteLine("GetValues出错！");
                    return false;
                }
                //if (overflow != 0)
                //{
                //    System.Console.WriteLine("Overflow!");
                //    return false;
                //}
                for (int i = 0; i < samples; i++)
                    doubletemp[i] += temp[i];
            }
            for (int i = 0; i < samples; i++)
                measurements[i] = (short)(doubletemp[i] / repeat);
            return true;
            
        }
        public bool GetOneTrace_BitSlicedAND2(byte[] plain, byte[] cipher, int samples,uint timebase)
        {


            //Random plaintext
            ra.NextBytes(plain);


                short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer出错！");
                    return false;
                }
                ////5、开始测量
                int time_interval_ms = 0;
                ret = Imports.RunBlock(_handle, (int)(samples * 1), (int)(samples * (1 - 1)), timebase, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
                if (ret != 0)
                    System.Console.WriteLine("RunBlock出错！");
                try
                {

                    //执行硬件加密
                    cipher = cipher_hw.Encrypt(plain);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("   failed: {0}", e.Message);
                    return false;
                }
                short red = 0;
                int c = 0;
                do
                {
                    Imports.IsReady(_handle, out red);
                    if (c > 0)
                        System.Threading.Thread.Sleep(50);//延时
                    c++;
                    if (c == 5)
                        return false;
                }
                while (red == 0);

                //7、测量完成，取回数据
                short overflow = 0;
                uint getSamples = (uint)samples;
                ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
                if (ret != 0)
                {
                    System.Console.WriteLine("GetValues出错！");
                    return false;
                }
               
            return true;

        }
        public bool GetOneTrace_PowerModel_Full(int samples, int mlen, byte[] plain, byte[] cipher, int instr, int repeat)
        {

            //明密文


            //Random plaintext
            ra.NextBytes(plain);

            plain[28] = (byte)instr;

            for (int i = 0; i < mlen; i++)
            {
                text_in[i] = plain[i];
            }
            for (int i = 0; i < measurements.Length; i++)
                doubletemp[i] = 0;
            for (int time = 0; time < repeat; time++)
            {
                short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer出错！");
                    return false;
                }
                ////5、开始测量
                int time_interval_ms = 0;
                ret = Imports.RunBlock(_handle, (int)(samples * 0), (int)(samples * (1 - 0)), 1, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
                if (ret != 0)
                    System.Console.WriteLine("RunBlock出错！");
                try
                {

                    //执行硬件加密
                    text_out = cipher_hw.Encrypt(text_in);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("   failed: {0}", e.Message);
                    return false;
                }
                for (int i = 0; i < mlen; i++)
                    cipher[i] = text_out[i];
                short red = 0;
                int c = 0;
                do
                {
                    Imports.IsReady(_handle, out red);
                    if (c > 0)
                        System.Threading.Thread.Sleep(50);//延时
                    c++;
                    if (c == 5)
                        return false;
                }
                while (red == 0);

                //7、测量完成，取回数据
                short overflow = 0;
                uint getSamples = (uint)samples;
                ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
                if (ret != 0)
                {
                    System.Console.WriteLine("GetValues出错！");
                    return false;
                }
                if (overflow != 0)
                {
                    System.Console.WriteLine("Overflow!");
                    return false;
                }
                for (int i = 0; i < samples; i++)
                    doubletemp[i] += temp[i];
            }
            for (int i = 0; i < samples; i++)
                measurements[i] = (short)(doubletemp[i] / repeat);
            return true;

        }
        //make result=0
        public void ZeroResult(byte[] plain, int instr)
        {
            int a = (plain[11] << 24) ^ (plain[10] << 16) ^ (plain[9] << 8) ^ (plain[8]);
            switch(instr)
            {
                case 1:
                    {
                        int b = -a;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 2://r3+0=r3; r3->0===a->0
                    {
                        plain[11] = (byte)0;
                        plain[10] = (byte)0;
                        plain[9] = (byte)0;
                        plain[8] = (byte)0;
                    };
                    break;
                case 3://r3&r4=0===b->0xfffffffff^a
                    {
                        int b = (~a);
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 4://r3-r4=0===b->a
                    {
                        int b = a;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 5://r3-0=r3===a=0
                    {
                        plain[11] = (byte)0;
                        plain[10] = (byte)0;
                        plain[9] = (byte)0;
                        plain[8] = (byte)0;
                    };
                    break;
                case 6://r3^r4=0===a=b
                    {
                        int b = a;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 7://result=[r4] ===b=0
                    {
                        int b = 0;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 8://result=[r4] ===b=0
                    {
                        int b = 0;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 9://result=[r4] ===b=0
                    {
                        int b = 0;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 10://result=r3<<(r4%32))
                    {
                        uint b = (uint)((plain[15] << 24) ^ (plain[14] << 16) ^ (plain[13] << 8) ^ (plain[12]));
                        if (b < 32)
                        {
                            int bt = (int)(32 - b);
                            uint mask = ~((((uint)0x1 << bt)) - 1);
                            uint at = ((uint)a & mask);
                            plain[11] = (byte)((at >> 24) & 0xff);
                            plain[10] = (byte)((at >> 16) & 0xff);
                            plain[9] = (byte)((at >> 8) & 0xff);
                            plain[8] = (byte)((at >> 0) & 0xff);
                        }
                    };
                    break;
                case 11://result=r3<<(1%32)
                    {
                        uint mask = ~(((uint)0x1 << 31) - 1);
                        uint at = ((uint)a & mask);
                        plain[11] = (byte)((at >> 24) & 0xff);
                        plain[10] = (byte)((at >> 16) & 0xff);
                        plain[9] = (byte)((at >> 8) & 0xff);
                        plain[8] = (byte)((at >> 0) & 0xff);
                    };
                    break;
                case 12://result=r3>>(r4%32))
                    {
                        uint b = (uint)((plain[15] << 24) ^ (plain[14] << 16) ^ (plain[13] << 8) ^ (plain[12]));
                        if (b < 32)
                        {
                            int bt = (int)(32 - b);
                            
                            uint mask = ~((((((uint)0x1 << bt)) - 1)) << (32 - bt));
                            uint at = ((uint)a & mask);
                            plain[11] = (byte)((at >> 24) & 0xff);
                            plain[10] = (byte)((at >> 16) & 0xff);
                            plain[9] = (byte)((at >> 8) & 0xff);
                            plain[8] = (byte)((at >> 0) & 0xff);
                        }
                    };
                    break;
                case 13://result=r3>>1)
                    {
                        
                        uint mask = ~((((((uint)0x1 << 31)) - 1)) << 1);
                        uint at = ((uint)a & mask);
                        plain[11] = (byte)((at >> 24) & 0xff);
                        plain[10] = (byte)((at >> 16) & 0xff);
                        plain[9] = (byte)((at >> 8) & 0xff);
                        plain[8] = (byte)((at >> 0) & 0xff);
                    };
                    break;
                case 14://result=r4 ===b=0
                    {
                        int b = 0;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 15://result=imm
                    {
                        
                    };
                    break;
                case 16://result=r3*r4 ===a=0 or b=0
                    {
                        if (ra.NextDouble() > 0.5)
                        {
                            int b = 0;
                            plain[15] = (byte)((b >> 24) & 0xff);
                            plain[14] = (byte)((b >> 16) & 0xff);
                            plain[13] = (byte)((b >> 8) & 0xff);
                            plain[12] = (byte)((b >> 0) & 0xff);
                        }
                        else
                        {
                            a = 0;
                            plain[11] = (byte)((a >> 24) & 0xff);
                            plain[10] = (byte)((a >> 16) & 0xff);
                            plain[9] = (byte)((a >> 8) & 0xff);
                            plain[8] = (byte)((a >> 0) & 0xff);
                        }
                    };
                    break;
                case 17://result=r3|r4=0xffffffff ===a=0 and b=0
                    {

                            int b = 0;
                            plain[15] = (byte)((b >> 24) & 0xff);
                            plain[14] = (byte)((b >> 16) & 0xff);
                            plain[13] = (byte)((b >> 8) & 0xff);
                            plain[12] = (byte)((b >> 0) & 0xff);

                            a = 0;
                            plain[11] = (byte)((a >> 24) & 0xff);
                            plain[10] = (byte)((a >> 16) & 0xff);
                            plain[9] = (byte)((a >> 8) & 0xff);
                            plain[8] = (byte)((a >> 0) & 0xff);
                    };
                    break;
                case 18://result=r3>>>r4===a=0
                    {
                        plain[11] = (byte)0;
                        plain[10] = (byte)0;
                        plain[9] = (byte)0;
                        plain[8] = (byte)0;
                    };
                    break;
                case 19://result=r3===a=0
                    {
                        plain[11] = (byte)0;
                        plain[10] = (byte)0;
                        plain[9] = (byte)0;
                        plain[8] = (byte)0;
                    };
                    break;
                case 20://result=r3===a=0
                    {
                        plain[11] = (byte)0;
                        plain[10] = (byte)0;
                        plain[9] = (byte)0;
                        plain[8] = (byte)0;
                    };
                    break;
                case 21://result=r3===a=0
                    {
                        plain[11] = (byte)0;
                        plain[10] = (byte)0;
                        plain[9] = (byte)0;
                        plain[8] = (byte)0;
                    };
                    break;
                case 22://r3-r4=0===b->a
                    {
                        int b = a;
                        plain[15] = (byte)((b >> 24) & 0xff);
                        plain[14] = (byte)((b >> 16) & 0xff);
                        plain[13] = (byte)((b >> 8) & 0xff);
                        plain[12] = (byte)((b >> 0) & 0xff);
                    };
                    break;
                case 23://r3-0=r3===a=0
                    {
                        plain[11] = (byte)0;
                        plain[10] = (byte)0;
                        plain[9] = (byte)0;
                        plain[8] = (byte)0;
                    };
                    break;
                default:
                    break;
                
            };
        }
        //make result=0
        public int GetResult(byte[] plain, int instr)
        {
            int a = (plain[11] << 24) ^ (plain[10] << 16) ^ (plain[9] << 8) ^ (plain[8]);
            int b = (plain[15] << 24) ^ (plain[14] << 16) ^ (plain[13] << 8) ^ (plain[12]);
            switch (instr)
            {
                case 1:
                    {
                        return a + b;
                    };
                case 2:
                    {
                        return a;
                    };
                case 3:
                    {
                        return a&b;
                    };
                case 4:
                    {
                        return a-b;
                    };
                case 5:
                    {
                        return a;
                    };
                case 6:
                    {
                        return a^b;
                    };
                case 7:
                    {
                        return b;
                    };
                case 8:
                    {
                        return b;
                    };
                case 9:
                    {
                        return b;
                    };
                case 10:
                    {
                        if (b >= 32)
                            return 0;
                        else
                            return a<<b;
                    };
                case 11:
                    {
                        return a << 1;
                    };
                case 12:
                    {
                        if (b >= 32)
                            return 0;
                        else
                            return (int)((uint)a >> b);
                    };
                case 13:
                    {
                        return a >> 1;
                    };
                case 14:
                    {
                        return b;
                    };
                case 15:
                    {
                        return 0;
                    };
                case 16:
                    {
                        return a*b;
                    };
                case 17:
                    {
                        return a|b;
                    };
                case 18:
                    {
                        return (a >> (b % 32)) | (a << (31-(b % 32)));
                    };
                case 19:
                    {
                        return a;
                    };
                case 20:
                    {
                        return a;
                    };
                case 21:
                    {
                        return a;
                    };
                case 22:
                    {
                        return a - b;
                    };
                case 23:
                    {
                        return a;
                    };
                default: return 0;
            };
        }
        public bool GetOneTrace_PowerModel_Ttest(int samples, int mlen, byte[] plain, byte[] cipher,int instr, int[] Ttarget, bool Tflag)
        {

            //明密文
            byte[] text_ans = new byte[mlen];
            byte[] text_in = new byte[mlen];
            byte[] text_out = new byte[mlen];

            //Random plaintext
            ra.NextBytes(plain);

            plain[28] = (byte)instr;
            //If Ttest, change the plaintext
            if (Tflag)
            {
                if (Ttarget.Length == 1)//Operand Test
                {
                    if (Ttarget[0] == -1)//result
                    {
                        ZeroResult(plain, instr);
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                            plain[4 * Ttarget[0] + i] = 0;
                    }
                    
                }
                else//Transition Test
                {
                    if (Ttarget[0] == -1)//result:little endian
                    {
                        int result=GetResult(plain, instr);
                        plain[4 * Ttarget[1]] = (byte)(result & 0xff);
                        plain[4 * Ttarget[1]+1] = (byte)((result>>8) & 0xff);
                        plain[4 * Ttarget[1] + 2] = (byte)((result >> 16) & 0xff);
                        plain[4 * Ttarget[1] + 3] = (byte)((result >> 24) & 0xff);
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                            plain[4 * Ttarget[1] + i] = plain[4 * Ttarget[0] + i];
                    }
                }
            }

            for (int i = 0; i < mlen; i++)
            {
                text_in[i] = plain[i];
            }

                short ret = Imports.SetDataBuffer(_handle, Imports.Channel.ChannelA, temp, samples, 0, Imports.RatioMode.None);
                if (ret != 0)
                {
                    System.Console.WriteLine("SetDataBuffer出错！");
                    return false;
                }
                ////5、开始测量
                int time_interval_ms = 0;
                ret = Imports.RunBlock(_handle, (int)(samples * 0), (int)(samples * (1 - 0)), 1, (short)0, out time_interval_ms, (ushort)0, null, IntPtr.Zero);
                if (ret != 0)
                    System.Console.WriteLine("RunBlock出错！");
                try
                {

                    //执行硬件加密
                    text_out = cipher_hw.Encrypt(text_in);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("   failed: {0}", e.Message);
                    return false;
                }
                for (int i = 0; i < mlen; i++)
                    cipher[i] = text_out[i];
                short red = 0;
                int c = 0;
                do
                {
                    Imports.IsReady(_handle, out red);
                    if (c > 0)
                        System.Threading.Thread.Sleep(50);//延时
                    c++;
                    if (c == 5)
                        return false;
                }
                while (red == 0);

                //7、测量完成，取回数据
                short overflow = 0;
                uint getSamples = (uint)samples;
                ret = Imports.GetValues(_handle, (uint)0, ref getSamples, (uint)1, Imports.DownSamplingMode.None, (ushort)0, out overflow);
                if (ret != 0)
                {
                    System.Console.WriteLine("GetValues出错！");
                    return false;
                }
                if (overflow != 0)
                {
                    System.Console.WriteLine("Overflow!");
                    return false;
                }
                Array.Copy(temp, measurements, samples);
            return true;

        }
        //Verify 4bit SITISbox
        public bool Verify_SITI4(byte[] plain, byte[] cipher)
        {
            byte[] inb = new byte[4];
            byte[] outb = new byte[4];
            for(int i=0;i<4;i++)
            {
                inb[i]=0;
                outb[i]=0;
            }
            for (int i = 0; i < plain.Length; i++)
            {
                inb[i % 4] ^= plain[i];
                outb[i % 4] ^=cipher[i];
            }
            int inv = 0;
            int outv = 0;
            for (int i = 0; i < 4; i++)
            {
                if (inb[i] != 0)
                    inv = inv + 1;
                if (i != 3)
                    inv = inv << 1;
                if (outb[i] != 0)
                    outv = outv + 1;
                if (i != 3)
                    outv = outv << 1;
            }
            int[] Sbox = { 0x0, 0x4, 0x8, 0xA, 0xF, 0xC, 0x6, 0x9, 0x1, 0xE, 0xB, 0xD, 0x7, 0x5, 0x3, 0x2 };
            if (Sbox[inv] != outv)
                return false;
            else
                return true;
        }

        //Verify 8bit SITISbox
        public bool Verify_SITI8(byte[] plain, byte[] cipher)
        {
            byte[] inb = new byte[4];
            byte[] outb = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                inb[i] = 0;
                outb[i] = 0;
            }
            for (int i = 0; i < plain.Length; i++)
            {
                inb[i % 4] ^= plain[i];
                outb[i % 4] ^= cipher[i];
            }
            int inv = 0;
            int outv = 0;
            for (int i = 0; i < 4; i++)
            {
                if ((inb[i] &0xf0)!=0)
                    inv = inv + 1;
                inv = inv << 1;
                if ((inb[i] & 0x0f) != 0)
                    inv = inv + 1;
                if (i != 3)
                    inv = inv << 1;
                if ((outb[i] & 0xf0) != 0)
                    outv = outv + 1;
                outv = outv << 1;
                if ((outb[i] & 0x0f) != 0)
                    outv = outv + 1;
                if (i != 3)
                    outv = outv << 1;
            }
            int[] Sbox = {0,109,241,143,61,128,180,49,80,130,63,46,81,15,28,193,160,196,37,18,93,103,164,101,129,30,224,29,56,229,151,5,25,243,218,3,186,145,7,181,158,127,199,119,50,118,163,225,152,147,148,92,126,23,194,10,112,67,203,166,94,172,124,161,139,165,214,42,24,237,192,87,154,107,35,6,136,8,43,205,36,123,210,44,231,89,105,220,159,14,97,117,32,137,252,255,12,189,39,157,22,185,134,253,115,215,177,90,240,95,20,64,116,227,223,213,242,54,230,100,47,233,146,228,250,113,190,178,156,206,65,66,182,99,135,162,48,41,204,239,140,104,198,60,74,102,176,201,188,221,142,69,33,144,209,174,31,98,86,219,72,150,246,171,141,167,88,183,34,248,236,40,13,247,187,245,45,106,77,254,235,11,1,19,82,234,122,16,249,114,125,138,108,110,52,149,208,197,111,73,238,75,179,76,175,59,168,79,78,57,195,155,169,132,120,17,96,85,170,133,21,2,251,9,55,202,121,71,62,244,216,226,83,217,38,58,153,232,200,51,222,84,91,184,26,131,70,53,211,173,68,212,191,4,207,27};
            if (Sbox[inv] != outv)
                return false;
            else
                return true;
        }
        //关闭示波器
        public void CloseScope()
        {
            //10、关闭示波器
            short ret = Imports.CloseUnit(_handle);
            if (ret == 0)
                System.Console.WriteLine("Stop出错！");
        }
        //关闭硬件设备
        public void CloseCryptoModule()
        {
            //关闭硬件设备
            cipher_hw.Close();
            
        }

        //打开输出文件
        public void OpenOutputFile(string name)
        {
            fs = new FileStream(name, FileMode.Create);
            wr = new BinaryWriter(fs);
        }

        //关闭输出文件
        public void CloseOutputFile()
        {
            wr.Close();
            fs.Close();
        }

        //写trs文件头
        public void WriteFileHeader(int TraceNum,int samples,byte SampleCoding,int mlen,float XScale,float YScale)
        {
            int CryptoDataLen = mlen*2;//曲线中密码数据的长度（0x44），即每条trace开端部分保存的密码相关数据（明密文等）的字节长度
            
            //读取文件头
            System.Console.WriteLine("写文件头");
            //写TraceNum
            byte mark = 0x41;
            byte len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(TraceNum);
            //写SampleNum
            mark = 0x42;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(samples);
            //写SampleCoding
            mark = 0x43;
            len = 1;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(SampleCoding);
            //写CryptoDataLen
            mark = 0x44;
            len = 2;
            wr.Write(mark);
            wr.Write(len);
            wr.Write((Int16)CryptoDataLen);
            //写XScale
            mark = 0x4b;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(XScale);
            //写YScale
            mark = 0x4c;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(YScale);
            //写结束标志
            mark = 0x5f;
            len = 0;
            wr.Write(mark);
            wr.Write(len);
        }
        //写trs文件头
        public void WriteFileHeader_RandomKey(int TraceNum, int samples, byte SampleCoding, int mlen, float XScale, float YScale)
        {
            int CryptoDataLen = mlen * 3;//曲线中密码数据的长度（0x44），即每条trace开端部分保存的密码相关数据（明密文等）的字节长度

            //读取文件头
            System.Console.WriteLine("写文件头");
            //写TraceNum
            byte mark = 0x41;
            byte len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(TraceNum);
            //写SampleNum
            mark = 0x42;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(samples);
            //写SampleCoding
            mark = 0x43;
            len = 1;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(SampleCoding);
            //写CryptoDataLen
            mark = 0x44;
            len = 2;
            wr.Write(mark);
            wr.Write(len);
            wr.Write((Int16)CryptoDataLen);
            //写XScale
            mark = 0x4b;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(XScale);
            //写YScale
            mark = 0x4c;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(YScale);
            //写结束标志
            mark = 0x5f;
            len = 0;
            wr.Write(mark);
            wr.Write(len);
        }
        //向trs文件写入measurment对应的曲线
        public void WriteOneTrace(int samples,byte[] plain,byte[] cipher)
        {
            try
            {
                //写明文
                wr.Write(plain);
                //写密文
                wr.Write(cipher);
                //写出数据
                for (int k = 0; k < samples; k++)
                {
                    wr.Write(measurements[k]);
                }
                wr.Flush();
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception {0}", e.Message);
            }
        }
        //构造函数
        public Measurement(int samples,int mlen)
        {
            ra = new Random();
            //申请空间
            measurements = new short[samples];
            temp = new short[samples + 100];
            doubletemp = new double[samples + 100];
            text_ans = new byte[mlen];
            text_in = new byte[mlen];
            text_out = new byte[mlen];
        }

        //采集函数
        public void MeasureTraces_Ttest_SITI4(string outfile, byte[] key, int samples, int TraceNum, string portname, uint delay)
        {
            byte SampleCoding = 0x02;
            int mlen = 12;
            float XScale = (float)2E-9;
            float YScale = (float)1;
            bool flag = true;
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples,delay);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            OpenOutputFile(outfile);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            double[] Ttrend1 = new double[TraceNum / 1000];
            double[] Ttrend2 = new double[TraceNum / 1000];
            double[] Ttrend3 = new double[TraceNum / 1000];
            FileStream fs1 = new FileStream("Ttrend-O1.txt", FileMode.Create);
            FileStream fs2 = new FileStream("Ttrend-O2.txt", FileMode.Create);
            FileStream fs3 = new FileStream("Ttrend-O3.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            StreamWriter sw2 = new StreamWriter(fs2);
            StreamWriter sw3 = new StreamWriter(fs3);
            double[] temp = new double[measurements.Length];
            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Trace {0}:", i);
                bool Tv = ra.NextDouble() > 0.5;
                flag = GetOneTrace_Ttest_SITI4(samples, mlen, plain, cipher, Tv, true);
                if (flag)
                {
                    //centeredTrace(measurements);
                    if (i < 10000)
                        WriteOneTrace(samples, plain, cipher);
                   
                    for (int m = 0; m < temp.Length; m++)
                        temp[m] = (double)measurements[m];
                    Tvla.UpdateTrace(temp, Tv);
                    if (i % 1000 == 0)
                    {
                        Ttrend1[i / 1000] = Tvla.WriteTTrace("Ttest_O1.txt", 1);
                        Ttrend2[i / 1000] = Tvla.WriteTTrace("Ttest_O2.txt", 2);
                        Ttrend3[i / 1000] = Tvla.WriteTTrace("Ttest_O3.txt", 3);
                        sw1.WriteLine("{0}", Ttrend1[i / 1000]);
                        sw2.WriteLine("{0}", Ttrend2[i / 1000]);
                        sw3.WriteLine("{0}", Ttrend3[i / 1000]);
                        sw1.Flush();
                        sw2.Flush();
                        sw3.Flush();
                    }
                }
                else
                    i = i - 1;
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            CloseOutputFile();
            sw1.Close();
            sw2.Close();
            sw3.Close();
            fs1.Close();
            fs2.Close();
            fs3.Close();
        }
        //采集函数
        public void MeasureTraces_Ttest_SITI8(string outfile, byte[] key, int samples, int TraceNum, string portname, uint delay)
        {
            byte SampleCoding = 0x02;
            int mlen = 12;
            float XScale = (float)2E-9;
            float YScale = (float)1;
            bool flag = true;
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples, delay);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            OpenOutputFile(outfile);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            double[] Ttrend1 = new double[TraceNum / 1000];
            double[] Ttrend2 = new double[TraceNum / 1000];
            double[] Ttrend3 = new double[TraceNum / 1000];
            FileStream fs1 = new FileStream("Ttrend-O1.txt", FileMode.Create);
            FileStream fs2 = new FileStream("Ttrend-O2.txt", FileMode.Create);
            FileStream fs3 = new FileStream("Ttrend-O3.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            StreamWriter sw2 = new StreamWriter(fs2);
            StreamWriter sw3 = new StreamWriter(fs3);
            double[] temp = new double[measurements.Length];
            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Trace {0}:", i);
                bool Tv = ra.NextDouble() > 0.5;
                flag = GetOneTrace_Ttest_SITI8(samples, mlen, plain, cipher, Tv, true);
                if (flag)
                {
                    //centeredTrace(measurements);
                    if (i < 10000)
                        WriteOneTrace(samples, plain, cipher);

                    for (int m = 0; m < temp.Length; m++)
                        temp[m] = (double)measurements[m];
                    Tvla.UpdateTrace(temp, Tv);
                    if (i % 1000 == 0)
                    {
                        Ttrend1[i / 1000] = Tvla.WriteTTrace("Ttest_O1.txt", 1);
                        Ttrend2[i / 1000] = Tvla.WriteTTrace("Ttest_O2.txt", 2);
                        Ttrend3[i / 1000] = Tvla.WriteTTrace("Ttest_O3.txt", 3);
                        sw1.WriteLine("{0}", Ttrend1[i / 1000]);
                        sw2.WriteLine("{0}", Ttrend2[i / 1000]);
                        sw3.WriteLine("{0}", Ttrend3[i / 1000]);
                        sw1.Flush();
                        sw2.Flush();
                        sw3.Flush();
                    }
                }
                else
                    i = i - 1;
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            CloseOutputFile();
            sw1.Close();
            sw2.Close();
            sw3.Close();
            fs1.Close();
            fs2.Close();
            fs3.Close();
        }

        //采集函数
        public void MeasureTraces_Ttest_MaskedAES(byte[] key, int samples, int TraceNum, string portname, uint delay,uint timebase)
        {
            //byte SampleCoding = 0x02;
            int mlen = 16;
            //float XScale = (float)2E-9;
            //float YScale = (float)1;
            bool flag = true;
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples, delay,timebase);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            //OpenOutputFile(outfile);
            //System.Console.WriteLine("写入trs文件头：");
            //WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            double[] Ttrend1 = new double[TraceNum / 1000];
            double[] Ttrend2 = new double[TraceNum / 1000];
            double[] Ttrend3 = new double[TraceNum / 1000];
            FileStream fs1 = new FileStream("Ttrend-O1.txt", FileMode.Create);
            FileStream fs2 = new FileStream("Ttrend-O2.txt", FileMode.Create);
            FileStream fs3 = new FileStream("Ttrend-O3.txt", FileMode.Create);
            FileStream fs4 = new FileStream("OneTrace.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            StreamWriter sw2 = new StreamWriter(fs2);
            StreamWriter sw3 = new StreamWriter(fs3);
            StreamWriter sw4 = new StreamWriter(fs4);
            double[] temp = new double[measurements.Length];
            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                {
                    System.Console.WriteLine("Trace {0}:", i);
                }
                bool Tv = ra.NextDouble() > 0.5;
                flag = GetOneTrace_Ttest_MaskedAES(samples, mlen, plain, cipher, Tv, true,timebase);
                if (flag)
                {
                    //centeredTrace(measurements);
                    //if (i < 10000)
                    //    WriteOneTrace(samples, plain, cipher);

                    for (int m = 0; m < temp.Length; m++)
                        temp[m] = (double)measurements[m];
                    Tvla.UpdateTrace(temp, Tv);
                    if (i % 1000 == 0)
                    {
                        Ttrend1[i / 1000] = Tvla.WriteTTrace("ANSSI_AffineMaskedAES_Sbox_Ttest_O1.txt", 1);
                        Ttrend2[i / 1000] = Tvla.WriteTTrace("ANSSI_AffineMaskedAES_Sbox_Ttest_O2.txt", 2);
                        Ttrend3[i / 1000] = Tvla.WriteTTrace("ANSSI_AffineMaskedAES_Sbox_Ttest_O3.txt", 3);
                        sw1.WriteLine("{0}", Ttrend1[i / 1000]);
                        sw2.WriteLine("{0}", Ttrend2[i / 1000]);
                        sw3.WriteLine("{0}", Ttrend3[i / 1000]);
                        sw1.Flush();
                        sw2.Flush();
                        sw3.Flush();
                        if (i == 0)
                        {
                            for (int j = 0; j < samples; j++)
                                sw4.WriteLine("{0}", temp[j]);
                            sw4.Close();
                            fs4.Close();
                        }
                    }
                }
                else
                    i = i - 1;
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            //CloseOutputFile();
            sw1.Close();
            sw2.Close();
            sw3.Close();
            fs1.Close();
            fs2.Close();
            fs3.Close();
        }

        //采集函数
        public void MeasureTraces_RapidTtest_MaskedAES(byte[] key, int samples, int TraceNum, string portname, uint delay, int nSeg)
        {
            //byte SampleCoding = 0x02;
            Mulmeasurements = new short[nSeg][];
            for(int i=0;i<nSeg;i++)
                Mulmeasurements[i]=new short[samples];
            int mlen = 16;
            //float XScale = (float)2E-9;
            //float YScale = (float)1;
            bool flag = true;
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope_Rapid(samples, delay, (ushort)nSeg);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            //OpenOutputFile(outfile);
            //System.Console.WriteLine("写入trs文件头：");
            //WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            double[] Ttrend1 = new double[TraceNum / nSeg];
            double[] Ttrend2 = new double[TraceNum / nSeg];
            double[] Ttrend3 = new double[TraceNum / nSeg];
            FileStream fs1 = new FileStream("Ttrend-O1.txt", FileMode.Create);
            FileStream fs2 = new FileStream("Ttrend-O2.txt", FileMode.Create);
            FileStream fs3 = new FileStream("Ttrend-O3.txt", FileMode.Create);
            FileStream fs4 = new FileStream("OneTrace.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            StreamWriter sw2 = new StreamWriter(fs2);
            StreamWriter sw3 = new StreamWriter(fs3);
            StreamWriter sw4 = new StreamWriter(fs4);
            double[] temp = new double[measurements.Length];
            for (int i = 0; i < TraceNum/nSeg; i++)
            {
                System.Console.WriteLine("Trace {0}:", i * nSeg);
                bool[] Tv = new bool[nSeg];
                for (int j = 0; j < nSeg; j++)
                    Tv[j] = (ra.NextDouble() > 0.5);
                flag = GetMultipleTrace_RapidTtest_MaskedAES(samples, mlen, Tv, true,nSeg);
                if (flag)
                {
                    for (int j = 0; j < nSeg; j++)
                    {
                        for (int m = 0; m < temp.Length; m++)
                            temp[m] = (double)Mulmeasurements[j][m];
                        Tvla.UpdateTrace(temp, Tv[j]);
                    }
                    Ttrend1[i] = Tvla.WriteTTrace("Sebastien_BS_MaskedAES_RNGOn_O1.txt", 1);
                    Ttrend2[i] = Tvla.WriteTTrace("Sebastien_BS_MaskedAES_RNGOn_O2.txt", 2);
                    Ttrend3[i] = Tvla.WriteTTrace("Sebastien_BS_MaskedAES_RNGOn_O3.txt", 3);
                        sw1.WriteLine("{0}", Ttrend1[i]);
                        sw2.WriteLine("{0}", Ttrend2[i]);
                        sw3.WriteLine("{0}", Ttrend3[i]);
                        sw1.Flush();
                        sw2.Flush();
                        sw3.Flush();
                }
                else
                    i = i - 1;
                if (i == 0)
                {
                    for (int j = 0; j < samples; j++)
                        sw4.WriteLine("{0}", temp[j]);
                    sw4.Close();
                    fs4.Close();
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            //CloseOutputFile();
            sw1.Close();
            sw2.Close();
            sw3.Close();
            fs1.Close();
            fs2.Close();
            fs3.Close();
        }

        //采集函数
        public void MeasureTraces_TableCollision(byte[] key, int samples, int TraceNum, string portname, uint timebase)
        {

            int mlen = 16;
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            //float XScale = (float)2E-9;
            //float YScale = (float)1;
            bool flag = true;
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples, 0);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            //OpenOutputFile(outfile);
            //System.Console.WriteLine("写入trs文件头：");
            //WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            double[] Ttrend1 = new double[TraceNum / 1000];
            double[] Ttrend2 = new double[TraceNum / 1000];
            double[] Ttrend3 = new double[TraceNum / 1000];
            FileStream fs1 = new FileStream("Ttrend-O1.txt", FileMode.Create);
            FileStream fs2 = new FileStream("Ttrend-O2.txt", FileMode.Create);
            FileStream fs3 = new FileStream("Ttrend-O3.txt", FileMode.Create);
            FileStream fs4 = new FileStream("OneTrace.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            StreamWriter sw2 = new StreamWriter(fs2);
            StreamWriter sw3 = new StreamWriter(fs3);
            StreamWriter sw4 = new StreamWriter(fs4);
            double[] temp = new double[measurements.Length];
            for (int i = 0; i < TraceNum ; i++)
            {
                
                bool Tv = ra.NextDouble() > 0.5;
                flag = GetOneTrace_TestCollision(samples, mlen, plain, cipher, Tv,timebase);
                if (flag)
                {
                    for (int m = 0; m < temp.Length; m++)
                        temp[m] = (double)measurements[m];
                    Tvla.UpdateTrace(temp, Tv);
                    if (i % 1000 == 0 && i > 0)
                    {
                        System.Console.WriteLine("Trace {0}:", i);
                        Ttrend1[i/1000] = Tvla.WriteTTrace("TestTableCollision_O1.txt", 1);
                        Ttrend2[i/1000] = Tvla.WriteTTrace("TestTableCollision_O2.txt", 2);
                        Ttrend3[i/1000] = Tvla.WriteTTrace("TestTableCollision_O3.txt", 3);
                        sw1.WriteLine("{0}", Ttrend1[i/1000]);
                        sw2.WriteLine("{0}", Ttrend2[i/1000]);
                        sw3.WriteLine("{0}", Ttrend3[i/1000]);
                        sw1.Flush();
                        sw2.Flush();
                        sw3.Flush();
                    }
                }
                else
                    i = i - 1;
                if (i == 0)
                {
                    for (int j = 0; j < samples; j++)
                        sw4.WriteLine("{0}", temp[j]);
                    sw4.Close();
                    fs4.Close();
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            //CloseOutputFile();
            sw1.Close();
            sw2.Close();
            sw3.Close();
            fs1.Close();
            fs2.Close();
            fs3.Close();
        }

        public void MeasureTraces_PowerModel(string outfile, byte[] key, int samples, int TraceNum, string portname, uint delay,int repeat)
        {
            byte SampleCoding = 0x02;
            int mlen = 32;
            float XScale = (float)2E-9;
            float YScale = (float)1;
            bool flag = true;
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples, delay);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            OpenOutputFile(outfile);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Trace {0}:", i);
                flag = GetOneTrace_PowerModel(samples, mlen, plain, cipher,i,repeat);
                if (flag)
                {
                    WriteOneTrace(samples, plain, cipher);
                }
                else
                    i = i - 1;
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            CloseOutputFile();

        }

        public void MeasureTraces_PowerModel_Full(string outfile, byte[] key, int samples, int TraceNum, string portname, uint delay, int repeat)
        {
            byte SampleCoding = 0x02;
            int mlen = 32;
            float XScale = (float)2E-9;
            float YScale = (float)1;
            bool flag = true;
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples, delay);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            OpenOutputFile(outfile);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            int num_of_instr = 125;
            int[] order = new int[num_of_instr];
            double[] value = new double[num_of_instr];
            for (int i = 0; i < TraceNum / num_of_instr; i++)
            {
                if (i % 10 == 0)
                    System.Console.WriteLine("Loop {0}:", i);
                //Create Random permutation
                for (int j = 0; j < num_of_instr; j++)
                {
                    value[j] = ra.NextDouble();
                    order[j] = j;
                }
                Array.Sort(value, order);
                for (int j = 0; j < num_of_instr; j++)
                {
                    flag = GetOneTrace_PowerModel_Full(samples, mlen, plain, cipher, order[j], repeat);
                    if (flag)
                    {
                        WriteOneTrace(samples, plain, cipher);
                    }
                    else
                        j = j - 1;
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            CloseOutputFile();

        }


        //
        public void MeasureTraces_BitSlicedAND2(string TraceFile, int samples, int TraceNum, string portname, uint timebase)
        {
            byte SampleCoding = 0x02;
            int mlen = 16;
            float XScale = (float)4E-9;
            float YScale = (float)1;
            bool flag = true;
            System.Console.WriteLine("Papare Scope:");
            flag = PrepareScope(samples, 0, timebase);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("Papare Cryptographic Device:");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            OpenOutputFile(TraceFile);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
           
            byte[] plain = new byte[16];
            byte[] cipher = new byte[16];

            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Trace {0}:", i);
                flag = GetOneTrace_BitSlicedAND2(plain,cipher,samples, timebase);

                if (flag)
                {
                    WriteOneTrace(samples, plain, cipher);
                }
                else
                    i = i - 1;
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            CloseOutputFile();
        }

        public void Window_Aggregation(double[] trace, int window)
        {
            for (int i = 0; i < trace.Length - window; i++)
                for (int j = 0; j < window; j++)
                    trace[i] = trace[i] + trace[i + j];
        }
        public void MeasureTraces_BitInteraction(string TRSfilename,int samples, int TraceNum, string portname, uint timebase, int instr, string instrname, int shares)
        {
            bool flag = true;
            byte SampleCoding = 0x02;
            int mlen = 16;
            float XScale = (float)4E-9;
            float YScale = (float)1;
            OpenOutputFile(TRSfilename);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            UnivariateTtest Tvla1 = new UnivariateTtest(samples);
            System.Console.WriteLine("Papare Scope:");
            flag = PrepareScope(samples, 0,timebase);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("Papare Cryptographic Device:");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            double[] Ttrend1 = new double[TraceNum / 1000];
            double[] Ttrend2 = new double[TraceNum / 1000];
            double[] Ttrend3 = new double[TraceNum / 1000];
            double[] Ttrend4 = new double[TraceNum / 1000];
            if (!Directory.Exists(instrname))
            {
                Directory.CreateDirectory(instrname);
            }
            FileStream fs1 = new FileStream(instrname + "/" + "Ttrend-O1.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            FileStream fs2 = new FileStream(instrname + "/" + "Ttrend-O2.txt", FileMode.Create);
            StreamWriter sw2 = new StreamWriter(fs2);
            FileStream fs3 = new FileStream(instrname + "/" + "Ttrend-O3.txt", FileMode.Create);
            StreamWriter sw3 = new StreamWriter(fs3);
            FileStream fs4 = new FileStream(instrname + "/" + "Ttrend-O4.txt", FileMode.Create);
            StreamWriter sw4 = new StreamWriter(fs4);
            FileStream fs = new FileStream(instrname + "/" + "OneTrace.txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            double[] temp = new double[measurements.Length];
            byte[] plain = new byte[16];
            byte[] cipher = new byte[16];
           
            for (int i = 0; i < TraceNum ; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Loop {0}:", i);
                bool Tv = ra.NextDouble() > 0.5;
                flag = GetOneTrace_Ttest_BitInteraction(samples, timebase, instr, shares, Tv);
                for (int m = 0; m < temp.Length; m++)
                    temp[m] = (double)measurements[m];
                if (Tv)
                    plain[0] = 0;
                else
                    plain[0] = 1;
                WriteOneTrace(samples,plain,cipher);

                //Filtering
                //HighPass(12E6, temp,250E6);
                if (i == 0)
                {
                    for (int j = 0; j < temp.Length; j++)
                        sw.WriteLine("{0}", temp[j]);
                    sw.Close();
                    fs.Close();
                }    
                //Window_Aggregation(temp,5);
                //Filtering
                Tvla.UpdateTrace(temp, Tv);
                for (int m = 0; m < temp.Length; m++)
                    temp[m] = (double)measurements[m];
                //Filtering
                //LowPass(2E6, temp, 250E6);

                //Filtering
                for (int m = 0; m < temp.Length; m++)
                    temp[m] = Math.Pow(temp[m],4);
                Tvla1.UpdateTrace(temp, Tv);
                if (i % 1000 == 0)
                {
                    Ttrend1[i / 1000] = Tvla.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O1.txt", 1);
                    Ttrend2[i / 1000] = Tvla.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O2.txt", 2);
                    Ttrend3[i / 1000] = Tvla.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O3.txt", 3);
                    Ttrend4[i / 1000] = Tvla1.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O4.txt", 1);
                    sw1.WriteLine("{0}", Ttrend1[i / 1000]);
                    sw1.Flush();
                    sw2.WriteLine("{0}", Ttrend2[i / 1000]);
                    sw2.Flush();
                    sw3.WriteLine("{0}", Ttrend3[i / 1000]);
                    sw3.Flush();
                    sw4.WriteLine("{0}", Ttrend4[i / 1000]);
                    sw4.Flush();
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            sw1.Close();
            fs1.Close();
            sw2.Close();
            fs2.Close();
            sw3.Close();
            fs3.Close();
            sw4.Close();
            fs4.Close();
            CloseOutputFile();
            
        }
        public int HW(byte x)
        {
            int hw=0;
            for (int i = 0; i < 8; i++)
            {
                if ((x & 0x01) == 1)
                    hw++;
                x=(byte)(x>>1);
            }
            return hw;
        }
        public void MeasureTraces_BitInteraction_RapidRepeat(string TRSfilename, int samples, int TraceNum, string portname, uint timebase, int instr, string instrname, int shares, int repeat)
        {
            bool flag = true;
            Mulmeasurements = new short[repeat][];
            for (int i = 0; i < repeat; i++)
                Mulmeasurements[i] = new short[samples];
            byte SampleCoding = 0x02;
            int mlen = 16;
            float XScale = (float)4E-9;
            float YScale = (float)1;
            OpenOutputFile(TRSfilename);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            UnivariateTtest Tvla1 = new UnivariateTtest(samples);
            UnivariateTtest Tvla2 = new UnivariateTtest(1);
            System.Console.WriteLine("Papare Scope:");
            //byte[] key = { 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
            //InspectorReader_int inspr = new InspectorReader_int("SCALE_4shares_M3_BitSlicedAND2_ThumbShiftRight_FULLAND2_BlockwiseSlicing_8SamePlaintexts_NoRefreshing_1M_Avg200.trs", "AES", true, 0, 600, key, 50000);
            flag = PrepareScope_Rapid(samples, 0, timebase,(ushort)repeat);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("Papare Cryptographic Device:");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            double[] Ttrend1 = new double[TraceNum / 1000];
            double[] Ttrend2 = new double[TraceNum / 1000];
            double[] Ttrend3 = new double[TraceNum / 1000];
            double[] Ttrend4 = new double[TraceNum / 1000];
            if (!Directory.Exists(instrname))
            {
                Directory.CreateDirectory(instrname);
            }
            FileStream fs1 = new FileStream(instrname + "/" + "Ttrend-O1.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            FileStream fs2 = new FileStream(instrname + "/" + "Ttrend-O2.txt", FileMode.Create);
            StreamWriter sw2 = new StreamWriter(fs2);
            FileStream fs3 = new FileStream(instrname + "/" + "Ttrend-O3.txt", FileMode.Create);
            StreamWriter sw3 = new StreamWriter(fs3);
            FileStream fs4 = new FileStream(instrname + "/" + "Ttrend-O4.txt", FileMode.Create);
            StreamWriter sw4 = new StreamWriter(fs4);
            FileStream fs = new FileStream(instrname + "/" + "OneTrace.txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            double[] temp = new double[measurements.Length];
            byte[] plain = new byte[16];
            byte[] cipher = new byte[16];
            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Loop {0}:", i);
               
                //plain[4] = inspr.ciphertext[i][0];
                //plain[5] = inspr.ciphertext[i][1];
                //plain[6] = inspr.ciphertext[i][2];
                //plain[7] = inspr.ciphertext[i][3];
               

                //bool Tv = GetBSSbox_M34(key, inspr.plaintext[i]);
                bool Tv = ra.NextDouble() > 0.5;
                
                flag = GetOneTrace_Ttest_BitInteraction_RapidRepeat(plain,samples, timebase, instr, shares, Tv,repeat);
                if (flag == false)
                {
                    System.Console.WriteLine("Acquisiton Error!");
                    i = i - 1;
                    continue;
                }
                double[] model = new double[1];
                for (int j = 0; j < 4; j++)
                    model[0] = model[0] + HW(plain[4 + j]);

                if ((Tv == true) && (HW((byte)(plain[7] & 0x0f)) % 2!=0))
                    System.Console.WriteLine("Error!");
                
                Tvla2.UpdateTrace(model, Tv);

                //avg traces
                //average all trace to one 
                double[] sum = new double[samples];
                for (int m = 0; m < repeat; m++)
                {
                    //if (Mulmeasurements[m][99] > 10000)
                    //{
                    //    n--;
                    //    continue;
                    //}
                    for (int j = 0; j < samples; j++)
                        sum[j] = sum[j] + Mulmeasurements[m][j];
                }
                for (int j = 0; j < samples; j++)
                {
                    temp[j] = (sum[j] / (repeat));
                    measurements[j] = (short)(sum[j] / (repeat));
                }

                if (Tv)
                    plain[0] = 0;
                else
                    plain[0] = 1;
                WriteOneTrace(samples, plain, cipher);
                if (i<100)
                {

                    for (int m = 0; m < repeat; m++)
                    {
                        for (int j = 0; j < temp.Length; j++)
                            sw.Write("{0}\t", Mulmeasurements[m][j]);
                        sw.WriteLine("");
                    }
                    sw.Close();
                    fs.Close();
                }
                //HighPass(12E6, temp, 250E6);
                Tvla.UpdateTrace(temp, Tv);
                for (int m = 0; m < temp.Length; m++)
                    temp[m] = measurements[m];

                for (int m = 0; m < temp.Length; m++)
                    temp[m] = Math.Pow(temp[m], 4);

                Tvla1.UpdateTrace(temp, Tv);
                if (i % 1000 == 0)
                {
                    Ttrend1[i / 1000] = Tvla.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O1.txt", 1);
                    Ttrend2[i / 1000] = Tvla.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O2.txt", 2);
                    Ttrend3[i / 1000] = Tvla.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O3.txt", 3);
                    Ttrend4[i / 1000] = Tvla1.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "shares_O4.txt", 1);
                    sw1.WriteLine("{0}", Ttrend1[i / 1000]);
                    sw1.Flush();
                    sw2.WriteLine("{0}", Ttrend2[i / 1000]);
                    sw2.Flush();
                    sw3.WriteLine("{0}", Ttrend3[i / 1000]);
                    sw3.Flush();
                    sw4.WriteLine("{0}", Ttrend4[i / 1000]);
                    sw4.Flush();
                    Tvla2.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "RandomShares_O1.txt", 1);
                    Tvla2.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "RandomShares_O2.txt", 2);
                    Tvla2.WriteTTrace(instrname + "/" + "TLVATest_Instr" + instrname + "_" + shares + "RandomShares_O3.txt", 3);
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            sw1.Close();
            fs1.Close();
            sw2.Close();
            fs2.Close();
            sw3.Close();
            fs3.Close();
            sw4.Close();
            fs4.Close();
            CloseOutputFile();

        }

        public uint SAND(uint a, uint b)
        {
            return a & b;
        }
        //取bit操作；single byte
        public bool GetBit(byte x, int index)
        {
            int no = index % 8;
            int mask;
            switch (no)
            {
                case 0: mask = 0x01; break;
                case 1: mask = 0x02; break;
                case 2: mask = 0x04; break;
                case 3: mask = 0x08; break;
                case 4: mask = 0x10; break;
                case 5: mask = 0x20; break;
                case 6: mask = 0x40; break;
                case 7: mask = 0x80; break;
                default: mask = 0x00; break;

            }
            return ((x & mask) > 0);
        }
        //置bit操作，32bit
        public int SetBit1(int x, int ind)
        {
            uint mask = 0;
            switch (ind)
            {
                case 0: mask = 0x00000001; break;
                case 1: mask = 0x00000002; break;
                case 2: mask = 0x00000004; break;
                case 3: mask = 0x00000008; break;
                case 4: mask = 0x00000010; break;
                case 5: mask = 0x00000020; break;
                case 6: mask = 0x00000040; break;
                case 7: mask = 0x00000080; break;
                case 8: mask = 0x00000100; break;
                case 9: mask = 0x00000200; break;
                case 10: mask = 0x00000400; break;
                case 11: mask = 0x00000800; break;
                case 12: mask = 0x00001000; break;
                case 13: mask = 0x00002000; break;
                case 14: mask = 0x00004000; break;
                case 15: mask = 0x00008000; break;
                case 16: mask = 0x00010000; break;
                case 17: mask = 0x00020000; break;
                case 18: mask = 0x00040000; break;
                case 19: mask = 0x00080000; break;
                case 20: mask = 0x00100000; break;
                case 21: mask = 0x00200000; break;
                case 22: mask = 0x00400000; break;
                case 23: mask = 0x00800000; break;
                case 24: mask = 0x01000000; break;
                case 25: mask = 0x02000000; break;
                case 26: mask = 0x04000000; break;
                case 27: mask = 0x08000000; break;
                case 28: mask = 0x10000000; break;
                case 29: mask = 0x20000000; break;
                case 30: mask = 0x40000000; break;
                case 31: mask = 0x80000000; break;
                default: mask = 0; break;
            }
            return (int)((uint)x | mask);
        }
        public bool BSSbox_M34(byte x)
        {
            uint
    T1, T2, T3, T4, T5, T6, T7, T8,
    T9, T10, T11, T12, T13, T14, T15, T16,
    T17, T18, T19, T20, T21, T22, T23, T24,
    T25, T26, T27;
            uint[] U = new uint[8];
            for (int i = 0; i < 8; i++)
                if (GetBit(x, i))
                    U[i] = 1;
                else
                    U[i] = 0;

            uint
                M1, M2, M3, M4, M5, M6, M7, M8,
                M9, M10, M11, M12, M13, M14, M15,
                M16, M17, M18, M19, M20, M21, M22,
                M23, M24, M25, M26, M27, M28, M29,
                M30, M31, M32, M33, M34, M35, M36,
                M37, M38, M39, M40, M41, M42, M43,
                M44, M45, M46, M47, M48, M49, M50,
                M51, M52, M53, M54, M55, M56, M57,
                M58, M59, M60, M61, M62, M63;

            uint
                L0, L1, L2, L3, L4, L5, L6, L7, L8,
                L9, L10, L11, L12, L13, L14,
                L15, L16, L17, L18, L19, L20,
                L21, L22, L23, L24, L25, L26,
                L27, L28, L29;



            T1 = U[7] ^ U[4];
            T2 = U[7] ^ U[2];
            T3 = U[7] ^ U[1];
            T4 = U[4] ^ U[2];
            T5 = U[3] ^ U[1];
            T6 = T1 ^ T5;
            T7 = U[6] ^ U[5];
            T8 = U[0] ^ T6;
            T9 = U[0] ^ T7;
            T10 = T6 ^ T7;
            T11 = U[6] ^ U[2];
            T12 = U[5] ^ U[2];
            T13 = T3 ^ T4;
            T14 = T6 ^ T11;
            T15 = T5 ^ T11;
            T16 = T5 ^ T12;
            T17 = T9 ^ T16;
            T18 = U[4] ^ U[0];
            T19 = T7 ^ T18;
            T20 = T1 ^ T19;
            T21 = U[1] ^ U[0];
            T22 = T7 ^ T21;
            T23 = T2 ^ T22;
            T24 = T2 ^ T10;
            T25 = T20 ^ T17;
            T26 = T3 ^ T16;
            T27 = T1 ^ T12;

            /*M1 = T13 & T6;*/
            M1 = SAND(T13, T6);//Masked AND2

            /*M2 = T23 & T8;*/
            M2 = SAND(T23, T8);

            M3 = T14 ^ M1;   //M0:12MHz <6.85us 

            //M4 = T19 & U[0];
            M4 = SAND(T19, U[0]);

            M5 = M4 ^ M1;

            //M6 = T3 & T16;
            M6 = SAND(T3, T16);
            //M7 = T22 & T9;
            M7 = SAND(T22, T9);

            M8 = T26 ^ M6;

            //M9 = T20 & T17;
            M9 = SAND(T20, T17);

            M10 = M9 ^ M6;

            //M11 = T1 & T15;
            M11 = SAND(T1, T15);
            //M12 = T4 & T27;
            M12 = SAND(T4, T27);

            M13 = M12 ^ M11;

            //M14 = T2 & T10;
            M14 = SAND(T2, T10);

            M15 = M14 ^ M11;
            M16 = M3 ^ M2;
            M17 = M5 ^ T24;
            M18 = M8 ^ M7;
            M19 = M10 ^ M15;
            M20 = M16 ^ M13;
            M21 = M17 ^ M15;
            M22 = M18 ^ M13;
            M23 = M19 ^ T25;
            M24 = M22 ^ M23;

            //M25 = M22 & M20;
            M25 = SAND(M22, M20);

            M26 = M21 ^ M25;
            M27 = M20 ^ M21;
            M28 = M23 ^ M25;

            //M29 = M28 & M27;
            M29 = SAND(M28, M27);
            //M30 = M26 & M24;
            M30 = SAND(M26, M24);
            //M31 = M20 & M23;
            M31 = SAND(M20, M23);
            //M32 = M27 & M31;/
            M32 = SAND(M27, M31);

            M33 = M27 ^ M25;

            //M34 = M21 & M22;
            M34 = SAND(M21, M22);
            //M35 = M24 & M34;
            M35 = SAND(M24, M34);

            M36 = M24 ^ M25;
            M37 = M21 ^ M29;
            M38 = M32 ^ M33;
            M39 = M23 ^ M30;
            M40 = M35 ^ M36;
            M41 = M38 ^ M40;
            M42 = M37 ^ M39;
            M43 = M37 ^ M38;
            M44 = M39 ^ M40;
            M45 = M42 ^ M41;

            //M46 = M44 & T6;
            M46 = SAND(M44, T6);
            //M47 = M40 & T8;
            M47 = SAND(M40, T8);
            //M48 = M39 & U[0];
            M48 = SAND(M39, U[0]);
            //M49 = M43 & T16;
            M49 = SAND(M43, T16);
            //M50 = M38 & T9;
            M50 = SAND(M38, T9);
            //M51 = M37 & T17;
            M51 = SAND(M37, T17);
            //M52 = M42 & T15;
            M52 = SAND(M42, T15);
            //M53 = M45 & T27;
            M53 = SAND(M45, T27);
            //M54 = M41 & T10;
            M54 = SAND(M41, T10);
            //M55 = M44 & T13;
            M55 = SAND(M44, T13);
            //M56 = M40 & T23;
            M56 = SAND(M40, T23);
            //M57 = M39 & T19;
            M57 = SAND(M39, T19);
            //M58 = M43 & T3;
            M58 = SAND(M43, T3);
            //M59 = M38 & T22;
            M59 = SAND(M38, T22);
            //M60 = M37 & T20;
            M60 = SAND(M37, T20);
            //M61 = M42 & T1;
            M61 = SAND(M42, T1);
            //M62 = M45 & T4;
            M62 = SAND(M45, T4);
            //M63 = M41 & T2;
            M63 = SAND(M41, T2);
            L0 = M61 ^ M62;
            L1 = M50 ^ M56;
            L2 = M46 ^ M48;
            L3 = M47 ^ M55;
            L4 = M54 ^ M58;
            L5 = M49 ^ M61;
            L6 = M62 ^ L5;
            L7 = M46 ^ L3;
            L8 = M51 ^ M59;
            L9 = M52 ^ M53;
            L10 = M53 ^ L4;
            L11 = M60 ^ L2;
            L12 = M48 ^ M51;
            L13 = M50 ^ L0;
            L14 = M52 ^ M61;
            L15 = M55 ^ L1;
            L16 = M56 ^ L0;
            L17 = M57 ^ L1;
            L18 = M58 ^ L8;
            L19 = M63 ^ L4;
            L20 = L0 ^ L1;
            L21 = L1 ^ L7;
            L22 = L3 ^ L12;
            L23 = L18 ^ L2;
            L24 = L15 ^ L9;
            L25 = L6 ^ L10;
            L26 = L7 ^ L9;
            L27 = L8 ^ L10;
            L28 = L11 ^ L14;
            L29 = L11 ^ L17;
            U[7] = L6 ^ L24;
            //U[6] = ~(L16 ^ L26);
            U[6] = 0x1 ^ (L16 ^ L26);
            //U[5] = ~(L19 ^ L28);
            U[5] = 0x1 ^ (L19 ^ L28);
            U[4] = L6 ^ L21;
            U[3] = L20 ^ L22;
            U[2] = L25 ^ L29;
            //U[1] = ~(L13 ^ L27);
            U[1] = 0x1 ^ (L13 ^ L27);
            //U[0] = ~(L6 ^ L23);
            U[0] = 0x1 ^ (L6 ^ L23);
            int y = 0;
            for (int i = 0; i < 8; i++)
                if ((U[i] & 0x01) > 0)
                    y = SetBit1(y, i);

            return (M34==0)?false:true;
        }
        public bool GetBSSbox_M34(byte[] key,byte[] plain)
        {
            AES aes = new AES();
            // byte[] c=aes.AES_Encrypt(Plaintext[0], key);
            byte[] temp = aes.GetSin_Correct(plain, key, 0);
            return BSSbox_M34(temp[0]);
        }


        //Adding traces to Ttest, in a seperate thread 
        public static void AddToTtest(double[][] Mulmeasurements, bool[] flag)
        {
            for (int i = 0; i < flag.Length; i++)
            {
                Tvla.UpdateTrace(Mulmeasurements[i], flag[i]);
                for (int j = 0; j < Mulmeasurements[i].Length; j++)
                    Mulmeasurements[i][j] = Math.Pow(Mulmeasurements[i][j], 4);
                Tvla1.UpdateTrace(Mulmeasurements[i], flag[i]);
            }
        }
        public void Ttest_BitInteraction_Thread(string TRSfilename, int samples, int TraceNum, string portname, uint timebase, byte[] key, bool refresh,byte internal_repeat, byte block_repeat)
        {
            Tvla = new UnivariateTtest(samples);
            Tvla1 = new UnivariateTtest(samples);
            UnivariateTtest Tvla2 = new UnivariateTtest(1);
            int repeat = 10 * internal_repeat;
            if (repeat == 0)
                repeat = 1;
            double[][] values = new double[repeat][];
            Mulmeasurements = new short[repeat][];
            for (int i = 0; i < repeat; i++)
            {
                Mulmeasurements[i] = new short[samples];
                values[i] = new double[samples];
            }
                

            bool flag = true;
            byte SampleCoding = 0x02;
            int mlen = 16;
            float XScale = (float)4E-9;
            float YScale = (float)1;
            int step = 1000;
            System.Console.WriteLine("Papare Scope:");
            flag = PrepareScope_Rapid(samples, 0, timebase, (ushort)repeat);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("Papare Cryptographic Device:");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            OpenOutputFile(TRSfilename);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            double[] Ttrend1 = new double[TraceNum / step];
            double[] Ttrend2 = new double[TraceNum / step];
            double[] Ttrend3 = new double[TraceNum / step];
            double[] Ttrend4 = new double[TraceNum / step];
            FileStream fs1 = new FileStream("Ttrend-O1.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            FileStream fs2 = new FileStream("Ttrend-O2.txt", FileMode.Create);
            StreamWriter sw2 = new StreamWriter(fs2);
            FileStream fs3 = new FileStream("Ttrend-O3.txt", FileMode.Create);
            StreamWriter sw3 = new StreamWriter(fs3);
            FileStream fs4 = new FileStream("Ttrend-O4.txt", FileMode.Create);
            StreamWriter sw4 = new StreamWriter(fs4);
            double[] temp = new double[measurements.Length];
            byte[] plain = new byte[16];
            byte[] cipher = new byte[16];
            Task pth = null;
            for (int i = 0; i < TraceNum; i++)
            {
                if (i % step == 0)
                    System.Console.WriteLine("Loop {0}:", i);
                ra.NextBytes(plain);
                flag = GetOneTrace_TAttack_Sbox_RapidRepeat_Thread(samples, timebase, plain, cipher, repeat, refresh, internal_repeat, block_repeat,pth,values);
                if (flag == false)
                {
                    System.Console.WriteLine("Overflow!");
                    i--;
                    continue;
                }

                //Get T flag
                bool Tv = GetBSSbox_M34(key, plain);
                if (Tv != (HW((byte)(cipher[0] & 0xf)) % 2 != 0))
                    System.Console.WriteLine("Error!");
                //Get T flag
                bool[] flaga=new bool[repeat];
                for(int j=0;j<repeat;j++)
                    flaga[j]=Tv;

                if(pth==null|| pth.IsCompleted==true)
                    pth = new Task(() => AddToTtest(values,flaga));
                pth.Start();

               
                if (i % step == 0)
                {
                    pth.Wait();
                    Ttrend1[i / step] = Tvla.WriteTTrace("TLVATest_BSSbox_M34_4shares_O1.txt", 1);
                    Ttrend2[i / step] = Tvla.WriteTTrace("TLVATest_BSSbox_M34_4shares_O2.txt", 2);
                    Ttrend3[i / step] = Tvla.WriteTTrace("TLVATest_BSSbox_M34_4shares_O3.txt", 3);
                    Ttrend4[i / step] = Tvla1.WriteTTrace("TLVATest_BSSbox_M34_4shares_O4.txt", 1);
                    sw1.WriteLine("{0}", Ttrend1[i / step]);
                    sw1.Flush();
                    sw2.WriteLine("{0}", Ttrend2[i / step]);
                    sw2.Flush();
                    sw3.WriteLine("{0}", Ttrend3[i / step]);
                    sw3.Flush();
                    sw4.WriteLine("{0}", Ttrend4[i / step]);
                    sw4.Flush();
                    Tvla2.WriteTTrace("TLVATest_Model_M34_4shares_O1.txt", 1);
                    Tvla2.WriteTTrace("TLVATest_Model_M34_4shares_O2.txt", 2);
                    Tvla2.WriteTTrace("TLVATest_Model_M34_4shares_O3.txt", 3);
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            sw1.Close();
            fs1.Close();
            sw2.Close();
            fs2.Close();
            sw3.Close();
            fs3.Close();
            sw4.Close();
            fs4.Close();
            CloseOutputFile();
        }

        public void Ttest_BitInteraction(string TRSfilename,int samples, int TraceNum, string portname, uint timebase,byte[] key,bool fresh, byte internal_repeat,byte block_repeat)
        {
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            UnivariateTtest Tvla1 = new UnivariateTtest(samples);
            UnivariateTtest Tvla2 = new UnivariateTtest(1);
            int repeat = 10*internal_repeat;
            if (repeat == 0)
                repeat = 1;
            Mulmeasurements = new short[repeat][];
            for (int i = 0; i < repeat; i++)
                Mulmeasurements[i] = new short[samples];

            bool flag = true;
            byte SampleCoding = 0x02;
            int mlen = 16;
            float XScale = (float)4E-9;
            float YScale = (float)1;
            int step = 1000;
            System.Console.WriteLine("Papare Scope:");
            flag = PrepareScope_Rapid(samples, 0, timebase,(ushort)repeat);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("Papare Cryptographic Device:");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            OpenOutputFile(TRSfilename);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            double[] Ttrend1 = new double[TraceNum / step];
            double[] Ttrend2 = new double[TraceNum / step];
            double[] Ttrend3 = new double[TraceNum / step];
            double[] Ttrend4 = new double[TraceNum / step];
            FileStream fs1 = new FileStream( "Ttrend-O1.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            FileStream fs2 = new FileStream( "Ttrend-O2.txt", FileMode.Create);
            StreamWriter sw2 = new StreamWriter(fs2);
            FileStream fs3 = new FileStream( "Ttrend-O3.txt", FileMode.Create);
            StreamWriter sw3 = new StreamWriter(fs3);
            FileStream fs4 = new FileStream("Ttrend-O4.txt", FileMode.Create);
            StreamWriter sw4 = new StreamWriter(fs4);
            FileStream fs = null;
            StreamWriter sw = null;
            double[] temp = new double[measurements.Length];
            byte[] plain = new byte[16];
            byte[] cipher = new byte[16];

            for (int i = 0; i < TraceNum; i++)
            {
                if (i % step == 0)
                    System.Console.WriteLine("Loop {0}:", i);
                ra.NextBytes(plain);
                flag = GetOneTrace_TAttack_Sbox_RapidRepeat(samples, timebase, plain,cipher,repeat,fresh,internal_repeat,block_repeat);
                if (flag == false)
                {
                    System.Console.WriteLine("Overflow!");
                    i--;
                    continue;
                }
                double[] sum = new double[samples];

                //Get T flag
                bool Tv = GetBSSbox_M34(key, plain);
                if (Tv != (HW((byte)(cipher[0] & 0xf)) % 2 != 0))
                    System.Console.WriteLine("Error!");
                //Get T flag
                if (fresh)
                {
                    for (int m = 0; m < repeat; m++)
                    {
                        for (int j = 0; j < samples; j++)
                        {
                            temp[j] = Mulmeasurements[m][j];
                            measurements[j] = Mulmeasurements[m][j];
                        }
                        WriteOneTrace(samples, plain, cipher);
                        Tvla.UpdateTrace(temp, Tv);
                        for (int j = 0; j < temp.Length; j++)
                            temp[j] = measurements[j];
                        for (int j = 0; j < temp.Length; j++)
                            temp[j] = Math.Pow(temp[j], 4);
                        Tvla1.UpdateTrace(temp, Tv);
                    }
                }
                else
                {
                    for (int m = 0; m < repeat; m++)
                    {
                        
                        for (int j = 0; j < samples; j++)
                        {
                            sum[j] += Mulmeasurements[m][j];
                        }

                    }
                    double mean = 0;

                    for (int j = 0; j < samples; j++)
                    {
                        temp[j] = sum[j] / (repeat);
                        //if(j>=400)
                           mean += temp[j];
                    }
                    mean = mean / samples;
                    //mean= 0;
                    for (int j = 0; j < samples; j++)
                    {
                        temp[j] = temp[j] - mean;
                        //measurements[j] = (short)((sum[j] / repeat));
                        measurements[j] = (short)((sum[j] / (repeat)) - mean);
                    }
                    
                    WriteOneTrace(samples, plain, cipher);

                    double[] model = new double[1];
                    for (int j = 0; j < 4; j++)
                        model[0] = model[0] + HW((byte)cipher[j]);
                    Tvla2.UpdateTrace(model, Tv);
                    //Filtering
                    //LowPass(12E6, temp, 250E6);
                    //HighPass(12E6, temp, 250E6);

                    Tvla.UpdateTrace(temp, Tv);
                    for (int j = 0; j < temp.Length; j++)
                        temp[j] = measurements[j];
                    
                    //Filtering
                   // LowPass(15E6, temp, 250E6);

                    //Filtering
                    for (int j = 0; j < temp.Length; j++)
                        temp[j] = Math.Pow(temp[j], 4);
                    Tvla1.UpdateTrace(temp, Tv);
                }

                if ((i % step) < 10)
                {
                    if (i % step == 0)
                    {
                        fs = new FileStream("OneTrace.txt", FileMode.Create);
                        sw = new StreamWriter(fs);
                    }
                   // for (int m = 0; m < repeat; m++)
                   // {
                        for (int j = 0; j < samples; j++)
                            sw.Write("{0}\t", measurements[j]);
                        sw.WriteLine("");
                   // }
                        if (i % step == 9)
                        {
                            sw.Close();
                            fs.Close();
                        }

                }
                if (i % step == 0)
                {
                    Ttrend1[i / step] = Tvla.WriteTTrace("TLVATest_BSSbox_M34_4shares_O1.txt", 1);
                    Ttrend2[i / step] = Tvla.WriteTTrace("TLVATest_BSSbox_M34_4shares_O2.txt", 2);
                    Ttrend3[i / step] = Tvla.WriteTTrace("TLVATest_BSSbox_M34_4shares_O3.txt", 3);
                    Ttrend4[i / step] = Tvla1.WriteTTrace("TLVATest_BSSbox_M34_4shares_O4.txt", 1);
                    sw1.WriteLine("{0}", Ttrend1[i / step]);
                    sw1.Flush();
                    sw2.WriteLine("{0}", Ttrend2[i / step]);
                    sw2.Flush();
                    sw3.WriteLine("{0}", Ttrend3[i / step]);
                    sw3.Flush();
                    sw4.WriteLine("{0}", Ttrend4[i / step]);
                    sw4.Flush();
                    Tvla2.WriteTTrace("TLVATest_Model_M34_4shares_O1.txt", 1);
                    Tvla2.WriteTTrace("TLVATest_Model_M34_4shares_O2.txt", 2);
                    Tvla2.WriteTTrace("TLVATest_Model_M34_4shares_O3.txt", 3);
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            sw1.Close();
            fs1.Close();
            sw2.Close();
            fs2.Close();
            sw3.Close();
            fs3.Close();
            sw4.Close();
            fs4.Close();
            CloseOutputFile();
        }

        public void Ttest_BitInteraction_Attack(int samples, int TraceNum, string portname, uint timebase, byte[] key)
        {
           
            bool flag = true;
            System.Console.WriteLine("Papare Scope:");
            flag = PrepareScope(samples, 0, timebase);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("Papare Cryptographic Device:");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            double[,] maxTtrend1 = new double[256,TraceNum / 1000];
            double[,] maxTtrend2 = new double[256,TraceNum / 1000];
            UnivariateTtest[] Tvla = new UnivariateTtest[256];
            for (int k = 0; k < 256; k++)
            {
                Tvla[k] = new UnivariateTtest(samples);
            }
            FileStream fs1 = new FileStream("maxTtrend-O1.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            FileStream fs2 = new FileStream("maxTtrend-O2.txt", FileMode.Create);
            StreamWriter sw2 = new StreamWriter(fs2);
           
            double[] temp = new double[measurements.Length];
            byte[] plain = new byte[16];
            byte[] cipher = new byte[16];

            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Loop {0}:", i);
                ra.NextBytes(plain);
                flag = GetOneTrace_TAttack_Sbox(samples, timebase, plain,cipher);
                if (flag == false)
                {
                    System.Console.WriteLine("Overflow!");
                    i--;
                    continue;
                }
                for (int m = 0; m < temp.Length; m++)
                    temp[m] = (double)measurements[m];
                //Get T flag

               

                //Get T flag
                for (int k = 0; k < 256; k++)
                {
                    key[0] = (byte)k;
                    bool Tv = GetBSSbox_M34(key, plain);
                    Tvla[k].UpdateTrace(temp,Tv);
                }
                if (i % 1000 == 0)
                {
                    for (int k = 0; k < 256; k++)
                    {
                        maxTtrend1[k,i / 1000]= Tvla[k].GetMaxT(1);
                        maxTtrend2[k, i / 1000] = Tvla[k].GetMaxT(2);
                    }
                    
                }
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();

            for (int k = 0; k < 256; k++)
            {
                for (int i = 0; i < TraceNum / 1000; i++)
                {
                    sw1.Write("{0}\t", maxTtrend1[k,i]);
                    sw2.Write("{0}\t", maxTtrend2[k, i]);
                }
                sw1.WriteLine("");
                sw2.WriteLine("");
                Tvla[k].WriteTTrace_Row("keyTTrace-O1.txt", 1);
                Tvla[k].WriteTTrace_Row("keyTTrace-O2.txt", 2);
            }

            sw1.Close();
            fs1.Close();
            sw2.Close();
            fs2.Close();
           

        }
        //采集函数
        public void MeasureTraces_PowerModel_Ttest(string outfile, byte[] key, int samples, int TraceNum, string portname, uint delay, int instr,int[] target)
        {
            byte SampleCoding = 0x02;
            int mlen =32;
            float XScale = (float)4E-9;
            float YScale = (float)1;
            bool flag = true;
            UnivariateTtest Tvla = new UnivariateTtest(samples);
            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples, delay);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            System.Console.WriteLine("打开输入文件：");
            OpenOutputFile(outfile);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);
            System.Console.WriteLine("开始采集：");
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            double[] Ttrend1 = new double[TraceNum / 1000];       
            FileStream fs1 = new FileStream("Ttrend-O1.txt", FileMode.Create);
            StreamWriter sw1 = new StreamWriter(fs1);
            double[] temp = new double[measurements.Length];
            for (int i = 0; i < TraceNum; i++)
            {
                if (i % 1000 == 0)
                    System.Console.WriteLine("Trace {0}:", i);
                bool Tv = ra.NextDouble() > 0.5;
                flag = GetOneTrace_PowerModel_Ttest(samples, mlen, plain, cipher, instr,target,Tv);
                if (flag)
                {
                    if (i < 10000)
                        WriteOneTrace(samples, plain, cipher);

                    for (int m = 0; m < temp.Length; m++)
                        temp[m] = (double)measurements[m];
                    Tvla.UpdateTrace(temp, Tv);
                    if (i % 1000 == 0)
                    {
                        Ttrend1[i / 1000] = Tvla.WriteTTrace("Ttest_O1.txt", 1);
                        sw1.WriteLine("{0}", Ttrend1[i / 1000]);
                        sw1.Flush();
                    }
                }
                else
                    i = i - 1;
            }
            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();
            sw1.Close();
            fs1.Close();

        }

        public bool[] TestOneInstr(int samples,int TraceNum, int instr, int[][] target, string instr_name, string[] TtestTarget)
        {
            
            int mlen = 32;
            byte[] plain = new byte[mlen];
            byte[] cipher = new byte[mlen];
            double[] temp = new double[measurements.Length];
            bool[] result = new bool[target.Length];
            FileStream fs = new FileStream("Tinspecting_"+instr_name+".txt", FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            Directory.CreateDirectory(instr_name + "//");
            for (int j = 0; j < target.Length; j++)
            {
                UnivariateTtest Tvla = new UnivariateTtest(samples);
                for (int i = 0; i < TraceNum; i++)
                {
                    //if (i % 1000 == 0)
                    //    System.Console.WriteLine("Trace {0}:", i);
                    bool Tv = ra.NextDouble() > 0.5;
                    bool flag = GetOneTrace_PowerModel_Ttest(samples, mlen, plain, cipher, instr, target[j], Tv);
                    if (flag)
                    {
                        for (int m = 0; m < temp.Length; m++)
                            temp[m] = (double)measurements[m];
                        Tvla.UpdateTrace(temp, Tv);
                        //if (i % 1000 == 999)
                        //{
                        //    Tvla.WriteTTrace(instr_name + "//Ttest" + TtestTarget[j] + ".txt", 1);
                       // }
                    }
                    else
                        i = i - 1;
                }
                double v=Tvla.WriteTTrace(instr_name + "//Ttest" + TtestTarget[j] + ".txt", 1);
                if (v > 5)
                {
                    result[j] = true;
                    sw.WriteLine(TtestTarget[j] + ":\t{0}\t{1}", "YES", v);
                    System.Console.WriteLine(TtestTarget[j] + ":\t{0}\t{1}", "YES", v);
                }
                else
                {
                    result[j] = false;
                    sw.WriteLine(TtestTarget[j] + ":\t{0}\t{1}", "NO", v);
                    System.Console.WriteLine(TtestTarget[j] + ":\t{0}\t{1}", "NO", v);
                }
                sw.Flush();
            }
            sw.Close();
            fs.Close();
            return result;
        }

        public void Ttest_Inspecting(string TRSfilename, byte[] key, int samples, int TraceNum, string portname, uint delay)
        {
            bool flag = true;
            byte SampleCoding = 0x02;
            int mlen = 16;
            float XScale = (float)4E-9;
            float YScale = (float)1;
            OpenOutputFile(TRSfilename);
            System.Console.WriteLine("写入trs文件头：");
            WriteFileHeader(TraceNum, samples, SampleCoding, mlen, XScale, YScale);

            System.Console.WriteLine("示波器准备：");
            flag = PrepareScope(samples, delay);
            if (!flag)
            {
                return;
            }
            System.Console.WriteLine("密码设备准备：");
            flag = PrepareCryptoModule(portname);
            if (!flag)
            {
                CloseScope();
                return;
            }
            //string[] TtestTarget={"Op1","Op2","Addr","PrevOp1^Op1","PrevOp1^Op2","PrevOp2^Op1","PrevOp2^Op2","ALUoutBus","Op1^Op2","ALUresult"};
            string[] TtestTarget = { "PrevOp2^Op2" };
            string[] instr_name ={"add","addimm0","and","cmp","cmpimm","eor","ldr","ldrb","ldrh","lsl","lslimm","lsr","lsrimm","mov","movimm","mul","orr",
                                    "ror","str","strb","strh","sub","subimm"};
            System.Console.WriteLine("开始采集：");
            //int[][] target=new int[10][];
            //target[0]=new int[]{2};//Op1
            //target[1]=new int[]{3};//Op2
            //target[2]=new int[]{6};//Addr
            //target[3]=new int[]{0,2};//PrevOp1^Op1
            //target[4]=new int[]{0,3};//PrevOp1^Op2
            //target[5]=new int[]{1,2};//PrevOp2^Op1
            //target[6]=new int[]{1,3};//PrevOp2^Op2
            //target[7]=new int[]{-1,1};//ALUoutBus=PrevOp2^result
            //target[8]=new int[]{2,3};//Op1^Op2
            //target[9]=new int[]{-1};//result
            int[][] target = new int[1][];
            target[0] = new int[] { 1, 3 };//PrevOp2^Op2
            for (int instr =1; instr < 12; instr++)
            {
                System.Console.WriteLine("Instr {0}:{1}", instr, instr_name[instr - 1]);
                TestOneInstr(samples, TraceNum, instr, target, instr_name[instr-1], TtestTarget);
            }

            System.Console.WriteLine("采集完成，关闭设备");
            CloseScope();
            CloseCryptoModule();

        }

        #region
        //低通滤波
        public void LowPassFilter(float[] trace, double LowPassLimit, double SamplingRate)
        {
            alglib.complex[] f = new alglib.complex[trace.Length];
            double[] padsig = new double[trace.Length];
            for (int i = 0; i < trace.Length; i++)
                padsig[i] = trace[i];

            //fft
            alglib.fftr1d(padsig, trace.Length, out f);
            //滤波向量

            double[] filter = new double[trace.Length];

            filter[0] = 1;
            for (int i = 1; i < trace.Length / 2; i++)
            {
                if ((i * SamplingRate / trace.Length < LowPassLimit))
                    filter[i] = 1;
                else
                    filter[i] = 0;
            }
            filter[trace.Length / 2] = 1;
            for (int i = trace.Length / 2 + 1; i < trace.Length; i++)
            {
                if ((trace.Length - i) * SamplingRate / trace.Length < LowPassLimit)
                    filter[i] = 1;
                else
                    filter[i] = 0;
            }
            //作用
            for (int i = 0; i < trace.Length; i++)
                f[i] = f[i] * filter[i];
            //ifft
            alglib.fftr1dinv(f, trace.Length, out padsig);
            //去0
            for (int i = 0; i < trace.Length; i++)
                trace[i] = (float)padsig[i];

        }
        //高通滤波
        public void HighPassFilter(float[] trace, double HighPassLimit, double SamplingRate)
        {
            alglib.complex[] f = new alglib.complex[trace.Length];
            double[] padsig = new double[trace.Length];
            for (int i = 0; i < trace.Length; i++)
                padsig[i] = trace[i];

            //fft
            alglib.fftr1d(padsig, trace.Length, out f);
            //滤波向量

            double[] filter = new double[trace.Length];

            //filter[0] = 1;
            for (int i = 1; i < trace.Length / 2; i++)
            {
                if ((i * SamplingRate / trace.Length > HighPassLimit))
                    filter[i] = 1;
                else
                    filter[i] = 0;
            }
            filter[trace.Length / 2] = 1;
            for (int i = trace.Length / 2 + 1; i < trace.Length; i++)
            {
                if ((trace.Length - i) * SamplingRate / trace.Length > HighPassLimit)
                    filter[i] = 1;
                else
                    filter[i] = 0;
            }
            //作用
            for (int i = 0; i < trace.Length; i++)
                f[i] = f[i] * filter[i];
            //ifft
            alglib.fftr1dinv(f, trace.Length, out padsig);
            //去0
            for (int i = 0; i < trace.Length; i++)
                trace[i] = (float)padsig[i];

        }
        //低通滤波
        public void BandPassFilter(float[] trace, double LowPassLimit, double HighPassLimit, double SamplingRate)
        {
            alglib.complex[] f = new alglib.complex[trace.Length];
            double[] padsig = new double[trace.Length];
            for (int i = 0; i < trace.Length; i++)
                padsig[i] = trace[i];

            //fft
            alglib.fftr1d(padsig, trace.Length, out f);
            //滤波向量

            double[] filter = new double[trace.Length];

            //filter[0] = 1;
            for (int i = 1; i < trace.Length / 2; i++)
            {
                if ((i * SamplingRate / trace.Length >= LowPassLimit) && (i * SamplingRate / trace.Length <= HighPassLimit))
                    filter[i] = 1;
                else
                    filter[i] = 0;
            }
            filter[trace.Length / 2] = 1;
            for (int i = trace.Length / 2 + 1; i < trace.Length; i++)
            {
                if (((trace.Length - i) * SamplingRate / trace.Length >= LowPassLimit) && (((trace.Length - i) * SamplingRate / trace.Length) <= HighPassLimit))
                    filter[i] = 1;
                else
                    filter[i] = 0;
            }
            //作用
            for (int i = 0; i < trace.Length; i++)
                f[i] = f[i] * filter[i];
            //ifft
            alglib.fftr1dinv(f, trace.Length, out padsig);
            //去0
            for (int i = 0; i < trace.Length; i++)
                trace[i] = (float)padsig[i];

        }
        //double[]-> ToFloat
        public void ToFloat(double[] t, float[] t1)
        {
            for (int i = 0; i < t.Length; i++)
                t1[i] = (float)t[i];
        }
        //float[]->double[] 
        public void ToDouble(double[] t, float[] t1)
        {
            for (int i = 0; i < t.Length; i++)
                t[i] = (double)t1[i];
        }
        //低通滤波
        public void LowPass(double LowPassLimit, double[] Trace, double SamplingRate)
        {
            float[] temp = new float[Trace.Length];
            ToFloat(Trace, temp);
            LowPassFilter(temp, LowPassLimit, SamplingRate);
            ToDouble(Trace, temp);
        }
        //高通滤波
        public void HighPass(double LowPassLimit, double[] Trace, double SamplingRate)
        {
            float[] temp = new float[Trace.Length];
            ToFloat(Trace, temp);
            HighPassFilter(temp, LowPassLimit, SamplingRate);
            ToDouble(Trace, temp);
        }
        //带通滤波
        public void BandPass(double LowPassLimit, double HighPassLimit, double SamplingRate, double[] Trace)
        {
            float[] temp = new float[Trace.Length];
            ToFloat(Trace, temp);
            BandPassFilter(temp, LowPassLimit, HighPassLimit, SamplingRate);
            ToDouble(Trace, temp);
        }
        #endregion
    }
}
