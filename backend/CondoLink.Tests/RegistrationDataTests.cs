using CondoLink.Domain;
using CondoLink.Domain.Entities;

namespace CondoLink.Tests;

public sealed class RegistrationDataTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void Accepts_valid_cpf(string value) =>
        Assert.True(RegistrationData.IsValidCpf(value));

    [Theory]
    [InlineData("04.252.011/0001-10")]
    [InlineData("04252011000110")]
    public void Accepts_valid_cnpj(string value) =>
        Assert.True(RegistrationData.IsValidCnpj(value));

    [Theory]
    [InlineData("11111111111")]
    [InlineData("52998224724")]
    public void Rejects_invalid_cpf(string value) =>
        Assert.False(RegistrationData.IsValidCpf(value));

    [Theory]
    [InlineData("00000000000000")]
    [InlineData("04252011000111")]
    public void Rejects_invalid_cnpj(string value) =>
        Assert.False(RegistrationData.IsValidCnpj(value));

    [Fact]
    public void Condominium_normalizes_dependent_doorman_fields()
    {
        var condominium = new Condominium(
            "Condomínio", null, "04.252.011/0001-10", "Rua A", "São Paulo",
            "sp", false, true, "Central");
        Assert.False(condominium.IsRemoteDoorman);
        Assert.Null(condominium.DoormanContact);
        Assert.Equal("04252011000110", condominium.Cnpj);
        Assert.Equal("SP", condominium.State);
    }

    [Fact]
    public void Employee_requires_job_title_and_trims_it()
    {
        var employee = new ManagementCompanyEmployee(
            Guid.NewGuid(), Guid.NewGuid(), "  Financeiro  ");
        Assert.Equal("Financeiro", employee.JobTitle);
        Assert.Throws<ArgumentException>(() => new ManagementCompanyEmployee(
            Guid.NewGuid(), Guid.NewGuid(), " "));
    }
}
