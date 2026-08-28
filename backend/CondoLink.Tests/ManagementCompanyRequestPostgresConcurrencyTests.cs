using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class ManagementCompanyRequestPostgresConcurrencyTests
{
    private static string? Connection=>Environment.GetEnvironmentVariable("COMVY_TEST_POSTGRES");
    [Fact] public async Task Simultaneous_first_acknowledgement_has_one_event()
    {
        if(Connection is null)return;var id=await Seed(ManagementCompanyRequestStatus.Submitted);
        var outcomes=await Task.WhenAll(Acknowledge(id),Acknowledge(id));Assert.Equal(1,outcomes.Count(x=>x));
        await using var db=Db();Assert.Equal(ManagementCompanyRequestStatus.Acknowledged,(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id)).Status);Assert.Equal(1,await db.ManagementCompanyRequestHistories.CountAsync(x=>x.RequestId==id&&x.EventType==ManagementCompanyRequestEventType.Acknowledged));
    }
    [Fact] public async Task Cancel_and_complete_race_has_one_terminal_winner()
    {
        if(Connection is null)return;var id=await Seed(ManagementCompanyRequestStatus.InProgress);var outcomes=await Task.WhenAll(Terminal(id,true),Terminal(id,false));Assert.Equal(1,outcomes.Count(x=>x));
        await using var db=Db();var r=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);Assert.True(r.IsTerminal);Assert.Equal(1,await db.ManagementCompanyRequestHistories.CountAsync(x=>x.RequestId==id&&(x.EventType==ManagementCompanyRequestEventType.Completed||x.EventType==ManagementCompanyRequestEventType.Cancelled)));
    }
    private static async Task<bool> Acknowledge(Guid id){await using var db=Db();var r=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);var old=r.Status;var user=r.CreatedByUserId;r.Acknowledge(user,DateTime.UtcNow);db.ManagementCompanyRequestHistories.Add(new(id,ManagementCompanyRequestEventType.Acknowledged,old,r.Status,user,null,DateTime.UtcNow));try{await db.SaveChangesAsync();return true;}catch(DbUpdateException){return false;}}
    private static async Task<bool> Terminal(Guid id,bool complete){await using var db=Db();var r=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);var old=r.Status;var user=r.CreatedByUserId;if(complete)r.Complete(user,DateTime.UtcNow);else r.Cancel(user,"concorrência",DateTime.UtcNow);db.ManagementCompanyRequestHistories.Add(new(id,complete?ManagementCompanyRequestEventType.Completed:ManagementCompanyRequestEventType.Cancelled,old,r.Status,user,null,DateTime.UtcNow));try{await db.SaveChangesAsync();return true;}catch(DbUpdateConcurrencyException){return false;}}
    private static async Task<Guid> Seed(ManagementCompanyRequestStatus desired){await using var db=Db();var condo=new Condominium("Concorrência",null,null);var company=new ManagementCompany("Empresa",null,null,null,null);var user=CoreTestSeed.User("Ator",$"ator-{Guid.NewGuid():N}@test.local");var category=new ManagementCompanyRequestCategory(company.Id,"Dúvidas",null,ManagementCompanyRequestFormType.Generic);var r=new ManagementCompanyRequest(condo.Id,company.Id,category.Id,user.Id,ManagementCompanyRequestType.GeneralQuestion);if(desired!=ManagementCompanyRequestStatus.Submitted){r.Acknowledge(user.Id,DateTime.UtcNow);r.TransitionTo(ManagementCompanyRequestStatus.InProgress,DateTime.UtcNow);}db.AddRange(condo,company,user,category,r);await db.SaveChangesAsync();return r.Id;}
    private static AppDbContext Db(){var o=new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options;return new(o);}
}
