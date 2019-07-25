using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
namespace SASEBO_Measure_Lecroy
{
    class InspectorReader_int
    {
        /*
  * 基本信息
  */
        public string AlgName;//算法名
        public bool Enc;//加密/解密，为true时表示加密；否则表示解密
        public string filename;//trs文件名
        /*
         * header信息
         */
        public int TraceNum;//曲线数量（0x41)
        public int SampleNum;//每条曲线上采样点的数量（0x42)
        public byte SampleCoding;//采样编码（0x43）:bit 8-6恒为0，bit 5为0时表示整数；否则表示浮点。bit 4-0表示采样点占据的字节数(只有1，2,4是合法的取值)
        public int CryptoDataLen;//曲线中密码数据的长度（0x44），即每条trace开端部分保存的密码相关数据（明密文等）的字节长度
        public string TraceTitle;//曲线标题（0x45）
        public string GlobalTraceTitle;//全局曲线标题（0x46）
        public string Discription;//描述（0x47）
        public int Xoffset;//X轴偏移（0x48）
        public string XLabel;//X轴标识（0x49）
        public string YLabel;//Y轴标识（0x4A）
        public float XScale;//X轴放缩（0x4B）
        public float YScale = 1;//Y轴放缩（0x4C）
        public int TraceOffset;//曲线展示的offset（0x4D）
        public byte LogScale;//曲线的对数缩放（0x4E）

