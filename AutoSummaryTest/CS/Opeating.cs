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
    /* 這只是個工具libary
     * 一堆無關緊要、書籍操作等等的function都在這
     */
    public class Opeating
    {
        private string SystemPath;


        public Opeating(string system_path)
        {
            this.SystemPath = system_path;
        }

        #region Load file or Save file Function
        /* 傳入：
         *      List<T> data            內容    
         *      string path             儲存的路徑，應自行加入檔名(name.txt)
         * 簡易介紹：
         *      存data到指定路徑
         */
        public void SaveData<T>(List<T> data, string path)
        {
            using (StreamWriter SW = new StreamWriter(path))
            {
                foreach (T str in data)
                {
                    SW.WriteLine(str);
                }
            }
        }


        public void SaveDataHaveCount(List<string> data, string path)
        {
            using (StreamWriter sw = new StreamWriter(path))
            {
                int space_count = 0;  //空白行計數變數
                for (int i = 0; i < data.Count; i++)
                {
                    if (data[i].Length == 0) //如果是空白行
                    {
                        space_count++;
                        sw.WriteLine("");  //寫入空白行，但不加行數
                        continue;
                    }
                    sw.WriteLine((i + 1 - space_count) + "." + data[i]);
                }
            }
        }


        /* 傳入：
         *      List<string[]> data     句子向量    
         *      string path             儲存的路徑，應自行加入檔名(name.txt)
         *      mode                    txt,vec    看要哪一個
         * 簡易介紹：
         *      將句子向量存到指定路徑
         */
        public void SaveBookVector(List<string[]> data, string path, string mode)
        {
            if (mode == "txt")
            {
                using (StreamWriter SW = new StreamWriter(path))
                {
                    foreach (string[] str in data)
                    {
                        if (str.Length == 0)        //空白行也存一個空白行進去
                            SW.WriteLine("");
                        else
                            SW.WriteLine(str.Aggregate((sum, temp_str) => sum += $" {temp_str}"));      //用累加函式來儲存，這邊傳入lambda式來做累加的動作
                    }
                }

            }
            else if (mode == "vec")
            {
                using (StreamWriter SW = new StreamWriter(path))
                {
                    int i = 0;
                    SW.WriteLine(data.Count + " " + data[0].Length);
                    foreach (string[] str in data)
                    {
                        if (str.Length == 0)        //空白行也存一個空白行進去
                            SW.WriteLine("");
                        else
                            SW.WriteLine("sent_" + i + " " + str.Aggregate((sum, temp_str) => sum += $" {temp_str}"));
                        i++;
                    }
                }
            }
        }
        /// <讀原書分段>
        /// normal,abnormal
        /// 是為了判斷文章是不是用空白行當分段。
        /// 如果不是，那大多在讀入字串結尾會是 => 。」 ! ?
        /// 這時normal就會加一，反之如果是文字那abnormal就會加一
        /// 這樣判斷才比較不會把段落支解了(可能還是會有例外)
        /// </summary>
        public void GetOgBlocking(string book_name, string path)
        {
            List<string> data = new List<string>();
            List<string> tempData = new List<string>();
            int normal = 0;
            int abnormal = 0;
            string endStr = "。!?」…﹂";

            using (StreamReader SR = GetStramReader(SystemPath + $@"Document\{book_name}\{book_name}.txt"))
            {
                string str;
                var temp = "";
                while ((str = SR.ReadLine()) != null)
                {
                    /*學長用的錯誤讀法，會把段落切成碎片，長文本有機會被誤判成短文本
                    while ((str = SR.ReadLine()) != null)
                    {
                        str = str.Replace("\n", "").Replace("\r", "").Replace("\t", "").Replace(" ", "").Replace("　", "");  //先清除掉不要的字元("\n", "\r", "\t", 空白, 全型空白)
                        data.Add(str);
                    }
                    */
                    if (str != "\n" && str != "")
                    {   //排除掉小標題了(正文開始)，並累積字
                        if (str.IndexOf("。") != -1 || str.IndexOf("，") != -1 || str.IndexOf("！") != -1 || str.IndexOf("「") != -1 || str.IndexOf("」") != -1 || str.IndexOf("？") != -1 || str.IndexOf("…") != -1)
                        {
                            str = str.Replace("\n", "").Replace("\r", "").Replace("\t", "").Replace(" ", "").Replace("　", "");  //先清除掉不要的字元("\n", "\r", "\t", 空白, 全型空白)
                            if (endStr.IndexOf(str[str.Length - 1]) != -1)
                            {
                                normal++;
                            }
                            else
                            {
                                abnormal++;
                            }

                            tempData.Add(str);
                            temp += string.Concat(str);
                        }
                    }
                    else if (temp != "")//不要存空行
                    {
                        data.Add(temp);
                        data.Add("");
                        temp = "";
                    }
                }
                if (normal > abnormal)
                {
                    Console.WriteLine("normal {0} > abnormal {1}", normal, abnormal);
                    Console.WriteLine("句子完整，正常讀法", normal, abnormal);
                    data = tempData;
                }
                else
                {
                    Console.WriteLine("normal {0} < abnormal {1}", normal, abnormal);
                    Console.WriteLine("句子被切爛，用保護讀法", normal, abnormal);
                }
            }
            using (StreamWriter SW = new StreamWriter(path))
            {
                int i = 1;
                SW.WriteLine(book_name + "原段落");
                foreach (string str in data)
                {
                    //Console.WriteLine("Print : {0}", str);

                    if (str.Length == 0 || str == "\n" || str == "")
                    {
                        SW.WriteLine("");
                    }
                    else
                    {
                        SW.WriteLine(i + "." + str);
                        i++;
                    }
                }
            }
        }

        /* 傳入：
         *      List<string> data       用來儲存書本內容。該變數會在該function改變
         *      string path             書名(不用加入.txt)
         * 回傳：
         *      int word_count              該本書的總字數
         * 簡易介紹：
         *      讀書本內容到data裡，會依照書本內容來分段
         */
        public int LoadBookToNotNormalized(ref List<string> data, string book_name)
        {
            data.Clear();
            int word_count = 0;  //累計這本書的總字數的變數
            string str;
            var temp = "";
            using (StreamReader SR = GetStramReader(SystemPath + $@"Document\{book_name}\{book_name}.txt"))
            {
                while ((str = SR.ReadLine()) != null)
                {
                    if (str != "\n" && str != "")
                    {
                        str = str.Replace("\n", "").Replace("\r", "").Replace("\t", "").Replace(" ", "").Replace("　", "");  //先清除掉不要的字元("\n", "\r", "\t", 空白, 全型空白)
                        temp += string.Concat(str);
                    }
                    else if (temp != "")
                    {
                        data.Add(temp);
                        word_count += temp.Length;
                        temp = "";
                    }
                }
                /*
                string str;
                while ((str = SR.ReadLine()) != null)
                {
                    str = str.Replace("\n", "").Replace("\r", "").Replace("\t", "").Replace(" ", "").Replace("　", "");  //先清除掉不要的字元("\n", "\r", "\t", 空白, 全型空白)
                    data.Add(str);
                    word_count += str.Length;
                }*/
            }
            return word_count;
        }


        /* 傳入：
         *      List<string> data       用來儲存書本內容。該變數會在該function改變
         *      string path             書名(不用加入.txt)
         * 回傳：
         *      int word_count              該本書的總字數
         * 簡易介紹：
         *      讀書本內容到data裡，會依照書本內容來分段，這邊會多判斷每一行是否有存在一些標點符號，沒有則不加入data裡
         */
        public int LoadBook(ref List<string> data, string book_name)
        {
            data.Clear();
            int word_count = 0;  //累計這本書的總字數的變數
            List<string> tempData = new List<string>();
            int normal = 0;
            int abnormal = 0;
            using (StreamReader SR = GetStramReader(SystemPath + $@"Document\{book_name}\{book_name}.txt"))
            {
                string str;
                var temp = "";
                string endStr = "。!?」…﹂";

                while ((str = SR.ReadLine()) != null)
                {
                    if (str != "\n" && str != "")
                    {   //排除掉小標題了(正文開始)，並累積字數
                        if (str.IndexOf("。") != -1 || str.IndexOf("，") != -1 || str.IndexOf("！") != -1 || str.IndexOf("「") != -1 || str.IndexOf("」") != -1 || str.IndexOf("？") != -1 || str.IndexOf("…") != -1)
                        {
                            str = str.Replace("\n", "").Replace("\r", "").Replace("\t", "").Replace(" ", "").Replace("　", "");  //先清除掉不要的字元("\n", "\r", "\t", 空白, 全型空白)
                            if (endStr.IndexOf(str[str.Length - 1]) != -1)
                            {
                                normal++;
                            }
                            else
                            {
                                abnormal++;
                            }

                            tempData.Add(str);
                            temp += string.Concat(str);
                        }

                    }
                    else if (temp != "")//不要存空行
                    {
                        data.Add(temp);
                        word_count += temp.Length;
                        temp = "";
                    }

                    /*
                    str = str.Replace("\n", "").Replace("\r", "").Replace("\t", "").Replace(" ", "").Replace("　", "");  //先清除掉不要的字元("\n", "\r", "\t", 空白, 全型空白)
                                                                                                                        //接著要把內容儲存起來，但一本書內會有小標題、副標題等，那些是沒有以下那些標點符號的，而我們想把它去除掉，所以有了下面那個判斷式
                    if (str.IndexOf("。") != -1 || str.IndexOf("，") != -1 || str.IndexOf("！") != -1 || str.IndexOf("「") != -1 || str.IndexOf("」") != -1 || str.IndexOf("？") != -1 || str.IndexOf("…") != -1)
                    {
                        //排除掉小標題了(正文開始)，儲存內容(以行為單位)=>data，並累積字數
                        data.Add(str);
                        word_count += str.Length;
                    }*/
                }
                if (normal > abnormal)
                {
                    Console.WriteLine("normal {0} > abnormal {1}", normal, abnormal);
                    Console.WriteLine("句子完整，正常讀法", normal, abnormal);
                    data = tempData;
                }
                else
                {
                    Console.WriteLine("normal {0} < abnormal {1}", normal, abnormal);
                    Console.WriteLine("句子被切爛，用保護讀法", normal, abnormal);
                }
            }
            return word_count;
        }
        #endregion


        #region 斷詞系列Function
        /* 傳入：
         *      List<string> data           書本內容。通常用到這function時應該是已經做完區塊切割了，準備要做自動摘要前了，所以這data會有空白行，而空白行是當作區塊分割的點
         *      int type                    切割方式。0:使用新方法  1:使用舊方法
         * 簡易介紹：
         *      新方法：
         *      把書本內容拆成句子型態，主要會拆成2種型態
         *      型態1. 非說話句型(用句號當結尾)：今天天氣真好，好想要去玩水，所以我現在正在去海水浴場的路上，並且跟我同行的有我的朋友。
         *      型態2. 說話句型(用下引號當結尾)：我被打了，所以我很生氣的說：「你幹嘛打我？不要這樣。幹甚麼東西阿你。」  //注：下引號前一個不一定要是句點，其他標點符號也可
         *      
         *      舊方法：
         *      每個句子用句號來拆開，然後補上句號
         */
        public void SegmentToSentence(ref List<string> data, int type)
        {
            /* ((?:[\w]+[^。\w])*[\w]+：「(?:[\w]+[^\w])*」)
             *      想法：若是說話句型，則會是 (不是句號結尾的句子)：「(任意句子)」
             * ((?:[\w]+[^。\w])*[\w]+[。])
             *      想法：不屬於說話句型，則會是(不是句號結尾的句子)(必定有1個字以上,加上結尾是句號)
             * 你看不懂嗎?
             * 沒關係，看看下面網址(第一個是來搞笑的)，我也花了3小時才打出這2句正規表示式，加油
             * 參考網址：https://zh.wikipedia.org/wiki/%E6%AD%A3%E5%88%99%E8%A1%A8%E8%BE%BE%E5%BC%8F#%E7%AF%84%E4%BE%8B
             *           https://docs.microsoft.com/zh-tw/dotnet/standard/base-types/regular-expressions
             *///重點!!!!!!這裡沒用正規表示，學長說效果沒多好就沒用了，有興趣再看:)
            List<string> temp_data = new List<string>();
            Regex reg = new Regex(@"((?:[\w]+[^。\w])*[\w]+：「(?:[\w]+[^\w])*」)|((?:[\w]+[^。\w])*[\w]+[。])");
            foreach (string str in data)
            {
                if (str.Length != 0)  //看是不是空白行
                {
                    string[] temp;
                    if (type == 0)
                        temp = reg.Split(str);              //這邊暫時這樣做。 目前沒有做(該句不是"。"結尾，也不是說話句型)的處理。  參考句型：小琪小琪房間亂，媽媽耐心除髒亂，  即不符合那2個條件
                    else if (type == 1)
                        temp = str.Split('。');             //這句是原本用的方法，就用'。'開拆當句子
                    else
                        throw new Exception("function SegmentToSentence() parameter:type error.");

                    foreach (string temp_str in temp)
                    {
                        if (temp_str.Length != 0)           //只存非空白句
                        {
                            if (type == 0)
                                temp_data.Add(temp_str);        //new
                            else
                                temp_data.Add(temp_str + "。"); //old
                        }
                    }
                }
                else  //是空白行，那就加空白進去
                {
                    temp_data.Add("");
                }
            }
            data = temp_data;       //寫回data裡

        }


        /* 傳入：
         *      List<string> data           要被斷(手斷腳)詞的內容，內容可為段落、整本書的內容、句子都可。該變數會在該function被改變
         *      HanParser.HanParser HP      斷詞工具，相同的有WECAn，但這邊使用的是HanParser
         * 簡易介紹：
         *      對data裡的內容做斷詞
         */
        public void WordSegmentation(ref List<string> data, WECAnAPI.WECAnAPI WECAn)
        {
            List<string> temp_data = new List<string>();
            List<string> segment_word = new List<string>();
            foreach (string str in data)
            {
                WECAn.Segment(str.Replace(" ", ""), segment_word);
                //string[] segment_word = HP.Segment(str.Replace(" ", "")).Split(' ');     //斷詞，既然都要斷詞了，那先把空白都清空再來斷

                //斷完詞會被分開，所以在用一個變數把它存成同一行，並且用" "分開
                string temp_str = "";
                for (int i = 0; i < segment_word.Count; i++)
                    temp_str += segment_word[i] + " ";

                temp_str = temp_str.Replace("\n", "").Replace("\t", "").Replace("\r", "").Trim();  //清除掉不必要的字元。正常應該是不用啦，LoadBook()那就清過了，這邊頂多做Trim()而已，但還是以防萬一，所以就多做一下
                temp_data.Add(temp_str);  //把斷完詞並且用" "分開完的句子存起來
            }
            data = temp_data;  //把data更新成斷完詞的資料
        }
        #endregion


        /* 傳入：
         *      string path     檔案路徑，應該要是完整檔名
         * 回傳：
         *      StreamReader    
         * 簡易介紹：
         *      主要是在做編碼的確認，目前只能分{Big5 & other}
         */
        protected StreamReader GetStramReader(string path)
        {
            //變成byte[]存起來
            byte[] data_bytes = File.ReadAllBytes(path);                                //將檔案的內容讀入位元組陣列
            Encoding big5 = Encoding.GetEncoding(950);                                  //950是big5的代號
            //將byte[]轉為string再轉回byte[]看位元數是否有變，沒變就是編碼一樣
            if (data_bytes.Length == big5.GetByteCount(big5.GetString(data_bytes)))     //如果是big5的
            {
                //是的話就要改編碼
                return new StreamReader(path, Encoding.GetEncoding(950));
            }
            else                                                                        //不是big5，就自動偵測就好
            {
                return new StreamReader(path, true);
            }
        }


        /* 傳入：
         *      List<string[]> book_vector      存載入後的向量的變數，會在被function改變內容
         *      List<string> book_data          書籍內容，這邊會根據這內容來取得對應的向量，然後存到book_vector裡
         *      int type                        根據輸入決定是否顯示做sentence2vec時的bash  0=不顯示 ,other=顯示
         * 簡易說明：
         *      根據book_data的內容，來取得其對應的語意向量，並存到book_vector裡
         */
        public void GetBookDataVector(ref List<string[]> book_vector, List<string> book_data, WECAnAPI.WECAnAPI WECAn, int type, string book_name, string mode)
        {

            mode = mode.ToLower();
            if (mode == "sentence2vec")
            {
                List<string> book_data_seg = book_data;                                     //存要被斷詞的書籍內容的變數
                WordSegmentation(ref book_data_seg, WECAn);                                    //斷詞
                SaveData<string>(book_data_seg, SystemPath + @"Sentence2vec\Sentence_Seg.txt");    //把斷詞好的內容存給sentence2vec的python準備使用，來算語意向量
            }
            else if (mode == "bert")
            {
                SaveData<string>(book_data, SystemPath + @"bert\Sentence_Seg.txt");
            }

            book_vector.Clear();                                                        //存語意向量的變數，這行主要是要把它清空
            Run(type, book_name, mode); //#####                                                //做embadding，並載入語意向量

            if (mode == "sentence2vec")
            {
                Sentence2vec sentence2vec = new Sentence2vec(this.SystemPath);
                sentence2vec.LoadVector(ref book_vector);
            }
            else if (mode == "bert")
            {
                Bert bert = new Bert(this.SystemPath);
                bert.LoadVector(ref book_vector);
            }
            else
            {
                throw new Exception("Opeating.Run.mode input error.");
            }
        }


        /* 傳入：
         *      List<string> data       用來儲存書本內容。該變數會在該function改變
         * 回傳：
         *      int word_count              該本書的總字數
         * 簡易介紹：
         *      將data裡的內容做集合，並在data裡存成1段
         */
        public int ToOneParagraph(ref List<string> data)
        {
            int word_count = 0;
            string all_data = "";
            for (int i = 0; i < data.Count; i++)
            {
                all_data += data[i];
                word_count += data[i].Length;
            }
            data.Clear();  //清除data
            data.Add(all_data);  //將all_data存入data

            return word_count;
        }


        /* 傳入：
         *      int type                input(0)=隱藏cmd
         *      string book_name        當前在做處理的書籍名稱
         * 簡易介紹：
         *      把文本做Sentence2vec。
         *      2019/10/28:新增固定句子向量方法，同時可以減少重複執行sentence2vec。
         */
        public void Run(int type, string book_name, string mode = "sentence2vec")
        {
            mode = mode.ToLower();
            string file_name;
            if (mode == "sentence2vec")
            {
                //先找看過往有沒有做過一樣內容的sentence2vec，有的話就複製過去
                try
                {
                    string[] data_2 = System.IO.Directory.GetFiles($@"{SystemPath}data\{book_name}", $"{mode}*.txt");      //因為這行不一定成立，所以有機會跳Exception出來。因為不一定曾經做過該書籍，所以不一定有該書籍的資料夾
                    foreach (string str in data_2)                                                                         //上面那行成立，那就去抓所有對應的文本，來看有沒有相符合的，有的話就用過去的句子向量即可
                    {
                        if (CheckBook($@"{SystemPath}Sentence2vec\Sentence_Seg.txt", str))        //########               //檢查2文本是否相同。CheckBook(這次在做的文本, 過往的文本)
                        {
                            System.IO.FileInfo file_info = new System.IO.FileInfo(str + ".vec");                           //開向量檔，準備copy
                            file_info.CopyTo($@"{SystemPath}Sentence2vec\Sentence_Seg.txt.vec", true);                     //把文本對應的句子向量檔案複製過去Sentence2vec的資料夾
                            Console.WriteLine("拿之前的Vector");
                            return;                                                                                        //結束
                        }
                    }
                }
                catch
                {
                    System.IO.Directory.CreateDirectory($@"{SystemPath}data\{book_name}");                                      //因為沒做過該書籍，所以創個資料夾，然後開始做轉換文本向量
                }
                Console.WriteLine("做新的Vector");
                Sentence2vec sentence2vec = new Sentence2vec(this.SystemPath);
                sentence2vec.Run(type);//##############
                file_name = mode;
            }
            else if (mode == "bert")
            {
                Bert bert = new Bert(this.SystemPath);
                List<string> config = bert.GetConfig();
                if (int.Parse(config[0]) > 512)
                    config[0] = "512";
                string config_name = $"{config[0]}_{config[1]}_{config[2]}_{config[3]}";
                //先找看過往有沒有做過一樣內容的sentence2vec，有的話就複製過去
                try
                {
                    string[] data_2 = System.IO.Directory.GetFiles($@"{SystemPath}data\{book_name}", $"{mode}_{config_name}*.txt");      //因為這行不一定成立，所以有機會跳Exception出來。因為不一定曾經做過該書籍，所以不一定有該書籍的資料夾
                    foreach (string str in data_2)                                                                         //上面那行成立，那就去抓所有對應的文本，來看有沒有相符合的，有的話就用過去的句子向量即可
                    {
                        if (CheckBook($@"{SystemPath}{mode}\Sentence_Seg.txt", str))                             //檢查2文本是否相同。CheckBook(這次在做的文本, 過往的文本)
                        {
                            System.IO.FileInfo file_info = new System.IO.FileInfo(str + ".vec");                           //開向量檔，準備copy
                            file_info.CopyTo($@"{SystemPath}{mode}\Sentence_Seg.txt.vec", true);                 //把文本對應的句子向量檔案複製過去Sentence2vec的資料夾
                            return;                                                                                        //結束
                        }
                    }
                }
                catch
                {
                    System.IO.Directory.CreateDirectory($@"{SystemPath}data\{book_name}");                               //因為沒做過該書籍，所以創個資料夾，然後開始做轉換文本向量
                }

                bert.Run(type);
                file_name = mode + "_" + config_name;
            }
            else
            {
                throw new Exception("Opeating.Run.mode input error.");
            }

            Thread.Sleep(1000);                                                 //暫停執行緒1秒

            //會跑到這來，代表先前不曾做過該文本的sentence2vec，且已經做完sentence2vec了，所以要保留起來
            string date = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds.ToString().Replace(".", "");        //先取得unix time，來當作流水編號。到.TotalSeconds都是在擷取unix time，然後ToString，再來因為有"."，所以Replace去掉"."
            string[] data = new string[0];
            if (mode == "sentence2vec")
                data = System.IO.Directory.GetFiles($@"{SystemPath}sentence2vec", "Sentence_Seg*");                    //先去抓mode資料夾下的Sentence_Seg*的檔案，也就那2個，一個是文本跟對應的句子向量
            else if (mode == "bert")
                data = System.IO.Directory.GetFiles($@"{SystemPath}bert", "Sentence_Seg*");                    //先去抓mode資料夾下的Sentence_Seg*的檔案，也就那2個，一個是文本跟對應的句子向量
            foreach (string str in data)
            {
                System.IO.FileInfo file_info = new System.IO.FileInfo(str);                                                     //開檔，開剛Sentence2vec底下的那兩個檔
                if (file_info.Name.Contains(".vec"))                                                                            //如果是向量檔
                    file_info.CopyTo($@"{SystemPath}data\{book_name}\{file_name}_{date}.txt.vec");
                else                                                                                                            //如果不是向量檔，那就是文本檔
                    file_info.CopyTo($@"{SystemPath}data\{book_name}\{file_name}_{date}.txt");
            }
        }


        /* 傳入：
         *      string book1        書籍1，需要完整檔名路徑
         *      string book2        書籍2，需要完整檔名路徑
         * 回傳：
         *      2個檔案相同=true
         *      2個檔案不相同=false
         * 簡易介紹：
         *      檢查2個檔案是否相同。專門給sent2vec使用的，用來檢查過往有沒有做過相同文本，以減少sentence2vec次數跟固定句子向量
         */
        protected bool CheckBook(string book1, string book2)
        {
            using (StreamReader sr1 = new StreamReader(book1))
            {
                using (StreamReader sr2 = new StreamReader(book2))
                {
                    return sr1.ReadToEnd() == sr2.ReadToEnd();
                }
            }
        }


        /* 傳入：
         *      double[] ArrayA     語意向量_A
         *      double[] ArrayB     語意向量_B
         * 回傳：
         *      double              相似度數值
         * 簡易介紹：
         *      計算相似度值，其實就是在算acos(value)
         *      如果使用下面的歐幾里得距離的話，需要正規在-1~0之間，所以要用的話要想辦法來正規化值ㄛ
         */
        public static double cosine_measure_similarity(double[] ArrayA, double[] ArrayB)
        {
            int dimension = ArrayA.Length;  //向量維度
            double Molecular = 0;           //分子(A dot B)
            double Denominator1 = 0;        //分母(陣列A的向量的長度)
            double Denominator2 = 0;        //分母(陣列B的向量的長度)

            //以下是公式: dot(A,B) / ( sqrt(sigma(A[i]^2)) * sqrt(sigma(B[i]^2)) )  的實作
            for (int i = 0; i < dimension; i++)
            {
                Molecular += (ArrayA[i] * ArrayB[i]);
                Denominator1 += Math.Pow(ArrayA[i], 2);
                Denominator2 += Math.Pow(ArrayB[i], 2);
            }

            Denominator1 = Math.Sqrt(Denominator1);
            Denominator2 = Math.Sqrt(Denominator2);

            return Molecular / (Denominator1 * Denominator2);  //回傳的這是相似度數值
        }


        /* 傳入：
         *      double[] ArrayA     語意向量_A
         *      double[] ArrayB     語意向量_B
         * 回傳：
         *      double              距離數值
         * 簡易介紹：
         *      計算兩向量的距離，這邊用的是歐幾里得距離。
         *      我這邊只是想看他們的距離，寫好起來放，沒特別用在哪
         */
        public static double EuclideanDistance(double[] array_a, double[] array_b)
        {
            double sub_sum = 0;
            for (int i = 0; i < array_a.Length; i++)
            {
                sub_sum += Math.Pow(array_a[i] - array_b[i], 2);
            }

            return Math.Sqrt(sub_sum);
        }
    }
}
