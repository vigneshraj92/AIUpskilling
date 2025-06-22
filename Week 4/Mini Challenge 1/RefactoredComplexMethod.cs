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
        var relatedEntities = GetRelatedEntities(billingPeriodItem);
        var shortenedDescription = GetShortenedDescription(relatedEntities);
        
        return GetFormattedDescriptionByType(billingPeriodItem, relatedEntities, shortenedDescription);
    }

    private RelatedEntities GetRelatedEntities(ProjectBillingPeriodItemModel billingPeriodItem)
    {
        return new RelatedEntities
        {
            BidPhase = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.BidEntityNo),
            Crew = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.CrewEntityNo),
            Expense = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.ExpenseEntityNo),
            Equipment = _phases.FirstOrDefault(p => p.EntityNo == billingPeriodItem.EquipmentEntityNo)
        };
    }

    private string GetShortenedDescription(RelatedEntities entities)
    {
        var parentProjectDescription = GetParentProjectDescription(entities);
        var bidProjectDescription = entities.BidPhase?.EntityDesc ?? string.Empty;
        
        return CreateShortenedDescription(parentProjectDescription, bidProjectDescription);
    }

    private string GetParentProjectDescription(RelatedEntities entities)
    {
        var descriptions = new[]
        {
            entities.Crew?.EntityDesc,
            entities.Expense?.EntityDesc,
            entities.Equipment?.EntityDesc
        };
        
        return descriptions.FirstOrDefault(desc => !string.IsNullOrEmpty(desc)) ?? string.Empty;
    }

    private string CreateShortenedDescription(string parentDescription, string bidDescription)
    {
        if (string.IsNullOrEmpty(parentDescription))
            return string.Empty;

        var parentWords = parentDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bidWords = bidDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var commonPrefixLength = GetCommonPrefixLength(parentWords, bidWords);
        var remainingWords = parentWords.Skip(commonPrefixLength);
        
        return string.Join(" ", remainingWords.Except(new[] { "BID" }, StringComparer.OrdinalIgnoreCase));
    }

    private int GetCommonPrefixLength(string[] parentWords, string[] bidWords)
    {
        int i = 0;
        while (i < Math.Min(parentWords.Length, bidWords.Length) && 
               string.Equals(parentWords[i], bidWords[i], StringComparison.OrdinalIgnoreCase))
        {
            i++;
        }
        return i;
    }

    private string GetFormattedDescriptionByType(ProjectBillingPeriodItemModel billingPeriodItem, 
        RelatedEntities entities, string shortenedDescription)
    {
        if (entities.Crew != null)
            return FormatCrewDescription(billingPeriodItem, entities, shortenedDescription);
        
        if (entities.Expense != null)
            return FormatExpenseDescription(billingPeriodItem, entities, shortenedDescription);
        
        if (entities.Equipment != null)
            return FormatEquipmentDescription(shortenedDescription);
        
        return "???";
    }

    private string FormatCrewDescription(ProjectBillingPeriodItemModel billingPeriodItem, 
        RelatedEntities entities, string shortenedDescription)
    {
        var bidCrew = _crews.FirstOrDefault(cr => 
            cr.EntityNo == billingPeriodItem.CrewEntityNo && 
            cr.SeqNo == billingPeriodItem.CrewSeqNo) ?? new();
        
        var crewSizeSuffix = bidCrew.CrewSize > 1 ? $" ({bidCrew.CrewSize})" : "";
        var description = $"{shortenedDescription}: {bidCrew.JobDesc}{crewSizeSuffix}";
        
        return description.TrimStart(' ', ':');
    }

    private string FormatExpenseDescription(ProjectBillingPeriodItemModel billingPeriodItem, 
        RelatedEntities entities, string shortenedDescription)
    {
        var bidExpense = _bidExpenses.FirstOrDefault(bdExp => 
            bdExp.EntityNo == billingPeriodItem.ExpenseEntityNo && 
            bdExp.SeqNo == billingPeriodItem.ExpenseSeqNo) ?? new();
        
        var expenseCode = _expenseCode.FirstOrDefault(ec => ec.ExpCd == bidExpense.ExpenseCode) ?? new();
        
        var operationalPrefix = bidExpense.Category == "O" ? "**" : "";
        var notesSuffix = !string.IsNullOrWhiteSpace(bidExpense.Notes) ? $" - {bidExpense.Notes.Trim()}" : "";
        
        return $"{shortenedDescription} Expense: {operationalPrefix}{expenseCode.Description}{notesSuffix}".Trim();
    }

    private string FormatEquipmentDescription(string shortenedDescription)
    {
        return string.IsNullOrWhiteSpace(shortenedDescription) ? "Equipment" : $"{shortenedDescription} Equipment";
    }

    private class RelatedEntities
    {
        public PhaseModel? BidPhase { get; set; }
        public PhaseModel? Crew { get; set; }
        public PhaseModel? Expense { get; set; }
        public PhaseModel? Equipment { get; set; }
    }
} 