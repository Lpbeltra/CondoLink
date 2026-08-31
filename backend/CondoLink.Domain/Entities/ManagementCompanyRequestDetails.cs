using CondoLink.Domain.Enums;
namespace CondoLink.Domain.Entities;

public sealed class ManagementCompanyFineRequest
{
    private ManagementCompanyFineRequest() { }
    public ManagementCompanyFineRequest(Guid requestId, Guid unitId, string nature, string description,
        DateOnly occurrenceDate, decimal? value, bool valueNotDefined)
    {
        if (requestId == Guid.Empty || unitId == Guid.Empty) throw new ArgumentException("Request and unit are required.");
        if (string.IsNullOrWhiteSpace(nature) || nature.Trim().Length > 200) throw new ArgumentException("Fine nature is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 4000) throw new ArgumentException("Fine description is required and must not exceed 4000 characters.");
        if (valueNotDefined == value.HasValue || value is < 0) throw new ArgumentException("Provide a non-negative value or explicitly mark it as not defined.");
        RequestId=requestId; UnitId=unitId; Nature=nature.Trim(); Description=description.Trim(); OccurrenceDate=occurrenceDate; Value=value; ValueNotDefined=valueNotDefined;
    }
    public Guid RequestId { get; private set; } public Guid UnitId { get; private set; }
    public string Nature { get; private set; }=null!; public string Description { get; private set; }=null!;
    public DateOnly OccurrenceDate { get; private set; } public decimal? Value { get; private set; } public bool ValueNotDefined { get; private set; }
}

public sealed class ManagementCompanyPaymentRequest
{
    private ManagementCompanyPaymentRequest() { }
    public ManagementCompanyPaymentRequest(Guid requestId, string nature, decimal value, DateOnly eventDate,
        DateOnly? dueDate, bool isReimbursement, string? notes, Guid? beneficiaryUserId, string? beneficiaryName, PixKeyType? pixKeyType, string? pixKey,
        string? thirdPartyIdentification, ManagementCompanyPaymentThirdPartyForm? thirdPartyForm, string? thirdPartyPixKey,
        string? thirdPartyBank, string? thirdPartyAgency, string? thirdPartyAccount)
    {
        if (requestId==Guid.Empty) throw new ArgumentException("Request is required.");
        if (string.IsNullOrWhiteSpace(nature) || nature.Trim().Length>500) throw new ArgumentException("Payment nature is required and must not exceed 500 characters.");
        if (value < 0) throw new ArgumentException("Payment value cannot be negative.");
        if (dueDate is null) throw new ArgumentException("Due date is required.");
        if (notes?.Trim().Length>4000) throw new ArgumentException("Notes must not exceed 4000 characters.");
        if (isReimbursement && (beneficiaryUserId is null || string.IsNullOrWhiteSpace(beneficiaryName) || pixKeyType is null || string.IsNullOrWhiteSpace(pixKey)))
            throw new ArgumentException("A reimbursement requires beneficiary and PIX snapshot.");
        if (!isReimbursement && (beneficiaryUserId is not null || beneficiaryName is not null || pixKeyType is not null || pixKey is not null))
            throw new ArgumentException("Non-reimbursement payments cannot contain beneficiary data.");
        if (isReimbursement && (!string.IsNullOrWhiteSpace(thirdPartyIdentification) || thirdPartyForm is not null || !string.IsNullOrWhiteSpace(thirdPartyPixKey) || !string.IsNullOrWhiteSpace(thirdPartyBank) || !string.IsNullOrWhiteSpace(thirdPartyAgency) || !string.IsNullOrWhiteSpace(thirdPartyAccount)))
            throw new ArgumentException("Reimbursement cannot contain third-party data.");
        if (!isReimbursement)
        {
            if (string.IsNullOrWhiteSpace(thirdPartyIdentification)) throw new ArgumentException("Third-party identification is required.");
            if (thirdPartyForm is null) throw new ArgumentException("Third-party payment form is required.");
            if (thirdPartyForm == ManagementCompanyPaymentThirdPartyForm.Pix)
            {
                if (string.IsNullOrWhiteSpace(thirdPartyPixKey)) throw new ArgumentException("PIX key is required for third-party PIX payments.");
                if (!string.IsNullOrWhiteSpace(thirdPartyBank) || !string.IsNullOrWhiteSpace(thirdPartyAgency) || !string.IsNullOrWhiteSpace(thirdPartyAccount))
                    throw new ArgumentException("PIX payments cannot include bank account data.");
            }
            if (thirdPartyForm == ManagementCompanyPaymentThirdPartyForm.Boleto)
            {
                if (!string.IsNullOrWhiteSpace(thirdPartyPixKey)) throw new ArgumentException("Boleto payments cannot include PIX data.");
                if (!string.IsNullOrWhiteSpace(thirdPartyBank) || !string.IsNullOrWhiteSpace(thirdPartyAgency) || !string.IsNullOrWhiteSpace(thirdPartyAccount))
                    throw new ArgumentException("Boleto payments cannot include bank account data.");
            }
            if (thirdPartyForm == ManagementCompanyPaymentThirdPartyForm.DepositAccount && (string.IsNullOrWhiteSpace(thirdPartyBank) || string.IsNullOrWhiteSpace(thirdPartyAgency) || string.IsNullOrWhiteSpace(thirdPartyAccount)))
                throw new ArgumentException("Bank, branch and account are required for deposit account payments.");
        }
        RequestId=requestId; Nature=nature.Trim(); Value=value; EventDate=eventDate; DueDate=dueDate; IsReimbursement=isReimbursement;
        Notes=Normalize(notes); BeneficiaryUserId=beneficiaryUserId; BeneficiaryName=Normalize(beneficiaryName); PixKeyType=pixKeyType; PixKey=Normalize(pixKey);
        ThirdPartyIdentification=Normalize(thirdPartyIdentification); ThirdPartyForm=thirdPartyForm; ThirdPartyPixKey=Normalize(thirdPartyPixKey); ThirdPartyBank=Normalize(thirdPartyBank); ThirdPartyAgency=Normalize(thirdPartyAgency); ThirdPartyAccount=Normalize(thirdPartyAccount);
    }
    public Guid RequestId { get;private set;} public string Nature {get;private set;}=null!; public decimal Value {get;private set;}
    public DateOnly EventDate {get;private set;} public DateOnly? DueDate {get;private set;} public bool IsReimbursement {get;private set;} public string? Notes {get;private set;}
    public Guid? BeneficiaryUserId {get;private set;} public string? BeneficiaryName {get;private set;} public PixKeyType? PixKeyType {get;private set;} public string? PixKey {get;private set;}
    public string? ThirdPartyIdentification {get;private set;} public ManagementCompanyPaymentThirdPartyForm? ThirdPartyForm {get;private set;} public string? ThirdPartyPixKey {get;private set;} public string? ThirdPartyBank {get;private set;} public string? ThirdPartyAgency {get;private set;} public string? ThirdPartyAccount {get;private set;}
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value)?null:value.Trim();
}

public sealed class ManagementCompanyGeneralQuestionRequest
{
    private ManagementCompanyGeneralQuestionRequest() { }
    public ManagementCompanyGeneralQuestionRequest(Guid requestId,string theme)
    { if(requestId==Guid.Empty)throw new ArgumentException("Request is required."); if(string.IsNullOrWhiteSpace(theme)||theme.Trim().Length>200)throw new ArgumentException("Theme is required and must not exceed 200 characters."); RequestId=requestId;Theme=theme.Trim(); }
    public Guid RequestId {get;private set;} public string Theme {get;private set;}=null!;
}
