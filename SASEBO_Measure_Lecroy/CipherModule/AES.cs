using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
namespace SASEBO_Measure_Lecroy
{
    class AES
    {
        // AES S-Box
        private static uint[] S = {
            0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76,
            0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0,
            0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15,
            0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75,
            0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84,
            0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf,
            0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8,
            0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2,
            0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73,
            0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb,
            0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79,
            0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08,
            0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a,
            0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e,
            0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf,
            0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16
        };

        private static uint[] InvSub = // Inverse of S
        {
	        0x52, 0x09, 0x6a, 0xd5, 0x30, 0x36, 0xa5, 0x38, 0xbf, 0x40, 0xa3, 0x9e, 0x81, 0xf3, 0xd7, 0xfb,
	        0x7c, 0xe3, 0x39, 0x82, 0x9b, 0x2f, 0xff, 0x87, 0x34, 0x8e, 0x43, 0x44, 0xc4, 0xde, 0xe9, 0xcb,
	        0x54, 0x7b, 0x94, 0x32, 0xa6, 0xc2, 0x23, 0x3d, 0xee, 0x4c, 0x95, 0x0b, 0x42, 0xfa, 0xc3, 0x4e,
	        0x08, 0x2e, 0xa1, 0x66, 0x28, 0xd9, 0x24, 0xb2, 0x76, 0x5b, 0xa2, 0x49, 0x6d, 0x8b, 0xd1, 0x25,
	        0x72, 0xf8, 0xf6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xd4, 0xa4, 0x5c, 0xcc, 0x5d, 0x65, 0xb6, 0x92,
	        0x6c, 0x70, 0x48, 0x50, 0xfd, 0xed, 0xb9, 0xda, 0x5e, 0x15, 0x46, 0x57, 0xa7, 0x8d, 0x9d, 0x84,
	        0x90, 0xd8, 0xab, 0x00, 0x8c, 0xbc, 0xd3, 0x0a, 0xf7, 0xe4, 0x58, 0x05, 0xb8, 0xb3, 0x45, 0x06,
	        0xd0, 0x2c, 0x1e, 0x8f, 0xca, 0x3f, 0x0f, 0x02, 0xc1, 0xaf, 0xbd, 0x03, 0x01, 0x13, 0x8a, 0x6b,
	        0x3a, 0x91, 0x11, 0x41, 0x4f, 0x67, 0xdc, 0xea, 0x97, 0xf2, 0xcf, 0xce, 0xf0, 0xb4, 0xe6, 0x73,
	        0x96, 0xac, 0x74, 0x22, 0xe7, 0xad, 0x35, 0x85, 0xe2, 0xf9, 0x37, 0xe8, 0x1c, 0x75, 0xdf, 0x6e,
	        0x47, 0xf1, 0x1a, 0x71, 0x1d, 0x29, 0xc5, 0x89, 0x6f, 0xb7, 0x62, 0x0e, 0xaa, 0x18, 0xbe, 0x1b,
	        0xfc, 0x56, 0x3e, 0x4b, 0xc6, 0xd2, 0x79, 0x20, 0x9a, 0xdb, 0xc0, 0xfe, 0x78, 0xcd, 0x5a, 0xf4,
	        0x1f, 0xdd, 0xa8, 0x33, 0x88, 0x07, 0xc7, 0x31, 0xb1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xec, 0x5f,
	        0x60, 0x51, 0x7f, 0xa9, 0x19, 0xb5, 0x4a, 0x0d, 0x2d, 0xe5, 0x7a, 0x9f, 0x93, 0xc9, 0x9c, 0xef,
	        0xa0, 0xe0, 0x3b, 0x4d, 0xae, 0x2a, 0xf5, 0xb0, 0xc8, 0xeb, 0xbb, 0x3c, 0x83, 0x53, 0x99, 0x61,
	        0x17, 0x2b, 0x04, 0x7e, 0xba, 0x77, 0xd6, 0x26, 0xe1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0c, 0x7d
        };
        // InvShift
        public uint InvShift(uint x9)
        {
            switch (x9) // and returns x10
            {
                case 0: return 0;
                case 5: return 1;
                case 10: return 2;
                case 15: return 3;
                case 4: return 4;
                case 9: return 5;
                case 14: return 6;
                case 3: return 7;
                case 8: return 8;
                case 13: return 9;
                case 2: return 10;
                case 7: return 11;
                case 12: return 12;
                case 1: return 13;
                case 6: return 14;
                case 11: return 15;
            }
            return 0;
        }
        // Shift
        public uint Shift(uint x9)
        {
            switch (x9) // and returns x10
            {
                case 0: return 0;
                case 1: return 5;
                case 2: return 10;
                case 3: return 15;
                case 4: return 4;
                case 5: return 9;
                case 6: return 14;
                case 7: return 3;
                case 8: return 8;
                case 9: return 13;
                case 10: return 2;
                case 11: return 7;
                case 12: return 12;
                case 13: return 1;
                case 14: return 6;
                case 15: return 11;
            }
            return 0;
        }
        //InvShiftRow
        private void InvShiftRow(byte[] state)
        {
            uint i;
            byte[] state1 = new byte[16];
            for (i = 0; i < 16; i++)
                state1[i] = state[InvShift(i)];
            for (i = 0; i < 16; i++)
                state[i] = state1[i];

        }
        //ShiftRow
        private void ShiftRow(byte[] state)
        {
            uint i;
            byte[] state1 = new byte[16];
            for (i = 0; i < 16; i++)
                state1[i] = state[Shift(i)];
            for (i = 0; i < 16; i++)
                state[i] = state1[i];

        }
        //AddRoundKey
        private void AddRoundKey(byte[] state, byte[] roundkey)
        {
            int i;
            for (i = 0; i < 16; i++)
                state[i] = (byte)(((uint)state[i]) ^ ((uint)roundkey[i]));
        }
        //InvSBox
        private void InvSbox(byte[] state)
        {
            int i;
            for (i = 0; i < 16; i++)
                state[i] = (byte)InvSub[(uint)state[i]];
        }
        //SBox
        private void Sbox(byte[] state)
        {
            int i;
            for (i = 0; i < state.Length; i++)
                state[i] = (byte)S[(uint)state[i]];
        }
        private byte xtime(byte t)
        {
            if ((uint)t > 127)
                return (byte)(((uint)t << 1) ^ (0x11b));
            else
                return (byte)((uint)t << 1);
        }
        private uint xmul(byte t, uint[] x)
        {
            uint result = 0;
            uint temp = (uint)t;
            for (int i = 7; i > -1; i--)
            {
                if (x[i] == 1)
                    result ^= (uint)temp;
                temp = xtime((byte)temp);
            }
            return (byte)result;
        }
        //InvMixColumn
        private void InvMixColumn(byte[] state)
        {
            uint[] mul9 = { 0, 0, 0, 0, 1, 0, 0, 1 };
            uint[] mulb = { 0, 0, 0, 0, 1, 0, 1, 1 };
            uint[] muld = { 0, 0, 0, 0, 1, 1, 0, 1 };
            uint[] mule = { 0, 0, 0, 0, 1, 1, 1, 0 };
            int i, j;
            byte[] temp = new byte[16];

            for (j = 0; j < 4; j++)
            {
                temp[4 * j] = (byte)(xmul(state[4 * j], mule) ^ xmul(state[1 + 4 * j], mulb) ^ xmul(state[2 + 4 * j], muld) ^ xmul(state[3 + 4 * j], mul9));
                temp[1 + 4 * j] = (byte)(xmul(state[4 * j], mul9) ^ xmul(state[1 + 4 * j], mule) ^ xmul(state[2 + 4 * j], mulb) ^ xmul(state[3 + 4 * j], muld));
                temp[2 + 4 * j] = (byte)(xmul(state[4 * j], muld) ^ xmul(state[1 + 4 * j], mul9) ^ xmul(state[2 + 4 * j], mule) ^ xmul(state[3 + 4 * j], mulb));
                temp[3 + 4 * j] = (byte)(xmul(state[4 * j], mulb) ^ xmul(state[1 + 4 * j], muld) ^ xmul(state[2 + 4 * j], mul9) ^ xmul(state[3 + 4 * j], mule));
            }
            for (i = 0; i < 16; i++)
                state[i] = temp[i];
        }
        //InvMixColumn
        private void MixColumn(byte[] state)
        {
            uint[] mul2 = { 0, 0, 0, 0, 0, 0, 1, 0 };
            uint[] mul3 = { 0, 0, 0, 0, 0, 0, 1, 1 };

            int i, j;
            byte[] temp = new byte[16];

            for (j = 0; j < 4; j++)
            {
                temp[4 * j] = (byte)(xmul(state[4 * j], mul2) ^ xmul(state[1 + 4 * j], mul3) ^ state[2 + 4 * j] ^ state[3 + 4 * j]);
                temp[1 + 4 * j] = (byte)(state[4 * j] ^ xmul(state[1 + 4 * j], mul2) ^ xmul(state[2 + 4 * j], mul3) ^ state[3 + 4 * j]);
                temp[2 + 4 * j] = (byte)(state[4 * j] ^ state[1 + 4 * j] ^ xmul(state[2 + 4 * j], mul2) ^ xmul(state[3 + 4 * j], mul3));
                temp[3 + 4 * j] = (byte)(xmul(state[4 * j], mul3) ^ state[1 + 4 * j] ^ state[2 + 4 * j] ^ xmul(state[3 + 4 * j], mul2));
            }
            for (i = 0; i < 16; i++)
                state[i] = temp[i];
        }
        //InvOneRound
        public void InvOneRound(byte[] state, byte[] correct_key, int Roundno)
        {
            byte[] correct_subkey = new byte[16];
            AES_key_schedule(correct_key, correct_subkey, Roundno);
            AddRoundKey(state, correct_subkey);
            if (Roundno != 10)
                InvMixColumn(state);
            InvShiftRow(state);
            InvSbox(state);
        }

