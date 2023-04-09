using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;

namespace AutoSummaryTest
{
    /* 區塊分割-改良版 20190828開寫
     * 因為原方法的時間複雜度是 (n^2-n)/2  ((其實原方法是O(n^2)啦      //n是總段落數 ws是windows_size
     * 我想的方法為 (n-ws+1) * (3*(ws/2-1))
     * 基本上ws=(n/2)時，計算複雜度最大，但還是比原方法快一些，大概少1/4的計算次數
     * 但其實原方法只是先計算全部的相似度而已，但好亂，所以我重寫 ：）  ((因為我根本不知道他到底存ㄌ啥
     * ※ 如果你仔細看code，你會發現List<string> data我都多花時間算他的總字數，因為我不喜歡要傳那麼多東西，而且不缺那點時間來計算總字數啦
     */
    class BlockCohesion
    {
        /* 快速使用說明：
         *      BlockCohesion bc = new BlockCohesion();
         *      bc.Process(List<string> data, ArrayList data_vec, int windows_size, double stdevp, double content_multiple, string system_path);  //分割完的內容會存在data裡
         *      //若要畫折線圖來看切割可能性的值可使用以下function
         *      bc.SaveAllResult(string path);      //path為儲存路徑，可搭配我寫的工具"LineChart.exe"來使用
         * 
         * 傳入參數說明：
         *      List<string> data                       使用Opeating.LoadBook()載入進來的書籍內容
         *      List<string[]> data_vec  使用Opeating.LoadVector()載入進來的書籍段落向量
         *      int windows_size                        這演算法的重要參數之一，這值越高，切割出來的區塊數越少，應該設為4的倍數，建議設為8，但不一定是最佳，可自行測試
         *      doble stdevp                            這演算法的重要參數之一，這值越高，切割出來的區塊數越多，建議設為1.5，但不一定是最佳，可自行測試
         *      double content_multiple                 這演算法的重要參數之一，這值越高，切割出來的區塊數越少，建議設為2.5，但不一定是最佳，可自行測試
         *      string system_path                      儲存切割完的書籍內容的路徑，檔名為"block.txt"
         *      
         *  ※若想修改該演算法，應該只須改Process() FindSharpPoint() BlockCutting()這三個function
         *    並且須了解AllResult 、 Cohesion_L_List 、 Cohesion_M_List 、 Cohesion_M_List 、 SharpPointLocation這幾個變數的內容值在幹嘛
         */
        protected int windows_size;                                                                  //應該要是4的倍數，且>=4，否則根本沒辦法計算、並執行該演算法
        protected List<string[]> data_vec;                                                           //存書籍中所有段落的向量，List裡存的是句子，string[]存的是維度和各維度的值
        protected List<double> AllResult = new List<double>();                                       //存代表所有分割點其切割可能性的值，index 0存的是(ws/2,ws/2+1)斷的切割可能性
        protected List<int> CuttingPoint = new List<int>();                                          //存可以切割的位置，存的index是後面那個index  EX:(4,5)要切，則會存index:5
        protected List<double> Cohesion_L_List = new List<double>();                                 //存左區塊的區塊凝聚力的值，index 0存的是(ws/2,ws/2+1)斷的左區塊凝聚力
        protected List<double> Cohesion_R_List = new List<double>();                                 //存右區塊的區塊凝聚力的值，index 0存的是(ws/2,ws/2+1)斷的右區塊凝聚力
        protected List<double> Cohesion_M_List = new List<double>();                                 //存中心區塊的區塊凝聚力的值，index 0存的是(ws/2,ws/2+1)斷的中心區塊凝聚力
        protected List<int> SharpPointLocation = new List<int>();                                    //存尖點的位置(index)，主要對照到的是AllResult裡的index，像AllResult[0]存的是(4,5)段，而該段是尖點，則這裡會存int(0)
        protected class SPTemp                                                                          //用來儲存SharpPoint的句數、累積字數、切割可能性
        {
            public int  location;
            public int wordCount;
            public double possibility;
            public SPTemp(int location,int wordCount,double possibility)
            {
                this.location = location;
                this.wordCount = wordCount;
                this.possibility = possibility;
            }
        }

