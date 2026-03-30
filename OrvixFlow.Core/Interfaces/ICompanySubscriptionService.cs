using System;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Core.Interfaces;

public interface ICompanySubscriptionService
{
    Task<CompanySubscription> CreateTrialSubscriptionAsync(Guid companyId, Guid planTemplateId);
    Task<CompanySubscription?> GetSubscriptionAsync(Guid companyId);
    Task<CompanySubscription> AssignPlanAsync(Guid companyId, Guid planTemplateId, string? billingInterval = null);
    Task<CompanySubscription> ChangePlanAsync(Guid companyId, Guid newPlanTemplateId, bool immediate = false);
    Task<CompanySubscription> SuspendSubscriptionAsync(Guid companyId);
    Task<CompanySubscription> ReactivateSubscriptionAsync(Guid companyId);
    Task<CompanySubscription> CancelSubscriptionAsync(Guid companyId);
    Task<bool> IsPlanChangeAllowedAsync(Guid companyId, Guid newPlanTemplateId);
}

public class SubscriptionException : Exception
{
    public SubscriptionException(string message) : base(message) { }
}

public class CompanyNotFoundException : SubscriptionException
{
    public Guid CompanyId { get; }
    public CompanyNotFoundException(Guid companyId) : base($"Company {companyId} not found")
    {
        CompanyId = companyId;
    }
}

public class PlanNotFoundException : SubscriptionException
{
    public Guid PlanId { get; }
    public PlanNotFoundException(Guid planId) : base($"Plan {planId} not found")
    {
        PlanId = planId;
    }
}

public class SubscriptionNotFoundException : SubscriptionException
{
    public Guid CompanyId { get; }
    public SubscriptionNotFoundException(Guid companyId) : base($"Subscription for company {companyId} not found")
    {
        CompanyId = companyId;
    }
}

public class SeatLimitExceededException : SubscriptionException
{
    public int CurrentSeats { get; }
    public int MaxSeats { get; }
    public SeatLimitExceededException(int currentSeats, int maxSeats) 
        : base($"Cannot assign plan: {currentSeats} seats exceed plan limit of {maxSeats}")
    {
        CurrentSeats = currentSeats;
        MaxSeats = maxSeats;
    }
}

public class PlanNotActiveException : SubscriptionException
{
    public Guid PlanId { get; }
    public PlanNotActiveException(Guid planId) : base($"Plan {planId} is not active")
    {
        PlanId = planId;
    }
}
