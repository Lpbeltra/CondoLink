using CondoLink.Infrastructure.Identity;

namespace CondoLink.Tests;

public sealed class ApplicationUserPhoneNumberTests
{
    [Fact]
    public void Valid_phone_is_trimmed_and_normalized_without_confirmation()
    {
        var user = new ApplicationUser(
            "Maria", "maria.phone@example.com", "  (44) 99999-9999  ");

        Assert.Equal("(44) 99999-9999", user.PhoneNumber);
        Assert.Equal("+5544999999999", user.NormalizedPhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
    }

    [Fact]
    public void Invalid_phone_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ApplicationUser(
            "Maria", "invalid.phone@example.com", "WhatsApp"));
    }

    [Fact]
    public void Empty_phone_is_optional()
    {
        var user = new ApplicationUser(
            "Maria", "optional.phone@example.com", "  ");

        Assert.Null(user.PhoneNumber);
        Assert.Null(user.NormalizedPhoneNumber);
    }

    [Fact]
    public void Updating_and_removing_phone_keeps_both_values_synchronized()
    {
        var user = new ApplicationUser(
            "Maria", "updated.phone@example.com", "(11) 99999-0001");

        user.Update("Maria", "+55 21 98888-0002");
        Assert.Equal("+55 21 98888-0002", user.PhoneNumber);
        Assert.Equal("+5521988880002", user.NormalizedPhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);

        user.Update("Maria", null);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.NormalizedPhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
    }

    [Fact]
    public void Formatting_only_change_preserves_confirmation_but_new_phone_clears_it()
    {
        var user = new ApplicationUser(
            "Maria", "format.phone@example.com", "(11) 99999-0001");
        user.ConfirmPhoneNumber();

        user.Update("Maria", "+55 11 99999-0001");
        Assert.True(user.PhoneNumberConfirmed);

        user.Update("Maria", "(21) 98888-0002");
        Assert.False(user.PhoneNumberConfirmed);
    }
}