        //InvOneRound
        public void InvAllRound(byte[] state, byte[] correct_key)
        {

            for (int i = 10; i > 0; i--)
            {
                InvOneRound(state, correct_key, i);
            }
            AddRoundKey(state, correct_key);

        }
        //InvLastRound
        public void InvLastRound(byte[] state, byte[] correct_key)
        {
            byte[] correct_subkey = new byte[16];
            AES_key_schedule(correct_key, correct_subkey, 10);
            AddRoundKey(state, correct_subkey);
            InvShiftRow(state);
            InvSbox(state);
        }
        //InvLastRound
        public void InvLastRoundWithoutS(byte[] state, byte[] correct_key)
        {
            byte[] correct_subkey = new byte[16];
            AES_key_schedule(correct_key, correct_subkey, 10);
            AddRoundKey(state, correct_subkey);
            InvShiftRow(state);
        }
        // AES Rcon values
        private static uint[] Rcon = {
            0x01000000U, 0x02000000U, 0x04000000U, 0x08000000U, 0x10000000U, 
            0x20000000U, 0x40000000U, 0x80000000U, 0x1B000000U, 0x36000000U };

        // AES rotate word function
        private static uint AES_rot_word(uint value)
        {
            uint result;

            result = (value << 8) | (value >> 24);

            return result;
        }