        public Boolean[] AlignedRemoved;//对齐时去除
        public int remove;//对齐中去除的数量
        /*
         * 读入数据
         */
        public int StartIndex;
        public int EndIndex;
        public byte[][] plaintext;
        public byte[][] ciphertext;
        public byte[] key;
        public double[][] Trace;
        public byte[][] XState;
        //构造函数，读取数据
        public InspectorReader_int(string FileNameIn, string AlgNameIn, bool EncIn, int StartIndexIn, int EndIndexIn, byte[] keyIn)
        {
            filename = FileNameIn;
            AlgName = AlgNameIn;
            Enc = EncIn;
            StartIndex = StartIndexIn;
            EndIndex = EndIndexIn;
            int len = keyIn.Length;
            key = new byte[len];
            for (int i = 0; i < len; i++)
                key[i] = keyIn[i];


            ReadFile();

        }
        //构造函数，读取部分数据
        public InspectorReader_int(string FileNameIn, string AlgNameIn, bool EncIn, int StartIndexIn, int EndIndexIn, byte[] keyIn, int traceNum)
        {
            filename = FileNameIn;
            AlgName = AlgNameIn;
            Enc = EncIn;
            StartIndex = StartIndexIn;
            EndIndex = EndIndexIn;
            int len = keyIn.Length;
            key = new byte[len];
            for (int i = 0; i < len; i++)
                key[i] = keyIn[i];

            ReadFile(traceNum);

        }
        //读取部分曲线
        public void ReadFile(int traceNum)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            if (fs == null)
            {
                System.Console.WriteLine("Cannot open trs file!\n");
                return;
            }
            //读取文件头
            //System.Console.WriteLine("Read TRS file head");
            //使用二进制读取
            BinaryReader br = new BinaryReader(fs);
            //读取头信息
            while (true)
            {
                byte mark = br.ReadByte();//读取标号
                byte len = br.ReadByte();//对应域长度
                if (mark == 0x5f)//结束标志
                    break;
                switch (mark)
                {
                    case 0x41: TraceNum = br.ReadInt32(); break;
                    case 0x42: SampleNum = br.ReadInt32(); break;
                    case 0x43: SampleCoding = br.ReadByte();
                        // if ((SampleCoding & 0x10) != 0x00)
                        // {
                        //    System.Console.WriteLine("采样点为浮点，请修改代码！");
                        //   return;
                        // }
                        break;
                    case 0x44: CryptoDataLen = (int)br.ReadInt16(); break;
                    case 0x45: char[] temp = new char[1];
                        temp[0] = (char)br.ReadByte();
                        TraceTitle = new string(temp);
                        break;
                    case 0x46: byte[] GT = new byte[len];
                        GT = br.ReadBytes(len);
                        GlobalTraceTitle = Encoding.ASCII.GetString(GT);
                        break;
                    case 0x47: byte[] DS = new byte[len];
                        DS = br.ReadBytes(len);
                        Discription = Encoding.ASCII.GetString(DS);
                        break;
                    case 0x48: Xoffset = br.ReadInt32();
                        break;
                    case 0x49: byte[] XL = new byte[len];
                        XL = br.ReadBytes(len);
                        XLabel = Encoding.ASCII.GetString(XL);
                        break;
                    case 0x4a: byte[] YL = new byte[len];
                        YL = br.ReadBytes(len);
                        YLabel = Encoding.ASCII.GetString(YL);
                        break;
                    case 0x4b: XScale = br.ReadSingle();
                        break;
                    case 0x4c: YScale = br.ReadSingle();
                        break;
                    case 0x4d: TraceOffset = br.ReadInt32();
                        break;
                    case 0x4e: LogScale = br.ReadByte();
                        break;
                }
            }
            //读取曲线数据
            //System.Console.WriteLine("Read trace date");
            //申请空间
            plaintext = new byte[traceNum][];
            ciphertext = new byte[traceNum][];
            Trace = new double[traceNum][];
            int traceLen = EndIndex - StartIndex;//曲线上选取的点数量
            int plen = CryptoDataLen / 2;//分组字节长度
            byte[] t1;
            byte[] t2;
            for (int i = 0; i < traceNum; i++)
            {
                //if (i % 1000 == 0)
                //    System.Console.WriteLine("Read trace{0}", i);
                plaintext[i] = new byte[plen];
                ciphertext[i] = new byte[plen];
                Trace[i] = new double[traceLen];
                //开始读取
                t1 = br.ReadBytes(plen);
                t2 = br.ReadBytes(plen);
                //根据Enc判断哪个是明文
                if (Enc)
                {
                    for (int j = 0; j < plen; j++)
                    {
                        plaintext[i][j] = t1[j];
                        ciphertext[i][j] = t2[j];
                    }
                }
                else
                {
                    for (int j = 0; j < plen; j++)
                    {
                        plaintext[i][j] = t2[j];
                        ciphertext[i][j] = t1[j];
                    }
                }
                //读取存取数据
                br.BaseStream.Seek(StartIndex * ((int)(SampleCoding & 0xf)), SeekOrigin.Current);
                //if (System.BitConverter.IsLittleEndian)
                //    System.Console.WriteLine("Little Endian!");
                //else
                //    System.Console.WriteLine("Big Endian!");
                for (int j = 0; j < traceLen; j++)
                {
                    //Trace[i][j] = (int)br.ReadByte();
                    int size = SampleCoding & 0x0f;
                    byte[] t = br.ReadBytes(size);
                    if (size == 2)
                        Trace[i][j] = System.BitConverter.ToInt16(t, 0);
                    else
                    {
                        if (size == 1)
                            Trace[i][j] = (t[0] >= 128) ? (int)(t[0] | 0xffffff00) : (int)(t[0]);
                        else
                            if (size == 4)
                                Trace[i][j] = System.BitConverter.ToInt32(t, 0);
                    }
                }
                br.BaseStream.Seek((SampleNum - EndIndex) * ((int)(SampleCoding & 0xf)), SeekOrigin.Current);
            }
            br.Close();
            fs.Close();
            //System.Console.WriteLine("Finish Reading");
            TraceNum = traceNum;
        }
        //读取
        public void ReadFile()
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            if (fs == null)
            {
                System.Console.WriteLine("Cannot open TRS file!\n");
                return;
            }
            //读取文件头
            System.Console.WriteLine("Reading TRS head");
            //使用二进制读取
            BinaryReader br = new BinaryReader(fs);
            //读取头信息
            while (true)
            {
                byte mark = br.ReadByte();//读取标号
                byte len = br.ReadByte();//对应域长度
                if (mark == 0x5f)//结束标志
                    break;
                switch (mark)
                {
                    case 0x41: TraceNum = br.ReadInt32(); break;
                    case 0x42: SampleNum = br.ReadInt32(); break;
                    case 0x43: SampleCoding = br.ReadByte();
                        if ((SampleCoding & 0x10) != 0x00)
                        {
                            System.Console.WriteLine("Sample Code suggests float,please revise your code!");
                            return;
                        }
                        break;
                    case 0x44: CryptoDataLen = (int)br.ReadInt16(); break;
                    case 0x45: char[] temp = new char[1];
                        temp[0] = (char)br.ReadByte();
                        TraceTitle = new string(temp);
                        break;
                    case 0x46: byte[] GT = new byte[len];
                        GT = br.ReadBytes(len);
                        GlobalTraceTitle = Encoding.ASCII.GetString(GT);
                        break;
                    case 0x47: byte[] DS = new byte[len];
                        DS = br.ReadBytes(len);
                        Discription = Encoding.ASCII.GetString(DS);
                        break;
                    case 0x48: Xoffset = br.ReadInt32();
                        break;
                    case 0x49: byte[] XL = new byte[len];
                        XL = br.ReadBytes(len);
                        XLabel = Encoding.ASCII.GetString(XL);
                        break;
                    case 0x4a: byte[] YL = new byte[len];
                        YL = br.ReadBytes(len);
                        YLabel = Encoding.ASCII.GetString(YL);
                        break;
                    case 0x4b: XScale = br.ReadSingle();
                        break;
                    case 0x4c: YScale = br.ReadSingle();
                        break;
                    case 0x4d: TraceOffset = br.ReadInt32();
                        break;
                    case 0x4e: LogScale = br.ReadByte();
                        break;
                }
            }
            //读取曲线数据
            System.Console.WriteLine("Read TRS data");
            //申请空间
            plaintext = new byte[TraceNum][];
            ciphertext = new byte[TraceNum][];
            Trace = new double[TraceNum][];
            int traceLen = EndIndex - StartIndex;//曲线上选取的点数量
            int plen = CryptoDataLen / 2;//分组字节长度
            byte[] t1;
            byte[] t2;
            for (int i = 0; i < TraceNum; i++)
            {
                //if(i%1000==0)
                //     System.Console.WriteLine("Reading trace {0}",i);
                plaintext[i] = new byte[plen];
                ciphertext[i] = new byte[plen];
                Trace[i] = new double[traceLen];
                //开始读取
                t1 = br.ReadBytes(plen);
                t2 = br.ReadBytes(plen);
                //根据Enc判断哪个是明文
                if (Enc)
                {
                    for (int j = 0; j < plen; j++)
                    {
                        plaintext[i][j] = t1[j];
                        ciphertext[i][j] = t2[j];
                    }
                }
                else
                {
                    for (int j = 0; j < plen; j++)
                    {
                        plaintext[i][j] = t2[j];
                        ciphertext[i][j] = t1[j];
                    }
                }
                //读取存取数据
                br.BaseStream.Seek(StartIndex * ((int)(SampleCoding & 0xf)), SeekOrigin.Current);
                for (int j = 0; j < traceLen; j++)
                {
                    //Trace[i][j] = (int)br.ReadByte();
                    int size = SampleCoding & 0x0f;
                    byte[] t = br.ReadBytes(size);
                    if (size == 2)
                        Trace[i][j] = System.BitConverter.ToInt16(t, 0);
                    else
                        if (size == 1)
                            Trace[i][j] = (t[0] >= 128) ? (int)(t[0] | 0xffffff00) : (int)(t[0]);
                    //  Trace[i][j] *= YScale;

                }
                br.BaseStream.Seek((SampleNum - EndIndex) * ((int)(SampleCoding & 0xf)), SeekOrigin.Current);
            }
            br.Close();
            fs.Close();
            System.Console.WriteLine("Finish Reading");
        }
        //写一条曲线
        public void WriteOneTrace(string outfile)
        {
            FileStream fs = new FileStream(outfile, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);

            for (int i = 0; i < Trace[0].Length; i++)
                sw.Write("{0}\t", Trace[0][i]);

            sw.Close();
            fs.Close();
        }

        //写出重采样后的曲线
        public void WriteReSampleFile(string outfile, int ReSampleLen)
        {
            //向上取整
            int newl = SampleNum / ReSampleLen;
            if (SampleNum % ReSampleLen != 0)
                newl++;

            FileStream fs = new FileStream(outfile, FileMode.Create);
            if (fs == null)
            {
                System.Console.WriteLine("Cannot open TRS file!\n");
                return;
            }
            //读取文件头
            System.Console.WriteLine("Open file head");
            //使用二进制读取
            BinaryWriter wr = new BinaryWriter(fs);

            //写TraceNum
            byte mark = 0x41;
            byte len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(TraceNum);
            //wr.Write(Validcount);
            //写SampleNum
            mark = 0x42;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(newl);
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


            float sum;
            int i, j;
            for (int t = 0; t < TraceNum; t++)
            {
                //写明文
                wr.Write(plaintext[t]);
                //写密文
                wr.Write(ciphertext[t]);
                //写出数据
                for (i = 0; i < newl; i++)
                {
                    sum = 0;
                    if (i != newl - 1)
                    {
                        for (j = 0; j < ReSampleLen; j++)
                            sum += (float)Trace[t][i * ReSampleLen + j];
                        sum = sum / ReSampleLen;
                    }
                    else
                    {
                        for (j = 0; i * ReSampleLen + j < SampleNum; j++)
                            sum += (float)Trace[t][i * ReSampleLen + j];
                        sum = sum / j;
                    }
                    wr.Write((Int16)sum);
                }
            }
            wr.Close();
            fs.Close();
            System.Console.WriteLine("Finish writing");
        }

        //明文作为对象的X状态
        public void GetXState_Plaintext()
        {
            XState = new byte[TraceNum][];
            //byte[] k1 = { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0xfe, 0xdc, 0xba, 0x98, 0x76, 0x54, 0x32, 0x10 };
            //AES a = new AES();
            //byte[] c = a.AES_Encrypt(plaintext[0], k1);
            for (int i = 0; i < TraceNum; i++)
            {
                XState[i] = new byte[plaintext[i].Length];
                for (int j = 0; j < plaintext[i].Length; j++)
                    XState[i][j] = plaintext[i][j];
            }
        }

        //中心化
        public void centeredTrace(double[] t)
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
                t[i] = t[i] - mean;
            }
        }

        //中心化
        public void CenterTraces()
        {
            for (int i = 0; i < TraceNum; i++)
                centeredTrace(Trace[i]);
        }

        //向左右移位，移出的部分用头尾填充
        //shift为正向右，为负向左
        public double[] Shift(double[] t, int shift)
        {
            double[] newt = new double[t.Length];
            int i = 0;
            if (shift >= 0)
            {
                for (i = 0; i + shift < t.Length; i++)
                {
                    newt[i + shift] = t[i];
                }
                for (i = 0; i < shift; i++)
                    newt[i] = t[0];
            }
            else
            {
                for (i = -shift; i < t.Length; i++)
                {
                    newt[i + shift] = t[i];
                }
                for (i = t.Length + shift; i < t.Length; i++)
                    newt[i] = t[t.Length - 1];
            }
            return newt;
        }


        //用相关系数对齐
        //对齐范围：[-range,range],取最大的相关系数
        public void CorrAlign(int basis, int range)
        {
            double[] basisT = Trace[basis];
            int i;
            double maxcorr = 0;
            int maxshift = 0;
            int remove = 0;
            AlignedRemoved = new Boolean[TraceNum];
            for (i = 1; i < TraceNum; i++)
            {
                //if (i % 10 == 0)
                System.Console.WriteLine("Align Trace {0}", i);

                maxcorr = 0;
                maxshift = 0;
                for (int shift = -range; shift <= range; shift++)
                {
                    double[] temp = Shift(Trace[i], shift);
                    double corr = alglib.pearsoncorr2(basisT, temp, basisT.Length);
                    if (corr > maxcorr)
                    {
                        maxcorr = corr;
                        maxshift = shift;
                    }
                }
                Trace[i] = Shift(Trace[i], maxshift);
                System.Console.WriteLine("Trace {0}: maxshift={1}, maxcorr={2}", i, maxshift, maxcorr);
                if (maxcorr < 0.8)
                {
                    System.Console.WriteLine("Trace {0}: maxshift={1}, maxcorr={2}", i, maxshift, maxcorr);
                    remove++;
                    AlignedRemoved[i] = true;
                }
            }


        }
        public void WriteFile(string outfile)
        {


            FileStream fs = new FileStream(outfile, FileMode.Create);
            if (fs == null)
            {
                System.Console.WriteLine("Cannot open output file！\n");
                return;
            }
            //读取文件头
            System.Console.WriteLine("Write file head");
            //使用二进制读取
            BinaryWriter wr = new BinaryWriter(fs);

            //写TraceNum
            byte mark = 0x41;
            byte len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(TraceNum - remove);
            //写SampleNum
            mark = 0x42;
            len = 4;
            wr.Write(mark);
            wr.Write(len);
            wr.Write(Trace[0].Length);
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

            for (int i = 0; i < TraceNum; i++)
            {
                // if (AlignedRemoved[i])
                //    continue;
                //写明文
                wr.Write(plaintext[i]);
                //写密文
                wr.Write(ciphertext[i]);
                //写出数据

                for (int k = 0; k < Trace[i].Length; k++)
                {
                    Int16 temp = (Int16)Trace[i][k];
                    byte[] t = System.BitConverter.GetBytes(temp);
                    wr.Write(t);
                }
            }

            wr.Close();
            fs.Close();
            System.Console.WriteLine("Finish Writing");
        }
    }
}
