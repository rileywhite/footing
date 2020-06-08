using System;
using System.ComponentModel.DataAnnotations;
using Remeter.Portal.Components;

namespace Remeter.Portal.Model
{
    public class NewMoneyInflowModel
    {
        [Required(ErrorMessage = "Please enter a short name for the income stream")]
        public string? NewInflowName { get; set; }

        [Required(ErrorMessage = "Please enter valid income amount")]
        [RegularExpression(@"^\d+(\.\d{0,2})?$", ErrorMessage = "Please enter an income amount in dollars and cents, like xxxxx.xx")]
        [Range(0, 9999999999999999.99, ErrorMessage = "Unfortunately, this tool will not be helpful with very high incomes or with negative incomes")]
        public decimal? NewInflowAmount { get; set; }

        [Required(ErrorMessage = "Please specify how often you receive this income")]
        public MoneyFlows.Period? NewInflowPeriod { get; set; }
    }
}