        // AES S Box function
        private static uint AES_sub_word(uint value)
        {
            uint result;

            result = ((uint)(S[((value & 0xFF000000U) >> 24)] << 24)) |
                ((uint)(S[((value & 0x00FF0000U) >> 16)] << 16)) |
                ((uint)(S[((value & 0x0000FF00U) >> 8)] << 8)) |
                ((uint)(S[(value & 0x000000FFU)]));

            return result;
        }


        // AES key schedule. Computes the correct subkey from the selected round number.
        /*
         * 输入： byte[] correct_key: 原始密钥
         *        subkey_num:         指定要输出的轮密钥轮数
         * 输出： byte[] correct_subkey: 对应轮数的轮密钥输出
         */
        public static void AES_key_schedule(byte[] correct_key, byte[] correct_subkey, int subkey_num)
        {
            uint[] key = new uint[4];

            key[0] = (((uint)correct_key[0]) << 24) |
                (((uint)correct_key[1]) << 16) |
                (((uint)correct_key[2]) << 8) |
                ((uint)correct_key[3]);
            key[1] = (((uint)correct_key[4]) << 24) |
                (((uint)correct_key[5]) << 16) |
                (((uint)correct_key[6]) << 8) |
                ((uint)correct_key[7]);
            key[2] = (((uint)correct_key[8]) << 24) |
                (((uint)correct_key[9]) << 16) |
                (((uint)correct_key[10]) << 8) |
                ((uint)correct_key[11]);
            key[3] = (((uint)correct_key[12]) << 24) |
                (((uint)correct_key[13]) << 16) |
                (((uint)correct_key[14]) << 8) |
                ((uint)correct_key[15]);

            for (int index = 0; index < subkey_num; index++)
            {
                key[0] ^= AES_sub_word(AES_rot_word(key[3])) ^ Rcon[index];
                key[1] ^= key[0];
                key[2] ^= key[1];
                key[3] ^= key[2];
            }

            correct_subkey[0] = (byte)((key[0] & 0xFF000000U) >> 24);
            correct_subkey[1] = (byte)((key[0] & 0x00FF0000U) >> 16);
            correct_subkey[2] = (byte)((key[0] & 0x0000FF00U) >> 8);
            correct_subkey[3] = (byte)((key[0] & 0x000000FFU));
            correct_subkey[4] = (byte)((key[1] & 0xFF000000U) >> 24);
            correct_subkey[5] = (byte)((key[1] & 0x00FF0000U) >> 16);
            correct_subkey[6] = (byte)((key[1] & 0x0000FF00U) >> 8);
            correct_subkey[7] = (byte)((key[1] & 0x000000FFU));
            correct_subkey[8] = (byte)((key[2] & 0xFF000000U) >> 24);
            correct_subkey[9] = (byte)((key[2] & 0x00FF0000U) >> 16);
            correct_subkey[10] = (byte)((key[2] & 0x0000FF00U) >> 8);
            correct_subkey[11] = (byte)((key[2] & 0x000000FFU));
            correct_subkey[12] = (byte)((key[3] & 0xFF000000U) >> 24);
            correct_subkey[13] = (byte)((key[3] & 0x00FF0000U) >> 16);
            correct_subkey[14] = (byte)((key[3] & 0x0000FF00U) >> 8);
            correct_subkey[15] = (byte)((key[3] & 0x000000FFU));
        }


