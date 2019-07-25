using System;
using System.Collections.Generic;
using System.Text;

namespace SASEBO_Measure_Lecroy.CipherModule
{
    //================================================================ CipherInterface
    public interface IBlockCipher
    {
        void open();
        void close();

        void setKey(byte[] key, int len);
        void setEnc();
        void setDec();
        void writeText(byte[] text, int len);
        void readText(byte[] text, int len);
        void execute();
    }
}
