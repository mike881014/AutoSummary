using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoSummaryTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string system_path = System.IO.Directory.GetCurrentDirectory().Replace(@"AutoSummaryTest\AutoSummaryTest\bin\Debug", @"RunTimeDatas\");
            string book_path = system_path + @"原書\";

            Calibration ca = new Calibration("sentence2vec");  //sentence2vec  bert   別用bert，因為效果沒很好，而且也沒再去研究，所以用sentence2vec就好
            //Console.WriteLine("請輸入書名 : ");
            string name = args[0];//Console.ReadLine();
            ca.ProcessTest(system_path, book_path, name, 0.1);
                                                       //0.1 = 取每個block前10%

            Console.WriteLine("press any key to continue....");
            Console.ReadKey();
        }
    }
}