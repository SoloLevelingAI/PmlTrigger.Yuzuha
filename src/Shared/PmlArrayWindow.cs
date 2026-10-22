#nullable disable
using System;
using System.Collections.Generic;

namespace YuzuhaToolkit.Pml
{
    /// <summary>一个有界索引窗口；上界不是稀疏数组的已设置元素数，字符串保留原生显示。</summary>
    public sealed class PmlArrayWindowResult
    {
        /// <summary>原生 MAXINDEX 返回的索引上界。</summary>
        public int IndexUpperBound { get; set; }
        /// <summary>本窗口首个原生索引，从 1 开始。</summary>
        public int StartIndex { get; set; }
        /// <summary>未返回的下一个原生索引；0 表示已到末端。</summary>
        public int NextIndex { get; set; }
        /// <summary>是否仍有索引未返回；部分数据不会冒充完整数组。</summary>
        public bool Truncated { get; set; }
        /// <summary>原始显示行，位置依次对应 StartIndex 加偏移，不丢弃 unset。</summary>
        public string[] Rows { get; set; }
        /// <summary>返回行的 UTF-16 字符总数，不含 JSON 包装。</summary>
        public int Characters { get; set; }
    }
    /// <summary>NET35、NET48 与 NET10 共用的有界读取策略；最多读取 maxItems 个槽位。</summary>
    public static class PmlArrayWindow
    {
        /// <summary>在读取任何元素前检查页起点和硬预算。</summary>
        public static void Validate(int startIndex,int maxItems,int maxCharacters)
        {
            if(startIndex<1)throw new ArgumentOutOfRangeException("startIndex");
            if(maxItems<1||maxItems>1000)throw new ArgumentOutOfRangeException("maxItems");
            if(maxCharacters<1||maxCharacters>262144)throw new ArgumentOutOfRangeException("maxCharacters");
        }
        /// <summary>读取一个窗口；字符预算不拆行，首行超预算明确失败，其余保留续读索引。</summary>
        public static PmlArrayWindowResult Read(int indexUpperBound,int startIndex,int maxItems,int maxCharacters,Func<int,string> read)
        {
            Validate(startIndex,maxItems,maxCharacters);
            if(indexUpperBound<0)throw new ArgumentOutOfRangeException("indexUpperBound");
            if(read==null)throw new ArgumentNullException("read");
            var rows=new List<string>();var characters=0;long index=startIndex;
            while(index<=indexUpperBound&&rows.Count<maxItems)
            {
                var row=read((int)index)??String.Empty;
                if(row.Length>maxCharacters-characters)
                {
                    if(rows.Count==0)throw new InvalidOperationException("PML_ARRAY_ITEM_EXCEEDS_BUDGET: increase maxCharacters within the hard limit or inspect a smaller value.");
                    break;
                }
                rows.Add(row);characters+=row.Length;index++;
            }
            var truncated=index<=indexUpperBound;
            return new PmlArrayWindowResult{IndexUpperBound=indexUpperBound,StartIndex=startIndex,NextIndex=truncated?(int)index:0,Truncated=truncated,Rows=rows.ToArray(),Characters=characters};
        }
    }
}