        //該演算法主要執行之架構 & 順序 & 供外主要使用之function  ，  分割完的內容會存在data裡，且data_vec也會被切割，讓他跟data是互相對應的
        public void Process(ref List<string> data, ref List<string[]> data_vec, int windows_size, double stdevp, double content_multiple, string system_path)
        {                                                                                         //幾倍標準差           設定格多少字會用
            if (windows_size % 4 != 0 && windows_size >= 4)  //檢查是不是4的倍數啦跟大於等於4，破英文，還是註解一下好ㄌ
                throw new Exception("windows_size should be to 4 multiple and more than 4.");
            this.windows_size = windows_size;
            this.data_vec = data_vec;

            CalculateAllBlock();                                            //算所有區塊的切割可能性
            FindSharpPoint(stdevp);                                         //取得所有的尖點
            BlockCutting(ref data, ref data_vec, content_multiple);         //做書籍切割
            SaveBook(data, system_path + "block.txt", content_multiple);    //儲存切割完的書籍
            SaveAllResult(system_path);                                     //儲存AllResult到指定路徑
        }


        //找特定的尖點
        //輸入參數為要留下value個標準差以上的尖點，值越大，留的尖點越多
        protected double FindSharpPoint(double value)
        {
            //先挑出尖點
            //第1個點和最後1個點不可能成為尖點，得range(0,final)
            for (int i = 0; i < this.AllResult.Count; i++)
            {
                //第一步：只看超過0的值
                if (this.AllResult[i] <= 0)
                    continue;
                //第二步：跟左右兩旁的點比較，看自己是不是數值比較高的點(也就是尖點)
                if (i - 1 >= 0)  //預防超出左邊界
                    if (this.AllResult[i] < this.AllResult[i - 1])  //如果當前的值，小於左邊的值，那我這點就不是尖點
                        continue;

                if (i + 1 < this.AllResult.Count)  //預防超出右邊界
                    if (this.AllResult[i] < this.AllResult[i + 1])  //如果當前的值，小於右邊的值，那我這點就不是尖點
                        continue;

                //第三步：看目前i這點的左、右區塊凝聚力，有沒有大於中間區塊凝聚力
                if (this.Cohesion_L_List[i] > this.Cohesion_M_List[i] && this.Cohesion_R_List[i] > this.Cohesion_M_List[i])
                {
                    //第四步：因為上面的條件都成立，所以該點是尖點，存入list裡
                    this.SharpPointLocation.Add(i);
                }
            }

            if (this.SharpPointLocation.Count == 0)  //如果都沒有尖點的話，就沒必要做下面的事了
                return 0;

            //先取得所有是尖點的值
            List<double> sharp_point_value = new List<double>();
            for (int i = 0; i < this.SharpPointLocation.Count; i++)
                sharp_point_value.Add(this.AllResult[this.SharpPointLocation[i]]);

            //先算出邊界值，這邊用標準差來算
            //參考網站https://zhidao.baidu.com/question/1948871768880202428.html
            //計算平均數 
            double avg = sharp_point_value.Average();
            //計算各數值與平均數的差值的平方，然後求和
            double sum = sharp_point_value.Sum(d => Math.Pow(d - avg, 2));
            //除以數量，然後開方(標準差)
            double STDEVP = Math.Sqrt(sum / sharp_point_value.Count());
            //界線 = 平均數-n倍標準差
            double Boundary = avg - value * STDEVP;
            Console.WriteLine("門檻值 : {0}", Boundary);
            //然後來篩選掉值小於邊界值的尖點
            List<int> temp_sharp_point_location = new List<int>();
            for (int i = 0; i < sharp_point_value.Count; i++)
            {
                if (sharp_point_value[i] >= Boundary)  //如果我的值比邊界值大或是等於邊界值
                    temp_sharp_point_location.Add(this.SharpPointLocation[i]);  //那我就是比較好的尖點，所以記錄起來
            }

            this.SharpPointLocation = temp_sharp_point_location;  //換成真正尖點的位置
            Console.WriteLine("SharpPointLocation = {0}\n",this.SharpPointLocation.Count);
            return Boundary;
        }


