using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Web;

namespace AutoSummaryTest
{
    class AutoSummary
    {
        /* 快速使用說明：
         *      AutoSummary autosummary = new AutoSummary();
         *      autosummary.precess(ref List<string> auto_summary_data, List<string> data, List<string[]> data_vec, double multiple);
         *      //存摘要請使用下面這Method
         *      autosummary.SaveAutoSummary(List<string> auto_summary_data, List<string> data, string path);
         * 傳入參數說明：
         *      List<string> auto_summary_data      儲存該演算法做完後會得到的機器自動摘要內容(以句子為單位)，該變數會在這function被改變，儲存內容為機器自動摘要內容
         *      List<string[]> summary_vec          儲存摘要具Vector
         *      ref List<string[]> blockInfo        儲存格式 [num(句數),blockCount,Percentage,rank] 
         *      List<string> data                   文本內容
         *      List<string[]> data_vec             文本內容對應的語意向量
         *      double multiple                     取得前 (multiple*100%) 靠近區塊中心的句子，來當摘要
         *      string path                         摘要儲存路徑，不用自己加檔名&副檔名，但傳入閣是應該為："C:\Users\ISLAB\Desktop\交接\"這樣，重點是最後面需有"\"
         */


        #region Public Method
        /* 簡易介紹：
         *      機器自動摘要的主要架構跟順序的function
         */
        public void Process(ref List<string> auto_summary_data, ref List<string[]> summary_vec, ref List<string[]> blockInfo, List<string> data, List<string[]> data_vec, double multiple)
        {
            //先做一次機器摘要
            GetSummary(ref auto_summary_data, ref summary_vec, ref blockInfo, data, data_vec, multiple);

            //接著做重複摘要的處理
            //如果auto_summary_data裡全部都是一樣的句子的話，這邊採用Count(lambda)來查詢出現次數  (相同摘要數量)+(空白行數量) == auto_summary_data.Count 就等於auto_summary_data裡都是一樣的摘要
            string temp_str = auto_summary_data[0];
            if (auto_summary_data.Count(str => str == temp_str) + auto_summary_data.Count(str => str.Length == 0) == auto_summary_data.Count)  //另一種寫法: auto_summary_data.FindAll(str => str == temp_str).Count + auto_summary_data.FindAll(str => str.Length == 0).Count == auto_summary_data.Count
            {
                //如果機器摘要全部都相同的話
                List<string> temp_data = data;                      //因為不該變動到data的資料，所以這邊多用一個變數來複製data
                int num = temp_data.Count(str => str == temp_str);  //紀錄該句摘要在data裡的出現次數
                auto_summary_data.Clear();                          //清除先前的機器摘要
                summary_vec.Clear();                                //清除先前的摘要Vector
                List<string[]> padding = new List<string[]>();
                for (int i = 0, index = 0; i < num; i++, index++)   //要將data裡的重複摘要句全部清除，當成空白行(也就是區塊分割點)
                {
                    //會要不斷給新Index 是因為，他只會找第一個，所以如果有找到，就要移一下起始點
                    index = temp_data.FindIndex(index, str => str == temp_str);     //找到在temp_data裡的index
                    temp_data[index] = "";                          //對該index所儲存的資料清除掉
                }
                //捨棄重複很多變的句子後，再來一次
                GetSummary(ref auto_summary_data, ref summary_vec, ref  padding, temp_data, data_vec, multiple);   //再對temp_data做一次機器摘要
            }
        }


        /* 傳入：
         *      List<string> auto_summary_data      儲存著機器自動摘要過後的內容
         *      List<string> data                   文本內容
         *      string path                         儲存路徑
         * 簡易介紹：
         *      儲存auto_summary_data裡的內容至path，這邊會對機器自動摘要做排序，從上到下看下來應該是對照內容的開始到結束
         */
        public void SaveAutoSummary(List<string> auto_summary_data, List<string> data, string path)
        {
            using (StreamWriter SW = new StreamWriter(path + "AutoSummary.txt"))
            {
                //因為auto_summary_data 都是從data挑出來的，所以必定會在data找到對應的index，將這index存到data_sort裡，然後排序，就可以得到摘要在文本內容裡的順序了
                List<int> data_sort = new List<int>();
                foreach (string str in auto_summary_data)
                {
                    if (str.Length != 0)  //如果不是空白行
                    {
                        data_sort.Add(data.IndexOf(str));  //儲存index到data_sort裡
                    }
                    else  //如果是空白行
                    {
                        data_sort.Sort(delegate (int value1, int value2)  //做sort，這邊用lambda
                        {
                            return value1.CompareTo(value2);  //從小排到大
                        });
                        for (int i = 0; i < data_sort.Count; i++)
                        {
                            SW.WriteLine(data[data_sort[i]]);  //寫入data[data_sort[i]]至指定路徑的txt
                        }
                        SW.WriteLine();  //寫空白行
                        data_sort.Clear();  //清除data_sort
                    }
                }
            }
        }
        #endregion


