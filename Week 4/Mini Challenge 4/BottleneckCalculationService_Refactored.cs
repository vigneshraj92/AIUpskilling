using ClairTourTiny.Core.Models.ProjectMaintenance;
using ClairTourTiny.Infrastructure.Dto.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClairTourTiny.Core.Services
{
    /// <summary>
    /// Handles bottleneck calculations for equipment items with improved separation of concerns
    /// </summary>
    public class BottleneckCalculationService : IBottleneckCalculationService
    {
        private readonly ILogger<BottleneckCalculationService> _logger;
        private readonly IDemandEventFactory _demandEventFactory;
        private readonly IBottleneckCalculator _bottleneckCalculator;

        public BottleneckCalculationService(
            ILogger<BottleneckCalculationService> logger,
            IDemandEventFactory demandEventFactory,
            IBottleneckCalculator bottleneckCalculator)
        {
            _logger = logger;
            _demandEventFactory = demandEventFactory;
            _bottleneckCalculator = bottleneckCalculator;
        }

        /// <summary>
        /// Calculates bottleneck for a single equipment item
        /// </summary>
        public async Task<BottleneckResult> CalculateBottleneckForItemAsync(
            EquipmentBottleneckItem equipmentItem,
            List<BaseInventoryDto> baseInventory,
            List<ExternalDemandDto> externalDemands,
            List<EquipmentBottleneckItem> allEquipmentItems)
        {
            var calculationContext = new BottleneckCalculationContext
            {
                EquipmentItem = equipmentItem,
                BaseInventory = baseInventory,
                ExternalDemands = externalDemands,
                AllEquipmentItems = allEquipmentItems
            };

            try
            {
                var result = InitializeBottleneckResult(equipmentItem);
                
                // Validate inventory data
                var inventoryValidation = await ValidateInventoryDataAsync(calculationContext);
                if (!inventoryValidation.IsValid)
                {
                    return CreateErrorResult(result, inventoryValidation.ErrorMessage);
                }

                // Collect all demand events
                var demandEvents = await CollectDemandEventsAsync(calculationContext);
                
                // Calculate bottleneck values
                var bottleneckValues = await _bottleneckCalculator.CalculateBottleneckValuesAsync(
                    calculationContext, demandEvents);

                // Populate result with calculated values
                PopulateBottleneckResult(result, bottleneckValues, demandEvents.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating bottleneck for equipment item: {EntityNo}", 
                    equipmentItem.EntityNo);
                return CreateErrorResult(InitializeBottleneckResult(equipmentItem), ex.Message);
            }
        }

        private BottleneckResult InitializeBottleneckResult(EquipmentBottleneckItem equipmentItem)
        {
            return new BottleneckResult
            {
                EntityNo = equipmentItem.EntityNo,
                PartNo = equipmentItem.PartNo,
                Warehouse = equipmentItem.Warehouse,
                StartDate = equipmentItem.StartDate,
                EndDate = equipmentItem.EndDate
            };
        }

        private async Task<ValidationResult> ValidateInventoryDataAsync(BottleneckCalculationContext context)
        {
            var baseInventoryRow = context.BaseInventory.Find(d => 
                d.Warehouse == context.EquipmentItem.Warehouse && 
                d.Partno == context.EquipmentItem.PartNo);

            if (baseInventoryRow == null)
            {
                return ValidationResult.Failure("No base inventory found for this part/warehouse combination");
            }

            return ValidationResult.Success();
        }

        private async Task<List<DemandEvent>> CollectDemandEventsAsync(BottleneckCalculationContext context)
        {
            var demandEvents = new List<DemandEvent>();

            // Collect different types of demand events
            var internalDemands = await _demandEventFactory.CreateInternalDemandsAsync(context);
            var externalDemands = await _demandEventFactory.CreateExternalDemandsAsync(context);
            var earlyCheckoutDemands = await _demandEventFactory.CreateEarlyCheckoutDemandsAsync(context);
            var returningDemands = await _demandEventFactory.CreateReturningDemandsAsync(context);

            demandEvents.AddRange(internalDemands);
            demandEvents.AddRange(externalDemands);
            demandEvents.AddRange(earlyCheckoutDemands);
            demandEvents.AddRange(returningDemands);

            return demandEvents;
        }

        private void PopulateBottleneckResult(BottleneckResult result, BottleneckValues values, int demandEventCount)
        {
            result.BaseInventory = values.BaseQuantity;
            result.DemandEventCount = demandEventCount;
            result.MaxDemandInDateRange = values.MaxDemandInDateRange;
            result.MaxDemandOnStartDate = values.MaxDemandOnStartDate;
            result.MaxDemandInFirstWeek = values.MaxDemandInFirstWeek;
            result.Bottleneck = values.Bottleneck;
            result.Bottleneck1d = values.Bottleneck1d;
            result.Bottleneck1w = values.Bottleneck1w;
        }

        private BottleneckResult CreateErrorResult(BottleneckResult result, string errorMessage)
        {
            result.Status = "Error";
            result.ErrorMessage = errorMessage;
            return result;
        }
    }

    /// <summary>
    /// Parameter object for bottleneck calculation context
    /// </summary>
    public class BottleneckCalculationContext
    {
        public EquipmentBottleneckItem EquipmentItem { get; set; } = null!;
        public List<BaseInventoryDto> BaseInventory { get; set; } = new();
        public List<ExternalDemandDto> ExternalDemands { get; set; } = new();
        public List<EquipmentBottleneckItem> AllEquipmentItems { get; set; } = new();
    }

    /// <summary>
    /// Factory for creating different types of demand events
    /// </summary>
    public class DemandEventFactory : IDemandEventFactory
    {
        public async Task<List<DemandEvent>> CreateInternalDemandsAsync(BottleneckCalculationContext context)
        {
            return context.AllEquipmentItems
                .Where(item => IsInternalDemand(item, context.EquipmentItem))
                .Select(item => new DemandEvent
                {
                    Source = "leaving",
                    FromDate = item.StartDate,
                    Quantity = CalculateInternalDemandQuantity(item)
                })
                .ToList();
        }

        public async Task<List<DemandEvent>> CreateExternalDemandsAsync(BottleneckCalculationContext context)
        {
            return context.ExternalDemands
                .Where(d => IsMatchingWarehousePart(d, context.EquipmentItem))
                .Select(d => new DemandEvent
                {
                    Source = "external",
                    FromDate = d.FromDate,
                    Quantity = d.Qty
                })
                .ToList();
        }

        public async Task<List<DemandEvent>> CreateEarlyCheckoutDemandsAsync(BottleneckCalculationContext context)
        {
            return context.AllEquipmentItems
                .Where(item => IsEarlyCheckout(item, context.EquipmentItem))
                .Select(item => new DemandEvent
                {
                    Source = "early",
                    FromDate = DateTime.Today,
                    Quantity = item.CheckedOut
                })
                .ToList();
        }

        public async Task<List<DemandEvent>> CreateReturningDemandsAsync(BottleneckCalculationContext context)
        {
            return context.AllEquipmentItems
                .Where(item => IsReturningDemand(item))
                .Select(item => new DemandEvent
                {
                    Source = "returning",
                    FromDate = item.EndDate.AddDays(1),
                    Quantity = -CalculateReturningQuantity(item)
                })
                .Where(d => d.Quantity != 0)
                .ToList();
        }

        private bool IsInternalDemand(EquipmentBottleneckItem item, EquipmentBottleneckItem currentItem)
        {
            return item.PartNo == currentItem.PartNo &&
                   item.Warehouse == currentItem.Warehouse &&
                   item.EntityNo != currentItem.EntityNo;
        }

        private bool IsMatchingWarehousePart(ExternalDemandDto demand, EquipmentBottleneckItem item)
        {
            return demand.Warehouse == item.Warehouse && demand.Partno == item.PartNo;
        }

        private bool IsEarlyCheckout(EquipmentBottleneckItem item, EquipmentBottleneckItem currentItem)
        {
            return item.PartNo == currentItem.PartNo &&
                   item.Warehouse == currentItem.Warehouse &&
                   item.StartDate >= DateTime.Today &&
                   item.CheckedOut > 0;
        }

        private bool IsReturningDemand(EquipmentBottleneckItem item)
        {
            return item.EndDate >= DateTime.Today;
        }

        private int CalculateInternalDemandQuantity(EquipmentBottleneckItem item)
        {
            return item.StartDate >= DateTime.Today ? item.Quantity - item.CheckedOut : item.CheckedOut;
        }

        private int CalculateReturningQuantity(EquipmentBottleneckItem item)
        {
            return item.StartDate >= DateTime.Today ? item.Quantity : item.CheckedOut;
        }
    }

    /// <summary>
    /// Calculator for bottleneck values using strategy pattern
    /// </summary>
    public class BottleneckCalculator : IBottleneckCalculator
    {
        private readonly IDateRangeCalculator _dateRangeCalculator;
        private readonly IDemandAnalyzer _demandAnalyzer;

        public BottleneckCalculator(IDateRangeCalculator dateRangeCalculator, IDemandAnalyzer demandAnalyzer)
        {
            _dateRangeCalculator = dateRangeCalculator;
            _demandAnalyzer = demandAnalyzer;
        }

        public async Task<BottleneckValues> CalculateBottleneckValuesAsync(
            BottleneckCalculationContext context, 
            List<DemandEvent> demandEvents)
        {
            var dateRange = _dateRangeCalculator.CalculateDateRange(context.EquipmentItem);
            var baseQuantity = GetBaseQuantity(context);
            var demandAnalysis = _demandAnalyzer.AnalyzeDemands(demandEvents, dateRange);

            return new BottleneckValues
            {
                BaseQuantity = baseQuantity,
                MaxDemandInDateRange = demandAnalysis.MaxDemandInDateRange,
                MaxDemandOnStartDate = demandAnalysis.MaxDemandOnStartDate,
                MaxDemandInFirstWeek = demandAnalysis.MaxDemandInFirstWeek,
                Bottleneck = baseQuantity - demandAnalysis.MaxDemandInDateRange,
                Bottleneck1d = baseQuantity - demandAnalysis.MaxDemandOnStartDate,
                Bottleneck1w = baseQuantity - demandAnalysis.MaxDemandInFirstWeek
            };
        }

        private int GetBaseQuantity(BottleneckCalculationContext context)
        {
            var baseInventoryRow = context.BaseInventory.Find(d => 
                d.Warehouse == context.EquipmentItem.Warehouse && 
                d.Partno == context.EquipmentItem.PartNo);
            
            return baseInventoryRow?.Qty ?? 0;
        }
    }

    /// <summary>
    /// Calculates date ranges for bottleneck analysis
    /// </summary>
    public class DateRangeCalculator : IDateRangeCalculator
    {
        public DateRange CalculateDateRange(EquipmentBottleneckItem equipmentItem)
        {
            var startDate = equipmentItem.StartDate;
            var endOfFirstWeek = startDate.AddDays(6);
            var endDate = equipmentItem.EndDate;
            var lastDateWeCareAbout = endDate > endOfFirstWeek ? endDate : endOfFirstWeek;

            return new DateRange
            {
                StartDate = startDate,
                EndOfFirstWeek = endOfFirstWeek,
                EndDate = endDate,
                LastDateWeCareAbout = lastDateWeCareAbout
            };
        }
    }

    /// <summary>
    /// Analyzes demand events to find maximum demands
    /// </summary>
    public class DemandAnalyzer : IDemandAnalyzer
    {
        public DemandAnalysis AnalyzeDemands(List<DemandEvent> demandEvents, DateRange dateRange)
        {
            var dailyDemandChanges = demandEvents
                .Where(d => d.FromDate <= dateRange.LastDateWeCareAbout)
                .OrderBy(d => d.FromDate)
                .ThenBy(d => d.Quantity)
                .ToList();

            var runningTotal = 0;
            var maxDemandInDateRange = 0;
            var maxDemandInFirstWeek = 0;
            var maxDemandOnOutDate = 0;

            foreach (var demand in dailyDemandChanges)
            {
                runningTotal += demand.Quantity;

                if (demand.FromDate >= dateRange.StartDate && runningTotal > maxDemandInDateRange)
                {
                    maxDemandInDateRange = runningTotal;
                }

                if (demand.FromDate >= dateRange.StartDate && 
                    demand.FromDate <= dateRange.EndOfFirstWeek && 
                    runningTotal > maxDemandInFirstWeek)
                {
                    maxDemandInFirstWeek = runningTotal;
                }

                if (demand.FromDate == dateRange.StartDate && runningTotal > maxDemandOnOutDate)
                {
                    maxDemandOnOutDate = runningTotal;
                }
            }

            return new DemandAnalysis
            {
                MaxDemandInDateRange = maxDemandInDateRange,
                MaxDemandOnStartDate = maxDemandOnOutDate,
                MaxDemandInFirstWeek = maxDemandInFirstWeek
            };
        }
    }

    // Data Transfer Objects
    public class BottleneckValues
    {
        public int BaseQuantity { get; set; }
        public int MaxDemandInDateRange { get; set; }
        public int MaxDemandOnStartDate { get; set; }
        public int MaxDemandInFirstWeek { get; set; }
        public int Bottleneck { get; set; }
        public int Bottleneck1d { get; set; }
        public int Bottleneck1w { get; set; }
    }

    public class DateRange
    {
        public DateTime StartDate { get; set; }
        public DateTime EndOfFirstWeek { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime LastDateWeCareAbout { get; set; }
    }

    public class DemandAnalysis
    {
        public int MaxDemandInDateRange { get; set; }
        public int MaxDemandOnStartDate { get; set; }
        public int MaxDemandInFirstWeek { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static ValidationResult Success() => new() { IsValid = true };
        public static ValidationResult Failure(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
    }

    public class DemandEvent
    {
        public string Source { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public int Quantity { get; set; }
    }

    // Interfaces for dependency injection
    public interface IBottleneckCalculationService
    {
        Task<BottleneckResult> CalculateBottleneckForItemAsync(
            EquipmentBottleneckItem equipmentItem,
            List<BaseInventoryDto> baseInventory,
            List<ExternalDemandDto> externalDemands,
            List<EquipmentBottleneckItem> allEquipmentItems);
    }

    public interface IDemandEventFactory
    {
        Task<List<DemandEvent>> CreateInternalDemandsAsync(BottleneckCalculationContext context);
        Task<List<DemandEvent>> CreateExternalDemandsAsync(BottleneckCalculationContext context);
        Task<List<DemandEvent>> CreateEarlyCheckoutDemandsAsync(BottleneckCalculationContext context);
        Task<List<DemandEvent>> CreateReturningDemandsAsync(BottleneckCalculationContext context);
    }

    public interface IBottleneckCalculator
    {
        Task<BottleneckValues> CalculateBottleneckValuesAsync(
            BottleneckCalculationContext context, 
            List<DemandEvent> demandEvents);
    }

    public interface IDateRangeCalculator
    {
        DateRange CalculateDateRange(EquipmentBottleneckItem equipmentItem);
    }

    public interface IDemandAnalyzer
    {
        DemandAnalysis AnalyzeDemands(List<DemandEvent> demandEvents, DateRange dateRange);
    }
} 