using System;
using System.Collections.Generic;
using System.Text;

namespace SASEBO_Measure_Lecroy.CipherTool
{
    //================================================================ CipherInterface
    public interface IBlockCipher
    {
        void setKey(byte[] key);
        byte[] encrypt(byte[] pt);
        byte[] decrypt(byte[] ct);
    }
}
