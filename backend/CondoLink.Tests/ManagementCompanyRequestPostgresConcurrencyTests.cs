using CondoLink.Api.Common;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.ManagementCompanyRequests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

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
    [Fact] public async Task Concurrent_friendly_identifiers_are_unique_and_all_requests_are_persisted()
    {
        if(Connection is null)return;
        var seed=await SeedIdentifierDependencies();
        var ids=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>CreateWithAnnualIdentifier(seed)));
        await using var db=Db();
        var rows=await db.ManagementCompanyRequests.AsNoTracking().Where(x=>ids.Contains(x.Id)).Select(x=>x.FriendlyIdentifier).ToListAsync();
        Assert.Equal(20,rows.Count);Assert.Equal(20,rows.Distinct().Count());
        Assert.All(rows,x=>Assert.Matches($"^ADM-{DateTime.UtcNow.Year}-[0-9]{{4,}}$",x));
    }
    [Fact] public async Task Concurrent_refresh_attempts_allow_only_one_rotation()
    {
        if(Connection is null)return;
        var services=new ServiceCollection();services.AddLogging();services.AddDbContext<AppDbContext>(o=>o.UseNpgsql(Connection));services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
        services.Configure<IdentityOptions>(o=>o.Password.RequireNonAlphanumeric=false);
        await using var provider=services.BuildServiceProvider();Guid userId;string raw;
        await using(var scope=provider.CreateAsyncScope()){var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var user=CoreTestSeed.User("Refresh concorrente",$"refresh-{Guid.NewGuid():N}@test.local");var create=await users.CreateAsync(user,"Passw0rd1");Assert.True(create.Succeeded,string.Join(";",create.Errors.Select(x=>x.Description)));userId=user.Id;var session=new AuthenticationSessionService(users,scope.ServiceProvider.GetRequiredService<AppDbContext>(),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Issuer","tests"},{"Jwt:Audience","tests"},{"Jwt:Key","refresh-test-key-with-at-least-32-bytes"},{"Jwt:ExpirationMinutes","60"}}).Build(),Options.Create(new AuthenticationSessionOptions{RefreshTokenDays=30}),TimeProvider.System,scope.ServiceProvider.GetRequiredService<ILogger<AuthenticationSessionService>>() );var response=new DefaultHttpContext().Response;Assert.NotNull(await session.IssueAsync(user,response,default));raw=Uri.UnescapeDataString(response.Headers.SetCookie.Single().Split(';')[0].Split('=',2)[1]);}
        async Task<Login.Response?> Refresh(){await using var scope=provider.CreateAsyncScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var session=new AuthenticationSessionService(users,scope.ServiceProvider.GetRequiredService<AppDbContext>(),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Issuer","tests"},{"Jwt:Audience","tests"},{"Jwt:Key","refresh-test-key-with-at-least-32-bytes"},{"Jwt:ExpirationMinutes","60"}}).Build(),Options.Create(new AuthenticationSessionOptions{RefreshTokenDays=30}),TimeProvider.System,scope.ServiceProvider.GetRequiredService<ILogger<AuthenticationSessionService>>() );return await session.RefreshAsync(raw,new DefaultHttpContext().Response,default);}
        var results=await Task.WhenAll(Refresh(),Refresh());Assert.Equal(1,results.Count(x=>x is not null));
        await using(var scope=provider.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var rows=await db.RefreshSessions.Where(x=>x.UserId==userId).ToListAsync();Assert.Equal(2,rows.Count);Assert.Single(rows,x=>x.RevokedAt is null);Assert.Single(rows,x=>x.RevokedAt is not null);}
    }
    [Fact] public async Task Cancel_and_complete_race_has_one_terminal_winner()
    {
        if(Connection is null)return;var id=await Seed(ManagementCompanyRequestStatus.InProgress);var outcomes=await Task.WhenAll(Terminal(id,true),Terminal(id,false));Assert.Equal(1,outcomes.Count(x=>x));
        await using var db=Db();var r=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);Assert.True(r.IsTerminal);Assert.Equal(1,await db.ManagementCompanyRequestHistories.CountAsync(x=>x.RequestId==id&&(x.EventType==ManagementCompanyRequestEventType.Completed||x.EventType==ManagementCompanyRequestEventType.Cancelled)));
    }

    // Lote 6 — cenário A: duas respostas concorrentes da gestão em WaitingManager, desta vez
    // através da camada de serviço real (InteractAsync), não domínio cru + SaveChanges.
    [Fact] public async Task Concurrent_manager_replies_in_waiting_manager_produce_exactly_one_transition_and_message()
    {
        if(Connection is null)return;
        var(id,managerUserId,_)=await SeedForService(ManagementCompanyRequestStatus.WaitingManager);
        var actor=new ManagementCompanyRequestActor(managerUserId,"Gestor",ManagementCompanyRequestActorKind.Management);
        var outcomes=await Task.WhenAll(
            InteractSafe(id,actor,"Primeira resposta"),
            InteractSafe(id,actor,"Segunda resposta"));
        Assert.Equal(1,outcomes.Count(x=>x));
        await using var db=Db();
        Assert.Equal(ManagementCompanyRequestStatus.InProgress,(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id)).Status);
        Assert.Equal(1,await db.ManagementCompanyRequestHistories.CountAsync(x=>x.RequestId==id&&x.EventType==ManagementCompanyRequestEventType.ManagerResponded));
        Assert.Equal(1,await db.ManagementCompanyRequestMessages.CountAsync(x=>x.RequestId==id));
    }

    // Lote 6 — cenário B: WaitingManager vs Completed a partir de InProgress, via TransitionAsync real.
    [Fact] public async Task Waiting_manager_versus_completed_race_leaves_exactly_one_new_terminal_or_waiting_transition()
    {
        if(Connection is null)return;
        var(id,_,companyUserId)=await SeedForService(ManagementCompanyRequestStatus.InProgress);
        var actor=new ManagementCompanyRequestActor(companyUserId,"Atendente",ManagementCompanyRequestActorKind.ManagementCompany);
        var outcomes=await Task.WhenAll(
            TransitionSafe(id,actor,ManagementCompanyRequestStatus.WaitingManager),
            TransitionSafe(id,actor,ManagementCompanyRequestStatus.Completed));
        Assert.Equal(1,outcomes.Count(x=>x));
        await using var db=Db();
        var r=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);
        Assert.True(r.Status==ManagementCompanyRequestStatus.WaitingManager||r.Status==ManagementCompanyRequestStatus.Completed);
        // Setup mutates the seeded entity directly (no history rows recorded for it, same as the
        // pre-existing Cancel_and_complete_race test above); only the race's winner writes history.
        Assert.Equal(1,await db.ManagementCompanyRequestHistories.CountAsync(x=>x.RequestId==id));
    }

    // Lote 6 — item 4: primeira ciência concorrente via o serviço real (não domínio cru).
    // AcknowledgeAsync nunca deveria lançar para o chamador (swallow por design) mesmo sob corrida real.
    [Fact] public async Task Concurrent_first_view_via_the_service_produces_one_acknowledged_event_without_throwing()
    {
        if(Connection is null)return;
        var(id,_,companyUserId)=await SeedForService(ManagementCompanyRequestStatus.Submitted);
        var actor=new ManagementCompanyRequestActor(companyUserId,"Atendente",ManagementCompanyRequestActorKind.ManagementCompany);
        await Task.WhenAll(AcknowledgeViaService(id,actor),AcknowledgeViaService(id,actor));
        await using var db=Db();
        Assert.Equal(ManagementCompanyRequestStatus.Acknowledged,(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id)).Status);
        Assert.Equal(1,await db.ManagementCompanyRequestHistories.CountAsync(x=>x.RequestId==id&&x.EventType==ManagementCompanyRequestEventType.Acknowledged));
    }

    // Lote 6 — item 15: idempotência de notificação sob concorrência real, não só chamada sequencial dupla.
    [Fact] public async Task Concurrent_notification_dispatch_for_the_same_event_is_deduplicated_by_the_unique_index()
    {
        if(Connection is null)return;
        var(id,_,_)=await SeedForService(ManagementCompanyRequestStatus.Submitted);
        var options=Options.Create(new FirstAccessOptions{FrontendBaseUrl="https://app.test.local"});
        await using var db1=Db();await using var db2=Db();
        var request=await db1.ManagementCompanyRequests.AsNoTracking().SingleAsync(x=>x.Id==id);
        var service1=new ManagementCompanyRequestNotificationService(db1,new NoOpEmailSender(),options);
        var service2=new ManagementCompanyRequestNotificationService(db2,new NoOpEmailSender(),options);
        await Task.WhenAll(service1.NotifyCreatedAsync(request,default),service2.NotifyCreatedAsync(request,default));
        await using var verify=Db();
        Assert.Equal(1,await verify.Notifications.CountAsync(n=>n.ManagementCompanyRequestId==id));
    }

    private static async Task<bool> Acknowledge(Guid id){await using var db=Db();var r=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);var old=r.Status;var user=r.CreatedByUserId;r.Acknowledge(user,DateTime.UtcNow);db.ManagementCompanyRequestHistories.Add(new(id,ManagementCompanyRequestEventType.Acknowledged,old,r.Status,user,null,DateTime.UtcNow));try{await db.SaveChangesAsync();return true;}catch(DbUpdateException){return false;}}
    private static async Task<bool> Terminal(Guid id,bool complete){await using var db=Db();var r=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);var old=r.Status;var user=r.CreatedByUserId;if(complete)r.Complete(user,DateTime.UtcNow);else r.Cancel(user,"concorrência",DateTime.UtcNow);db.ManagementCompanyRequestHistories.Add(new(id,complete?ManagementCompanyRequestEventType.Completed:ManagementCompanyRequestEventType.Cancelled,old,r.Status,user,null,DateTime.UtcNow));try{await db.SaveChangesAsync();return true;}catch(DbUpdateConcurrencyException){return false;}}
    private static async Task<Guid> Seed(ManagementCompanyRequestStatus desired){await using var db=Db();var condo=new Condominium("Concorrência",null,null);var company=new ManagementCompany("Empresa",null,null,null,null);var user=CoreTestSeed.User("Ator",$"ator-{Guid.NewGuid():N}@test.local");var category=new ManagementCompanyRequestCategory(company.Id,"Dúvidas",null,ManagementCompanyRequestFormType.Generic);var r=new ManagementCompanyRequest(condo.Id,company.Id,category.Id,user.Id,ManagementCompanyRequestType.GeneralQuestion);if(desired!=ManagementCompanyRequestStatus.Submitted){r.Acknowledge(user.Id,DateTime.UtcNow);r.TransitionTo(ManagementCompanyRequestStatus.InProgress,DateTime.UtcNow);}db.AddRange(condo,company,user,category,r);await db.SaveChangesAsync();return r.Id;}

    /// <summary>Seed with two real, persisted actors and the question-detail row, for tests that exercise the service layer (which enforces FKs on history/message authors and reads request detail).</summary>
    private static async Task<(Guid RequestId,Guid ManagerUserId,Guid CompanyUserId)> SeedForService(ManagementCompanyRequestStatus desired)
    {
        await using var db=Db();
        var condo=new Condominium("Concorrência Serviço",null,null);
        var company=new ManagementCompany("Empresa",null,null,null,null);
        var manager=CoreTestSeed.User("Gestor",$"gestor-{Guid.NewGuid():N}@test.local");
        var companyUser=CoreTestSeed.User("Atendente",$"atendente-{Guid.NewGuid():N}@test.local");
        companyUser.SetEmailDeliveryEnabled(true);
        var category=new ManagementCompanyRequestCategory(company.Id,"Dúvidas",null,ManagementCompanyRequestFormType.Generic);
        var employee=new ManagementCompanyEmployee(company.Id,companyUser.Id,"Atendimento");
        var r=new ManagementCompanyRequest(condo.Id,company.Id,category.Id,manager.Id,ManagementCompanyRequestType.GeneralQuestion);
        if(desired!=ManagementCompanyRequestStatus.Submitted)
        {
            r.Acknowledge(companyUser.Id,DateTime.UtcNow);
            if(desired!=ManagementCompanyRequestStatus.Acknowledged)r.TransitionTo(ManagementCompanyRequestStatus.InProgress,DateTime.UtcNow);
            if(desired==ManagementCompanyRequestStatus.WaitingManager)r.TransitionTo(ManagementCompanyRequestStatus.WaitingManager,DateTime.UtcNow);
        }
        db.AddRange(condo,company,manager,companyUser,category,employee,r,
            new ManagementCompanyGeneralQuestionRequest(r.Id,"Contrato"),
            new ManagementCompanyRequestCategoryResponsible(category.Id,employee.Id));
        await db.SaveChangesAsync();
        return(r.Id,manager.Id,companyUser.Id);
    }
    private static async Task<(Guid Condo,Guid Company,Guid Category,Guid User)> SeedIdentifierDependencies()
    {await using var db=Db();var condo=new Condominium("Sequência",null,null);var company=new ManagementCompany("Sequência Empresa",null,null,null,null);var user=CoreTestSeed.User("Sequência",$"seq-{Guid.NewGuid():N}@test.local");var category=new ManagementCompanyRequestCategory(company.Id,"Sequência",null,ManagementCompanyRequestFormType.Generic);db.AddRange(condo,company,user,category);await db.SaveChangesAsync();return(condo.Id,company.Id,category.Id,user.Id);}
    private static async Task<Guid> CreateWithAnnualIdentifier((Guid Condo,Guid Company,Guid Category,Guid User) seed)
    {await using var db=Db();await using var tx=await db.Database.BeginTransactionAsync();var generator=new ManagementCompanyRequestIdentifierService(db,TimeProvider.System);var(fid,created)=await generator.NextAsync(default);var request=new ManagementCompanyRequest(seed.Condo,seed.Company,seed.Category,seed.User,ManagementCompanyRequestType.GeneralQuestion,fid,created);db.ManagementCompanyRequests.Add(request);await db.SaveChangesAsync();await tx.CommitAsync();return request.Id;}
    private static async Task<bool> InteractSafe(Guid id,ManagementCompanyRequestActor actor,string content)
    {
        await using var db=Db();var access=new ManagementCompanyRequestAccessService(db);var service=new ManagementCompanyRequestService(db,access,null!);
        var request=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);
        try{await service.InteractAsync(request,actor,content,null,null,default);return true;}catch(ConflictAppException){return false;}
    }
    private static async Task<bool> TransitionSafe(Guid id,ManagementCompanyRequestActor actor,ManagementCompanyRequestStatus next)
    {
        await using var db=Db();var access=new ManagementCompanyRequestAccessService(db);var service=new ManagementCompanyRequestService(db,access,null!);
        var request=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);
        try{await service.TransitionAsync(request,actor,next,next==ManagementCompanyRequestStatus.WaitingManager?"Preciso de mais informações":null,default);return true;}catch(ConflictAppException){return false;}
    }
    private static async Task AcknowledgeViaService(Guid id,ManagementCompanyRequestActor actor)
    {
        await using var db=Db();var access=new ManagementCompanyRequestAccessService(db);var service=new ManagementCompanyRequestService(db,access,null!);
        var request=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==id);
        await service.AcknowledgeAsync(request,actor,default);
    }
    private static AppDbContext Db(){var o=new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options;return new(o);}
}
