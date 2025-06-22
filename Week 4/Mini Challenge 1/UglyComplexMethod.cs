using ClairTourTiny.Core.Models.ProjectMaintenance;
using ClairTourTiny.Infrastructure;
using ClairTourTiny.Infrastructure.Dto.DTOs;

public class BidSummaryHelper
{
    private List<PhaseModel> _phases { get; set; }
    private List<ProjectBillingItemModel> _billingItems { get; set; }
    private List<ProjectBillingPeriodModel> _billingPeriods { get; set; }
    private List<ProjectBillingPeriodItemModel> _billingPeriodItems { get; set; }
    private List<ProjectBidExpenseModel> _bidExpenses { get; set; }
    private List<ProjectCrewModel> _crews { get; set; }
    private List<ExpenseCode> _expenseCode { get; set; }
    private List<JobType> _jobTypes { get; set; }
    private List<PropertyTypeDTO> _propertyTypes { get; set; }
    private ClairTourTinyContext _dbContext { get; set; }

    private string GetBillingItemDescription(ProjectBillingPeriodItemModel billingPeriodItem)
    {
        var bidPhaseRow = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.BidEntityNo);
        var crew = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.CrewEntityNo);
        var expense = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.ExpenseEntityNo);
        var equipment = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.EquipmentEntityNo);
        var parentProjectDescription = new[] {
            crew?.EntityDesc,
            expense?.EntityDesc,
            equipment?.EntityDesc
        }.FirstOrDefault(desc => !string.IsNullOrEmpty(desc)) ?? string.Empty;
        var bidProjectDescription = bidPhaseRow?.EntityDesc ?? string.Empty;
        var parentWords = parentProjectDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bidWords = bidProjectDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        while (i < Math.Min(parentWords.Length, bidWords.Length) && parentWords[i].ToLower()== bidWords[i].ToLower())
        {
            i++;
        }
        var shortened = string.Join(" ", parentWords.Skip(i).Except(new[] { "BID" }, StringComparer.OrdinalIgnoreCase));
        if (crew != null)
        {
            var bidCrew = _crews.FirstOrDefault(cr => cr.EntityNo == billingPeriodItem.CrewEntityNo && cr.SeqNo== billingPeriodItem.CrewSeqNo) ?? new();
            var maybeCrewSize = bidCrew.CrewSize > 1 ? $" ({bidCrew.CrewSize})" : "";
            return $"{shortened}: {bidCrew.JobDesc}{maybeCrewSize}".TrimStart(' ', ':');
        }
        else if (expense != null)
        {
            var bidExpense = _bidExpenses.FirstOrDefault(bdExp => bdExp.EntityNo == billingPeriodItem.ExpenseEntityNo && bdExp.SeqNo==billingPeriodItem.ExpenseSeqNo) ?? new();
            var maybeAsterisks = bidExpense.Category == "O" ? "**" : "";
            var maybeNotes = !string.IsNullOrWhiteSpace(bidExpense.Notes) ? $" - {bidExpense.Notes.Trim()}" : "";
            var expenseCode = _expenseCode.FirstOrDefault(ec => ec.ExpCd == bidExpense.ExpenseCode) ?? new();
            return $"{shortened} Expense: {maybeAsterisks}{expenseCode.Description}{maybeNotes}".Trim();
        }
        else if (equipment != null)
        {
            return string.IsNullOrWhiteSpace(shortened) ? "Equipment" : $"{shortened} Equipment";
        }
        else
        {
            return "???";
        }
    }
} 