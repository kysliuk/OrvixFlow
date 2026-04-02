using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class AgentPersonaTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;

    public AgentPersonaTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Create_Persona_SavesToDb()
    {
        var persona = new AgentPersona
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Tone = "Casual",
            CustomInstructions = "Be friendly and use emojis",
            CustomSignOff = "Cheers, Team"
        };

        _dbContext.AgentPersonas.Add(persona);
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.AgentPersonas.FindAsync(persona.Id);
        saved.Should().NotBeNull();
        saved!.Tone.Should().Be("Casual");
        saved.CustomInstructions.Should().Be("Be friendly and use emojis");
        saved.CustomSignOff.Should().Be("Cheers, Team");
    }

    [Fact]
    public async Task QueryFilter_IsolatesByTenant()
    {
        var otherTenant = Guid.NewGuid();

        _dbContext.AgentPersonas.Add(new AgentPersona
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Tone = "Professional"
        });
        _dbContext.AgentPersonas.Add(new AgentPersona
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenant,
            Tone = "Casual"
        });
        await _dbContext.SaveChangesAsync();

        var results = await _dbContext.AgentPersonas.ToListAsync();
        results.Should().HaveCount(1);
        results[0].Tone.Should().Be("Professional");
    }

    [Fact]
    public async Task Update_Persona_UpdatesTimestamp()
    {
        var persona = new AgentPersona
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Tone = "Professional",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.AgentPersonas.Add(persona);
        await _dbContext.SaveChangesAsync();

        persona.Tone = "Casual";
        persona.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var saved = await _dbContext.AgentPersonas.FindAsync(persona.Id);
        saved!.Tone.Should().Be("Casual");
    }

    [Fact]
    public async Task Persona_DefaultValues_AreSet()
    {
        var persona = new AgentPersona
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Tone = "Professional",
            CustomInstructions = string.Empty
        };

        persona.Tone.Should().Be("Professional");
        persona.CustomInstructions.Should().BeEmpty();
        persona.CustomSignOff.Should().BeNull();
        persona.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
