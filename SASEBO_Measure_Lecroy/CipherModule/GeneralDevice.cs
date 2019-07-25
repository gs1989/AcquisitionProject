using System;
using System.Collections.Generic;
using System.Text;

namespace SASEBO_Measure_Lecroy.CipherModule
{
    class GeneralDevice
    {
        RS232 rs = null;
        public GeneralDevice(string portname)
        {
            rs = new RS232(portname);
            rs.open();
        }
        public void Initial(byte[] plain)
        {
            rs.write(plain, plain.Length);
            //rs.flush();
        }
        public void Send(byte[] plain)
        {
            rs.write(plain, plain.Length);
            byte[] result = new byte[1];
            rs.read(result, result.Length);
            //rs.flush();
        }
        public byte[] Encrypt(byte[] plain)
        {
            rs.write(plain, plain.Length);
            //rs.flush();
            byte[] result=new byte[plain.Length];
            rs.read(result, result.Length);
            return result;
        }
        public byte[] SendAReadBack(byte[] plain,int len)
        {
            rs.write(plain, plain.Length);
            //rs.flush();
            byte[] result = new byte[len];
            rs.read(result, result.Length);
            return result;
        }
        public byte[] Encrypt_Masked(byte[] plain,int len)
        {
            rs.write(plain, plain.Length);
            //rs.flush();
            byte[] result = new byte[len];
            rs.read(result, result.Length);
            return result;
        }
        //Randomized key as the first 16B of plaintext
        public byte[] Encrypt_RandomKey(byte[] plain)
        {
            rs.write(plain, plain.Length);
            //rs.flush();
            byte[] result = new byte[plain.Length/2];
            rs.read(result, result.Length);
            return result;
        }
        public void Close()
        {
            rs.close();
        }
    }
}