        /* 傳入：
         *      List<string> data           書本內容，會在這function裡改變內容，改變後儲存的內容為做完區塊切割的內容
         *      List<string[]> data_vec     書本內容對應的向量，會在這function裡改變內容，改變後儲存的內容為做完區塊切割的內容對應的向量(就讓他跟書本內容是一樣的，這樣方便讀取)
         *      double multiple             區塊內要有多少字數的參數，越高區塊內的字越多
         * 簡易介紹：
         *      做書籍的區塊切割
         */
        protected void BlockCutting(ref List<string> data, ref List<string[]> data_vec, double multiple)
        {
            //沒有尖點，直接回去
            if (this.SharpPointLocation.Count == 0)
            {
                data.Add("");
                data_vec.Add(new string[0]);
                return;
            }


            //先取得data的總字數
            int word_count = 0;
            for (int i = 0; i < data.Count; i++)
                word_count += data[i].Length;

            int CurrentWordCount = 0;           //當前字數
            int Segment = (int)(word_count / this.SharpPointLocation.Count * multiple);  //算每隔多少字(切一次/找尖點)

            //等等要把SharpPoint由大到小排列(依照AllResault)，這裡先給他們在文章中的字數、切割可能性
            List< SPTemp >  sharpPointTemp  = new List<SPTemp>();           //儲存SharpPointLocation對應句數、字數、切割可能性
            int SPIndex = 0;                                                //sharpPointIndex
            for (int i = 0;i < this.SharpPointLocation[this.SharpPointLocation.Count - 1] + 1; i ++)
            {
                CurrentWordCount += data[i].Length;
                //如果到了SharpPoint就存起來( i (句數) 到達尖點所在 句數)
                if (i == this.SharpPointLocation[SPIndex])
                {
                    //補 WindowSize 字數
                    for (int j = 1; j < this.windows_size / 2; j++)
                    {
                        CurrentWordCount += data[i + j].Length;
                    }
                    sharpPointTemp.Add(new SPTemp(this.SharpPointLocation[SPIndex], CurrentWordCount, this.AllResult[this.SharpPointLocation[SPIndex]]) );
                    SPIndex ++;
                    for (int j = 1; j < this.windows_size / 2; j++)
                    {
                        CurrentWordCount -= data[i + j].Length;
                    }
                }
            }
            //開始依照AllResault(SPTemp.possibility)排序。
            sharpPointTemp.Sort((x, y) => { return -x.possibility.CompareTo(y.possibility); });
            
            for (int i = 0; i < sharpPointTemp.Count; i ++)
            {
                Console.WriteLine("SharpPointLocation : {0} CurrentWordCount : {1} Possibility : {2}", sharpPointTemp[i].location + this.windows_size / 2, sharpPointTemp[i].wordCount, sharpPointTemp[i].possibility);
            }
            //儲存到this.CuttingPoint，每個點都要比對之前的，看有沒有過於接近
            bool flag = true;
            int range = 0;
            int acceptable = Segment * 4 / 5;
            //int acceptable = 0;
            for (int i = 0;i < sharpPointTemp.Count;i++)//從頭到尾
            {
                for (int j = 0; j < this.CuttingPoint.Count;j++)  //跟在他之前錄取的的都相減一次，看有沒有跟哪個太近
                {
                    int index = sharpPointTemp.FindIndex(r => r.location == (this.CuttingPoint[j] - this.windows_size / 2));
                    range = Math.Abs(sharpPointTemp[i].wordCount - sharpPointTemp[index].wordCount);
                    //太靠近某一個已記錄的切割點
                    if (range < acceptable) 
                    {
                        Console.WriteLine("Range : {0} Acceptable : {1} 不合格",range, acceptable);
                        flag = false;
                        break;
                    }
                    //前面都沒Break到最後就會是TRUE
                    flag = true;
                }
                if(flag == true)
                {
                    Console.WriteLine("Range : {0} Acceptable : {1} 合格", range, acceptable);
                    this.CuttingPoint.Add(sharpPointTemp[i].location + this.windows_size / 2);
                }
            }
            //塞選完後，如果有因距離不合格被淘汰，但切割可能性大於 平均值 - num *標準差，那就無條件納入
            AddAboveAvarage(sharpPointTemp,0.5);


            this.CuttingPoint.Sort();
            Console.WriteLine("CuttingPoint(這裡是從0開始數 EX: 1,2要切，存2) : ");
            for (int i = 0; i < this.CuttingPoint.Count; i++)
            {
                Console.WriteLine(this.CuttingPoint[i]);
            }

            //做切割(用空直)，並存到對應的data跟data_vec   
            List<string> temp_data = new List<string>();
            List<string[]> temp_vec = new List<string[]>();
            for (int i = 0; i < data.Count; i++)
            {
                if (this.CuttingPoint.IndexOf(i) != -1)
                {
                    temp_data.Add("");
                    temp_vec.Add(new string[] { });
                }
                temp_data.Add(data[i]);
                temp_vec.Add(data_vec[i]);
            }
            temp_data.Add("");
            temp_vec.Add(new string[] { });

            data = temp_data;
            data_vec = temp_vec;
        }

