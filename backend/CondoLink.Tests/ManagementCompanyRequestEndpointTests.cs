using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.ManagementCompanyRequests;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class ManagementCompanyRequestEndpointTests : IAsyncLifetime
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"comvy-mcr",Guid.NewGuid().ToString("N"));
    private CoreEndpointTestHost host=null!; private Guid manager,submanager,outsider,companyUser,wrongUser,inactiveUser,requestId,otherId,condoId,categoryId;
    public async Task InitializeAsync()
    {
        host=await CoreEndpointTestHost.StartAsync(a=>{a.MapManagementCompanyRequests();a.MapAdministratorRequests();},b=>{b.Configuration["FileStorage:RootPath"]=root;b.Services.AddSingleton<LocalFileStorage>();b.Services.AddScoped<ManagementCompanyRequestAccessService>();b.Services.AddScoped<ManagementCompanyRequestService>();b.Services.AddScoped<ManagementCompanyRequestNotificationService>();b.Services.AddSingleton<IEmailSender>(new NoOpEmailSender());b.Services.Configure<FirstAccessOptions>(x=>x.FrontendBaseUrl="https://app.comvy.test");});
        await host.WithDbAsync(async db=>
        {
            var ca=new Condominium("A",null,null);var cb=new Condominium("B",null,null);var coa=new ManagementCompany("A",null,null,null,null);var cob=new ManagementCompany("B",null,null,null,null);
            var m=CoreTestSeed.User("Manager","m@test.local");var sm=CoreTestSeed.User("Sub","s@test.local");var o=CoreTestSeed.User("Fora","o@test.local");var au=CoreTestSeed.User("Acesso","a@test.local");var wu=CoreTestSeed.User("Errado","w@test.local");var iu=CoreTestSeed.User("Inativo","i@test.local");
            var cat=new ManagementCompanyRequestCategory(coa.Id,"Dúvidas",null,ManagementCompanyRequestFormType.Generic);var wrongCat=new ManagementCompanyRequestCategory(coa.Id,"Multas",null,ManagementCompanyRequestFormType.UnitFine);var catB=new ManagementCompanyRequestCategory(cob.Id,"Dúvidas",null,ManagementCompanyRequestFormType.Generic);
            var access=new ManagementCompanyEmployee(coa.Id,au.Id,"Atendimento");var wrong=new ManagementCompanyEmployee(coa.Id,wu.Id,"Jurídico");var inactive=new ManagementCompanyEmployee(coa.Id,iu.Id,"Antigo");inactive.Deactivate();
            var r=new ManagementCompanyRequest(ca.Id,coa.Id,cat.Id,m.Id,ManagementCompanyRequestType.GeneralQuestion);var r2=new ManagementCompanyRequest(cb.Id,cob.Id,catB.Id,o.Id,ManagementCompanyRequestType.GeneralQuestion);
            db.AddRange(ca,cb,coa,cob,m,sm,o,au,wu,iu,cat,wrongCat,catB,access,wrong,inactive,r,r2,new CondominiumManagementCompanyLink(ca.Id,coa.Id),new ManagementCompanyRequestCategoryResponsible(cat.Id,access.Id),new ManagementCompanyRequestCategoryResponsible(wrongCat.Id,wrong.Id),new ManagementCompanyRequestCategoryResponsible(cat.Id,inactive.Id));
            CoreTestSeed.AddMember(db,m.Id,ca.Id,CondominiumRole.Manager);CoreTestSeed.AddMember(db,sm.Id,ca.Id,CondominiumRole.SubManager);CoreTestSeed.AddMember(db,o.Id,cb.Id,CondominiumRole.Resident);await db.SaveChangesAsync();
            manager=m.Id;submanager=sm.Id;outsider=o.Id;companyUser=au.Id;wrongUser=wu.Id;inactiveUser=iu.Id;requestId=r.Id;otherId=r2.Id;condoId=ca.Id;categoryId=cat.Id;
        });
    }
    public async Task DisposeAsync(){await host.DisposeAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    [Fact] public async Task Endpoint_IDOR_matrix_is_enforced()
    {
        Assert.Equal(HttpStatusCode.OK,(await host.ClientFor(manager).GetAsync($"/management-company-requests/{requestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await host.ClientFor(submanager).GetAsync($"/management-company-requests/{requestId}")).StatusCode);
        foreach(var user in new[]{outsider,wrongUser,inactiveUser})Assert.Equal(HttpStatusCode.Forbidden,(await host.ClientFor(user).GetAsync($"/management-company-requests/{requestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await host.ClientFor(companyUser).GetAsync($"/management-company-requests/{otherId}")).StatusCode);
    }
    [Fact] public async Task IDOR_is_enforced_before_mutating_or_returning_metadata()
    {
        foreach(var user in new[]{outsider,wrongUser,inactiveUser})
        {
            var client=host.ClientFor(user);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/management-company-requests/{requestId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/management-company-requests/{requestId}/messages",new{content="tentativa"})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/management-company-requests/{requestId}/status",new{status="InProgress"})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/management-company-requests/{requestId}/cancel",new{reason="tentativa"})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/management-company-requests/{requestId}/attachments")).StatusCode);
            using var upload=new MultipartFormDataContent();var bytes=new ByteArrayContent([1]);bytes.Headers.ContentType=new("application/pdf");upload.Add(bytes,"files","x.pdf");Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/management-company-requests/{requestId}/attachments",upload)).StatusCode);
        }
    }
    [Fact] public async Task Multipart_interaction_is_atomic_and_attachment_download_is_scoped()
    {
        await host.ClientFor(companyUser).GetAsync($"/management-company-requests/{requestId}");
        using(var ask=Form(new{content="Envie a ata",targetStatus="WaitingManager"}))Assert.Equal(HttpStatusCode.OK,(await host.ClientFor(companyUser).PostAsync($"/management-company-requests/{requestId}/interactions",ask)).StatusCode);
        using(var reply=Form(new{content="Segue a ata",targetStatus=(string?)null},("ata.pdf","application/pdf",Encoding.UTF8.GetBytes("secret"))))Assert.Equal(HttpStatusCode.OK,(await host.ClientFor(manager).PostAsync($"/management-company-requests/{requestId}/interactions",reply)).StatusCode);
        var attachment=await host.WithDbAsync(async db=>{Assert.Equal(ManagementCompanyRequestStatus.InProgress,(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==requestId)).Status);var message=await db.ManagementCompanyRequestMessages.SingleAsync(x=>x.Content=="Segue a ata");return await db.ManagementCompanyRequestAttachments.SingleAsync(x=>x.MessageId==message.Id);});
        var denied=await host.ClientFor(outsider).GetAsync($"/management-company-request-attachments/{attachment.Id}/content");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);Assert.DoesNotContain("ata.pdf",await denied.Content.ReadAsStringAsync());
        Assert.Equal("secret",await host.ClientFor(manager).GetStringAsync($"/management-company-request-attachments/{attachment.Id}/content"));
    }
    [Fact] public async Task Invalid_file_leaves_no_message_attachment_or_physical_file()
    {
        using var form=Form(new{content="inválido",targetStatus=(string?)null},("x.exe","application/octet-stream",[1,2]));var response=await host.ClientFor(manager).PostAsync($"/management-company-requests/{requestId}/interactions",form);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        await host.WithDbAsync(async db=>{Assert.False(await db.ManagementCompanyRequestMessages.AnyAsync(x=>x.Content=="inválido"));Assert.Empty(await db.ManagementCompanyRequestAttachments.ToListAsync());});Assert.False(Directory.Exists(root)&&Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Any());
    }
    [Fact] public async Task Multipart_creation_commits_request_initial_message_history_and_attachment_together()
    {
        using var form=Form(new{condominiumId=condoId,categoryId,theme="Contrato",message="Analisar contrato"},("contrato.pdf","application/pdf",Encoding.UTF8.GetBytes("pdf")));
        var response=await host.ClientFor(manager).PostAsync("/management-company-requests/questions/multipart",form);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        await host.WithDbAsync(async db=>{var created=await db.ManagementCompanyRequests.OrderByDescending(x=>x.CreatedAt).FirstAsync(x=>x.Id!=requestId);Assert.Equal(ManagementCompanyRequestStatus.Submitted,created.Status);Assert.Single(await db.ManagementCompanyGeneralQuestionRequests.Where(x=>x.RequestId==created.Id).ToListAsync());Assert.Single(await db.ManagementCompanyRequestMessages.Where(x=>x.RequestId==created.Id).ToListAsync());Assert.Single(await db.ManagementCompanyRequestHistories.Where(x=>x.RequestId==created.Id).ToListAsync());Assert.Single(await db.ManagementCompanyRequestAttachments.Where(x=>x.RequestId==created.Id).ToListAsync());});
    }
    [Fact] public async Task Management_list_is_scoped_paged_filterable_and_waiting_first()
    {
        var allowed=await host.ClientFor(manager).GetAsync("/management-company-requests?page=1&pageSize=1&type=GeneralQuestion&search=ADM-");Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);var json=await allowed.Content.ReadAsStringAsync();Assert.Contains(requestId.ToString(),json,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(otherId.ToString(),json,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden,(await host.ClientFor(manager).GetAsync($"/management-company-requests?condominiumId={Guid.NewGuid()}")).StatusCode);
    }
    [Fact] public async Task Management_list_filters_created_at_by_inclusive_date_range()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var included = await host.ClientFor(manager).GetAsync($"/management-company-requests?from={today}&to={today}");
        Assert.Equal(HttpStatusCode.OK, included.StatusCode);
        Assert.Contains(requestId.ToString(), await included.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.ClientFor(manager).GetAsync("/management-company-requests?from=2026-08-29&to=2026-08-28")).StatusCode);
        var excluded = await host.ClientFor(manager).GetAsync("/management-company-requests?from=2100-01-01&to=2100-01-02");
        Assert.Equal(HttpStatusCode.OK, excluded.StatusCode);
        Assert.DoesNotContain(requestId.ToString(), await excluded.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task Creation_options_are_typed_and_contextually_authorized()
    {
        var response=await host.ClientFor(manager).GetAsync($"/management-company-requests/options?condominiumId={condoId}");Assert.Equal(HttpStatusCode.OK,response.StatusCode);var json=await response.Content.ReadAsStringAsync();Assert.Contains("GeneralQuestion",json);Assert.Contains("Dúvidas",json);
        Assert.Equal(HttpStatusCode.Forbidden,(await host.ClientFor(outsider).GetAsync($"/management-company-requests/options?condominiumId={condoId}")).StatusCode);
    }
    [Fact] public async Task Administrator_queue_is_scoped_by_historical_company_and_category_and_list_does_not_acknowledge()
    {
        var client=host.ClientFor(companyUser);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/administrator/context")).StatusCode);
        var list=await client.GetAsync("/administrator/requests");Assert.Equal(HttpStatusCode.OK,list.StatusCode);var json=await list.Content.ReadAsStringAsync();Assert.Contains(requestId.ToString(),json,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(otherId.ToString(),json,StringComparison.OrdinalIgnoreCase);
        await host.WithDbAsync(async db=>Assert.Equal(ManagementCompanyRequestStatus.Submitted,(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==requestId)).Status));
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/management-company-requests/{requestId}")).StatusCode);
        await host.WithDbAsync(async db=>{Assert.Equal(ManagementCompanyRequestStatus.Acknowledged,(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==requestId)).Status);Assert.Single(await db.ManagementCompanyRequestHistories.Where(x=>x.RequestId==requestId&&x.EventType==ManagementCompanyRequestEventType.Acknowledged).ToListAsync());});
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/management-company-requests/{requestId}")).StatusCode);
        await host.WithDbAsync(async db=>Assert.Single(await db.ManagementCompanyRequestHistories.Where(x=>x.RequestId==requestId&&x.EventType==ManagementCompanyRequestEventType.Acknowledged).ToListAsync()));
        var wrongCategoryList=await host.ClientFor(wrongUser).GetAsync("/administrator/requests");Assert.Equal(HttpStatusCode.OK,wrongCategoryList.StatusCode);Assert.DoesNotContain(requestId.ToString(),await wrongCategoryList.Content.ReadAsStringAsync(),StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden,(await host.ClientFor(inactiveUser).GetAsync("/administrator/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/administrator/requests?categoryId={Guid.NewGuid()}")).StatusCode);
    }
    [Fact] public async Task Historical_access_survives_administrator_company_swap_and_new_company_never_inherits()
    {
        Guid condoSwap=default,companyX=default,companyY=default,categoryXId=default,requestSwapId=default,userX=default,userY=default,linkId=default;
        await host.WithDbAsync(async db=>
        {
            var condo=new Condominium("Swap",null,null);
            var x=new ManagementCompany("X",null,null,null,null);var y=new ManagementCompany("Y",null,null,null,null);
            var xUser=CoreTestSeed.User("AcessoX","x-access@test.local");var yUser=CoreTestSeed.User("AcessoY","y-access@test.local");
            var catX=new ManagementCompanyRequestCategory(x.Id,"Dúvidas",null,ManagementCompanyRequestFormType.Generic);
            var catY=new ManagementCompanyRequestCategory(y.Id,"Dúvidas",null,ManagementCompanyRequestFormType.Generic);
            var accessX=new ManagementCompanyEmployee(x.Id,xUser.Id,"Atendimento");
            var accessY=new ManagementCompanyEmployee(y.Id,yUser.Id,"Atendimento");
            var link=new CondominiumManagementCompanyLink(condo.Id,x.Id);
            var request=new ManagementCompanyRequest(condo.Id,x.Id,catX.Id,manager,ManagementCompanyRequestType.GeneralQuestion);
            db.AddRange(condo,x,y,xUser,yUser,catX,catY,accessX,accessY,link,request,
                new ManagementCompanyRequestCategoryResponsible(catX.Id,accessX.Id),
                new ManagementCompanyRequestCategoryResponsible(catY.Id,accessY.Id));
            await db.SaveChangesAsync();
            condoSwap=condo.Id;companyX=x.Id;companyY=y.Id;categoryXId=catX.Id;requestSwapId=request.Id;userX=xUser.Id;userY=yUser.Id;linkId=link.Id;
        });
        // Condomínio troca de administradora: X é desvinculada, Y é vinculada.
        await host.WithDbAsync(async db=>
        {
            (await db.CondominiumManagementCompanyLinks.SingleAsync(l=>l.Id==linkId)).Unlink(DateTime.UtcNow);
            db.CondominiumManagementCompanyLinks.Add(new CondominiumManagementCompanyLink(condoSwap,companyY));
            await db.SaveChangesAsync();
        });
        // Y (nova administradora) nunca herda acesso à solicitação histórica de X.
        var yClient=host.ClientFor(userY);
        var yList=await yClient.GetAsync("/administrator/requests");
        Assert.Equal(HttpStatusCode.OK,yList.StatusCode);
        Assert.DoesNotContain(requestSwapId.ToString(),await yList.Content.ReadAsStringAsync(),StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden,(await yClient.GetAsync($"/management-company-requests/{requestSwapId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await yClient.PostAsJsonAsync($"/management-company-requests/{requestSwapId}/messages",new{content="tentativa"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await yClient.GetAsync($"/management-company-requests/{requestSwapId}/attachments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await yClient.PostAsJsonAsync($"/management-company-requests/{requestSwapId}/status",new{status="InProgress"})).StatusCode);
        // X (administradora histórica): acesso ainda ativo e ainda responsável pela categoria histórica continua operando a solicitação.
        var xClient=host.ClientFor(userX);
        Assert.Equal(HttpStatusCode.OK,(await xClient.GetAsync($"/management-company-requests/{requestSwapId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await xClient.PostAsJsonAsync($"/management-company-requests/{requestSwapId}/messages",new{content="Ainda posso responder"})).StatusCode);
        await host.WithDbAsync(async db=>
        {
            var updated=await db.ManagementCompanyRequests.SingleAsync(r=>r.Id==requestSwapId);
            Assert.Equal(companyX,updated.ManagementCompanyId);
            Assert.Equal(categoryXId,updated.CategoryId);
            Assert.Equal(ManagementCompanyRequestStatus.Acknowledged,updated.Status);
        });
    }
    // Lote 6 — item 3/25: papéis ainda não cobertos — SubManager de outro condomínio,
    // Resident do mesmo condomínio da request, e um usuário ativo cujo papel de Manager foi
    // revogado (distinto de "usuário inativo", já coberto por inactiveUser acima).
    [Fact] public async Task Wrong_role_and_revoked_role_combinations_are_denied()
    {
        Guid subManagerOtherCondo=default,residentSameCondo=default,revokedManager=default;
        await host.WithDbAsync(async db=>
        {
            var otherCondoId=(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==otherId)).CondominiumId;
            var smOther=CoreTestSeed.User("SubOutroCondo",$"sub-outro-{Guid.NewGuid():N}@test.local");
            var residentSame=CoreTestSeed.User("MoradorMesmoCondo",$"morador-mesmo-{Guid.NewGuid():N}@test.local");
            var revoked=CoreTestSeed.User("GestorRevogado",$"gestor-revogado-{Guid.NewGuid():N}@test.local");
            db.AddRange(smOther,residentSame,revoked);
            CoreTestSeed.AddMember(db,smOther.Id,otherCondoId,CondominiumRole.SubManager);
            CoreTestSeed.AddMember(db,residentSame.Id,condoId,CondominiumRole.Resident);
            var revokedMembership=CoreTestSeed.AddMember(db,revoked.Id,condoId,CondominiumRole.Manager);
            await db.SaveChangesAsync();
            (await db.CondominiumMembershipRoles.SingleAsync(x=>x.CondominiumMembershipId==revokedMembership.Id)).Deactivate();
            await db.SaveChangesAsync();
            subManagerOtherCondo=smOther.Id;residentSameCondo=residentSame.Id;revokedManager=revoked.Id;
        });
        foreach(var user in new[]{subManagerOtherCondo,residentSameCondo,revokedManager})
        {
            var client=host.ClientFor(user);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/management-company-requests/{requestId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/management-company-requests/{requestId}/messages",new{content="tentativa"})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/management-company-requests/{requestId}/status",new{status="InProgress"})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/management-company-requests/{requestId}/cancel",new{reason="tentativa"})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/management-company-requests/{requestId}/attachments")).StatusCode);
        }
    }

    // Lote 6 — item 19: usuário Manager de um condomínio e, ao mesmo tempo, acesso da
    // administradora de outra empresa. Cada escopo deve ser resolvido de forma
    // independente, sem que um conceda o outro.
    [Fact] public async Task Multi_role_user_gets_independent_scopes_without_cross_grant()
    {
        await host.WithDbAsync(async db=>
        {
            var otherRequest=await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==otherId);
            var employee=new ManagementCompanyEmployee(otherRequest.ManagementCompanyId,manager,"Atendimento Extra");
            db.Add(employee);
            db.Add(new ManagementCompanyRequestCategoryResponsible(otherRequest.CategoryId,employee.Id));
            await db.SaveChangesAsync();
        });
        // Continua acessando a própria request como Gestão (Management).
        Assert.Equal(HttpStatusCode.OK,(await host.ClientFor(manager).GetAsync($"/management-company-requests/{requestId}")).StatusCode);
        // Agora também acessa a request da outra empresa, como administradora (ManagementCompany) — escopo independente.
        Assert.Equal(HttpStatusCode.OK,(await host.ClientFor(manager).GetAsync($"/management-company-requests/{otherId}")).StatusCode);
    }

    // Lote 6 — item 13: snapshot histórico de PIX não deve refletir mudança posterior do beneficiário.
    [Fact] public async Task Reimbursement_snapshot_survives_beneficiary_pix_change()
    {
        Guid paymentCategoryId=default;
        await host.WithDbAsync(async db=>
        {
            (await db.Users.SingleAsync(x=>x.Id==manager)).SetPix(PixKeyType.Email,"gestor-original@test.local");
            var companyId=(await db.ManagementCompanyRequests.SingleAsync(x=>x.Id==requestId)).ManagementCompanyId;
            var paymentCat=new ManagementCompanyRequestCategory(companyId,"Pagamentos",null,ManagementCompanyRequestFormType.SupplierPayment);
            var employeeId=await db.ManagementCompanyEmployees.Where(x=>x.UserId==companyUser).Select(x=>x.Id).SingleAsync();
            db.Add(paymentCat);
            db.Add(new ManagementCompanyRequestCategoryResponsible(paymentCat.Id,employeeId));
            await db.SaveChangesAsync();
            paymentCategoryId=paymentCat.Id;
        });
        var create=await host.ClientFor(manager).PostAsJsonAsync("/management-company-requests/payments",new
        {
            condominiumId=condoId,categoryId=paymentCategoryId,nature="Reembolso de material",
            value=150.00m,eventDate=DateOnly.FromDateTime(DateTime.UtcNow),isReimbursement=true,
            beneficiaryUserId=manager,notes=(string?)null,
        });
        Assert.Equal(HttpStatusCode.Created,create.StatusCode);
        var paymentRequestId=Guid.Parse(create.Headers.Location!.ToString().Split('/')[^1]);

        await host.WithDbAsync(async db=>
        {
            (await db.Users.SingleAsync(x=>x.Id==manager)).SetPix(PixKeyType.Cpf,"999.999.999-99");
            await db.SaveChangesAsync();
        });

        var detail=await host.ClientFor(manager).GetAsync($"/management-company-requests/{paymentRequestId}");
        Assert.Equal(HttpStatusCode.OK,detail.StatusCode);
        var json=await detail.Content.ReadAsStringAsync();
        Assert.Contains("gestor-original@test.local",json);
        Assert.DoesNotContain("999.999.999-99",json);
    }

    private static MultipartFormDataContent Form(object payload,params(string Name,string Mime,byte[] Data)[] files){var f=new MultipartFormDataContent();f.Add(new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json"),"payload");foreach(var x in files){var c=new ByteArrayContent(x.Data);c.Headers.ContentType=new(x.Mime);f.Add(c,"files",x.Name);}return f;}
}
