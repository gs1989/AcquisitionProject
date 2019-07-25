using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
namespace SASEBO_Measure_Lecroy
{
    class UnivariateTtest
    {
        public int samples=0;
        public double[] u_fix=null;
        public double[][] CS_fix=null;
        public double num_fix=0;
        public double[] u_random=null;
        public double[][] CS_random=null;
        public double num_random = 0;

        public UnivariateTtest(int s)
        {
            samples = s;
            u_fix=new double[s];
            u_random = new double[s];
            num_fix = 0;
            num_random = 0;
            CS_fix=new double[7][];
            CS_random = new double[7][];
            for (int i = 0; i < 7; i++)
            {
                CS_fix[i]=new double[samples];
                CS_random[i] = new double[samples];
                for (int j = 0; j < samples; j++)
                {
                    CS_fix[i][j] = 0;
                    CS_random[i][j] = 0;
                }
            }
        }
        public int choose(int n,int k)
        {
            int ntok = 1;
            int ktok = 1;
            if (k >= 0 && k <= n)
            {
                for (int t = 1; t < Math.Min(k, n - k) + 1; t++)
                {
                    ntok *= n;
                    ktok *= t;
                    n -= 1;
                }
                return ntok;
            }
            else
                return 0;
        }

        public double[] Add(double[] s1,double[] s2)
        {
             double[] result=new double[s1.Length];
            for(int i=0;i<s1.Length;i++)
                result[i]=s1[i]+s2[i];
            return result;
        }
        public double[] Sub(double[] s1,double[] s2)
        {
             double[] result=new double[s1.Length];
            for(int i=0;i<s1.Length;i++)
                result[i]=s1[i]-s2[i];
            return result;
        }
        public double[] Mul(double[] s1, double[] s2)
        {
            double[] result = new double[s1.Length];
            for (int i = 0; i < s1.Length; i++)
                result[i] = s1[i] * s2[i];
            return result;
        }
        public double[] Mul(double[] s1, double s2)
        {
            double[] result = new double[s1.Length];
            for (int i = 0; i < s1.Length; i++)
                result[i] = s1[i] * s2;
            return result;
        }
        public double[] Div(double[] s1, double[] s2)
        {
            double[] result = new double[s1.Length];
            for (int i = 0; i < s1.Length; i++)
                result[i] = s1[i] /s2[i];
            return result;
        }
        public double[] Div(double[] s1,double s2)
        {
             double[] result=new double[s1.Length];
            for(int i=0;i<s1.Length;i++)
                result[i]=s1[i]/s2;
            return result;
        }
        public double[] Pow(double[] s1, int k)
        {
            double[] result = new double[s1.Length];
            for (int i = 0; i < s1.Length; i++)
                result[i] = Math.Pow(s1[i],k);
            return result;
        }
        public double[] Sqrt(double[] s1)
        {
            double[] result = new double[s1.Length];
            for (int i = 0; i < s1.Length; i++)
                result[i] = Math.Sqrt(s1[i]);
            return result;
        }
        public void UpdateTrace(double[] trace, bool flag)
        {
            if (flag)
            {
                //Update n
                num_fix = num_fix + 1;
                //Update mean
                double[] delta = Sub(trace, u_fix);
                u_fix = Add(u_fix, Div(delta, num_fix));
                if (num_fix < 2)
                    return;
                //Update CS
                double[][] tempCS = new double[7][];
                for (int i = 0; i < 7; i++)
                {
                    tempCS[i] = new double[samples];
                    Array.Copy(CS_fix[i], tempCS[i], samples);
                }
                for (int d = 2; d < 7; d++)
                {
                    for (int k = 1; k < d - 1; k++)
                    {
                        CS_fix[d] = Add(CS_fix[d], Mul(Mul(tempCS[d - k], choose(d, k)), Pow(Div(delta, -1 * num_fix), k)));
                    }
                    CS_fix[d] = Add(CS_fix[d], Mul(Pow(Mul(delta, (num_fix - 1) / num_fix), d), 1 - Math.Pow((1.0 / (1.0 - num_fix)), d - 1)));
                }
            }
            else
            {
                //Update n
                num_random = num_random + 1;
                //Update mean
                double[] delta = Sub(trace, u_random);
                u_random = Add(u_random, Div(delta, num_random));
                if (num_random < 2)
                    return;
                //Update CS
                double[][] tempCS = new double[7][];
                for (int i = 0; i < 7; i++)
                {
                    tempCS[i] = new double[samples];
                    Array.Copy(CS_random[i], tempCS[i], samples);
                }
                for (int d = 2; d < 7; d++)
                {
                    for (int k = 1; k < d - 1; k++)
                    {
                        CS_random[d] = Add(CS_random[d], Mul(Mul(tempCS[d - k], choose(d, k)), Pow(Div(delta, -1 * num_random), k)));
                    }
                    CS_random[d] = Add(CS_random[d], Mul(Pow(Mul(delta, (num_random - 1) / num_random), d), 1 - Math.Pow((1.0 / (1.0 - num_random)), d - 1)));
                }
            }
        }

