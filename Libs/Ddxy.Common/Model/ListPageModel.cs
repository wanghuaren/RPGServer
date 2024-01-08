using System;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Ddxy.Common.Model
{
    [Serializable]
    public class ListPageReq
    {
        [Range(10, 100)]
        public int PageSize { get; set; }

        [Range(1, int.MaxValue)]
        public int PageIndex { get; set; }

        [StringLength(32)]
        public string Search { get; set; }

        public uint? StartTime { get; set; }

        public uint? EndTime { get; set; }
        
        public void Build()
        {
            if (PageSize == 0) PageSize = 10;
            else if (PageSize > 100) PageSize = 100;
            if (PageIndex == 0) PageIndex = 1;
        }
    }

    public class ListPageResp
    {
        public long Total { get; set; }
        
        public long Sum { get; set; }
        
        public long Sum1 { get; set; }
        
        public long Sum2 { get; set; }
        
        public long Sum3 { get; set; }

        public IEnumerable Rows { get; set; }
    }
}