        //塞選完後，如果有因距離不合格被淘汰，但切割可能性大於平均值，那就無條件納入
        //value == - 多少標準差
        protected void AddAboveAvarage(List<SPTemp> sharpPointTemp ,double value)
        {
            double avg = 0;
            double temp = 0;
            double diffSum = 0;
            for (int i = 0; i < this.CuttingPoint.Count; i++)
            {
                temp += sharpPointTemp[sharpPointTemp.FindIndex(r => r.location == (this.CuttingPoint[i]) - this.windows_size / 2)].possibility;
            }

            avg = temp / this.CuttingPoint.Count;

            for(int i = 0; i < this.CuttingPoint.Count; i++)
            {
                diffSum += Math.Pow(sharpPointTemp[sharpPointTemp.FindIndex(r => r.location == (this.CuttingPoint[i]) - this.windows_size / 2)].possibility - avg, 2);
            }
            //除以數量，然後開方(標準差)
            double STDEVP = Math.Sqrt(diffSum / this.CuttingPoint.Count);
            //界線 = 平均數-n倍標準差
            double Boundary = avg - value * STDEVP;
            Console.WriteLine("入取直 : {0}", Boundary);

            //檢查所有sharpPointTemp，如果有大於Avg，就檢查是不是已經在this.CuttingPoint，沒有的話就加入
            for (int i = 0; i < sharpPointTemp.Count; i ++)
            {
                if (sharpPointTemp[i].possibility >= Boundary && this.CuttingPoint.IndexOf(sharpPointTemp[i].location + this.windows_size / 2) == -1)
                {
                    this.CuttingPoint.Add(sharpPointTemp[i].location + this.windows_size / 2);
                }
            }
        }

        //儲存書籍
        protected void SaveBook(List<string> dataList, string path, double content_multiple)
        {
            //先算他所有的字數
            int AllParagraphWordCount = 0, space_num = 0;
            for (int i = 0; i < dataList.Count; i++)
            {
                AllParagraphWordCount += dataList[i].Length;
                if (dataList[i].Length == 0)
                    space_num++;
            }

            StreamWriter SW = new StreamWriter(path);             //開檔，準備寫入
            //開始寫入
            SW.WriteLine("段落總字數:" + AllParagraphWordCount);
            SW.WriteLine("段落數:" + (dataList.Count - space_num));
            SW.WriteLine("尖點數:" + this.SharpPointLocation.Count);
            SW.WriteLine("最終選擇的切割點數:" + this.CuttingPoint.Count);
            if (this.SharpPointLocation.Count > 0)
            {
                SW.WriteLine("每" + (int)((AllParagraphWordCount / this.SharpPointLocation.Count) * content_multiple) + "字就找合適的尖點");
            }

            //因為直接上層就做切割在dataList裡了，所以這邊要把空白濾掉
            int space_count = 0;  //空白行計數變數
            for (int i = 0; i < dataList.Count; i++)
            {
                if (dataList[i].Length == 0) //如果是空白行
                {
                    space_count++;
                    SW.WriteLine("");  //寫入空白行，但不加行數
                    continue;
                }
                SW.WriteLine((i + 1 - space_count) + "." + dataList[i]);
            }
            //關閉寫入
            SW.Close();
        }


        //儲存AllResult(每個尖點值)
        public void SaveAllResult(string path)
        {
            StreamWriter SW = new StreamWriter(path + "AllResult.txt");
            for (int i = 0; i < this.AllResult.Count; i++)
            {
                SW.Write(this.AllResult[i]);
                if (i != this.AllResult.Count - 1)
                    SW.Write(",");
            }
            SW.Close();
        }


        #region ***正常應該不會修改到這區塊的程式碼啦***
        //計算所有分割點的切割可能性，並存到AllResult，且也會存各區塊的凝聚力到個別的List裡
        //這部分已經參數自動化了，真的不可能改到這function和LR & Middle計算的function
        protected void CalculateAllBlock()
        {
            int data_count = this.data_vec.Count;
            for (int i = 0; i < data_count - this.windows_size + 1; i++)
            {
                /* 傳值解釋：
                 * i都不用理，因為那只是偏移值，或是當前ws範圍內最左邊的那索引值
                 * Cohesion_L: i+ws/2-1 , (ws/2)是在取得左切割點，-1是因為索引值是從0開始
                 * Cohesion_R: i+ws-1 , 取得當前最右邊的索引值而已
                 *             i+ws/2 , 其實就是右切割點
                 * Cohesion_Middle: i+ws/4 , 要取得目前範圍的一半的一半，來當作最左邊的索引值
                 *                  i+ws-ws/4 , i+ws=當前最右邊的索引值，再去減掉ws/4就可以知道右區塊一半的索引值在哪
                 */
                double Cohesion_L = CalculateCohesion_LR(i, i + this.windows_size / 2 - 1);  //算左區塊的區塊凝聚力
                double Cohesion_R = CalculateCohesion_LR(i + this.windows_size - 1, i + this.windows_size / 2);  //算右區塊的區塊凝聚力
                double Cohesion_Middle = CalculateCohesion_Middle(i + this.windows_size / 4, i + this.windows_size - this.windows_size / 4);  //算中心區塊的區塊凝聚力

                this.AllResult.Add(Cohesion_L + Cohesion_R - (2 * Cohesion_Middle));  //計算切割可能性後存進去
                this.Cohesion_L_List.Add(Cohesion_L);  //存左區塊的凝聚力
                this.Cohesion_R_List.Add(Cohesion_R);  //存右區塊的凝聚力
                this.Cohesion_M_List.Add(Cohesion_Middle);  //存中心區塊的凝聚力
            }
        }