        public double WriteTTrace(string filename, int d)
        {
               FileStream fs = new FileStream(filename,FileMode.Create);
               StreamWriter sw=new StreamWriter(fs);
               double[] Tvalue=null;
               if(d==1)
               {
                   double[] e_fix=u_fix;
                   double[] e_random=u_random;
                   double[] var_fix=Div(CS_fix[2],num_fix);
                   double[] var_random=Div(CS_random[2],num_random);
                   Tvalue=Div(Sub(e_fix,e_random),Sqrt(Add(Div(var_fix,num_fix),Div(var_random,num_random))));
               }
               else
               {
                   if(d==2)
                   {
                       double[] e_fix=Div(CS_fix[2],num_fix);
                       double[] e_random=Div(CS_random[2],num_random);
                       double[] var_fix=Sub(Div(CS_fix[4],num_fix),Pow(Div(CS_fix[2],num_fix),2));
                       double[] var_random=Sub(Div(CS_random[4],num_random),Pow(Div(CS_random[2],num_random),2));
                       Tvalue=Div(Sub(e_fix,e_random),Sqrt(Add(Div(var_fix,num_fix),Div(var_random,num_random))));
                   }
                   else
                   {
                       if(d==3)
                       {
                           double[] e_fix=Div(Div(CS_fix[3],num_fix),Pow(Sqrt(Div(CS_fix[2],num_fix)),3));
                           double[] e_random=Div(Div(CS_random[3],num_random),Pow(Sqrt(Div(CS_random[2],num_random)),3));
                           double[] var_fix=Div(Sub(Div(CS_fix[6],num_fix),Pow(Div(CS_fix[3],num_fix),2)),Pow(Div(CS_fix[2],num_fix),3));
                           double[] var_random = Div(Sub(Div(CS_random[6], num_random), Pow(Div(CS_random[3], num_random), 2)), Pow(Div(CS_random[2], num_random), 3));
                           Tvalue=Div(Sub(e_fix,e_random),Sqrt(Add(Div(var_fix,num_fix),Div(var_random,num_random))));
                         }
                   }
               }
            double maxt=0;
               for (int i = 0; i < Tvalue.Length; i++)
               {
                   sw.WriteLine("{0}", Tvalue[i]);
                   if (Math.Abs(Tvalue[i]) > maxt)
                       maxt=Math.Abs(Tvalue[i]) ;
               }
            if(maxt>4.5)
               System.Console.WriteLine("d={0}, Tvalue={1}", d, maxt);
            sw.Close();
            fs.Close();
            return maxt;
        }

