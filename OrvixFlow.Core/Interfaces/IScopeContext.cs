using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Interfaces;

/// <summary>
/// Provides the effective data-access scope for the currently authenticated user.
///
/// Visibility rules:
///   SuperAdmin / InternalOperator  → full platform access (IgnoreQueryFilters)
///   CompanyOwner / CompanyAdmin    → all company data (no extra filter)
///   DepartmentManager              → only their assigned department IDs
///   Operator / Viewer              → only their assigned department IDs
/// </summary>
public interface IScopeContext
{
    /// <summary>Current user ID, or <see cref="Guid.Empty"/> when unauthenticated.</summary>
    Guid UserId { get; }

    /// <summary>Active company / tenant ID.</summary>
    Guid CompanyId { get; }

    /// <summary>
    /// When <c>true</c> the user can see the full company dataset.
    /// When <c>false</c> queries MUST be filtered to <see cref="AllowedDepartmentIds"/>.
    /// </summary>
    bool HasCompanyWideAccess { get; }

    /// <summary>
    /// Department IDs the user is allowed to interact with.
    /// Only relevant when <see cref="HasCompanyWideAccess"/> is <c>false</c>.
    /// Returning an empty set means "no department access".
    /// </summary>
    IReadOnlyList<Guid> AllowedDepartmentIds { get; }
}