        //計算左或右區塊的區塊凝聚力，主要供CalculateAllBlock()使用
        //參數的right其實就是切割點的index  EX:1 2 3 4 ∥ 5 6 7 8  當丟入(1,4) 就等於在算左區塊，而4就是計算區塊凝聚力的切割點
        //                                                         當丟入(8,5) 就等於在算右區塊，而5就是計算區塊凝聚力的切割點
        protected double CalculateCohesion_LR(int left, int right)
        {
            int step;  //每次要加的數字
            if (left < right)  //準備要計算左區塊，所以 L_index < R_index
                step = 1;
            else               //準備要計算右區塊，所以 L_index > R_index
                step = -1;

            double MAX = -1;  //紀錄最大的段落相似度的值，而為甚麼要給-1這值呢，因為cos(theta)最小值就是-1，所以給-1
            for (int i = 0; i < this.windows_size / 2 - 1; i++)
            {
                //這部分在算相似度，若MAX小於算出來的值，那就記錄起來
                double[] ArrayA = Array.ConvertAll(this.data_vec[i * step + left], Double.Parse);
                double[] ArrayB = Array.ConvertAll(this.data_vec[right], Double.Parse);
                double cos = Opeating.cosine_measure_similarity(ArrayA, ArrayB);
                if (MAX < cos)
                    MAX = cos;
            }

            return MAX;
        }


        //計算中心區塊的區塊凝聚力，主要供CalculateAllBlock()使用
        protected double CalculateCohesion_Middle(int left, int right)
        {
            /* 這邊寫的想法是，若我要算block(5,6,7,8,9,10) ws=12 ((隨便取的，但只做ws為4的數而已
             * 那我切割點則會是7,8，取得方法為 k=ws/4-1 => k=2 , left+k and left+k+1 => 成功取得7,8
             * 另一範例： block(5,6,7,8) ws=8 => 成立   就這樣以此類推
             * 然後 2個切割點計算1次，再計算 sim(左切割點的左邊的那些值,右切割點)  sim(左切割點,右切割點的右邊的那些值)
             *   上面這句範例：block(5,6,7,8,9,10)  計算 sim(5,8) sim(6,8)  &  sim(7,9) sim(7,10)這樣
             *   可得知，計算次數為 2*(ws/4-1)+1 如上面例子為 5次，然後前式化簡，得計算次數為 (ws/2)-1
             */

            //先取得2個切割點
            int cut_point_L = left + (this.windows_size / 4 - 1);
            int cut_point_R = cut_point_L + 1;

            //MAX預設為sim(左切割點,右切割點)
            double MAX = Opeating.cosine_measure_similarity(Array.ConvertAll(this.data_vec[cut_point_L], Double.Parse), Array.ConvertAll(this.data_vec[cut_point_R], Double.Parse));
            for (int i = 0; i < (this.windows_size / 4 - 1); i++)
            {
                //sim(左切割點的左邊的那些值,右切割點)
                double[] ArrayA = Array.ConvertAll(this.data_vec[left + i], Double.Parse);
                double[] ArrayB = Array.ConvertAll(this.data_vec[cut_point_R], Double.Parse);
                double cos = Opeating.cosine_measure_similarity(ArrayA, ArrayB);
                if (MAX < cos)
                    MAX = cos;

                //sim(左切割點, 右切割點的右邊的那些值)
                ArrayA = Array.ConvertAll(this.data_vec[cut_point_L], Double.Parse);
                ArrayB = Array.ConvertAll(this.data_vec[right - i - 1], Double.Parse);
                cos = Opeating.cosine_measure_similarity(ArrayA, ArrayB);
                if (MAX < cos)
                    MAX = cos;
            }

            return MAX;
        }
        #endregion
    }
}