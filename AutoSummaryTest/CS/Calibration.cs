using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;

namespace AutoSummaryTest
{
    class Calibration
    {
        private WECAnAPI.WECAnAPI WECAn;
        private string Mode;

        public Calibration(string mode= "sentence2vec")
        {
            this.Mode = mode;          
            WECAn = new WECAnAPI.WECAnAPI();
        }

        /* 測試的主程序 */
        public void ProcessTest(string system_path, string book_path, string book_name, double multiple)
        {
            AutoSummary(system_path, book_path, book_name, 1, multiple);    //做機器自動摘要
        }

        /* 傳入：
         *      string system_path      當前系統的路徑
         *      string book_path        要讀取的書籍其存放的路徑
         *      string book_name        當前要做的書籍的書籍名稱
         *      int seg_type            切割句子的方法，0=新方法  1=舊方法
         * 簡易介紹：
         *      產生機器自動摘要
         */
        public void AutoSummary(string system_path, string book_path, string book_name, int seg_type, double multiple)
        {                                                                                             //取摘要範圍(之後會是 Multiple * 100%)
            Opeating book_opeating = new Opeating(system_path);//路徑存起來

            string file_path = system_path + @"Document\" + book_name;             //這本書的儲存路徑 (SystemPath\書名)

            //檔案路徑處理，要先弄一些檔案路徑的問題
            try { System.IO.Directory.Delete(file_path, true); }                             //先試著刪除資料夾，因為可能已經有做過機器摘要了
            catch { }
            finally { Thread.Sleep(1000); }                                                  //因為有時會有刪除後卻沒建立資料夾的問題，所以這邊延遲1秒來讓程式不要跑太快
            System.IO.Directory.CreateDirectory(file_path);                                  //上面把資料刪除掉了，所以這邊再建新的資料夾
            //postedFile.SaveAs(file_path + $@"\{book_name}.txt");                             //先把使用者上傳的書本內容存檔 這個是SummaryWEB用的，這邊不能用，所以註解
            //取得我們要的書的資料，GetFiles會回傳一個array(找到的所有檔案路徑)，我們選第0個，因為有指定了
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(System.IO.Directory.GetFiles(book_path, book_name + ".txt")[0]);
            //複製到我們會放結果的地方，方便看
            fileInfo.CopyTo(file_path + "\\" + fileInfo.Name);

            //載入書籍內容
            List<string> book_data = new List<string>();                                     //存書的內容的變數
            int word_count = book_opeating.LoadBook(ref book_data, book_name);               //先讀取書籍
                                                    //指標
            //判斷長短文本
            //短文本用句子做2vector;長文本用段落做2vector
            //ex:小說用一段講一件事，詩詞用一句就講完了。
            //所以，以意義作2vector的話，小說(長文本)一段才夠表達，而詩詞(短文本)一句就夠。
            Console.WriteLine("averageWordsInOneParagraph = {0}", (word_count / book_data.Count));
            if (word_count / book_data.Count < 60)
                Console.WriteLine("{0} 是 短文本", book_name);
            else
                Console.WriteLine("{0} 是 長文本", book_name);

            //取得原書分段
            book_opeating.GetOgBlocking(book_name, file_path + "\\" +book_name +"原分段.txt");


            if (word_count / book_data.Count < 60)                                           //如果屬於短文本的話
            {
                //book_opeating.LoadBookToNotNormalized(ref book_data, book_name);             //重讀一次書本內容，這邊不做額外處理的讀取，直接從頭讀到尾。上面要去除標題是因為判斷文本是依據每段有多少字
                book_opeating.ToOneParagraph(ref book_data);                                 //那就把書的內容整理成1個段落
                book_opeating.SegmentToSentence(ref book_data, seg_type);                    //接著拆成句子
            }
            Console.WriteLine("inputData.length = {0}", book_data.Count());
            //取得語意向量
            List<string[]> book_vector = new List<string[]>();                                                      //存語意向量的變數
            //book_opeating.GetBookDataVector(ref book_vector, book_data, HP, 1, book_name, this.Mode);              //取得book_data對應的語意向量，讀取到book_vector裡
            book_opeating.GetBookDataVector(ref book_vector, book_data, WECAn, 1, book_name, this.Mode);

            Console.WriteLine("inputData.book_vecter.count = {0}",book_vector.Count());
            //區塊分割
            BlockCohesion block_cohesion = new BlockCohesion(); //中斷點檢查Vector是不是跟Block量一致                                                //準備做區塊分割
            block_cohesion.Process(ref book_data, ref book_vector, 8, 1.5, 7, file_path + "\\");    //做區塊分割
                         //ref book_data, ref book_vector, 8, 1.5, 7, file_path + "\\"              //Mutiple值越高，Block 越大 OG:2.5 Best:7
            Console.WriteLine("區塊分割結束");

            //判斷長短文本
            //文本區段分割後才分句子
           
            if (word_count / book_data.Count >= 60)                                          //如果屬於長文本的話
            {
                book_opeating.SegmentToSentence(ref book_data, seg_type);                    //將各段落拆成句子
                book_opeating.SaveDataHaveCount(book_data, file_path + "\\block_sentence.txt"); //儲存上面結果，不過現在只有BlockVector，所以要再取一次每個句子的

                Console.WriteLine("賦予每個句子向量(長文本)");
                book_opeating.GetBookDataVector(ref book_vector, book_data, WECAn, 1, book_name, this.Mode);          //取得book_data對應的語意向量，讀取到book_vector裡

                //因為先區塊分割後才來做拆分句子，導致長文本再度重新訓練的sentence vector會有空白行的存在，所以這裡要挑掉空白行的vector
                for (int i = 0; i < book_data.Count; i++)
                {
                    if (book_data[i].Length == 0)
                        book_vector[i] = new string[0];
                }
            }
            //取得機器摘要
            List<string> auto_summary_data = new List<string>();                                //儲存機器摘要的變數
            List<string[]> summary_vec = new List<string[]>();                                  //儲存機器摘要的Vector
            List<string[]> blockInfo = new List<string[]>();                                    //儲存格式 [num(句數),blockCount,Percentage,rank]
            AutoSummary auto_summary = new AutoSummary();                                       //準備做機器自動摘要
           
            auto_summary.Process(ref auto_summary_data, ref summary_vec,ref blockInfo, book_data, book_vector, multiple);        //做機器自動摘要  multiple是要取前n%的句子來當摘要句
            Console.WriteLine("auto_summary_data.Length = " + auto_summary_data.Count().ToString());

            //SaveStuff========================================================================================
            auto_summary.SaveAutoSummary(auto_summary_data, book_data, file_path + "\\");    //儲存機器自動摘要
            book_opeating.SaveBookVector(book_vector, file_path + "\\BookVector.txt","txt");         //整本

            Console.WriteLine("準備存下 {0} 的摘要句 Vector", book_name);
            book_opeating.SaveBookVector(summary_vec, file_path + "\\summary_vec.txt.vec","vec");

            Console.WriteLine("準備存下 {0} 的BlockInfo(BlockNum start from 0) ", book_name);
            blockInfo.Sort((v1, v2) => -int.Parse(v2[0]).CompareTo(int.Parse(v1[0])));                             //(照句數排，小到大)
            blockInfo.Insert(0,new string[] {"格式 : 句數 BlockNumber Percentage Ranking"});
            book_opeating.SaveBookVector(blockInfo, file_path + "\\BlockInfo.txt", "txt");
        }  
    }
}
