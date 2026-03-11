using System.ComponentModel.DataAnnotations;
using Footing.Models;

namespace Footing.Client.ViewModels;

public class NewMoneyFlowModel
{
    [Required(ErrorMessage = "Please enter a short name")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Please enter valid amount")]
    [RegularExpression(@"^\d+(\.\d{0,2})?$", ErrorMessage = "Please enter an amount in dollars and cents, like xxxxx.xx")]
    [Range(0, 9999999999999999.99, ErrorMessage = "Unfortunately, this tool will not be helpful with very high or negative amounts")]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Please specify how often this occurs")]
    public Period? Period { get; set; }
}
