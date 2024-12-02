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
        public string Type { get; set; }

        public Transaction()
        {
            Date = DateTime.Now;
        }

        public Transaction(decimal amount, string category, string description, string type)
        {
            Date = DateTime.Now;
            Amount = amount;
            Category = category;
            Description = description;
            Type = type;
        }

        public bool IsValid()
        {
            return Amount > 0 &&
                   !string.IsNullOrEmpty(Category) &&
                   (Type == "수입" || Type == "지출");
        }

        public string ToSqliteDate()
        {
            return Date.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}