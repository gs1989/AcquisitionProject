using System;
using System.Collections.Generic;
using System.Text;

namespace SASEBO_Measure_Lecroy
{
    class Program
    {
        static void Main(string[] args)
        {
            //byte[] key = { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0xfe, 0xdc, 0xba, 0x98, 0x76, 0x54, 0x32, 0x10 };
            byte[] key = { 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6, 0xab, 0xf7, 0x15, 0x88, 0x09, 0xcf, 0x4f, 0x3c };
            //byte[] key = { 0x2b, 0x7e, 0x15, 0x16, 0x28, 0xae, 0xd2, 0xa6 };
            
            ////int[] target = { 0,2 };
            //mea.MeasureTraces_RapidTtest_MaskedAES(key, 25625, 1000000, "COM12", 0, 500);
            //mea.MeasureTraces_PowerModel_Full("SCALE_PowerModel_5Instrs_Full_Pico_125kT_1000S.trs", key, 1000, 125000, "COM8", 250, 10);

            //string[] InstrName = { "NULL", "MOV", "MOVS", "LSL", "LSR", "ROR", "AND", "OR", "XOR", "LDR", "LDRB", "STR", "STRB" };
            //int samples = 600;
            //uint timebase = 1;//4ns
            //int shares = 4;
            //int traces = 100000;
            //int RapidRepeat = 100;
            //Measurement mea = new Measurement(samples, 16);
            //for (int instr = 3; instr >= 3; instr--)
            //{
            //    mea.MeasureTraces_BitInteraction_RapidRepeat("M3_" + InstrName[instr] + "_4shares600S_250MSa_8SameValue_DifferentShares_Rapid100.trs", samples, traces, "COM12", timebase, instr, InstrName[instr], shares, RapidRepeat);
            //}
            int samples = 600;
            int traces = 1000000;
            byte internal_repeat = 100 / 10;
            byte block_repeat = 8;
            uint timebase = 1;//4ns
            bool refresh = false;
            Measurement mea = new Measurement(samples, 16);
            mea.Ttest_BitInteraction("SCALE_4shares_M3_BitSlicedAND2_ThumbShiftLeft_REALAND2_BlockwiseSlicing_8SamePlaintexts_NoRefreshing_1M_Avg100.trs", samples, traces, "COM12", timebase, key, refresh, internal_repeat, block_repeat);
            ////mea.Ttest_Inspecting(key, 250, 50000, "COM5", 250);
            //int samples = 13000;
            //int traces = 1000000;
            //uint timebase = 1;//4ns;
            //Measurement mea = new Measurement(samples, 16);
            //mea.MeasureTraces_Ttest_MaskedAES(key, samples, traces, "COM11", 0,timebase);

            //CipherModule.GeneralDevice cipher_hw = new CipherModule.GeneralDevice("COM10");//指定当前硬件设备接口实例为SASEBO_G
            //////准备硬件设备
            //byte[] plain = new byte[32];
            //byte[] cipher = new byte[32];
            ////执行硬件加密
            //Random ra = new Random();
            //for (int instr = 0; instr < 125; instr++)
            //{
            //    ra.NextBytes(plain);
            //    plain[28] = (byte)instr;
            //    //mea.ZeroResult(plain, instr);
            //    cipher = cipher_hw.Encrypt(plain);
            //}
        }
    }
}
