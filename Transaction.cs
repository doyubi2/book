using System;

namespace AccountBook
{
    public class Transaction
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }  // "수입" 또는 "지출"
    }
}