        public double GetMaxT(int d)
        {
            double[] Tvalue = null;
            if (d == 1)
            {
                double[] e_fix = u_fix;
                double[] e_random = u_random;
                double[] var_fix = Div(CS_fix[2], num_fix);
                double[] var_random = Div(CS_random[2], num_random);
                Tvalue = Div(Sub(e_fix, e_random), Sqrt(Add(Div(var_fix, num_fix), Div(var_random, num_random))));
            }
            else
            {
                if (d == 2)
                {
                    double[] e_fix = Div(CS_fix[2], num_fix);
                    double[] e_random = Div(CS_random[2], num_random);
                    double[] var_fix = Sub(Div(CS_fix[4], num_fix), Pow(Div(CS_fix[2], num_fix), 2));
                    double[] var_random = Sub(Div(CS_random[4], num_random), Pow(Div(CS_random[2], num_random), 2));
                    Tvalue = Div(Sub(e_fix, e_random), Sqrt(Add(Div(var_fix, num_fix), Div(var_random, num_random))));
                }
                else
                {
                    if (d == 3)
                    {
                        double[] e_fix = Div(Div(CS_fix[3], num_fix), Pow(Sqrt(Div(CS_fix[2], num_fix)), 3));
                        double[] e_random = Div(Div(CS_random[3], num_random), Pow(Sqrt(Div(CS_random[2], num_random)), 3));
                        double[] var_fix = Div(Sub(Div(CS_fix[6], num_fix), Pow(Div(CS_fix[3], num_fix), 2)), Pow(Div(CS_fix[2], num_fix), 3));
                        double[] var_random = Div(Sub(Div(CS_random[6], num_random), Pow(Div(CS_random[3], num_random), 2)), Pow(Div(CS_random[2], num_random), 3));
                        Tvalue = Div(Sub(e_fix, e_random), Sqrt(Add(Div(var_fix, num_fix), Div(var_random, num_random))));
                    }
                }
            }
            double maxt = 0;
            for (int i = 0; i < Tvalue.Length; i++)
            {
                if (Math.Abs(Tvalue[i]) > maxt)
                    maxt = Math.Abs(Tvalue[i]);
            }

            return maxt;
        }
        public double WriteTTrace_Row(string filename, int d)
        {
            FileStream fs = new FileStream(filename, FileMode.Append);
            StreamWriter sw = new StreamWriter(fs);
            double[] Tvalue = null;
            if (d == 1)
            {
                double[] e_fix = u_fix;
                double[] e_random = u_random;
                double[] var_fix = Div(CS_fix[2], num_fix);
                double[] var_random = Div(CS_random[2], num_random);
                Tvalue = Div(Sub(e_fix, e_random), Sqrt(Add(Div(var_fix, num_fix), Div(var_random, num_random))));
            }
            else
            {
                if (d == 2)
                {
                    double[] e_fix = Div(CS_fix[2], num_fix);
                    double[] e_random = Div(CS_random[2], num_random);
                    double[] var_fix = Sub(Div(CS_fix[4], num_fix), Pow(Div(CS_fix[2], num_fix), 2));
                    double[] var_random = Sub(Div(CS_random[4], num_random), Pow(Div(CS_random[2], num_random), 2));
                    Tvalue = Div(Sub(e_fix, e_random), Sqrt(Add(Div(var_fix, num_fix), Div(var_random, num_random))));
                }
                else
                {
                    if (d == 3)
                    {
                        double[] e_fix = Div(Div(CS_fix[3], num_fix), Pow(Sqrt(Div(CS_fix[2], num_fix)), 3));
                        double[] e_random = Div(Div(CS_random[3], num_random), Pow(Sqrt(Div(CS_random[2], num_random)), 3));
                        double[] var_fix = Div(Sub(Div(CS_fix[6], num_fix), Pow(Div(CS_fix[3], num_fix), 2)), Pow(Div(CS_fix[2], num_fix), 3));
                        double[] var_random = Div(Sub(Div(CS_random[6], num_random), Pow(Div(CS_random[3], num_random), 2)), Pow(Div(CS_random[2], num_random), 3));
                        Tvalue = Div(Sub(e_fix, e_random), Sqrt(Add(Div(var_fix, num_fix), Div(var_random, num_random))));
                    }
                }
            }
            double maxt = 0;
            for (int i = 0; i < Tvalue.Length; i++)
            {
                sw.Write("{0}\t", Tvalue[i]);
                if (Math.Abs(Tvalue[i]) > maxt)
                    maxt = Math.Abs(Tvalue[i]);
            }
            sw.WriteLine("");
            if (maxt > 4.5)
                System.Console.WriteLine("d={0}, Tvalue={1}", d, maxt);
            sw.Close();
            fs.Close();
            return maxt;
        }
        
    }
}