        //AES Encrypt
        public byte[] AES_Encrypt(byte[] plain, byte[] key)
        {
            byte[] state = new byte[plain.Length];
            byte[] roundkey = new byte[plain.Length];
            Array.Copy(plain, state, plain.Length);

            AddRoundKey(state, key);

            for (int i = 0; i < 10; i++)
            {
                Sbox(state);
                ShiftRow(state);
                if (i != 9)
                    MixColumn(state);
                AES_key_schedule(key, roundkey, i + 1);
                AddRoundKey(state, roundkey);
            }
            return state;
        }
        //获取第rno轮的S盒输入
        public byte[] GetSin_Correct(byte[] plain, byte[] key, int rno)
        {
            byte[] state = new byte[plain.Length];
            byte[] roundkey = new byte[plain.Length];
            Array.Copy(plain, state, plain.Length);

            AddRoundKey(state, key);

            for (int i = 0; i < rno - 1; i++)
            {
                Sbox(state);
                ShiftRow(state);
                if (i != 9)
                    MixColumn(state);
                AES_key_schedule(key, roundkey, i + 1);
                AddRoundKey(state, roundkey);
            }
            return state;
        }
        //获取第rno轮的S盒输入
        public byte[] GetSOut_Correct(byte[] plain, byte[] key, int rno)
        {
            byte[] state = new byte[plain.Length];
            byte[] roundkey = new byte[plain.Length];
            Array.Copy(plain, state, plain.Length);

            AddRoundKey(state, key);

            for (int i = 0; i < rno - 1; i++)
            {
                Sbox(state);
                ShiftRow(state);
                if (i != 9)
                    MixColumn(state);
                AES_key_schedule(key, roundkey, i + 1);
                AddRoundKey(state, roundkey);
            }
            Sbox(state);
            return state;
        }
        //获取第rno轮的S盒输入
        public byte[] GetMCOut_Correct(byte[] plain, byte[] key, int rno)
        {
            byte[] state = new byte[plain.Length];
            byte[] roundkey = new byte[plain.Length];
            Array.Copy(plain, state, plain.Length);

            AddRoundKey(state, key);

            for (int i = 0; i < rno - 1; i++)
            {
                Sbox(state);
                ShiftRow(state);
                if (i != 9)
                    MixColumn(state);
                AES_key_schedule(key, roundkey, i + 1);
                AddRoundKey(state, roundkey);
            }
            Sbox(state);
            ShiftRow(state);
            MixColumn(state);
            return state;
        }
        //获取第rno轮的S盒输入
        public byte[] GetSROut_Correct(byte[] plain, byte[] key, int rno)
        {
            byte[] state = new byte[plain.Length];
            byte[] roundkey = new byte[plain.Length];
            Array.Copy(plain, state, plain.Length);

            AddRoundKey(state, key);

            for (int i = 0; i < rno - 1; i++)
            {
                Sbox(state);
                ShiftRow(state);
                if (i != 9)
                    MixColumn(state);
                AES_key_schedule(key, roundkey, i + 1);
                AddRoundKey(state, roundkey);
            }
            Sbox(state);
            ShiftRow(state);
            return state;
        }
        //获取第rno-1轮到第rno轮的S盒输入HD
        public byte[] GetSin_HD_Correct(byte[] plain, byte[] key, int rno)
        {
            byte[] laststate = new byte[plain.Length];
            byte[] state = new byte[plain.Length];
            byte[] roundkey = new byte[plain.Length];
            Array.Copy(plain, state, plain.Length);

            AddRoundKey(state, key);

            for (int i = 0; i < rno - 1; i++)
            {
                Array.Copy(state, laststate, state.Length);
                Sbox(state);
                ShiftRow(state);
                if (i != 9)
                    MixColumn(state);
                AES_key_schedule(key, roundkey, i + 1);
                AddRoundKey(state, roundkey);
            }
            for (int i = 0; i < state.Length; i++)
                state[i] = (byte)(state[i] ^ laststate[i]);
            return state;
        }
        public void WriteS(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);

            for (int i = 0; i < S.Length; i++)
                sw.Write("{0},", S[i]);
            sw.Close();
            fs.Close();

        }
    }
}
