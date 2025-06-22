using ClairTourTiny.Core.Models.ProjectMaintenance;
using ClairTourTiny.Infrastructure.Dto.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClairTourTiny.Core.Services
{
    /// <summary>
    /// Original large method before refactoring - 200+ lines with multiple responsibilities
    /// </summary>
    public class ProjectMaintenanceService
    {
        private async Task<BottleneckResult> CalculateBottleneckForItem(
            EquipmentBottleneckItem equipmentItem,
            List<BaseInventoryDto> baseInventory,
            List<ExternalDemandDto> externalDemands,
            List<EquipmentBottleneckItem> allEquipmentItems)
        {
            var result = new BottleneckResult
            {
                EntityNo = equipmentItem.EntityNo,
                PartNo = equipmentItem.PartNo,
                Warehouse = equipmentItem.Warehouse,
                StartDate = equipmentItem.StartDate,
                EndDate = equipmentItem.EndDate
            };

            try
            {
                var erStartDate = equipmentItem.StartDate;
                var erEndOfFirstWeek = erStartDate.AddDays(6);
                var erEndDate = equipmentItem.EndDate;
                var lastDateWeCareAbout = erEndDate > erEndOfFirstWeek ? erEndDate : erEndOfFirstWeek;

                // Collect all demand events
                var partDemands = new List<DemandEvent>();

                // Internal demands - parts leaving in project
                var partsLeaving = allEquipmentItems
                    .Where(item => item.PartNo == equipmentItem.PartNo &&
                                  item.Warehouse == equipmentItem.Warehouse &&
                                  item.EntityNo != equipmentItem.EntityNo)
                    .Select(item => new DemandEvent
                    {
                        Source = "leaving",
                        FromDate = item.StartDate,
                        Quantity = item.StartDate >= DateTime.Today ? item.Quantity - item.CheckedOut : item.CheckedOut
                    });

                // Parts checked out early
                var partsCheckedOutEarly = allEquipmentItems
                    .Where(item => item.PartNo == equipmentItem.PartNo &&
                                  item.Warehouse == equipmentItem.Warehouse &&
                                  item.StartDate >= DateTime.Today &&
                                  item.CheckedOut > 0)
                    .Select(item => new DemandEvent
                    {
                        Source = "early",
                        FromDate = DateTime.Today,
                        Quantity = item.CheckedOut
                    });

                // Parts returning
                var partsReturning = allEquipmentItems
                    .Where(item => item.PartNo == equipmentItem.PartNo &&
                                  item.EndDate >= DateTime.Today)
                    .Select(item => new DemandEvent
                    {
                        Source = "returning",
                        FromDate = item.EndDate.AddDays(1),
                        Quantity = -(item.StartDate >= DateTime.Today ? item.Quantity : item.CheckedOut)
                    })
                    .Where(d => d.Quantity != 0);

                // External demands
                var currentWarehousePartFilter = $"warehouse = '{equipmentItem.Warehouse}' and partno = '{equipmentItem.PartNo}'";
                var externalDemandEvents = externalDemands
                    .Where(d => d.Warehouse == equipmentItem.Warehouse && d.Partno == equipmentItem.PartNo)
                    .Select(d => new DemandEvent
                    {
                        Source = "external",
                        FromDate = d.FromDate,
                        Quantity = d.Qty
                    });

                // Combine all demands
                partDemands.AddRange(partsLeaving);
                partDemands.AddRange(partsCheckedOutEarly);
                partDemands.AddRange(partsReturning);
                partDemands.AddRange(externalDemandEvents);

                // Get base inventory
                var baseInventoryRows = baseInventory.Find(d => d.Warehouse == equipmentItem.Warehouse && d.Partno == equipmentItem.PartNo);
                if (baseInventoryRows == null)
                {
                    result.Status = "No Inventory Data";
                    result.ErrorMessage = "No base inventory found for this part/warehouse combination";
                    return result;
                }

                var baseQuantity = baseInventoryRows.Qty;
                result.BaseInventory = baseQuantity;

                // Calculate daily demand changes
                var dailyDemandChanges = partDemands
                    .Where(d => d.FromDate <= lastDateWeCareAbout)
                    .OrderBy(d => d.FromDate)
                    .ThenBy(d => d.Quantity)
                    .ToList();

                result.DemandEventCount = dailyDemandChanges.Count;

                // Calculate running totals and find maximums
                var maxDemandInDateRange = 0;
                var maxDemandInFirstWeek = 0;
                var maxDemandOnOutDate = 0;
                var runningTotal = 0;

                foreach (var demand in dailyDemandChanges)
                {
                    runningTotal += demand.Quantity;

                    if (demand.FromDate >= erStartDate && runningTotal > maxDemandInDateRange)
                    {
                        maxDemandInDateRange = runningTotal;
                    }

                    if (demand.FromDate >= erStartDate && demand.FromDate <= erEndOfFirstWeek && runningTotal > maxDemandInFirstWeek)
                    {
                        maxDemandInFirstWeek = runningTotal;
                    }

                    if (demand.FromDate == erStartDate && runningTotal > maxDemandOnOutDate)
                    {
                        maxDemandOnOutDate = runningTotal;
                    }
                }

                result.MaxDemandInDateRange = maxDemandInDateRange;
                result.MaxDemandOnStartDate = maxDemandOnOutDate;
                result.MaxDemandInFirstWeek = maxDemandInFirstWeek;

                // Calculate bottleneck values
                result.Bottleneck = baseQuantity - maxDemandInDateRange;
                result.Bottleneck1d = baseQuantity - maxDemandOnOutDate;
                result.Bottleneck1w = baseQuantity - maxDemandInFirstWeek;

                return result;
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private class DemandEvent
        {
            public string Source { get; set; } = string.Empty;
            public DateTime FromDate { get; set; }
            public int Quantity { get; set; }
        }
    }
} 