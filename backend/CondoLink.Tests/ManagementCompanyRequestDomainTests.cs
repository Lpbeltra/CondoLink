using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

namespace CondoLink.Tests;

public sealed class ManagementCompanyRequestDomainTests
{
    public static TheoryData<ManagementCompanyRequestStatus,ManagementCompanyRequestStatus,bool> Matrix => new()
    {
        {ManagementCompanyRequestStatus.Submitted,ManagementCompanyRequestStatus.Acknowledged,true},
        {ManagementCompanyRequestStatus.Submitted,ManagementCompanyRequestStatus.InProgress,false},
        {ManagementCompanyRequestStatus.Acknowledged,ManagementCompanyRequestStatus.InProgress,true},
        {ManagementCompanyRequestStatus.Acknowledged,ManagementCompanyRequestStatus.WaitingManager,true},
        {ManagementCompanyRequestStatus.InProgress,ManagementCompanyRequestStatus.WaitingManager,true},
        {ManagementCompanyRequestStatus.InProgress,ManagementCompanyRequestStatus.Completed,true},
        {ManagementCompanyRequestStatus.WaitingManager,ManagementCompanyRequestStatus.InProgress,true},
        {ManagementCompanyRequestStatus.WaitingManager,ManagementCompanyRequestStatus.Completed,false},
        {ManagementCompanyRequestStatus.Completed,ManagementCompanyRequestStatus.InProgress,false},
        {ManagementCompanyRequestStatus.Cancelled,ManagementCompanyRequestStatus.InProgress,false}
    };

    [Theory,MemberData(nameof(Matrix))]
    public void Transition_matrix_is_explicit(ManagementCompanyRequestStatus from,ManagementCompanyRequestStatus to,bool expected)
        =>Assert.Equal(expected,ManagementCompanyRequest.CanTransition(from,to));

    [Fact]
    public void Acknowledgement_is_idempotent_and_terminal_states_are_read_only()
    {
        var r=New();var actor=Guid.NewGuid();var first=DateTime.UtcNow;
        r.Acknowledge(actor,first);var stamp=r.ConcurrencyStamp;r.Acknowledge(Guid.NewGuid(),first.AddMinutes(1));
        Assert.Equal(actor,r.AcknowledgedByUserId);Assert.Equal(stamp,r.ConcurrencyStamp);
        r.TransitionTo(ManagementCompanyRequestStatus.InProgress,first.AddMinutes(2));r.Complete(actor,first.AddMinutes(3));
        Assert.True(r.IsTerminal);Assert.Throws<InvalidOperationException>(()=>r.TransitionTo(ManagementCompanyRequestStatus.WaitingManager,DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(()=>r.Cancel(actor,"motivo",DateTime.UtcNow));
    }

    [Fact]
    public void Cancellation_requires_reason_and_records_snapshot()
    {
        var r=New();var actor=Guid.NewGuid();var at=DateTime.UtcNow;
        Assert.Throws<ArgumentException>(()=>r.Cancel(actor," ",at));
        r.Cancel(actor,"  informação duplicada  ",at);
        Assert.Equal(ManagementCompanyRequestStatus.Cancelled,r.Status);Assert.Equal("informação duplicada",r.CancellationReason);Assert.Equal(actor,r.CancelledByUserId);Assert.Equal(at,r.CancelledAt);
    }

    [Fact]
    public void Type_details_enforce_money_and_historical_pix_snapshot()
    {
        var id=Guid.NewGuid();
        var fine=new ManagementCompanyFineRequest(id,Guid.NewGuid(),"Ruído","Ocorrência",new DateOnly(2026,8,28),850.37m,false);
        Assert.Equal(850.37m,fine.Value);
        Assert.Throws<ArgumentException>(()=>new ManagementCompanyFineRequest(id,Guid.NewGuid(),"Ruído","Ocorrência",DateOnly.FromDateTime(DateTime.UtcNow),null,false));
        var beneficiary=Guid.NewGuid();var payment=new ManagementCompanyPaymentRequest(id,"Reembolso",123.45m,new DateOnly(2026,8,28),true,null,beneficiary,"Maria",PixKeyType.Email,"maria@example.com");
        Assert.Equal("maria@example.com",payment.PixKey);Assert.Equal(beneficiary,payment.BeneficiaryUserId);
        Assert.Throws<ArgumentException>(()=>new ManagementCompanyPaymentRequest(id,"Reembolso",10m,DateOnly.FromDateTime(DateTime.UtcNow),true,null,null,null,null,null));
    }

    [Fact]
    public void Friendly_identifier_is_stable_prefixed_and_not_the_primary_key()
    {var r=New();Assert.StartsWith("ADM-",r.FriendlyIdentifier);Assert.Equal(16,r.FriendlyIdentifier.Length);Assert.DoesNotContain(r.Id.ToString(),r.FriendlyIdentifier);}

    private static ManagementCompanyRequest New()=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),ManagementCompanyRequestType.Fine);
}
