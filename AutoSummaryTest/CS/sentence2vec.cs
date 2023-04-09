using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Threading;
using System.Text.RegularExpressions;
using System.Text;

namespace AutoSummaryTest
{
    public class Sentence2vec
    {
        private string system_path;

        public Sentence2vec(string system_path)
        {
            this.system_path = system_path;
        }

        
        public void LoadVector(ref List<string[]> vec_list)
        {
            LoadVector(ref vec_list, this.system_path + @"Sentence2vec/Sentence_Seg.txt.vec");
        }


        /* 傳入：
         *      List<string[]> vec_list         
         * 簡易介紹：
         *      載入用完python的語意向量。
         *      這邊雖然在win開檔起來看時會是同一行，但其實他是有用'\n'分行的，
         *      所以載入第一行會得到 (總共有幾句句子 語意向量的維度)，
         *      之後載入後面的行數就是語意向量，維度為dim+1，因為開頭會有標記是第幾句句子的文字，所以會+1。
         *      然後都是用" "來當作分割點，所以讀取都用split(" ")來分開即可得到向量或需要的資料。
         *      Sentence_Seg.txt.vec內容範例：  (在windows底下看不會是這樣顯示，但這樣我這樣打會比較好懂他到底存了甚麼東西，所以我自動分行跟多打出換行符號了)
         *          2 3\n
         *          sent_0 1.0 2.0 3.0\n
         *          sent_1 5.0 9.0 0.1\n
         */
        public void LoadVector(ref List<string[]> vec_list, string path)
        {
            vec_list = new List<string[]>();

            using (StreamReader sr = new StreamReader(path))
            {
                //讀取第一行，第一行是紀錄有多少句子以及多少維度  Str_temp[0]=句子數量  Str_temp[1]=向量維度
                string[] Str_temp = sr.ReadLine().Split(' ');                            //先取得句子數量 跟 向量維度

                //開始取得各向量的數字
                for (int j = 0; j < int.Parse(Str_temp[0]); j++)
                {
                    string[] vec_temp = sr.ReadLine().Split(' ');                        //讀取向量的數字
                    //留下數字(向量)
                    vec_temp = vec_temp.Where(val => val != vec_temp[0]).ToArray();      //刪除 Array 陣列中指定的元素，因為開頭是標示第幾句句子，所以需要刪除。這邊用的是linq的where，val!=標示句子的文字 時 就留下，然後toarray()轉成陣列
                    vec_list.Add(vec_temp);
                }
            }
        }


        /* 傳入：
         *      int type                input(0)=隱藏cmd
         *      string book_name        當前在做處理的書籍名稱
         * 簡易介紹：
         *      把文本做Sentence2vec。
         *      2019/10/28:新增固定句子向量方法，同時可以減少重複執行sentence2vec。
         */
        public void Run(int type)
        {
            //指定要寫入的路徑，並用big5編碼
            using (StreamWriter sw = new StreamWriter(system_path + @"Sentence2vec\AutoSentence2vec.bat", false, System.Text.Encoding.GetEncoding("big5")))
            {
                /* 若在win底下，且有python2 和 python3的版本，那須改成"py rum.py"
                 * 若只有單一個python2而已，則可以改"python rum.py" ，也可以不改，因為沒啥差。痾.....我覺得有差..會直接出問題
                 * 在rum.py的第一行已改#! python2，若在linux家族底下，則可以改自行安裝的python2的位置  該行所講內容詳情請參考：https://python.freelycode.com/contribution/detail/139
                */
                sw.WriteLine($@"cd {system_path}Sentence2vec");
                sw.WriteLine("py rum.py");          //用bat檔下執行python檔案指令
            }

            //IIS 7以上如何執行執行檔 exe, bat，參考網站:http://cattoncareer.blogspot.com/2015/06/iis-7-exe-bat.html
            ProcessStartInfo pInfo = new System.Diagnostics.ProcessStartInfo(system_path + @"Sentence2vec\AutoSentence2vec.bat");

            if (type == 0)  //隱藏cmd
            {
                pInfo.UseShellExecute = false;
                // 2019/08/05 原本只有上面那行，但因為不管怎麼執行都會錯誤，所以順手de了一下bug，測試後發現多增加下面那行即可
                pInfo.CreateNoWindow = true;
            }

            try
            {
                using (Process processExec = Process.Start(pInfo))              //目前我沒設權限(IIS_IUSRS)，如果IIS不能執行就試試權限，參考網站:https://dotblogs.com.tw/am940625/2016/02/15/140257
                {
                    processExec.WaitForExit();
                }
            }
            catch
            {
                //若要完整，這邊應該throw exception出去給外面的chtch，這樣程式才不會卡在這
            }
        }
    }
}