        #region 內部用到的function, 有機會更改到的
        /**/
        protected void GetSummary(ref List<string> auto_summary_data, ref List<string[]> summary_vec, ref List<string[]> blockInfo, List<string> data, List<string[]> data_vec, double multiple)
        {
            //data裡的區塊是用空白行當分割的，所以遇到空白行就等於(start_index,i)這範圍是一個區塊，而i指著的index是空白行的index
            int start_index = 0;  //標記區塊的第一行
            int blockCount = 0;   //BlockCount
             
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Length == 0)  //如果遇到空白行
                {                                                             //start_index (start of block)
                                                                              //i           (end of block)
                    GetImportantSummary(ref auto_summary_data,ref summary_vec ,ref blockInfo,blockCount, data, data_vec, start_index, i, multiple);  //取得前multiple倍個靠近區塊中心的向量
                    start_index = i + 1;  //目前這行是空白行，所以下個區塊的第一行是當前index+1
                    
                    //做機器自動摘要裡的區塊分割，好方便儲存後查看而已
                    auto_summary_data.Add("");  
                    blockCount++;
                }
                
            }
        }


        /* 傳入：
         *      ref List<string> auto_summary           儲存這演算法取得的摘要 的list
         *      ref List<string[]> summary_vec          儲存摘要具Vector
         *      ref List<string[]> blockInfo            儲存格式 [num(句數),blockCount,Percentage,rank] 
         *      int blockCount                          紀錄到第幾個Block
         *      List<string> data                       文本內容
         *      List<string[]> Sentence_vec_ArrayList   存著句子向量的變數
         *      int start_index                         該值是標記在data的區塊的第一行的int index
         *      int end_index                           該值是標記在data的區塊的空白行的int index
         *      double multiple                         取得前 (multiple*100%) 靠近區塊中心的句子，來當摘要
         * 簡易介紹：
         *      取得前multiple倍靠近中心向量的摘要。
         */
        protected void GetImportantSummary(ref List<string> auto_summary_data,ref List<string[]> summary_vec, ref List<string[]> blockInfo, int blockCount, List<string> data, List<string[]> data_vec, int start_index, int end_index, double multiple)
        {
            int dimension = data_vec[0].Length;  //取得句子向量的維度
            double[] center_vector = new double[dimension];  //儲存中心向量的變數

            //先計算出中心向量
            //公式：center_vector = sigma(data_vector) / data_vector.count
            for (int i = start_index; i < end_index; i++)
            {
                double[] temp_vector = Array.ConvertAll(data_vec[i], Double.Parse);  //得到句子向量

                for (int j = 0; j < dimension; j++)
                {
                    center_vector[j] = center_vector[j] + temp_vector[j];                 //累計所有句子的向量
                }
            }
            for (int i = 0; i < dimension; i++)
                center_vector[i] /= (double)(end_index - start_index);  //取得中心向量

            //計算各個向量跟中心向量的相似度
            List<double[]> result = new List<double[]>();
            for (int i = start_index; i < end_index; i++)
            {
                double[] temp_vector = Array.ConvertAll(data_vec[i], Double.Parse);  //得到句子向量
                double similarity = Opeating.cosine_measure_similarity(center_vector, temp_vector);  //計算data_vector和center_vector的相似度
                result.Add(new double[] { i, similarity });  //儲存結果，這邊直接new一個double[]來儲存
            }

            result.Sort((value1, value2) => value2[1].CompareTo(value1[1]));  //排序，這邊用lambda來傳入比較方法(比similarity)(大到小)

            for (int i = 0; i < result.Count; i++)//這裡result的index(i)是data的(包括空白)，不是句數，所以要 - blockCount
            {
                float percentage = (float)(i) / (float)(result.Count);
                blockInfo.Add(new string[] { (result[i][0] + 1 - blockCount).ToString(), blockCount.ToString(), percentage.ToString(), i.ToString() });//儲存資料[num(句數),blockCount,Percentage,rank] 
            }

            //取得multiple倍數的摘要 + Vector
            for (int i = 0; i < (end_index - start_index) * multiple; i++)
            {
                auto_summary_data.Add(data[(int)result[i][0]]);//句子
                summary_vec.Add(data_vec[(int)result[i][0]]);          //向量
            }
        }
        #endregion
    }
}