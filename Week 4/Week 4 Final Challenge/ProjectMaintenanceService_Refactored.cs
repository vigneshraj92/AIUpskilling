using AutoMapper;
using ClairTourTiny.Core.Helpers;
using ClairTourTiny.Core.Interfaces;
using ClairTourTiny.Core.Models.ProjectMaintenance;
using ClairTourTiny.Infrastructure;
using ClairTourTiny.Infrastructure.Dto.ProjectMaintenance;
using ClairTourTiny.Infrastructure.Models.ProjectMaintenance;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ClairTourTiny.Core.Services
{
    public class ProjectMaintenanceService : IProjectMaintenanceService
    {
        private readonly ClairTourTinyContext _dbContext;
        private readonly IMapper _mapper;
        private readonly ILogger<ProjectMaintenanceService> _logger;
        private readonly IProjectMaintenanceHelper _pjtHelper;
        private readonly IProjectDataPointsService _projectDataPointsService;

        public ProjectMaintenanceService(ClairTourTinyContext clairTourTinyContext, IMapper mapper, ILogger<ProjectMaintenanceService> logger, IProjectMaintenanceHelper pjtHelper, IProjectDataPointsService projectDataPointsService)
        {
            _dbContext = clairTourTinyContext;
            _mapper = mapper;
            _pjtHelper = pjtHelper;
            _logger = logger;
            _projectDataPointsService = projectDataPointsService;
        }

        public async Task<int> GetNextAvailableProjectNumber()
        {
            var param = new SqlParameter("@nextProjectNumber", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output,
            };
            await _dbContext.ExecuteStoredProcedureNonQueryOutputParamAsync("get_next_project_number", param);
            if (param?.Value != DBNull.Value)
            {
                return Convert.ToInt32(param?.Value ?? 0);
            }
            return -1;
        }

        public async Task<List<PhaseModel>> GetPhases(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var phasesDtos = await _dbContext.ExecuteStoredProcedureAsync<PhaseDto>("pm2_get_phases_new", param);
            return _mapper.Map<List<PhaseModel>>(phasesDtos);
        }

        public async Task<List<ProjectPurchaseModel>> GetPurchases(string entityNo, int? selectedPo = null)
        {
            try
            {
                var param = new SqlParameter("@entityno", entityNo);
                var purchaseOrdersDtos = await _dbContext.ExecuteStoredProcedureAsync<PurchaseDto>("Get_Purchase_Orders_By_Project", param);
                var purchaseOrders = _mapper.Map<List<ProjectPurchaseModel>>(purchaseOrdersDtos);
                if (selectedPo.HasValue && selectedPo.Value > 0)
                {
                    var selectedPurchase = purchaseOrders.FirstOrDefault(po => po.PONumber == selectedPo.Value);
                    if (selectedPurchase != null)
                    {
                        selectedPurchase.IsNewlyAdded = true;
                        purchaseOrders.Remove(selectedPurchase);
                        purchaseOrders.Insert(0, selectedPurchase);
                    }
                }
                return purchaseOrders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                throw new Exception($"Failed to fetch Purchases for EntityNo: {entityNo}");
            }
        }

        public async Task<int> AddNewPOAsync(string entityNo, string poDescription)
        {
            try
            {
                var sqlParams = new SqlParameter[]
                {
                    new("@entityno", entityNo),
                    new("@poDescription", poDescription),
                    new("@newPONumber", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output,
                    }
                };
                await _dbContext.ExecuteStoredProcedureNonQueryOutputParamAsync("Create_Purchase_Order_Blank", sqlParams);
                if (sqlParams[2]?.Value != DBNull.Value)
                {
                    return Convert.ToInt32(sqlParams[2]?.Value ?? 0);
                }
                return -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                throw new Exception($"Failed to Add new Purchase Order for EntityNo: {entityNo}");
            }
        }

        public async Task<List<ProjectEquipmentModel>> GetEquipments(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var equipmentDtos = await _dbContext.ExecuteStoredProcedureAsync<EquipmentDto>("pm2_get_equipment_new", param);
            return _mapper.Map<List<ProjectEquipmentModel>>(equipmentDtos);
        }

        public async Task<List<ProjectEquipmentSubhireModel>> GetEquipmentSubhires(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var subhireDtos = await _dbContext.ExecuteStoredProcedureAsync<EquipmentSubhireDto>("pm2_get_equipment_subhires", param);
            var subhires = _mapper.Map<List<ProjectEquipmentSubhireModel>>(subhireDtos);

            var projects = await this.GetPhases(entityNo);
            var equipments = await this.GetEquipments(entityNo);
            subhires.ForEach(subhire =>
            {
                var part = equipments.Find(e => e.Entityno == entityNo && e.Partno == subhire.PartNo);
                subhire.PartDescription = part?.PartDescription ?? "THIS PART NO LONGER ORDERED ON THIS PROJECT.";
                var equipmentPhase = projects.Find(p => p.EntityNo == subhire.EntityNo);
                if (equipmentPhase != null)
                {
                    var baseEntityNo = $"{entityNo}-$U3-{equipmentPhase.Agency}-{subhire.VendorNo}.{subhire.SiteNo}";
                    var inbound = equipments.Find(e => e.Entityno == $"{baseEntityNo}-IN" && e.Partno == subhire.PartNo);
                    if (inbound != null)
                        subhire.TransferInboundEntityno = inbound.Entityno;
                    var outbound = equipments.Find(e => e.Entityno == $"{baseEntityNo}-OUT" && e.Partno == subhire.PartNo);
                    if (outbound != null)
                        subhire.TransferOutboundEntityno = outbound.Entityno;
                    subhire.TransferLinkedPhases = !string.IsNullOrEmpty(subhire.TransferInboundEntityno);
                }
            });
            return subhires;
        }

        public async Task<BidSummaryResponse> GetBidSummaryData(string entityNo)
        {
            var phases = await this.GetPhases(entityNo);
            var billingItems = await this.GetBillingItems(entityNo);
            var billingPeriods = await this.GetBillingPeriods(entityNo);
            var billingPeriodItems = await this.GetBillingPeriodItems(entityNo);
            var bidExpenses = await this.GetBidExpenses(entityNo);
            var crews = await this.GetCrews(entityNo);
            var expenseCodes = await _projectDataPointsService.GetExpenseCodes();
            var jobTypes = await _projectDataPointsService.GetJobTypes();
            var propertyTypes = await _projectDataPointsService.GetPropertyTypes();
            var bidSummaryHelper = new BidSummaryHelper(phases, billingItems, billingPeriods, billingPeriodItems, bidExpenses, crews, expenseCodes.ToList(), jobTypes.ToList(), propertyTypes.ToList(), _dbContext);
            return bidSummaryHelper.GetBidSummaryData(entityNo);
        }


        public async Task<List<ProjectBidExpenseModel>> GetBidExpenses(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var bidExpenseDtos = await _dbContext.ExecuteStoredProcedureAsync<BidExpenseDto>("pm2_get_bid_expenses", param);
            return _mapper.Map<List<ProjectBidExpenseModel>>(bidExpenseDtos);
        }

        public async Task<List<ProjectRfiModel>> GetRfis(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var rfiDtos = await _dbContext.ExecuteStoredProcedureAsync<RfiDto>("pm2_get_RFIs", param);
            return _mapper.Map<List<ProjectRfiModel>>(rfiDtos);
        }

        public async Task<List<ProjectNoteModel>> GetNotes(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var noteDtos = await _dbContext.ExecuteStoredProcedureAsync<NoteDto>("pm2_get_notes", param);
            return _mapper.Map<List<ProjectNoteModel>>(noteDtos);
        }

        public async Task<List<ProjectAssignedCrewOtModel>> GetAssignedCrewOtData(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var assignedCrewOtDtos = await _dbContext.ExecuteStoredProcedureAsync<AssignedCrewOtDto>("pm2_get_assigned_crew_OT_data", param);
            return _mapper.Map<List<ProjectAssignedCrewOtModel>>(assignedCrewOtDtos);
        }

        public async Task<List<ProjectBillingItemModel>> GetBillingItems(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var billingItemDtos = await _dbContext.ExecuteStoredProcedureAsync<BillingItemDto>("pm2_get_project_billing_items", param);
            return _mapper.Map<List<ProjectBillingItemModel>>(billingItemDtos);
        }

        public async Task<List<ProjectPartBidValueModel>> GetPartBids(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var partBidDtos = await _dbContext.ExecuteStoredProcedureAsync<PartBidValueDto>("pm2_get_part_bid_values_on_project", param);
            return _mapper.Map<List<ProjectPartBidValueModel>>(partBidDtos);
        }

        public async Task<List<ProjectProductionScheduleModel>> GetProductionSchedules(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var productionScheduleDtos = await _dbContext.ExecuteStoredProcedureAsync<ProductionScheduleDto>("pm2_get_project_production_schedule", param);
            return _mapper.Map<List<ProjectProductionScheduleModel>>(productionScheduleDtos);
        }

        public async Task<List<ProjectAssignedCrewModel>> GetAssignedCrews(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var assignedCrewDtos = await _dbContext.ExecuteStoredProcedureAsync<AssignedCrewDto>("pm2_get_assigned_crew", param);
            return _mapper.Map<List<ProjectAssignedCrewModel>>(assignedCrewDtos);
        }

        public async Task<List<ProjectCrewModel>> GetCrews(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var crewDtos = await _dbContext.ExecuteStoredProcedureAsync<CrewDto>("pm2_get_crew", param);
            var crews = _mapper.Map<List<ProjectCrewModel>>(crewDtos);
            var crewJobTypes = crews.Select(crew => crew.JobType).ToList();
            var assignedCrews = await this.GetAssignedCrews(entityNo);
            var assignedCrewOtData = await this.GetAssignedCrewOtData(entityNo);
            var jobTypes = _dbContext.JobTypesInMyDivisions.Where(e => crewJobTypes.Contains(e.Jobtype)).Select(e => new { e.Jobtype, e.Hours }).ToList();
            foreach (var crew in crews)
            {
                var assignedCrewData = assignedCrews.Where(ac => ac.EntityNo == crew.EntityNo && ac.JobType == crew.JobType && ac.EmpLineNo == crew.EmpLineNo)?.ToList();
                assignedCrewData?.ForEach(e =>
                {
                    e.AssignedCrewOt = assignedCrewOtData.Where(ot => ot.EmpNo == e.EmpNo && ot.EntityNo == e.EntityNo && ot.JobType == e.JobType && ot.FromDate == e.FromDate)?.ToList();
                });
                crew.AssignedCrew = assignedCrewData ?? [];
                var jobType = jobTypes.FirstOrDefault(j => j.Jobtype == crew.JobType);
                crew.DailyBill = (jobType != null) ? crew.EstRate * jobType.Hours : 0;
                crew.WeeklyBill = (jobType != null) ? crew.EstRate * jobType.Hours * 7 : 0;
            }
            return crews;
        }

        public async Task<List<ProjectBillingPeriodModel>> GetBillingPeriods(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var billingPeriodDtos = await _dbContext.ExecuteStoredProcedureAsync<BillingPeriodDto>("pm2_get_project_billing_periods", param);
            return _mapper.Map<List<ProjectBillingPeriodModel>>(billingPeriodDtos);
        }

        public async Task<List<ProjectBillingPeriodItemModel>> GetBillingPeriodItems(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var billingPeriodItemDtos = await _dbContext.ExecuteStoredProcedureAsync<BillingPeriodItemDto>("pm2_get_project_billing_period_items_new", param);
            return _mapper.Map<List<ProjectBillingPeriodItemModel>>(billingPeriodItemDtos);
        }

        public async Task<List<ProjectClientShippingAddressModel>> GetClientShippingAddresses(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var clientAddressDtos = await _dbContext.ExecuteStoredProcedureAsync<ClientShippingAddressDto>("pm2_get_project_client_shipping_addresses", param);
            return _mapper.Map<List<ProjectClientShippingAddressModel>>(clientAddressDtos);
        }

        public async Task<List<ProjectPartModel>> GetParts(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var partDtos = await _dbContext.ExecuteStoredProcedureAsync<PartDto>("pm2_get_parts_on_project", param);
            return _mapper.Map<List<ProjectPartModel>>(partDtos);
        }

        public async Task<List<ProjectClientContactModel>> GetClientContacts(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var clientContactDtos = await _dbContext.ExecuteStoredProcedureAsync<ClientContactDto>("pm2_get_project_client_contacts", param);
            return _mapper.Map<List<ProjectClientContactModel>>(clientContactDtos);
        }

        public async Task<List<ProjectClientAddressModel>> GetClientAddresses(string entityNo)
        {
            var param = new SqlParameter("@entityno", entityNo);
            var clientAddressDtos = await _dbContext.ExecuteStoredProcedureAsync<ClientAddressDto>("pm2_get_project_client_addresses", param);
            return _mapper.Map<List<ProjectClientAddressModel>>(clientAddressDtos);
        }

        public async Task<List<ProjectShipmentModel>> GetShipments(string entityNo)
        {
            var sql = @"
                DECLARE @p varchar(50) = '%-[0-9]%-%'
                SELECT 
                    SUBSTRING(s.entityno, 0, PATINDEX(@p, s.entityno) + PATINDEX('%-%', SUBSTRING(s.entityno, PATINDEX(@p, s.entityno) + 1, 100))) as ProjectLegNo,
                    s.entityno as EntityNo, 
                    g.entitydesc as EntityDesc,
                    s.ShipDate,
                    s.DestinationName as Destination,
                    s.City,
                    s.State,
                    p.MasterTrackingNumber as TrackingNo,
                    s.Amount as EstimatedCost,
                    s.Amount * 1.25 as Cost,
                    s.idShippingRequest as ShippingRequestID,
                    s.ServiceTypeDisplayName as ServiceType
                FROM dbo.ShippingRequestForShipmentVault s
                LEFT JOIN dbo.glentities g ON g.entityno = s.entityno
                LEFT JOIN dbo.ShippingPackages p ON p.idShippingRequest = s.idShippingRequest
                WHERE s.entityno LIKE @entityNo + '-%'";
            
            var parameter = new SqlParameter("@entityNo", entityNo);
            var shipmentDtos = await _dbContext.ExecuteSqlQueryAsync<ShipmentDto>(sql, parameter);
            return _mapper.Map<List<ProjectShipmentModel>>(shipmentDtos);
        }

        public async Task<BottleneckCalculationResponse> CalculateBottlenecks(BottleneckCalculationRequest request)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = new BottleneckCalculationResponse();

            try
            {
                if (request.EquipmentItems == null || !request.EquipmentItems.Any())
                {
                    throw new ArgumentException("Equipment items list cannot be empty");
                }

                // Get root project number from the first equipment item
                var rootProjectNumber = GetRootProjectNumber(request.EquipmentItems.First().EntityNo);

                // Build parts subquery for database queries
                var warehouseParts = request.EquipmentItems
                    .Where(item => !string.IsNullOrEmpty(item.PartNo) && !string.IsNullOrEmpty(item.Warehouse))
                    .Select(item => new { Warehouse = item.Warehouse.ToUpper(), Partno = item.PartNo.ToUpper() })
                    .Distinct()
                    .ToList();

                if (!warehouseParts.Any())
                {
                    throw new ArgumentException("No valid warehouse/part combinations found");
                }

                var warehousePartSelectRows = warehouseParts
                    .Select(wp => $"select warehouse = '{wp.Warehouse}', partno = '{wp.Partno}'");

                var partsSubquery = $"({string.Join(" union all ", warehousePartSelectRows)})";

                // Get base inventory and external demands
                var (baseInventory, externalDemands) = await GetInventoryAndDemands(partsSubquery, rootProjectNumber);

                // Calculate bottlenecks for each equipment item
                foreach (var equipmentItem in request.EquipmentItems)
                {
                    var result = await CalculateBottleneckForItem(equipmentItem, baseInventory, externalDemands, request.EquipmentItems);
                    response.Results.Add(result);
                }

                response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return response;
            }
            catch (Exception ex)
            {
                response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                response.Results.Add(new BottleneckResult
                {
                    Status = "Error",
                    ErrorMessage = ex.Message
                });
                throw;
            }
        }

        private async Task<(List<BaseInventoryDto> baseInventory, List<ExternalDemandDto> externalDemands)> GetInventoryAndDemands(string partsSubquery, string rootProjectNumber)
        {
            // Query for base inventory
            var baseInventorySql = $@"
                select p.warehouse, p.partno, qty = isnull(wpq.totalqty, 0)
                from {partsSubquery} p
                left outer join dbo.warehouse_part_qty wpq with (noexpand) on wpq.bld = p.warehouse and wpq.partno = p.partno";

            // Query for external demands
            var externalDemandsSql = $@"
                select warehouse = t.bld, t.partno, t.fromdate, qty = sum(t.qty)
                from job_budgets_parts_transactions t
                join {partsSubquery} p on t.bld = p.warehouse and t.partno = p.partno
                where t.entityno <> '{rootProjectNumber}'
                and t.entityno not like '{rootProjectNumber}%'
                group by t.bld, t.partno, t.fromdate";

            // Use raw SQL queries without EF tracking
            var baseInventoryDtos = new List<BaseInventoryDto>();
            var externalDemandsDtos = new List<ExternalDemandDto>();

            // Execute base inventory query
            using (var command = _dbContext.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = baseInventorySql;
                command.CommandType = System.Data.CommandType.Text;

                if (command.Connection.State != System.Data.ConnectionState.Open)
                    await command.Connection.OpenAsync();

                using (var result = await command.ExecuteReaderAsync())
                {
                    while (await result.ReadAsync())
                    {
                        baseInventoryDtos.Add(new BaseInventoryDto
                        {
                            Warehouse = result.GetString(result.GetOrdinal("warehouse")),
                            Partno = result.GetString(result.GetOrdinal("partno")),
                            Qty = result.GetInt32(result.GetOrdinal("qty"))
                        });
                    }
                }
            }

            // Execute external demands query
            using (var command = _dbContext.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = externalDemandsSql;
                command.CommandType = System.Data.CommandType.Text;

                if (command.Connection.State != System.Data.ConnectionState.Open)
                    await command.Connection.OpenAsync();

                using (var result = await command.ExecuteReaderAsync())
                {
                    while (await result.ReadAsync())
                    {
                        externalDemandsDtos.Add(new ExternalDemandDto
                        {
                            Warehouse = result.GetString(result.GetOrdinal("warehouse")),
                            Partno = result.GetString(result.GetOrdinal("partno")),
                            FromDate = result.GetDateTime(result.GetOrdinal("fromdate")),
                            Qty = result.GetInt32(result.GetOrdinal("qty"))
                        });
                    }
                }
            }

            return (baseInventoryDtos, externalDemandsDtos);
        }

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

        private string GetRootProjectNumber(string entityNo)
        {
            // Extract root project number from entity number
            // This is a simplified version - you may need to adjust based on your entity number format
            var parts = entityNo.Split('-');
            return parts[0];
        }

        private class DemandEvent
        {
            public string Source { get; set; } = string.Empty;
            public DateTime FromDate { get; set; }
            public int Quantity { get; set; }
        }

        public async Task<bool> SubmitPhases(string entityNo, ProjectSaveModel model)
        {
            bool result = false;
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                await _dbContext.ExecuteStoredProcedureNonQueryAsync("clear_pm2_temp_tables", []);
                await _pjtHelper.ProcessAllBulkSaves(model, _mapper, _dbContext);
                await _dbContext.ExecuteStoredProcedureNonQueryAsync("commit_project_maintenance", []);
                await transaction.CommitAsync();
                result = true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
            return result;

        }
    }
}
