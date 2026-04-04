import re

path = "OrvixFlow.Infrastructure/OrvixFlow.Infrastructure/Data/Migrations/AddImageSupport/20260403124135_FixMissingSeeds.cs"
with open(path, "r") as f:
    text = f.read()

sql_snippet = """
            var plans = OrvixFlow.Core.Entities.PlanCatalog.BuildPlanSeed();
            foreach (var p in plans)
            {
                var maxSeats = p.MaxSeats.HasValue ? p.MaxSeats.Value.ToString() : "NULL";
                migrationBuilder.Sql($@"
                    INSERT INTO ""PlanTemplates"" 
                    (""Id"", ""Name"", ""Slug"", ""Description"", ""MonthlyPriceCents"", ""YearlyPriceCents"", ""Currency"", ""BillingInterval"", ""MaxSeats"", ""IsActive"", ""IsFree"", ""IsTrialAllowed"", ""TrialDays"", ""LegacyLocked"", ""CreatedAt"", ""IsPubliclyVisible"", ""SortOrder"")
                    VALUES 
                    ('{p.Id}', '{p.Name.Replace("'", "''")}', '{p.Slug}', '{p.Description.Replace("'", "''")}', {p.MonthlyPriceCents}, {p.YearlyPriceCents}, '{p.Currency}', '{p.BillingInterval}', {maxSeats}, {(p.IsActive?"true":"false")}, {(p.IsFree?"true":"false")}, {(p.IsTrialAllowed?"true":"false")}, {p.TrialDays}, {(p.LegacyLocked?"true":"false")}, '{p.CreatedAt:O}', {(p.IsPubliclyVisible?"true":"false")}, {p.SortOrder})
                    ON CONFLICT (""Id"") DO NOTHING;
                ");
            }

            var ents = OrvixFlow.Core.Entities.PlanCatalog.BuildEntitlementsSeed();
            foreach (var e in ents)
            {
                migrationBuilder.Sql($@"
                    INSERT INTO ""PlanEntitlements""
                    (""Id"", ""PlanTemplateId"", ""MaxMonthlyTokens"", ""MaxApiRequestsPerDay"", ""MaxStorageMb"", ""MaxKnowledgeBases"", ""CreatedAt"")
                    VALUES
                    ('{e.Id}', '{e.PlanTemplateId}', {e.MaxMonthlyTokens}, {e.MaxApiRequestsPerDay}, {e.MaxStorageMb}, {e.MaxKnowledgeBases}, '{e.CreatedAt:O}')
                    ON CONFLICT (""Id"") DO NOTHING;
                ");
            }
"""

text = re.sub(r"(protected override void Up\(MigrationBuilder migrationBuilder\)\s*\{)", r"\1" + sql_snippet, text)

with open(path, "w") as f:
    f.write(text)

